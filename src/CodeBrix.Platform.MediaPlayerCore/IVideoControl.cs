#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;

namespace CodeBrix.Platform.MediaPlayerCore; //was previously: LibVLCSharp.Shared;

/// <summary>
/// Interface for video control
/// </summary>
internal interface IVideoControl : IVideoView
{
    /// <summary>
    /// Occurs when the size of the control changes
    /// </summary>
    event EventHandler SizeChanged;

    /// <summary>
    /// Gets the width of the video view
    /// </summary>
    double Width { get; }

    /// <summary>
    /// Gets the height of the video view
    /// </summary>
    double Height { get; }
}
