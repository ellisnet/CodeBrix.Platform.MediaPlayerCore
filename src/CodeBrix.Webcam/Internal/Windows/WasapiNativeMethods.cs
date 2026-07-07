using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// The minimal WASAPI (Windows Audio Session API) COM surface needed for microphone
/// capture (recording audio and the sidecar WAV) and live monitoring playback. These
/// interfaces compile on every platform; they are only ever instantiated behind
/// OperatingSystem.IsWindows() guards.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WasapiNativeMethods
{
    internal static readonly Guid ClsidMMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    internal static readonly Guid IidIAudioClient = new Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    internal static readonly Guid IidIAudioCaptureClient = new Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
    internal static readonly Guid IidIAudioRenderClient = new Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");

    // PKEY_Device_FriendlyName: fmtid {A45C254E-DF1C-4EFD-8020-67D146A850E0}, pid 14.
    internal static readonly Guid PkeyDeviceFriendlyNameFmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0");
    internal const uint PkeyDeviceFriendlyNamePid = 14;

    internal const int ERender = 0;
    internal const int ECapture = 1;
    internal const int EConsole = 0;

    internal const uint DeviceStateActive = 0x1;

    internal const int AudclntShareModeShared = 0;

    // Let the audio engine convert between our fixed PCM format and the device mix
    // format (Windows 10+): AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM | SRC_DEFAULT_QUALITY.
    internal const uint AudclntStreamFlagsAutoConvertPcm = 0x80000000;
    internal const uint AudclntStreamFlagsSrcDefaultQuality = 0x08000000;

    internal const ushort WaveFormatPcm = 1;

    internal const uint StgmRead = 0;
    internal const uint ClsctxAll = 0x17;

    /// <summary>WAVEFORMATEX for plain PCM (cbSize 0).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct WaveFormatEx
    {
        internal ushort FormatTag;
        internal ushort Channels;
        internal uint SamplesPerSecond;
        internal uint AverageBytesPerSecond;
        internal ushort BlockAlign;
        internal ushort BitsPerSample;
        internal ushort ExtraSize;
    }

    /// <summary>PROPERTYKEY for IPropertyStore access.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        internal Guid FormatId;
        internal uint PropertyId;
    }

    /// <summary>Minimal PROPVARIANT layout — enough to read VT_LPWSTR values.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PropVariant
    {
        internal ushort VariantType;
        internal ushort Reserved1;
        internal ushort Reserved2;
        internal ushort Reserved3;
        internal IntPtr Pointer;
        internal IntPtr Pointer2;
    }

    internal const ushort VtLpwstr = 31;

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern int PropVariantClear(ref PropVariant propVariant);

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string deviceId, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate([In] ref Guid iid, uint clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(uint accessMode, out IPropertyStore propertyStore);
        [PreserveSig] int GetId(out IntPtr deviceId);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue([In] ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue([In] ref PropertyKey key, [In] ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [ComImport]
    [Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration,
            long devicePeriod, [In] ref WaveFormatEx format, IntPtr audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint bufferFrames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint paddingFrames);
        [PreserveSig] int IsFormatSupported(int shareMode, [In] ref WaveFormatEx format, out IntPtr closestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr eventHandle);
        [PreserveSig] int GetService([In] ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport]
    [Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frameCount, out uint flags,
            out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frameCount);
        [PreserveSig] int GetNextPacketSize(out uint frameCount);
    }

    [ComImport]
    [Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(uint frameCount, out IntPtr data);
        [PreserveSig] int ReleaseBuffer(uint frameCount, uint flags);
    }

    /// <summary>Reads a device's friendly name from its property store, or null.</summary>
    internal static string GetDeviceFriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(StgmRead, out var store) != 0 || store == null)
        {
            return null;
        }
        try
        {
            var key = new PropertyKey
            {
                FormatId = PkeyDeviceFriendlyNameFmtid,
                PropertyId = PkeyDeviceFriendlyNamePid,
            };
            if (store.GetValue(ref key, out var value) != 0)
            {
                return null;
            }
            try
            {
                return value.VariantType == VtLpwstr && value.Pointer != IntPtr.Zero
                    ? Marshal.PtrToStringUni(value.Pointer)
                    : null;
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }
}
