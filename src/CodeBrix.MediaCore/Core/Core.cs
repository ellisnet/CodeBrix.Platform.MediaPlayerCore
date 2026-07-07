#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using CodeBrix.Platform.MediaPlayerCore.Helpers;

namespace CodeBrix.Platform.MediaPlayerCore; //was previously: LibVLCSharp.Shared;

/// <summary>
/// The Core class handles libvlc loading intricacies on various platforms as well as
/// the libvlc/libvlcsharp version match check.
/// </summary>
public static partial class Core
{
    partial struct Native
    {
        [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "libvlc_get_version")]
        internal static extern IntPtr LibVLCVersion();
        [DllImport(Constants.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibraryW(string dllToLoad);

        [DllImport(Constants.LibSystem, EntryPoint = "dlopen")]
        internal static extern IntPtr Dlopen(string libraryPath, int mode = 1);
    }

    /// <summary>
    /// Checks whether the major version of LibVLC and LibVLCSharp match <para/>
    /// Throws a VLCException if the major versions mismatch
    /// </summary>
    static void EnsureVersionsMatch()
    {
        var libvlcMajorVersion = int.Parse(Native.LibVLCVersion().FromUtf8()?.Split('.').FirstOrDefault() ?? "0");
        var libvlcsharpMajorVersion = Assembly.GetExecutingAssembly().GetName().Version?.Major;
        if (libvlcMajorVersion != libvlcsharpMajorVersion)
            throw new VLCException($"Version mismatch between LibVLC {libvlcMajorVersion} and LibVLCSharp {libvlcsharpMajorVersion}. " +
                $"They must share the same major version number");
    }

    static string LibVLCPath(string dir) => Path.Combine(dir, $"{Constants.LibraryName}{LibraryExtension}");
    static string LibVLCCorePath(string dir) => Path.Combine(dir, $"{Constants.CoreLibraryName}{LibraryExtension}");
    static string LibraryExtension => PlatformHelper.IsWindows ? Constants.WindowsLibraryExtension : Constants.MacLibraryExtension;
    static void Log(string message)
    {
        Trace.WriteLine(message);
    }

    static bool _libvlcLoaded;
    static internal bool LibVLCLoaded
    {
        get => _libvlcLoaded || LibvlcHandle != IntPtr.Zero;
        set => _libvlcLoaded = value;
    }

    static List<(string libvlccore, string libvlc)> ComputeLibVLCSearchPaths()
    {
        var paths = new List<(string, string)>();
        string arch;

        if (PlatformHelper.IsMac)
        {
            arch = Path.Combine(ArchitectureNames.MacOS64, Constants.Lib);
        }

        else if (PlatformHelper.IsWindows)
        {
            arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => ArchitectureNames.Win64,
                Architecture.X86 => ArchitectureNames.Win86,
                Architecture.Arm64 => ArchitectureNames.WinArm64,
                _ => PlatformHelper.IsX64BitProcess ? ArchitectureNames.Win64 : ArchitectureNames.Win86
            };
        }

        else
        {
            arch = PlatformHelper.IsX64BitProcess ? ArchitectureNames.Win64 : ArchitectureNames.Win86;
        }

        var libvlcAssemblyLocation = AppContext.BaseDirectory;
        var libvlcDirPath1 = Path.Combine(Path.GetDirectoryName(libvlcAssemblyLocation)!,
            Constants.LibrariesRepositoryFolderName, arch);

        var libvlccorePath1 = LibVLCCorePath(libvlcDirPath1);

        var libvlcPath1 = LibVLCPath(libvlcDirPath1);
        paths.Add((libvlccorePath1, libvlcPath1));

        var assemblyLocation = AppContext.BaseDirectory;
        if(!string.IsNullOrEmpty(assemblyLocation))
        { 
            var libvlcDirPath2 = Path.Combine(Path.GetDirectoryName(assemblyLocation)!,
                Constants.LibrariesRepositoryFolderName, arch);

            var libvlccorePath2 = string.Empty;
            if (PlatformHelper.IsWindows)
            {
                libvlccorePath2 = LibVLCCorePath(libvlcDirPath2);
            }

            var libvlcPath2 = LibVLCPath(libvlcDirPath2);
            paths.Add((libvlccorePath2, libvlcPath2));
        }
        var libvlcPath3 = LibVLCPath(Path.GetDirectoryName(libvlcAssemblyLocation)!);

        paths.Add((string.Empty, libvlcPath3));

        // Add Win64 folders as fallback for WinArm64 to keep compatibility
        if (arch == ArchitectureNames.WinArm64)
        {
            var fallbackLibvlcDirPath1 = Path.Combine(Path.GetDirectoryName(libvlcAssemblyLocation)!,
                Constants.LibrariesRepositoryFolderName, ArchitectureNames.Win64);

            var fallbackLibvlccorePath1 = LibVLCCorePath(fallbackLibvlcDirPath1);
            var fallbackLibvlcPath1 = LibVLCPath(fallbackLibvlcDirPath1);
            paths.Add((fallbackLibvlccorePath1, fallbackLibvlcPath1));
        
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var fallbackLibvlcDirPath2 = Path.Combine(Path.GetDirectoryName(assemblyLocation)!,
                    Constants.LibrariesRepositoryFolderName, ArchitectureNames.Win64);

                var fallbackLibvlccorePath2 = LibVLCCorePath(fallbackLibvlcDirPath2);
                var fallbackLibvlcPath2 = LibVLCPath(fallbackLibvlcDirPath2);
                paths.Add((fallbackLibvlccorePath2, fallbackLibvlcPath2));
            }
        }

