using System.Globalization;
using System.Text.RegularExpressions;
using CodeBrix.Webcam.Devices;

namespace CodeBrix.Webcam.Internal.Darwin;

/// <summary>
/// Pure string/number mapping helpers for the macOS device provider — no interop, so
/// they are unit-testable on every platform. AVFoundation reports USB identity only as
/// text inside the modelID (e.g. "UVC Camera VendorID_1133 ProductID_2140", decimal),
/// and CoreMedia reports formats as four-character codes; these methods translate both
/// into the shared CodeBrix.Webcam device model.
/// </summary>
internal static class DarwinDeviceInfoParser
{
    private static readonly Regex VendorProductPattern = new Regex(
        @"VendorID_(\d+)\s+ProductID_(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// Best-effort extraction of the USB vendor/product ids from an AVCaptureDevice
    /// modelID string; (0, 0) when the modelID does not carry them.
    /// </summary>
    internal static (ushort VendorId, ushort ProductId) ParseVendorProduct(string modelId)
    {
        var match = VendorProductPattern.Match(modelId ?? string.Empty);
        if (match.Success
            && ushort.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var vendor)
            && ushort.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var product))
        {
            return (vendor, product);
        }
        return (0, 0);
    }

    /// <summary>
    /// Maps a CoreMedia media subtype (a four-character code packed into a UInt32) to
    /// the shared capability representation: the printable four-character code (or a
    /// hex rendering when unprintable) plus the well-known pixel format when one applies.
    /// </summary>
    internal static (string FourCc, ImagingPixelFormat PixelFormat) MapSubtype(uint subtype)
    {
        var fourCc = FourCcToString(subtype);
        var pixelFormat = fourCc switch
        {
            // Apple's MJPEG flavors: 'dmb1' (OpenDML), 'jpeg', 'mjpg'.
            "dmb1" or "jpeg" or "mjpg" or "MJPG" => ImagingPixelFormat.Mjpeg,
            // 'yuvs' is kCVPixelFormatType_422YpCbCr8_yuvs — the YUY2/YUYV byte order.
            "yuvs" or "yuv2" or "YUY2" => ImagingPixelFormat.Yuyv,
            // '420v'/'420f' are the bi-planar 4:2:0 formats — the NV12 layout.
            "420v" or "420f" => ImagingPixelFormat.Nv12,
            "avc1" or "h264" or "H264" => ImagingPixelFormat.H264,
            "BGRA" or "RGBA" => ImagingPixelFormat.Rgb32,
            "24RG" or "24BG" => ImagingPixelFormat.Rgb24,
            "L008" => ImagingPixelFormat.Grey,
            _ => ImagingPixelFormat.Unknown,
        };
        return (fourCc, pixelFormat);
    }

    /// <summary>
    /// Renders a packed four-character code as text (most-significant byte first), or
    /// as "0x????????" when any character is not printable ASCII.
    /// </summary>
    internal static string FourCcToString(uint fourCc)
    {
        var characters = new[]
        {
            (char)((fourCc >> 24) & 0xFF),
            (char)((fourCc >> 16) & 0xFF),
            (char)((fourCc >> 8) & 0xFF),
            (char)(fourCc & 0xFF),
        };
        foreach (var character in characters)
        {
            if (character < 0x20 || character > 0x7E)
            {
                return "0x" + fourCc.ToString("X8", CultureInfo.InvariantCulture);
            }
        }
        return new string(characters);
    }

    /// <summary>
    /// Renders an AVCaptureDevice transportType (a four-character code like 'usb ' or
    /// 'bltn') as trimmed text for <see cref="ImagingDeviceHardwareInfo.BusInfo"/>;
    /// null when the device reports none.
    /// </summary>
    internal static string TransportTypeToString(int transportType)
    {
        if (transportType == 0)
        {
            return null;
        }
        var text = FourCcToString(unchecked((uint)transportType)).Trim();
        return text.Length == 0 ? null : text;
    }
}
