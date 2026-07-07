using System;
using CodeBrix.Webcam.Capture;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// The per-platform capture engine behind a <see cref="WebcamSession"/>: opens the
/// camera, streams decoded BGRA frames, plays back monitored audio, and provides the
/// two recording pipelines. <see cref="CaptureBackendFactory"/> picks the implementation
/// for the current operating system; the session owns exactly one backend for its
/// lifetime and never exposes it (per the CodeBrix.Webcam no-leak rule, backends and
/// engine types stay internal).
/// <para/>
/// Threading contract: every control-surface member (<see cref="Start"/>,
/// <see cref="Stop"/>, the recording methods, <see cref="SetAudioMonitoring"/>,
/// <see cref="IDisposable.Dispose"/>) is called serialized under the owning session's
/// API lock, so implementations need no control-surface locking of their own.
/// <see cref="FrameReady"/> is raised on an internal capture thread; the session's
/// handler copies what it needs and returns quickly. Implementations must never raise
/// <see cref="FrameReady"/> from inside a control-surface call.
/// </summary>
internal interface ICaptureBackend : IDisposable
{
    /// <summary>
    /// Raised on an internal capture thread for every decoded frame. The event args
    /// instance is reused; the pixel buffer is only valid until the handler returns.
    /// </summary>
    event EventHandler<CaptureFrameEventArgs> FrameReady;

    /// <summary>Frame width in pixels once frames are flowing, else 0.</summary>
    uint FrameWidth { get; }

    /// <summary>Frame height in pixels once frames are flowing, else 0.</summary>
    uint FrameHeight { get; }

    /// <summary>
    /// True when frame-path (overlay burn-in) recording is available on this machine.
    /// May probe the underlying engine on first call; the result is stable for the
    /// process lifetime.
    /// </summary>
    bool SupportsFramePathRecording { get; }

    /// <summary>
    /// Throws a <see cref="WebcamException"/> whose message names the missing piece and
    /// the per-platform fix when <see cref="SupportsFramePathRecording"/> is false;
    /// otherwise does nothing.
    /// </summary>
    void EnsureFramePathRecordingSupported();

    /// <summary>
    /// Applies the live audio-monitoring state (play the captured microphone through
    /// the default output, at the given 0–100 volume). Called both before and during
    /// capture; implementations re-apply the latest values whenever they (re)start
    /// their pipeline.
    /// </summary>
    void SetAudioMonitoring(bool monitor, int volume);

    /// <summary>Opens the camera and starts the frame stream.</summary>
    /// <exception cref="WebcamException">The camera could not be opened, capture
    /// permission was refused, or the capture engine is unavailable — the message
    /// states the per-platform fix.</exception>
    void Start();

    /// <summary>Stops the frame stream and releases the capture pipeline. Safe to call
    /// when not started.</summary>
    void Stop();

    /// <summary>
    /// Starts a direct recording: the backend records the camera stream itself while
    /// the live preview continues. The session guarantees no recording is in progress
    /// and that the output directory exists.
    /// </summary>
    /// <param name="options">What and where to record.</param>
    /// <param name="forceMjpeg">True to record the camera's native MJPEG bytes with no
    /// transcoding (<see cref="WebcamVideoFormat.MjpegAvi"/>).</param>
    /// <exception cref="WebcamException">The recording pipeline could not start.</exception>
    void StartDirectRecording(WebcamRecordingOptions options, bool forceMjpeg);

    /// <summary>Stops a direct recording, finalizes the output file, and resumes the
    /// plain preview stream.</summary>
    void StopDirectRecording();

    /// <summary>
    /// Creates (without starting) the encoder for a frame-path recording — the pipeline
    /// where the session pushes composited BGRA frames. Only called after
    /// <see cref="EnsureFramePathRecordingSupported"/> has passed.
    /// </summary>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="frameRate">The nominal frames-per-second hint for encoder setup.</param>
    /// <param name="options">What and where to record.</param>
    IFramePathRecorder CreateFramePathRecorder(uint width, uint height, uint frameRate,
        WebcamRecordingOptions options);

    /// <summary>
    /// Starts sidecar audio capture accompanying a frame-path recording, writing a WAV
    /// file next to the given video file. Returns null when the session captures no
    /// microphone, when the backend records audio into the video file itself, or when
    /// the sidecar failed to start (already logged; the recording proceeds video-only).
    /// </summary>
    /// <param name="videoOutputPath">The frame-path recording's video file path.</param>
    IAudioSidecar StartAudioSidecar(string videoOutputPath);
}
