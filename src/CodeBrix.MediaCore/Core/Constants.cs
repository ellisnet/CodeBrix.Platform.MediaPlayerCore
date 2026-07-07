#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;

namespace CodeBrix.Platform.MediaPlayerCore; //was previously: LibVLCSharp.Shared;

internal static class Constants
{
    internal const string LibraryName = "libvlc";
    internal const string CoreLibraryName = "libvlccore";

    /// <summary>
    /// The name of the folder that contains the per-architecture folders
    /// </summary>
    internal const string LibrariesRepositoryFolderName = "libvlc";

    internal const string Msvcrt = "msvcrt";
    internal const string Libc = "libc";
    internal const string LibSystem = "libSystem";
    internal const string Kernel32 = "kernel32";
    internal const string WindowsLibraryExtension = ".dll";
    internal const string MacLibraryExtension = ".dylib";
    internal const string Lib = "lib";
    internal const string LibVLC = "libvlc";

    /// <summary>
    /// CodeBrix addition: the library directory inside a standard VLC.app install —
    /// the documented no-bundling way to provide libvlc on macOS.
    /// </summary>
    internal const string MacVlcAppLibraryDirectory = "/Applications/VLC.app/Contents/MacOS/lib";

    /// <summary>
    /// CodeBrix addition: the plugin directory inside a standard VLC.app install; libvlc
    /// needs VLC_PLUGIN_PATH pointed here when loaded out of the app bundle.
    /// </summary>
    internal const string MacVlcAppPluginsDirectory = "/Applications/VLC.app/Contents/MacOS/plugins";
}

internal static class ArchitectureNames
{
    internal const string WinArm64 = "win-arm64";
    internal const string Win64 = "win-x64";
    internal const string Win86 = "win-x86";
    internal const string MacOS64 = "osx-x64";
    internal const string MacOSArm64 = "osx-arm64";
}

[Flags]
internal enum ErrorModes : uint
{
    SYSTEM_DEFAULT = 0x0,
    SEM_FAILCRITICALERRORS = 0x0001,
    SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
    SEM_NOGPFAULTERRORBOX = 0x0002,
    SEM_NOOPENFILEERRORBOX = 0x8000
}
