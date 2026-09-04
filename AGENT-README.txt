================================================================================
AGENT-README: CodeBrix.MediaCore
A Guide for AI Coding Agents — CONSUMING the CodeBrix.MediaCore.LgplLicenseForever NuGet package
================================================================================


OVERVIEW
========
CodeBrix.MediaCore is a fully managed, cross-platform audio / video media
ENGINE for .NET 10 or later: the P/Invoke binding onto VideoLAN's native
libvlc library. It plays every file format, codec and streaming protocol
libvlc supports, decodes with hardware acceleration where available,
renders video into CPU memory (windowing-system-agnostic), captures decoded
audio through callbacks, discovers local media sources and network
renderers (Chromecast / UPnP), applies an audio equalizer, and routes
libvlc's user dialogs and log output to managed code.

Provenance: the package is a port of LibVLCSharp (the official .NET
wrapper for libvlc), restricted to the cross-platform managed core and
narrowed to .NET 10. Upstream namespaces (LibVLCSharp.Shared.*) were
renamed to CodeBrix.Platform.MediaPlayerCore.*; do NOT write upstream
namespaces or reference the upstream NuGet package. Two types are CodeBrix
additions not found upstream: VideoFrameSink (video frames OUT of libvlc
into memory) and VideoFrameSource (frames INTO libvlc for encoding).

Ported source files keep `#nullable enable annotations`, so signatures
below show `?` exactly where the source does; it marks members that may be
null. Consumers compiling with nullable reference types off simply ignore
the `?`.

CRITICAL NAMESPACE RULE — DO NOT "FIX":
The assembly and package are named CodeBrix.MediaCore, but every type in
them lives in the CodeBrix.Platform.MediaPlayerCore namespace (and two
sub-namespaces). The package-name/namespace mismatch is deliberate and
permanent: it lets existing consumers (notably the CodeBrix.Platform
MediaPlayer add-in) compile unchanged. Never write
`using CodeBrix.MediaCore;` — that namespace does not exist.


INSTALLATION
============
    dotnet add package CodeBrix.MediaCore.LgplLicenseForever

PackageId:    CodeBrix.MediaCore.LgplLicenseForever   (the suffix is part
              of the id; "CodeBrix.MediaCore" alone does not exist on
              nuget.org)
Assembly:     CodeBrix.MediaCore.dll
Target:       .NET 10 or later
Dependencies: none (no managed NuGet dependencies)
License:      LGPL-2.1-or-later. Consume via <PackageReference>; never
              merge the DLL into your own assembly (ILMerge/ILRepack), which
              forfeits LGPL relinkability. The LICENSE and
              THIRD-PARTY-NOTICES.txt files ship inside the package.

NATIVE LIBVLC REQUIREMENT — READ THIS. The package is a pure managed
assembly and does NOT bundle the native libvlc engine. It hard-requires
libvlc at runtime on every platform, and the managed package's MAJOR
version must equal the native libvlc major version (the loader checks this
and throws VLCException on a mismatch). What "libvlc present" means
differs per platform:

  Windows:  the CONSUMING APPLICATION must reference the official
            VideoLAN.LibVLC.Windows NuGet package (it drops libvlc.dll,
            libvlccore.dll and the plugins folder next to the exe). An
            installed VLC desktop application is NOT used and NOT
            searched on Windows — the NuGet reference is the only
            supported mechanism.
  Linux:    install the runtime libraries through the system package
            manager: `sudo apt install libvlc5 vlc-plugin-base` on
            Debian-based distributions. Neither the `vlc` desktop
            application nor the build-time `libvlc-dev` package is
            required — the loader maps the `libvlc` P/Invoke name onto
            the versioned runtime soname `libvlc.so.5`. No VideoLAN NuGet
            runtime package exists for Linux.
  macOS:    the VLC media player application must be installed. That is
            the ordinary desktop app whose icon is named "VLC" in the
            Applications folder; its on-disk bundle is /Applications/VLC.app.
            Get it from https://www.videolan.org/vlc/ ("Download VLC";
            pick the build matching the Mac — Apple Silicon, Intel or
            universal), open the .dmg and drag VLC into /Applications.
            The loader finds /Applications/VLC.app automatically and
            points libvlc at the bundle's plugin directory via
            VLC_PLUGIN_PATH — no configuration needed. (An application
            may instead ship the libvlc dylibs in its own output —
            app-bundled libraries win over VLC.app — but the
            VideoLAN.LibVLC.Mac NuGet package is abandoned with x64-only
            binaries, so on Apple Silicon installing VLC is effectively
            THE route, and it is the one this package verifies.)

So: "the VLC application must be installed" is TRUE on macOS, wrong on
Windows (NuGet reference instead) and wrong on Linux (runtime libraries
instead).

When libvlc is missing or cannot be loaded, `Core.Initialize()` (or the
first `new LibVLC()`) throws CodeBrix.Platform.MediaPlayerCore.VLCException
listing the search paths it tried. Call `Core.Initialize()` once, before
constructing any LibVLC instance.


KEY NAMESPACES / USINGS
=======================
    using CodeBrix.Platform.MediaPlayerCore;             // almost everything
    using CodeBrix.Platform.MediaPlayerCore.Structures;  // five DTO structs

Those two are the only usings you ever need. (A third namespace,
CodeBrix.Platform.MediaPlayerCore.Helpers, exists in the assembly but holds
nothing public — MarshalExtensions and MarshalUtils are internal — so never
import it.) Everything else — Core, LibVLC, Media, MediaPlayer, MediaList,
discoverers, Equalizer, Dialog, VideoFrameSink, VideoFrameSource,
PlatformHelper, every event-args class, every enum, and most DTO structs —
is in the ROOT namespace. The `.Structures` namespace holds only
AudioOutputDescription, AudioOutputDevice, ChapterDescription,
ModuleDescription and TrackDescription. (There is no `.Core` and no
`.Events` namespace; those are source-folder names only.)


OTHER PACKAGES IN THIS REPOSITORY
=================================
Two sibling packages are built from the same repository and always publish
at the same version as this one. Each has its own AGENT-README:

  CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever   (LGPL-2.1-or-later)
      The IVideoView seam and MediaPosition value type that a XAML-style
      MediaPlayerElement builds on; depends on this engine package.
      See src/CodeBrix.Platform.MediaPlayerCore/AGENT-README.txt

  CodeBrix.Webcam.LgplLicenseForever                     (LGPL-2.1-or-later)
      Webcam capture: device enumeration with the full capability matrix,
      live BGRA frames, photos, MP4/H.264 recording, overlay burn-in.
      Depends on this engine package but never exposes an engine type.
      See src/CodeBrix.Webcam/AGENT-README.txt


CORE API REFERENCE
==================
All signatures below are transcribed from the shipped source.

Core (static)
-------------
    public static void Initialize(string? libvlcDirectoryPath = null)
        Loads libvlc/libvlccore. Throws VLCException if they cannot be
        found or loaded, or if the native major version differs from the
        package's major version. `libvlcDirectoryPath` overrides the
        search location on Windows/macOS; it is NOT supported on Linux
        (use LD_LIBRARY_PATH there).

