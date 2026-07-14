using System;
using CodeBrix.Webcam.Capture;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Webcam.Tests;

public class WebcamPhotoTests
{
    //A 3x2 photo whose pixels are numbered by column so mirroring is easy to verify:
    //  each pixel's blue byte carries its (x + 1), the alpha carries its (y + 1)
    private static WebcamPhoto CreateTestPhoto()
    {
        const int width = 3;
        const int height = 2;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;
                pixels[offset] = (byte)(x + 1);       //blue = column marker
                pixels[offset + 3] = (byte)(y + 1);   //alpha = row marker
            }
        }
        return new WebcamPhoto(pixels, width, height, new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void FlipHorizontal_reverses_each_row()
    {
        //Arrange
        var photo = CreateTestPhoto();

        //Act
        var flipped = photo.FlipHorizontal();

        //Assert - column markers read 3, 2, 1 in every row; row markers are untouched
        for (var y = 0; y < flipped.Height; y++)
        {
            var rowStart = y * flipped.StrideBytes;
            flipped.PixelsBgra32[rowStart].Should().Be((byte)3);
            flipped.PixelsBgra32[rowStart + 4].Should().Be((byte)2);
            flipped.PixelsBgra32[rowStart + 8].Should().Be((byte)1);
            flipped.PixelsBgra32[rowStart + 3].Should().Be((byte)(y + 1));
        }
    }

    [Fact]
    public void FlipHorizontal_returns_a_new_photo_and_leaves_the_original_unmodified()
    {
        //Arrange
        var photo = CreateTestPhoto();

        //Act
        var flipped = photo.FlipHorizontal();

        //Assert
        ReferenceEquals(photo, flipped).Should().BeFalse();
        ReferenceEquals(photo.PixelsBgra32, flipped.PixelsBgra32).Should().BeFalse();
        photo.PixelsBgra32[0].Should().Be((byte)1);   //original still reads 1, 2, 3
    }

    [Fact]
    public void FlipHorizontal_preserves_dimensions_and_timestamp()
    {
        //Arrange
        var photo = CreateTestPhoto();

        //Act
        var flipped = photo.FlipHorizontal();

        //Assert
        flipped.Width.Should().Be(photo.Width);
        flipped.Height.Should().Be(photo.Height);
        flipped.StrideBytes.Should().Be(photo.StrideBytes);
        flipped.CapturedAtUtc.Should().Be(photo.CapturedAtUtc);
    }

    [Fact]
    public void FlipHorizontal_twice_restores_the_original_pixels()
    {
        //Arrange
        var photo = CreateTestPhoto();

        //Act
        var roundTripped = photo.FlipHorizontal().FlipHorizontal();

        //Assert
        roundTripped.PixelsBgra32.Should().Equal(photo.PixelsBgra32);
    }
}
