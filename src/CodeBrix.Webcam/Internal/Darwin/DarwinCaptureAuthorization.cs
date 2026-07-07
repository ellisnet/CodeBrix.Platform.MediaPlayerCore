using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using static CodeBrix.Webcam.Internal.Darwin.DarwinNativeMethods;

namespace CodeBrix.Webcam.Internal.Darwin;

/// <summary>
/// Obtains macOS camera / microphone capture consent (TCC) before a session opens.
/// libvlc's avcapture and qtsound modules only CHECK the authorization status — they
/// never trigger the consent prompt themselves — so a process that has not asked would
/// always fail with "access has not been granted by the user". This asks: it calls
/// AVCaptureDevice.requestAccessForMediaType:completionHandler: (which shows the system
/// prompt on first use) and waits for the answer. The completion handler is a
/// hand-assembled capture-free global Objective-C block — the stable, documented block
/// ABI — whose invoke function signals a static semaphore.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class DarwinCaptureAuthorization
{
    private const long StatusNotDetermined = 0;
    private const long StatusAuthorized = 3;

    private const int BlockIsGlobalFlag = 1 << 28;

    // How long the user gets to answer the system consent prompt before we give up
    // and report the (still undetermined) status.
    private static readonly TimeSpan PromptTimeout = TimeSpan.FromSeconds(90);

    private static readonly object RequestLock = new object();
    private static readonly SemaphoreSlim CompletionSignal = new SemaphoreSlim(0);

    private delegate void AccessCompletionHandler(IntPtr block, [MarshalAs(UnmanagedType.I1)] bool granted);

    // The delegate (and the unmanaged block built around it) must stay rooted for the
    // process lifetime — the OS may invoke it long after the request call returned.
    private static readonly AccessCompletionHandler CompletionDelegate = OnAccessCompletion;
    private static IntPtr _completionBlock;

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockDescriptor
    {
        internal ulong Reserved;
        internal ulong Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        internal IntPtr Isa;
        internal int Flags;
        internal int Reserved;
        internal IntPtr Invoke;
        internal IntPtr Descriptor;
    }

    /// <summary>
    /// Ensures the process is authorized to capture video (and audio when the session
    /// records a microphone), prompting the user when consent has not been decided yet.
    /// </summary>
    /// <param name="includeAudio">True to also ensure microphone consent.</param>
    /// <exception cref="WebcamException">Consent is denied, restricted, or the user did
    /// not answer the prompt in time.</exception>
    internal static void EnsureAccess(bool includeAudio)
    {
        EnsureAccess("AVMediaTypeVideo", "camera", "Camera");
        if (includeAudio)
        {
            EnsureAccess("AVMediaTypeAudio", "microphone", "Microphone");
        }
    }

    private static void EnsureAccess(string mediaTypeSymbol, string deviceKind, string settingsPane)
    {
        lock (RequestLock)
        {
            var mediaType = GetAVFoundationConstant(mediaTypeSymbol);
            if (mediaType == IntPtr.Zero)
            {
                return; // cannot check here; libvlc will report the failure
            }
            var deviceClass = objc_getClass("AVCaptureDevice");
            var statusSelector = Sel("authorizationStatusForMediaType:");
            var status = (long)SendId(deviceClass, statusSelector, mediaType);
            if (status == StatusNotDetermined)
            {
                // Drain a stale signal from an earlier timed-out prompt, then ask.
                while (CompletionSignal.Wait(0))
                {
                }
                SendVoid(deviceClass, Sel("requestAccessForMediaType:completionHandler:"),
                    mediaType, GetCompletionBlock());
                CompletionSignal.Wait(PromptTimeout);
                status = (long)SendId(deviceClass, statusSelector, mediaType);
            }
            if (status != StatusAuthorized)
            {
                throw new WebcamException(
                    $"macOS {deviceKind} access is not granted to this application. Allow it under " +
                    $"System Settings > Privacy & Security > {settingsPane} (an app bundle also needs " +
                    $"NS{settingsPane}UsageDescription in its Info.plist), then try again.");
            }
        }
    }

    private static void OnAccessCompletion(IntPtr block, bool granted)
    {
        CompletionSignal.Release();
    }

    private static IntPtr GetCompletionBlock()
    {
        if (_completionBlock != IntPtr.Zero)
        {
            return _completionBlock;
        }
        var descriptor = Marshal.AllocHGlobal(Marshal.SizeOf<BlockDescriptor>());
        Marshal.StructureToPtr(new BlockDescriptor
        {
            Reserved = 0,
            Size = (ulong)Marshal.SizeOf<BlockLiteral>(),
        }, descriptor, false);

        var literal = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
        Marshal.StructureToPtr(new BlockLiteral
        {
            Isa = GetGlobalSymbol("_NSConcreteGlobalBlock"),
            Flags = BlockIsGlobalFlag,
            Reserved = 0,
            Invoke = Marshal.GetFunctionPointerForDelegate(CompletionDelegate),
            Descriptor = descriptor,
        }, literal, false);

        _completionBlock = literal;
        return literal;
    }
}
