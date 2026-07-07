namespace CodeBrix.Webcam.Devices;

/// <summary>
/// Hardware identity details for an imaging device, populated with whatever the platform
/// exposes; members are null or zero when the information is unavailable.
/// </summary>
public sealed class ImagingDeviceHardwareInfo
{
    /// <summary>Creates the hardware info snapshot.</summary>
    /// <param name="vendorId">USB vendor id, or 0 when unknown.</param>
    /// <param name="productId">USB product id, or 0 when unknown.</param>
    /// <param name="serialNumber">The device serial number, or null when unknown.</param>
    /// <param name="busInfo">Bus location information (e.g. "usb-0000:00:14.0-12"), or null.</param>
    /// <param name="driverName">The kernel/OS driver in use (e.g. "uvcvideo"), or null.</param>
    public ImagingDeviceHardwareInfo(ushort vendorId, ushort productId, string serialNumber,
        string busInfo, string driverName)
    {
        VendorId = vendorId;
        ProductId = productId;
        SerialNumber = serialNumber;
        BusInfo = busInfo;
        DriverName = driverName;
    }

    /// <summary>USB vendor id (e.g. 0x046D for Logitech), or 0 when unknown.</summary>
    public ushort VendorId { get; }

    /// <summary>USB product id, or 0 when unknown.</summary>
    public ushort ProductId { get; }

    /// <summary>The device serial number, or null when the platform does not expose one.</summary>
    public string SerialNumber { get; }

    /// <summary>Bus location information (e.g. "usb-0000:00:14.0-12"), or null.</summary>
    public string BusInfo { get; }

    /// <summary>The kernel/OS driver in use (e.g. "uvcvideo"), or null.</summary>
    public string DriverName { get; }
}
