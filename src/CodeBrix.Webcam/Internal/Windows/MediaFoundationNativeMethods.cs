using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// The minimal Media Foundation COM surface needed for native Windows webcam capture
/// (IMFSourceReader) and MP4/H.264 recording (IMFSinkWriter). These interfaces compile
/// on every platform; they are only ever instantiated behind
/// OperatingSystem.IsWindows() guards. COM interface inheritance is flattened (each
/// interface redeclares its base methods) because the CLR builds ComImport vtables
/// from the declared method list only.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class MediaFoundationNativeMethods
{
    // MF_VERSION for MFStartup: (MF_SDK_VERSION 0x0002 << 16) | MF_API_VERSION 0x0070.
    internal const uint MfVersion = 0x00020070;
    internal const uint MfStartupFull = 0;

    // "First video stream" pseudo-index for IMFSourceReader stream arguments.
    internal const uint MfSourceReaderFirstVideoStream = 0xFFFFFFFC;

    // MF_SOURCE_READER_FLAG values returned by ReadSample.
    internal const uint MfSourceReaderFlagError = 0x1;
    internal const uint MfSourceReaderFlagEndOfStream = 0x2;
    internal const uint MfSourceReaderFlagNewStream = 0x4;
    internal const uint MfSourceReaderFlagNativeMediaTypeChanged = 0x10;
    internal const uint MfSourceReaderFlagCurrentMediaTypeChanged = 0x20;
    internal const uint MfSourceReaderFlagStreamTick = 0x100;

    internal const uint MfVideoInterlaceProgressive = 2;

    // HRESULT raised by GetNativeMediaType when the type index runs off the end.
    internal const int MfErrorNoMoreTypes = unchecked((int)0xC00D36B9);

    // Device-source attributes (device enumeration / selection).
    internal static readonly Guid MfDevsourceAttributeSourceType = new Guid("c60ac5fe-252a-478f-a0ef-bc8fa5f7cad3");
    internal static readonly Guid MfDevsourceAttributeSourceTypeVidcapGuid = new Guid("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");
    internal static readonly Guid MfDevsourceAttributeFriendlyName = new Guid("60d0e559-52f8-4fa2-bbce-acdb34a8ec01");
    internal static readonly Guid MfDevsourceAttributeVidcapSymbolicLink = new Guid("58f0aad8-22bf-4f8a-bb3d-d2c4978c6e2f");

    // Media-type attributes.
    internal static readonly Guid MfMtMajorType = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    internal static readonly Guid MfMtSubtype = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    internal static readonly Guid MfMtFrameSize = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
    internal static readonly Guid MfMtFrameRate = new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    internal static readonly Guid MfMtDefaultStride = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    internal static readonly Guid MfMtInterlaceMode = new Guid("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    internal static readonly Guid MfMtAvgBitrate = new Guid("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    internal static readonly Guid MfMtAllSamplesIndependent = new Guid("c9173739-5e56-461c-b713-46fb995cb95f");
    internal static readonly Guid MfMtAudioSamplesPerSecond = new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    internal static readonly Guid MfMtAudioNumChannels = new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    internal static readonly Guid MfMtAudioBitsPerSample = new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    internal static readonly Guid MfMtAudioAvgBytesPerSecond = new Guid("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    internal static readonly Guid MfMtAudioBlockAlignment = new Guid("322de230-9eeb-43bd-ab7a-ff412251541d");

    // Major types and subtypes.
    internal static readonly Guid MfMediaTypeVideo = new Guid("73646976-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MfMediaTypeAudio = new Guid("73647561-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MfVideoFormatRgb32 = new Guid("00000016-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MfVideoFormatH264 = new Guid("34363248-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MfVideoFormatMjpg = new Guid("47504a4d-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MfVideoFormatYuy2 = new Guid("32595559-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MfVideoFormatNv12 = new Guid("3231564e-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MfAudioFormatPcm = new Guid("00000001-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MfAudioFormatAac = new Guid("00001610-0000-0010-8000-00aa00389b71");

    // Source-reader / sink-writer configuration attributes.
    internal static readonly Guid MfSourceReaderEnableAdvancedVideoProcessing = new Guid("0f81da2c-b537-4672-a8b2-a681b17307a3");
    internal static readonly Guid MfReadWriteEnableHardwareTransforms = new Guid("a634a91c-822b-41b9-a494-4de4643612b0");
    internal static readonly Guid MfSinkWriterDisableThrottling = new Guid("08b845d8-2b74-4afe-9d53-be16d2d5ae4f");

    internal static readonly Guid IidIMFMediaSource = new Guid("279a808d-aec7-40c8-9c6b-a6b492c78a66");

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFStartup(uint version, uint flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFCreateAttributes(out IMFAttributes attributes, uint initialSize);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFCreateMediaType(out IMFMediaType mediaType);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFCreateSample(out IMFSample sample);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFCreateMemoryBuffer(uint maxLength, out IMFMediaBuffer buffer);

    [DllImport("mf.dll", ExactSpelling = true)]
    internal static extern int MFEnumDeviceSources(IMFAttributes attributes,
        out IntPtr activateArray, out uint count);

    [DllImport("mfreadwrite.dll", ExactSpelling = true)]
    internal static extern int MFCreateSourceReaderFromMediaSource(
        [MarshalAs(UnmanagedType.IUnknown)] object mediaSource, IMFAttributes attributes,
        out IMFSourceReader sourceReader);

    [DllImport("mfreadwrite.dll", ExactSpelling = true)]
    internal static extern int MFCreateSinkWriterFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string outputUrl, IntPtr byteStream,
        IMFAttributes attributes, out IMFSinkWriter sinkWriter);

    /// <summary>
    /// IMFAttributes. Attribute-store base of most Media Foundation objects. The
    /// PROPVARIANT-typed members are declared with IntPtr parameters (never called
    /// here); only the typed getters/setters are used.
    /// </summary>
    [ComImport]
    [Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFAttributes
    {
        [PreserveSig] int GetItem([In] ref Guid key, IntPtr value);
        [PreserveSig] int GetItemType([In] ref Guid key, out int type);
        [PreserveSig] int CompareItem([In] ref Guid key, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool result);
        [PreserveSig] int Compare(IntPtr theirs, int matchType, [MarshalAs(UnmanagedType.Bool)] out bool result);
        [PreserveSig] int GetUINT32([In] ref Guid key, out uint value);
        [PreserveSig] int GetUINT64([In] ref Guid key, out ulong value);
        [PreserveSig] int GetDouble([In] ref Guid key, out double value);
        [PreserveSig] int GetGUID([In] ref Guid key, out Guid value);
        [PreserveSig] int GetStringLength([In] ref Guid key, out uint length);
        [PreserveSig] int GetString([In] ref Guid key, IntPtr value, uint size, IntPtr length);
        [PreserveSig] int GetAllocatedString([In] ref Guid key, out IntPtr value, out uint length);
        [PreserveSig] int GetBlobSize([In] ref Guid key, out uint size);
        [PreserveSig] int GetBlob([In] ref Guid key, IntPtr buffer, uint size, IntPtr blobSize);
        [PreserveSig] int GetAllocatedBlob([In] ref Guid key, out IntPtr buffer, out uint size);
        [PreserveSig] int GetUnknown([In] ref Guid key, [In] ref Guid iid, out IntPtr value);
        [PreserveSig] int SetItem([In] ref Guid key, IntPtr value);
        [PreserveSig] int DeleteItem([In] ref Guid key);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32([In] ref Guid key, uint value);
        [PreserveSig] int SetUINT64([In] ref Guid key, ulong value);
        [PreserveSig] int SetDouble([In] ref Guid key, double value);
        [PreserveSig] int SetGUID([In] ref Guid key, [In] ref Guid value);
        [PreserveSig] int SetString([In] ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
        [PreserveSig] int SetBlob([In] ref Guid key, IntPtr buffer, uint size);
        [PreserveSig] int SetUnknown([In] ref Guid key, [MarshalAs(UnmanagedType.IUnknown)] object value);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetItemByIndex(uint index, out Guid key, IntPtr value);
        [PreserveSig] int CopyAllItems(IntPtr destination);
    }

    /// <summary>IMFActivate: a deferred device/object activation handle (flattened IMFAttributes base).</summary>
    [ComImport]
    [Guid("7FEE9E9A-4A89-47A6-899C-B6A53A70FB67")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFActivate
    {
        // IMFAttributes
        [PreserveSig] int GetItem([In] ref Guid key, IntPtr value);
        [PreserveSig] int GetItemType([In] ref Guid key, out int type);
        [PreserveSig] int CompareItem([In] ref Guid key, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool result);
        [PreserveSig] int Compare(IntPtr theirs, int matchType, [MarshalAs(UnmanagedType.Bool)] out bool result);
        [PreserveSig] int GetUINT32([In] ref Guid key, out uint value);
        [PreserveSig] int GetUINT64([In] ref Guid key, out ulong value);
        [PreserveSig] int GetDouble([In] ref Guid key, out double value);
        [PreserveSig] int GetGUID([In] ref Guid key, out Guid value);
        [PreserveSig] int GetStringLength([In] ref Guid key, out uint length);
        [PreserveSig] int GetString([In] ref Guid key, IntPtr value, uint size, IntPtr length);
        [PreserveSig] int GetAllocatedString([In] ref Guid key, out IntPtr value, out uint length);
        [PreserveSig] int GetBlobSize([In] ref Guid key, out uint size);
        [PreserveSig] int GetBlob([In] ref Guid key, IntPtr buffer, uint size, IntPtr blobSize);
        [PreserveSig] int GetAllocatedBlob([In] ref Guid key, out IntPtr buffer, out uint size);
        [PreserveSig] int GetUnknown([In] ref Guid key, [In] ref Guid iid, out IntPtr value);
        [PreserveSig] int SetItem([In] ref Guid key, IntPtr value);
        [PreserveSig] int DeleteItem([In] ref Guid key);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32([In] ref Guid key, uint value);
        [PreserveSig] int SetUINT64([In] ref Guid key, ulong value);
        [PreserveSig] int SetDouble([In] ref Guid key, double value);
        [PreserveSig] int SetGUID([In] ref Guid key, [In] ref Guid value);
        [PreserveSig] int SetString([In] ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
        [PreserveSig] int SetBlob([In] ref Guid key, IntPtr buffer, uint size);
        [PreserveSig] int SetUnknown([In] ref Guid key, [MarshalAs(UnmanagedType.IUnknown)] object value);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetItemByIndex(uint index, out Guid key, IntPtr value);
        [PreserveSig] int CopyAllItems(IntPtr destination);

        // IMFActivate
        [PreserveSig] int ActivateObject([In] ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int ShutdownObject();
        [PreserveSig] int DetachObject();
    }

    /// <summary>IMFMediaType: a media-format description (flattened IMFAttributes base).</summary>
    [ComImport]
    [Guid("44AE0FA8-EA31-4109-8D2E-4CAE4997C555")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFMediaType
    {
        // IMFAttributes
        [PreserveSig] int GetItem([In] ref Guid key, IntPtr value);
        [PreserveSig] int GetItemType([In] ref Guid key, out int type);
        [PreserveSig] int CompareItem([In] ref Guid key, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool result);
        [PreserveSig] int Compare(IntPtr theirs, int matchType, [MarshalAs(UnmanagedType.Bool)] out bool result);
        [PreserveSig] int GetUINT32([In] ref Guid key, out uint value);
        [PreserveSig] int GetUINT64([In] ref Guid key, out ulong value);
        [PreserveSig] int GetDouble([In] ref Guid key, out double value);
        [PreserveSig] int GetGUID([In] ref Guid key, out Guid value);
        [PreserveSig] int GetStringLength([In] ref Guid key, out uint length);
        [PreserveSig] int GetString([In] ref Guid key, IntPtr value, uint size, IntPtr length);
        [PreserveSig] int GetAllocatedString([In] ref Guid key, out IntPtr value, out uint length);
        [PreserveSig] int GetBlobSize([In] ref Guid key, out uint size);
        [PreserveSig] int GetBlob([In] ref Guid key, IntPtr buffer, uint size, IntPtr blobSize);
        [PreserveSig] int GetAllocatedBlob([In] ref Guid key, out IntPtr buffer, out uint size);
        [PreserveSig] int GetUnknown([In] ref Guid key, [In] ref Guid iid, out IntPtr value);
        [PreserveSig] int SetItem([In] ref Guid key, IntPtr value);
        [PreserveSig] int DeleteItem([In] ref Guid key);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32([In] ref Guid key, uint value);
        [PreserveSig] int SetUINT64([In] ref Guid key, ulong value);
        [PreserveSig] int SetDouble([In] ref Guid key, double value);
        [PreserveSig] int SetGUID([In] ref Guid key, [In] ref Guid value);
        [PreserveSig] int SetString([In] ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
        [PreserveSig] int SetBlob([In] ref Guid key, IntPtr buffer, uint size);
        [PreserveSig] int SetUnknown([In] ref Guid key, [MarshalAs(UnmanagedType.IUnknown)] object value);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetItemByIndex(uint index, out Guid key, IntPtr value);
        [PreserveSig] int CopyAllItems(IntPtr destination);

        // IMFMediaType
        [PreserveSig] int GetMajorType(out Guid majorType);
        [PreserveSig] int IsCompressedFormat([MarshalAs(UnmanagedType.Bool)] out bool compressed);
        [PreserveSig] int IsEqual(IntPtr mediaType, out uint flags);
        [PreserveSig] int GetRepresentation(Guid representation, out IntPtr value);
        [PreserveSig] int FreeRepresentation(Guid representation, IntPtr value);
    }

    /// <summary>IMFSample: one media sample carrying buffers + timing (flattened IMFAttributes base).</summary>
    [ComImport]
    [Guid("C40A00F2-B93A-4D80-AE8C-5A1C634F58E4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSample
    {
        // IMFAttributes
        [PreserveSig] int GetItem([In] ref Guid key, IntPtr value);
        [PreserveSig] int GetItemType([In] ref Guid key, out int type);
        [PreserveSig] int CompareItem([In] ref Guid key, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool result);
        [PreserveSig] int Compare(IntPtr theirs, int matchType, [MarshalAs(UnmanagedType.Bool)] out bool result);
        [PreserveSig] int GetUINT32([In] ref Guid key, out uint value);
        [PreserveSig] int GetUINT64([In] ref Guid key, out ulong value);
        [PreserveSig] int GetDouble([In] ref Guid key, out double value);
        [PreserveSig] int GetGUID([In] ref Guid key, out Guid value);
        [PreserveSig] int GetStringLength([In] ref Guid key, out uint length);
        [PreserveSig] int GetString([In] ref Guid key, IntPtr value, uint size, IntPtr length);
        [PreserveSig] int GetAllocatedString([In] ref Guid key, out IntPtr value, out uint length);
        [PreserveSig] int GetBlobSize([In] ref Guid key, out uint size);
        [PreserveSig] int GetBlob([In] ref Guid key, IntPtr buffer, uint size, IntPtr blobSize);
        [PreserveSig] int GetAllocatedBlob([In] ref Guid key, out IntPtr buffer, out uint size);
        [PreserveSig] int GetUnknown([In] ref Guid key, [In] ref Guid iid, out IntPtr value);
        [PreserveSig] int SetItem([In] ref Guid key, IntPtr value);
        [PreserveSig] int DeleteItem([In] ref Guid key);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32([In] ref Guid key, uint value);
        [PreserveSig] int SetUINT64([In] ref Guid key, ulong value);
        [PreserveSig] int SetDouble([In] ref Guid key, double value);
        [PreserveSig] int SetGUID([In] ref Guid key, [In] ref Guid value);
        [PreserveSig] int SetString([In] ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
        [PreserveSig] int SetBlob([In] ref Guid key, IntPtr buffer, uint size);
        [PreserveSig] int SetUnknown([In] ref Guid key, [MarshalAs(UnmanagedType.IUnknown)] object value);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetItemByIndex(uint index, out Guid key, IntPtr value);
        [PreserveSig] int CopyAllItems(IntPtr destination);

        // IMFSample
        [PreserveSig] int GetSampleFlags(out uint flags);
        [PreserveSig] int SetSampleFlags(uint flags);
        [PreserveSig] int GetSampleTime(out long timestamp);
        [PreserveSig] int SetSampleTime(long timestamp);
        [PreserveSig] int GetSampleDuration(out long duration);
        [PreserveSig] int SetSampleDuration(long duration);
        [PreserveSig] int GetBufferCount(out uint count);
        [PreserveSig] int GetBufferByIndex(uint index, out IMFMediaBuffer buffer);
        [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
        [PreserveSig] int AddBuffer(IMFMediaBuffer buffer);
        [PreserveSig] int RemoveBufferByIndex(uint index);
        [PreserveSig] int RemoveAllBuffers();
        [PreserveSig] int GetTotalLength(out uint length);
        [PreserveSig] int CopyToBuffer(IMFMediaBuffer buffer);
    }

    /// <summary>IMFMediaBuffer: a block of sample memory.</summary>
    [ComImport]
    [Guid("045FA593-8799-42B8-BC8D-8968C6453507")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out IntPtr buffer, out uint maxLength, out uint currentLength);
        [PreserveSig] int Unlock();
        [PreserveSig] int GetCurrentLength(out uint length);
        [PreserveSig] int SetCurrentLength(uint length);
        [PreserveSig] int GetMaxLength(out uint length);
    }

    /// <summary>
    /// IMFMediaSource (flattened IMFMediaEventGenerator base). Only
    /// <see cref="Shutdown"/> is called from managed code; the other members exist to
    /// keep the vtable layout correct.
    /// </summary>
    [ComImport]
    [Guid("279A808D-AEC7-40C8-9C6B-A6B492C78A66")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFMediaSource
    {
        // IMFMediaEventGenerator
        [PreserveSig] int GetEvent(uint flags, out IntPtr mediaEvent);
        [PreserveSig] int BeginGetEvent(IntPtr callback, IntPtr state);
        [PreserveSig] int EndGetEvent(IntPtr result, out IntPtr mediaEvent);
        [PreserveSig] int QueueEvent(uint mediaEventType, [In] ref Guid extendedType, int status, IntPtr value);

        // IMFMediaSource
        [PreserveSig] int GetCharacteristics(out uint characteristics);
        [PreserveSig] int CreatePresentationDescriptor(out IntPtr presentationDescriptor);
        [PreserveSig] int Start(IntPtr presentationDescriptor, IntPtr timeFormat, IntPtr startPosition);
        [PreserveSig] int Stop();
        [PreserveSig] int Pause();
        [PreserveSig] int Shutdown();
    }

    /// <summary>IMFSourceReader: pull-model capture/decode pipeline over a media source.</summary>
    [ComImport]
    [Guid("70AE66F2-C809-4E4F-8915-BDCB406B7993")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSourceReader
    {
        [PreserveSig] int GetStreamSelection(uint streamIndex, [MarshalAs(UnmanagedType.Bool)] out bool selected);
        [PreserveSig] int SetStreamSelection(uint streamIndex, [MarshalAs(UnmanagedType.Bool)] bool selected);
        [PreserveSig] int GetNativeMediaType(uint streamIndex, uint mediaTypeIndex, out IMFMediaType mediaType);
        [PreserveSig] int GetCurrentMediaType(uint streamIndex, out IMFMediaType mediaType);
        [PreserveSig] int SetCurrentMediaType(uint streamIndex, IntPtr reserved, IMFMediaType mediaType);
        [PreserveSig] int SetCurrentPosition([In] ref Guid timeFormat, IntPtr position);
        [PreserveSig] int ReadSample(uint streamIndex, uint controlFlags, out uint actualStreamIndex,
            out uint streamFlags, out long timestamp, out IMFSample sample);
        [PreserveSig] int Flush(uint streamIndex);
        [PreserveSig] int GetServiceForStream(uint streamIndex, [In] ref Guid service, [In] ref Guid iid, out IntPtr instance);
        [PreserveSig] int GetPresentationAttribute(uint streamIndex, [In] ref Guid attribute, IntPtr value);
    }

    /// <summary>IMFSinkWriter: push-model encode/mux pipeline into an output file.</summary>
    [ComImport]
    [Guid("3137F1CD-FE5E-4805-A5D8-FB477448CB3D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSinkWriter
    {
        [PreserveSig] int AddStream(IMFMediaType targetMediaType, out uint streamIndex);
        [PreserveSig] int SetInputMediaType(uint streamIndex, IMFMediaType inputMediaType, IntPtr encodingParameters);
        [PreserveSig] int BeginWriting();
        [PreserveSig] int WriteSample(uint streamIndex, IMFSample sample);
        [PreserveSig] int SendStreamTick(uint streamIndex, long timestamp);
        [PreserveSig] int PlaceMarker(uint streamIndex, IntPtr context);
        [PreserveSig] int NotifyEndOfSegment(uint streamIndex);
        [PreserveSig] int Flush(uint streamIndex);
        [PreserveSig] int Finalize_();
        [PreserveSig] int GetServiceForStream(uint streamIndex, [In] ref Guid service, [In] ref Guid iid, out IntPtr instance);
        [PreserveSig] int GetStatistics(uint streamIndex, IntPtr statistics);
    }

    /// <summary>Throws a <see cref="COMException"/>-style error for a failed Media Foundation call.</summary>
    internal static void ThrowOnFailure(int hresult, string operation)
    {
        if (hresult < 0)
        {
            throw new WebcamException(
                $"Media Foundation call failed: {operation} (HRESULT 0x{hresult:X8}).",
                Marshal.GetExceptionForHR(hresult));
        }
    }

    /// <summary>Reads a CoTaskMem-allocated string attribute, or null when absent.</summary>
    internal static string GetAllocatedString(IMFActivate activate, Guid key)
    {
        if (activate.GetAllocatedString(ref key, out var pointer, out _) != 0 || pointer == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }
}
