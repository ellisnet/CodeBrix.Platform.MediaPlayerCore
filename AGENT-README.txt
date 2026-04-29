================================================================================
AGENT-README: CodeBrix.Platform.MediaPlayerCore
A Comprehensive Guide for AI Coding Agents
================================================================================
Last updated: 2026-04-20


OVERVIEW
--------
CodeBrix.Platform.MediaPlayerCore is a fully managed, cross-platform audio /
video media-player library for .NET 10. It is a drop-in-compatible port of
the LibVLCSharp NuGet package, version 3.9.7 (the official .NET wrapper
around VideoLAN's LibVLC library), restricted to the cross-platform managed
core. The library wraps the native libvlc dynamic library via P/Invoke and
exposes high-level, managed classes (LibVLC, Media, MediaPlayer,
MediaDiscoverer, RendererDiscoverer, Equalizer, Dialog, and a
MediaPlayerElement-style management layer).

Upstream LibVLCSharp ships a large multi-TFM matrix including platform-
specific views (Android AWindow, Apple UIKit, UWP/WinUI XAML, WPF, WinForms,
MAUI, Avalonia, Eto, GTK, Forms). This library is the NET10.0-only core;
platform-specific view layers are deliberately NOT included and may be
introduced in separate CodeBrix.Platform.* libraries.


INSTALLATION
------------
NuGet Package: CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever
Target framework: .NET 10.0 or higher

To add to a .NET 10+ project:

    dotnet add package CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever

Or in a .csproj file:

    <PackageReference Include="CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever" />

IMPORTANT: The package name is "CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever"
(not "CodeBrix.Platform.MediaPlayerCore"). Always use the full package name when
installing.

The library depends on the native libvlc runtime. On Windows install
VideoLAN.LibVLC.Windows via NuGet; on Linux install libvlc via the system
package manager (e.g. `apt install vlc` on Debian/Ubuntu); on macOS install
VLC.app or VideoLAN.LibVLC.Mac via NuGet. The library will search the
standard system search paths at runtime; call `Core.Initialize()` before
constructing any LibVLC instance to ensure the native library is loaded.


KEY NAMESPACE
-------------
All managed types live under:

    using CodeBrix.Platform.MediaPlayerCore;

Sub-namespaces:

    CodeBrix.Platform.MediaPlayerCore.Core             (native-library loader)
    CodeBrix.Platform.MediaPlayerCore.Events           (event args / managers)
    CodeBrix.Platform.MediaPlayerCore.Helpers          (marshalling helpers)
    CodeBrix.Platform.MediaPlayerCore.MediaPlayerElement  (UI-agnostic
                                                         management layer)
    CodeBrix.Platform.MediaPlayerCore.Structures       (native DTOs /
                                                         descriptions)


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

  MediaDiscoverer / RendererDiscoverer / Equalizer / Dialog
    See the source files under src/CodeBrix.Platform.MediaPlayerCore/ for
    exact signatures. API parity with LibVLCSharp 3.9.7 is the target.


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
The project's managed code mirrors the sub-folder layout of upstream
LibVLCSharp 3.9.7 / src/LibVLCSharp/Shared/, with namespaces adapted to
the CodeBrix family:

    src/CodeBrix.Platform.MediaPlayerCore/
        Core/                       -- libvlc native loader (desktop only)
        Events/                     -- MediaEventManager, MediaPlayerEventManager,
                                       MediaListEventManager, RendererDiscovererEventManager,
                                       EventManager base + strongly-typed event args
        Helpers/                    -- MarshalExtensions, MarshalUtils, PlatformHelper
        MediaPlayerElement/         -- UI-agnostic managers for AspectRatio,
                                       AudioTracks, AutoHide, BufferingProgress,
                                       CastRenderers, DeviceAwakening, MediaPosition,
                                       SeekBar, State, SubtitlesTracks, Volume, etc.
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
        IVideoControl.cs            -- video-control interface
        IVideoView.cs               -- video-view interface
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
        VLCException.cs             -- domain exception


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
Install:         dotnet add package CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever
Namespace:       using CodeBrix.Platform.MediaPlayerCore;
Initialize:      Core.Initialize();
Main handle:     var lib = new LibVLC();
Play a file:     new MediaPlayer(new Media(lib, new Uri(...))).Play();
Upstream source: https://code.videolan.org/videolan/LibVLCSharp (tag 3.9.7)
