using System.Collections.Generic;
using CodeBrix.Webcam.Devices;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// The concrete <see cref="IImagingMediaDevice"/> snapshot shared by all platform
/// providers; the platform-specific behavior lives in the control implementations.
/// </summary>
internal sealed class ImagingMediaDevice : IImagingMediaDevice
{
    public ImagingMediaDevice(string id, string friendlyName, ImagingDeviceHardwareInfo hardware,
        IReadOnlyList<ImagingMediaCapability> capabilities, IReadOnlyList<IImagingDeviceControl> controls,
        ImagingAudioPairing pairedMicrophone)
    {
        Id = id;
        FriendlyName = friendlyName;
        Hardware = hardware;
        Capabilities = capabilities;
        Controls = controls;
        PairedMicrophone = pairedMicrophone;
    }

    public string Id { get; }

    public string FriendlyName { get; }

    public ImagingDeviceHardwareInfo Hardware { get; }

    public IReadOnlyList<ImagingMediaCapability> Capabilities { get; }

    public IReadOnlyList<IImagingDeviceControl> Controls { get; }

    public ImagingAudioPairing PairedMicrophone { get; }

    public override string ToString() => $"{FriendlyName} ({Id})";
}
