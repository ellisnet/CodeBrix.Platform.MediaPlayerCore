using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CodeBrix.Webcam.Devices;
using static CodeBrix.Webcam.Internal.Windows.DirectShowNativeMethods;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// A DirectShow camera control (IAMVideoProcAmp or IAMCameraControl property). Every
/// get/set re-binds the device filter by its device path, so the control object stays
/// valid across sessions; adjusting while another component streams from the camera is
/// driver-dependent (best-effort).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class DirectShowDeviceControl : IImagingDeviceControl
{
    private readonly string _devicePath;
    private readonly bool _isCameraControl; // false = IAMVideoProcAmp, true = IAMCameraControl
    private readonly bool _supportsAuto;

    public DirectShowDeviceControl(string devicePath, bool isCameraControl,
        ImagingDeviceControlKind kind, string name, int propertyId,
        int minimum, int maximum, int step, int defaultValue, bool supportsAuto)
    {
        _devicePath = devicePath;
        _isCameraControl = isCameraControl;
        Kind = kind;
        Name = name;
        RawId = propertyId;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        DefaultValue = defaultValue;
        _supportsAuto = supportsAuto;
    }

    public ImagingDeviceControlKind Kind { get; }

    public string Name { get; }

    public int RawId { get; }

    public ImagingDeviceControlType ControlType => ImagingDeviceControlType.Integer;

    public int Minimum { get; }

    public int Maximum { get; }

    public int Step { get; }

    public int DefaultValue { get; }

    public bool SupportsAuto => _supportsAuto;

    public int GetValue()
    {
        var (value, _) = ReadValueAndFlags();
        return value;
    }

    public void SetValue(int value) => Write(value, ControlFlags_Manual);

    public bool GetAuto()
    {
        if (!_supportsAuto)
        {
            return false;
        }
        var (_, flags) = ReadValueAndFlags();
        return (flags & ControlFlags_Auto) != 0;
    }

    public void SetAuto(bool enabled)
    {
        if (!_supportsAuto)
        {
            throw new WebcamException($"The '{Name}' control has no automatic mode on this device.");
        }
        var (value, _) = ReadValueAndFlags();
        Write(value, enabled ? ControlFlags_Auto : ControlFlags_Manual);
    }

    private (int Value, int Flags) ReadValueAndFlags()
    {
        var filter = DirectShowDeviceProvider.BindFilterByDevicePath(_devicePath);
        try
        {
            int hr, value, flags;
            if (_isCameraControl)
            {
                hr = ((IAMCameraControl)filter).Get(RawId, out value, out flags);
            }
            else
            {
                hr = ((IAMVideoProcAmp)filter).Get(RawId, out value, out flags);
            }
            if (hr != 0)
            {
                throw new WebcamException($"Reading the '{Name}' control failed (HRESULT 0x{hr:X8})");
            }
            return (value, flags);
        }
        finally
        {
            Marshal.ReleaseComObject(filter);
        }
    }

    private void Write(int value, int flags)
    {
        var filter = DirectShowDeviceProvider.BindFilterByDevicePath(_devicePath);
        try
        {
            var hr = _isCameraControl
                ? ((IAMCameraControl)filter).Set(RawId, value, flags)
                : ((IAMVideoProcAmp)filter).Set(RawId, value, flags);
            if (hr != 0)
            {
                throw new WebcamException($"Setting the '{Name}' control failed (HRESULT 0x{hr:X8})");
            }
        }
        finally
        {
            Marshal.ReleaseComObject(filter);
        }
    }
}
