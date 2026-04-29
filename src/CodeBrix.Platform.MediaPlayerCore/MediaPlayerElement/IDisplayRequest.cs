#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

namespace CodeBrix.Platform.MediaPlayerCore.MediaPlayerElement; //was previously: LibVLCSharp.Shared.MediaPlayerElement;

/// <summary>
/// Interface for display requests
/// </summary>
internal interface IDisplayRequest
{
    /// <summary>
    /// Activates a display request
    /// </summary>
    void RequestActive();

    /// <summary>
    /// Deactivates a display request
    /// </summary>
    void RequestRelease();
}