LibVLC : Internal (IDisposable)
-------------------------------
The root handle; create one per application (creating many is expensive).

    public LibVLC(params string[] options)
    public LibVLC(bool enableDebugLogs, params string[] options)
    public LibVLC(bool enableDebugLogs, bool useDefaultLibVLCOptions,
                  params string[] options)
        `options` are libvlc command-line switches such as "--no-audio",
        "--no-video", "--verbose=2". They are unsupported by VideoLAN
        beyond debugging; prefer per-Media options (":option").

    Information
    public string Version                     // libvlc version string
    public string Changeset
    public string LibVLCCompiler
    public long Clock                         // libvlc's clock, microseconds
    public string? LastLibVLCError
    public void ClearLibVLCError()

    Configuration
    public bool AddInterface(string? name)
    public void SetExitHandler(ExitCallback cb)      // delegate void ExitCallback()
    public void SetUserAgent(string name, string http)
    public void SetAppId(string? id, string? version, string? icon)

    Logging (see THE LOG SURFACE below)
    public event EventHandler<LogEventArgs> Log
    public void SetLogFile(string filename)
    public bool CloseLogFile()

    Module / output enumeration
    public ModuleDescription[] AudioFilters
    public ModuleDescription[] VideoFilters
    public AudioOutputDescription[] AudioOutputs
    public AudioOutputDevice[] AudioOutputDevices(string audioOutputName)
    public MediaDiscovererDescription[] MediaDiscoverers(
        MediaDiscovererCategory discovererCategory)
    public RendererDescription[] RendererList

    Dialogs (see DIALOGS below)
    public void SetDialogHandlers(DisplayError error, DisplayLogin login,
        DisplayQuestion question, DisplayProgress displayProgress,
        UpdateProgress updateProgress)
    public void UnsetDialogHandlers()
    public bool DialogHandlersSet

Media : Internal (IDisposable)
------------------------------
One playable resource (file, URL, stream, node) plus its metadata/tracks.

    public Media(LibVLC libVLC, string mrl, FromType type = FromType.FromPath,
                 params string[] options)
    public Media(LibVLC libVLC, Uri uri, params string[] options)
    public Media(LibVLC libVLC, int fd, params string[] options)      // open file descriptor
    public Media(LibVLC libVLC, MediaInput input, params string[] options)
        // e.g. new StreamMediaInput(stream); NOT disposed by Media
    public Media(MediaList mediaList)
        `options` are per-media libvlc options in the form ":your-option"
        (e.g. ":input-repeat=65535", ":sout=#...", ":network-caching=1000").

    public string Mrl
    public void AddOption(string option)
    public void AddOption(MediaConfiguration mediaConfiguration)
    public void AddOptionFlag(string option, uint flags)
    public Media Duplicate()

    Metadata
    public string? Meta(MetadataType metadataType)
    public void SetMeta(MetadataType metadataType, string metaValue)
    public bool SaveMeta()

    State / statistics
    public VLCState State
    public MediaStats Statistics
    public long Duration                     // milliseconds; -1 until parsed/played
    public MediaType Type

    Parsing (async; needed before Tracks / Duration / SubItems are populated)
    public async Task<MediaParsedStatus> Parse(
        MediaParseOptions options = MediaParseOptions.ParseLocal,
        int timeout = -1, CancellationToken cancellationToken = default)
    public bool IsParsed
    public MediaParsedStatus ParsedStatus
    public void ParseStop()

    Tracks and sub-items
    public MediaTrack[] Tracks
    public MediaList SubItems                // playlists/directories expand here
    public string CodecDescription(TrackType type, uint codec)

    Slaves (external subtitle / audio files)
    public bool AddSlave(MediaSlaveType type, uint priority, string uri)
    public bool AddSlave(MediaSlaveType type, uint priority, Uri uri)
    public void ClearSlaves()
    public MediaSlave[] Slaves

    Events (raised on libvlc threads)
    public event EventHandler<MediaMetaChangedEventArgs>     MetaChanged
        // args: MetadataType MetadataType
    public event EventHandler<MediaParsedChangedEventArgs>   ParsedChanged
        // args: MediaParsedStatus ParsedStatus
    public event EventHandler<MediaSubItemAddedEventArgs>    SubItemAdded
        // args: Media SubItem
    public event EventHandler<MediaDurationChangedEventArgs> DurationChanged
        // args: long Duration
    public event EventHandler<MediaFreedEventArgs>           MediaFreed
        // args: Media Media
    public event EventHandler<MediaStateChangedEventArgs>    StateChanged
        // args: VLCState State
    public event EventHandler<MediaSubItemTreeAddedEventArgs> SubItemTreeAdded
        // args: Media SubItem

