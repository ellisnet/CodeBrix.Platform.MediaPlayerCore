namespace CodeBrix.Webcam;

/// <summary>How a <see cref="WebcamSession"/> handles microphone audio.</summary>
public enum AudioCaptureMode
{
    /// <summary>
    /// Capture from the camera's own microphone when it has one; silently video-only
    /// when it does not. The default.
    /// </summary>
    Auto = 0,

    /// <summary>Never capture audio — recordings are always silent video files.</summary>
    Off,

    /// <summary>
    /// Capture from the specific audio device named by
    /// <see cref="WebcamSessionOptions.AudioDeviceId"/>.
    /// </summary>
    SpecificDevice,
}
