# CodeBrix.Platform.MediaPlayerCore

A fully managed, cross-platform audio / video media library family for .NET, providing a comprehensive multimedia API that can render video, output audio, capture from a webcam, and control playback across Windows, Linux, and macOS desktops.

The heart of this repository is a .NET 10 managed binding to the native `libvlc` engine, narrowed to the cross-platform managed core. It produces three NuGet packages:

| Package | Contents |
| --- | --- |
| `CodeBrix.MediaCore.LgplLicenseForever` | The media engine: the libvlc binding itself — `LibVLC`, `Media`, `MediaPlayer`, `VideoFrameSink`, `VideoFrameSource`, media/renderer discoverers, equalizer, and dialogs. No managed NuGet dependencies of its own. |
| `CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever` | The `MediaPlayerElement`-style management layer and video-view interfaces for building playback UIs. Brings in `CodeBrix.MediaCore.LgplLicenseForever` automatically. |
| `CodeBrix.Webcam.LgplLicenseForever` | Webcam capture: rich async device enumeration (resolutions, frame rates, formats, camera controls, paired microphones), live BGRA preview frames for any UI stack, frame-photo capture, MP4/H.264 and MJPEG-passthrough recording, and live-updatable transparent overlay burn-in. Brings in `CodeBrix.MediaCore.LgplLicenseForever` automatically — but consumers only ever use `CodeBrix.Webcam.*` types. |

All of these packages support applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Installation

```
dotnet add package CodeBrix.MediaCore.LgplLicenseForever
dotnet add package CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever
dotnet add package CodeBrix.Webcam.LgplLicenseForever
```

**Which one do I reference?**

* Playing media, or rendering video frames into your own surface — reference `CodeBrix.MediaCore.LgplLicenseForever` alone.
* Building a player UI (state, seek bar, volume, track selection) — reference `CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever`; the engine comes with it.
* Capturing from a webcam — reference `CodeBrix.Webcam.LgplLicenseForever`; the engine comes with it too, but you never touch an engine type.

Note that the NuGet package IDs, the assembly names and the namespaces are not all the same:

* `CodeBrix.MediaCore.LgplLicenseForever` → assembly `CodeBrix.MediaCore`, namespace `CodeBrix.Platform.MediaPlayerCore` - i.e. `using CodeBrix.Platform.MediaPlayerCore;`
* `CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever` → assembly and namespace `CodeBrix.Platform.MediaPlayerCore` - i.e. `using CodeBrix.Platform.MediaPlayerCore;`
* `CodeBrix.Webcam.LgplLicenseForever` → assembly and namespace `CodeBrix.Webcam` - i.e. `using CodeBrix.Webcam;`

**Namespace note:** the `CodeBrix.MediaCore` assembly deliberately carries the `CodeBrix.Platform.MediaPlayerCore` namespace, so one `using CodeBrix.Platform.MediaPlayerCore;` covers both playback packages. That package-name/namespace mismatch is deliberate and permanent. `CodeBrix.Webcam` types live under `CodeBrix.Webcam.*`, and its public API never exposes an engine type (a reflection test in this repository enforces that permanently).

XML documentation (IntelliSense) ships alongside every assembly. None of these packages bundles the native `libvlc` engine — see **Native runtime** below for what each platform needs.

## CodeBrix.MediaCore supports:

* Play every media file format, every codec, and every streaming protocol supported by libvlc
* Hardware-accelerated decoding up to 8K
* Network browsing for distant filesystems (SMB, FTP, SFTP, NFS) and servers (UPnP, DLNA)
* Playback of Audio CD, DVD, and Bluray with menu navigation
* HDR, including tonemapping for SDR streams
* Audio passthrough with SPDIF and HDMI, including HD codecs (DD+, TrueHD, DTS-HD)
* Video and audio filters
* 360 video and 3D audio playback, including Ambisonics
* Casting and streaming to distant renderers (Chromecast and UPnP renderers)
* Windowing-system-agnostic video output: `VideoFrameSink` hands you decoded BGRA frames in memory

## CodeBrix.Platform.MediaPlayerCore supports:

* A `MediaPlayerElement`-style management layer for building playback UIs on top of the engine: state, seek bar, volume, audio / video / subtitle track selection, aspect ratio, auto-hide, buffering progress, cast-renderer discovery and device-awakening (keep-screen-on) managers
* `IVideoView` / `IVideoControl` interfaces so any UI stack can supply the view surface
* Dispatcher, display-information and display-request abstractions, so the managers stay UI-framework-agnostic

## CodeBrix.Webcam supports:

