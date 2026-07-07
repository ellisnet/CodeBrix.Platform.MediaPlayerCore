using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using CodeBrix.Platform.MediaPlayerCore;
using CodeBrix.Webcam.Capture;
using CodeBrix.Webcam.Devices;
using CodeBrix.Webcam.Internal;
using CodeBrix.Webcam.Internal.Darwin;

namespace CodeBrix.Webcam;

/// <summary>
/// A live capture session on one camera: continuous BGRA preview frames via
/// <see cref="FrameReceived"/>, on-demand frame-photos via <see cref="CapturePhoto"/>,
/// video recording via <see cref="StartRecording"/>/<see cref="StopRecording"/>, and a
/// live-updatable transparent overlay (<see cref="SetOverlay"/>) that is burned into
/// photos and recordings.
/// <para/>
/// Typical flow: enumerate with <see cref="WebcamDevices.GetImagingMediaDeviceListAsync"/>,
/// construct a session for the chosen device, subscribe <see cref="FrameReceived"/>,
/// <see cref="Start"/>, and dispose when done. A session is bound to its device; create
/// a new session to switch cameras. Session control methods are safe to call from any
/// one thread at a time, but must not be called from inside a
/// <see cref="FrameReceived"/> handler.
/// </summary>
public sealed class WebcamSession : IDisposable
{
    private enum RecordingMode
    {
        None = 0,
        Direct,    // libvlc records the camera stream itself (sout duplicate)
        FramePath, // frames pass through managed code (overlay burn-in) into the encoder
    }

    private readonly IImagingMediaDevice _device;
    private readonly WebcamSessionOptions _options;
    private readonly string _audioDeviceId;

    // _apiLock serializes the control surface (Start/Stop/recording changes/Dispose) and
    // is the only context that touches the MediaPlayer lifecycle. _frameSync guards the
    // state the frame callback reads, and is NEVER held across a player operation —
    // player.Stop() waits for in-flight frame callbacks, which take _frameSync.
    private readonly object _apiLock = new object();
    private readonly object _frameSync = new object();

    private readonly WebcamFrameEventArgs _frameArgs = new WebcamFrameEventArgs();
    private readonly ManualResetEventSlim _photoReady = new ManualResetEventSlim(false);

    private MediaPlayer _player;
    private Media _media;
    private VideoFrameSink _sink;
    private volatile bool _running;
    private volatile bool _disposed;
    private bool _monitorAudio;
    private int _monitorVolume = 100;

    private WebcamOverlay _overlay;
    private IntPtr _compositeBuffer;
    private uint _compositeBufferSize;

    private volatile bool _photoPending;
    private byte[] _photoBuffer;
    private int _photoWidth;
    private int _photoHeight;

    private volatile RecordingMode _recordingMode;
    private WebcamRecordingOptions _recordingOptions;
    private VideoFrameSource _frameSource;
    private AudioSidecarRecorder _audioSidecar;
    private DateTime _recordingStartedUtc;
    private DateTime _firstRecordedFrameUtc;
    private bool _sawFirstRecordedFrame;

    /// <summary>Creates a session on the given camera with default options.</summary>
    /// <param name="device">The camera, from <see cref="WebcamDevices.GetImagingMediaDeviceListAsync"/>.</param>
    public WebcamSession(IImagingMediaDevice device)
        : this(device, new WebcamSessionOptions())
    {
    }

