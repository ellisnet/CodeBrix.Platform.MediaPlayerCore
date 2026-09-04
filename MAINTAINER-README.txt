================================================================================
MAINTAINER-README: CodeBrix.Platform.MediaPlayerCore
Notes for people and agents MAINTAINING this repository — not for package consumers
================================================================================


PURPOSE AND SCOPE
=================
This repository produces THREE NuGet packages, always built and shipped
together at (effectively) the same version:

  CodeBrix.MediaCore.LgplLicenseForever
      The media ENGINE: the libvlc P/Invoke binding — LibVLC, Media,
      MediaPlayer, MediaList, VideoFrameSink, VideoFrameSource,
      MediaDiscoverer, RendererDiscoverer, Equalizer, Dialog, event args,
      structures, marshalling helpers and the Core native-library loader.
      Project:      src/CodeBrix.MediaCore/
      AGENT-README: AGENT-README.txt (repository root)

  CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever
      IVideoView + MediaPosition (public) and the internal
      MediaPlayerElement management layer. ProjectReference to
      CodeBrix.MediaCore, which packs as a same-version dependency.
      Project:      src/CodeBrix.Platform.MediaPlayerCore/
      AGENT-README: src/CodeBrix.Platform.MediaPlayerCore/AGENT-README.txt

  CodeBrix.Webcam.LgplLicenseForever
      Webcam capture (original CodeBrix code). ProjectReference to
      CodeBrix.MediaCore; public API never exposes an engine type.
      Project:      src/CodeBrix.Webcam/
      AGENT-README: src/CodeBrix.Webcam/AGENT-README.txt

Each AGENT-README documents exactly one package for consumers. This file
holds everything a consumer does not need: build, test, pack, versioning,
provenance, architecture and conventions.


