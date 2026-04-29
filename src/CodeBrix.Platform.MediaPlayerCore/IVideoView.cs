#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

namespace CodeBrix.Platform.MediaPlayerCore; //was previously: LibVLCSharp.Shared;

/// <summary>
/// VideoView Interface
/// </summary>
public interface IVideoView
{
    /// <summary>
    /// MediaPlayer object connected to the view
    /// </summary>
    MediaPlayer? MediaPlayer { get; set; }
}
