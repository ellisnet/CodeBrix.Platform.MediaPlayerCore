namespace CodeBrix.Webcam.Devices;

/// <summary>The value shape of an imaging device control.</summary>
public enum ImagingDeviceControlType
{
    /// <summary>An integer value between the control's minimum and maximum, in steps.</summary>
    Integer = 0,

    /// <summary>An on/off value (0 or 1).</summary>
    Boolean,

    /// <summary>A choice from an enumerated menu of values between minimum and maximum.</summary>
    Menu,
}
