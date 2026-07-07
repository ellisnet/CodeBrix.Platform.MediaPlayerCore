using CodeBrix.Webcam.Devices;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// Picks the <see cref="ICaptureBackend"/> implementation for the current operating
/// system. Every platform currently captures through the libvlc backend; the seam
/// exists so a platform can move to a native capture engine without touching
/// <see cref="WebcamSession"/> or the public API.
/// </summary>
internal static class CaptureBackendFactory
{
    /// <summary>Creates the backend for one session; cheap — no capture engine is
    /// loaded until the backend's Start().</summary>
    internal static ICaptureBackend Create(IImagingMediaDevice device, WebcamSessionOptions options,
        string audioDeviceId)
    {
        return new LibVlcCaptureBackend(device, options, audioDeviceId);
    }
}
