namespace CodeBrix.Webcam.Capture;

/// <summary>The on-disk format of a webcam video recording.</summary>
public enum WebcamVideoFormat
{
    /// <summary>
    /// H.264 video in an MP4 container — the default: universally playable, good
    /// quality/size, supports overlay burn-in, and carries an AAC audio track when the
    /// session captures audio and no overlay is in use.
    /// </summary>
    Mp4H264 = 0,

    /// <summary>
    /// The camera's native Motion-JPEG stream muxed into an AVI container with no
    /// transcoding — near-zero CPU. Requires a camera streaming MJPEG, cannot burn in
    /// overlays, and carries no audio track (captured audio arrives as a sidecar file).
    /// </summary>
    MjpegAvi,
}
