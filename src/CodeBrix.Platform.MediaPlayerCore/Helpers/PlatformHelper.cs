#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.Runtime.InteropServices;

namespace CodeBrix.Platform.MediaPlayerCore; //was previously: LibVLCSharp.Shared;

/// <summary>
/// Small helper for determining the current platform
/// </summary>
public class PlatformHelper
{
    /// <summary>
    /// Returns true if running on Windows, false otherwise
    /// </summary>
    public static bool IsWindows
    {
        get => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    /// <summary>
    /// Returns true if running on Linux, false otherwise
    /// </summary>
    public static bool IsLinux
    {
        get => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }

    /// <summary>
    /// Returns true if running on Linux desktop, false otherwise
    /// </summary>
    public static bool IsLinuxDesktop
    {
        get => IsLinux;
    }

    /// <summary>
    /// Returns true if running on macOS, false otherwise
    /// </summary>
    public static bool IsMac
    {
        get => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    /// <summary>
    /// Returns true if running in 64bit process, false otherwise
    /// </summary>
    public static bool IsX64BitProcess => IntPtr.Size == 8;
}
