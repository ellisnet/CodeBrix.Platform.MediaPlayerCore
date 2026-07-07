namespace CodeBrix.Webcam.Devices;

/// <summary>
/// The audio capture device (microphone) that is physically part of a camera — e.g. a
/// webcam's built-in mic — discovered by matching the camera and audio device on the
/// same hardware bus location.
/// </summary>
public sealed class ImagingAudioPairing
{
    /// <summary>Creates the pairing record.</summary>
    /// <param name="deviceId">The platform audio-capture device identity (an ALSA device
    /// like "hw:0,0" on Linux; a DirectShow audio device name on Windows).</param>
    /// <param name="friendlyName">The human-readable microphone name.</param>
    public ImagingAudioPairing(string deviceId, string friendlyName)
    {
        DeviceId = deviceId;
        FriendlyName = friendlyName;
    }

    /// <summary>
    /// The platform audio-capture device identity (an ALSA device like "hw:0,0" on Linux;
    /// a DirectShow audio device name on Windows).
    /// </summary>
    public string DeviceId { get; }

    /// <summary>The human-readable microphone name.</summary>
    public string FriendlyName { get; }
}
