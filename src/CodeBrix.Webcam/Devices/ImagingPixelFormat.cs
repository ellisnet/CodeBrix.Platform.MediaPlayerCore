namespace CodeBrix.Webcam.Devices;

/// <summary>
/// Well-known camera pixel/stream formats. <see cref="ImagingMediaCapability.FourCc"/>
/// always carries the exact four-character code for formats not named here.
/// </summary>
public enum ImagingPixelFormat
{
    /// <summary>A format not (yet) named in this enumeration; see the capability's four-character code.</summary>
    Unknown = 0,

    /// <summary>Motion-JPEG — per-frame JPEG compression; a webcam's low-CPU high-resolution workhorse.</summary>
    Mjpeg,

    /// <summary>YUYV / YUY2 — uncompressed packed 4:2:2 YUV.</summary>
    Yuyv,

    /// <summary>NV12 — uncompressed planar 4:2:0 YUV.</summary>
    Nv12,

    /// <summary>An H.264 elementary stream straight from the camera.</summary>
    H264,

    /// <summary>Uncompressed 24-bit RGB.</summary>
    Rgb24,

    /// <summary>Uncompressed 32-bit RGB/BGRA.</summary>
    Rgb32,

    /// <summary>8-bit greyscale.</summary>
    Grey,
}