MediaPlayer : Internal (IDisposable)
------------------------------------
The playback engine. One MediaPlayer plays one Media at a time; reuse it
by assigning `Media`.

    public MediaPlayer(LibVLC libVLC)
    public MediaPlayer(Media media)

    Lifecycle
    public Media? Media { get; set; }
    public bool Play()
    public bool Play(Media media)
    public void Pause()                      // toggles
    public void SetPause(bool pause)
    public void Stop()
    public bool IsPlaying
    public VLCState State
    public bool WillPlay
    public bool CanPause
    public bool IsSeekable

    Position and timing
    public long Length                       // ms
    public long Time { get; set; }           // ms
    public float Position { get; set; }      // 0.0 .. 1.0
    public void SeekTo(TimeSpan time)
    public float Rate
    public int SetRate(float rate)
    public float Fps
    public void NextFrame()

    Native window embedding (desktop; NOT available on Wayland/framebuffer —
    use VideoFrameSink there)
    public IntPtr Hwnd { get; set; }         // Windows HWND
    public uint XWindow { get; set; }        // X11 window id
    public IntPtr NsObject { get; set; }     // macOS NSView
    public bool Fullscreen { get; set; }
    public void ToggleFullscreen()
    public bool EnableKeyInput { get; set; }
    public bool EnableMouseInput { get; set; }
    public uint VoutCount
    public bool Size(uint num, ref uint px, ref uint py)
    public bool Cursor(uint num, ref int px, ref int py)

    Video geometry and picture
    public float Scale { get; set; }
    public string? AspectRatio { get; set; } // "16:9", null = source
    public string? CropGeometry { get; set; }
    public void ApplyUniformScale(double viewWidth, double viewHeight,
                                  double scalingFactor)
    public void SetDeinterlace(string? deinterlaceMode)
    public bool TakeSnapshot(uint num, string? filePath, uint width, uint height)
        // 0,0 = original size; one of them 0 = keep aspect
    public VideoViewpoint Viewpoint { get; }
    public bool UpdateViewpoint(float yaw, float pitch, float roll, float fov,
                                bool absolute = true)         // 360 video

    Video tracks, subtitles (SPU), titles and chapters
    public int VideoTrackCount
    public TrackDescription[] VideoTrackDescription
    public int VideoTrack
    public bool SetVideoTrack(int trackIndex)
    public int SpuCount
    public TrackDescription[] SpuDescription
    public int Spu
    public bool SetSpu(int spu)
    public long SpuDelay
    public bool SetSpuDelay(long delay)
    public int TitleCount
    public int Title { get; set; }
    public TrackDescription[] TitleDescription
    public int ChapterCount
    public int Chapter { get; set; }
    public int ChapterCountForTitle(int title)
    public TrackDescription[] ChapterDescription(int titleIndex)
    public ChapterDescription[] FullChapterDescriptions(int titleIndex = -1)
    public void PreviousChapter()
    public void NextChapter()
    public void Navigate(uint navigate)      // cast a NavigationMode value
    public void SetVideoTitleDisplay(Position position, uint timeout)
    public int Teletext { get; set; }
    public void ToggleTeletext()
    public bool ProgramScambled              // (sic — upstream spelling)
    public bool AddSlave(MediaSlaveType type, string uri, bool select)

    Audio
    public int Volume { get; set; }          // 0..100
    public bool Mute { get; set; }
    public void ToggleMute()
    public long AudioDelay                   // microseconds
    public bool SetAudioDelay(long delay)
    public int AudioTrackCount
    public TrackDescription[] AudioTrackDescription
    public int AudioTrack
    public bool SetAudioTrack(int trackIndex)
    public AudioOutputChannel Channel
    public bool SetChannel(AudioOutputChannel channel)
    public bool SetAudioOutput(string name)  // from LibVLC.AudioOutputs
    public string? OutputDevice
    public void SetOutputDevice(string deviceId, string? module = null)
    public AudioOutputDevice[] AudioOutputDeviceEnum
    public bool SetEqualizer(Equalizer equalizer)
    public bool UnsetEqualizer()

    Video filters (marquee / logo / adjust)
    public int MarqueeInt(VideoMarqueeOption option)
    public string? MarqueeString(VideoMarqueeOption option)
    public void SetMarqueeInt(VideoMarqueeOption option, int value)
    public void SetMarqueeString(VideoMarqueeOption option, string? marqueeValue)
    public int LogoInt(VideoLogoOption option)
    public void SetLogoInt(VideoLogoOption option, int value)
    public void SetLogoString(VideoLogoOption option, string? logoValue)
    public int AdjustInt(VideoAdjustOption option)
    public void SetAdjustInt(VideoAdjustOption option, int value)
    public float AdjustFloat(VideoAdjustOption option)
    public void SetAdjustFloat(VideoAdjustOption option, float value)

    Renderer (casting) and role
    public bool SetRenderer(RendererItem? rendererItem)   // null = local output
    public MediaPlayerRole Role
    public bool SetRole(MediaPlayerRole role)

    Media-option shortcuts (apply to the CURRENT Media)
    public bool EnableHardwareDecoding { get; set; }
    public uint FileCaching { get; set; }    // ms
    public uint NetworkCaching { get; set; } // ms

    Raw video callbacks (what VideoFrameSink wraps; use the sink instead)
    public void SetVideoFormat(string chroma, uint width, uint height, uint pitch)
    public void SetVideoFormatCallbacks(LibVLCVideoFormatCb formatCb,
                                        LibVLCVideoCleanupCb? cleanupCb)
    public void SetVideoCallbacks(LibVLCVideoLockCb lockCb,
        LibVLCVideoUnlockCb? unlockCb, LibVLCVideoDisplayCb? displayCb)

    Audio callbacks (see AUDIO CALLBACKS below)
    public void SetAudioFormat(string format, uint rate, uint channels)
    public void SetAudioFormatCallback(LibVLCAudioSetupCb setupCb,
                                       LibVLCAudioCleanupCb cleanupCb)
    public void SetAudioCallbacks(LibVLCAudioPlayCb playCb,
        LibVLCAudioPauseCb? pauseCb, LibVLCAudioResumeCb? resumeCb,
        LibVLCAudioFlushCb? flushCb, LibVLCAudioDrainCb? drainCb)
    public void SetVolumeCallback(LibVLCVolumeCb volumeCb)

    Nested callback delegates (all [UnmanagedFunctionPointer(Cdecl)])
    public delegate IntPtr LibVLCVideoLockCb(IntPtr opaque, IntPtr planes)
    public delegate void LibVLCVideoUnlockCb(IntPtr opaque, IntPtr picture, IntPtr planes)
    public delegate void LibVLCVideoDisplayCb(IntPtr opaque, IntPtr picture)
    public delegate uint LibVLCVideoFormatCb(ref IntPtr opaque, IntPtr chroma,
        ref uint width, ref uint height, ref uint pitches, ref uint lines)
    public delegate void LibVLCVideoCleanupCb(ref IntPtr opaque)
    public delegate int LibVLCAudioSetupCb(ref IntPtr opaque, ref IntPtr format,
        ref uint rate, ref uint channels)
    public delegate void LibVLCAudioCleanupCb(IntPtr opaque)
    public delegate void LibVLCAudioPlayCb(IntPtr data, IntPtr samples, uint count, long pts)
    public delegate void LibVLCAudioPauseCb(IntPtr data, long pts)
    public delegate void LibVLCAudioResumeCb(IntPtr data, long pts)
    public delegate void LibVLCAudioFlushCb(IntPtr data, long pts)
    public delegate void LibVLCAudioDrainCb(IntPtr data)
    public delegate void LibVLCVolumeCb(IntPtr data, float volume, bool mute)

    Events (ALL raised on libvlc threads — see COMMON PITFALLS)
    public event EventHandler<MediaPlayerMediaChangedEventArgs>    MediaChanged
        // args: Media Media
    public event EventHandler<EventArgs>                           NothingSpecial
    public event EventHandler<EventArgs>                           Opening
    public event EventHandler<MediaPlayerBufferingEventArgs>       Buffering
        // args: float Cache   (0..100 percent)
    public event EventHandler<EventArgs>                           Playing
    public event EventHandler<EventArgs>                           Paused
    public event EventHandler<EventArgs>                           Stopped
    public event EventHandler<EventArgs>                           Forward
    public event EventHandler<EventArgs>                           Backward
    public event EventHandler<EventArgs>                           EndReached
    public event EventHandler<EventArgs>                           EncounteredError
    public event EventHandler<MediaPlayerTimeChangedEventArgs>     TimeChanged
        // args: long Time   (ms)
    public event EventHandler<MediaPlayerPositionChangedEventArgs> PositionChanged
        // args: float Position
    public event EventHandler<MediaPlayerSeekableChangedEventArgs> SeekableChanged
        // args: int Seekable
    public event EventHandler<MediaPlayerPausableChangedEventArgs> PausableChanged
        // args: int Pausable
    public event EventHandler<MediaPlayerTitleChangedEventArgs>    TitleChanged
        // args: int Title
    public event EventHandler<MediaPlayerChapterChangedEventArgs>  ChapterChanged
        // args: int Chapter
    public event EventHandler<MediaPlayerSnapshotTakenEventArgs>   SnapshotTaken
        // args: string Filename
    public event EventHandler<MediaPlayerLengthChangedEventArgs>   LengthChanged
        // args: long Length   (ms)
    public event EventHandler<MediaPlayerVoutEventArgs>            Vout
        // args: int Count   (video outputs)
    public event EventHandler<MediaPlayerScrambledChangedEventArgs> ScrambledChanged
        // args: int Scrambled
    public event EventHandler<MediaPlayerESAddedEventArgs>         ESAdded
        // args: int Id; TrackType Type
    public event EventHandler<MediaPlayerESDeletedEventArgs>       ESDeleted
        // args: int Id; TrackType Type
    public event EventHandler<MediaPlayerESSelectedEventArgs>      ESSelected
        // args: int Id; TrackType Type
    public event EventHandler<MediaPlayerAudioDeviceEventArgs>     AudioDevice
        // args: string AudioDevice
    public event EventHandler<EventArgs>                           Corked
    public event EventHandler<EventArgs>                           Uncorked
    public event EventHandler<EventArgs>                           Muted
    public event EventHandler<EventArgs>                           Unmuted
    public event EventHandler<MediaPlayerVolumeChangedEventArgs>   VolumeChanged
        // args: float Volume

    The event-args members above are public readonly FIELDS (not
    properties): e.g. `e.Time`, `e.Cache`, `e.Filename`.