    /// <summary>Creates a session on the given camera.</summary>
    /// <param name="device">The camera, from <see cref="WebcamDevices.GetImagingMediaDeviceListAsync"/>.</param>
    /// <param name="options">The session options.</param>
    public WebcamSession(IImagingMediaDevice device, WebcamSessionOptions options)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _audioDeviceId = CaptureMediaFactory.ResolveAudioDevice(device, options);
    }

    /// <summary>The camera this session captures from.</summary>
    public IImagingMediaDevice Device => _device;

    /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>/dispose.</summary>
    public bool IsRunning => _running;

    /// <summary>True while a recording is in progress.</summary>
    public bool IsRecording => _recordingMode != RecordingMode.None;

    /// <summary>
    /// True when this session will capture microphone audio (a device was resolved from
    /// the options — the camera's paired mic for Auto, or the explicit device).
    /// </summary>
    public bool IsAudioCaptureActive => _audioDeviceId != null;

    /// <summary>
    /// True when overlay burn-in on RECORDINGS is available on this machine (photos can
    /// always burn in the overlay). Probes the media engine's in-memory frame input on
    /// first call; the result is cached for the process lifetime.
    /// </summary>
    public bool IsOverlayRecordingSupported => WebcamEngine.SupportsFrameInput();

    /// <summary>Frame width in pixels once frames are flowing, else 0.</summary>
    public uint FrameWidth => _sink?.Width ?? 0;

    /// <summary>Frame height in pixels once frames are flowing, else 0.</summary>
    public uint FrameHeight => _sink?.Height ?? 0;

    /// <summary>
    /// Plays the captured microphone live through the default audio output ("monitoring").
    /// Default false — and beware: enabling this with the camera and speakers in the same
    /// room feeds back audibly. Recording captures the microphone regardless of this switch.
    /// </summary>
    public bool MonitorAudio
    {
        get => _monitorAudio;
        set
        {
            _monitorAudio = value;
            var player = _player;
            if (player != null)
            {
                player.Mute = !value;
            }
        }
    }

    /// <summary>The monitoring volume, 0–100. Default 100. Does not affect recordings.</summary>
    public int MonitorVolume
    {
        get => _monitorVolume;
        set
        {
            _monitorVolume = Math.Clamp(value, 0, 100);
            var player = _player;
            if (player != null)
            {
                player.Volume = _monitorVolume;
            }
        }
    }

    /// <summary>
    /// Raised on an internal capture thread for every live frame. The event args' pixel
    /// buffer is only valid until the handler returns — copy what you need and return
    /// quickly; do not touch UI objects directly and do not call this session's methods
    /// from inside the handler.
    /// </summary>
    public event EventHandler<WebcamFrameEventArgs> FrameReceived;

    /// <summary>Opens the camera and starts the live frame stream.</summary>
    /// <exception cref="WebcamException">The camera could not be opened, capture
    /// permission was refused (macOS), or the native libvlc runtime is not installed —
    /// the message states the per-platform fix.</exception>
    public void Start()
    {
        lock (_apiLock)
        {
            ThrowIfDisposed();
            if (_running)
            {
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                // libvlc's macOS capture modules fail (rather than prompt) without TCC
                // consent, so obtain it — prompting the user if needed — up front.
                DarwinCaptureAuthorization.EnsureAccess(_audioDeviceId != null);
            }

            _media = CaptureMediaFactory.Build(_device, _options, _audioDeviceId, false, null);
            _player = new MediaPlayer(_media);
            _sink = new VideoFrameSink(_player);
            _sink.FrameReady += OnFrameReady;
            _player.Mute = !_monitorAudio;
            _player.Volume = _monitorVolume;

            if (!_player.Play())
            {
                CleanUpPlayer();
                throw new WebcamException($"Could not start capturing from '{_device.FriendlyName}'.");
            }
            _running = true;
        }
    }

    /// <summary>Stops the live frame stream (and any recording in progress).</summary>
    public void Stop()
    {
        lock (_apiLock)
        {
            if (!_running)
            {
                return;
            }
            if (_recordingMode != RecordingMode.None)
            {
                StopRecordingUnderApiLock();
            }
            _running = false;
            _player?.Stop();
            CleanUpPlayer();
        }
    }

    /// <summary>
    /// Sets (or replaces) the transparent overlay that is burned into photos and
    /// frame-path recordings — and into preview frames when
    /// <see cref="WebcamSessionOptions.CompositeOverlayOnPreview"/> is set. The overlay
    /// must match the session's frame dimensions exactly.
    /// </summary>
    /// <param name="overlay">The overlay (straight-alpha BGRA).</param>
    /// <exception cref="WebcamException">The overlay dimensions do not match the video,
    /// or a direct (non-frame-path) recording is in progress.</exception>
    public void SetOverlay(WebcamOverlay overlay)
    {
        if (overlay == null)
        {
            throw new ArgumentNullException(nameof(overlay));
        }
        if (_recordingMode == RecordingMode.Direct)
        {
            throw new WebcamException(
                "A direct recording is in progress; overlays cannot be introduced mid-recording " +
                "on the direct pipeline. Start the recording with WebcamRecordingOptions.AllowLiveOverlay " +
                "= true (or with the overlay already set) to change overlays while recording.");
        }
        var width = FrameWidth;
        var height = FrameHeight;
        if (width != 0 && (overlay.Width != width || overlay.Height != height))
        {
            throw new WebcamException(
                $"Overlay is {overlay.Width}x{overlay.Height} but the video is {width}x{height}; " +
                "they must match exactly.");
        }
        lock (_frameSync)
        {
            _overlay = overlay;
        }
    }

    /// <summary>Removes the overlay; subsequent photos, recordings, and preview frames are unmodified video.</summary>
    public void ClearOverlay()
    {
        lock (_frameSync)
        {
            _overlay = null;
        }
    }

    /// <summary>
    /// Captures the next live frame as a photo — tightly packed BGRA pixels ready for an
    /// image library. If an overlay is set, it is burned in.
    /// </summary>
    /// <param name="timeout">How long to wait for the next frame; default 2 seconds when omitted.</param>
    /// <returns>The captured photo.</returns>
    /// <exception cref="WebcamException">The session is not running, or no frame arrived in time.</exception>
    public WebcamPhoto CapturePhoto(TimeSpan timeout = default)
    {
        if (!_running)
        {
            throw new WebcamException("Start() the session before capturing photos.");
        }
        if (timeout == default)
        {
            timeout = TimeSpan.FromSeconds(2);
        }

        _photoReady.Reset();
        _photoPending = true;
        if (!_photoReady.Wait(timeout))
        {
            _photoPending = false;
            throw new WebcamException(
                $"No video frame arrived within {timeout.TotalSeconds:0.#}s — is the camera streaming?");
        }

        byte[] pixels;
        int width, height;
        lock (_frameSync)
        {
            pixels = _photoBuffer;
            _photoBuffer = null; // hand ownership to the photo
            width = _photoWidth;
            height = _photoHeight;
        }
        if (pixels == null)
        {
            throw new WebcamException("Photo capture failed (no frame data).");
        }
        return new WebcamPhoto(pixels, width, height, DateTime.UtcNow);
    }

    /// <summary>
    /// Starts recording. With <see cref="WebcamVideoFormat.Mp4H264"/> and no overlay
    /// involvement, libvlc records the camera stream directly (in-file audio when the
    /// session captures a microphone; the live preview restarts briefly). With an overlay
    /// set — or <see cref="WebcamRecordingOptions.AllowLiveOverlay"/> — frames flow
    /// through managed compositing into the encoder without interrupting the preview,
    /// and captured audio arrives as a sidecar WAV file (see
    /// <see cref="WebcamRecordingResult.AudioFilePath"/>).
    /// </summary>
    /// <param name="options">What, where, and how to record.</param>
    /// <exception cref="WebcamException">The session is not running / already recording /
    /// the format+overlay combination is invalid / overlay recording is unsupported here.</exception>
    public void StartRecording(WebcamRecordingOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new WebcamException("WebcamRecordingOptions.OutputPath is empty.");
        }

        lock (_apiLock)
        {
            ThrowIfDisposed();
            if (!_running)
            {
                throw new WebcamException("Start() the session before recording.");
            }
            if (_recordingMode != RecordingMode.None)
            {
                throw new WebcamException("A recording is already in progress.");
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var outputPath = QuoteForSout(options.OutputPath);

            bool overlayInvolved;
            lock (_frameSync)
            {
                overlayInvolved = _overlay != null || options.AllowLiveOverlay;
            }

            if (options.Format == WebcamVideoFormat.MjpegAvi)
            {
                if (overlayInvolved)
                {
                    throw new WebcamException(
                        "MJPEG passthrough records the camera's own bytes and cannot burn in overlays; " +
                        "use Mp4H264 for overlay recordings.");
                }
                StartDirectRecordingUnderApiLock(forceMjpeg: true,
                    "#duplicate{dst=display,dst=standard{access=file,mux=avi,dst="
                    + outputPath + "}}");
            }
            else if (overlayInvolved)
            {
                StartFramePathRecordingUnderApiLock(options, outputPath);
            }
            else
            {
                var audioPart = _audioDeviceId != null ? ",acodec=mp4a,ab=128" : string.Empty;
                StartDirectRecordingUnderApiLock(forceMjpeg: false,
                    "#duplicate{dst=display,dst=transcode{vcodec=h264,vb="
                    + options.VideoBitrateKbps.ToString(CultureInfo.InvariantCulture)
                    + ",venc=x264{tune=zerolatency}" + audioPart
                    + "}:standard{access=file,mux=mp4,dst=" + outputPath + "}}");
            }

            _recordingOptions = options;
            _recordingStartedUtc = DateTime.UtcNow;
            _sawFirstRecordedFrame = false;
        }
    }

    /// <summary>Stops the recording and finalizes the output file(s).</summary>
    /// <returns>The recording outcome: file paths, sidecar audio (if any), and timing.</returns>
    /// <exception cref="WebcamException">No recording is in progress.</exception>
    public WebcamRecordingResult StopRecording()
    {
        lock (_apiLock)
        {
            if (_recordingMode == RecordingMode.None)
            {
                throw new WebcamException("No recording is in progress.");
            }
            return StopRecordingUnderApiLock();
        }
    }

    /// <summary>Stops the session and releases the capture pipeline.</summary>
    public void Dispose()
    {
        lock (_apiLock)
        {
            if (_disposed)
            {
                return;
            }
            if (_running)
            {
                if (_recordingMode != RecordingMode.None)
                {
                    try
                    {
                        StopRecordingUnderApiLock();
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine($"CodeBrix.Webcam: recording teardown during Dispose failed: {e.Message}");
                    }
                }
                _running = false;
                _player?.Stop();
                CleanUpPlayer();
            }
            _disposed = true;
            lock (_frameSync)
            {
                if (_compositeBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_compositeBuffer);
                    _compositeBuffer = IntPtr.Zero;
                    _compositeBufferSize = 0;
                }
            }
        }
        _photoReady.Set(); // release any waiting CapturePhoto
    }

    private void StartDirectRecordingUnderApiLock(bool forceMjpeg, string soutChain)
    {
        // The direct pipeline records by restarting the SAME player on a media that
        // duplicates the stream: one branch keeps feeding the frame sink (the "display"
        // output), the other goes to disk. The preview blinks briefly during the restart.
        _player.Stop();
        var oldMedia = _media;
        _media = CaptureMediaFactory.Build(_device, _options, _audioDeviceId, forceMjpeg, soutChain);
        _player.Media = _media;
        oldMedia?.Dispose();
        _player.Mute = !_monitorAudio;
        _player.Volume = _monitorVolume;
        if (!_player.Play())
        {
            throw new WebcamException("Could not restart the camera with the recording pipeline.");
        }
        _recordingMode = RecordingMode.Direct;
    }

    private void StartFramePathRecordingUnderApiLock(WebcamRecordingOptions options, string quotedOutputPath)
    {
        if (!WebcamEngine.SupportsFrameInput())
        {
            throw new WebcamException(
                "Overlay recording is unavailable: this libvlc installation cannot accept " +
                "in-memory video frames (the \"imem\" input plugin is missing). Plain recording " +
                "and photo overlay burn-in still work. On Debian/Ubuntu, verify vlc-plugin-base " +
                "is installed.");
        }
        var width = FrameWidth;
        var height = FrameHeight;
        if (width == 0 || height == 0)
        {
            throw new WebcamException("No video frames have arrived yet; try again in a moment.");
        }

        var fpsHint = _options.RequestedFrameRate > 0 ? (uint)Math.Round(_options.RequestedFrameRate) : 30u;
        var source = new VideoFrameSource(WebcamEngine.Shared, width, height, fpsHint,
            ":sout=#transcode{vcodec=h264,vb=" + options.VideoBitrateKbps.ToString(CultureInfo.InvariantCulture)
            + ",venc=x264{tune=zerolatency}}:standard{access=file,mux=mp4,dst=" + quotedOutputPath + "}");
        if (!source.Start())
        {
            source.Dispose();
            throw new WebcamException("Could not start the overlay recording encoder.");
        }

        AudioSidecarRecorder sidecar = null;
        if (_audioDeviceId != null)
        {
            sidecar = new AudioSidecarRecorder();
            var wavPath = Path.ChangeExtension(options.OutputPath, ".wav");
            if (!sidecar.Start(_audioDeviceId, wavPath))
            {
                Trace.WriteLine("CodeBrix.Webcam: sidecar audio capture failed to start; recording video only.");
                sidecar.Dispose();
                sidecar = null;
            }
        }

        lock (_frameSync)
        {
            _frameSource = source;
            _audioSidecar = sidecar;
            _recordingMode = RecordingMode.FramePath;
        }
    }

    private WebcamRecordingResult StopRecordingUnderApiLock()
    {
        var options = _recordingOptions;
        _recordingOptions = null;
        var duration = DateTime.UtcNow - _recordingStartedUtc;

        if (_recordingMode == RecordingMode.Direct)
        {
            _recordingMode = RecordingMode.None;
            // Swap back to the plain preview media.
            _player.Stop();
            var oldMedia = _media;
            _media = CaptureMediaFactory.Build(_device, _options, _audioDeviceId, false, null);
            _player.Media = _media;
            oldMedia?.Dispose();
            _player.Mute = !_monitorAudio;
            _player.Volume = _monitorVolume;
            _player.Play();
            return new WebcamRecordingResult(options.OutputPath, null, null, duration, 0);
        }

        // Frame path: detach from the frame callback first, then drain and finalize.
        VideoFrameSource source;
        AudioSidecarRecorder sidecar;
        lock (_frameSync)
        {
            source = _frameSource;
            sidecar = _audioSidecar;
            _frameSource = null;
            _audioSidecar = null;
            _recordingMode = RecordingMode.None;
        }

        var frames = 0L;
        if (source != null)
        {
            source.Complete();
            if (!source.WaitForCompletion(TimeSpan.FromSeconds(30)))
            {
                Trace.WriteLine("CodeBrix.Webcam: the recording encoder did not finalize cleanly.");
            }
            frames = source.FramesPushed;
            source.Dispose();
        }

        string audioPath = null;
        TimeSpan? audioOffset = null;
        if (sidecar != null)
        {
            sidecar.Stop();
            audioPath = sidecar.OutputPath;
            if (_sawFirstRecordedFrame)
            {
                audioOffset = _firstRecordedFrameUtc - sidecar.StartedAtUtc;
            }
            sidecar.Dispose();
        }
        return new WebcamRecordingResult(options.OutputPath, audioPath, audioOffset, duration, frames);
    }

    private void OnFrameReady(object sender, VideoFrameReadyEventArgs frame)
    {
        try
        {
            lock (_frameSync)
            {
                if (!_running || _disposed)
                {
                    return;
                }

                var overlay = _overlay;
                var composited = IntPtr.Zero;
                if (overlay != null
                    && overlay.Width == frame.Width && overlay.Height == frame.Height)
                {
                    EnsureCompositeBufferLocked(frame.PitchBytes * frame.Height);
                    OverlayCompositor.Blend(frame.Plane, frame.PitchBytes, frame.Width, frame.Height,
                        overlay, _compositeBuffer, frame.PitchBytes);
                    composited = _compositeBuffer;
                }

                var burned = composited != IntPtr.Zero ? composited : frame.Plane;

                if (_recordingMode == RecordingMode.FramePath && _frameSource != null)
                {
                    if (!_sawFirstRecordedFrame)
                    {
                        _sawFirstRecordedFrame = true;
                        _firstRecordedFrameUtc = DateTime.UtcNow;
                    }
                    _frameSource.PushFrame(burned, frame.PitchBytes);
                }

                if (_photoPending)
                {
                    _photoPending = false;
                    CopyPackedLocked(burned, frame);
                    _photoReady.Set();
                }

                var previewPlane = _options.CompositeOverlayOnPreview && composited != IntPtr.Zero
                    ? composited
                    : frame.Plane;
                var handler = FrameReceived;
                if (handler != null)
                {
                    _frameArgs.Update(previewPlane, frame.Width, frame.Height, frame.PitchBytes);
                    handler(this, _frameArgs);
                }
            }
        }
        catch (Exception e)
        {
            // An exception escaping into native libvlc code would crash the process.
            Trace.WriteLine($"CodeBrix.Webcam frame handling threw: {e}");
        }
    }

    private void EnsureCompositeBufferLocked(uint requiredBytes)
    {
        if (_compositeBufferSize < requiredBytes)
        {
            if (_compositeBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_compositeBuffer);
            }
            _compositeBuffer = Marshal.AllocHGlobal((int)requiredBytes);
            _compositeBufferSize = requiredBytes;
        }
    }

    private void CopyPackedLocked(IntPtr plane, VideoFrameReadyEventArgs frame)
    {
        var packedRow = (int)(frame.Width * 4);
        var buffer = new byte[packedRow * frame.Height];
        for (var y = 0; y < frame.Height; y++)
        {
            Marshal.Copy(plane + (int)(y * frame.PitchBytes), buffer, y * packedRow, packedRow);
        }
        _photoBuffer = buffer;
        _photoWidth = (int)frame.Width;
        _photoHeight = (int)frame.Height;
    }

    private void CleanUpPlayer()
    {
        if (_sink != null)
        {
            _sink.FrameReady -= OnFrameReady;
            _sink.Dispose();
            _sink = null;
        }
        _player?.Dispose();
        _player = null;
        _media?.Dispose();
        _media = null;
    }

    private static string QuoteForSout(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (fullPath.IndexOf('\'') >= 0)
        {
            throw new WebcamException(
                "Recording paths containing a single-quote character (') are not supported.");
        }
        return "'" + fullPath + "'";
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WebcamSession));
        }
    }
}
