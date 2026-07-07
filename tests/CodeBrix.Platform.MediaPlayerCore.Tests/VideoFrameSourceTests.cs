using System;
using System.IO;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Platform.MediaPlayerCore.Tests;

public class VideoFrameSourceTests : BaseSetup
{
    [Fact]
    public void Ctor_throws_on_null_libvlc()
        => ((Action)(() => new VideoFrameSource(null, 64, 64, 30))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Ctor_throws_on_zero_width()
        => ((Action)(() => new VideoFrameSource(_libVLC, 0, 64, 30))).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Ctor_throws_on_zero_height()
        => ((Action)(() => new VideoFrameSource(_libVLC, 64, 0, 30))).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Ctor_throws_on_zero_frame_rate()
        => ((Action)(() => new VideoFrameSource(_libVLC, 64, 64, 0))).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Properties_reflect_ctor_arguments()
    {
        //Arrange + Act
        using var source = new VideoFrameSource(_libVLC, 128, 96, 30);

        //Assert
        source.Width.Should().Be((uint)128);
        source.Height.Should().Be((uint)96);
        source.FrameRate.Should().Be((uint)30);
        source.PitchBytes.Should().Be((uint)(128 * 4));
        source.FramesPushed.Should().Be(0L);
        source.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void Push_frame_before_start_throws()
    {
        //Arrange
        using var source = new VideoFrameSource(_libVLC, 64, 64, 30);
        var frame = new byte[64 * 64 * 4];

        //Act + Assert
        ((Action)(() => source.PushFrame(frame))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Push_frame_throws_on_undersized_managed_buffer()
    {
        //Arrange
        using var source = new VideoFrameSource(_libVLC, 64, 64, 30);
        var tooSmall = new byte[16];

        //Act + Assert
        ((Action)(() => source.PushFrame(tooSmall))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Start_twice_throws()
    {
        //Arrange
        using var source = new VideoFrameSource(_libVLC, 64, 64, 30, ":sout=#dummy");
        source.Start();

        //Act + Assert
        ((Action)(() => source.Start())).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_without_start_does_not_throw()
    {
        //Arrange
        var source = new VideoFrameSource(_libVLC, 64, 64, 30);

        //Act + Assert
        ((Action)(() => source.Dispose())).Should().NotThrow();
        ((Action)(() => source.Dispose())).Should().NotThrow(); // double-dispose is safe
    }

    [Fact]
    public void Is_supported_on_a_host_with_the_imem_plugin()
        => VideoFrameSource.IsSupported(_libVLC).Should().BeTrue();

    [Fact]
    public void Ensure_supported_does_not_throw_on_a_host_with_the_imem_plugin()
        => ((Action)(() => VideoFrameSource.EnsureSupported(_libVLC))).Should().NotThrow();

#if ENABLE_PUSHED_FRAMES_TESTS

    [Fact]
    public void Pushed_frames_encode_to_a_playable_mp4_file()
    {
        //Arrange
        var outPath = Path.Combine(Path.GetTempPath(), $"vfs_test_{Guid.NewGuid():N}.mp4");
        const uint width = 128;
        const uint height = 96;
        const int frameCount = 30;
        var frame = new byte[width * height * 4];
        try
        {
            // tune=zerolatency: see the VideoFrameSource class docs — without it, x264's
            // rate-control lookahead swallows short streams whole and the file comes out empty.
            using var source = new VideoFrameSource(_libVLC, width, height, 30,
                ":sout=#transcode{vcodec=h264,vb=800,venc=x264{tune=zerolatency}}:standard{access=file,mux=mp4,dst=" + outPath + "}");

            //Act
            source.Start().Should().BeTrue();
            for (var i = 0; i < frameCount; i++)
            {
                for (var p = 0; p < frame.Length; p += 4)
                {
                    frame[p] = (byte)(i * 8);       // B animates per frame
                    frame[p + 1] = 0x40;            // G
                    frame[p + 2] = (byte)(255 - (i * 8)); // R
                    frame[p + 3] = 0xFF;
                }
                source.PushFrame(frame, i * 33333L).Should().BeTrue();
            }
            source.Complete();
            var finished = source.WaitForCompletion(TimeSpan.FromSeconds(30));

            //Assert
            finished.Should().BeTrue();
            source.FramesPushed.Should().Be((long)frameCount);
            source.IsFinished.Should().BeTrue();
            File.Exists(outPath).Should().BeTrue();
            new FileInfo(outPath).Length.Should().BeGreaterThan(1000L);
        }
        finally
        {
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public void Pushed_frames_with_padded_pitch_encode_successfully()
    {
        //Arrange
        var outPath = Path.Combine(Path.GetTempPath(), $"vfs_pitch_test_{Guid.NewGuid():N}.mp4");
        const uint width = 100; // 400-byte packed rows...
        const uint height = 60;
        const uint paddedPitch = 416; // ...but source rows padded to a 32-byte multiple
        var paddedFrame = new byte[paddedPitch * height];
        try
        {
            using var source = new VideoFrameSource(_libVLC, width, height, 30,
                ":sout=#transcode{vcodec=h264,vb=800,venc=x264{tune=zerolatency}}:standard{access=file,mux=mp4,dst=" + outPath + "}");

            //Act
            source.Start().Should().BeTrue();
            unsafe
            {
                fixed (byte* p = paddedFrame)
                {
                    for (var i = 0; i < 15; i++)
                    {
                        source.PushFrame((IntPtr)p, paddedPitch, i * 33333L).Should().BeTrue();
                    }
                }
            }
            source.Complete();
            var finished = source.WaitForCompletion(TimeSpan.FromSeconds(30));

            //Assert
            finished.Should().BeTrue();
            File.Exists(outPath).Should().BeTrue();
            new FileInfo(outPath).Length.Should().BeGreaterThan(500L);
        }
        finally
        {
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

#endif
}
