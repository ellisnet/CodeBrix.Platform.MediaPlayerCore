using System;
using CodeBrix.Webcam.Capture;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// Straight-alpha src-over compositing of a <see cref="WebcamOverlay"/> onto BGRA video
/// frames. Pure managed pixel math — no image-library dependency.
/// </summary>
internal static unsafe class OverlayCompositor
{
    /// <summary>
    /// Blends <paramref name="overlay"/> over the source frame, writing the result to
    /// <paramref name="destination"/> (which may have the same pitch as the source).
    /// The overlay dimensions must equal the frame dimensions.
    /// </summary>
    /// <param name="source">The source frame's top-left pixel.</param>
    /// <param name="sourcePitchBytes">The source frame's bytes per scanline.</param>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="overlay">The overlay to blend (straight alpha).</param>
    /// <param name="destination">The destination buffer's top-left pixel.</param>
    /// <param name="destinationPitchBytes">The destination's bytes per scanline.</param>
    internal static void Blend(IntPtr source, uint sourcePitchBytes, uint width, uint height,
        WebcamOverlay overlay, IntPtr destination, uint destinationPitchBytes)
    {
        fixed (byte* overlayBase = overlay.Pixels)
        {
            var overlayRowBytes = overlay.Width * 4;
            for (var y = 0; y < height; y++)
            {
                var src = (byte*)source + ((long)y * sourcePitchBytes);
                var dst = (byte*)destination + ((long)y * destinationPitchBytes);
                var ovl = overlayBase + ((long)y * overlayRowBytes);
                for (var x = 0; x < width; x++)
                {
                    var alpha = ovl[3];
                    if (alpha == 0)
                    {
                        dst[0] = src[0];
                        dst[1] = src[1];
                        dst[2] = src[2];
                        dst[3] = 0xFF;
                    }
                    else if (alpha == 255)
                    {
                        dst[0] = ovl[0];
                        dst[1] = ovl[1];
                        dst[2] = ovl[2];
                        dst[3] = 0xFF;
                    }
                    else
                    {
                        var inverse = 255 - alpha;
                        dst[0] = (byte)(((ovl[0] * alpha) + (src[0] * inverse) + 127) / 255);
                        dst[1] = (byte)(((ovl[1] * alpha) + (src[1] * inverse) + 127) / 255);
                        dst[2] = (byte)(((ovl[2] * alpha) + (src[2] * inverse) + 127) / 255);
                        dst[3] = 0xFF;
                    }
                    src += 4;
                    dst += 4;
                    ovl += 4;
                }
            }
        }
    }
}
