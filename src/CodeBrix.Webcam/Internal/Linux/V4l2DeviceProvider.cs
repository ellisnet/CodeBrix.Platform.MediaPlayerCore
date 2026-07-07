using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBrix.Webcam.Devices;
using static CodeBrix.Webcam.Internal.Linux.V4l2NativeMethods;

namespace CodeBrix.Webcam.Internal.Linux;

/// <summary>
/// Enumerates Linux video-capture devices via sysfs plus V4L2 ioctls: identity from
/// VIDIOC_QUERYCAP and the USB sysfs tree, the capability matrix from
/// VIDIOC_ENUM_FMT / ENUM_FRAMESIZES / ENUM_FRAMEINTERVALS, controls from
/// VIDIOC_QUERYCTRL, and microphone pairing by finding a sound card on the same USB
/// device as the camera.
/// </summary>
internal static unsafe class V4l2DeviceProvider
{
    private const string SysVideo4Linux = "/sys/class/video4linux";

    private static readonly Regex UsbInterfaceDirPattern = new Regex(@"^\d+-[\d.]+:\d+\.\d+$", RegexOptions.Compiled);
    private static readonly Regex SoundCardDirPattern = new Regex(@"^card(\d+)$", RegexOptions.Compiled);

    internal static List<IImagingMediaDevice> GetDevices()
    {
        var devices = new List<IImagingMediaDevice>();
        if (!Directory.Exists(SysVideo4Linux))
        {
            return devices;
        }

        var nodes = Directory.GetDirectories(SysVideo4Linux)
            .Select(Path.GetFileName)
            .Where(n => n.StartsWith("video", StringComparison.Ordinal))
            .OrderBy(n => int.TryParse(n.Substring(5), out var i) ? i : int.MaxValue)
            .ToList();

        foreach (var node in nodes)
        {
            try
            {
                var device = BuildDevice(node);
                if (device != null)
                {
                    devices.Add(device);
                }
            }
            catch (Exception e)
            {
                // One broken device node must never hide the others.
                Trace.WriteLine($"CodeBrix.Webcam: skipping {node}: {e.Message}");
            }
        }
        return devices;
    }

    private static IImagingMediaDevice BuildDevice(string node)
    {
        var devicePath = "/dev/" + node;
        var fd = Open(devicePath, O_RDWR);
        if (fd < 0)
        {
            return null; // no permission or vanished device — not ours to report
        }
        try
        {
            var caps = default(v4l2_capability);
            if (Ioctl(fd, (UIntPtr)VIDIOC_QUERYCAP, &caps) != 0)
            {
                return null;
            }
            // Filter out the metadata companion nodes UVC cameras register alongside
            // the real capture node (e.g. C922 = video2 capture + video3 metadata).
            if ((caps.device_caps & V4L2_CAP_VIDEO_CAPTURE) == 0)
            {
                return null;
            }

            var card = FixedBytesToString(caps.card, 32);
            var driver = FixedBytesToString(caps.driver, 16);
            var busInfo = FixedBytesToString(caps.bus_info, 32);

            var usb = ReadUsbInfo(node);
            var friendlyName = usb.Product ?? (card.Length > 0 ? card : node);

            var hardware = new ImagingDeviceHardwareInfo(usb.VendorId, usb.ProductId,
                usb.SerialNumber, busInfo.Length > 0 ? busInfo : null,
                driver.Length > 0 ? driver : null);

            return new ImagingMediaDevice(
                devicePath,
                friendlyName,
                hardware,
                ReadCapabilities(fd),
                ReadControls(fd, devicePath),
                FindPairedMicrophone(node, friendlyName));
        }
        finally
        {
            Close(fd);
        }
    }

