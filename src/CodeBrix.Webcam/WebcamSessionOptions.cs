using CodeBrix.Webcam.Devices;

namespace CodeBrix.Webcam;

/// <summary>
/// Options for opening a <see cref="WebcamSession"/>. The zero/default values mean
/// "let the camera choose"; to open a specific mode, copy the values from one of the
/// device's <see cref="IImagingMediaDevice.Capabilities"/> entries.
/// </summary>
public sealed class WebcamSessionOptions
{
    /// <summary>Requested frame width in pixels, or 0 to let the camera choose.</summary>
    public uint RequestedWidth { get; set; }

    /// <summary>Requested frame height in pixels, or 0 to let the camera choose.</summary>
    public uint RequestedHeight { get; set; }

    /// <summary>Requested frame rate in frames per second, or 0 to let the camera choose.</summary>
    public double RequestedFrameRate { get; set; }

    /// <summary>
    /// The camera stream format to request, or <see cref="ImagingPixelFormat.Unknown"/>
    /// (the default) to let the camera choose. Requesting <see cref="ImagingPixelFormat.Mjpeg"/>
    /// is common: high resolutions at full frame rate with low USB bandwidth.
    /// </summary>
    public ImagingPixelFormat PreferredFormat { get; set; }

    /// <summary>How microphone audio is handled; default <see cref="AudioCaptureMode.Auto"/>.</summary>
    public AudioCaptureMode AudioCapture { get; set; } = AudioCaptureMode.Auto;

    /// <summary>
    /// The audio device to capture when <see cref="AudioCapture"/> is
    /// <see cref="AudioCaptureMode.SpecificDevice"/> — an ALSA device like "hw:1,0" on
    /// Linux, a DirectShow audio device name on Windows.
    /// </summary>
    public string AudioDeviceId { get; set; }

    /// <summary>
    /// When true and an overlay is set, <see cref="WebcamSession.FrameReceived"/> delivers
    /// frames with the overlay already composited, so a simple view can render them as-is.
    /// Default false: preview frames stay raw (the app draws its own live overlay), and
    /// the overlay is burned in only for photos and recordings.
    /// </summary>
    public bool CompositeOverlayOnPreview { get; set; }

    /// <summary>
    /// The capture pipeline's buffering in milliseconds. Lower is snappier preview;
    /// higher tolerates scheduling hiccups. Default 100.
    /// </summary>
    public int LiveCachingMs { get; set; } = 100;
}
