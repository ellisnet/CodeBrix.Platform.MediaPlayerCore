using CodeBrix.Webcam.Devices;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Webcam.Tests;

public class WebcamSessionOptionsTests
{
    [Fact]
    public void Defaults_are_let_the_camera_choose_with_auto_audio()
    {
        //Arrange + Act
        var options = new WebcamSessionOptions();

        //Assert
        options.RequestedWidth.Should().Be((uint)0);
        options.RequestedHeight.Should().Be((uint)0);
        options.RequestedFrameRate.Should().Be(0d);
        options.PreferredFormat.Should().Be(ImagingPixelFormat.Unknown);
        options.AudioCapture.Should().Be(AudioCaptureMode.Auto);
        options.AudioDeviceId.Should().BeNull();
        options.CompositeOverlayOnPreview.Should().BeFalse();
        options.LiveCachingMs.Should().Be(100);
    }
}