REPOSITORY LAYOUT
=================
    CodeBrix.Platform.MediaPlayerCore.slnx   solution: 3 library projects,
                                             2 test projects, and a Solution
                                             Items folder carrying
                                             .gitignore, AGENT-README.txt,
                                             EXTRAS-README.txt, global.json,
                                             icon-codebrix-128.png, LICENSE,
                                             MAINTAINER-README.txt,
                                             README-INDEX.txt, README.md and
                                             THIRD-PARTY-NOTICES.txt (plus the
                                             stale MAC-PORTING-GUIDE.txt entry
                                             noted below)
    AGENT-README.txt                         consumer docs: CodeBrix.MediaCore
    MAINTAINER-README.txt                    this file
    EXTRAS-README.txt                        samples/tools description
    README-INDEX.txt                         map of the README files
    README.md                                GitHub / nuget.org overview
    LICENSE                                  LGPL-2.1 text (packed in all three)
    THIRD-PARTY-NOTICES.txt                  upstream attribution (packed)
    icon-codebrix-128.png                    package icon (packed)
    global.json                              selects the
                                             Microsoft.Testing.Platform test
                                             runner; pins no SDK version
    AGENTS.md, CLAUDE.md, .clinerules, .cursorrules, .windsurfrules,
    .cursor/rules/agent-readme.mdc, .github/copilot-instructions.md,
    .junie/guidelines.md                     AI pointer stubs -> AGENT-README

    src/CodeBrix.MediaCore/                  == the ENGINE package ==
        Core/                    Constants, Core (loader; EnsureVersionsMatch),
                                 Core.Desktop (Initialize; Linux soname
                                 resolver libvlc -> libvlc.so.5; macOS
                                 VLC.app fallback + VLC_PLUGIN_PATH)
        Events/                  EventManager base, MediaEventManager,
                                 MediaPlayerEventManager, MediaListEventManager,
                                 MediaDiscovererEventManager,
                                 RendererDiscovererEventManager,
                                 MediaPlayerChanged/ChangingEventArgs,
                                 VideoFrameReady/FormatChangedEventArgs
        Helpers/                 MarshalExtensions, MarshalUtils, PlatformHelper
        Structures/              AudioOutputDescription, AudioOutputDevice,
                                 ChapterDescription, MediaDiscovererDescription,
                                 MediaSlave, MediaStats, MediaTrack (+Audio/
                                 Video/SubtitleTrack), MediaTrackData,
                                 ModuleDescription, RendererDescription,
                                 TrackDescription, VideoViewpoint
        Dialog.cs, Equalizer.cs, Internal.cs, InternalsVisibleTo.cs,
        LibVLC.cs, LibVLCEvents.cs (native event structs + all the
        upstream event-args classes), MediaConfiguration.cs, Media.cs,
        MediaDiscoverer.cs, MediaInput.cs, MediaList.cs, MediaPlayer.cs,
        RendererDiscoverer.cs (+ RendererItem), StreamMediaInput.cs,
        VideoFrameSink.cs (CodeBrix addition: vmem -> FrameReady),
        VideoFrameSource.cs (CodeBrix addition: imem <- PushFrame),
        VLCException.cs

        NAMESPACES: only CodeBrix.Platform.MediaPlayerCore (37 files),
        .Helpers (2) and .Structures (5). The Events/ and Core/ folders
        do NOT introduce sub-namespaces, and most Structures/ files are in
        the root namespace. Keep it that way; consumers depend on it.

    src/CodeBrix.Platform.MediaPlayerCore/   == the seam package ==
        IVideoView.cs            public
        IVideoControl.cs         internal (IVideoView + SizeChanged/Width/Height)
        MediaPlayerElement/      MediaPosition (public); everything else
                                 internal: MediaPlayerElementManagerBase,
                                 MediaPlayerElementManager, AspectRatio(+Manager),
                                 AudioTracksManager, AutoHideNotifier,
                                 BufferingProgressNotifier,
                                 CastRenderersDiscoverer, DeviceAwakeningManager,
                                 IDispatcher, IDisplayInformation,
                                 IDisplayRequest, SeekBarManager, StateManager,
                                 SubtitlesTracksManager, TimeSpanExtensions,
                                 TracksManager, VideoTracksManager,
                                 VolumeManager, FontAwesomeIcons (namespace
                                 FontAwesome; MIT, see THIRD-PARTY-NOTICES)
        InternalsVisibleTo.cs    -> CodeBrix.Platform.MediaPlayerCore.Tests

    src/CodeBrix.Webcam/                     == the webcam package ==
        WebcamDevices.cs, WebcamSession.cs, WebcamSessionOptions.cs,
        AudioCaptureMode.cs, WebcamException.cs, InternalsVisibleTo.cs
        Devices/                 IImagingMediaDevice, IImagingDeviceControl,
                                 ImagingMediaCapability, ImagingPixelFormat,
                                 ImagingDeviceHardwareInfo, ImagingAudioPairing,
                                 ImagingDeviceControlKind, ImagingDeviceControlType
        Capture/                 WebcamFrameEventArgs, WebcamPhoto, WebcamOverlay,
                                 WebcamVideoFormat, WebcamRecordingOptions,
                                 WebcamRecordingResult
        Internal/                the ICaptureBackend seam (WebcamSession is
                                 backend-agnostic: overlay tee, photos,
                                 FrameReceived, locking, latest-frame cache):
                                 CaptureBackendFactory, CaptureFrameEventArgs,
                                 CaptureMediaFactory, ImagingMediaDevice,
                                 OverlayCompositor (shared), WebcamEngine,
                                 IAudioSidecar, IFramePathRecorder,
                                 AudioSidecarRecorder, LibVlcCaptureBackend
                                 (Linux/macOS: libvlc player + VideoFrameSink +
                                 sout recording), LibVlcFramePathRecorder
                                 (VideoFrameSource-based overlay recording)
            Linux/               V4l2DeviceProvider, V4l2DeviceControl,
                                 V4l2NativeMethods (libc ioctls)
            Windows/             DirectShowDeviceProvider/Control/NativeMethods
                                 (enumeration + controls, COM),
                                 MediaFoundationCaptureBackend + NativeMethods
                                 (IMFSourceReader capture), MfSinkWriterRecorder
                                 (MP4/H.264 + AAC), MfFramePathRecorder,
                                 WasapiMicrophoneCapture, WasapiAudioMonitor,
                                 WasapiNativeMethods, WavSidecarRecorder,
                                 MtaThread
            Darwin/              DarwinDeviceProvider, DarwinDeviceControl,
                                 DarwinDeviceInfoParser, DarwinNativeMethods
                                 (objc_msgSend P/Invoke; no shim dylib),
                                 DarwinCaptureAuthorization (TCC consent)

    tests/CodeBrix.Platform.MediaPlayerCore.Tests/   engine + seam tests
    tests/CodeBrix.Webcam.Tests/                      webcam tests
    samples/WebcamViewer/                             see EXTRAS-README.txt


