using CodeBrix.Webcam.Internal.Windows;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Webcam.Tests;

/// <summary>
/// The Windows backend matches the DirectShow-enumerated device to its Media Foundation
/// activate by device-interface path. These are the pure string helpers behind that
/// match, so they are verified on every platform. The sample paths are real ones: a
/// Logitech UVC webcam, and the Surface Pro X front/rear cameras — two filter factories
/// on ONE PnP instance (the Qualcomm camera subsystem), distinguishable only by the
/// reference string after the interface-class GUID.
/// </summary>
#pragma warning disable CA1416 // pure string helpers; no Windows interop involved
public class MediaFoundationDeviceMatchTests
{
    private const string DshowUvc =
        @"\\?\usb#vid_046d&pid_0944&mi_00#7&10e98aea&0&0000#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\global";
    private const string MfUvc =
        @"\\?\usb#vid_046d&pid_0944&mi_00#7&10e98aea&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\GLOBAL";

    private const string DshowSurfaceFront =
        @"\\?\display#qcom_avstream_8180#3&2a0541de&0&uid32768#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\{4faeafd4-041b-4e46-85fd-400473891182}";
    private const string DshowSurfaceRear =
        @"\\?\display#qcom_avstream_8180#3&2a0541de&0&uid32768#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\{5584f823-3830-4cfd-947f-78de17a8b14c}";
    private const string MfSurfaceFront =
        @"\\?\display#qcom_avstream_8180#3&2a0541de&0&uid32768#{e5323777-f976-4f5b-9b55-b94699c46e44}\{4faeafd4-041b-4e46-85fd-400473891182}";
    private const string MfSurfaceRear =
        @"\\?\display#qcom_avstream_8180#3&2a0541de&0&uid32768#{e5323777-f976-4f5b-9b55-b94699c46e44}\{5584f823-3830-4cfd-947f-78de17a8b14c}";

    [Fact]
    public void Match_key_pairs_the_dshow_and_mf_paths_of_a_uvc_camera()
    {
        MediaFoundationCaptureBackend.DeviceMatchKey(DshowUvc)
            .Should().BeEquivalentTo(MediaFoundationCaptureBackend.DeviceMatchKey(MfUvc));
    }

    [Fact]
    public void Match_key_pairs_each_shared_instance_camera_with_itself()
    {
        MediaFoundationCaptureBackend.DeviceMatchKey(DshowSurfaceFront)
            .Should().BeEquivalentTo(MediaFoundationCaptureBackend.DeviceMatchKey(MfSurfaceFront));
        MediaFoundationCaptureBackend.DeviceMatchKey(DshowSurfaceRear)
            .Should().BeEquivalentTo(MediaFoundationCaptureBackend.DeviceMatchKey(MfSurfaceRear));
    }

    [Fact]
    public void Match_key_distinguishes_cameras_sharing_one_pnp_instance()
    {
        // The regression this guards: InstanceSegment alone collapses front and rear
        // to the same key, so the capture backend always opened the first-enumerated
        // camera no matter which one was selected.
        MediaFoundationCaptureBackend.DeviceMatchKey(DshowSurfaceFront)
            .Should().NotBe(MediaFoundationCaptureBackend.DeviceMatchKey(MfSurfaceRear));
    }

    [Fact]
    public void Instance_segment_still_collapses_the_shared_instance()
    {
        // Documents why InstanceSegment is only a fallback tier.
        MediaFoundationCaptureBackend.InstanceSegment(DshowSurfaceFront)
            .Should().Be(MediaFoundationCaptureBackend.InstanceSegment(MfSurfaceRear));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Null_or_empty_paths_yield_null_keys(string path)
    {
        MediaFoundationCaptureBackend.DeviceMatchKey(path).Should().BeNull();
        MediaFoundationCaptureBackend.InstanceSegment(path).Should().BeNull();
    }

    [Fact]
    public void A_path_with_no_interface_guid_is_its_own_key()
    {
        // DirectShow's DevicePath can be absent, in which case the friendly name is
        // used as the device id — the key must pass it through unchanged.
        MediaFoundationCaptureBackend.DeviceMatchKey("Surface Camera Front")
            .Should().Be("Surface Camera Front");
    }
}
#pragma warning restore CA1416
