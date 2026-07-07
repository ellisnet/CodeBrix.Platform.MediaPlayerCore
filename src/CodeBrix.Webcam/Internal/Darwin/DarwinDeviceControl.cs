using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using CodeBrix.Webcam.Devices;
using static CodeBrix.Webcam.Internal.Darwin.DarwinNativeMethods;

namespace CodeBrix.Webcam.Internal.Darwin;

/// <summary>
/// A macOS camera control backed by one of AVCaptureDevice's mode selectors (focus /
/// exposure / white-balance mode). All three families share the same mode values:
/// 0 = locked (manual), 1 = one-shot automatic, 2 = continuous automatic — exposed as a
/// Menu control over the modes the device supports. Every get/set re-resolves the
/// device by uniqueID and runs under lockForConfiguration, so controls keep working
/// while the camera is streaming through libvlc.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class DarwinDeviceControl : IImagingDeviceControl
{
    private const int ModeLocked = 0;
    private const int ModeAutoOnce = 1;
    private const int ModeContinuousAuto = 2;

    private readonly string _deviceUniqueId;
    private readonly IReadOnlyList<int> _supportedModes;
    private readonly string _modeSelector;
    private readonly string _setModeSelector;
    private readonly string _supportedSelector;

    public DarwinDeviceControl(string deviceUniqueId, ImagingDeviceControlKind kind, string name,
        int rawId, IReadOnlyList<int> supportedModes, int currentMode,
        string modeSelector, string setModeSelector, string supportedSelector)
    {
        _deviceUniqueId = deviceUniqueId;
        Kind = kind;
        Name = name;
        RawId = rawId;
        _supportedModes = supportedModes;
        DefaultValue = currentMode;
        _modeSelector = modeSelector;
        _setModeSelector = setModeSelector;
        _supportedSelector = supportedSelector;
        Minimum = supportedModes.Min();
        Maximum = supportedModes.Max();
    }

    public ImagingDeviceControlKind Kind { get; }

    public string Name { get; }

    public int RawId { get; }

    public ImagingDeviceControlType ControlType => ImagingDeviceControlType.Menu;

    public int Minimum { get; }

    public int Maximum { get; }

    public int Step => 1;

    public int DefaultValue { get; }

    public bool SupportsAuto
        => _supportedModes.Contains(ModeLocked)
           && (_supportedModes.Contains(ModeAutoOnce) || _supportedModes.Contains(ModeContinuousAuto));

    public int GetValue()
    {
        var pool = objc_autoreleasePoolPush();
        try
        {
            return (int)(long)SendId(ResolveDevice(), Sel(_modeSelector));
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    public void SetValue(int value)
    {
        var pool = objc_autoreleasePoolPush();
        try
        {
            var device = ResolveDevice();
            if (!SendBool(device, Sel(_supportedSelector), (IntPtr)value))
            {
                throw new WebcamException(
                    $"The '{Name}' control does not support mode {value} on this device.");
            }
            LockForConfiguration(device);
            try
            {
                SendVoid(device, Sel(_setModeSelector), (IntPtr)value);
            }
            finally
            {
                SendVoid(device, Sel("unlockForConfiguration"));
            }
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    public bool GetAuto() => GetValue() != ModeLocked;

    public void SetAuto(bool enabled)
    {
        if (!SupportsAuto)
        {
            throw new WebcamException($"The '{Name}' control has no automatic mode on this device.");
        }
        if (enabled)
        {
            SetValue(_supportedModes.Contains(ModeContinuousAuto) ? ModeContinuousAuto : ModeAutoOnce);
        }
        else
        {
            SetValue(ModeLocked);
        }
    }

    private IntPtr ResolveDevice()
    {
        var device = SendId(objc_getClass("AVCaptureDevice"), Sel("deviceWithUniqueID:"),
            NSStringFromManaged(_deviceUniqueId));
        if (device == IntPtr.Zero)
        {
            throw new WebcamException(
                $"Camera '{_deviceUniqueId}' is no longer present; cannot access the '{Name}' control.");
        }
        return device;
    }

    private void LockForConfiguration(IntPtr device)
    {
        var error = IntPtr.Zero;
        if (SendBool(device, Sel("lockForConfiguration:"), ref error))
        {
            return;
        }
        var reason = error == IntPtr.Zero
            ? null
            : NSStringToManaged(SendId(error, Sel("localizedDescription")));
        throw new WebcamException(
            $"Cannot lock the camera to change the '{Name}' control"
            + (reason == null ? "." : $": {reason}"));
    }
}
