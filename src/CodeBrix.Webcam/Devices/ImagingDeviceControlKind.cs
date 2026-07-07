namespace CodeBrix.Webcam.Devices;

/// <summary>
/// Well-known camera control kinds, mapped from the platform's control identifiers.
/// Controls the platform exposes that have no well-known mapping appear as
/// <see cref="Unknown"/> with their raw identifier and name preserved.
/// </summary>
public enum ImagingDeviceControlKind
{
    /// <summary>A control not (yet) named in this enumeration; see its name and raw id.</summary>
    Unknown = 0,

    /// <summary>Picture brightness.</summary>
    Brightness,

    /// <summary>Picture contrast.</summary>
    Contrast,

    /// <summary>Color saturation.</summary>
    Saturation,

    /// <summary>Hue adjustment.</summary>
    Hue,

    /// <summary>Gamma correction.</summary>
    Gamma,

    /// <summary>Edge sharpness.</summary>
    Sharpness,

    /// <summary>Sensor gain.</summary>
    Gain,

    /// <summary>White balance color temperature (Kelvin on most devices).</summary>
    WhiteBalanceTemperature,

    /// <summary>Automatic white balance on/off.</summary>
    AutoWhiteBalance,

    /// <summary>Absolute exposure time.</summary>
    ExposureTime,

    /// <summary>Automatic exposure mode.</summary>
    AutoExposure,

    /// <summary>Absolute focus distance.</summary>
    Focus,

    /// <summary>Automatic (continuous) focus on/off.</summary>
    AutoFocus,

    /// <summary>Optical or digital zoom.</summary>
    Zoom,

    /// <summary>Horizontal pan.</summary>
    Pan,

    /// <summary>Vertical tilt.</summary>
    Tilt,

    /// <summary>Backlight compensation.</summary>
    BacklightCompensation,

    /// <summary>Power-line (anti-flicker) frequency: typically 0=off, 1=50 Hz, 2=60 Hz.</summary>
    PowerLineFrequency,
}
