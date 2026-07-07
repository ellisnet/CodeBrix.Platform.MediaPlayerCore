using System;
using System.Runtime.InteropServices;
using CodeBrix.Webcam.Capture;
using CodeBrix.Webcam.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Webcam.Tests;

public class OverlayCompositorTests
{
    private static (IntPtr Source, IntPtr Destination) AllocateFrames(byte[] sourcePixels)
    {
        var source = Marshal.AllocHGlobal(sourcePixels.Length);
        var destination = Marshal.AllocHGlobal(sourcePixels.Length);
        Marshal.Copy(sourcePixels, 0, source, sourcePixels.Length);
        return (source, destination);
    }

    [Fact]
    public void Transparent_overlay_pixels_pass_the_source_through()
    {
        //Arrange: 1x1 red source, fully transparent overlay pixel.
        var sourcePixels = new byte[] { 0x00, 0x00, 0xFF, 0xFF }; // B G R A = red
        var overlay = new WebcamOverlay(new byte[] { 0xFF, 0xFF, 0xFF, 0x00 }, 1, 1);
        var (source, destination) = AllocateFrames(sourcePixels);
        try
        {
            //Act
            OverlayCompositor.Blend(source, 4, 1, 1, overlay, destination, 4);

            //Assert
            var result = new byte[4];
            Marshal.Copy(destination, result, 0, 4);
            result[0].Should().Be((byte)0x00);
            result[1].Should().Be((byte)0x00);
            result[2].Should().Be((byte)0xFF);
            result[3].Should().Be((byte)0xFF);
        }
        finally
        {
            Marshal.FreeHGlobal(source);
            Marshal.FreeHGlobal(destination);
        }
    }

    [Fact]
    public void Opaque_overlay_pixels_replace_the_source()
    {
        //Arrange: 1x1 red source, opaque green overlay pixel.
        var sourcePixels = new byte[] { 0x00, 0x00, 0xFF, 0xFF };
        var overlay = new WebcamOverlay(new byte[] { 0x00, 0xFF, 0x00, 0xFF }, 1, 1);
        var (source, destination) = AllocateFrames(sourcePixels);
        try
        {
            //Act
            OverlayCompositor.Blend(source, 4, 1, 1, overlay, destination, 4);

            //Assert
            var result = new byte[4];
            Marshal.Copy(destination, result, 0, 4);
            result[0].Should().Be((byte)0x00);
            result[1].Should().Be((byte)0xFF);
            result[2].Should().Be((byte)0x00);
        }
        finally
        {
            Marshal.FreeHGlobal(source);
            Marshal.FreeHGlobal(destination);
        }
    }

    [Fact]
    public void Half_transparent_overlay_pixels_blend_toward_the_overlay_color()
    {
        //Arrange: black source, white overlay at ~50% alpha (128).
        var sourcePixels = new byte[] { 0x00, 0x00, 0x00, 0xFF };
        var overlay = new WebcamOverlay(new byte[] { 0xFF, 0xFF, 0xFF, 0x80 }, 1, 1);
        var (source, destination) = AllocateFrames(sourcePixels);
        try
        {
            //Act
            OverlayCompositor.Blend(source, 4, 1, 1, overlay, destination, 4);

            //Assert: (255*128 + 0*127 + 127)/255 = 128 (with the rounding term).
            var result = new byte[4];
            Marshal.Copy(destination, result, 0, 4);
            result[0].Should().Be((byte)128);
            result[1].Should().Be((byte)128);
            result[2].Should().Be((byte)128);
            result[3].Should().Be((byte)0xFF);
        }
        finally
        {
            Marshal.FreeHGlobal(source);
            Marshal.FreeHGlobal(destination);
        }
    }
}
