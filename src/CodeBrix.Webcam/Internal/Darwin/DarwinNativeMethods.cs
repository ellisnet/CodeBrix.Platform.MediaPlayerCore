using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CodeBrix.Webcam.Internal.Darwin;

/// <summary>
/// The minimal Objective-C runtime and CoreMedia interop surface for talking to
/// AVFoundation directly from managed code — no native shim library. Everything goes
/// through <c>objc_msgSend</c>, declared once per needed native signature (the
/// marshaller then uses the correct ABI on both arm64 and x64). The only native struct
/// return, <see cref="CMVideoDimensions"/>, comes from a plain C CoreMedia function, so
/// none of the fiddly objc_msgSend struct-return variants are required.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class DarwinNativeMethods
{
    private const string LibObjc = "/usr/lib/libobjc.A.dylib";
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";
    private const string CoreMediaFramework = "/System/Library/Frameworks/CoreMedia.framework/CoreMedia";
    private const string AVFoundationFramework = "/System/Library/Frameworks/AVFoundation.framework/AVFoundation";

    private const int RTLD_NOW = 2;

    private static readonly IntPtr AVFoundationHandle = dlopen(AVFoundationFramework, RTLD_NOW);

    // ---- Objective-C runtime ----

    [DllImport(LibObjc)]
    internal static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjc)]
    internal static extern IntPtr sel_registerName(string name);

    [DllImport(LibObjc)]
    internal static extern IntPtr objc_autoreleasePoolPush();

    [DllImport(LibObjc)]
    internal static extern void objc_autoreleasePoolPop(IntPtr pool);

    // objc_msgSend, once per distinct native signature used. NSInteger arguments and
    // returns travel as IntPtr (same size, same registers).

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr SendId(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr SendId(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr SendId(IntPtr receiver, IntPtr selector, UIntPtr arg1);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr SendId(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr SendId(IntPtr receiver, IntPtr selector,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string arg1);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern UIntPtr SendCount(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern int SendInt(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern double SendDouble(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoid(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoid(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoid(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool SendBool(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool SendBool(IntPtr receiver, IntPtr selector, ref IntPtr arg1);

    // ---- CoreMedia (plain C functions) ----

    /// <summary>The pixel dimensions of a CoreMedia video format description.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct CMVideoDimensions
    {
        internal int Width;
        internal int Height;
    }

    [DllImport(CoreMediaFramework)]
    internal static extern CMVideoDimensions CMVideoFormatDescriptionGetDimensions(IntPtr videoDescription);

    [DllImport(CoreMediaFramework)]
    internal static extern uint CMFormatDescriptionGetMediaSubType(IntPtr description);

    // ---- dyld ----

    [DllImport(LibSystem)]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport(LibSystem)]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    // ---- Helpers ----

    /// <summary>Registers (or looks up) an Objective-C selector.</summary>
    internal static IntPtr Sel(string name) => sel_registerName(name);

    /// <summary>
    /// Resolves an exported AVFoundation NSString constant (e.g. AVMediaTypeVideo,
    /// the AVCaptureDeviceType* names) by symbol, or IntPtr.Zero when the running OS
    /// version does not export it.
    /// </summary>
    internal static IntPtr GetAVFoundationConstant(string symbol)
    {
        if (AVFoundationHandle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        var address = dlsym(AVFoundationHandle, symbol);
        return address == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(address);
    }

    /// <summary>
    /// Resolves a globally exported native symbol (RTLD_DEFAULT lookup) — used for the
    /// Objective-C block runtime's _NSConcreteGlobalBlock class object.
    /// </summary>
    internal static IntPtr GetGlobalSymbol(string symbol)
        // RTLD_DEFAULT is (void*)-2 on macOS.
        => dlsym((IntPtr)(-2), symbol);

    /// <summary>Creates an (autoreleased) NSString from a managed string.</summary>
    internal static IntPtr NSStringFromManaged(string value)
        => SendId(objc_getClass("NSString"), Sel("stringWithUTF8String:"), value ?? string.Empty);

    /// <summary>Reads an NSString into a managed string; null for a nil NSString.</summary>
    internal static string NSStringToManaged(IntPtr nsString)
        => nsString == IntPtr.Zero
            ? null
            : Marshal.PtrToStringUTF8(SendId(nsString, Sel("UTF8String")));

    /// <summary>The number of elements in an NSArray (0 for nil).</summary>
    internal static int NSArrayCount(IntPtr nsArray)
        => nsArray == IntPtr.Zero ? 0 : (int)SendCount(nsArray, Sel("count"));

    /// <summary>The element of an NSArray at an index.</summary>
    internal static IntPtr NSArrayAt(IntPtr nsArray, int index)
        => SendId(nsArray, Sel("objectAtIndex:"), (UIntPtr)index);
}