    private static List<ImagingMediaCapability> ReadCapabilities(int fd)
    {
        var capabilities = new List<ImagingMediaCapability>();
        for (uint formatIndex = 0; ; formatIndex++)
        {
            var format = new v4l2_fmtdesc { index = formatIndex, type = V4L2_BUF_TYPE_VIDEO_CAPTURE };
            if (Ioctl(fd, (UIntPtr)VIDIOC_ENUM_FMT, &format) != 0)
            {
                break;
            }
            var fourCc = FourCcToString(format.pixelformat);
            var pixelFormat = MapPixelFormat(fourCc);

            for (uint sizeIndex = 0; ; sizeIndex++)
            {
                var size = new v4l2_frmsizeenum { index = sizeIndex, pixel_format = format.pixelformat };
                if (Ioctl(fd, (UIntPtr)VIDIOC_ENUM_FRAMESIZES, &size) != 0)
                {
                    break;
                }

                if (size.type == V4L2_FRMSIZE_TYPE_DISCRETE)
                {
                    var rates = ReadFrameRates(fd, format.pixelformat, size.u0, size.u1, out var isRange);
                    capabilities.Add(new ImagingMediaCapability(pixelFormat, fourCc, size.u0, size.u1, rates, isRange));
                }
                else
                {
                    // Continuous/stepwise sizes: report the maximum the device offers.
                    var maxWidth = size.u1;
                    var maxHeight = size.u4;
                    var rates = ReadFrameRates(fd, format.pixelformat, maxWidth, maxHeight, out var isRange);
                    capabilities.Add(new ImagingMediaCapability(pixelFormat, fourCc, maxWidth, maxHeight, rates, isRange));
                    break; // one entry describes the whole stepwise range
                }
            }
        }
        return capabilities;
    }

    private static IReadOnlyList<double> ReadFrameRates(int fd, uint pixelFormat, uint width, uint height,
        out bool isRange)
    {
        var rates = new List<double>();
        isRange = false;
        for (uint i = 0; ; i++)
        {
            var interval = new v4l2_frmivalenum
            {
                index = i,
                pixel_format = pixelFormat,
                width = width,
                height = height,
            };
            if (Ioctl(fd, (UIntPtr)VIDIOC_ENUM_FRAMEINTERVALS, &interval) != 0)
            {
                break;
            }
            if (interval.type == V4L2_FRMIVAL_TYPE_DISCRETE)
            {
                if (interval.u0 != 0)
                {
                    rates.Add(Math.Round((double)interval.u1 / interval.u0, 3));
                }
            }
            else
            {
                // Continuous/stepwise: min interval (u0/u1) = fastest rate, max = slowest.
                if (interval.u0 != 0 && interval.u2 != 0)
                {
                    rates.Add(Math.Round((double)interval.u1 / interval.u0, 3));
                    rates.Add(Math.Round((double)interval.u3 / interval.u2, 3));
                    isRange = true;
                }
                break;
            }
        }
        rates.Sort((a, b) => b.CompareTo(a));
        return rates;
    }