        if (PlatformHelper.IsMac)
        {
            var libvlcPath4 = Path.Combine(Path.Combine(Path.GetDirectoryName(libvlcAssemblyLocation)!,
                Constants.Lib), $"{Constants.LibVLC}{LibraryExtension}");
            var libvlccorePath4 = LibVLCCorePath(Path.Combine(Path.GetDirectoryName(libvlcAssemblyLocation)!, Constants.Lib));
            paths.Add((libvlccorePath4, libvlcPath4));
        }

        return paths;
    }

    static void LoadLibVLC(string? libvlcDirectoryPath = null)
    {
        // full path to directory location of libvlc and libvlccore has been provided
        if (!string.IsNullOrEmpty(libvlcDirectoryPath))
        {
            bool loadResult;
            var libvlccorePath = LibVLCCorePath(libvlcDirectoryPath!);
            loadResult = LoadNativeLibrary(libvlccorePath, out LibvlccoreHandle);
            if (!loadResult)
            {
                Log($"Failed to load required native libraries at {libvlccorePath}");
                return;
            }

            var libvlcPath = LibVLCPath(libvlcDirectoryPath!);
            loadResult = LoadNativeLibrary(libvlcPath, out LibvlcHandle);
            if (!loadResult)
                Log($"Failed to load required native libraries at {libvlcPath}");
            return;
        }

        var paths = ComputeLibVLCSearchPaths();

        foreach (var (libvlccore, libvlc) in paths)
        {
            LoadNativeLibrary(libvlccore, out LibvlccoreHandle);
            var loadResult = LoadNativeLibrary(libvlc, out LibvlcHandle);
            if (loadResult)
                break;
        }

        if (!LibVLCLoaded)
        {
            throw new VLCException("Failed to load required native libraries. " +
                $"{Environment.NewLine}Have you installed the latest LibVLC package from nuget for your target platform?" +
                $"{Environment.NewLine}Search paths include {string.Join("; ", paths.Select(p => $"{p.libvlc},{p.libvlccore}"))}");
        }
    }
    internal static void EnsureLoaded()
    {
        if (LibVLCLoaded)
        {
            return;
        }

        Initialize();
    }

    static bool LoadNativeLibrary(string nativeLibraryPath, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        Log($"Loading {nativeLibraryPath}");

        if (!File.Exists(nativeLibraryPath))
        {
            Log($"Cannot find {nativeLibraryPath}");
            return false;
        }
        if (PlatformHelper.IsMac)
        {
            handle = Native.Dlopen(nativeLibraryPath);
        }
        else
        {
            handle = Native.LoadLibraryW(nativeLibraryPath);
        }

        return handle != IntPtr.Zero;
    }
}
