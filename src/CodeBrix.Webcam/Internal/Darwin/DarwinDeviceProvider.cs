using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using CodeBrix.Webcam.Devices;
using static CodeBrix.Webcam.Internal.Darwin.DarwinNativeMethods;

namespace CodeBrix.Webcam.Internal.Darwin;

/// <summary>
/// Enumerates macOS video-capture devices via AVFoundation, called directly through the
/// Objective-C runtime (objc_msgSend P/Invoke — no native shim library). Identity comes
/// from AVCaptureDevice.uniqueID, which is also the identifier libvlc's avcapture MRL
/// accepts; the capability matrix from device.formats (CoreMedia format descriptions
/// plus videoSupportedFrameRateRanges); controls from the focus / exposure /
/// white-balance mode selectors (AVFoundation exposes no UVC processing-amp controls
/// such as brightness or contrast, so the controls list is legitimately sparse — often
/// empty); and microphone pairing from linkedDevices, falling back to a localizedName
/// match against the audio-capture devices (paired ids are qtsound-compatible uniqueIDs).
/// </summary>
[SupportedOSPlatform("macos")]
internal static class DarwinDeviceProvider
{
    // Synthetic RawId values — macOS has no native control identifier scheme.
    private const int RawIdFocusMode = 1;
    private const int RawIdExposureMode = 2;
    private const int RawIdWhiteBalanceMode = 3;

    internal static List<IImagingMediaDevice> GetDevices()
    {
        var devices = new List<IImagingMediaDevice>();
        // AVFoundation returns autoreleased objects; everything read here is copied
        // into managed objects before the pool pops.
        var pool = objc_autoreleasePoolPush();
        try
        {
            var audioInputs = ListAudioCaptureDevices();
            foreach (var device in DiscoverDevices("AVMediaTypeVideo", VideoDeviceTypeSymbols()))
            {
                var friendlyName = NSStringToManaged(SendId(device, Sel("localizedName"))) ?? "Camera";
                try
                {
                    devices.Add(BuildDevice(device, friendlyName, audioInputs));
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"CodeBrix.Webcam: skipping '{friendlyName}': {e.Message}");
                }
            }
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
        return devices;
    }

    private static ImagingMediaDevice BuildDevice(IntPtr device, string friendlyName,
        List<(string Id, string Name)> audioInputs)
    {
        var uniqueId = NSStringToManaged(SendId(device, Sel("uniqueID")));
        if (string.IsNullOrEmpty(uniqueId))
        {
            throw new WebcamException("The camera reports no uniqueID.");
        }
        var modelId = NSStringToManaged(SendId(device, Sel("modelID")));
        var (vendorId, productId) = DarwinDeviceInfoParser.ParseVendorProduct(modelId);
        var busInfo = DarwinDeviceInfoParser.TransportTypeToString(SendInt(device, Sel("transportType")));

        var capabilities = new List<ImagingMediaCapability>();
        var formats = SendId(device, Sel("formats"));
        for (var i = 0; i < NSArrayCount(formats); i++)
        {
            AddCapability(capabilities, NSArrayAt(formats, i));
        }

        return new ImagingMediaDevice(
            uniqueId,
            friendlyName,
            new ImagingDeviceHardwareInfo(vendorId, productId, null, busInfo, "avfoundation"),
            capabilities,
            BuildModeControls(device, uniqueId),
            FindPairedMicrophone(device, friendlyName, audioInputs));
    }