BUILDING
========
    dotnet build CodeBrix.Platform.MediaPlayerCore.slnx

  - All projects target net10.0 only; no multi-targeting.
  - All three library projects set AllowUnsafeBlocks=true and
    GenerateDocumentationFile=true (CS1591 must be fixed at source).
  - GeneratePackageOnBuild=true on every library project: every build
    writes a fresh .nupkg into bin/<Configuration>/ with a new
    date-stamped version (see PACKAGING AND PUBLISHING).
  - Building needs no native libvlc; only running/testing does.
  - The slnx "Solution Items" folder still lists MAC-PORTING-GUIDE.txt,
    which no longer exists in the repository (the macOS porting handoff
    was completed and the guide removed). Remove the entry or restore the
    file; it is harmless to the build.


TESTING
=======
Test framework: xUnit v3, asserted with SilverAssertions. Test projects
are net10.0 with AllowUnsafeBlocks. Neither test project uses a coverage
collector.

    dotnet test CodeBrix.Platform.MediaPlayerCore.slnx

THE TEST RUNNER IS Microsoft.Testing.Platform, selected by global.json at
the repository root. That file does NOT pin an SDK version, so the newest
installed .NET 10 SDK is still used; it exists solely to select the
runner:

    { "test": { "runner": "Microsoft.Testing.Platform" } }

Because the setting lives in global.json rather than in the test csprojs,
it applies to every `dotnet test` run anywhere in the repository. Keep the
file committed -- without it `dotnet test` silently falls back to the
older VSTest bridge.

Native libvlc for the engine tests:
  - Tests that construct LibVLC need native libvlc on the host. The
    CodeBrix.Platform.MediaPlayerCore.Tests csproj conditionally references
    VideoLAN.LibVLC.Windows on Windows and VideoLAN.LibVLC.Mac on macOS
    (the Mac package is x64-only; on Apple Silicon install VLC.app
    instead). On Linux install `libvlc5 vlc-plugin-base`. Tests that
    cannot locate libvlc at runtime skip themselves (CoreLoadingTests
    covers the failure path).
  - BaseSetup creates the shared fixture as
    `new LibVLC("--no-audio", "--no-video")`; tests that need video
    output (VideoFrameSinkTests) create their own LibVLC("--no-audio").
  - Fixture media in the test project: sample.mp3, motörhead.mp3 (the
    special-character path case), sample.mp4 (has a video track; used by
    VideoFrameSinkTests). All three are CopyToOutputDirectory=PreserveNewest
    and located via Assembly.Location at runtime.
  - libvlc events are awaited through TaskCompletionSource created with
    RunContinuationsAsynchronously (BaseSetup.AwaitMediaEventAsync, hard
    15 s timeout) — inline continuations would run Stop() on a libvlc
    thread and deadlock. Keep that pattern in new tests.
  - BaseSetup.IsWindows gates tests that need the `mmdevice` audio output
    (Windows-only libvlc build):
        [Fact(SkipUnless = nameof(IsWindows), SkipType = typeof(BaseSetup))]

