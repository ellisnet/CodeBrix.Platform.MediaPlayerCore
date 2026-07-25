#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.Platform.MediaPlayerCore; //was previously: LibVLCSharp.Shared;

/// <summary>
/// Configuration helper designed to be used for advanced libvlc configuration
/// <para/> More info at https://wiki.videolan.org/VLC_command-line_help/
/// </summary>
public class MediaConfiguration
{
    readonly Dictionary<string, string> _options = new Dictionary<string, string>
    {
        { nameof(EnableHardwareDecoding), string.Empty },
        { nameof(FileCaching), string.Empty },
        { nameof(NetworkCaching), string.Empty },
    };

    bool _enableHardwareDecoding;
    /// <summary>
    /// Enable/disable hardware decoding (crossplatform).
    /// </summary>
    public bool EnableHardwareDecoding
    {
        get => _enableHardwareDecoding;
        set
        {
            _enableHardwareDecoding = value;
            _options[nameof(EnableHardwareDecoding)] = HardwareDecodingOptionString(_enableHardwareDecoding);
        }
    }

    uint _fileCaching;
    /// <summary>
    /// Caching value for local files, in milliseconds [0 .. 60000ms]
    /// </summary>
    public uint FileCaching
    {
        get => _fileCaching;
        set
        {
            _fileCaching = value;
            _options[nameof(FileCaching)] = FileCachingOptionString(_fileCaching);
        }
    }

    uint _networkCaching;
    /// <summary>
    /// Caching value for network resources, in milliseconds [0 .. 60000ms]
    /// </summary>
    public uint NetworkCaching
    {
        get => _networkCaching;
        set
        {
            _networkCaching = value;
            _options[nameof(NetworkCaching)] = NetworkCachingOptionString(_networkCaching);
        }
    }

    const string ENABLE_HW_APPLE = ":videotoolbox";
    const string ENABLE_HW_WINDOWS = ":avcodec-hw=d3d11va";
    // Linux uses FFmpeg's avcodec hardware-acceleration selector: "any" lets libvlc auto-pick an available
    // backend (VA-API / VDPAU); "none" forces pure software decoding. Previously the Linux branches returned
    // string.Empty, so EnableHardwareDecoding = false was a no-op on Linux (hardware decode could not be
    // turned off) - which breaks memory-output (vmem) rendering, whose GPU-surface frames cannot be adapted
    // to the system-memory format the sink requires.
    const string ENABLE_HW_LINUX = ":avcodec-hw=any";

    const string DISABLE_HW_APPLE = ":no-videotoolbox";
    const string DISABLE_HW_WINDOWS = ":avcodec-hw=none";
    const string DISABLE_HW_LINUX = ":avcodec-hw=none";

    private string HardwareDecodingOptionString(bool enable)
    {
        if(enable)
        {
            if (PlatformHelper.IsWindows)
                return ENABLE_HW_WINDOWS;
            if (PlatformHelper.IsMac)
                return ENABLE_HW_APPLE;
            return ENABLE_HW_LINUX;
        }
        else
        {
            if (PlatformHelper.IsWindows)
                return DISABLE_HW_WINDOWS;
            if (PlatformHelper.IsMac)
                return DISABLE_HW_APPLE;
            return DISABLE_HW_LINUX;
        }

    }

    private string FileCachingOptionString(uint fileCaching)
    {
        return ":file-caching=" + fileCaching;
    }

    private string NetworkCachingOptionString(uint networkCaching)
    {
        return ":network-caching=" + networkCaching;
    }

    /// <summary>
    /// Builds the current MediaConfiguration for consumption by libvlc (or storage)
    /// </summary>
    /// <returns>Configured libvlc options as strings</returns>
    public string[] Build() => _options.Values.Where(option => !string.IsNullOrEmpty(option)).ToArray();
}
