using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using CodeBrix.Webcam.Devices;
using static CodeBrix.Webcam.Internal.Windows.DirectShowNativeMethods;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// Enumerates Windows video-capture devices via DirectShow — deliberately DirectShow
/// rather than MediaFoundation, because libvlc 3 captures through its dshow module, so
/// the names discovered here are exactly the names the capture session hands to libvlc.
/// Capabilities come from IAMStreamConfig, controls from IAMVideoProcAmp and
/// IAMCameraControl, and microphone pairing matches the audio-input device whose name
/// embeds the camera name (the "Microphone (C922 Pro Stream Webcam)" convention).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class DirectShowDeviceProvider
{
    private static readonly Regex VidPidPattern = new Regex(
        @"vid_([0-9a-fA-F]{4})&pid_([0-9a-fA-F]{4})", RegexOptions.Compiled);

    internal static List<IImagingMediaDevice> GetDevices()
    {
        var devices = new List<IImagingMediaDevice>();
        var audioInputs = ListDeviceMonikers(ClsidAudioInputDeviceCategory)
            .Select(m => m.FriendlyName)
            .ToList();

        foreach (var (friendlyName, devicePath) in ListDeviceMonikers(ClsidVideoInputDeviceCategory))
        {
            try
            {
                devices.Add(BuildDevice(friendlyName, devicePath, audioInputs));
            }
            catch (Exception e)
            {
                Trace.WriteLine($"CodeBrix.Webcam: skipping '{friendlyName}': {e.Message}");
            }
        }
        return devices;
    }

    private static ImagingMediaDevice BuildDevice(string friendlyName, string devicePath,
        List<string> audioInputNames)
    {
        ushort vendorId = 0, productId = 0;
        var vidPid = VidPidPattern.Match(devicePath ?? string.Empty);
        if (vidPid.Success)
        {
            vendorId = ushort.Parse(vidPid.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            productId = ushort.Parse(vidPid.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        var capabilities = new List<ImagingMediaCapability>();
        var controls = new List<IImagingDeviceControl>();

        var filter = BindFilterByDevicePath(devicePath);
        try
        {
            ReadCapabilities(filter, capabilities);
            ReadControls(filter, devicePath, controls);
        }
        finally
        {
            Marshal.ReleaseComObject(filter);
        }

        return new ImagingMediaDevice(
            devicePath ?? friendlyName,
            friendlyName,
            new ImagingDeviceHardwareInfo(vendorId, productId, null, devicePath, "dshow"),
            capabilities,
            controls,
            FindPairedMicrophone(friendlyName, audioInputNames));
    }

    private static List<(string FriendlyName, string DevicePath)> ListDeviceMonikers(Guid category)
    {
        var results = new List<(string, string)>();
        var enumeratorType = Type.GetTypeFromCLSID(ClsidSystemDeviceEnum);
        var deviceEnum = (ICreateDevEnum)Activator.CreateInstance(enumeratorType);
        try
        {
            var categoryGuid = category;
            if (deviceEnum.CreateClassEnumerator(ref categoryGuid, out var enumMoniker, 0) != 0
                || enumMoniker == null)
            {
                return results; // no devices in this category
            }
            try
            {
                var monikers = new IMoniker[1];
                while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    var moniker = monikers[0];
                    try
                    {
                        var bagGuid = typeof(IPropertyBag).GUID;
                        moniker.BindToStorage(null, null, ref bagGuid, out var bagObject);
                        var bag = (IPropertyBag)bagObject;
                        try
                        {
                            bag.Read("FriendlyName", out var name, IntPtr.Zero);
                            bag.Read("DevicePath", out var path, IntPtr.Zero);
                            results.Add((name as string ?? "Camera", path as string));
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(bag);
                        }
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine($"CodeBrix.Webcam: moniker read failed: {e.Message}");
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(moniker);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumMoniker);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(deviceEnum);
        }
        return results;
    }

    /// <summary>Binds the capture filter for a device path (or friendly name fallback).</summary>
    internal static object BindFilterByDevicePath(string devicePath)
    {
        var enumeratorType = Type.GetTypeFromCLSID(ClsidSystemDeviceEnum);
        var deviceEnum = (ICreateDevEnum)Activator.CreateInstance(enumeratorType);
        try
        {
            var categoryGuid = ClsidVideoInputDeviceCategory;
            if (deviceEnum.CreateClassEnumerator(ref categoryGuid, out var enumMoniker, 0) != 0
                || enumMoniker == null)
            {
                throw new WebcamException("No video capture devices are present.");
            }
            try
            {
                var monikers = new IMoniker[1];
                while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    var moniker = monikers[0];
                    try
                    {
                        var bagGuid = typeof(IPropertyBag).GUID;
                        moniker.BindToStorage(null, null, ref bagGuid, out var bagObject);
                        var bag = (IPropertyBag)bagObject;
                        string path;
                        try
                        {
                            bag.Read("DevicePath", out var pathValue, IntPtr.Zero);
                            path = pathValue as string;
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(bag);
                        }

                        if (string.Equals(path, devicePath, StringComparison.OrdinalIgnoreCase))
                        {
                            CreateBindCtx(0, out var bindCtx);
                            var filterGuid = IidBaseFilter;
                            moniker.BindToObject(bindCtx, null, ref filterGuid, out var filterObject);
                            return filterObject;
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(moniker);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumMoniker);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(deviceEnum);
        }
        throw new WebcamException($"Video capture device not found: {devicePath}");
    }

    private static void ReadCapabilities(object filter, List<ImagingMediaCapability> capabilities)
    {
        var baseFilter = (IBaseFilter)filter;
        if (baseFilter.EnumPins(out var enumPins) != 0 || enumPins == null)
        {
            return;
        }
        try
        {
            var pins = new IPin[1];
            while (enumPins.Next(1, pins, IntPtr.Zero) == 0)
            {
                var pin = pins[0];
                try
                {
                    if (pin is IAMStreamConfig streamConfig
                        && streamConfig.GetNumberOfCapabilities(out var count, out var size) == 0
                        && size == Marshal.SizeOf<VideoStreamConfigCaps>())
                    {
                        ReadPinCapabilities(streamConfig, count, capabilities);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(pin);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumPins);
        }
    }

    private static void ReadPinCapabilities(IAMStreamConfig streamConfig, int count,
        List<ImagingMediaCapability> capabilities)
    {
        var capsBuffer = Marshal.AllocCoTaskMem(Marshal.SizeOf<VideoStreamConfigCaps>());
        try
        {
            for (var i = 0; i < count; i++)
            {
                if (streamConfig.GetStreamCaps(i, out var mediaTypePointer, capsBuffer) != 0)
                {
                    continue;
                }
                try
                {
                    var mediaType = Marshal.PtrToStructure<AmMediaType>(mediaTypePointer);
                    if (mediaType.FormatType != FormatVideoInfo
                        || mediaType.FormatPointer == IntPtr.Zero
                        || mediaType.FormatSize < VideoInfoHeader_BiHeightOffset + 4)
                    {
                        continue;
                    }

                    var width = Marshal.ReadInt32(mediaType.FormatPointer, VideoInfoHeader_BiWidthOffset);
                    var height = Math.Abs(Marshal.ReadInt32(mediaType.FormatPointer, VideoInfoHeader_BiHeightOffset));
                    var caps = Marshal.PtrToStructure<VideoStreamConfigCaps>(capsBuffer);

                    var rates = new List<double>();
                    var isRange = false;
                    // Frame intervals are REFERENCE_TIME (100 ns); min interval = fastest rate.
                    var maxFps = caps.MinFrameInterval > 0 ? Math.Round(10000000.0 / caps.MinFrameInterval, 3) : 0;
                    var minFps = caps.MaxFrameInterval > 0 ? Math.Round(10000000.0 / caps.MaxFrameInterval, 3) : 0;
                    if (maxFps > 0 && minFps > 0 && Math.Abs(maxFps - minFps) > 0.001)
                    {
                        rates.Add(maxFps);
                        rates.Add(minFps);
                        isRange = true;
                    }
                    else if (maxFps > 0)
                    {
                        rates.Add(maxFps);
                    }

                    var (fourCc, pixelFormat) = MapSubtype(mediaType.SubType);
                    if (width > 0 && height > 0)
                    {
                        capabilities.Add(new ImagingMediaCapability(pixelFormat, fourCc,
                            (uint)width, (uint)height, rates, isRange));
                    }
                }
                finally
                {
                    FreeMediaType(mediaTypePointer);
                }
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(capsBuffer);
        }
    }

    private static void ReadControls(object filter, string devicePath, List<IImagingDeviceControl> controls)
    {
        if (filter is IAMVideoProcAmp procAmp)
        {
            AddControlIfPresent(controls, devicePath, false, VideoProcAmp_Brightness,
                ImagingDeviceControlKind.Brightness, "Brightness", procAmp.GetRange);
            AddControlIfPresent(controls, devicePath, false, VideoProcAmp_Contrast,
                ImagingDeviceControlKind.Contrast, "Contrast", procAmp.GetRange);
            AddControlIfPresent(controls, devicePath, false, VideoProcAmp_Hue,
                ImagingDeviceControlKind.Hue, "Hue", procAmp.GetRange);
            AddControlIfPresent(controls, devicePath, false, VideoProcAmp_Saturation,
                ImagingDeviceControlKind.Saturation, "Saturation", procAmp.GetRange);
            AddControlIfPresent(controls, devicePath, false, VideoProcAmp_Sharpness,
                ImagingDeviceControlKind.Sharpness, "Sharpness", procAmp.GetRange);
            AddControlIfPresent(controls, devicePath, false, VideoProcAmp_Gamma,
                ImagingDeviceControlKind.Gamma, "Gamma", procAmp.GetRange);
            AddControlIfPresent(controls, devicePath, false, VideoProcAmp_WhiteBalance,
                ImagingDeviceControlKind.WhiteBalanceTemperature, "White Balance", procAmp.GetRange);
            AddControlIfPresent(controls, devicePath, false, VideoProcAmp_BacklightCompensation,
                ImagingDeviceControlKind.BacklightCompensation, "Backlight Compensation", procAmp.GetRange);
            AddControlIfPresent(controls, devicePath, false, VideoProcAmp_Gain,
                ImagingDeviceControlKind.Gain, "Gain", procAmp.GetRange);
        }

        if (filter is IAMCameraControl cameraControl)
        {
            AddControlIfPresent(controls, devicePath, true, CameraControl_Pan,
                ImagingDeviceControlKind.Pan, "Pan", cameraControl.GetRange);
            AddControlIfPresent(controls, devicePath, true, CameraControl_Tilt,
                ImagingDeviceControlKind.Tilt, "Tilt", cameraControl.GetRange);
            AddControlIfPresent(controls, devicePath, true, CameraControl_Zoom,
                ImagingDeviceControlKind.Zoom, "Zoom", cameraControl.GetRange);
            AddControlIfPresent(controls, devicePath, true, CameraControl_Exposure,
                ImagingDeviceControlKind.ExposureTime, "Exposure", cameraControl.GetRange);
            AddControlIfPresent(controls, devicePath, true, CameraControl_Focus,
                ImagingDeviceControlKind.Focus, "Focus", cameraControl.GetRange);
        }
    }

    private delegate int GetRangeDelegate(int property, out int min, out int max,
        out int steppingDelta, out int defaultValue, out int capsFlags);

    private static void AddControlIfPresent(List<IImagingDeviceControl> controls, string devicePath,
        bool isCameraControl, int propertyId, ImagingDeviceControlKind kind, string name,
        GetRangeDelegate getRange)
    {
        if (getRange(propertyId, out var min, out var max, out var step, out var defaultValue,
                out var capsFlags) == 0)
        {
            controls.Add(new DirectShowDeviceControl(devicePath, isCameraControl, kind, name,
                propertyId, min, max, step, defaultValue, (capsFlags & ControlFlags_Auto) != 0));
        }
    }

    private static ImagingAudioPairing FindPairedMicrophone(string cameraName, List<string> audioInputNames)
    {
        if (string.IsNullOrEmpty(cameraName))
        {
            return null;
        }
        // Windows names camera mics like "Microphone (C922 Pro Stream Webcam)".
        var match = audioInputNames.FirstOrDefault(a =>
            a != null && a.IndexOf(cameraName, StringComparison.OrdinalIgnoreCase) >= 0);
        return match == null ? null : new ImagingAudioPairing(match, match);
    }

    private static (string FourCc, ImagingPixelFormat PixelFormat) MapSubtype(Guid subtype)
    {
        if (subtype == MediaSubtypeMjpg)
        {
            return ("MJPG", ImagingPixelFormat.Mjpeg);
        }
        if (subtype == MediaSubtypeYuy2)
        {
            return ("YUY2", ImagingPixelFormat.Yuyv);
        }
        if (subtype == MediaSubtypeNv12)
        {
            return ("NV12", ImagingPixelFormat.Nv12);
        }
        if (subtype == MediaSubtypeH264)
        {
            return ("H264", ImagingPixelFormat.H264);
        }
        if (subtype == MediaSubtypeRgb24)
        {
            return ("RGB3", ImagingPixelFormat.Rgb24);
        }
        if (subtype == MediaSubtypeRgb32)
        {
            return ("RGB4", ImagingPixelFormat.Rgb32);
        }
        // For compressed formats the subtype GUID's first four bytes ARE the fourcc.
        var bytes = subtype.ToByteArray();
        var fourCc = new string(new[] { (char)bytes[0], (char)bytes[1], (char)bytes[2], (char)bytes[3] });
        return (fourCc, ImagingPixelFormat.Unknown);
    }
}
