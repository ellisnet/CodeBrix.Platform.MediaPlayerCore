using System;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// One decoded video frame delivered by an <see cref="ICaptureBackend"/>: 32-bit BGRA
/// pixels (byte order blue, green, red, alpha; alpha opaque), top-down, with
/// <see cref="PitchBytes"/> bytes per scanline.
/// <para/>
/// Raised on an internal capture thread. The pixel buffer at <see cref="Plane"/> is
/// owned by the backend and is only valid until the handler returns. For efficiency the
/// same event-args instance is reused for every frame, so handlers must not retain it.
/// </summary>
internal sealed class CaptureFrameEventArgs : EventArgs
{
    internal CaptureFrameEventArgs()
    {
    }

    /// <summary>Pointer to the top-left pixel; valid only until the handler returns.</summary>
    public IntPtr Plane { get; private set; }

    /// <summary>Frame width in pixels.</summary>
    public uint Width { get; private set; }

    /// <summary>Frame height in pixels.</summary>
    public uint Height { get; private set; }

    /// <summary>Bytes per scanline of <see cref="Plane"/> (at least <see cref="Width"/> * 4).</summary>
    public uint PitchBytes { get; private set; }

    /// <summary>Points the reusable instance at the next frame.</summary>
    public void Update(IntPtr plane, uint width, uint height, uint pitchBytes)
    {
        Plane = plane;
        Width = width;
        Height = height;
        PitchBytes = pitchBytes;
    }
}
