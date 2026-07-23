using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using CodeBrix.Webcam.Capture;
using CodeBrix.Webcam.Devices;
using static CodeBrix.Webcam.Internal.Windows.MediaFoundationNativeMethods;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// The Windows-native <see cref="ICaptureBackend"/>: captures through Media Foundation's
/// source reader (no libvlc anywhere on this path), which decodes/converts every camera
/// format (YUY2, NV12, MJPEG, H.264) to BGRA via in-box MFTs; records MP4/H.264 with
/// in-file AAC audio through the sink writer; and monitors/captures microphone audio
/// via WASAPI. Recording never interrupts the preview — the recorder simply tees off
/// the capture loop, so there is no restart blink and no camera renegotiation.
/// <para/>
/// Per the <see cref="ICaptureBackend"/> contract, all control-surface calls arrive
/// serialized under the owning session's API lock. The capture thread reads
/// recording/monitoring state through volatile fields.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class MediaFoundationCaptureBackend : ICaptureBackend
{
    private static readonly object MfStartupLock = new object();
    private static bool _mfStarted;

    private readonly IImagingMediaDevice _device;
    private readonly WebcamSessionOptions _options;
    private readonly string _audioDeviceId;
    private readonly CaptureFrameEventArgs _frameArgs = new CaptureFrameEventArgs();

    // _reader/_source are created, used, and released ONLY on the capture thread: MF
    // objects are not apartment-agile, so they must live entirely in the MTA (see
    // MtaThread). Start() blocks on _startedSignal until setup succeeds or fails.
    private IMFSourceReader _reader;
    private IMFMediaSource _source;
    private Thread _captureThread;
    private readonly ManualResetEventSlim _startedSignal = new ManualResetEventSlim(false);
    private WebcamException _startupError;
    private volatile bool _stopRequested;
    private bool _running;

    // Negotiated stream format (written by Start/format-change on the capture thread).
    private uint _width;
    private uint _height;
    private int _sourceStride;
    private uint _frameRate = 30;

    // Packed top-down BGRA delivery buffer owned by the backend.
    private IntPtr _frameBuffer;
    private uint _frameBufferSize;

    private bool _monitorAudio;
    private int _monitorVolume = 100;
    private WasapiAudioMonitor _monitor;

    // Direct-recording tee, read by the capture thread.
    private volatile MfSinkWriterRecorder _directRecorder;
    private WasapiMicrophoneCapture _directAudioCapture;
    private readonly Stopwatch _recordClock = new Stopwatch();

    private bool _disposed;

    /// <summary>Creates the backend; Media Foundation is not touched until <see cref="Start"/>.</summary>
    internal MediaFoundationCaptureBackend(IImagingMediaDevice device, WebcamSessionOptions options,
        string audioDeviceId)
    {
        _device = device;
        _options = options;
        _audioDeviceId = audioDeviceId;
    }

    /// <summary>Releases the unmanaged frame buffer if <see cref="Dispose"/> was never called.</summary>
    ~MediaFoundationCaptureBackend() => FreeFrameBuffer();

    /// <inheritdoc/>
    public event EventHandler<CaptureFrameEventArgs> FrameReady;

    /// <inheritdoc/>
    public uint FrameWidth => _running ? _width : 0;

    /// <inheritdoc/>
    public uint FrameHeight => _running ? _height : 0;

    /// <inheritdoc/>
    public bool SupportsFramePathRecording => true;

    /// <inheritdoc/>
    public void EnsureFramePathRecordingSupported()
    {
        // The sink writer's H.264 encoder is an OS component — always present.
    }

    /// <inheritdoc/>
    public void SetAudioMonitoring(bool monitor, int volume)
    {
        _monitorAudio = monitor;
        _monitorVolume = Math.Clamp(volume, 0, 100);

        if (!_running)
        {
            return;
        }
        if (monitor && _monitor == null && _audioDeviceId != null)
        {
            StartMonitor();
        }
        else if (!monitor && _monitor != null)
        {
            _monitor.Dispose();
            _monitor = null;
        }
        if (_monitor != null)
        {
            _monitor.Volume = _monitorVolume;
        }
    }

    /// <inheritdoc/>
    public void Start()
    {
        EnsureMediaFoundationStarted();

        _stopRequested = false;
        _startupError = null;
        _startedSignal.Reset();
        var thread = new Thread(CaptureThreadMain)
        {
            IsBackground = true,
            Name = "CodeBrix.Webcam MF capture",
        };
        thread.Start();
        _startedSignal.Wait();
        if (_startupError != null)
        {
            thread.Join(TimeSpan.FromSeconds(5));
            throw _startupError;
        }
        _captureThread = thread;
        _running = true;

        if (_monitorAudio && _audioDeviceId != null)
        {
            StartMonitor();
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _stopRequested = true;
        _captureThread?.Join(TimeSpan.FromSeconds(5));
        _captureThread = null;
        _running = false;

        if (_monitor != null)
        {
            _monitor.Dispose();
            _monitor = null;
        }
        StopDirectRecorderIfAny();
    }

    /// <inheritdoc/>
    public void StartDirectRecording(WebcamRecordingOptions options, bool forceMjpeg)
    {
        if (forceMjpeg)
        {
            throw new WebcamException(
                "MJPEG passthrough recording (WebcamVideoFormat.MjpegAvi) is not available with "
                + "the Windows native capture engine; use WebcamVideoFormat.Mp4H264 — its H.264 "
                + "encoding is hardware-accelerated where available.");
        }
        if (_width == 0 || _height == 0)
        {
            throw new WebcamException("No video frames have arrived yet; try again in a moment.");
        }

        var recorder = new MfSinkWriterRecorder(System.IO.Path.GetFullPath(options.OutputPath),
            _width, _height, _frameRate, options.VideoBitrateKbps,
            includeAudio: _audioDeviceId != null);
        WasapiMicrophoneCapture audioCapture = null;
        try
        {
            recorder.Begin();
            if (_audioDeviceId != null)
            {
                try
                {
                    audioCapture = new WasapiMicrophoneCapture(_audioDeviceId,
                        (buffer, bytes) => recorder.WriteAudio(buffer, bytes));
                    audioCapture.Start();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(
                        $"CodeBrix.Webcam: recording audio capture failed to start; recording video only. {e.Message}");
                    audioCapture?.Dispose();
                    audioCapture = null;
                }
            }
        }
        catch
        {
            recorder.Dispose();
            throw;
        }

        _directAudioCapture = audioCapture;
        _recordClock.Restart();
        _directRecorder = recorder; // publish last: the capture thread tees from here on
    }

    /// <inheritdoc/>
    public void StopDirectRecording()
    {
        StopDirectRecorderIfAny();
    }

    /// <inheritdoc/>
    public IFramePathRecorder CreateFramePathRecorder(uint width, uint height, uint frameRate,
        WebcamRecordingOptions options)
    {
        var recorder = new MfSinkWriterRecorder(System.IO.Path.GetFullPath(options.OutputPath),
            width, height, frameRate, options.VideoBitrateKbps, includeAudio: false);
        return new MfFramePathRecorder(recorder);
    }

    /// <inheritdoc/>
    public IAudioSidecar StartAudioSidecar(string videoOutputPath)
    {
        if (_audioDeviceId == null)
        {
            return null;
        }
        try
        {
            var wavPath = System.IO.Path.ChangeExtension(videoOutputPath, ".wav");
            return new WavSidecarRecorder(_audioDeviceId, wavPath);
        }
        catch (Exception e)
        {
            Trace.WriteLine(
                $"CodeBrix.Webcam: sidecar audio capture failed to start; recording video only. {e.Message}");
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
        FreeFrameBuffer();
        GC.SuppressFinalize(this);
    }

    private void CaptureThreadMain()
    {
        try
        {
            _source = CreateMediaSource();
            _reader = CreateSourceReader(_source);
            ApplyRequestedNativeType();
            SetRgb32Output();
            RefreshNegotiatedFormat();
        }
        catch (Exception e)
        {
            ReleaseReaderAndSource();
            _startupError = e as WebcamException
                ?? new WebcamException($"Could not start capturing from '{_device.FriendlyName}'.", e);
            _startedSignal.Set();
            return;
        }
        _startedSignal.Set();

        try
        {
            CaptureLoop();
        }
        finally
        {
            ReleaseReaderAndSource();
        }
    }

    private void CaptureLoop()
    {
        while (!_stopRequested)
        {
            var hr = _reader.ReadSample(MfSourceReaderFirstVideoStream, 0,
                out _, out var streamFlags, out _, out var sample);
            if (hr < 0)
            {
                Trace.WriteLine($"CodeBrix.Webcam: MF ReadSample failed (HRESULT 0x{hr:X8}); capture stopping.");
                return;
            }
            if ((streamFlags & MfSourceReaderFlagEndOfStream) != 0)
            {
                Trace.WriteLine("CodeBrix.Webcam: the camera stream ended unexpectedly.");
                return;
            }
            if ((streamFlags & MfSourceReaderFlagCurrentMediaTypeChanged) != 0)
            {
                RefreshNegotiatedFormat();
            }
            if (sample == null)
            {
                continue; // stream tick / gap
            }

            try
            {
                DeliverSample(sample);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"CodeBrix.Webcam: MF frame delivery threw: {e}");
            }
            finally
            {
                Marshal.ReleaseComObject(sample);
            }
        }
    }

    private unsafe void DeliverSample(IMFSample sample)
    {
        if (sample.ConvertToContiguousBuffer(out var buffer) != 0)
        {
            return;
        }
        try
        {
            if (buffer.Lock(out var data, out _, out var length) != 0)
            {
                return;
            }
            try
            {
                var width = _width;
                var height = _height;
                var packedPitch = width * 4;
                if (width == 0 || height == 0 || length < packedPitch * height)
                {
                    return; // format race — a fresh negotiated size arrives next read
                }
                EnsureFrameBuffer(packedPitch * height);

                // Normalize to packed top-down BGRA with opaque alpha. A negative
                // source stride means bottom-up rows (DIB convention).
                var stride = _sourceStride != 0 ? _sourceStride : (int)packedPitch;
                var rowBytes = (int)packedPitch;
                var src = (byte*)data;
                if (stride < 0)
                {
                    src += (long)(height - 1) * -stride;
                }
                var dst = (byte*)_frameBuffer;
                for (var y = 0; y < height; y++)
                {
                    var srcRow = (uint*)(src + ((long)y * stride));
                    var dstRow = (uint*)(dst + ((long)y * rowBytes));
                    for (var x = 0; x < width; x++)
                    {
                        dstRow[x] = srcRow[x] | 0xFF000000u;
                    }
                }

                var handler = FrameReady;
                if (handler != null)
                {
                    _frameArgs.Update(_frameBuffer, width, height, packedPitch);
                    handler(this, _frameArgs);
                }

                var recorder = _directRecorder;
                if (recorder != null)
                {
                    var timestampHns = _recordClock.ElapsedTicks * 10_000_000L / Stopwatch.Frequency;
                    recorder.WriteVideoFrame(_frameBuffer, packedPitch, timestampHns);
                }
            }
            finally
            {
                buffer.Unlock();
            }
        }
        finally
        {
            Marshal.ReleaseComObject(buffer);
        }
    }

    private void StartMonitor()
    {
        try
        {
            _monitor = new WasapiAudioMonitor(_audioDeviceId, _monitorVolume);
        }
        catch (Exception e)
        {
            Trace.WriteLine($"CodeBrix.Webcam: audio monitoring could not start: {e.Message}");
            _monitor = null;
        }
    }

    private void StopDirectRecorderIfAny()
    {
        var recorder = _directRecorder;
        if (recorder == null)
        {
            return;
        }
        _directRecorder = null; // stop the tee first; WriteVideoFrame is lock-guarded
        if (_directAudioCapture != null)
        {
            _directAudioCapture.Dispose();
            _directAudioCapture = null;
        }
        if (!recorder.Finish())
        {
            Trace.WriteLine("CodeBrix.Webcam: the recording encoder did not finalize cleanly.");
        }
        recorder.Dispose();
    }

    private IMFMediaSource CreateMediaSource()
    {
        ThrowOnFailure(MFCreateAttributes(out var attributes, 1), "MFCreateAttributes");
        IntPtr activateArray = IntPtr.Zero;
        uint count = 0;
        try
        {
            var sourceTypeKey = MfDevsourceAttributeSourceType;
            var vidcapGuid = MfDevsourceAttributeSourceTypeVidcapGuid;
            attributes.SetGUID(ref sourceTypeKey, ref vidcapGuid);
            ThrowOnFailure(MFEnumDeviceSources(attributes, out activateArray, out count),
                "MFEnumDeviceSources");

            // Match by device path. The DirectShow DevicePath and the MF symbolic link
            // name the same KS device interface through different interface-class GUIDs,
            // so the comparison key strips the GUID but KEEPS the trailing reference
            // string: on devices whose cameras are separate filter factories on ONE PnP
            // instance (e.g. the Qualcomm camera subsystem on Windows-on-ARM Surfaces,
            // where front and rear differ only in that reference string), the reference
            // string is the only part that tells the cameras apart. Fall back to the
            // instance segment alone (pre-existing behavior for drivers whose reference
            // strings differ across categories), then to the friendly name.
            var wantedKey = DeviceMatchKey(_device.Id);
            var wantedInstance = InstanceSegment(_device.Id);
            IMFActivate keyMatch = null;
            IMFActivate instanceMatch = null;
            IMFActivate nameMatch = null;
            var wrappers = new IMFActivate[count];
            for (uint i = 0; i < count; i++)
            {
                var pointer = Marshal.ReadIntPtr(activateArray, (int)i * IntPtr.Size);
                var activate = (IMFActivate)Marshal.GetObjectForIUnknown(pointer);
                Marshal.Release(pointer); // the wrapper holds its own reference now
                wrappers[i] = activate;

                var symbolicLink = GetAllocatedString(activate, MfDevsourceAttributeVidcapSymbolicLink);
                if (keyMatch == null && wantedKey != null
                    && string.Equals(DeviceMatchKey(symbolicLink), wantedKey, StringComparison.OrdinalIgnoreCase))
                {
                    keyMatch = activate;
                }
                if (instanceMatch == null && wantedInstance != null
                    && string.Equals(InstanceSegment(symbolicLink), wantedInstance, StringComparison.OrdinalIgnoreCase))
                {
                    instanceMatch = activate;
                }
                if (nameMatch == null)
                {
                    var friendlyName = GetAllocatedString(activate, MfDevsourceAttributeFriendlyName);
                    if (string.Equals(friendlyName, _device.FriendlyName, StringComparison.OrdinalIgnoreCase))
                    {
                        nameMatch = activate;
                    }
                }
            }

            var chosen = keyMatch ?? instanceMatch ?? nameMatch;
            if (chosen == null)
            {
                throw new WebcamException(
                    $"Camera not found by the Windows media engine: {_device.FriendlyName}. "
                    + "Was it disconnected?");
            }

            var sourceIid = IidIMFMediaSource;
            ThrowOnFailure(chosen.ActivateObject(ref sourceIid, out var sourceObject),
                "IMFActivate.ActivateObject");
            var source = (IMFMediaSource)sourceObject;

            foreach (var wrapper in wrappers)
            {
                if (wrapper != null)
                {
                    Marshal.ReleaseComObject(wrapper);
                }
            }
            return source;
        }
        finally
        {
            if (activateArray != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(activateArray);
            }
            Marshal.ReleaseComObject(attributes);
        }
    }

    private IMFSourceReader CreateSourceReader(IMFMediaSource source)
    {
        ThrowOnFailure(MFCreateAttributes(out var attributes, 1), "MFCreateAttributes");
        try
        {
            // Advanced video processing enables the in-box converters/decoders that
            // turn any camera format into our RGB32 output.
            var advancedKey = MfSourceReaderEnableAdvancedVideoProcessing;
            attributes.SetUINT32(ref advancedKey, 1);
            ThrowOnFailure(MFCreateSourceReaderFromMediaSource(source, attributes, out var reader),
                "MFCreateSourceReaderFromMediaSource");
            return reader;
        }
        finally
        {
            Marshal.ReleaseComObject(attributes);
        }
    }

    /// <summary>
    /// When the session options request a mode (size / rate / format), picks the best
    /// matching native camera type and makes it current — mirroring the dshow-size /
    /// dshow-fps / dshow-chroma options the libvlc backend passes. With no requests,
    /// the camera's default type stands.
    /// </summary>
    private void ApplyRequestedNativeType()
    {
        var wantsSize = _options.RequestedWidth > 0 && _options.RequestedHeight > 0;
        var wantsRate = _options.RequestedFrameRate > 0;
        var wantedSubtype = SubtypeForFormat(_options.PreferredFormat);
        if (!wantsSize && !wantsRate && wantedSubtype == null)
        {
            return;
        }

        IMFMediaType best = null;
        var bestScore = double.MinValue;
        for (uint index = 0; ; index++)
        {
            var hr = _reader.GetNativeMediaType(MfSourceReaderFirstVideoStream, index, out var type);
            if (hr == MfErrorNoMoreTypes)
            {
                break;
            }
            if (hr < 0)
            {
                break;
            }

            var subtypeKey = MfMtSubtype;
            type.GetGUID(ref subtypeKey, out var subtype);
            var sizeKey = MfMtFrameSize;
            type.GetUINT64(ref sizeKey, out var packedSize);
            var width = (uint)(packedSize >> 32);
            var height = (uint)packedSize;
            var rateKey = MfMtFrameRate;
            double fps = 0;
            if (type.GetUINT64(ref rateKey, out var packedRate) == 0 && (uint)packedRate != 0)
            {
                fps = (uint)(packedRate >> 32) / (double)(uint)packedRate;
            }

            double score = 0;
            if (wantsSize)
            {
                score += width == _options.RequestedWidth && height == _options.RequestedHeight
                    ? 1000
                    : -Math.Abs((double)width * height
                        - (double)_options.RequestedWidth * _options.RequestedHeight) / 100000.0;
            }
            if (wantedSubtype != null)
            {
                score += subtype == wantedSubtype.Value ? 500 : 0;
            }
            if (wantsRate && fps > 0)
            {
                score += -Math.Abs(fps - _options.RequestedFrameRate);
            }

            if (score > bestScore)
            {
                if (best != null)
                {
                    Marshal.ReleaseComObject(best);
                }
                best = type;
                bestScore = score;
            }
            else
            {
                Marshal.ReleaseComObject(type);
            }
        }

        if (best != null)
        {
            var hr = _reader.SetCurrentMediaType(MfSourceReaderFirstVideoStream, IntPtr.Zero, best);
            if (hr < 0)
            {
                Trace.WriteLine(
                    $"CodeBrix.Webcam: the requested camera mode was refused (HRESULT 0x{hr:X8}); using the default mode.");
            }
            Marshal.ReleaseComObject(best);
        }
    }

    private void SetRgb32Output()
    {
        ThrowOnFailure(MFCreateMediaType(out var rgb), "MFCreateMediaType");
        try
        {
            var majorKey = MfMtMajorType;
            var videoMajor = MfMediaTypeVideo;
            rgb.SetGUID(ref majorKey, ref videoMajor);
            var subtypeKey = MfMtSubtype;
            var rgb32 = MfVideoFormatRgb32;
            rgb.SetGUID(ref subtypeKey, ref rgb32);
            ThrowOnFailure(
                _reader.SetCurrentMediaType(MfSourceReaderFirstVideoStream, IntPtr.Zero, rgb),
                $"Could not open '{_device.FriendlyName}' for BGRA capture");
        }
        finally
        {
            Marshal.ReleaseComObject(rgb);
        }
    }

    private void RefreshNegotiatedFormat()
    {
        if (_reader.GetCurrentMediaType(MfSourceReaderFirstVideoStream, out var current) != 0)
        {
            return;
        }
        try
        {
            var sizeKey = MfMtFrameSize;
            if (current.GetUINT64(ref sizeKey, out var packedSize) == 0)
            {
                _width = (uint)(packedSize >> 32);
                _height = (uint)packedSize;
            }
            var strideKey = MfMtDefaultStride;
            _sourceStride = current.GetUINT32(ref strideKey, out var stride) == 0
                ? unchecked((int)stride)
                : 0;
            var rateKey = MfMtFrameRate;
            if (current.GetUINT64(ref rateKey, out var packedRate) == 0 && (uint)packedRate != 0)
            {
                var fps = (uint)(packedRate >> 32) / (double)(uint)packedRate;
                if (fps > 0)
                {
                    _frameRate = (uint)Math.Max(1, Math.Round(fps));
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(current);
        }
    }

    private void EnsureFrameBuffer(uint requiredBytes)
    {
        if (_frameBufferSize < requiredBytes)
        {
            FreeFrameBuffer();
            _frameBuffer = Marshal.AllocHGlobal((int)requiredBytes);
            _frameBufferSize = requiredBytes;
        }
    }

    private void FreeFrameBuffer()
    {
        if (_frameBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_frameBuffer);
            _frameBuffer = IntPtr.Zero;
            _frameBufferSize = 0;
        }
    }

    private void ReleaseReaderAndSource()
    {
        if (_reader != null)
        {
            Marshal.ReleaseComObject(_reader);
            _reader = null;
        }
        if (_source != null)
        {
            _source.Shutdown();
            Marshal.ReleaseComObject(_source);
            _source = null;
        }
    }

    private static void EnsureMediaFoundationStarted()
    {
        lock (MfStartupLock)
        {
            if (_mfStarted)
            {
                return;
            }
            var hr = MFStartup(MfVersion, MfStartupFull);
            if (hr < 0)
            {
                throw new WebcamException(
                    "The Windows Media Foundation engine could not start (HRESULT 0x"
                    + hr.ToString("X8")
                    + "). On Windows 'N' editions, install the Media Feature Pack.");
            }
            _mfStarted = true; // left running for the process lifetime
        }
    }

    /// <summary>
    /// The PnP instance segment of a device-interface path — the part before the
    /// interface-class GUID, e.g. <c>\\?\usb#vid_046d&amp;pid_0944&amp;mi_00#7&amp;10e98aea&amp;0&amp;0000</c>.
    /// </summary>
    internal static string InstanceSegment(string deviceInterfacePath)
    {
        if (string.IsNullOrEmpty(deviceInterfacePath))
        {
            return null;
        }
        var guidStart = deviceInterfacePath.IndexOf("#{", StringComparison.Ordinal);
        return guidStart > 0 ? deviceInterfacePath.Substring(0, guidStart) : deviceInterfacePath;
    }

    /// <summary>
    /// A device-interface path with ONLY the interface-class GUID removed — the PnP
    /// instance segment plus the KS reference string, e.g.
    /// <c>\\?\usb#vid_046d&amp;mi_00#7&amp;10e98aea&amp;0&amp;0000\global</c>. Unlike
    /// <see cref="InstanceSegment"/>, this keeps the reference string, which is the only
    /// part that distinguishes cameras sharing one PnP instance (front vs. rear on
    /// Qualcomm Windows-on-ARM devices).
    /// </summary>
    internal static string DeviceMatchKey(string deviceInterfacePath)
    {
        if (string.IsNullOrEmpty(deviceInterfacePath))
        {
            return null;
        }
        var guidStart = deviceInterfacePath.IndexOf("#{", StringComparison.Ordinal);
        if (guidStart <= 0)
        {
            return deviceInterfacePath;
        }
        var guidEnd = deviceInterfacePath.IndexOf('}', guidStart);
        return guidEnd < 0
            ? deviceInterfacePath.Substring(0, guidStart)
            : deviceInterfacePath.Substring(0, guidStart) + deviceInterfacePath.Substring(guidEnd + 1);
    }

    private static Guid? SubtypeForFormat(ImagingPixelFormat format) => format switch
    {
        ImagingPixelFormat.Mjpeg => MfVideoFormatMjpg,
        ImagingPixelFormat.Yuyv => MfVideoFormatYuy2,
        ImagingPixelFormat.Nv12 => MfVideoFormatNv12,
        ImagingPixelFormat.H264 => MfVideoFormatH264,
        _ => null,
    };
}