    private static List<IImagingDeviceControl> ReadControls(int fd, string devicePath)
    {
        // First pass: query everything the driver advertises via the NEXT_CTRL walk.
        var queried = new List<v4l2_queryctrl>();
        var query = new v4l2_queryctrl { id = V4L2_CTRL_FLAG_NEXT_CTRL };
        while (Ioctl(fd, (UIntPtr)VIDIOC_QUERYCTRL, &query) == 0)
        {
            if ((query.flags & V4L2_CTRL_FLAG_DISABLED) == 0
                && query.type is V4L2_CTRL_TYPE_INTEGER or V4L2_CTRL_TYPE_BOOLEAN or V4L2_CTRL_TYPE_MENU)
            {
                queried.Add(query);
            }
            query.id |= V4L2_CTRL_FLAG_NEXT_CTRL;
        }

        var presentIds = new HashSet<uint>(queried.Select(q => q.id));
        var controls = new List<IImagingDeviceControl>();
        foreach (var q in queried)
        {
            string name;
            var localQ = q;
            name = FixedBytesToString(localQ.name, 32);

            var controlType = q.type switch
            {
                V4L2_CTRL_TYPE_BOOLEAN => ImagingDeviceControlType.Boolean,
                V4L2_CTRL_TYPE_MENU => ImagingDeviceControlType.Menu,
                _ => ImagingDeviceControlType.Integer,
            };

            // Wire the automatic-mode sibling for the three classic manual/auto pairs.
            uint autoSibling = 0;
            int autoOn = 1, autoOff = 0;
            switch (q.id)
            {
                case V4L2_CID_FOCUS_ABSOLUTE when presentIds.Contains(V4L2_CID_FOCUS_AUTO):
                    autoSibling = V4L2_CID_FOCUS_AUTO;
                    break;
                case V4L2_CID_WHITE_BALANCE_TEMPERATURE when presentIds.Contains(V4L2_CID_AUTO_WHITE_BALANCE):
                    autoSibling = V4L2_CID_AUTO_WHITE_BALANCE;
                    break;
                case V4L2_CID_EXPOSURE_ABSOLUTE when presentIds.Contains(V4L2_CID_EXPOSURE_AUTO):
                    autoSibling = V4L2_CID_EXPOSURE_AUTO;
                    autoOn = V4L2_EXPOSURE_APERTURE_PRIORITY;
                    autoOff = V4L2_EXPOSURE_MANUAL;
                    break;
            }

            controls.Add(new V4l2DeviceControl(devicePath, MapControlKind(q.id), name, q.id,
                controlType, q.minimum, q.maximum, q.step, q.default_value,
                autoSibling, autoOn, autoOff));
        }
        return controls;
    }

