using System;
using CodeBrix.Platform.MediaPlayerCore;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// The libvlc <see cref="IFramePathRecorder"/>: a thin adapter over
/// <see cref="VideoFrameSource"/>, whose imem input feeds pushed BGRA frames into an
/// x264 transcode-to-MP4 stream-output chain.
/// </summary>
internal sealed class LibVlcFramePathRecorder : IFramePathRecorder
{
    private readonly VideoFrameSource _source;

    internal LibVlcFramePathRecorder(VideoFrameSource source)
    {
        _source = source;
    }

    /// <inheritdoc/>
    public long FramesPushed => _source.FramesPushed;

    /// <inheritdoc/>
    public bool Start() => _source.Start();

    /// <inheritdoc/>
    public bool PushFrame(IntPtr pixels, uint sourcePitchBytes)
        => _source.PushFrame(pixels, sourcePitchBytes);

    /// <inheritdoc/>
    public void Complete() => _source.Complete();

    /// <inheritdoc/>
    public bool WaitForCompletion(TimeSpan timeout) => _source.WaitForCompletion(timeout);

    /// <inheritdoc/>
    public void Dispose() => _source.Dispose();
}
