using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using static CodeBrix.Webcam.Internal.Windows.WasapiNativeMethods;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// Captures a microphone through WASAPI (shared mode) and delivers PCM packets to a
/// callback. The stream is always 48 kHz / 16-bit / stereo — the audio engine's
/// AUTOCONVERTPCM path resamples from whatever the device's mix format is — so every
/// consumer (AAC encoding, WAV sidecar, monitoring) sees one fixed format.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WasapiMicrophoneCapture : IDisposable
{
    internal const uint SampleRate = 48000;
    internal const ushort ChannelCount = 2;
    internal const ushort BitsPerSample = 16;
    internal const ushort BytesPerFrame = ChannelCount * BitsPerSample / 8;

    private const uint BufferFlagSilent = 0x2;
    private const long BufferDurationHns = 2_000_000; // 200 ms

    // Assigned via MtaThread in the constructor so the WASAPI objects live in the MTA.
    private IAudioClient _client;
    private IAudioCaptureClient _captureClient;
    private readonly Action<byte[], int> _onSamples;

    private Thread _thread;
    private volatile bool _stopRequested;
    private bool _disposed;
    private byte[] _packet = new byte[BytesPerFrame * 4800];

    /// <summary>The fixed PCM format every capture instance delivers.</summary>
    internal static WaveFormatEx PcmFormat => new WaveFormatEx
    {
        FormatTag = WaveFormatPcm,
        Channels = ChannelCount,
        SamplesPerSecond = SampleRate,
        AverageBytesPerSecond = SampleRate * BytesPerFrame,
        BlockAlign = BytesPerFrame,
        BitsPerSample = BitsPerSample,
        ExtraSize = 0,
    };

    /// <summary>
    /// Opens the microphone whose friendly name matches (falling back to the default
    /// capture endpoint) — capture starts on <see cref="Start"/>.
    /// </summary>
    /// <param name="microphoneFriendlyName">The device's friendly name, e.g.
    /// "Microphone (C922 Pro Stream Webcam)", or null for the default microphone.</param>
    /// <param name="onSamples">Receives each PCM packet (buffer, valid byte count) on
    /// the capture thread. The buffer is reused between packets.</param>
    internal WasapiMicrophoneCapture(string microphoneFriendlyName, Action<byte[], int> onSamples)
    {
        _onSamples = onSamples ?? throw new ArgumentNullException(nameof(onSamples));

        MtaThread.Run(() =>
        {
            var device = FindCaptureDevice(microphoneFriendlyName);
            try
            {
                _client = InitializeSharedClient(device);
                var serviceIid = IidIAudioCaptureClient;
                var hr = _client.GetService(ref serviceIid, out var service);
                if (hr < 0)
                {
                    throw new WebcamException(
                        $"WASAPI capture service unavailable (HRESULT 0x{hr:X8}).");
                }
                _captureClient = (IAudioCaptureClient)service;
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        });
    }

    /// <summary>Starts the capture stream and the packet-delivery thread.</summary>
    internal void Start()
    {
        MtaThread.Run(() =>
        {
            var hr = _client.Start();
            if (hr < 0)
            {
                throw new WebcamException($"WASAPI capture failed to start (HRESULT 0x{hr:X8}).");
            }
        });
        _thread = new Thread(CaptureLoop)
        {
            IsBackground = true,
            Name = "CodeBrix.Webcam WASAPI capture",
        };
        _thread.Start();
    }

    /// <summary>Stops packet delivery and the capture stream.</summary>
    internal void Stop()
    {
        _stopRequested = true;
        _thread?.Join(TimeSpan.FromSeconds(5));
        _thread = null;
        MtaThread.Run(() => _client.Stop());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
        MtaThread.Run(() =>
        {
            Marshal.ReleaseComObject(_captureClient);
            Marshal.ReleaseComObject(_client);
        });
    }

    private void CaptureLoop()
    {
        while (!_stopRequested)
        {
            var delivered = false;
            while (!_stopRequested
                && _captureClient.GetNextPacketSize(out var packetFrames) == 0
                && packetFrames > 0)
            {
                if (_captureClient.GetBuffer(out var data, out var frames, out var flags,
                        out _, out _) != 0)
                {
                    break;
                }
                var bytes = (int)(frames * BytesPerFrame);
                if (_packet.Length < bytes)
                {
                    _packet = new byte[bytes];
                }
                if ((flags & BufferFlagSilent) != 0)
                {
                    Array.Clear(_packet, 0, bytes);
                }
                else
                {
                    Marshal.Copy(data, _packet, 0, bytes);
                }
                _captureClient.ReleaseBuffer(frames);
                _onSamples(_packet, bytes);
                delivered = true;
            }
            if (!delivered)
            {
                Thread.Sleep(5);
            }
        }
    }

    /// <summary>Creates the MMDevice enumerator.</summary>
    internal static IMMDeviceEnumerator CreateDeviceEnumerator()
        => (IMMDeviceEnumerator)Activator.CreateInstance(
            Type.GetTypeFromCLSID(ClsidMMDeviceEnumerator));

    /// <summary>
    /// Finds the active capture endpoint whose friendly name matches (exact first, then
    /// substring), falling back to the default capture endpoint.
    /// </summary>
    internal static IMMDevice FindCaptureDevice(string friendlyName)
    {
        var enumerator = CreateDeviceEnumerator();
        try
        {
            if (friendlyName != null
                && enumerator.EnumAudioEndpoints(ECapture, DeviceStateActive, out var devices) == 0)
            {
                try
                {
                    IMMDevice substringMatch = null;
                    devices.GetCount(out var count);
                    for (uint i = 0; i < count; i++)
                    {
                        if (devices.Item(i, out var device) != 0)
                        {
                            continue;
                        }
                        var name = GetDeviceFriendlyName(device);
                        if (string.Equals(name, friendlyName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (substringMatch != null)
                            {
                                Marshal.ReleaseComObject(substringMatch);
                            }
                            return device;
                        }
                        if (substringMatch == null && name != null
                            && (name.IndexOf(friendlyName, StringComparison.OrdinalIgnoreCase) >= 0
                                || friendlyName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            substringMatch = device;
                            continue;
                        }
                        Marshal.ReleaseComObject(device);
                    }
                    if (substringMatch != null)
                    {
                        return substringMatch;
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(devices);
                }
            }

            var hr = enumerator.GetDefaultAudioEndpoint(ECapture, EConsole, out var fallback);
            if (hr < 0)
            {
                throw new WebcamException(
                    $"No usable microphone was found (HRESULT 0x{hr:X8}).");
            }
            return fallback;
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    /// <summary>The default render (speaker) endpoint, for monitoring playback.</summary>
    internal static IMMDevice GetDefaultRenderDevice()
    {
        var enumerator = CreateDeviceEnumerator();
        try
        {
            var hr = enumerator.GetDefaultAudioEndpoint(ERender, EConsole, out var device);
            if (hr < 0)
            {
                throw new WebcamException(
                    $"No audio output device is available for monitoring (HRESULT 0x{hr:X8}).");
            }
            return device;
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    /// <summary>
    /// Activates an IAudioClient on the device and initializes it shared-mode with the
    /// fixed PCM format (the engine converts to/from the device's mix format).
    /// </summary>
    internal static IAudioClient InitializeSharedClient(IMMDevice device)
    {
        var clientIid = IidIAudioClient;
        var hr = device.Activate(ref clientIid, ClsctxAll, IntPtr.Zero, out var instance);
        if (hr < 0)
        {
            throw new WebcamException($"WASAPI audio client activation failed (HRESULT 0x{hr:X8}).");
        }
        var client = (IAudioClient)instance;
        var format = PcmFormat;
        hr = client.Initialize(AudclntShareModeShared,
            AudclntStreamFlagsAutoConvertPcm | AudclntStreamFlagsSrcDefaultQuality,
            BufferDurationHns, 0, ref format, IntPtr.Zero);
        if (hr < 0)
        {
            Marshal.ReleaseComObject(client);
            throw new WebcamException($"WASAPI audio client initialization failed (HRESULT 0x{hr:X8}).");
        }
        return client;
    }
}