    private static void AddCapability(List<ImagingMediaCapability> capabilities, IntPtr format)
    {
        var description = SendId(format, Sel("formatDescription"));
        if (description == IntPtr.Zero)
        {
            return;
        }
        var dimensions = CMVideoFormatDescriptionGetDimensions(description);
        if (dimensions.Width <= 0 || dimensions.Height <= 0)
        {
            return;
        }
        var (fourCc, pixelFormat) =
            DarwinDeviceInfoParser.MapSubtype(CMFormatDescriptionGetMediaSubType(description));

        // Each AVFrameRateRange is either a discrete rate (min == max) or a continuous
        // range. Discrete-only formats get the discrete list (highest first); any real
        // range collapses to the overall endpoints with the range flag set — the same
        // convention the Windows provider uses.
        var ranges = SendId(format, Sel("videoSupportedFrameRateRanges"));
        var discreteRates = new List<double>();
        var overallMin = double.MaxValue;
        var overallMax = 0.0;
        var isRange = false;
        for (var i = 0; i < NSArrayCount(ranges); i++)
        {
            var range = NSArrayAt(ranges, i);
            var minRate = Math.Round(SendDouble(range, Sel("minFrameRate")), 3);
            var maxRate = Math.Round(SendDouble(range, Sel("maxFrameRate")), 3);
            if (maxRate <= 0)
            {
                continue;
            }
            if (Math.Abs(maxRate - minRate) > 0.001)
            {
                isRange = true;
            }
            overallMin = Math.Min(overallMin, minRate > 0 ? minRate : maxRate);
            overallMax = Math.Max(overallMax, maxRate);
            if (!discreteRates.Contains(maxRate))
            {
                discreteRates.Add(maxRate);
            }
        }

        List<double> rates;
        if (isRange)
        {
            rates = new List<double> { overallMax, overallMin };
        }
        else
        {
            discreteRates.Sort((a, b) => b.CompareTo(a));
            rates = discreteRates;
        }

        capabilities.Add(new ImagingMediaCapability(pixelFormat, fourCc,
            (uint)dimensions.Width, (uint)dimensions.Height, rates, isRange));
    }

    private static List<IImagingDeviceControl> BuildModeControls(IntPtr device, string uniqueId)
    {
        var controls = new List<IImagingDeviceControl>();
        AddModeControl(controls, device, uniqueId, ImagingDeviceControlKind.AutoFocus,
            "Focus Mode", RawIdFocusMode,
            "focusMode", "setFocusMode:", "isFocusModeSupported:");
        AddModeControl(controls, device, uniqueId, ImagingDeviceControlKind.AutoExposure,
            "Exposure Mode", RawIdExposureMode,
            "exposureMode", "setExposureMode:", "isExposureModeSupported:");
        AddModeControl(controls, device, uniqueId, ImagingDeviceControlKind.AutoWhiteBalance,
            "White Balance Mode", RawIdWhiteBalanceMode,
            "whiteBalanceMode", "setWhiteBalanceMode:", "isWhiteBalanceModeSupported:");
        return controls;
    }

    private static void AddModeControl(List<IImagingDeviceControl> controls, IntPtr device,
        string uniqueId, ImagingDeviceControlKind kind, string name, int rawId,
        string modeSelector, string setModeSelector, string supportedSelector)
    {
        // Modes are 0 = locked, 1 = one-shot auto, 2 = continuous auto in all three
        // families. A control only exists when there is something to choose between;
        // fabricating a one-mode "control" would be noise.
        var supportedModes = new List<int>();
        for (var mode = 0; mode <= 2; mode++)
        {
            if (SendBool(device, Sel(supportedSelector), (IntPtr)mode))
            {
                supportedModes.Add(mode);
            }
        }
        if (supportedModes.Count < 2)
        {
            return;
        }
        var currentMode = (int)(long)SendId(device, Sel(modeSelector));
        controls.Add(new DarwinDeviceControl(uniqueId, kind, name, rawId,
            supportedModes, currentMode, modeSelector, setModeSelector, supportedSelector));
    }

