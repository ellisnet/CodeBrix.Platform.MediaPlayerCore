using System;

namespace CodeBrix.Webcam.Capture;

/// <summary>
/// A transparent overlay to burn into a session's photos and recordings (and, when
/// <see cref="WebcamSessionOptions.CompositeOverlayOnPreview"/> is set, its preview
/// frames): 32-bit BGRA pixels with STRAIGHT (non-premultiplied) alpha, sized exactly
/// to the session's video dimensions.
/// <para/>
/// Producing the buffer: from CodeBrix.Imaging, load/render as <c>Bgra32</c> and use
/// <c>CopyPixelDataTo</c> (ImageSharp-lineage Bgra32 is straight alpha). From SkiaSharp,
/// render to a Bgra8888 surface and read pixels with <c>SKAlphaType.Unpremul</c> —
/// Skia's default is PREMULTIPLIED alpha, which blends incorrectly here.
/// <para/>
/// The pixel data is copied by the constructor, so the source buffer can be reused;
/// an instance is immutable and safe to share.
/// </summary>
public sealed class WebcamOverlay
{
    /// <summary>Creates an overlay from straight-alpha BGRA pixels.</summary>
    /// <param name="pixelsBgra32">The overlay pixels: BGRA with straight alpha.</param>
    /// <param name="width">Overlay width in pixels; must equal the session's frame width.</param>
    /// <param name="height">Overlay height in pixels; must equal the session's frame height.</param>
    /// <param name="strideBytes">The source buffer's bytes per row; pass 0 for tightly
    /// packed input (<paramref name="width"/> * 4).</param>
    public WebcamOverlay(byte[] pixelsBgra32, int width, int height, int strideBytes = 0)
    {
        if (pixelsBgra32 == null)
        {
            throw new ArgumentNullException(nameof(pixelsBgra32));
        }
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height),
                "Overlay dimensions must be greater than zero");
        }
        var packedRow = width * 4;
        var sourceStride = strideBytes == 0 ? packedRow : strideBytes;
        if (sourceStride < packedRow)
        {
            throw new ArgumentOutOfRangeException(nameof(strideBytes), strideBytes,
                $"strideBytes must be 0 (tightly packed) or at least {packedRow}");
        }
        if (pixelsBgra32.Length < ((long)sourceStride * (height - 1)) + packedRow)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelsBgra32), pixelsBgra32.Length,
                "The pixel buffer is smaller than the given dimensions and stride imply");
        }

        Width = width;
        Height = height;

        // Copy to a tightly packed private buffer, so later mutation of the caller's
        // array cannot tear frames mid-blend.
        Pixels = new byte[packedRow * height];
        for (var y = 0; y < height; y++)
        {
            Array.Copy(pixelsBgra32, (long)y * sourceStride, Pixels, (long)y * packedRow, packedRow);
        }
    }

    /// <summary>Overlay width in pixels.</summary>
    public int Width { get; }

    /// <summary>Overlay height in pixels.</summary>
    public int Height { get; }

    /// <summary>The private, tightly packed straight-alpha BGRA pixel copy.</summary>
    internal byte[] Pixels { get; }
}