    private static string FindUsbDeviceDir(string node)
    {
        // /sys/class/video4linux/videoN is a symlink like
        // ../../devices/pci0000:00/.../usb1/1-12/1-12:1.2/video4linux/videoN. The USB
        // *device* directory (recognizable by its idVendor attribute) sits a few levels
        // above the symlink TARGET. Managed file APIs are useless for walking "through"
        // the symlink: .NET normalizes ".." lexically (never physically), so both
        // ResolveLinkTarget and "..\"-based paths land in the wrong place. Instead,
        // resolve the link target against the REAL /sys/class/video4linux directory —
        // that combination is lexically safe — and then climb genuine parent
        // directories of the physical path.
        var link = new DirectoryInfo(Path.Combine(SysVideo4Linux, node));
        var target = link.LinkTarget;
        var physicalNodeDir = target == null
            ? link.FullName
            : Path.GetFullPath(Path.IsPathRooted(target) ? target : Path.Combine(SysVideo4Linux, target));

        var dir = new DirectoryInfo(physicalNodeDir);
        for (var depth = 0; dir != null && depth < 8; depth++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "idVendor")))
            {
                return dir.FullName;
            }
        }
        return null;
    }

    private static (ushort VendorId, ushort ProductId, string Product, string SerialNumber) ReadUsbInfo(
        string node)
    {
        try
        {
            var usbDeviceDir = FindUsbDeviceDir(node);
            if (usbDeviceDir != null)
            {
                return (ReadHexAttribute(Path.Combine(usbDeviceDir, "idVendor")),
                    ReadHexAttribute(Path.Combine(usbDeviceDir, "idProduct")),
                    ReadTextAttribute(Path.Combine(usbDeviceDir, "product")),
                    ReadTextAttribute(Path.Combine(usbDeviceDir, "serial")));
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine($"CodeBrix.Webcam: USB info walk failed for {node}: {e.Message}");
        }
        return (0, 0, null, null);
    }

    private static ImagingAudioPairing FindPairedMicrophone(string node, string cameraName)
    {
        try
        {
            // Find the camera's USB device directory (see FindUsbDeviceDir) and look
            // through its interface subdirectories for an ALSA sound card — a webcam's
            // built-in mic is an audio interface on the same USB device.
            var usbDeviceDir = FindUsbDeviceDir(node);
            if (usbDeviceDir == null)
            {
                return null;
            }

            foreach (var sibling in Directory.EnumerateDirectories(usbDeviceDir))
            {
                if (!UsbInterfaceDirPattern.IsMatch(Path.GetFileName(sibling)))
                {
                    continue;
                }
                var soundDir = Path.Combine(sibling, "sound");
                if (!Directory.Exists(soundDir))
                {
                    continue;
                }
                foreach (var cardDir in Directory.EnumerateDirectories(soundDir))
                {
                    var match = SoundCardDirPattern.Match(Path.GetFileName(cardDir));
                    if (match.Success)
                    {
                        var cardNumber = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                        return new ImagingAudioPairing($"hw:{cardNumber},0", $"{cameraName} Microphone");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine($"CodeBrix.Webcam: microphone pairing failed for {node}: {e.Message}");
        }
        return null;
    }

    private static ushort ReadHexAttribute(string path)
    {
        try
        {
            if (File.Exists(path)
                && ushort.TryParse(File.ReadAllText(path).Trim(), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }
        catch (IOException)
        {
        }
        return 0;
    }

    private static string ReadTextAttribute(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                return text.Length > 0 ? text : null;
            }
        }
        catch (IOException)
        {
        }
        return null;
    }

    internal static ImagingPixelFormat MapPixelFormat(string fourCc) => fourCc switch
    {
        "MJPG" => ImagingPixelFormat.Mjpeg,
        "YUYV" => ImagingPixelFormat.Yuyv,
        "NV12" => ImagingPixelFormat.Nv12,
        "H264" => ImagingPixelFormat.H264,
        "RGB3" or "BGR3" => ImagingPixelFormat.Rgb24,
        "RGB4" or "BGR4" or "AR24" or "XR24" or "BA24" => ImagingPixelFormat.Rgb32,
        "GREY" => ImagingPixelFormat.Grey,
        _ => ImagingPixelFormat.Unknown,
    };

    internal static ImagingDeviceControlKind MapControlKind(uint controlId) => controlId switch
    {
        V4L2_CID_BRIGHTNESS => ImagingDeviceControlKind.Brightness,
        V4L2_CID_CONTRAST => ImagingDeviceControlKind.Contrast,
        V4L2_CID_SATURATION => ImagingDeviceControlKind.Saturation,
        V4L2_CID_HUE => ImagingDeviceControlKind.Hue,
        V4L2_CID_GAMMA => ImagingDeviceControlKind.Gamma,
        V4L2_CID_SHARPNESS => ImagingDeviceControlKind.Sharpness,
        V4L2_CID_GAIN => ImagingDeviceControlKind.Gain,
        V4L2_CID_WHITE_BALANCE_TEMPERATURE => ImagingDeviceControlKind.WhiteBalanceTemperature,
        V4L2_CID_AUTO_WHITE_BALANCE => ImagingDeviceControlKind.AutoWhiteBalance,
        V4L2_CID_EXPOSURE_ABSOLUTE => ImagingDeviceControlKind.ExposureTime,
        V4L2_CID_EXPOSURE_AUTO => ImagingDeviceControlKind.AutoExposure,
        V4L2_CID_FOCUS_ABSOLUTE => ImagingDeviceControlKind.Focus,
        V4L2_CID_FOCUS_AUTO => ImagingDeviceControlKind.AutoFocus,
        V4L2_CID_ZOOM_ABSOLUTE => ImagingDeviceControlKind.Zoom,
        V4L2_CID_PAN_ABSOLUTE => ImagingDeviceControlKind.Pan,
        V4L2_CID_TILT_ABSOLUTE => ImagingDeviceControlKind.Tilt,
        V4L2_CID_BACKLIGHT_COMPENSATION => ImagingDeviceControlKind.BacklightCompensation,
        V4L2_CID_POWER_LINE_FREQUENCY => ImagingDeviceControlKind.PowerLineFrequency,
        _ => ImagingDeviceControlKind.Unknown,
    };
}
