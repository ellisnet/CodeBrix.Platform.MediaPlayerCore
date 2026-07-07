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
    internal static LibVLC Shared
    {
        get
        {
            lock (Sync)
            {
                if (_shared == null)
                {
                    Core.Initialize();
                    _shared = new LibVLC();
                }
                return _shared;
            }
        }
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
