using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// The minimal DirectShow COM surface needed for device enumeration, capability
/// listing, and camera-control access on Windows. These interfaces compile on every
/// platform; they are only ever instantiated behind OperatingSystem.IsWindows() guards.
/// </summary>
internal static class DirectShowNativeMethods
{
    internal static readonly Guid ClsidSystemDeviceEnum = new Guid("62BE5D10-60EB-11D0-BD3B-00A0C911CE86");
    internal static readonly Guid ClsidVideoInputDeviceCategory = new Guid("860BB310-5D01-11D0-BD3B-00A0C911CE86");
    internal static readonly Guid ClsidAudioInputDeviceCategory = new Guid("33D9A762-90C8-11D0-BD43-00A0C911CE86");
    internal static readonly Guid IidBaseFilter = new Guid("56A86895-0AD4-11CE-B03A-0020AF0BA770");
    internal static readonly Guid FormatVideoInfo = new Guid("05589F80-C356-11CE-BF01-00AA0055595A");

    internal static readonly Guid MediaSubtypeMjpg = new Guid("47504A4D-0000-0010-8000-00AA00389B71");
    internal static readonly Guid MediaSubtypeYuy2 = new Guid("32595559-0000-0010-8000-00AA00389B71");
    internal static readonly Guid MediaSubtypeNv12 = new Guid("3231564E-0000-0010-8000-00AA00389B71");
    internal static readonly Guid MediaSubtypeH264 = new Guid("34363248-0000-0010-8000-00AA00389B71");
    internal static readonly Guid MediaSubtypeRgb24 = new Guid("E436EB7D-524F-11CE-9F53-0020AF0BA770");
    internal static readonly Guid MediaSubtypeRgb32 = new Guid("E436EB7E-524F-11CE-9F53-0020AF0BA770");

    // IAMVideoProcAmp property ids.
    internal const int VideoProcAmp_Brightness = 0;
    internal const int VideoProcAmp_Contrast = 1;
    internal const int VideoProcAmp_Hue = 2;
    internal const int VideoProcAmp_Saturation = 3;
    internal const int VideoProcAmp_Sharpness = 4;
    internal const int VideoProcAmp_Gamma = 5;
    internal const int VideoProcAmp_ColorEnable = 6;
    internal const int VideoProcAmp_WhiteBalance = 7;
    internal const int VideoProcAmp_BacklightCompensation = 8;
    internal const int VideoProcAmp_Gain = 9;

    // IAMCameraControl property ids.
    internal const int CameraControl_Pan = 0;
    internal const int CameraControl_Tilt = 1;
    internal const int CameraControl_Roll = 2;
    internal const int CameraControl_Zoom = 3;
    internal const int CameraControl_Exposure = 4;
    internal const int CameraControl_Iris = 5;
    internal const int CameraControl_Focus = 6;

    // Shared flag values for both control interfaces.
    internal const int ControlFlags_Auto = 1;
    internal const int ControlFlags_Manual = 2;

