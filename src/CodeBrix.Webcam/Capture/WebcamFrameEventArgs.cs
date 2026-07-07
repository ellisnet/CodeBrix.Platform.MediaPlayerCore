using System;
using System.Runtime.InteropServices;

namespace CodeBrix.Webcam.Capture;

/// <summary>
/// One live video frame from a <see cref="WebcamSession"/>: 32-bit BGRA pixels in CPU
/// memory (byte order blue, green, red, alpha; alpha opaque), which maps directly onto
/// common bitmap formats such as SkiaSharp's Bgra8888 and WPF's Bgra32.
/// <para/>
/// Raised on an internal capture thread. The pixel buffer is only valid until the
/// handler returns — copy it (e.g. via <see cref="CopyTo(byte[])"/>) before returning,
/// keep handlers fast, and do not touch UI objects directly from the handler.
/// </summary>
public sealed class WebcamFrameEventArgs : EventArgs
{
    internal WebcamFrameEventArgs()
    {
    }

    /// <summary>Frame width in pixels.</summary>
    public uint Width { get; private set; }

    /// <summary>Frame height in pixels.</summary>
    public uint Height { get; private set; }

    /// <summary>Bytes per scanline of <see cref="PixelPlane"/> (at least <see cref="Width"/> * 4).</summary>
    public uint PitchBytes { get; private set; }

    /// <summary>Pointer to the top-left pixel; valid only until the handler returns.</summary>
    public IntPtr PixelPlane { get; private set; }

    /// <summary>The UTC time this frame was delivered.</summary>
    public DateTime TimestampUtc { get; private set; }

    internal void Update(IntPtr plane, uint width, uint height, uint pitchBytes)
    {
        PixelPlane = plane;
        Width = width;
        Height = height;
        PitchBytes = pitchBytes;
        TimestampUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Copies the frame into a tightly packed BGRA buffer (row stride =
    /// <see cref="Width"/> * 4, no padding), the layout image libraries expect.
    /// </summary>
    /// <param name="destination">A buffer of at least <see cref="Width"/> * <see cref="Height"/> * 4 bytes.</param>
    public void CopyTo(byte[] destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        var packedRow = (int)(Width * 4);
        if (destination.Length < packedRow * Height)
        {
            throw new ArgumentOutOfRangeException(nameof(destination), destination.Length,
                $"Destination must hold at least {packedRow * Height} bytes");
        }
        for (var y = 0; y < Height; y++)
        {
            Marshal.Copy(PixelPlane + (int)(y * PitchBytes), destination, y * packedRow, packedRow);
        }
    }
}
