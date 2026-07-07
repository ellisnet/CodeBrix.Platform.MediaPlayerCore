using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using static CodeBrix.Webcam.Internal.Windows.WasapiNativeMethods;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// Live audio monitoring on Windows: pumps a microphone's WASAPI capture stream into
/// the default render endpoint with a 0–100 volume scale. Both clients run shared-mode
/// at the fixed 48 kHz / 16-bit / stereo PCM format (engine-converted), so the pump is
/// a plain copy with gain. Packets that outrun the render buffer are dropped — for
/// live monitoring, staying current beats completeness.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WasapiAudioMonitor : IDisposable
{
    // Assigned via MtaThread in the constructor so the WASAPI objects live in the MTA.
    private IAudioClient _captureClient;
    private IAudioCaptureClient _capture;
    private IAudioClient _renderClient;
    private IAudioRenderClient _render;
    private uint _renderBufferFrames;

    private Thread _thread;
    private volatile bool _stopRequested;
    private volatile int _volume = 100;
    private bool _disposed;

    /// <summary>Opens the microphone and the default output; the pump starts immediately.</summary>
    /// <param name="microphoneFriendlyName">The microphone's friendly name (or null for default).</param>
    /// <param name="volume">The initial monitoring volume, 0–100.</param>
    internal WasapiAudioMonitor(string microphoneFriendlyName, int volume)
    {
        _volume = Math.Clamp(volume, 0, 100);
        MtaThread.Run(() => InitializeClients(microphoneFriendlyName));
        _thread = new Thread(PumpLoop)
        {
            IsBackground = true,
            Name = "CodeBrix.Webcam audio monitor",
        };
        _thread.Start();
    }

    private void InitializeClients(string microphoneFriendlyName)
    {
        var microphone = WasapiMicrophoneCapture.FindCaptureDevice(microphoneFriendlyName);
        try
        {
            _captureClient = WasapiMicrophoneCapture.InitializeSharedClient(microphone);
        }
        finally
        {
            Marshal.ReleaseComObject(microphone);
        }

        var speaker = WasapiMicrophoneCapture.GetDefaultRenderDevice();
        try
        {
            _renderClient = WasapiMicrophoneCapture.InitializeSharedClient(speaker);
        }
        catch
        {
            Marshal.ReleaseComObject(_captureClient);
            throw;
        }
        finally
        {
            Marshal.ReleaseComObject(speaker);
        }

        try
        {
            var captureIid = IidIAudioCaptureClient;
            ThrowIfFailed(_captureClient.GetService(ref captureIid, out var captureService),
                "capture service");
            _capture = (IAudioCaptureClient)captureService;

            var renderIid = IidIAudioRenderClient;
            ThrowIfFailed(_renderClient.GetService(ref renderIid, out var renderService),
                "render service");
            _render = (IAudioRenderClient)renderService;

            ThrowIfFailed(_renderClient.GetBufferSize(out _renderBufferFrames), "render buffer size");
            ThrowIfFailed(_captureClient.Start(), "capture start");
            ThrowIfFailed(_renderClient.Start(), "render start");
        }
        catch
        {
            ReleaseAll();
            throw;
        }
    }

    /// <summary>The monitoring volume, 0–100.</summary>
    internal int Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0, 100);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _stopRequested = true;
        _thread?.Join(TimeSpan.FromSeconds(5));
        _thread = null;
        MtaThread.Run(() =>
        {
            _captureClient?.Stop();
            _renderClient?.Stop();
            ReleaseAll();
        });
    }

    private unsafe void PumpLoop()
    {
        const uint silentFlag = 0x2;
        while (!_stopRequested)
        {
            var pumped = false;
            while (!_stopRequested
                && _capture.GetNextPacketSize(out var packetFrames) == 0
                && packetFrames > 0)
            {
                if (_capture.GetBuffer(out var source, out var frames, out var flags, out _, out _) != 0)
                {
                    break;
                }

                // Write into whatever space the render buffer has; drop the rest.
                var writable = frames;
                if (_renderClient.GetCurrentPadding(out var padding) == 0)
                {
                    var available = _renderBufferFrames > padding ? _renderBufferFrames - padding : 0;
                    if (writable > available)
                    {
                        writable = available;
                    }
                }

                if (writable > 0 && _render.GetBuffer(writable, out var target) == 0)
                {
                    var sampleCount = (int)writable * WasapiMicrophoneCapture.ChannelCount;
                    var volume = _volume;
                    var src = (short*)source;
                    var dst = (short*)target;
                    if ((flags & silentFlag) != 0 || volume == 0)
                    {
                        new Span<byte>((void*)target, sampleCount * 2).Clear();
                    }
                    else if (volume == 100)
                    {
                        Buffer.MemoryCopy((void*)source, (void*)target, sampleCount * 2L, sampleCount * 2L);
                    }
                    else
                    {
                        for (var i = 0; i < sampleCount; i++)
                        {
                            dst[i] = (short)(src[i] * volume / 100);
                        }
                    }
                    _render.ReleaseBuffer(writable, 0);
                }

                _capture.ReleaseBuffer(frames);
                pumped = true;
            }
            if (!pumped)
            {
                Thread.Sleep(5);
            }
        }
    }

    private void ReleaseAll()
    {
        if (_capture != null)
        {
            Marshal.ReleaseComObject(_capture);
        }
        if (_render != null)
        {
            Marshal.ReleaseComObject(_render);
        }
        if (_captureClient != null)
        {
            Marshal.ReleaseComObject(_captureClient);
        }
        if (_renderClient != null)
        {
            Marshal.ReleaseComObject(_renderClient);
        }
    }

    private static void ThrowIfFailed(int hresult, string operation)
    {
        if (hresult < 0)
        {
            throw new WebcamException(
                $"WASAPI monitoring setup failed at {operation} (HRESULT 0x{hresult:X8}).");
        }
    }
}
