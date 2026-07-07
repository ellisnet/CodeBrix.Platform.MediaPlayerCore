using System;
using System.Collections.Generic;
using CodeBrix.Webcam.Devices;

namespace CodeBrix.Webcam.Internal.Darwin;

/// <summary>
/// macOS device enumeration is NOT implemented yet. The capture pipeline itself is
/// platform-neutral (libvlc's avcapture input), but enumeration needs AVFoundation —
/// see MAC-PORTING-GUIDE.txt at the repository root for the implementation handoff.
/// </summary>
internal static class DarwinDeviceProvider
{
    internal static List<IImagingMediaDevice> GetDevices()
        => throw new PlatformNotSupportedException(
            "CodeBrix.Webcam device enumeration is not implemented on macOS yet. " +
            "See MAC-PORTING-GUIDE.txt in the CodeBrix.Platform.MediaPlayerCore repository " +
            "for the AVFoundation implementation handoff.");
}
