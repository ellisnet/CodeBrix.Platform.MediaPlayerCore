using System;

namespace CodeBrix.Webcam.Capture;

/// <summary>
/// The outcome of a completed recording: which files were written and, when audio was
/// captured to a sidecar file, the measured offset for re-muxing them together.
/// </summary>
public sealed class WebcamRecordingResult
{
    internal WebcamRecordingResult(string videoFilePath, string audioFilePath,
        TimeSpan? estimatedAudioOffset, TimeSpan duration, long framesRecorded)
    {
        VideoFilePath = videoFilePath;
        AudioFilePath = audioFilePath;
        EstimatedAudioOffset = estimatedAudioOffset;
        Duration = duration;
        FramesRecorded = framesRecorded;
    }

    /// <summary>The recorded video file. For the direct MP4 pipeline with audio capture
    /// active, this file already contains the audio track.</summary>
    public string VideoFilePath { get; }

    /// <summary>
    /// The sidecar WAV file holding the captured audio, or null when audio was off or
    /// already muxed into <see cref="VideoFilePath"/>. Combine the two with a stream-copy
    /// mux (e.g. CodeBrix.VideoProcessing) to produce a single file with sound.
    /// </summary>
    public string AudioFilePath { get; }

    /// <summary>
    /// The measured gap between the start of the audio capture and the first recorded
    /// video frame (positive = audio started first). Pass as an audio offset when muxing
    /// the sidecar, if lip-sync needs the correction. Null when there is no sidecar.
    /// </summary>
    public TimeSpan? EstimatedAudioOffset { get; }

    /// <summary>The wall-clock duration of the recording.</summary>
    public TimeSpan Duration { get; }

    /// <summary>The number of video frames recorded via the frame-path pipeline, or 0
    /// for the direct pipeline (libvlc consumes the stream internally there).</summary>
    public long FramesRecorded { get; }
}
