using System.Collections.Generic;

namespace CodeBrix.Webcam.Devices;

/// <summary>
/// A connected imaging (video capture) device — a webcam or similar camera — with
/// everything a consuming application wants to know about it: identity, hardware
/// information, the full matrix of supported pixel formats / resolutions / frame rates,
/// the adjustable camera controls, and the microphone physically paired with the camera
/// (if any). Obtain instances from
/// <see cref="WebcamDevices.GetImagingMediaDeviceListAsync()"/>.
/// </summary>
public interface IImagingMediaDevice
{
    /// <summary>
    /// The stable operating-system identity of the device — the value to persist and to
    /// pass around. On Linux this is the device node path (e.g. <c>/dev/video2</c>); on
    /// Windows it is the DirectShow device path.
    /// </summary>
    string Id { get; }

    /// <summary>The human-readable device name (e.g. "C922 Pro Stream Webcam").</summary>
    string FriendlyName { get; }

    /// <summary>
    /// Hardware identity details: USB vendor/product ids, serial number, bus information,
    /// and driver name, where the platform exposes them.
    /// </summary>
    ImagingDeviceHardwareInfo Hardware { get; }

    /// <summary>
    /// The full capability matrix: every (pixel format, resolution, frame rates)
    /// combination the device advertises. Pass a chosen capability's values into
    /// <see cref="WebcamSessionOptions"/> to open the device in exactly that mode.
    /// </summary>
    IReadOnlyList<ImagingMediaCapability> Capabilities { get; }

    /// <summary>
    /// The adjustable device controls (brightness, focus, exposure, zoom, ...) with their
    /// ranges, defaults, and live get/set access where the platform allows it.
    /// </summary>
    IReadOnlyList<IImagingDeviceControl> Controls { get; }

    /// <summary>
    /// The microphone that is physically part of this camera (e.g. a webcam's built-in
    /// mic), or null when the camera has none. Used by <see cref="AudioCaptureMode.Auto"/>.
    /// </summary>
    ImagingAudioPairing PairedMicrophone { get; }
}
