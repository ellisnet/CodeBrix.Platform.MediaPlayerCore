using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using CodeBrix.Platform.MediaPlayerCore;
using CodeBrix.Webcam.Capture;
using CodeBrix.Webcam.Devices;
using CodeBrix.Webcam.Internal.Darwin;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// The libvlc-based <see cref="ICaptureBackend"/>: captures through libvlc's per-platform
/// access module (v4l2 on Linux, avcapture on macOS, dshow on Windows), renders frames
/// into memory via <see cref="VideoFrameSink"/>, records directly with stream-output
/// chains, and encodes frame-path recordings through <see cref="VideoFrameSource"/>
/// (imem). Direct recording works by restarting the SAME player on a media whose sout
/// chain duplicates the stream: one branch keeps feeding the frame sink (the "display"
/// output), the other goes to disk — the preview blinks briefly during the restart.
/// <para/>
/// Per the <see cref="ICaptureBackend"/> contract, all control-surface calls arrive
/// serialized under the owning session's API lock; this class adds no locking.
/// </summary>
internal sealed class LibVlcCaptureBackend : ICaptureBackend
{
    private readonly IImagingMediaDevice _device;
    private readonly WebcamSessionOptions _options;
    private readonly string _audioDeviceId;
    private readonly CaptureFrameEventArgs _frameArgs = new CaptureFrameEventArgs();

    private MediaPlayer _player;
    private Media _media;
    private VideoFrameSink _sink;
    private bool _monitorAudio;
    private int _monitorVolume = 100;

    /// <summary>Creates the backend; the libvlc engine is not touched until <see cref="Start"/>.</summary>
    internal LibVlcCaptureBackend(IImagingMediaDevice device, WebcamSessionOptions options,
        string audioDeviceId)
    {
        _device = device;
        _options = options;
        _audioDeviceId = audioDeviceId;
    }

    /// <inheritdoc/>
    public event EventHandler<CaptureFrameEventArgs> FrameReady;

    /// <inheritdoc/>
    public uint FrameWidth => _sink?.Width ?? 0;

    /// <inheritdoc/>
    public uint FrameHeight => _sink?.Height ?? 0;

    /// <inheritdoc/>
    public bool SupportsFramePathRecording => WebcamEngine.SupportsFrameInput();

    /// <inheritdoc/>
    public void EnsureFramePathRecordingSupported()
    {
        if (!SupportsFramePathRecording)
        {
            throw new WebcamException(
                "Overlay recording is unavailable: this libvlc installation cannot accept " +
                "in-memory video frames (the \"imem\" input plugin is missing). Plain recording " +
                "and photo overlay burn-in still work. On Debian/Ubuntu, verify vlc-plugin-base " +
                "is installed.");
        }
    }

    /// <inheritdoc/>
    public void SetAudioMonitoring(bool monitor, int volume)
    {
        _monitorAudio = monitor;
        _monitorVolume = Math.Clamp(volume, 0, 100);
        var player = _player;
        if (player != null)
        {
            player.Mute = !monitor;
            player.Volume = _monitorVolume;
        }
    }

    /// <inheritdoc/>
    public void Start()
    {
        if (OperatingSystem.IsMacOS())
        {
            // libvlc's macOS capture modules fail (rather than prompt) without TCC
            // consent, so obtain it — prompting the user if needed — up front.
            DarwinCaptureAuthorization.EnsureAccess(_audioDeviceId != null);
        }

        _media = CaptureMediaFactory.Build(_device, _options, _audioDeviceId, false, null);
        _player = new MediaPlayer(_media);
        _sink = new VideoFrameSink(_player);
        _sink.FrameReady += OnSinkFrameReady;
        ApplyMonitoring();

        if (!_player.Play())
        {
            CleanUpPlayer();
            throw new WebcamException($"Could not start capturing from '{_device.FriendlyName}'.");
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _player?.Stop();
        CleanUpPlayer();
    }

    /// <inheritdoc/>
    public void StartDirectRecording(WebcamRecordingOptions options, bool forceMjpeg)
    {
        var outputPath = QuoteForSout(options.OutputPath);
        string soutChain;
        if (forceMjpeg)
        {
            soutChain = "#duplicate{dst=display,dst=standard{access=file,mux=avi,dst="
                + outputPath + "}}";
        }
        else
        {
            var audioPart = _audioDeviceId != null ? ",acodec=mp4a,ab=128" : string.Empty;
            soutChain = "#duplicate{dst=display,dst=transcode{vcodec=h264,vb="
                + options.VideoBitrateKbps.ToString(CultureInfo.InvariantCulture)
                + ",venc=x264{tune=zerolatency}" + audioPart
                + "}:standard{access=file,mux=mp4,dst=" + outputPath + "}}";
        }

        _player.Stop();
        var oldMedia = _media;
        _media = CaptureMediaFactory.Build(_device, _options, _audioDeviceId, forceMjpeg, soutChain);
        _player.Media = _media;
        oldMedia?.Dispose();
        ApplyMonitoring();
        if (!_player.Play())
        {
            throw new WebcamException("Could not restart the camera with the recording pipeline.");
        }
    }

    /// <inheritdoc/>
    public void StopDirectRecording()
    {
        // Swap back to the plain preview media.
        _player.Stop();
        var oldMedia = _media;
        _media = CaptureMediaFactory.Build(_device, _options, _audioDeviceId, false, null);
        _player.Media = _media;
        oldMedia?.Dispose();
        ApplyMonitoring();
        _player.Play();
    }

    /// <inheritdoc/>
    public IFramePathRecorder CreateFramePathRecorder(uint width, uint height, uint frameRate,
        WebcamRecordingOptions options)
    {
        var outputPath = QuoteForSout(options.OutputPath);
        var source = new VideoFrameSource(WebcamEngine.Shared, width, height, frameRate,
            ":sout=#transcode{vcodec=h264,vb=" + options.VideoBitrateKbps.ToString(CultureInfo.InvariantCulture)
            + ",venc=x264{tune=zerolatency}}:standard{access=file,mux=mp4,dst=" + outputPath + "}");
        return new LibVlcFramePathRecorder(source);
    }

    /// <inheritdoc/>
    public IAudioSidecar StartAudioSidecar(string videoOutputPath)
    {
        if (_audioDeviceId == null)
        {
            return null;
        }
        var sidecar = new AudioSidecarRecorder();
        var wavPath = Path.ChangeExtension(videoOutputPath, ".wav");
        if (!sidecar.Start(_audioDeviceId, wavPath))
        {
            Trace.WriteLine("CodeBrix.Webcam: sidecar audio capture failed to start; recording video only.");
            sidecar.Dispose();
            return null;
        }
        return sidecar;
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();

    private void OnSinkFrameReady(object sender, VideoFrameReadyEventArgs frame)
    {
        var handler = FrameReady;
        if (handler != null)
        {
            _frameArgs.Update(frame.Plane, frame.Width, frame.Height, frame.PitchBytes);
            handler(this, _frameArgs);
        }
    }

    private void ApplyMonitoring()
    {
        _player.Mute = !_monitorAudio;
        _player.Volume = _monitorVolume;
    }

    private void CleanUpPlayer()
    {
        if (_sink != null)
        {
            _sink.FrameReady -= OnSinkFrameReady;
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
}