VideoFrameSink : IDisposable                (CodeBrix addition)
----------------------------
Renders a MediaPlayer's decoded video into CPU memory (libvlc "vmem") and
raises FrameReady per displayed frame as 32-bit BGRA. Works on every
desktop platform and windowing system — including Wayland and
bare-framebuffer hosts where libvlc has no window-embedding API — and
needs only libvlc's base plugin set.

    public VideoFrameSink(MediaPlayer mediaPlayer)          // 3-buffer ring
    public VideoFrameSink(MediaPlayer mediaPlayer, int bufferCount)  // 1..8
    public event EventHandler<VideoFrameReadyEventArgs> FrameReady
    public event EventHandler<VideoFrameFormatChangedEventArgs> FormatChanged
    public MediaPlayer MediaPlayer
    public int BufferCount
    public uint Width          // 0 until a format is negotiated
    public uint Height
    public uint PitchBytes     // >= Width*4, rounded up to a 32-byte multiple
    public void Dispose()      // ONLY after the player is stopped/disposed

    Rules: construct BEFORE Play() — attaching permanently switches the
    player to memory rendering for its lifetime. Events are raised on
    libvlc threads: copy pixels and return fast, never touch UI objects,
    never call MediaPlayer members from inside a handler (deadlock). A
    handler that throws is caught and traced (an escaping exception
    would crash the process inside native code).

VideoFrameReadyEventArgs : EventArgs   (sealed; instance is REUSED per frame)
    public IntPtr Plane        // first pixel; BGRA top-down; valid only until
                               // the handler returns
    public uint Width
    public uint Height         // only the first Height scanlines hold picture
    public uint PitchBytes     // bytes per scanline, >= Width*4

VideoFrameFormatChangedEventArgs : EventArgs   (sealed)
    public uint Width
    public uint Height
    public uint PitchBytes
    public uint Lines          // scanlines allocated per buffer (>= Height,
                               // 32-line multiple); buffer = PitchBytes*Lines

VideoFrameSource : IDisposable              (CodeBrix addition)
------------------------------
The push-model mirror of the sink: caller-supplied BGRx frames go INTO
libvlc through its in-memory input ("imem") and out through a stream-
output chain — typically a transcode-to-file chain. ONE-SHOT: construct,
Start, PushFrame..., Complete, WaitForCompletion, Dispose.

    public VideoFrameSource(LibVLC libVLC, uint width, uint height,
                            uint frameRate, params string[] mediaOptions)
    public static bool IsSupported(LibVLC libVLC)     // probes "imem"; cached
    public static void EnsureSupported(LibVLC libVLC) // throws VLCException
    public uint Width
    public uint Height
    public uint FrameRate
    public uint PitchBytes                            // Width*4 (packed input)
    public long FramesPushed
    public bool IsFinished
    public bool Start()
    public bool PushFrame(IntPtr pixels, uint sourcePitchBytes = 0,
                          long presentationTimeUs = -1)   // blocks when full
    public unsafe bool PushFrame(ReadOnlySpan<byte> pixels,
                                 long presentationTimeUs = -1)
    public void Complete()
    public bool WaitForCompletion(TimeSpan timeout)
    public void Dispose()

    ENCODER SETTING THAT MATTERS: with x264 defaults the transcode chain
    buffers ~40 frames of lookahead and does not drain them at end of
    stream, so a short clip yields an EMPTY file. Always pass a live-
    tuned chain, e.g.
      ":sout=#transcode{vcodec=h264,vb=2000,venc=x264{tune=zerolatency}}"
      + ":standard{access=file,mux=mp4,dst=out.mp4}"

MediaList : Internal, IEnumerable<Media>
----------------------------------------
    public MediaList(LibVLC libVLC)
    public MediaList(Media media)                // media.SubItems view
    public MediaList(MediaDiscoverer mediaDiscoverer)
    public void SetMedia(Media media)
    public bool AddMedia(Media media)
    public bool InsertMedia(Media media, int position)
    public bool RemoveIndex(int positionIndex)
    public int Count
    public Media? this[int position]
    public int IndexOf(Media media)
    public bool IsReadonly
    public IEnumerator<Media> GetEnumerator()
    public event EventHandler<MediaListItemAddedEventArgs>      ItemAdded
    public event EventHandler<MediaListWillAddItemEventArgs>    WillAddItem
    public event EventHandler<MediaListItemDeletedEventArgs>    ItemDeleted
    public event EventHandler<MediaListWillDeleteItemEventArgs> WillDeleteItem
    public event EventHandler<EventArgs>                        EndReached
        // the four *Item* args derive from MediaListBaseEventArgs:
        //   public readonly Media Media; public readonly int Index;
    Note: this is a list, not a player. There is no media-list player in
    this package; iterate the list and assign Media to a MediaPlayer.

MediaDiscoverer : Internal   (local sources: devices, LAN shares, podcasts)
--------------------------
    public MediaDiscoverer(LibVLC libVLC, string name)
        // name from LibVLC.MediaDiscoverers(category)[i].Name
    public bool Start()
    public void Stop()
    public bool IsRunning
    public string? LocalizedName
    public MediaList? MediaList              // discovered items appear here
    public event EventHandler<EventArgs> Started
    public event EventHandler<EventArgs> Stopped

    public enum MediaDiscovererCategory { Devices = 0, Lan = 1, Podcasts = 2,
                                          Localdirs = 3 }
    public readonly struct MediaDiscovererDescription
        { string? Name; string? LongName; MediaDiscovererCategory Category; }

RendererDiscoverer / RendererItem   (Chromecast, UPnP renderers)
---------------------------------
    public RendererDiscoverer(LibVLC libVLC, string? name = null)
        // null lets the library pick the platform's protocol; otherwise a
        // name from LibVLC.RendererList
    public bool Start()
    public void Stop()
    public event EventHandler<RendererDiscovererItemAddedEventArgs>   ItemAdded
    public event EventHandler<RendererDiscovererItemDeletedEventArgs> ItemDeleted
        // both args expose: public RendererItem RendererItem { get; }

    public class RendererItem : Internal
        public string Name
        public string Type                   // e.g. "chromecast"
        public string? IconUri
        public bool CanRenderVideo
        public bool CanRenderAudio
    Hand an item to MediaPlayer.SetRenderer(item) BEFORE Play() to cast.

    public readonly struct RendererDescription { string? Name; string? LongName; }

Equalizer : Internal
--------------------
    public Equalizer()                       // flat
    public Equalizer(uint index)             // from preset index
    public bool SetPreamp(float preamp)      // dB
    public float Preamp
    public bool SetAmp(float amp, uint band)
    public float Amp(uint band)
    public uint PresetCount
    public string? PresetName(uint index)
    public uint BandCount
    public float BandFrequency(uint index)   // Hz
    Apply with mediaPlayer.SetEqualizer(eq); changes to a set equalizer
    require calling SetEqualizer again.

Dialog and the dialog delegates
-------------------------------
libvlc asks the application questions (credentials, certificate
acceptance, progress). Opt in with LibVLC.SetDialogHandlers; each handler
returns a Task and answers through the Dialog instance it receives.

    public class Dialog
        public bool PostLogin(string? username, string? password, bool store)
        public bool PostAction(int actionIndex)   // 1 = first, 2 = second
        public bool Dismiss()
    public enum DialogQuestionType { Normal = 0, Warning = 1, Critical = 2 }
    public delegate Task DisplayError(string? title, string? text)
    public delegate Task DisplayLogin(Dialog dialog, string? title,
        string? text, string? defaultUsername, bool askStore,
        CancellationToken token)
    public delegate Task DisplayQuestion(Dialog dialog, string? title,
        string? text, DialogQuestionType type, string? cancelText,
        string? firstActionText, string? secondActionText,
        CancellationToken token)
    public delegate Task DisplayProgress(Dialog dialog, string? title,
        string? text, bool indeterminate, float position,
        string? cancelText, CancellationToken token)
    public delegate Task UpdateProgress(Dialog dialog, float position,
        string? text)

