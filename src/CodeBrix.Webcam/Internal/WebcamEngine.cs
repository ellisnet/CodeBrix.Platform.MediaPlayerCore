using System;
using CodeBrix.Platform.MediaPlayerCore;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// Owns the process-wide libvlc engine instance behind all webcam sessions. Everything
/// engine-flavored stays internal — no CodeBrix.MediaCore type ever appears in the
/// public CodeBrix.Webcam surface (enforced by a reflection test in the test suite).
/// </summary>
internal static class WebcamEngine
{
    private static readonly object Sync = new object();
    private static LibVLC _shared;

    /// <summary>The shared libvlc instance, created (and the native library loaded) on first use.</summary>
    /// <exception cref="WebcamException">The native libvlc runtime is missing or failed
    /// to load — the message carries per-platform installation guidance. Engine
    /// exception types never escape to CodeBrix.Webcam consumers.</exception>
    internal static LibVLC Shared
    {
        get
        {
            lock (Sync)
            {
                if (_shared == null)
                {
                    try
                    {
                        Core.Initialize();
                        _shared = new LibVLC();
                    }
                    catch (VLCException e)
                    {
                        throw new WebcamException(LibVlcUnavailableMessage(), e);
                    }
                }
                return _shared;
            }
        }
    }

    private static string LibVlcUnavailableMessage()
    {
        string action;
        if (OperatingSystem.IsWindows())
        {
            action = "add the VideoLAN.LibVLC.Windows NuGet package to the application project "
                + "(an installed VLC desktop application is not used on Windows)";
        }
        else if (OperatingSystem.IsLinux())
        {
            action = "install the libvlc runtime via the system package manager, e.g. "
                + "'sudo apt install libvlc5 vlc-plugin-base' on Debian/Ubuntu";
        }
        else
        {
            action = "install VLC.app into /Applications (from videolan.org), or bundle the "
                + "libvlc libraries with the application";
        }
        return "CodeBrix.Webcam could not load the native libvlc media engine that powers "
            + $"capture sessions. To fix: {action}.";
    }

    /// <summary>True when the engine can accept in-memory frames (imem) — the
    /// prerequisite for overlay burn-in on recordings. Cached for the process lifetime.</summary>
    internal static bool SupportsFrameInput()
    {
        try
        {
            return VideoFrameSource.IsSupported(Shared);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