    private static ImagingAudioPairing FindPairedMicrophone(IntPtr device, string cameraName,
        List<(string Id, string Name)> audioInputs)
    {
        // First choice: the audio device AVFoundation itself links to the camera.
        var mediaTypeAudio = GetAVFoundationConstant("AVMediaTypeAudio");
        var linked = SendId(device, Sel("linkedDevices"));
        for (var i = 0; i < NSArrayCount(linked); i++)
        {
            var candidate = NSArrayAt(linked, i);
            if (mediaTypeAudio != IntPtr.Zero
                && SendBool(candidate, Sel("hasMediaType:"), mediaTypeAudio))
            {
                var id = NSStringToManaged(SendId(candidate, Sel("uniqueID")));
                var name = NSStringToManaged(SendId(candidate, Sel("localizedName")));
                if (!string.IsNullOrEmpty(id))
                {
                    return new ImagingAudioPairing(id, name ?? id);
                }
            }
        }
        // Fallback: an audio-capture device whose name embeds the camera name (the
        // same convention external webcams follow on Windows).
        if (string.IsNullOrEmpty(cameraName))
        {
            return null;
        }
        var match = audioInputs.FirstOrDefault(a =>
            a.Name != null && a.Name.IndexOf(cameraName, StringComparison.OrdinalIgnoreCase) >= 0);
        return match.Id == null ? null : new ImagingAudioPairing(match.Id, match.Name);
    }

    private static List<(string Id, string Name)> ListAudioCaptureDevices()
    {
        var audioInputs = new List<(string, string)>();
        foreach (var device in DiscoverDevices("AVMediaTypeAudio", AudioDeviceTypeSymbols()))
        {
            var id = NSStringToManaged(SendId(device, Sel("uniqueID")));
            var name = NSStringToManaged(SendId(device, Sel("localizedName")));
            if (!string.IsNullOrEmpty(id))
            {
                audioInputs.Add((id, name ?? id));
            }
        }
        return audioInputs;
    }

    private static string[] VideoDeviceTypeSymbols()
    {
        // AVCaptureDeviceTypeExternal arrived in macOS 14 as the replacement for
        // AVCaptureDeviceTypeExternalUnknown; probe for the new symbol and fall back.
        var external = GetAVFoundationConstant("AVCaptureDeviceTypeExternal") != IntPtr.Zero
            ? "AVCaptureDeviceTypeExternal"
            : "AVCaptureDeviceTypeExternalUnknown";
        return new[]
        {
            "AVCaptureDeviceTypeBuiltInWideAngleCamera",
            external,
            "AVCaptureDeviceTypeContinuityCamera",
        };
    }

    private static string[] AudioDeviceTypeSymbols()
        // AVCaptureDeviceTypeMicrophone (macOS 14) covers built-in and external mics;
        // older systems need the two pre-14 types.
        => GetAVFoundationConstant("AVCaptureDeviceTypeMicrophone") != IntPtr.Zero
            ? new[] { "AVCaptureDeviceTypeMicrophone" }
            : new[] { "AVCaptureDeviceTypeBuiltInMicrophone", "AVCaptureDeviceTypeExternalUnknown" };

    private static List<IntPtr> DiscoverDevices(string mediaTypeSymbol, string[] deviceTypeSymbols)
    {
        var found = new List<IntPtr>();
        var mediaType = GetAVFoundationConstant(mediaTypeSymbol);
        var sessionClass = objc_getClass("AVCaptureDeviceDiscoverySession");
        if (mediaType == IntPtr.Zero || sessionClass == IntPtr.Zero)
        {
            return found;
        }

        var deviceTypes = SendId(objc_getClass("NSMutableArray"), Sel("array"));
        foreach (var symbol in deviceTypeSymbols)
        {
            var deviceType = GetAVFoundationConstant(symbol);
            if (deviceType != IntPtr.Zero)
            {
                SendVoid(deviceTypes, Sel("addObject:"), deviceType);
            }
        }

        // position: AVCaptureDevicePositionUnspecified (0) — all devices.
        var session = SendId(sessionClass, Sel("discoverySessionWithDeviceTypes:mediaType:position:"),
            deviceTypes, mediaType, IntPtr.Zero);
        var devices = SendId(session, Sel("devices"));
        for (var i = 0; i < NSArrayCount(devices); i++)
        {
            found.Add(NSArrayAt(devices, i));
        }
        return found;
    }
}
