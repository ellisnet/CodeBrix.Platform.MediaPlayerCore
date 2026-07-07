using System;
using System.ComponentModel;
using CodeBrix.Webcam.Devices;
using static CodeBrix.Webcam.Internal.Linux.V4l2NativeMethods;

namespace CodeBrix.Webcam.Internal.Linux;

/// <summary>
/// A V4L2 camera control. Every get/set opens the device node briefly on a side file
/// descriptor, so controls work while the camera is streaming through libvlc.
/// </summary>
internal sealed unsafe class V4l2DeviceControl : IImagingDeviceControl
{
    private readonly string _devicePath;
    private readonly uint _autoSiblingId;   // 0 = no auto sibling
    private readonly int _autoOnValue;      // sibling value that means "automatic"
    private readonly int _autoOffValue;     // sibling value that means "manual"

    public V4l2DeviceControl(string devicePath, ImagingDeviceControlKind kind, string name, uint rawId,
        ImagingDeviceControlType controlType, int minimum, int maximum, int step, int defaultValue,
        uint autoSiblingId, int autoOnValue, int autoOffValue)
    {
        _devicePath = devicePath;
        Kind = kind;
        Name = name;
        RawId = unchecked((int)rawId);
        ControlType = controlType;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        DefaultValue = defaultValue;
        _autoSiblingId = autoSiblingId;
        _autoOnValue = autoOnValue;
        _autoOffValue = autoOffValue;
    }

    public ImagingDeviceControlKind Kind { get; }

    public string Name { get; }

    public int RawId { get; }

    public ImagingDeviceControlType ControlType { get; }

    public int Minimum { get; }

    public int Maximum { get; }

    public int Step { get; }

    public int DefaultValue { get; }

    public bool SupportsAuto => _autoSiblingId != 0;

    public int GetValue() => ReadControl(unchecked((uint)RawId));

    public void SetValue(int value) => WriteControl(unchecked((uint)RawId), value);

    public bool GetAuto()
    {
        if (!SupportsAuto)
        {
            return false;
        }
        return ReadControl(_autoSiblingId) == _autoOnValue;
    }

    public void SetAuto(bool enabled)
    {
        if (!SupportsAuto)
        {
            throw new WebcamException($"The '{Name}' control has no automatic mode on this device.");
        }
        WriteControl(_autoSiblingId, enabled ? _autoOnValue : _autoOffValue);
    }

    private int ReadControl(uint controlId)
    {
        var fd = Open(_devicePath, O_RDWR);
        if (fd < 0)
        {
            throw new WebcamException($"Cannot open {_devicePath} to read the '{Name}' control",
                new Win32Exception());
        }
        try
        {
            var control = new v4l2_control { id = controlId };
            if (Ioctl(fd, (UIntPtr)VIDIOC_G_CTRL, &control) != 0)
            {
                throw new WebcamException($"Reading the '{Name}' control failed", new Win32Exception());
            }
            return control.value;
        }
        finally
        {
            Close(fd);
        }
    }

    private void WriteControl(uint controlId, int value)
    {
        var fd = Open(_devicePath, O_RDWR);
        if (fd < 0)
        {
            throw new WebcamException($"Cannot open {_devicePath} to change the '{Name}' control",
                new Win32Exception());
        }
        try
        {
            var control = new v4l2_control { id = controlId, value = value };
            if (Ioctl(fd, (UIntPtr)VIDIOC_S_CTRL, &control) != 0)
            {
                throw new WebcamException($"Setting the '{Name}' control to {value} failed", new Win32Exception());
            }
        }
        finally
        {
            Close(fd);
        }
    }
}