Opt-in playback tests (engine):
  Tests that need a real audio/video output or LAN discovery skip unless
        MEDIAPLAYERCORE_RUN_PLAYBACK_TESTS=1 dotnet test tests/CodeBrix.Platform.MediaPlayerCore.Tests
  (gate: BaseSetup.CanRunMediaPlaybackTests; apply with
   [Fact(Skip = "...", SkipUnless = nameof(BaseSetup.CanRunMediaPlaybackTests),
          SkipType = typeof(BaseSetup))]).

Webcam tests:
  - CodeBrix.Webcam.Tests needs NO libvlc on Windows (Media Foundation
    backend); it references VideoLAN.LibVLC.Mac on macOS only.
  - PublicApiLeakTests reflects over every exported signature of
    CodeBrix.Webcam and fails if a CodeBrix.Platform.MediaPlayerCore type
    appears. If it fails, fix the API — never the test.
  - LIVE CAMERA TESTS (opt-in): LiveCameraTests open a REAL camera — they
    need a physical webcam, a desktop session and exclusive device access,
    so they skip unless
        CODEBRIX_WEBCAM_RUN_CAMERA_TESTS=1 dotnet test tests/CodeBrix.Webcam.Tests
    ("true" also works). The four tests: at least one device found; live
    frames + photo; latest-frame cache + mirrored photo; overlay recording
    to MP4 via the frame path.
  - On macOS that command MUST be run by a human from an interactive
    terminal at least once: the first live capture triggers the TCC
    camera-consent prompt, and consent is granted to the hosting terminal
    application. Runs from non-interactive shells (CI, ssh, AI-agent tool
    shells) are denied instantly without a prompt, so the live tests fail
    there with a WebcamException about camera access even though the code
    is fine. The plain unit/enumeration tests need no consent and run
    anywhere.
  - DarwinDeviceInfoParserTests and MediaFoundationDeviceMatchTests test
    the platform parsers with canned data and run on any host.


PACKAGING AND PUBLISHING
========================
Pack driver: GeneratePackageOnBuild=true in each library csproj; a plain
`dotnet build` (or `dotnet pack`) of the solution produces the three
.nupkg files. PackageRequireLicenseAcceptance=true on all three.

What ships in each nupkg (None/Pack items in the csproj):
  CodeBrix.MediaCore                    icon-codebrix-128.png, README.md,
                                        AGENT-README.txt (REPO ROOT),
                                        THIRD-PARTY-NOTICES.txt, LICENSE
  CodeBrix.Platform.MediaPlayerCore     icon, README.md,
                                        src/CodeBrix.Platform.MediaPlayerCore/AGENT-README.txt,
                                        THIRD-PARTY-NOTICES.txt, LICENSE
  CodeBrix.Webcam                       icon, README.md,
                                        src/CodeBrix.Webcam/AGENT-README.txt,
                                        THIRD-PARTY-NOTICES.txt, LICENSE
README.md is the PackageReadmeFile for all three — it is the shared,
human-facing overview; per-package consumer documentation is the
AGENT-README each project packs.

