using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;

namespace CodeBrix.Platform.MediaPlayerCore.Tests;

public class VideoFrameSinkTests : BaseSetup
{
    private string RealMp4Path => Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "sample.mp4");

    [Fact]
    public void Ctor_throws_on_null_media_player()
        => ((Action)(() => new VideoFrameSink(null))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Ctor_throws_on_zero_buffer_count()
    {
        //Arrange
        using var mp = new MediaPlayer(_libVLC);

        //Act + Assert
        ((Action)(() => new VideoFrameSink(mp, 0))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Ctor_throws_on_too_large_buffer_count()
    {
        //Arrange
        using var mp = new MediaPlayer(_libVLC);

        //Act + Assert
        ((Action)(() => new VideoFrameSink(mp, 9))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Format_properties_are_zero_before_playback()
    {
        //Arrange
        using var mp = new MediaPlayer(_libVLC);

        //Act
        using var sink = new VideoFrameSink(mp);

        //Assert
        sink.Width.Should().Be((uint)0);
        sink.Height.Should().Be((uint)0);
        sink.PitchBytes.Should().Be((uint)0);
        sink.BufferCount.Should().Be(3);
        sink.MediaPlayer.Should().BeSameAs(mp);
    }

    [Fact]
    public void Dispose_without_playback_does_not_throw()
    {
        //Arrange
        using var mp = new MediaPlayer(_libVLC);
        var sink = new VideoFrameSink(mp);

        //Act + Assert
        ((Action)(() => sink.Dispose())).Should().NotThrow();
        ((Action)(() => sink.Dispose())).Should().NotThrow(); // double-dispose is safe
    }

    [Fact(Skip = "Needs real A/V output + network; set MEDIAPLAYERCORE_RUN_PLAYBACK_TESTS=1 to run",
          SkipUnless = nameof(BaseSetup.CanRunMediaPlaybackTests), SkipType = typeof(BaseSetup))]
    public async Task Playback_raises_format_changed_and_frame_ready_with_valid_bgra_frames()
    {
        //Arrange
        // A dedicated LibVLC instance: the shared _libVLC is created with --no-video,
        // which would suppress the video output this test exists to exercise.
        using var libVLC = new LibVLC("--no-audio");
        using var mp = new MediaPlayer(libVLC);
        using var sink = new VideoFrameSink(mp);
        // RunContinuationsAsynchronously is essential: these are completed from inside
        // libvlc callbacks, and inline continuations would otherwise run the rest of the
        // test method (including Stop()) on a libvlc thread — which deadlocks.
        var formatTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var frameTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        uint formatWidth = 0, formatHeight = 0, formatPitch = 0;
        uint frameWidth = 0, frameHeight = 0, framePitch = 0;
        var framePlaneWasValid = false;
        var frameCount = 0;

        sink.FormatChanged += (_, args) =>
        {
            formatWidth = args.Width;
            formatHeight = args.Height;
            formatPitch = args.PitchBytes;
            formatTcs.TrySetResult(true);
        };
        sink.FrameReady += (_, args) =>
        {
            if (Interlocked.Increment(ref frameCount) == 3) // let a few frames flow first
            {
                frameWidth = args.Width;
                frameHeight = args.Height;
                framePitch = args.PitchBytes;
                framePlaneWasValid = args.Plane != IntPtr.Zero;
                if (framePlaneWasValid)
                {
                    // Prove the buffer is readable for the full advertised frame size.
                    var lastScanline = new byte[args.PitchBytes];
                    Marshal.Copy(args.Plane + (int)((args.Height - 1) * args.PitchBytes),
                        lastScanline, 0, lastScanline.Length);
                }
                frameTcs.TrySetResult(true);
            }
        };

        //Act
        // input-repeat loops the short clip: under parallel test load libvlc drops late
        // frames (dropped frames never reach the display callback), so a single pass of
        // a ~1s video may deliver fewer than the 3 frames this test waits for.
        mp.Media = new Media(libVLC, RealMp4Path, FromType.FromPath, ":input-repeat=65535");
        mp.Play();
        await AwaitMediaEventAsync(formatTcs.Task);
        await AwaitMediaEventAsync(frameTcs.Task);
        mp.Stop();

        //Assert
        formatWidth.Should().BeGreaterThan((uint)0);
        formatHeight.Should().BeGreaterThan((uint)0);
        formatPitch.Should().BeGreaterThanOrEqualTo(formatWidth * 4);
        (formatPitch % 32).Should().Be((uint)0);
        frameWidth.Should().Be(formatWidth);
        frameHeight.Should().Be(formatHeight);
        framePitch.Should().Be(formatPitch);
        framePlaneWasValid.Should().BeTrue();
        sink.Width.Should().Be(formatWidth);
        sink.Height.Should().Be(formatHeight);
        sink.PitchBytes.Should().Be(formatPitch);
    }
}
