using System;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// A microphone capture writing a sidecar WAV file alongside a frame-path recording,
/// started via <see cref="ICaptureBackend.StartAudioSidecar"/>. The WAV file is valid
/// only after <see cref="Stop"/>.
/// </summary>
internal interface IAudioSidecar : IDisposable
{
    /// <summary>The sidecar WAV file being written.</summary>
    string OutputPath { get; }

    /// <summary>When the audio capture started (UTC) — used to estimate the audio/video offset.</summary>
    DateTime StartedAtUtc { get; }

    /// <summary>Stops capture and finalizes the WAV file.</summary>
    void Stop();
}
