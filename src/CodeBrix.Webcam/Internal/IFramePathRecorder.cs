using System;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// The encoder side of a frame-path recording: the session pushes each (possibly
/// overlay-composited) BGRA frame, and the recorder encodes it into the output file.
/// One-shot: create via <see cref="ICaptureBackend.CreateFramePathRecorder"/>,
/// <see cref="Start"/>, push frames, <see cref="Complete"/>,
/// <see cref="WaitForCompletion"/>, dispose.
/// </summary>
internal interface IFramePathRecorder : IDisposable
{
    /// <summary>The number of frames accepted by <see cref="PushFrame"/> so far.</summary>
    long FramesPushed { get; }

    /// <summary>Starts the encoder.</summary>
    /// <returns>True if the encoder started; false if it refused (the caller disposes).</returns>
    bool Start();

    /// <summary>
    /// Copies one BGRA frame into the encoder. Called from the capture thread; must
    /// copy before returning. May block briefly when the encoder applies back-pressure.
    /// </summary>
    /// <param name="pixels">Pointer to the top-left pixel.</param>
    /// <param name="sourcePitchBytes">The source's bytes per scanline.</param>
    /// <returns>True if the frame was accepted; false once completed/disposed.</returns>
    bool PushFrame(IntPtr pixels, uint sourcePitchBytes);

    /// <summary>Declares that no more frames will be pushed.</summary>
    void Complete();

    /// <summary>
    /// Blocks until the encoder has drained and the output file is finalized.
    /// </summary>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>True on clean finalization; false on timeout or encoder error.</returns>
    bool WaitForCompletion(TimeSpan timeout);
}
