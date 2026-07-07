using System;
using System.Runtime.InteropServices;

namespace CodeBrix.Webcam.Internal.Linux;

/// <summary>
/// Raw libc / V4L2 interop for Linux camera enumeration and control. All ioctl request
/// codes are precomputed from the kernel's _IOC macro for the x86_64 struct sizes laid
/// out below — if a struct changes shape, its request code must be recomputed.
/// </summary>
internal static unsafe class V4l2NativeMethods
{
    internal const int O_RDWR = 2;

    internal const int EINVAL = 22;

    // V4L2 buffer type: video capture.
    internal const uint V4L2_BUF_TYPE_VIDEO_CAPTURE = 1;

    // v4l2_capability.device_caps flags.
    internal const uint V4L2_CAP_VIDEO_CAPTURE = 0x00000001;

    // Frame size / frame interval enumeration types.
    internal const uint V4L2_FRMSIZE_TYPE_DISCRETE = 1;
    internal const uint V4L2_FRMSIZE_TYPE_CONTINUOUS = 2;
    internal const uint V4L2_FRMSIZE_TYPE_STEPWISE = 3;
    internal const uint V4L2_FRMIVAL_TYPE_DISCRETE = 1;

    // Control enumeration.
    internal const uint V4L2_CTRL_FLAG_DISABLED = 0x00000001;
    internal const uint V4L2_CTRL_FLAG_NEXT_CTRL = 0x80000000;
    internal const uint V4L2_CTRL_TYPE_INTEGER = 1;
    internal const uint V4L2_CTRL_TYPE_BOOLEAN = 2;
    internal const uint V4L2_CTRL_TYPE_MENU = 3;

    // Well-known control ids (user class base 0x00980900, camera class base 0x009A0900).
    internal const uint V4L2_CID_BRIGHTNESS = 0x00980900;
    internal const uint V4L2_CID_CONTRAST = 0x00980901;
    internal const uint V4L2_CID_SATURATION = 0x00980902;
    internal const uint V4L2_CID_HUE = 0x00980903;
    internal const uint V4L2_CID_AUTO_WHITE_BALANCE = 0x0098090C;
    internal const uint V4L2_CID_GAMMA = 0x00980910;
    internal const uint V4L2_CID_GAIN = 0x00980913;
    internal const uint V4L2_CID_POWER_LINE_FREQUENCY = 0x00980918;
    internal const uint V4L2_CID_WHITE_BALANCE_TEMPERATURE = 0x0098091A;
    internal const uint V4L2_CID_SHARPNESS = 0x0098091B;
    internal const uint V4L2_CID_BACKLIGHT_COMPENSATION = 0x0098091C;
    internal const uint V4L2_CID_EXPOSURE_AUTO = 0x009A0901;
    internal const uint V4L2_CID_EXPOSURE_ABSOLUTE = 0x009A0902;
    internal const uint V4L2_CID_PAN_ABSOLUTE = 0x009A0908;
    internal const uint V4L2_CID_TILT_ABSOLUTE = 0x009A0909;
    internal const uint V4L2_CID_FOCUS_ABSOLUTE = 0x009A090A;
    internal const uint V4L2_CID_FOCUS_AUTO = 0x009A090C;
    internal const uint V4L2_CID_ZOOM_ABSOLUTE = 0x009A090D;

    // V4L2_CID_EXPOSURE_AUTO menu values.
    internal const int V4L2_EXPOSURE_MANUAL = 1;
    internal const int V4L2_EXPOSURE_APERTURE_PRIORITY = 3;

    // ioctl request codes: _IOC(dir, 'V', nr, sizeof(struct)) with
    // dir: read=2, write=1, rw=3 → (dir<<30) | (size<<16) | ('V'<<8) | nr.
    internal const uint VIDIOC_QUERYCAP = 0x80685600;           // _IOR ('V',  0, 104)
    internal const uint VIDIOC_ENUM_FMT = 0xC0405602;           // _IOWR('V',  2,  64)
    internal const uint VIDIOC_G_CTRL = 0xC008561B;             // _IOWR('V', 27,   8)
    internal const uint VIDIOC_S_CTRL = 0xC008561C;             // _IOWR('V', 28,   8)
    internal const uint VIDIOC_QUERYCTRL = 0xC0445624;          // _IOWR('V', 36,  68)
    internal const uint VIDIOC_ENUM_FRAMESIZES = 0xC02C564A;    // _IOWR('V', 74,  44)
    internal const uint VIDIOC_ENUM_FRAMEINTERVALS = 0xC034564B; // _IOWR('V', 75,  52)

    [StructLayout(LayoutKind.Sequential)]
    internal struct v4l2_capability // 104 bytes
    {
        public fixed byte driver[16];
        public fixed byte card[32];
        public fixed byte bus_info[32];
        public uint version;
        public uint capabilities;
        public uint device_caps;
        public fixed uint reserved[3];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct v4l2_fmtdesc // 64 bytes
    {
        public uint index;
        public uint type;
        public uint flags;
        public fixed byte description[32];
        public uint pixelformat;
        public fixed uint reserved[4];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct v4l2_frmsizeenum // 44 bytes
    {
        public uint index;
        public uint pixel_format;
        public uint type;
        // Union: discrete { width, height } / stepwise { min_w, max_w, step_w, min_h, max_h, step_h }.
        public uint u0; // discrete.width  | stepwise.min_width
        public uint u1; // discrete.height | stepwise.max_width
        public uint u2; //                 | stepwise.step_width
        public uint u3; //                 | stepwise.min_height
        public uint u4; //                 | stepwise.max_height
        public uint u5; //                 | stepwise.step_height
        public fixed uint reserved[2];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct v4l2_frmivalenum // 52 bytes
    {
        public uint index;
        public uint pixel_format;
        public uint width;
        public uint height;
        public uint type;
        // Union: discrete v4l2_fract { numerator, denominator } / stepwise { 3 fracts }.
        public uint u0; // discrete.numerator   | stepwise.min.numerator
        public uint u1; // discrete.denominator | stepwise.min.denominator
        public uint u2;
        public uint u3;
        public uint u4;
        public uint u5;
        public fixed uint reserved[2];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct v4l2_queryctrl // 68 bytes
    {
        public uint id;
        public uint type;
        public fixed byte name[32];
        public int minimum;
        public int maximum;
        public int step;
        public int default_value;
        public uint flags;
        public fixed uint reserved[2];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct v4l2_control // 8 bytes
    {
        public uint id;
        public int value;
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    internal static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    internal static extern int Close(int fd);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    internal static extern int Ioctl(int fd, UIntPtr request, void* argument);

    /// <summary>Reads a fixed-length, NUL-terminated byte field as a string.</summary>
    internal static string FixedBytesToString(byte* bytes, int maxLength)
    {
        var length = 0;
        while (length < maxLength && bytes[length] != 0)
        {
            length++;
        }
        return System.Text.Encoding.UTF8.GetString(bytes, length);
    }

    /// <summary>Renders a V4L2 pixel format code as its four-character string (e.g. "MJPG").</summary>
    internal static string FourCcToString(uint fourCc)
    {
        Span<char> chars = stackalloc char[4];
        chars[0] = (char)(fourCc & 0xFF);
        chars[1] = (char)((fourCc >> 8) & 0xFF);
        chars[2] = (char)((fourCc >> 16) & 0xFF);
        chars[3] = (char)((fourCc >> 24) & 0xFF);
        return new string(chars);
    }
}
