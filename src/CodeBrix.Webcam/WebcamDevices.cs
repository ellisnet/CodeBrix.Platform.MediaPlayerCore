using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.Webcam.Devices;
using CodeBrix.Webcam.Internal.Darwin;
using CodeBrix.Webcam.Internal.Linux;
using CodeBrix.Webcam.Internal.Windows;

namespace CodeBrix.Webcam;

/// <summary>
/// The entry point for discovering connected cameras. The returned devices carry the
/// full capability matrix (formats × resolutions × frame rates), the adjustable camera
/// controls, hardware identity, and the camera's paired microphone — everything a
/// consuming application needs to present camera options and open a
/// <see cref="WebcamSession"/> in a supported mode.
/// </summary>
public static class WebcamDevices
{
    /// <summary>
    /// Enumerates the connected video-capture devices with fully populated capability
    /// and control information. Enumeration performs real device I/O and runs off the
    /// calling thread — call from UI code without worry.
    /// </summary>
    /// <returns>The connected devices; empty when none are present.</returns>
    /// <exception cref="PlatformNotSupportedException">On macOS, where enumeration is
    /// not implemented yet (see MAC-PORTING-GUIDE.txt in the repository).</exception>
    public static Task<IReadOnlyList<IImagingMediaDevice>> GetImagingMediaDeviceListAsync()
        => Task.Run<IReadOnlyList<IImagingMediaDevice>>(() =>
        {
            if (OperatingSystem.IsWindows())
            {
                return DirectShowDeviceProvider.GetDevices();
            }
            if (OperatingSystem.IsLinux())
            {
                return V4l2DeviceProvider.GetDevices();
            }
            return DarwinDeviceProvider.GetDevices();
        });
}
