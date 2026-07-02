using System;

namespace CodeBrix.Platform.MediaPlayerCore;

/// <summary>
/// Describes a decoded video frame delivered by <see cref="VideoFrameSink.FrameReady"/>.
/// <para/>
/// The pixel buffer at <see cref="Plane"/> is owned by the sink and is only valid until
/// the event handler returns — libvlc will reuse it for an upcoming frame. Handlers must
/// copy the pixels (e.g. into an SKImage or bitmap) before returning. For efficiency the
/// same event-args instance is reused for every frame, so handlers must not retain it.
/// </summary>
public sealed class VideoFrameReadyEventArgs : EventArgs
{
    internal VideoFrameReadyEventArgs()
    {
    }

    internal void Update(IntPtr plane, uint width, uint height, uint pitchBytes)
    {
        Plane = plane;
        Width = width;
        Height = height;
        PitchBytes = pitchBytes;
    }

    /// <summary>
    /// Pointer to the first pixel of the frame: 32-bit BGRA, top-down, with
    /// <see cref="PitchBytes"/> bytes per scanline. Only valid until the handler returns.
    /// </summary>
    public IntPtr Plane { get; private set; }

    /// <summary>
    /// Pixel width of the frame.
    /// </summary>
    public uint Width { get; private set; }

    /// <summary>
    /// Pixel height of the frame. Only the first <see cref="Height"/> scanlines of the
    /// buffer contain picture data.
    /// </summary>
    public uint Height { get; private set; }

    /// <summary>
    /// Bytes per scanline (at least <see cref="Width"/> * 4; rounded up to a 32-byte multiple).
    /// </summary>
    public uint PitchBytes { get; private set; }
}