MediaInput / StreamMediaInput   (feed bytes to libvlc from managed code)
-----------------------------
    public abstract class MediaInput : IDisposable
        public GCHandle GcHandle
        public bool CanSeek { get; protected set; } = true
        public abstract bool Open(out ulong size)
        public abstract int Read(IntPtr buf, uint len)
        public abstract bool Seek(ulong offset)
        public abstract void Close()
        public void Dispose()
    public class StreamMediaInput : MediaInput
        public StreamMediaInput(Stream stream)   // wraps any Stream
    Pass to `new Media(libVLC, input)`; Media does NOT dispose the input.

MediaConfiguration   (typed builder for common per-media options)
------------------
    public bool EnableHardwareDecoding { get; set; }
    public uint FileCaching { get; set; }
    public uint NetworkCaching { get; set; }
    public string[] Build()
    Apply with media.AddOption(mediaConfiguration).

Structures (DTOs)
-----------------
In CodeBrix.Platform.MediaPlayerCore.Structures:
    public readonly struct AudioOutputDescription { string Name; string Description; }
    public readonly struct AudioOutputDevice { string DeviceIdentifier; string Description; }
    public readonly struct ChapterDescription { long TimeOffset {get;}
        long Duration {get;} string? Name {get;} }
    public readonly struct ModuleDescription { string? Name; string? ShortName;
        string? LongName; string? Help; }
    public readonly struct TrackDescription { int Id {get;} string Name {get;} }

In the root namespace:
    public readonly struct MediaTrack { uint Codec; uint OriginalFourcc; int Id;
        TrackType TrackType; int Profile; int Level; MediaTrackData Data;
        uint Bitrate; string? Language; string? Description; }
    public readonly struct MediaTrackData { AudioTrack Audio; VideoTrack Video;
        SubtitleTrack Subtitle; }        // read the member matching TrackType
    public readonly struct AudioTrack { uint Channels; uint Rate; }
    public readonly struct VideoTrack { uint Height; uint Width; uint SarNum;
        uint SarDen; uint FrameRateNum; uint FrameRateDen;
        VideoOrientation Orientation; VideoProjection Projection;
        VideoViewpoint Pose; }
    public readonly struct SubtitleTrack { string? Encoding; }
    public readonly struct VideoViewpoint { float Yaw; float Pitch; float Roll; float Fov; }
    public readonly struct MediaSlave { string Uri; MediaSlaveType Type; uint Priority; }
    public readonly struct MediaStats { int ReadBytes; float InputBitrate;
        int DemuxReadBytes; float DemuxBitrate; int DemuxCorrupted;
        int DemuxDiscontinuity; int DecodedVideo; int DecodedAudio;
        int DisplayedPictures; int LostPictures; int PlayedAudioBuffers;
        int LostAudioBuffers; int SentPackets; int SentBytes; float SendBitrate; }
    public readonly struct MediaDiscovererDescription   (see above)
    public readonly struct RendererDescription           (see above)

Other public types
------------------
    public class VLCException : Exception
        public VLCException(string message = "")
        public VLCException(string message, Exception innerException)
    public abstract class Internal : IDisposable        // base of every
        public IntPtr NativeReference                   // native wrapper
        public void Dispose()
    public class PlatformHelper
        public static bool IsWindows / IsLinux / IsLinuxDesktop / IsMac /
                           IsX64BitProcess
    public class MediaPlayerChangedEventArgs : EventArgs
        public MediaPlayerChangedEventArgs(MediaPlayer? oldMediaPlayer,
                                           MediaPlayer? newMediaPlayer)
        public MediaPlayer? OldMediaPlayer { get; }
        public MediaPlayer? NewMediaPlayer { get; }
    public class MediaPlayerChangingEventArgs : EventArgs   // same shape
        These two exist for view implementations that raise a
        "MediaPlayer swapped" event; nothing in this package raises them.

ENUMERATIONS (complete, with values)
------------------------------------
    LogLevel            Debug = 0, Notice = 2, Warning = 3, Error = 4
    VLCState            NothingSpecial = 0, Opening = 1, Buffering = 2,
                        Playing = 3, Paused = 4, Stopped = 5, Ended = 6,
                        Error = 7
    TrackType           Unknown = -1, Audio = 0, Video = 1, Text = 2
    VideoOrientation    TopLeft = 0, TopRight = 1, BottomLeft = 2,
                        BottomRight = 3, LeftTop = 4, LeftBottom = 5,
                        RightTop = 6, RightBottom = 7
    VideoProjection     Rectangular = 0, Equirectangular = 1,
                        CubemapLayoutStandard = 256
    MediaSlaveType      Subtitle = 0, Audio = 1
    MetadataType        Title = 0, Artist = 1, Genre = 2, Copyright = 3,
                        Album = 4, TrackNumber = 5, Description = 6,
                        Rating = 7, Date = 8, Setting = 9, URL = 10,
                        Language = 11, NowPlaying = 12, Publisher = 13,
                        EncodedBy = 14, ArtworkURL = 15, TrackID = 16,
                        TrackTotal = 17, Director = 18, Season = 19,
                        Episode = 20, ShowName = 21, Actors = 22,
                        AlbumArtist = 23, DiscNumber = 24, DiscTotal = 25
    FromType            FromPath, FromLocation, AsNode
                        (FromPath = a filesystem path; FromLocation = a
                        URL/MRL such as "http://", "imem://", "dvd://")
    MediaParseOptions   ParseLocal = 0, ParseNetwork = 1, FetchLocal = 2,
                        FetchNetwork = 4, DoInteract = 8
                        (single-bit values; combine with | — e.g.
                        ParseNetwork | FetchLocal fetches cover art)
    MediaParsedStatus   Skipped = 1, Failed = 2, Timeout = 3, Done = 4
    MediaType           Unknown = 0, File = 1, Directory = 2, Disc = 3,
                        Stream = 4, Playlist = 5
    MediaDiscovererCategory   Devices = 0, Lan = 1, Podcasts = 2, Localdirs = 3
    DialogQuestionType  Normal = 0, Warning = 1, Critical = 2
    Title               Menu = 1, Interactive = 2
    VideoMarqueeOption  Enable = 0, Text = 1, Color = 2, Opacity = 3,
                        Position = 4, Refresh = 5, Size = 6, Timeout = 7,
                        X = 8, Y = 9
    NavigationMode      Activate = 0, Up = 1, Down = 2, Left = 3, Right = 4,
                        Popup = 5          (DVD/Bluray menus; Navigate((uint)mode))
    Position            Disable = -1, Center = 0, Left = 1, Right = 2,
                        Top = 3, TopLeft = 4, TopRight = 5, Bottom = 6,
                        BottomLeft = 7, BottomRight = 8
    TeletextKey         Red = 7471104, Green = 6750208, Yellow = 7929856,
                        Blue = 6422528, Index = 6881280
    VideoLogoOption     Enable = 0, File = 1, X = 2, Y = 3, Delay = 4,
                        Repeat = 5, Opacity = 6, Position = 7
    VideoAdjustOption   Enable = 0, Contrast = 1, Brightness = 2, Hue = 3,
                        Saturation = 4, Gamma = 5
    AudioOutputChannel  Error = -1, Stereo = 1, RStereo = 2, Left = 3,
                        Right = 4, Dolbys = 5
    MediaPlayerRole     None = 0, Music = 1, Video = 2, Communication = 3,
                        Game = 4, LiblvcRoleNotification = 5 (sic),
                        Animation = 6, Production = 7, Accessibility = 8,
                        Test = 9

