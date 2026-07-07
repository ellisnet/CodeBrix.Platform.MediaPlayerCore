================================================================================
AGENT-README: CodeBrix.Platform.MediaPlayerCore
A Comprehensive Guide for AI Coding Agents
================================================================================
Last updated: 2026-07-06


OVERVIEW
--------
This repository is a fully managed, cross-platform audio / video media
library family for .NET 10. It is a drop-in-compatible port of the
LibVLCSharp NuGet package, version 3.9.7 (the official .NET wrapper around
VideoLAN's LibVLC library), restricted to the cross-platform managed core.
It produces THREE NuGet packages:

  CodeBrix.MediaCore.LgplLicenseForever
      The media ENGINE: the libvlc P/Invoke binding — LibVLC, Media,
      MediaPlayer, VideoFrameSink, VideoFrameSource, MediaDiscoverer,
      RendererDiscoverer, Equalizer, Dialog, events, structures, marshalling
      helpers, and the Core native-library loader. No managed NuGet
      dependencies.
      Project: src/CodeBrix.MediaCore/

  CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever
      The MediaPlayerElement-style management layer and the IVideoView /
      IVideoControl interfaces for building playback UIs. Depends on
      CodeBrix.MediaCore.LgplLicenseForever at the same version.
      Project: src/CodeBrix.Platform.MediaPlayerCore/

  CodeBrix.Webcam.LgplLicenseForever
      Webcam capture (ORIGINAL CodeBrix code, not ported): rich async device
      enumeration (WebcamDevices.GetImagingMediaDeviceListAsync →
      IImagingMediaDevice with the full format×resolution×framerate matrix,
      camera controls, hardware ids, paired microphone), WebcamSession (live
      BGRA FrameReceived frames, CapturePhoto, StartRecording/StopRecording
      to MP4/H.264 or MJPEG-passthrough AVI, SetOverlay burn-in). Depends on
      CodeBrix.MediaCore.LgplLicenseForever at the same version.
      Project: src/CodeBrix.Webcam/

CRITICAL NAMESPACE RULE — DO NOT "FIX":
All types in the first two packages keep the original
CodeBrix.Platform.MediaPlayerCore namespaces, including every type that ships
in the CodeBrix.MediaCore assembly. The package-name/namespace mismatch is
deliberate: it lets existing consumers (notably the CodeBrix.Platform
MediaPlayer add-in) keep compiling with zero source changes. Never rename
these namespaces to CodeBrix.MediaCore.* — that would be a breaking change.

CRITICAL NO-LEAK RULE FOR CodeBrix.Webcam — DO NOT "SIMPLIFY":
CodeBrix.Webcam's public API surface must NEVER expose a
CodeBrix.Platform.MediaPlayerCore.* type — no parameter, return, property,
event, base type, interface, or generic argument. Consumers use only
CodeBrix.Webcam.* types; the engine is an internal implementation detail.
The test PublicApiLeakTests (tests/CodeBrix.Webcam.Tests) enforces this by
reflection — if it fails, fix the API, never the test.

Upstream LibVLCSharp ships a large multi-TFM matrix including platform-
specific views (Android AWindow, Apple UIKit, UWP/WinUI XAML, WPF, WinForms,
MAUI, Avalonia, Eto, GTK, Forms). This library is the NET10.0-only core;
platform-specific view layers are deliberately NOT included and may be
introduced in separate CodeBrix.Platform.* libraries.


INSTALLATION
------------
Target framework: .NET 10.0 or higher

For the media engine only (playback, frame capture, discovery — most apps):

    dotnet add package CodeBrix.MediaCore.LgplLicenseForever

For the MediaPlayerElement-style management layer (pulls in the engine
automatically as a dependency):

    dotnet add package CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever

For webcam capture (device enumeration, live preview frames, photos,
recording, overlay burn-in — pulls in the engine automatically):

    dotnet add package CodeBrix.Webcam.LgplLicenseForever

IMPORTANT: The package names carry the ".LgplLicenseForever" suffix (e.g.
"CodeBrix.MediaCore.LgplLicenseForever", not "CodeBrix.MediaCore"). Always
use the full package name when installing. All packages from this repo
always publish at the same version; never mix versions.

NATIVE LIBVLC REQUIREMENT — READ THIS. All three packages are pure managed
assemblies; NONE of them bundles the native libvlc engine. The playback
packages (CodeBrix.MediaCore / CodeBrix.Platform.MediaPlayerCore) HARD-
REQUIRE libvlc at runtime on every platform. CodeBrix.Webcam requires it
ONLY on Linux and macOS — on Windows, webcam capture and recording run on
the operating system's built-in Media Foundation engine and need NO native
runtime and NO VideoLAN packages at all (see CODEBRIX.WEBCAM ON WINDOWS
below). What "libvlc present" means differs per platform:

  Windows:  (playback packages only) the CONSUMING APPLICATION must
            reference the VideoLAN.LibVLC.Windows NuGet package. An
            installed VLC desktop application is NOT used and NOT searched
            on Windows — the NuGet reference is the only supported
            mechanism. CodeBrix.Webcam needs none of this on Windows.
  Linux:    install the runtime libraries via the system package manager:
            `sudo apt install libvlc5 vlc-plugin-base` on Debian/Ubuntu.
            Neither the full `vlc` desktop application nor the build-time
            `libvlc-dev` package is required — the loader maps the `libvlc`
            P/Invoke onto the versioned runtime soname `libvlc.so.5`, so
            the runtime packages alone suffice.
  macOS:    the VLC media player application MUST be installed on the Mac.
            That is the ordinary desktop app whose icon is named "VLC" in
            the Applications folder / Launchpad; its on-disk bundle is
            /Applications/VLC.app, which is how the rest of these docs
            refer to it. To get it: go to https://www.videolan.org/vlc/
            and click "Download VLC" (the site detects macOS; the direct
            page is https://www.videolan.org/vlc/download-macosx.html —
            pick the build matching the Mac, Apple Silicon or Intel, or
            the universal binary), open the .dmg, and drag VLC into
            /Applications. The loader finds /Applications/VLC.app
            automatically and points libvlc at the bundle's plugin
            directory via VLC_PLUGIN_PATH — no configuration needed.
            (Alternatively an application may ship the libvlc dylibs in
            its own output — app-bundled libraries win over VLC.app — but
            the VideoLAN.LibVLC.Mac NuGet package is abandoned at 3.1.3.1
            with x64-only binaries, so on Apple Silicon installing VLC is
            effectively THE route, and it is the one this repo verifies.)

So: "the VLC application must be installed" is TRUE on macOS, wrong on
Windows (NuGet reference instead, and only for playback), and wrong on
Linux (runtime libraries instead).

What happens when libvlc is missing or cannot be loaded:
  - Engine level: `Core.Initialize()` (or the first `new LibVLC()`) throws
    CodeBrix.Platform.MediaPlayerCore.VLCException listing the search paths
    it tried.
  - CodeBrix.Webcam level (Linux/macOS): opening a session
    (WebcamSession.Start) wraps that failure in a
    CodeBrix.Webcam.WebcamException whose message states the per-platform
    fix — consumers of CodeBrix.Webcam never need to catch an engine
    exception type, consistent with the no-leak rule. On Windows a missing
    libvlc cannot affect CodeBrix.Webcam at all.
  - Webcam device ENUMERATION does not need libvlc at all: the device
    providers talk to the OS directly (DirectShow / V4L2 / AVFoundation),
    so WebcamDevices.GetImagingMediaDeviceListAsync works — and returns
    full capability data — on a machine with no libvlc installed.

Call `Core.Initialize()` before constructing any LibVLC instance to ensure
the native library is loaded (CodeBrix.Webcam does this internally).


KEY NAMESPACE
-------------
All managed types — in BOTH packages/assemblies — live under:

    using CodeBrix.Platform.MediaPlayerCore;

Sub-namespaces (assembly that ships them in parentheses):

    CodeBrix.Platform.MediaPlayerCore.Core             (native-library loader;
                                                        CodeBrix.MediaCore)
    CodeBrix.Platform.MediaPlayerCore.Events           (event args / managers;
                                                        CodeBrix.MediaCore)
    CodeBrix.Platform.MediaPlayerCore.Helpers          (marshalling helpers;
                                                        CodeBrix.MediaCore)
    CodeBrix.Platform.MediaPlayerCore.MediaPlayerElement  (UI-agnostic
                                                         management layer;
                                                         CodeBrix.Platform.
                                                         MediaPlayerCore)
    CodeBrix.Platform.MediaPlayerCore.Structures       (native DTOs /
                                                        descriptions;
                                                        CodeBrix.MediaCore)


CORE API REFERENCE
------------------
Main entry point (in order of typical use):

  Core
    Core.Initialize()               -- loads the native libvlc library

  LibVLC
    new LibVLC(params string[] args)
    Version, Changeset, CompilerInfo
    AudioOutputs(), AudioOutputDevices(device)
    MediaDiscoverers(), RendererDiscoverers()
    DialogHandlers                  -- opt in to user-dialog callbacks
    Dispose()

  Media
    new Media(LibVLC libVLC, string pathOrUrl, FromType fromType = ...)
    new Media(LibVLC libVLC, Uri uri)
    new Media(LibVLC libVLC, Stream input, params string[] options)
    Parse(), ParseAsync(), ParseStop()
    Duration, State, Meta(MetadataType), SubItems, Tracks
    Dispose()

  MediaPlayer
    new MediaPlayer(LibVLC libVLC)
    new MediaPlayer(Media media)
    Play(), Pause(), Stop()
    Time, Length, Position, Rate, Volume, Mute
    SetVideoTitleDisplay, SetVideoCallbacks, SetAudioFormat,
    AudioTracks, VideoTracks, SpuTracks
    Events: Playing, Paused, Stopped, EndReached, TimeChanged, ...
    Dispose()

  VideoFrameSink                  (CodeBrix addition, not in LibVLCSharp)
    new VideoFrameSink(MediaPlayer mediaPlayer, int bufferCount = 3)
    FrameReady                      -- per-frame event: 32-bit BGRA pixels in
                                       CPU memory (libvlc "vmem" output);
                                       copy the buffer before returning
    FormatChanged                   -- negotiated dimensions/pitch changed
    Width, Height, PitchBytes, BufferCount, MediaPlayer
    Dispose()                       -- only after the player is stopped/disposed
    Windowing-system-agnostic video rendering: works on hosts where libvlc
    has no window-embedding API (Wayland, framebuffer) and anywhere else.
    Construct BEFORE Play(). Events are raised on libvlc threads: handlers
    must copy pixels quickly, must not touch UI objects directly, and must
    not call back into MediaPlayer members. Requires only libvlc's base
    plugin set (`sudo apt install libvlc5 vlc-plugin-base` on Debian/Ubuntu).

  MediaDiscoverer / RendererDiscoverer / Equalizer / Dialog
    See the source files under src/CodeBrix.MediaCore/ for exact
    signatures. API parity with LibVLCSharp 3.9.7 is the target.


CODING CONVENTIONS (CodeBrix family)
------------------------------------
These conventions apply to all CodeBrix.* libraries; they are enforced on
every PR to this repo.

  - Target framework is net10.0 only. No multi-targeting.
  - Nullable-reference-types (NRT) are OFF at the family level. Do NOT add
    `<Nullable>enable</Nullable>`. Do NOT write `?` on reference types
    (`string?`, `MyClass?`) or use the null-forgiveness `!` operator. Value-
    type nullables (`int?`, `DateOnly?`, enum `?`) remain fine.
  - Implicit global usings are OFF. Every .cs file has explicit usings at
    the top, System.* first, then others, alphabetical within each group.
  - File-scoped namespaces only (`namespace X;`). No braced block-scoped
    namespaces.
  - `<GenerateDocumentationFile>` is on. Every `public` (and `protected` on
    unsealed) member needs an XML doc comment. Never add `<NoWarn>1591</>`;
    fix CS1591 at source.
  - Test project uses xUnit v3 + SilverAssertions + coverlet.collector. No
    NUnit, no MSTest, no FluentAssertions.
  - Ported files (copied from an upstream open-source project) carry a
    `//was previously: <upstream.namespace>;` provenance comment on the
    `namespace` line. Upstream copyright / license headers are preserved
    verbatim at the top.

  Forbidden csproj properties (never add, even for a "quick fix"):
    - <Nullable>enable</Nullable>
    - <ImplicitUsings>enable</ImplicitUsings>
    - <NoWarn>...</NoWarn>
    - <WarningLevel>0</WarningLevel>
    - <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    - <LangVersion>...</LangVersion>


ARCHITECTURE
------------
The managed code mirrors the sub-folder layout of upstream LibVLCSharp
3.9.7 / src/LibVLCSharp/Shared/, with namespaces adapted to the CodeBrix
family, split across two packable projects (split performed 2026-07-06;
every file kept its namespace and content verbatim):

    src/CodeBrix.MediaCore/                    == the ENGINE package ==
        Core/                       -- libvlc native loader (desktop only)
        Events/                     -- MediaEventManager, MediaPlayerEventManager,
                                       MediaListEventManager, RendererDiscovererEventManager,
                                       EventManager base + strongly-typed event args
        Helpers/                    -- MarshalExtensions, MarshalUtils, PlatformHelper
        Structures/                 -- native DTOs: AudioOutputDescription,
                                       AudioOutputDevice, ChapterDescription,
                                       MediaDiscovererDescription, MediaSlave,
                                       MediaStats, MediaTrack, MediaTrackData,
                                       ModuleDescription, RendererDescription,
                                       TrackDescription, VideoViewpoint
        Dialog.cs                   -- user-interaction callbacks
        Equalizer.cs                -- audio equalizer
        Internal.cs                 -- Internal base class for native-ref wrappers
        InternalsVisibleTo.cs       -- InternalsVisibleTo declarations for the
                                       test assembly
        LibVLC.cs                   -- main libvlc handle
        LibVLCEvents.cs             -- native event structs / enums
        MediaConfiguration.cs       -- parse options
        Media.cs                    -- managed Media wrapper
        MediaDiscoverer.cs          -- service/file discovery
        MediaInput.cs               -- abstract input stream
        MediaList.cs                -- playlist
        MediaPlayer.cs              -- playback engine
        RendererDiscoverer.cs       -- Chromecast/UPnP discovery
        StreamMediaInput.cs         -- Stream-backed input
        VideoFrameSink.cs           -- CodeBrix addition: renders video into
                                       CPU memory (vmem) and raises FrameReady
                                       per frame; windowing-system-agnostic
                                       (event args live in Events/)
        VLCException.cs             -- domain exception

    src/CodeBrix.Platform.MediaPlayerCore/     == the UI-layer package ==
                                       (ProjectReference -> CodeBrix.MediaCore)
        MediaPlayerElement/         -- UI-agnostic managers for AspectRatio,
                                       AudioTracks, AutoHide, BufferingProgress,
                                       CastRenderers, DeviceAwakening, MediaPosition,
                                       SeekBar, State, SubtitlesTracks, Volume, etc.
                                       (FontAwesomeIcons.cs lives here too)
        InternalsVisibleTo.cs       -- InternalsVisibleTo declarations for the
                                       test assembly
        IVideoControl.cs            -- video-control interface
        IVideoView.cs               -- video-view interface

    src/CodeBrix.Webcam/                       == the webcam package ==
                                       (ProjectReference -> CodeBrix.MediaCore;
                                        namespaces: CodeBrix.Webcam[.Devices/
                                        .Capture]; engine types NEVER public)
        WebcamDevices.cs            -- async device-list entry point
        WebcamSession.cs            -- preview / photos / recording / overlay
        WebcamSessionOptions.cs     -- mode, audio, preview-composite options
        AudioCaptureMode.cs         -- Off | Auto | SpecificDevice
        WebcamException.cs          -- webcam-specific failures
        Devices/                    -- IImagingMediaDevice + capability matrix,
                                       controls (IImagingDeviceControl), hardware
                                       info, paired-microphone record
        Capture/                    -- WebcamFrameEventArgs (BGRA preview),
                                       WebcamPhoto (packed BGRA), WebcamOverlay
                                       (straight-alpha BGRA burn-in),
                                       WebcamVideoFormat, WebcamRecordingOptions,
                                       WebcamRecordingResult
        Internal/                   -- the ICaptureBackend seam (WebcamSession is
                                       backend-agnostic: overlay tee, photos,
                                       FrameReceived, locking) with two engines:
                                       LibVlcCaptureBackend (Linux/macOS — libvlc
                                       player + VideoFrameSink + sout recording,
                                       WebcamEngine, capture Media factory) and
                                       Windows/MediaFoundationCaptureBackend
                                       (IMFSourceReader capture, IMFSinkWriter
                                       MP4/H.264+AAC recording, WASAPI audio; no
                                       libvlc). Overlay compositor is shared.
                                       Per-OS device providers:
                                       Linux/ (V4L2 via libc ioctls), Windows/
                                       (DirectShow COM), Darwin/ (AVFoundation via
                                       Objective-C-runtime P/Invoke — enumeration,
                                       mode controls, TCC consent; no shim dylib)


CODEBRIX.WEBCAM ON WINDOWS
--------------------------
On Windows, WebcamSession captures through the operating system's built-in
Media Foundation engine (src/CodeBrix.Webcam/Internal/Windows/), NOT libvlc
(implemented 2026-07-06). Key facts:

  - NO native runtime and NO VideoLAN packages are required or used for
    webcam work on Windows. MF and WASAPI ship with Windows. (Windows 'N'
    editions need the Media Feature Pack; the failure message says so.)
  - Capture: IMFSourceReader opened on the enumerated device path; the
    in-box MFT converters/decoders turn every camera format (YUY2, NV12,
    MJPEG, H.264-only cameras) into the BGRA frames the shared pipeline
    expects. Requested modes (size/fps/format) are negotiated against the
    camera's native type list.
  - Recording: IMFSinkWriter MP4/H.264 (hardware-accelerated where
    available) with an in-file AAC track when the session captures a
    microphone; frame-path (overlay) recordings keep the sidecar-WAV
    contract. Recording tees off the capture loop, so starting/stopping a
    recording never interrupts the preview (no restart blink).
  - LIMITATION: WebcamVideoFormat.MjpegAvi passthrough recording is not
    supported by the sink writer; StartRecording throws a WebcamException
    directing callers to Mp4H264. Do not "fix" this by re-introducing
    libvlc on Windows — keeping the Windows path libvlc-free (and
    GPL-plugin-free) is deliberate.
  - Threading: MF/WASAPI objects are NOT apartment-agile. The source
    reader lives entirely on its capture thread, and every COM-touching
    control call routes through Internal/Windows/MtaThread — desktop UI
    threads are STA and would otherwise fail with E_NOINTERFACE. Preserve
    this discipline when modifying the backend.
  - Enumeration and camera controls remain DirectShow COM
    (Windows/DirectShowDeviceProvider) — unchanged, and independent of the
    capture engine.


CODEBRIX.WEBCAM ON MACOS
------------------------
How the macOS side of CodeBrix.Webcam works, and the permission model that
governs it (implemented 2026-07-06; the historical handoff rationale lives in
MAC-PORTING-GUIDE.txt at the repo root).

Implementation shape:
  - Device enumeration and camera controls use AVFoundation, called directly
    through the Objective-C runtime (objc_msgSend P/Invoke) from
    src/CodeBrix.Webcam/Internal/Darwin/ — fully managed, no native shim
    dylib, nothing extra packed into the nuget. Enumeration is vendor-
    agnostic: built-in MacBook/iMac cameras, any external USB/Thunderbolt
    camera, and iPhone Continuity Cameras are all discovered.
  - IImagingMediaDevice.Id is the AVCaptureDevice uniqueID — the exact
    identifier libvlc's avcapture:// capture MRL accepts. Audio capture
    (paired microphone) rides on libvlc's qtsound:// input, keyed by the
    audio device's uniqueID.
  - Camera controls are sparser than on Windows/Linux BY DESIGN: AVFoundation
    only exposes the focus / exposure / white-balance MODE selectors, never
    UVC processing controls (brightness, contrast, zoom, ...). Many cameras
    therefore enumerate with few or zero controls on macOS. That is correct
    behavior — do not "fix" it by fabricating control ranges.
  - libvlc loading: application-bundled libraries win, then the loader falls
    back to /Applications/VLC.app and sets VLC_PLUGIN_PATH to the bundle's
    plugin directory (see the CodeBrix additions in
    src/CodeBrix.MediaCore/Core/). The VideoLAN.LibVLC.Mac nuget ships
    x64-only binaries, so installed VLC.app is the practical libvlc source on
    Apple Silicon.
  - Hardware quirk seen in the field: a USB 3 camera attached at USB 2.0
    speed (e.g. through a USB 2.0 hub) may present NO USB audio interface at
    all, so macOS sees no microphone and PairedMicrophone is legitimately
    null. Reconnecting the camera to a USB 3 port restores the mic.

PERMISSIONS (TCC) — the part that surprises everyone:
macOS gates camera and microphone capture behind per-application user
consent ("TCC"). The critical detail for this repo: libvlc's avcapture and
qtsound modules only CHECK the authorization status — they never trigger the
consent prompt — so without help, capture from a not-yet-authorized process
always fails with "access has not been granted by the user", and the user is
never even asked. WebcamSession.Start() therefore requests consent itself
(Internal/Darwin/DarwinCaptureAuthorization.cs): on first use the system
prompt appears, and a denial surfaces as a WebcamException pointing at
System Settings > Privacy & Security > Camera (or > Microphone).

Consequences to keep in mind:
  - Consent attaches to the RESPONSIBLE application. For an app bundle, that
    is the app itself — and a bundled .app MUST declare
    NSCameraUsageDescription (and NSMicrophoneUsageDescription when audio is
    captured) in its Info.plist, or macOS refuses access outright. For a bare
    `dotnet run` / `dotnet test` process, consent attaches to the hosting
    terminal application instead, and no usage-description string is needed.
  - Enumeration (WebcamDevices.GetImagingMediaDeviceListAsync) does NOT
    require consent; only opening a capture session does.
  - Non-interactive contexts (CI runners, ssh sessions, AI-agent shells)
    CANNOT show the consent prompt: the request is denied instantly and the
    TCC status stays "not determined". No amount of retrying helps — a human
    must run live capture once from an interactive session (e.g. Terminal)
    and click Allow; after that, capture works in that context from then on.


TESTING
-------
Test framework: xUnit v3, asserted with SilverAssertions.

    dotnet test CodeBrix.Platform.MediaPlayerCore.slnx

Tests that construct `LibVLC` need the native libvlc library available on
the host. On Windows CI the `VideoLAN.LibVLC.Windows` package is referenced
in the CodeBrix.Platform.MediaPlayerCore.Tests csproj (engine tests); the
CodeBrix.Webcam.Tests project needs NO libvlc on Windows (Media Foundation
backend). On Linux the host must have `libvlc` installed via the system
package manager. Tests that cannot locate libvlc at runtime skip
themselves.

LIVE CAMERA TESTS (opt-in): tests/CodeBrix.Webcam.Tests contains
LiveCameraTests, which open a REAL camera — they need a physical webcam, a
desktop session, and exclusive device access, so they skip unless explicitly
enabled with an environment variable:

    CODEBRIX_WEBCAM_RUN_CAMERA_TESTS=1 dotnet test tests/CodeBrix.Webcam.Tests

On macOS this command MUST be run by a human from an interactive terminal at
least once: the first live capture triggers the TCC camera-consent prompt
(see CODEBRIX.WEBCAM ON MACOS above), and consent is granted to the hosting
terminal application. Runs from non-interactive shells (CI, ssh, AI-agent
tool shells) are denied instantly without any prompt, so the live tests fail
there with a WebcamException about camera access even though the code is
fine. The plain unit/enumeration tests need no consent and run anywhere.


LICENSE
-------
GNU Lesser General Public License, version 2.1 or later. The complete license
text is in LICENSE at the repo root. See THIRD-PARTY-NOTICES.txt for the
upstream LibVLCSharp attribution and source-availability statement.


QUICK REFERENCE
---------------
Install engine:  dotnet add package CodeBrix.MediaCore.LgplLicenseForever
Install UI layer: dotnet add package CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever
                 (depends on, and pulls in, the engine package)
Namespace:       using CodeBrix.Platform.MediaPlayerCore;   (BOTH packages)
Initialize:      Core.Initialize();
Main handle:     var lib = new LibVLC();
Play a file:     new MediaPlayer(new Media(lib, new Uri(...))).Play();
Upstream source: https://code.videolan.org/videolan/LibVLCSharp (tag 3.9.7)