* Rich async device enumeration: resolutions, frame rates, pixel formats, camera controls and the camera's paired microphone
* Live BGRA preview frames for any UI stack
* Frame-photo capture to tightly packed BGRA, ready to load as an image
* Recording to MP4/H.264, and MJPEG passthrough to AVI (see the Windows note under **Native runtime**)
* Live-updatable transparent overlay burn-in, composited into photos and recordings
* Automatic capture of the camera's paired microphone, forceable off for silent files, with muted-by-default live monitoring

## Native runtime

None of these packages bundles the native `libvlc` engine. The playback packages require it at runtime on every platform; `CodeBrix.Webcam` requires it only on Linux and macOS (on Windows it captures through the built-in Media Foundation engine instead). The mechanism differs per platform:

* **Windows** — `CodeBrix.Webcam` needs no native runtime at all: webcam capture and recording use the operating system's built-in Media Foundation engine, so a webcam-only Windows application references no VideoLAN packages. Only the playback packages (`CodeBrix.MediaCore` / `CodeBrix.Platform.MediaPlayerCore`) require libvlc on Windows — for those, reference the official `VideoLAN.LibVLC.Windows` NuGet package in the *application* project. An installed VLC desktop application is not used on Windows.
* **Linux** — install the runtime libraries via the system package manager, e.g. `sudo apt install libvlc5 vlc-plugin-base` on Debian/Ubuntu (no VideoLAN NuGet runtime package exists for Linux; the desktop `vlc` application and `libvlc-dev` are not needed).
* **macOS** — the VLC media player application (`VLC.app`) **must be installed**: download it from [videolan.org/vlc](https://www.videolan.org/vlc/) and drag it into `/Applications`; the loader finds it (and its plugins) automatically. An application may instead bundle the libvlc dylibs itself. Note the `VideoLAN.LibVLC.Mac` NuGet package ships x64-only binaries, so installing VLC is the practical route on Apple Silicon.

If libvlc cannot be loaded, `Core.Initialize()` throws a `VLCException` listing the paths searched; `CodeBrix.Webcam` wraps that in a `WebcamException` whose message states the per-platform fix. Webcam *device enumeration* works without libvlc on every platform — only opening a capture session on Linux/macOS (and the playback APIs anywhere) requires it. One Windows-specific limitation of the Media Foundation engine: `WebcamVideoFormat.MjpegAvi` passthrough recording is not available there — use the default `Mp4H264` (hardware-accelerated where available).

## Sample Code

The playback samples below need only the `CodeBrix.MediaCore.LgplLicenseForever` package (the `using` directive stays `CodeBrix.Platform.MediaPlayerCore` — see the namespace note above).

### Play a local media file

```csharp
using CodeBrix.Platform.MediaPlayerCore;

Core.Initialize();

using var libVLC = new LibVLC();
using var media = new Media(libVLC, new Uri("file:///path/to/video.mp4"));
using var mediaPlayer = new MediaPlayer(media);

mediaPlayer.Play();

// ... keep the process alive while playback runs ...
```

### Render video frames into memory (windowing-system-agnostic)

```csharp
using CodeBrix.Platform.MediaPlayerCore;

Core.Initialize();

using var libVLC = new LibVLC();
using var mediaPlayer = new MediaPlayer(libVLC);
using var sink = new VideoFrameSink(mediaPlayer); // attach BEFORE Play()

sink.FrameReady += (_, frame) =>
{
    // frame.Plane points at 32-bit BGRA pixels (frame.Width x frame.Height,
    // frame.PitchBytes per scanline). Raised on a libvlc thread; copy the
    // pixels (e.g. into an SKImage or bitmap) before returning.
};

mediaPlayer.Media = new Media(libVLC, new Uri("file:///path/to/video.mp4"));
mediaPlayer.Play();
```

`VideoFrameSink` renders through libvlc's memory output ("vmem") instead of an operating-system window, so it works on platforms where libvlc has no window-embedding API — including Wayland and bare-framebuffer Linux hosts — and requires only libvlc's base plugin set.

### Enumerate audio output devices

```csharp
using CodeBrix.Platform.MediaPlayerCore;

using var libVLC = new LibVLC();

// AudioOutputDevices() takes the NAME of an audio output module, so enumerate
// the modules first and ask each one for the devices it can address.
foreach (var module in libVLC.AudioOutputs)
{
    Console.WriteLine($"{module.Name} - {module.Description}");

    foreach (var device in libVLC.AudioOutputDevices(module.Name))
    {
        Console.WriteLine($"   {device.Description} ({device.DeviceIdentifier})");
    }
}
```

### Capture from a webcam (CodeBrix.Webcam package)

```csharp
using CodeBrix.Webcam;
using CodeBrix.Webcam.Capture;
using CodeBrix.Webcam.Devices;

var devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
foreach (var cam in devices)
{
    Console.WriteLine(cam.FriendlyName);
    foreach (var cap in cam.Capabilities)
    {
        Console.WriteLine($"   {cap}"); // e.g. "MJPG 1920x1080 @ 30 fps"
    }
}

using var session = new WebcamSession(devices[0]);
session.FrameReceived += (_, frame) =>
{
    // BGRA pixels for live preview — copy into your UI stack's bitmap and return fast.
};
session.Start();

WebcamPhoto photo = session.CapturePhoto();     // tightly packed BGRA, ready for
                                                // CodeBrix.Imaging LoadPixelData<Bgra32>

session.StartRecording(new WebcamRecordingOptions("/path/to/clip.mp4"));
// ... later ...
WebcamRecordingResult result = session.StopRecording();
```

Overlay burn-in: hand `session.SetOverlay(...)` a straight-alpha BGRA buffer sized to the video, and it is composited into photos and recordings (live-updatable during frame-path recordings). Audio: the camera's paired microphone is captured automatically when it has one (`AudioCaptureMode.Auto`), can be forced off for silent files, and live monitoring is available but muted by default.

## Documentation

Each NuGet package includes an `AGENT-README.txt` at its root - a complete API reference and usage guide written for AI coding agents. Point your agent at the one inside the package you are consuming:

* `CodeBrix.MediaCore.LgplLicenseForever` - the engine API: `LibVLC`, `Media`, `MediaPlayer`, `VideoFrameSink`, discoverers, equalizer and dialogs.
* `CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever` - the `MediaPlayerElement` management layer and the video-view interfaces.
* `CodeBrix.Webcam.LgplLicenseForever` - device enumeration, capture sessions, photos, recording and overlays.

Additional sample code and usage examples are available in the two test projects:
https://github.com/ellisnet/CodeBrix.Platform.MediaPlayerCore/tree/main/tests/CodeBrix.Platform.MediaPlayerCore.Tests
https://github.com/ellisnet/CodeBrix.Platform.MediaPlayerCore/tree/main/tests/CodeBrix.Webcam.Tests

## License

Copyright (C) VideoLAN and the LibVLCSharp authors.
Copyright (c) 2026 Jeremy Ellis and contributors.

CodeBrix.MediaCore, CodeBrix.Platform.MediaPlayerCore, and CodeBrix.Webcam are licensed under the **GNU Lesser General Public License, version 2.1 or later** (LGPL-2.1-or-later) - see the
[LICENSE](https://github.com/ellisnet/CodeBrix.Platform.MediaPlayerCore/blob/main/LICENSE) file. The full,
verbatim LGPL-2.1 text is in `LICENSE` at the root of this repository and ships inside all three NuGet packages.
All upstream copyright and licence notices are preserved in the source files, and per-file modification notices
appear above every modified source file.

For licensing and provenance information about the open source code included in
these packages, see [THIRD-PARTY-NOTICES.txt](https://github.com/ellisnet/CodeBrix.Platform.MediaPlayerCore/blob/main/THIRD-PARTY-NOTICES.txt) -
it carries the upstream attribution, the source-availability statement, and the MIT notice covering the bundled
FontAwesome icon-codepoint file.

### Notes for consumers

CodeBrix.MediaCore, CodeBrix.Platform.MediaPlayerCore, and CodeBrix.Webcam are
distributed as standalone `CodeBrix.MediaCore.dll`,
`CodeBrix.Platform.MediaPlayerCore.dll`, and `CodeBrix.Webcam.dll` assemblies
inside NuGet packages and are intended to be consumed via
`<PackageReference>`. This satisfies LGPL-2.1 §6 (relinkability): end users
of your application can replace these DLLs in your bin folder with modified,
interface-compatible versions at any time. Do not static-link these
assemblies into your host assembly (e.g. via ILMerge / ILRepack); doing so
forfeits LGPL-2.1 §6 eligibility and extends the library's copyleft to the
combined work.

Consumers of these libraries are free to license their own code under any
license they choose, provided they (a) preserve this notice, (b) allow end
users to substitute modified versions of the DLLs named above, and (c)
include the LGPL-2.1 license text in their distribution (the `LICENSE` file
ships inside each NuGet package for this purpose).

### Trademark and non-affiliation

CodeBrix.MediaCore, CodeBrix.Platform.MediaPlayerCore, and CodeBrix.Webcam
are independent works and are not endorsed by, sponsored by, or affiliated
with VideoLAN or the LibVLCSharp project. "VideoLAN", "VLC", and "LibVLC" are trademarks of
VideoLAN. The native `libvlc` library is distributed separately by VideoLAN;
consult https://www.videolan.org/ for its terms (which include codec and
patent considerations not covered by this notice).
