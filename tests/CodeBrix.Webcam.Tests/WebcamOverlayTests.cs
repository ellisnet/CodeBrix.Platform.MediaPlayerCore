using System;
using CodeBrix.Webcam.Capture;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Webcam.Tests;

public class WebcamOverlayTests
{
    [Fact]
    public void Ctor_throws_on_null_pixels()
        => ((Action)(() => new WebcamOverlay(null, 4, 4))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Ctor_throws_on_zero_width()
        => ((Action)(() => new WebcamOverlay(new byte[64], 0, 4))).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Ctor_throws_on_zero_height()
        => ((Action)(() => new WebcamOverlay(new byte[64], 4, 0))).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Ctor_throws_on_undersized_buffer()
        => ((Action)(() => new WebcamOverlay(new byte[15], 2, 2))).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Ctor_throws_on_stride_smaller_than_row()
        => ((Action)(() => new WebcamOverlay(new byte[64], 4, 4, 8))).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Pixels_are_copied_so_source_mutation_does_not_affect_the_overlay()
    {
        //Arrange
        var source = new byte[2 * 2 * 4];
        source[0] = 0x11;

        //Act
        var overlay = new WebcamOverlay(source, 2, 2);
        source[0] = 0x99;

        //Assert
        overlay.Pixels[0].Should().Be((byte)0x11);
        overlay.Width.Should().Be(2);
        overlay.Height.Should().Be(2);
    }

    [Fact]
    public void Padded_source_stride_is_repacked_tightly()
    {
        //Arrange: 2x2 overlay with 12-byte stride (8 bytes of pixels + 4 padding per row).
        var source = new byte[12 * 2];
        source[0] = 0xAA;          // row 0, pixel 0, B
        source[12] = 0xBB;         // row 1, pixel 0, B

        //Act
        var overlay = new WebcamOverlay(source, 2, 2, 12);

        //Assert
        overlay.Pixels.Length.Should().Be(16);
        overlay.Pixels[0].Should().Be((byte)0xAA);
        overlay.Pixels[8].Should().Be((byte)0xBB);
    }
}
