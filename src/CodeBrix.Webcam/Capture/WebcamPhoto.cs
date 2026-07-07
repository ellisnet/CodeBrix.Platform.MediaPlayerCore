using System;

namespace CodeBrix.Webcam.Capture;

/// <summary>
/// A captured frame-photo: tightly packed 32-bit BGRA pixels (byte order blue, green,
/// red, alpha; row stride = <see cref="Width"/> * 4, no padding), ready to hand to an
/// image library — e.g. CodeBrix.Imaging's <c>Image.LoadPixelData&lt;Bgra32&gt;</c> —
/// for encoding to PNG/JPEG or further processing. If an overlay was set on the session,
/// it is already burned in.
/// </summary>
public sealed class WebcamPhoto
{
    internal WebcamPhoto(byte[] pixelsBgra32, int width, int height, DateTime capturedAtUtc)
    {
        PixelsBgra32 = pixelsBgra32;
        Width = width;
        Height = height;
        CapturedAtUtc = capturedAtUtc;
    }

    /// <summary>The pixel data: tightly packed BGRA, <see cref="Width"/> * <see cref="Height"/> * 4 bytes.</summary>
    public byte[] PixelsBgra32 { get; }

    /// <summary>Photo width in pixels.</summary>
    public int Width { get; }

    /// <summary>Photo height in pixels.</summary>
    public int Height { get; }

    /// <summary>Bytes per row: always <see cref="Width"/> * 4 (tightly packed).</summary>
    public int StrideBytes => Width * 4;

    /// <summary>The UTC time the frame was captured.</summary>
    public DateTime CapturedAtUtc { get; }
}