THE LOG SURFACE
---------------
    public event EventHandler<LogEventArgs> Log        (on LibVLC)
    public sealed class LogEventArgs : EventArgs
        public LogLevel Level { get; }
        public string Message { get; }
        public string? Module { get; }
        public string? SourceFile { get; }
        public uint? SourceLine { get; }
        public string FormattedLog { get; }
    Subscribing to Log REPLACES libvlc's default logger (nothing is
    printed to the console any more); unsubscribing the last handler
    restores it. SetLogFile(path)/CloseLogFile() route the native log to
    a file instead. `new LibVLC(enableDebugLogs: true)` raises the native
    verbosity so Debug entries reach the event.

AUDIO CALLBACKS
---------------
To receive decoded PCM instead of playing it (visualizers, level meters,
recording, custom output), fix the format and set the callbacks BEFORE
Play(). Setting audio callbacks disables ALL libvlc audio output.

    player.SetAudioFormat("S16N", 44100, 2);          // 16-bit native-endian
                                                      // stereo at 44.1 kHz
    player.SetAudioCallbacks(playCb, pauseCb, resumeCb, flushCb, drainCb);

    playCb   : LibVLCAudioPlayCb(IntPtr data, IntPtr samples, uint count, long pts)
               `samples` points at `count` sample FRAMES (per libvlc,
               count is the number of samples per channel); with S16N
               stereo that is count * 2 channels * 2 bytes of interleaved
               PCM. `pts` is the presentation time in microseconds. Copy
               the bytes out before returning.
    pauseCb/resumeCb/flushCb(IntPtr data, long pts), drainCb(IntPtr data):
               may be null. `data` is always IntPtr.Zero here (the managed
               binding passes no user pointer).
    Alternatively SetAudioFormatCallback(setupCb, cleanupCb) lets libvlc
    propose a format and the setup callback adjust rate/channels; the
    `format` pointer is a 4-byte fourcc buffer (e.g. "S16N", "FL32").
    SetVolumeCallback receives volume/mute changes when callbacks are on.

    As with every libvlc callback: these run on libvlc threads; never
    call back into MediaPlayer from inside them.


COMPLETE EXAMPLES
=================

1. Play a file to the end (console)
-----------------------------------
    using System;
    using System.Threading.Tasks;
    using CodeBrix.Platform.MediaPlayerCore;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Core.Initialize();                       // throws VLCException if
                                                     // libvlc is not installed
            using var libVLC = new LibVLC();
            using var media = new Media(libVLC, args[0]);   // FromType.FromPath
            using var player = new MediaPlayer(media);

            // RunContinuationsAsynchronously is ESSENTIAL: the TCS is completed
            // from a libvlc event thread, and an inline continuation would run
            // the rest of Main (including Stop()) on that thread -> deadlock.
            var finished = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            long lengthMs = 0;

            player.LengthChanged += (_, e) => lengthMs = e.Length;
            player.TimeChanged += (_, e) =>
                Console.Write($"\r{e.Time / 1000}s / {lengthMs / 1000}s   ");
            player.EndReached += (_, _) => finished.TrySetResult(true);
            player.EncounteredError += (_, _) => finished.TrySetResult(false);

            if (!player.Play())
            {
                Console.Error.WriteLine("libvlc refused to start playback.");
                return 1;
            }
            bool ok = await finished.Task;
            player.Stop();                           // safe here: we are back on
                                                     // a thread-pool thread
            Console.WriteLine(ok ? "\nDone." : "\nPlayback failed.");
            return ok ? 0 : 1;
        }
    }

    Notes: a URL is `new Media(libVLC, "http://host/clip.mp4",
    FromType.FromLocation)` or `new Media(libVLC, new Uri(...))`. Extra
    per-media options go last: `new Media(libVLC, path, FromType.FromPath,
    ":input-repeat=65535")` loops the clip. Video appears in a window that
    libvlc creates itself when no Hwnd/XWindow/NsObject is set and no
    VideoFrameSink is attached; pass "--no-video" to LibVLC for audio-only.

2. Video frames into memory with VideoFrameSink (Skia, any windowing system)
----------------------------------------------------------------------------
    using System;
    using System.Runtime.InteropServices;
    using CodeBrix.Platform.MediaPlayerCore;
    using SkiaSharp;                         // any BGRA-capable bitmap works

    public sealed class FramePump : IDisposable
    {
        private readonly LibVLC _libVLC = new LibVLC("--no-audio");
        private readonly MediaPlayer _player;
        private readonly VideoFrameSink _sink;
        private readonly object _sync = new object();
        private byte[] _packed;              // tightly packed BGRA copy
        private int _width, _height;
        private bool _dirty;

        public FramePump()
        {
            Core.Initialize();
            _player = new MediaPlayer(_libVLC);
            _sink = new VideoFrameSink(_player);       // BEFORE Play()
            _sink.FormatChanged += (_, e) =>
            {
                lock (_sync)
                {
                    _width = (int)e.Width;
                    _height = (int)e.Height;
                    _packed = new byte[_width * _height * 4];
                }
            };
            _sink.FrameReady += (_, e) =>
            {
                // libvlc thread: copy and leave. Rows are PitchBytes apart in
                // the source (32-byte aligned) but Width*4 apart in _packed.
                lock (_sync)
                {
                    if (_packed == null) { return; }
                    int row = (int)e.Width * 4;
                    for (int y = 0; y < e.Height; y++)
                    {
                        Marshal.Copy(e.Plane + (int)(y * e.PitchBytes),
                            _packed, y * row, row);
                    }
                    _dirty = true;
                }
                // signal the UI to repaint here (e.g. Dispatcher.Post(...));
                // do NOT draw from this thread.
            };
        }

        public void Open(string path)
        {
            _player.Media = new Media(_libVLC, path);
            _player.Play();
        }

        // Call from the UI thread's paint handler.
        public void Paint(SKCanvas canvas)
        {
            lock (_sync)
            {
                if (!_dirty || _packed == null) { return; }
                using var bitmap = new SKBitmap(new SKImageInfo(_width, _height,
                    SKColorType.Bgra8888, SKAlphaType.Opaque));
                Marshal.Copy(_packed, 0, bitmap.GetPixels(), _packed.Length);
                canvas.DrawBitmap(bitmap, 0, 0);
                _dirty = false;
            }
        }

        public void Dispose()
        {
            _player.Stop();                  // stop BEFORE disposing the sink
            _sink.Dispose();
            _player.Media?.Dispose();
            _player.Dispose();
            _libVLC.Dispose();
        }
    }

