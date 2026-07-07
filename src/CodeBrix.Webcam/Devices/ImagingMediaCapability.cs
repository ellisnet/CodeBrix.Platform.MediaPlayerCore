using System.Collections.Generic;

namespace CodeBrix.Webcam.Devices;

/// <summary>
/// One entry of a device's capability matrix: a pixel format at a specific resolution,
/// with the frame rates the device supports for that combination.
/// </summary>
public sealed class ImagingMediaCapability
{
    /// <summary>Creates a capability entry.</summary>
    /// <param name="pixelFormat">The well-known pixel format, or <see cref="ImagingPixelFormat.Unknown"/>.</param>
    /// <param name="fourCc">The exact four-character code of the format (e.g. "MJPG", "YUYV").</param>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="frameRates">The supported frame rates in frames per second, highest
    /// first. For a device that reports a continuous range instead of discrete rates,
    /// this holds the range endpoints and <paramref name="isFrameRateRange"/> is true.</param>
    /// <param name="isFrameRateRange">True when <paramref name="frameRates"/> represents
    /// the endpoints of a continuous range rather than an exhaustive list.</param>
    public ImagingMediaCapability(ImagingPixelFormat pixelFormat, string fourCc,
        uint width, uint height, IReadOnlyList<double> frameRates, bool isFrameRateRange)
    {
        PixelFormat = pixelFormat;
        FourCc = fourCc;
        Width = width;
        Height = height;
        FrameRates = frameRates;
        IsFrameRateRange = isFrameRateRange;
    }

    /// <summary>The well-known pixel format, or <see cref="ImagingPixelFormat.Unknown"/>.</summary>
    public ImagingPixelFormat PixelFormat { get; }

    /// <summary>The exact four-character code of the format (e.g. "MJPG", "YUYV").</summary>
    public string FourCc { get; }

    /// <summary>Frame width in pixels.</summary>
    public uint Width { get; }

    /// <summary>Frame height in pixels.</summary>
    public uint Height { get; }

    /// <summary>
    /// The supported frame rates in frames per second, highest first — or, when
    /// <see cref="IsFrameRateRange"/> is true, the endpoints of the supported range.
    /// </summary>
    public IReadOnlyList<double> FrameRates { get; }

    /// <summary>
    /// True when <see cref="FrameRates"/> represents the endpoints of a continuous range
    /// rather than an exhaustive list of discrete rates.
    /// </summary>
    public bool IsFrameRateRange { get; }

    /// <summary>Renders the capability as e.g. "MJPG 1920x1080 @ 30, 24, 15 fps".</summary>
    public override string ToString()
        => $"{FourCc} {Width}x{Height} @ {string.Join(", ", FrameRates)}{(IsFrameRateRange ? " (range)" : string.Empty)} fps";
}
