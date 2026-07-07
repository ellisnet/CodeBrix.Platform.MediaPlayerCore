using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Webcam.Capture;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Webcam.Tests;

/// <summary>
/// Tests that open a REAL camera. Opt-in: they need a physical webcam, a desktop
/// session, and exclusive access to the device — set
/// CODEBRIX_WEBCAM_RUN_CAMERA_TESTS=1 to run them.
/// </summary>
public class LiveCameraTests
{
    public static bool CanRunCameraTests
        => Environment.GetEnvironmentVariable("CODEBRIX_WEBCAM_RUN_CAMERA_TESTS") is "1" or "true";

    private const string SkipReason =
        "Needs a physical webcam; set CODEBRIX_WEBCAM_RUN_CAMERA_TESTS=1 to run";

    [Fact(Skip = SkipReason, SkipUnless = nameof(CanRunCameraTests), SkipType = typeof(LiveCameraTests))]
    public async Task At_least_one_capture_device_is_found()
    {
        var devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
        devices.Count.Should().BeGreaterThan(0);
        devices[0].Capabilities.Count.Should().BeGreaterThan(0);
        devices[0].Controls.Count.Should().BeGreaterThan(0);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(CanRunCameraTests), SkipType = typeof(LiveCameraTests))]
    public async Task Session_delivers_live_frames_and_captures_a_photo()
    {
        //Arrange
        var devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
        devices.Count.Should().BeGreaterThan(0);
        using var session = new WebcamSession(devices[0]);
        var frameSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        uint width = 0, height = 0;
        session.FrameReceived += (_, frame) =>
        {
            width = frame.Width;
            height = frame.Height;
            frameSeen.TrySetResult(true);
        };

        //Act
        session.Start();
        var winner = await Task.WhenAny(frameSeen.Task,
            Task.Delay(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));

        //Assert
        winner.Should().BeSameAs((object)frameSeen.Task);
        width.Should().BeGreaterThan((uint)0);
        height.Should().BeGreaterThan((uint)0);

        var photo = session.CapturePhoto(TimeSpan.FromSeconds(5));
        photo.Width.Should().Be((int)width);
        photo.Height.Should().Be((int)height);
        photo.PixelsBgra32.Length.Should().Be((int)(width * height * 4));
        // A live camera frame is essentially never all-zero.
        photo.PixelsBgra32.Any(b => b != 0).Should().BeTrue();
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(CanRunCameraTests), SkipType = typeof(LiveCameraTests))]
    public async Task Overlay_recording_produces_an_mp4_via_the_frame_path()
    {
        //Arrange
        var devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
        devices.Count.Should().BeGreaterThan(0);
        var outPath = Path.Combine(Path.GetTempPath(), $"webcam_overlay_test_{Guid.NewGuid():N}.mp4");
        try
        {
            using var session = new WebcamSession(devices[0],
                new WebcamSessionOptions { AudioCapture = AudioCaptureMode.Off });
            var frameSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.FrameReceived += (_, _) => frameSeen.TrySetResult(true);
            session.Start();
            (await Task.WhenAny(frameSeen.Task,
                Task.Delay(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken)))
                .Should().BeSameAs((object)frameSeen.Task);

            session.IsOverlayRecordingSupported.Should().BeTrue();

            // A frame-sized overlay: a translucent white banner across the top rows.
            var width = (int)session.FrameWidth;
            var height = (int)session.FrameHeight;
            var overlayPixels = new byte[width * height * 4];
            for (var i = 0; i < width * 40 * 4 && i < overlayPixels.Length; i += 4)
            {
                overlayPixels[i] = 0xFF;
                overlayPixels[i + 1] = 0xFF;
                overlayPixels[i + 2] = 0xFF;
                overlayPixels[i + 3] = 0xA0;
            }
            session.SetOverlay(new WebcamOverlay(overlayPixels, width, height));

            //Act
            session.StartRecording(new WebcamRecordingOptions(outPath));
            session.IsRecording.Should().BeTrue();
            Thread.Sleep(3000);
            var result = session.StopRecording();

            //Assert
            result.VideoFilePath.Should().Be(outPath);
            result.AudioFilePath.Should().BeNull();
            result.FramesRecorded.Should().BeGreaterThan(10L);
            File.Exists(outPath).Should().BeTrue();
            new FileInfo(outPath).Length.Should().BeGreaterThan(10_000L);
        }
        finally
        {
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }
}
