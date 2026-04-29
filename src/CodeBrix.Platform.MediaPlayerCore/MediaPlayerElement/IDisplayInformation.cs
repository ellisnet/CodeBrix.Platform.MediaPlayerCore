#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

namespace CodeBrix.Platform.MediaPlayerCore.MediaPlayerElement; //was previously: LibVLCSharp.Shared.MediaPlayerElement;

/// <summary>
/// Interface to get display-related information for an application view
/// </summary>
internal interface IDisplayInformation
{
    /// <summary>
    /// Gets the scale factor
    /// </summary>
    double ScalingFactor { get; }
}