3. Parse metadata and inspect tracks
------------------------------------
    using var libVLC = new LibVLC();
    using var media = new Media(libVLC, path);
    MediaParsedStatus status = await media.Parse(MediaParseOptions.ParseLocal);
    if (status == MediaParsedStatus.Done)
    {
        Console.WriteLine($"{media.Meta(MetadataType.Artist)} - " +
                          $"{media.Meta(MetadataType.Title)}  {media.Duration} ms");
        foreach (MediaTrack track in media.Tracks)
        {
            switch (track.TrackType)
            {
                case TrackType.Video:
                    VideoTrack v = track.Data.Video;
                    Console.WriteLine($"video {v.Width}x{v.Height} " +
                        $"{v.FrameRateNum}/{v.FrameRateDen} fps {v.Orientation}");
                    break;
                case TrackType.Audio:
                    AudioTrack a = track.Data.Audio;
                    Console.WriteLine($"audio {a.Channels}ch {a.Rate} Hz " +
                        $"{track.Language}");
                    break;
                case TrackType.Text:
                    Console.WriteLine($"subtitle {track.Language} " +
                        $"{track.Data.Subtitle.Encoding}");
                    break;
            }
        }
    }
    Use MediaParseOptions.ParseNetwork for URLs (ParseLocal refuses to
    touch the network). Directory/playlist media expand into
    media.SubItems after parsing.

4. Route libvlc's log into your logger
--------------------------------------
    libVLC.Log += (_, e) =>
    {
        if (e.Level >= LogLevel.Warning)
        {
            logger.LogWarning("libvlc[{Module}] {Message}", e.Module, e.Message);
        }
    };
    // or: libVLC.SetLogFile("/tmp/libvlc.log");

5. Capture decoded audio as PCM (level meter)
---------------------------------------------
    using var libVLC = new LibVLC("--no-video");
    using var player = new MediaPlayer(new Media(libVLC, path));
    var buffer = new byte[0];
    MediaPlayer.LibVLCAudioPlayCb play = (data, samples, count, pts) =>
    {
        int bytes = (int)count * 2 /*channels*/ * 2 /*bytes per S16 sample*/;
        if (buffer.Length < bytes) { buffer = new byte[bytes]; }
        Marshal.Copy(samples, buffer, 0, bytes);
        // ... compute RMS / write to a file / feed an encoder ...
    };
    player.SetAudioFormat("S16N", 44100, 2);
    player.SetAudioCallbacks(play, null, null, null, null);
    player.Play();
    Keep the delegate in a field or local that outlives playback (the
    player holds a reference, but do not let the only reference be a
    temporary).

6. Equalizer preset
-------------------
    using var eq = new Equalizer();
    for (uint i = 0; i < eq.PresetCount; i++)
        Console.WriteLine($"{i}: {eq.PresetName(i)}");
    using var rock = new Equalizer(index: 2);     // pick by index
    rock.SetPreamp(6f);
    rock.SetAmp(4f, band: 0);                     // boost the lowest band
    player.SetEqualizer(rock);

7. Discover and cast to a Chromecast
------------------------------------
    using var discoverer = new RendererDiscoverer(libVLC);
    discoverer.ItemAdded += (_, e) =>
    {
        RendererItem item = e.RendererItem;      // keep it alive while used
        Console.WriteLine($"{item.Name} ({item.Type}) video={item.CanRenderVideo}");
        if (item.CanRenderVideo)
        {
            player.SetRenderer(item);             // before Play()
            player.Play(new Media(libVLC, "http://host/movie.mp4",
                                  FromType.FromLocation));
        }
    };
    discoverer.Start();

8. Encode generated frames to MP4 with VideoFrameSource
-------------------------------------------------------
    VideoFrameSource.EnsureSupported(libVLC);           // "imem" present?
    using var source = new VideoFrameSource(libVLC, 640, 360, 30,
        ":sout=#transcode{vcodec=h264,vb=2000,venc=x264{tune=zerolatency}}" +
        ":standard{access=file,mux=mp4,dst=out.mp4}");
    if (!source.Start()) { throw new InvalidOperationException("start failed"); }
    var frame = new byte[640 * 360 * 4];                 // BGRx, packed
    for (int i = 0; i < 90; i++)
    {
        // ... draw into frame ...
        source.PushFrame(frame, presentationTimeUs: i * 1000000L / 30);
    }
    source.Complete();
    bool ok = source.WaitForCompletion(TimeSpan.FromSeconds(30));


MINIMUM VIABLE PROJECT
======================
A console player. The VideoLAN.LibVLC.Windows reference is what makes
libvlc available on Windows; on Linux install the runtime libraries
instead; on macOS install VLC.app.

    <!-- Player.csproj -->
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
      </PropertyGroup>
      <ItemGroup>
        <!-- use the latest published version of each package -->
        <PackageReference Include="CodeBrix.MediaCore.LgplLicenseForever"
                          Version="x.y.z" />
        <!-- Windows only: drops libvlc.dll + plugins next to the exe -->
        <PackageReference Include="VideoLAN.LibVLC.Windows" Version="x.y.z"
                          Condition="$([MSBuild]::IsOsPlatform('Windows'))" />
      </ItemGroup>
    </Project>

    Linux (Debian-based) one-time host setup:
        sudo apt install libvlc5 vlc-plugin-base

    // Program.cs
    using System;
    using System.Threading.Tasks;
    using CodeBrix.Platform.MediaPlayerCore;

    Core.Initialize();
    using var libVLC = new LibVLC();
    using var player = new MediaPlayer(new Media(libVLC, args[0]));
    var done = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    player.EndReached += (_, _) => done.TrySetResult(true);
    player.EncounteredError += (_, _) => done.TrySetResult(false);
    player.Play();
    Console.WriteLine(await done.Task ? "finished" : "error");
    player.Stop();

Run: `dotnet run -- /path/to/clip.mp3`


PERFORMANCE TIPS
================
  - One LibVLC per process. Constructing LibVLC loads and scans the plugin
    cache; create it once at startup and share it. MediaPlayer and Media
    are cheap by comparison; reuse a MediaPlayer by assigning `Media`.
  - Memory rendering costs: VideoFrameSink (and raw SetVideoCallbacks)
    forces CPU pixel-format conversion and disables or slows hardware
    decoding. Prefer window embedding (Hwnd/XWindow/NsObject) where the
    windowing system allows it; use the sink where it does not (Wayland,
    framebuffer) or when you need the pixels.
  - Keep FrameReady handlers to a memcpy. The sink reuses a small ring
    (default 3 buffers); a slow handler makes libvlc overwrite a buffer
    you are still reading. Raise `bufferCount` (up to 8) if handlers are
    occasionally slow; never do decoding, encoding or drawing there.
  - Copy with the pitch. Frames are PitchBytes wide (32-byte aligned),
    not Width*4; copy row by row when the destination is packed, or hand
    the pitch to an API that accepts a row stride.
  - Parse once. `Media.Parse` is I/O (and network for ParseNetwork);
    cache what you need rather than re-parsing on every property read.
  - Caching options control latency versus stutter: `NetworkCaching` /
    `FileCaching` (ms) on MediaPlayer, or ":network-caching=" on Media.
  - Event handlers run on libvlc threads. Marshal to the UI thread in
    batches (e.g. coalesce TimeChanged into one repaint per frame);
    TimeChanged fires many times per second.
  - VideoFrameSource applies back-pressure: PushFrame blocks when the
    encoder is behind. Produce frames from a dedicated thread, and use
    `tune=zerolatency` (or bframes=0,lookahead=0) so short streams flush.


