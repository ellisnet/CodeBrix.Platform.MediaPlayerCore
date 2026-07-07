using System;
using CodeBrix.Platform.MediaPlayerCore;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// Records a microphone to a sidecar WAV file (PCM — no encoder dependency, always
/// available) alongside a frame-path video recording. Runs its own small libvlc input
/// so it has no timing entanglement with the video pipeline.
/// </summary>
internal sealed class AudioSidecarRecorder : IDisposable
{
    private Media _media;
    private MediaPlayer _player;
    private bool _disposed;

    internal string OutputPath { get; private set; }

    internal DateTime StartedAtUtc { get; private set; }

    /// <summary>Starts capturing the given audio device to a WAV file.</summary>
    /// <returns>True if capture started; false if libvlc refused (missing device, etc.).</returns>
    internal bool Start(string audioDeviceId, string outputPath)
    {
        OutputPath = outputPath;
        string mrl = OperatingSystem.IsWindows() ? "dshow://" : "alsa://" + audioDeviceId;

        var options = OperatingSystem.IsWindows()
            ? new[]
            {
                ":dshow-vdev=none",
                ":dshow-adev=" + audioDeviceId,
                ":live-caching=100",
                ":sout=#transcode{acodec=s16l}:standard{access=file,mux=wav,dst=" + outputPath + "}",
            }
            : new[]
            {
                ":live-caching=100",
                ":sout=#transcode{acodec=s16l}:standard{access=file,mux=wav,dst=" + outputPath + "}",
            };

        _media = new Media(WebcamEngine.Shared, mrl, FromType.FromLocation, options);
        _player = new MediaPlayer(_media);
        StartedAtUtc = DateTime.UtcNow;
        return _player.Play();
    }

    /// <summary>Stops capture and finalizes the WAV file (the wav muxer writes its
    /// header on close, so the file is valid only after this).</summary>
    internal void Stop()
    {
        _player?.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
        _player?.Dispose();
        _media?.Dispose();
    }
}