VERSIONING — DELIBERATE EXCEPTION TO THE CODEBRIX FAMILY SCHEME:
  - The family standard is 1.<years since base>.<day of year>.<minute of
    day> (all UTC; strictly increasing; a new version on every build; two
    builds in the same UTC minute collide — never publish two from one
    minute). This repo uses the same date-stamp fields but pins MAJOR to
    3, i.e. 3.x.y.z, because Core.EnsureVersionsMatch() reads the executing
    assembly's Major and throws VLCException when it differs from the
    native libvlc major. The wrapped libvlc is 3.x, so MAJOR is 3. Bumping
    MAJOR to anything else makes every LibVLC construction throw.
  - Only bump MAJOR when the library is re-ported from a future upstream
    that targets a newer libvlc major (then 4.0.x, and update the
    rationale in THIRD-PARTY-NOTICES.txt).
  - ALL THREE packages share the 3.x.y.z scheme and ship together;
    CodeBrix.Platform.MediaPlayerCore and CodeBrix.Webcam pack their
    ProjectReference as a dependency on CodeBrix.MediaCore at the same
    build version. Publish the three together, from one build.
  - The "standard" version-explainer comment inside each csproj still
    says "major is always 1"; that text is unmodified boilerplate and is
    overridden by the 3. prefix in <BuildVersion>. The repo-compliance
    tooling knows this repo as the 3.x.y.z exception.
  - The repo-root AGENT-README must never carry version numbers; the
    version facts live here.


