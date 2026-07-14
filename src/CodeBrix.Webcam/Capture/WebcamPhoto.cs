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

    /// <summary>
    /// Returns a NEW photo whose pixels are flipped left-to-right — so a webcam still
    /// reads like a mirror, matching the mirrored ("selfie") live preview the user was
    /// watching when the photo was taken. This photo is not modified; the new photo
    /// keeps the same dimensions and <see cref="CapturedAtUtc"/>.
    /// </summary>
    /// <returns>A horizontally mirrored copy of this photo.</returns>
    public WebcamPhoto FlipHorizontal()
    {
        var flipped = new byte[PixelsBgra32.Length];
        var lastPixelOffset = (Width - 1) * 4;
        for (var y = 0; y < Height; y++)
        {
            var rowStart = y * StrideBytes;
            var sourceOffset = rowStart;
            var targetOffset = rowStart + lastPixelOffset;
            for (var x = 0; x < Width; x++)
            {
                flipped[targetOffset] = PixelsBgra32[sourceOffset];
                flipped[targetOffset + 1] = PixelsBgra32[sourceOffset + 1];
                flipped[targetOffset + 2] = PixelsBgra32[sourceOffset + 2];
                flipped[targetOffset + 3] = PixelsBgra32[sourceOffset + 3];
                sourceOffset += 4;
                targetOffset -= 4;
            }
        }
        return new WebcamPhoto(flipped, Width, Height, CapturedAtUtc);
    }
}
