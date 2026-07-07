namespace CodeBrix.Webcam.Capture;

/// <summary>Options for <see cref="WebcamSession.StartRecording"/>.</summary>
public sealed class WebcamRecordingOptions
{
    /// <summary>Creates recording options for the given output file.</summary>
    /// <param name="outputPath">The video file to write; its directory is created if missing.</param>
    public WebcamRecordingOptions(string outputPath)
    {
        OutputPath = outputPath;
    }

    /// <summary>The video file to write.</summary>
    public string OutputPath { get; }

    /// <summary>The on-disk format; default <see cref="WebcamVideoFormat.Mp4H264"/>.</summary>
    public WebcamVideoFormat Format { get; set; }

    /// <summary>The H.264 video bitrate in kilobits per second (ignored for MJPEG passthrough); default 4000.</summary>
    public uint VideoBitrateKbps { get; set; } = 4000;

    /// <summary>
    /// Force the frame-path pipeline even when no overlay is set at start, so an overlay
    /// can be attached or changed live DURING the recording. Only meaningful for
    /// <see cref="WebcamVideoFormat.Mp4H264"/>. When false (the default) and no overlay is
    /// set, recording uses the direct pipeline: lowest overhead, in-file audio, but
    /// overlays cannot be introduced until the recording stops.
    /// </summary>
    public bool AllowLiveOverlay { get; set; }
}
