# CodeBrix.Platform.MediaPlayerCore

A fully managed, cross-platform audio / video media library family for .NET, providing a comprehensive multimedia API that can render video, output audio, and control playback across Windows, Linux, and macOS desktops.

The heart of this repository is a .NET 10 drop-in-compatible port of the `LibVLCSharp` NuGet package (version 3.9.7) — the official .NET wrapper around VideoLAN's LibVLC library — narrowed to the cross-platform managed core. It produces three NuGet packages:

| Package | Contents |
| --- | --- |
| `CodeBrix.MediaCore.LgplLicenseForever` | The media engine: the libvlc binding itself — `LibVLC`, `Media`, `MediaPlayer`, `VideoFrameSink`, `VideoFrameSource`, media/renderer discoverers, equalizer, and dialogs. No managed NuGet dependencies of its own. |
| `CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever` | The `MediaPlayerElement`-style management layer and video-view interfaces for building playback UIs. Depends on `CodeBrix.MediaCore.LgplLicenseForever` (same version). |
| `CodeBrix.Webcam.LgplLicenseForever` | Webcam capture: rich async device enumeration (resolutions, frame rates, formats, camera controls, paired microphones), live BGRA preview frames for any UI stack, frame-photo capture, MP4/H.264 and MJPEG-passthrough recording, and live-updatable transparent overlay burn-in. Depends on `CodeBrix.MediaCore.LgplLicenseForever` (same version) — but consumers only ever use `CodeBrix.Webcam.*` types. |

**Namespace note:** for drop-in compatibility, all types in the first two packages live under the original `CodeBrix.Platform.MediaPlayerCore` namespaces — including the types that ship in the `CodeBrix.MediaCore` assembly. `using CodeBrix.Platform.MediaPlayerCore;` is the right directive for both. This package-name/namespace mismatch is deliberate and permanent. `CodeBrix.Webcam` types live under `CodeBrix.Webcam.*`, and its public API never exposes an engine type (a reflection test in this repository enforces that permanently).

Neither package bundles the native `libvlc` library: it must be installed or referenced via one of the official `VideoLAN.LibVLC.*` runtime packages (e.g. `VideoLAN.LibVLC.Windows`). On Linux there is no VideoLAN NuGet runtime package — install libvlc via the system package manager instead (e.g. `sudo apt install libvlc5 vlc-plugin-base` on Debian/Ubuntu).

Both packages support applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## The CodeBrix.MediaCore engine supports:

* Play every media file format, every codec, and every streaming protocol supported by libvlc
* Hardware-accelerated decoding up to 8K
* Network browsing for distant filesystems (SMB, FTP, SFTP, NFS) and servers (UPnP, DLNA)
* Playback of Audio CD, DVD, and Bluray with menu navigation
* HDR, including tonemapping for SDR streams
* Audio passthrough with SPDIF and HDMI, including HD codecs (DD+, TrueHD, DTS-HD)
* Video and audio filters
* 360 video and 3D audio playback, including Ambisonics
* Casting and streaming to distant renderers (Chromecast and UPnP renderers)

The `CodeBrix.Platform.MediaPlayerCore` package adds a `MediaPlayerElement`-style management layer (state, seek bar, volume, tracks, aspect ratio, auto-hide, buffering-progress, cast-renderers, and device-awakening managers) for building playback UIs on top of the engine.

## Sample Code

All samples below need only the `CodeBrix.MediaCore.LgplLicenseForever` package (the `using` directive stays `CodeBrix.Platform.MediaPlayerCore` — see the namespace note above).

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

`VideoFrameSink` is a CodeBrix addition (not part of the LibVLCSharp 3.9.7 API surface). Because it renders through libvlc's memory output ("vmem") instead of an operating-system window, it works on platforms where libvlc has no window-embedding API — including Wayland and bare-framebuffer Linux hosts — and requires only libvlc's base plugin set.

### Enumerate audio output devices

```csharp
using CodeBrix.Platform.MediaPlayerCore;

using var libVLC = new LibVLC();

foreach (var device in libVLC.AudioOutputDevices())
{
    Console.WriteLine($"{device.Description} ({device.DeviceIdentifier})");
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

## License

Copyright (C) VideoLAN and the LibVLCSharp authors.
Copyright (c) 2026 Jeremy Ellis and contributors.

CodeBrix.MediaCore, CodeBrix.Platform.MediaPlayerCore, and CodeBrix.Webcam are licensed under the **GNU Lesser General Public License, version 2.1 or later** (LGPL-2.1-or-later). See
https://en.wikipedia.org/wiki/GNU_Lesser_General_Public_License for an
overview. The full, verbatim LGPL-2.1 text is in `LICENSE` at the root of
this repository and ships inside all three NuGet packages.

CodeBrix.MediaCore and CodeBrix.Platform.MediaPlayerCore are a port of
LibVLCSharp version 3.9.7
(https://code.videolan.org/videolan/LibVLCSharp), originally authored by
VideoLAN and distributed under the LGPL-2.1-or-later license. All upstream
copyright and license notices are preserved in the ported source files.
Per-file modification notices dated 2026-04-18 appear above every ported
source file. CodeBrix.Webcam is original CodeBrix code (no ported LibVLCSharp
sources), distributed under the same LGPL-2.1-or-later terms for family
consistency. See `THIRD-PARTY-NOTICES.txt` for the upstream attribution,
the source-availability statement, and the MIT notice covering the bundled
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
