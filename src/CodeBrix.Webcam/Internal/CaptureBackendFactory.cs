using System;
using CodeBrix.Webcam.Devices;
using CodeBrix.Webcam.Internal.Windows;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// Picks the <see cref="ICaptureBackend"/> implementation for the current operating
/// system: the native Media Foundation engine on Windows (no libvlc — and none of the
/// GPL-licensed libvlc plugins — anywhere on the Windows capture path), and the libvlc
/// engine (v4l2 / avcapture) on Linux and macOS.
/// </summary>
internal static class CaptureBackendFactory
{
    /// <summary>Creates the backend for one session; cheap — no capture engine is
    /// loaded until the backend's Start().</summary>
    internal static ICaptureBackend Create(IImagingMediaDevice device, WebcamSessionOptions options,
        string audioDeviceId)
    {
        if (OperatingSystem.IsWindows())
        {
            return new MediaFoundationCaptureBackend(device, options, audioDeviceId);
        }
        return new LibVlcCaptureBackend(device, options, audioDeviceId);
    }
}