COMMON PITFALLS TO AVOID
========================
  - Missing native libvlc: Core.Initialize() / new LibVLC() throws
    VLCException with the paths searched. Windows = add the
    VideoLAN.LibVLC.Windows package to the APPLICATION project (a class
    library reference is not enough — the native files must land next to
    the exe); Linux = `sudo apt install libvlc5 vlc-plugin-base`; macOS =
    install VLC.app.
  - Calling MediaPlayer members from inside a MediaPlayer, Media,
    VideoFrameSink or audio-callback handler deadlocks or crashes: those
    run on libvlc's own threads. Set a flag / post to another thread.
    In particular never call Stop() or Dispose() from EndReached.
  - Awaiting a TaskCompletionSource that a libvlc event completes: create
    it with TaskCreationOptions.RunContinuationsAsynchronously, or the
    continuation (and whatever it calls on the player) runs on the libvlc
    thread.
  - VideoFrameSink after Play(): it must be constructed before the first
    Play() on that player, and it cannot be detached. Dispose it only
    after Stop()/Dispose() of the player.
  - The FrameReady event-args instance is reused for every frame and the
    Plane pointer dies when the handler returns. Never store the args or
    the pointer.
  - `using CodeBrix.MediaCore;` does not compile. The namespace is
    CodeBrix.Platform.MediaPlayerCore even though the package is
    CodeBrix.MediaCore.LgplLicenseForever.
  - Tracks/Duration are empty until parsed or played. Await
    media.Parse(...) first; ParseLocal deliberately skips network URLs
    (use ParseNetwork).
  - Media.Parse timeout: `timeout = -1` uses libvlc's default;
    `0` waits forever. The result may be Timeout or Skipped — check the
    returned MediaParsedStatus rather than assuming Done.
  - Version mismatch: the package's major version equals the libvlc major
    it wraps. Installing a libvlc with a different major (for example a
    newer VLC generation) makes every LibVLC construction throw
    VLCException — this is by design.
  - Subscribing to LibVLC.Log silences libvlc's console output; if you
    just want more console noise, use `new LibVLC(enableDebugLogs: true)`
    or "--verbose=2" instead.
  - Audio callbacks silence libvlc: once SetAudioCallbacks is set no audio
    is output through the system; you own playback.
  - MediaList is not a player. There is no media-list-player type in this
    package; drive a MediaPlayer yourself from the list.
  - Disposing order: Stop the player, dispose the sink, dispose the Media,
    dispose the MediaPlayer, dispose LibVLC last. Disposing LibVLC while
    players exist crashes inside native code.
  - Renderer items are native handles: keep the RendererItem you passed to
    SetRenderer alive (and its discoverer running) for as long as casting
    continues.


WHAT THIS PACKAGE DOES NOT DO
=============================
  - It does not bundle native libvlc on any platform (see INSTALLATION).
  - It contains no UI: no XAML control, no view for WPF/WinUI/GTK/Skia.
    The CodeBrix.Platform MediaPlayer add-in builds a control on top of it
    (see the sibling package's AGENT-README for the seam it uses).
  - It has no media-list player, no playlist sequencing, no playback
    queue: MediaList is a container only.
  - Window embedding (Hwnd/XWindow/NsObject) is unavailable where libvlc
    has no embedding API (Wayland, framebuffer): use VideoFrameSink.
  - It does not capture from cameras — that is the sibling
    CodeBrix.Webcam.LgplLicenseForever package.
  - Platform-specific upstream view layers (Android, iOS/macOS UIKit,
    UWP/WinUI XAML, WPF, WinForms, MAUI, Avalonia, Eto, GTK) are not
    ported and are not planned inside this package.
  - No Windows, Linux or macOS mobile targets: .NET 10 desktop only.


WORKING EXAMPLES ON GITHUB
==========================
The test suite (ported from upstream and extended) is the reference for
every API family; each file constructs real objects against a real libvlc:

  https://github.com/ellisnet/CodeBrix.Platform.MediaPlayerCore/tree/main/tests/CodeBrix.Platform.MediaPlayerCore.Tests
      BaseSetup.cs               -- LibVLC("--no-audio","--no-video") fixture;
                                    the RunContinuationsAsynchronously pattern
      VideoFrameSinkTests.cs     -- FormatChanged/FrameReady end to end on a
                                    real MP4 (":input-repeat=65535" to loop)
      VideoFrameSourceTests.cs   -- imem probe, push frames, encode to MP4
      MediaPlayerTests.cs        -- Play/Pause/Stop, Time/Position, events
      MediaTests.cs              -- ctors, Parse, Meta, Tracks, slaves,
                                    StreamMediaInput
      MediaListTests.cs          -- list mutation and events
      MediaDiscovererTests.cs    -- MediaDiscoverers + MediaDiscoverer
      RendererDiscovererTests.cs -- RendererList + RendererDiscoverer
      EqualizerTests.cs          -- presets, bands, preamp
      DialogTests.cs             -- SetDialogHandlers round trip
      EventManagerTests.cs       -- subscribe/unsubscribe semantics
      LibVLCTests.cs             -- ctor options, Version, filters, outputs
      CoreLoadingTests.cs        -- Core.Initialize / VLCException paths

  https://github.com/ellisnet/CodeBrix.Platform.MediaPlayerCore/tree/main/src/CodeBrix.Webcam/Internal
      LibVlcCaptureBackend.cs    -- a production consumer of this engine:
                                    Media from a capture MRL, VideoFrameSink
                                    for frames, VideoFrameSource + sout for
                                    overlay recording (Linux/macOS)


QUICK REFERENCE CARD
====================
Install:        dotnet add package CodeBrix.MediaCore.LgplLicenseForever
                (+ VideoLAN.LibVLC.Windows in the Windows app project;
                 apt libvlc5 vlc-plugin-base on Linux; VLC.app on macOS)
Namespace:      using CodeBrix.Platform.MediaPlayerCore;   (NOT .MediaCore)
                using CodeBrix.Platform.MediaPlayerCore.Structures;
Init:           Core.Initialize();                 // once; throws VLCException
Handle:         using var libVLC = new LibVLC();   // one per process
Media:          new Media(libVLC, "/path/file.mp4")                 // path
                new Media(libVLC, "http://...", FromType.FromLocation)
                new Media(libVLC, new Uri("file:///..."))
                new Media(libVLC, new StreamMediaInput(stream))
Player:         using var mp = new MediaPlayer(media);  mp.Play(); mp.Pause();
                mp.Stop(); mp.Time (ms) / mp.Position (0..1) / mp.Volume
Wait for end:   TCS(RunContinuationsAsynchronously) + mp.EndReached
Frames out:     var sink = new VideoFrameSink(mp);      // BEFORE Play()
                sink.FrameReady += (_, e) => copy(e.Plane, e.Width,
                                               e.Height, e.PitchBytes);
Frames in:      new VideoFrameSource(libVLC, w, h, fps, ":sout=#transcode{
                vcodec=h264,vb=2000,venc=x264{tune=zerolatency}}:standard{
                access=file,mux=mp4,dst=out.mp4}")  -> Start/PushFrame/
                Complete/WaitForCompletion
Metadata:       await media.Parse(MediaParseOptions.ParseLocal);
                media.Meta(MetadataType.Title); media.Tracks; media.Duration
Audio PCM:      mp.SetAudioFormat("S16N", 44100, 2); mp.SetAudioCallbacks(...)
Log:            libVLC.Log += (_, e) => ...e.Level/e.Module/e.Message
Cast:           new RendererDiscoverer(libVLC).ItemAdded -> mp.SetRenderer(item)
EQ:             mp.SetEqualizer(new Equalizer(presetIndex))
Threading:      every event/callback is on a libvlc thread — copy, flag,
                post; never call the player from inside a handler
Dispose order:  Stop -> sink -> Media -> MediaPlayer -> LibVLC
Siblings:       src/CodeBrix.Platform.MediaPlayerCore/AGENT-README.txt
                src/CodeBrix.Webcam/AGENT-README.txt
