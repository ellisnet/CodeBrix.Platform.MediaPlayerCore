using CodeBrix.Webcam.Devices;
using CodeBrix.Webcam.Internal.Darwin;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Webcam.Tests;

/// <summary>
/// The macOS provider's pure mapping helpers (modelID → USB ids, CoreMedia subtype →
/// fourcc/pixel format) have no interop, so they are verified on every platform.
/// </summary>
public class DarwinDeviceInfoParserTests
{
    private static uint FourCc(string code)
        => (uint)((code[0] << 24) | (code[1] << 16) | (code[2] << 8) | code[3]);

    [Fact]
    public void Vendor_and_product_ids_are_parsed_from_a_uvc_model_id()
    {
        // AVFoundation reports the ids in decimal; 1133/2140 is a Logitech C922.
        var (vendorId, productId) = DarwinDeviceInfoParser.ParseVendorProduct(
            "UVC Camera VendorID_1133 ProductID_2140");
        vendorId.Should().Be((ushort)0x046D);
        productId.Should().Be((ushort)0x085C);
    }

    [Theory]
    [InlineData("FaceTime HD Camera")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("VendorID_99999 ProductID_1")] // out of ushort range → best effort gives up
    public void Model_ids_without_usable_usb_identity_yield_zero(string modelId)
    {
        var (vendorId, productId) = DarwinDeviceInfoParser.ParseVendorProduct(modelId);
        vendorId.Should().Be((ushort)0);
        productId.Should().Be((ushort)0);
    }

    [Theory]
    [InlineData("dmb1", ImagingPixelFormat.Mjpeg)]
    [InlineData("jpeg", ImagingPixelFormat.Mjpeg)]
    [InlineData("yuvs", ImagingPixelFormat.Yuyv)]
    [InlineData("420v", ImagingPixelFormat.Nv12)]
    [InlineData("420f", ImagingPixelFormat.Nv12)]
    [InlineData("avc1", ImagingPixelFormat.H264)]
    [InlineData("BGRA", ImagingPixelFormat.Rgb32)]
    [InlineData("L008", ImagingPixelFormat.Grey)]
    public void Known_coremedia_subtypes_map_to_well_known_pixel_formats(
        string code, ImagingPixelFormat expected)
    {
        var (fourCc, pixelFormat) = DarwinDeviceInfoParser.MapSubtype(FourCc(code));
        fourCc.Should().Be(code);
        pixelFormat.Should().Be(expected);
    }

    [Fact]
    public void Unknown_printable_subtypes_keep_their_fourcc()
    {
        var (fourCc, pixelFormat) = DarwinDeviceInfoParser.MapSubtype(FourCc("2vuy"));
        fourCc.Should().Be("2vuy");
        pixelFormat.Should().Be(ImagingPixelFormat.Unknown);
    }

    [Fact]
    public void Unprintable_subtypes_render_as_hex_and_are_never_empty()
    {
        // kCVPixelFormatType_24RGB is the raw number 24, not a printable code.
        var (fourCc, pixelFormat) = DarwinDeviceInfoParser.MapSubtype(24);
        fourCc.Should().Be("0x00000018");
        pixelFormat.Should().Be(ImagingPixelFormat.Unknown);
    }

    [Fact]
    public void Transport_types_render_trimmed_or_null()
    {
        DarwinDeviceInfoParser.TransportTypeToString(unchecked((int)FourCc("usb "))).Should().Be("usb");
        DarwinDeviceInfoParser.TransportTypeToString(unchecked((int)FourCc("bltn"))).Should().Be("bltn");
        DarwinDeviceInfoParser.TransportTypeToString(0).Should().BeNull();
    }
}