PROVENANCE AND VENDORED SOURCES
===============================
CodeBrix.MediaCore and CodeBrix.Platform.MediaPlayerCore are a port of
LibVLCSharp 3.9.7 (tag 3.9.7, commit
6f25c13272c43325d642c1217db077c3efd4c7e5; https://code.videolan.org/videolan/LibVLCSharp),
restricted to src/LibVLCSharp/Shared/**/*.cs and narrowed to net10.0.
THIRD-PARTY-NOTICES.txt is the authoritative attribution; the
modification categories applied during the port were:
  - namespaces LibVLCSharp.Shared.* -> CodeBrix.Platform.MediaPlayerCore.*,
    with a `//was previously: <upstream.namespace>;` comment on every
    ported file's namespace line and a "Ported from LibVLCSharp 3.9.7 by
    Jeremy Ellis on 2026-04-18" header; upstream copyright/license headers
    preserved verbatim;
  - conditional-compile branches that can never execute on net10.0
    stripped, live branch kept;
  - platform-specific Core.Android/Apple/UWP files, Platforms/**,
    Themes/Generic.xaml and Properties/AssemblyInfo.cs omitted; only
    Core.Desktop.cs retained;
  - the engine/seam split into two projects (2026-07-06) moved files
    verbatim — no namespace or content changes.
  NOTE: THIRD-PARTY-NOTICES says nullable annotations were stripped, but
  many ported files still carry `#nullable enable annotations` and `?`
  on reference types (IVideoView.cs, LibVLC.cs, Media.cs, MediaPlayer.cs,
  Dialog.cs, ...). The AGENT-READMEs transcribe signatures as they are.
  Do not add `<Nullable>enable</Nullable>` to the csproj; the per-file
  pragma is the accepted state for ported files.

CodeBrix additions to the engine (not in upstream): VideoFrameSink.cs,
VideoFrameSource.cs, Events/VideoFrameReadyEventArgs.cs,
Events/VideoFrameFormatChangedEventArgs.cs, the Linux soname resolver and
the macOS VLC.app fallback in Core/Core.Desktop.cs + Core/Constants.cs.

FontAwesomeIcons.cs (namespace FontAwesome, MIT, generated by fa2cs —
https://github.com/matthewrdev/fa2cs/) was already bundled by upstream;
its MIT header must travel with the source.

CodeBrix.Webcam contains no ported upstream source; it is original
CodeBrix code under the same LGPL-2.1-or-later terms for family
consistency. Its Windows Media Foundation backend was implemented
2026-07-06, replacing libvlc for Windows capture; the macOS AVFoundation
path was implemented 2026-07-06 (the historical handoff rationale lived in
MAC-PORTING-GUIDE.txt, now removed).

Native libvlc is not bundled: VideoLAN.LibVLC.Windows / VideoLAN.LibVLC.Mac
NuGet packages or system packages supply it (see THIRD-PARTY-NOTICES §3).


CODING CONVENTIONS
==================
Family-wide (enforced on every PR):
  - Target framework net10.0 only. No multi-targeting.
  - Nullable-reference-types OFF at the family level: do NOT add
    `<Nullable>enable</Nullable>`; do NOT write `?` on reference types or
    `!` in NEW code. Value-type nullables (int?, enum?) are fine. (Ported
    upstream files that carry `#nullable enable annotations` are the
    documented exception above.)
  - Implicit global usings OFF: explicit usings at the top of every file,
    System.* first, then others, alphabetical within each group.
  - File-scoped namespaces only.
  - GenerateDocumentationFile is on: every public (and protected on
    unsealed) member needs an XML doc comment. Never add NoWarn 1591.
  - Tests: xUnit v3 + SilverAssertions; no NUnit, MSTest or
    FluentAssertions. Test files <Class>Tests.cs; snake_case test names;
    //Arrange //Act //Assert comments.
  - Ported files keep the `//was previously:` provenance comment on the
    namespace line and the upstream header verbatim.
  - Forbidden csproj properties (never add, even for a quick fix):
    <Nullable>enable</Nullable>, <ImplicitUsings>enable</ImplicitUsings>,
    <NoWarn>...</NoWarn>, <WarningLevel>0</WarningLevel>,
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>,
    <LangVersion>...</LangVersion>.

Repo-specific rules — DO NOT "FIX":
  - NAMESPACE MISMATCH: every type in the CodeBrix.MediaCore assembly
    keeps its CodeBrix.Platform.MediaPlayerCore namespace. RootNamespace
    in CodeBrix.MediaCore.csproj is set accordingly. Renaming to
    CodeBrix.MediaCore.* is a breaking change for the CodeBrix.Platform
    MediaPlayer add-in and is not allowed.
  - NO-LEAK RULE: CodeBrix.Webcam's public API must never expose a
    CodeBrix.Platform.MediaPlayerCore.* type in any signature position.
    PublicApiLeakTests enforces it by reflection; fix the API, never the
    test. Engine exceptions are wrapped in WebcamException.
  - The MediaPlayerElement managers and IVideoControl are internal on
    purpose; InternalsVisibleTo is for the test project only. Do not make
    them public "to be helpful" — the consumer contract is IVideoView +
    MediaPosition, documented as such.
  - Windows backend threading: MF/WASAPI objects are not apartment-agile.
    The source reader lives entirely on its capture thread, and every
    COM-touching control call routes through Internal/Windows/MtaThread
    (desktop UI threads are STA and would fail with E_NOINTERFACE).
    Preserve this discipline when modifying the backend.
  - Keep the Windows webcam path libvlc-free (and GPL-plugin-free): do not
    "fix" the MjpegAvi-on-Windows limitation by re-introducing libvlc.
  - macOS camera controls are sparse by design (AVFoundation only exposes
    mode selectors); do not fabricate control ranges.
  - VideoFrameSink/VideoFrameSource event handlers are wrapped in
    try/catch that traces and swallows, because an exception escaping
    into native libvlc code crashes the process. Keep that.


NOTES
=====
  - The CodeBrix.Platform MediaPlayer add-in
    (https://github.com/ellisnet/CodeBrix.Platform/tree/main/src/AddIns/Platform.UI.MediaPlayer.Skia)
    is the consumer the namespace rule protects; check it compiles after
    any public-surface change to the engine or the seam package.
  - AGENT-README.txt (root) documents the engine package only and is
    packed by CodeBrix.MediaCore.csproj; the other two csproj files pack
    the AGENT-README.txt beside them. Keep all three current when public
    surfaces change; never write version numbers into them.
  - README.md is shared by all three packages on nuget.org and states the
    native-libvlc story for each platform; keep it consistent with the
    INSTALLATION sections of the three AGENT-READMEs.
  - samples/WebcamViewer references the CodeBrix.Webcam project directly
    (ProjectReference), so it builds against the working tree, not the
    published package. See EXTRAS-README.txt.
