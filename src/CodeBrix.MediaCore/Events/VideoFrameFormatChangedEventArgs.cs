using System;

namespace CodeBrix.Platform.MediaPlayerCore;

/// <summary>
/// Describes the negotiated video format announced by <see cref="VideoFrameSink.FormatChanged"/>.
/// Frames delivered after this event are 32-bit BGRA with these dimensions, until the next
/// <see cref="VideoFrameSink.FormatChanged"/> event.
/// </summary>
public sealed class VideoFrameFormatChangedEventArgs : EventArgs
{
    internal VideoFrameFormatChangedEventArgs(uint width, uint height, uint pitchBytes, uint lines)
    {
        Width = width;
        Height = height;
        PitchBytes = pitchBytes;
        Lines = lines;
    }

    /// <summary>
    /// Pixel width of the video.
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Pixel height of the video.
    /// </summary>
    public uint Height { get; }

    /// <summary>
    /// Bytes per scanline (at least <see cref="Width"/> * 4; rounded up to a 32-byte multiple).
    /// </summary>
    public uint PitchBytes { get; }

    /// <summary>
    /// Scanlines allocated per buffer (at least <see cref="Height"/>; rounded up to a
    /// 32-line multiple). Each pixel buffer is <see cref="PitchBytes"/> * <see cref="Lines"/>
    /// bytes, of which the first <see cref="Height"/> scanlines carry picture data.
    /// </summary>
    public uint Lines { get; }
}