    [ComImport]
    [Guid("29840822-5B84-11D0-BD3B-00A0C911CE86")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICreateDevEnum
    {
        [PreserveSig]
        int CreateClassEnumerator([In] ref Guid deviceClass, out IEnumMoniker enumMoniker, int flags);
    }

    [ComImport]
    [Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyBag
    {
        [PreserveSig]
        int Read([MarshalAs(UnmanagedType.LPWStr)] string propertyName,
            [MarshalAs(UnmanagedType.Struct)] out object value, IntPtr errorLog);

        [PreserveSig]
        int Write([MarshalAs(UnmanagedType.LPWStr)] string propertyName,
            [MarshalAs(UnmanagedType.Struct)] ref object value);
    }

    [ComImport]
    [Guid("56A86895-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IBaseFilter
    {
        // IPersist
        [PreserveSig]
        int GetClassID(out Guid classId);

        // IMediaFilter
        [PreserveSig]
        int Stop();

        [PreserveSig]
        int Pause();

        [PreserveSig]
        int Run(long start);

        [PreserveSig]
        int GetState(int millisecondsTimeout, out int filterState);

        [PreserveSig]
        int SetSyncSource(IntPtr clock);

        [PreserveSig]
        int GetSyncSource(out IntPtr clock);

        // IBaseFilter
        [PreserveSig]
        int EnumPins(out IEnumPins enumPins);

        [PreserveSig]
        int FindPin([MarshalAs(UnmanagedType.LPWStr)] string id, out IPin pin);

        [PreserveSig]
        int QueryFilterInfo(IntPtr filterInfo);

        [PreserveSig]
        int JoinFilterGraph(IntPtr graph, [MarshalAs(UnmanagedType.LPWStr)] string name);

        [PreserveSig]
        int QueryVendorInfo([MarshalAs(UnmanagedType.LPWStr)] out string vendorInfo);
    }

    [ComImport]
    [Guid("56A86892-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnumPins
    {
        [PreserveSig]
        int Next(int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IPin[] pins,
            IntPtr fetched);

        [PreserveSig]
        int Skip(int count);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone(out IEnumPins enumPins);
    }

    [ComImport]
    [Guid("56A86891-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPin
    {
        [PreserveSig]
        int Connect(IPin receivePin, IntPtr mediaType);

        [PreserveSig]
        int ReceiveConnection(IPin receivePin, IntPtr mediaType);

        [PreserveSig]
        int Disconnect();

        [PreserveSig]
        int ConnectedTo(out IPin pin);

        [PreserveSig]
        int ConnectionMediaType(IntPtr mediaType);

        [PreserveSig]
        int QueryPinInfo(IntPtr pinInfo);

        [PreserveSig]
        int QueryDirection(out int pinDirection);

        [PreserveSig]
        int QueryId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int QueryAccept(IntPtr mediaType);

        [PreserveSig]
        int EnumMediaTypes(IntPtr enumMediaTypes);

        [PreserveSig]
        int QueryInternalConnections(IntPtr pins, ref int pinCount);

        [PreserveSig]
        int EndOfStream();

        [PreserveSig]
        int BeginFlush();

        [PreserveSig]
        int EndFlush();

        [PreserveSig]
        int NewSegment(long start, long stop, double rate);
    }

    [ComImport]
    [Guid("C6E13340-30AC-11D0-A18C-00A0C9118956")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAMStreamConfig
    {
        [PreserveSig]
        int SetFormat(IntPtr mediaType);

        [PreserveSig]
        int GetFormat(out IntPtr mediaType);

        [PreserveSig]
        int GetNumberOfCapabilities(out int count, out int size);

        [PreserveSig]
        int GetStreamCaps(int index, out IntPtr mediaType, IntPtr streamConfigCaps);
    }

    [ComImport]
    [Guid("C6E13360-30AC-11D0-A18C-00A0C9118956")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAMVideoProcAmp
    {
        [PreserveSig]
        int GetRange(int property, out int min, out int max, out int steppingDelta,
            out int defaultValue, out int capsFlags);

        [PreserveSig]
        int Set(int property, int value, int flags);

        [PreserveSig]
        int Get(int property, out int value, out int flags);
    }

    [ComImport]
    [Guid("C6E13370-30AC-11D0-A18C-00A0C9118956")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAMCameraControl
    {
        [PreserveSig]
        int GetRange(int property, out int min, out int max, out int steppingDelta,
            out int defaultValue, out int capsFlags);

        [PreserveSig]
        int Set(int property, int value, int flags);

        [PreserveSig]
        int Get(int property, out int value, out int flags);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AmMediaType
    {
        public Guid MajorType;
        public Guid SubType;
        [MarshalAs(UnmanagedType.Bool)]
        public bool FixedSizeSamples;
        [MarshalAs(UnmanagedType.Bool)]
        public bool TemporalCompression;
        public uint SampleSize;
        public Guid FormatType;
        public IntPtr Unknown;
        public uint FormatSize;
        public IntPtr FormatPointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VideoStreamConfigCaps // 128 bytes
    {
        public Guid Guid;
        public uint VideoStandard;
        public int InputSizeWidth;
        public int InputSizeHeight;
        public int MinCroppingSizeWidth;
        public int MinCroppingSizeHeight;
        public int MaxCroppingSizeWidth;
        public int MaxCroppingSizeHeight;
        public int CropGranularityX;
        public int CropGranularityY;
        public int CropAlignX;
        public int CropAlignY;
        public int MinOutputSizeWidth;
        public int MinOutputSizeHeight;
        public int MaxOutputSizeWidth;
        public int MaxOutputSizeHeight;
        public int OutputGranularityX;
        public int OutputGranularityY;
        public int StretchTapsX;
        public int StretchTapsY;
        public int ShrinkTapsX;
        public int ShrinkTapsY;
        public long MinFrameInterval; // 100 ns units
        public long MaxFrameInterval; // 100 ns units
        public int MinBitsPerSecond;
        public int MaxBitsPerSecond;
    }

    // VIDEOINFOHEADER field offsets (from the AM_MEDIA_TYPE format pointer).
    internal const int VideoInfoHeader_AvgTimePerFrameOffset = 40;
    internal const int VideoInfoHeader_BiWidthOffset = 52;
    internal const int VideoInfoHeader_BiHeightOffset = 56;

    [DllImport("ole32.dll")]
    internal static extern int CreateBindCtx(uint reserved, out IBindCtx bindCtx);

    /// <summary>Frees an AM_MEDIA_TYPE allocated by DirectShow.</summary>
    internal static void FreeMediaType(IntPtr mediaTypePointer)
    {
        if (mediaTypePointer == IntPtr.Zero)
        {
            return;
        }
        var mediaType = Marshal.PtrToStructure<AmMediaType>(mediaTypePointer);
        if (mediaType.FormatPointer != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(mediaType.FormatPointer);
        }
        if (mediaType.Unknown != IntPtr.Zero)
        {
            Marshal.Release(mediaType.Unknown);
        }
        Marshal.FreeCoTaskMem(mediaTypePointer);
    }
}
