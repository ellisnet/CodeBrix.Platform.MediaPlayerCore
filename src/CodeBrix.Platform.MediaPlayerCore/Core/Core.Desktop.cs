#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace CodeBrix.Platform.MediaPlayerCore; //was previously: LibVLCSharp.Shared;

/// <summary>
/// The Core class handles libvlc loading intricacies on various platforms as well as
/// the libvlc/libvlcsharp version match check.
/// </summary>
public static partial class Core
{
    partial struct Native
    {
        [DllImport(Constants.Kernel32, SetLastError = true)]
        internal static extern ErrorModes SetErrorMode(ErrorModes uMode);
    }
    static IntPtr LibvlcHandle;
    static IntPtr LibvlccoreHandle;
    /// <summary>
    /// Load the native libvlc library (if necessary, depending on platform)
    /// <para/> Ensure that you installed the VideoLAN.LibVLC.[YourPlatform] package in your target project
    /// <para/> This will throw a <see cref="VLCException"/> if the native libvlc libraries cannot be found or loaded.
    /// <para/> It may also throw a <see cref="VLCException"/> if the LibVLC and LibVLCSharp major versions do not match.
    /// See https://code.videolan.org/videolan/LibVLCSharp/-/blob/master/docs/versioning.md for more info about the versioning strategy.
    /// </summary>
    /// <param name="libvlcDirectoryPath">The path to the directory that contains libvlc and libvlccore
    /// No need to specify unless running netstandard 1.1, or using custom location for libvlc
    /// <para/> This parameter is NOT supported on Linux, use LD_LIBRARY_PATH instead.
    /// </param>
    public static void Initialize(string? libvlcDirectoryPath = null)
    {
        DisableMessageErrorBox();
        InitializeDesktop(libvlcDirectoryPath);
        EnsureVersionsMatch();
        LibVLCLoaded = true;
    }

    /// <summary>
    /// Disable error dialogs in case of dll loading failures on older Windows versions.
    /// <para/>
    /// This is mostly to fix Windows XP support (https://code.videolan.org/videolan/LibVLCSharp/issues/173),
    /// though it may happen under other conditions (broken plugins/wrong ABI).
    /// <para/>
    /// As libvlc may load additional plugins later in the lifecycle of the application,
    /// we should not unset this on exiting <see cref="Initialize(string)"/>
    /// </summary>
    static void DisableMessageErrorBox()
    {
        if (!PlatformHelper.IsWindows)
            return;

        var oldMode = Native.SetErrorMode(ErrorModes.SYSTEM_DEFAULT);
        Native.SetErrorMode(oldMode | ErrorModes.SEM_FAILCRITICALERRORS | ErrorModes.SEM_NOOPENFILEERRORBOX);
    }

    static void InitializeDesktop(string? libvlcDirectoryPath = null)
    {
        if(PlatformHelper.IsLinux)
        {
            if (!string.IsNullOrEmpty(libvlcDirectoryPath))
            {
                throw new InvalidOperationException($"Using {nameof(libvlcDirectoryPath)} is not supported on the Linux platform. " +
                    $"The recommended way is to have the libvlc librairies in /usr/lib. Use LD_LIBRARY_PATH if you need more customization");
            }

            // CodeBrix addition: on Linux we don't pre-load libvlc by path (the OS dynamic linker
            // resolves it lazily on the first P/Invoke). Register a resolver so those P/Invokes,
            // which bind the unversioned soname "libvlc", find the versioned runtime library.
            RegisterLinuxLibVLCResolver();
            return;
        }

        LoadLibVLC(libvlcDirectoryPath);
    }

    static int _linuxResolverRegistered;

    /// <summary>
    /// Registers a native-library resolver so the managed P/Invokes — which bind the unversioned
    /// soname <c>libvlc</c> — load the distro's runtime libvlc on Linux without requiring the
    /// build-time <c>libvlc-dev</c> package.
    /// <para/>
    /// .NET resolves the bare name <c>libvlc</c> only against a <c>libvlc.so</c> symlink, which on
    /// Debian/Ubuntu ships in <c>libvlc-dev</c>. The runtime package end users install
    /// (<c>libvlc5</c>) provides only the versioned <c>libvlc.so.5</c>. This resolver maps
    /// <c>libvlc</c>/<c>libvlccore</c> onto the versioned runtime sonames, so
    /// <c>sudo apt install libvlc5 vlc-plugin-base</c> alone is sufficient. Runs once, before the
    /// first native call (invoked from <see cref="EnsureLoaded"/> via
    /// <see cref="InitializeDesktop"/>). Not registered on Windows/macOS, which load libvlc by
    /// full path instead.
    /// </summary>
    static void RegisterLinuxLibVLCResolver()
    {
        if (Interlocked.CompareExchange(ref _linuxResolverRegistered, 1, 0) != 0)
        {
            return;
        }

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(Core).Assembly, (libraryName, _, _) =>
            {
                var candidates = libraryName switch
                {
                    Constants.LibraryName => new[] { "libvlc.so.5", "libvlc.so" },
                    Constants.CoreLibraryName => new[] { "libvlccore.so.9", "libvlccore.so" },
                    _ => null
                };

                if (candidates != null)
                {
                    foreach (var candidate in candidates)
                    {
                        if (NativeLibrary.TryLoad(candidate, out var handle))
                        {
                            return handle;
                        }
                    }
                }

                return IntPtr.Zero; // defer to the default resolver for unmapped names / if none load
            });
        }
        catch (InvalidOperationException)
        {
            // A resolver is already set for this assembly (e.g. a second Core.Initialize() call).
            // The existing registration is this same mapping, so there is nothing more to do.
        }
    }
}
