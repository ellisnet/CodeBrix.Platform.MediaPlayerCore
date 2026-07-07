using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// The Windows <see cref="IFramePathRecorder"/>: encodes session-pushed (overlay
/// composited) BGRA frames into MP4/H.264 through <see cref="MfSinkWriterRecorder"/>.
/// Frames are stamped with elapsed wall-clock time since <see cref="Start"/>, matching
/// the libvlc frame-path recorder's behavior.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class MfFramePathRecorder : IFramePathRecorder
{
    private readonly MfSinkWriterRecorder _recorder;
    private readonly Stopwatch _clock = new Stopwatch();
    private long _framesPushed;
    private bool _completed;
    private bool _disposed;

    internal MfFramePathRecorder(MfSinkWriterRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <inheritdoc/>
    public long FramesPushed => Interlocked.Read(ref _framesPushed);

    /// <inheritdoc/>
    public bool Start()
    {
        _recorder.Begin();
        _clock.Restart();
        return true;
    }

    /// <inheritdoc/>
    public bool PushFrame(IntPtr pixels, uint sourcePitchBytes)
    {
        if (_completed || _disposed)
        {
            return false;
        }
        var timestampHns = _clock.ElapsedTicks * 10_000_000L / Stopwatch.Frequency;
        if (!_recorder.WriteVideoFrame(pixels, sourcePitchBytes, timestampHns))
        {
            return false;
        }
        Interlocked.Increment(ref _framesPushed);
        return true;
    }

    /// <inheritdoc/>
    public void Complete()
    {
        _completed = true;
    }

    /// <inheritdoc/>
    public bool WaitForCompletion(TimeSpan timeout)
    {
        // The sink writer is synchronous from the caller's perspective: finalizing
        // drains the encoder and writes the MP4 index before returning.
        return _recorder.Finish();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _completed = true;
        _recorder.Dispose();
    }
}
