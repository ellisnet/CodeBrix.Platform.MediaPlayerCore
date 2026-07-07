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

The library depends on the native libvlc runtime. On Windows install
VideoLAN.LibVLC.Windows via NuGet; on Linux install libvlc via the system
package manager (`sudo apt install libvlc5 vlc-plugin-base` on
Debian/Ubuntu — the library plus base plugins; neither the full `vlc`
desktop application NOR the build-time `libvlc-dev` package is required —
on Linux the loader maps the `libvlc` P/Invoke onto the versioned runtime
soname `libvlc.so.5`, so the runtime package alone suffices); on macOS install
VLC.app or VideoLAN.LibVLC.Mac via NuGet. The library will search the
standard system search paths at runtime; call `Core.Initialize()` before
constructing any LibVLC instance to ensure the native library is loaded.


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
        Internal/                   -- engine glue (WebcamEngine, capture Media
                                       factory, overlay compositor, sidecar WAV
                                       recorder) and per-OS providers:
                                       Linux/ (V4L2 via libc ioctls), Windows/
                                       (DirectShow COM), Darwin/ (stub — see
                                       MAC-PORTING-GUIDE.txt)


TESTING
-------
Test framework: xUnit v3, asserted with SilverAssertions.

    dotnet test CodeBrix.Platform.MediaPlayerCore.slnx

Tests that construct `LibVLC` need the native libvlc library available on
the host. On Windows CI the `VideoLAN.LibVLC.Windows` package is referenced
in the test csproj. On Linux the host must have `libvlc` installed via the
system package manager. Tests that cannot locate libvlc at runtime skip
themselves.


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
