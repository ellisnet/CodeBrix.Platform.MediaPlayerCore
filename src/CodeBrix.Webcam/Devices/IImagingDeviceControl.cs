namespace CodeBrix.Webcam.Devices;

/// <summary>
/// One adjustable control of an imaging device (brightness, focus, exposure, ...),
/// including its range and live get/set access.
/// <para/>
/// Platform note: on Linux, reading and writing controls is fully supported while the
/// camera is streaming. On Windows, changing controls while another component holds the
/// capture graph is driver-dependent and best-effort — enumeration and adjustment between
/// sessions always works.
/// </summary>
public interface IImagingDeviceControl
{
    /// <summary>The well-known control kind, or <see cref="ImagingDeviceControlKind.Unknown"/>.</summary>
    ImagingDeviceControlKind Kind { get; }

    /// <summary>The control name as reported by the driver (e.g. "Brightness").</summary>
    string Name { get; }

    /// <summary>The platform's raw control identifier (a V4L2 CID on Linux; a
    /// DirectShow property id on Windows), for controls beyond the well-known set.</summary>
    int RawId { get; }

    /// <summary>The value shape of the control.</summary>
    ImagingDeviceControlType ControlType { get; }

    /// <summary>The minimum accepted value.</summary>
    int Minimum { get; }

    /// <summary>The maximum accepted value.</summary>
    int Maximum { get; }

    /// <summary>The step between valid values (1 for most controls).</summary>
    int Step { get; }

    /// <summary>The driver's default value.</summary>
    int DefaultValue { get; }

    /// <summary>
    /// True when the control has an associated automatic mode (e.g. auto-focus for the
    /// focus control) that <see cref="GetAuto"/>/<see cref="SetAuto"/> can read and change.
    /// </summary>
    bool SupportsAuto { get; }

    /// <summary>Reads the control's current value from the device.</summary>
    /// <returns>The current value.</returns>
    int GetValue();

    /// <summary>Writes a new value to the device.</summary>
    /// <param name="value">The value to set; clamped semantics are driver-defined, so
    /// pass a value between <see cref="Minimum"/> and <see cref="Maximum"/>.</param>
    void SetValue(int value);

    /// <summary>Reads whether the control's automatic mode is currently engaged.
    /// Only meaningful when <see cref="SupportsAuto"/> is true.</summary>
    /// <returns>True when the automatic mode is on.</returns>
    bool GetAuto();

    /// <summary>Engages or disengages the control's automatic mode.
    /// Only meaningful when <see cref="SupportsAuto"/> is true.</summary>
    /// <param name="enabled">True to engage automatic mode; false for manual control.</param>
    void SetAuto(bool enabled);
}
