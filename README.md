# CodeBrix.Platform.MediaPlayerCore

A fully managed, cross-platform audio / video media-player library for .NET, providing a comprehensive multimedia API that can render video, output audio, and control playback across Windows, Linux, and macOS desktops.

CodeBrix.Platform.MediaPlayerCore is a .NET 10 drop-in-compatible port of the `LibVLCSharp` NuGet package (version 3.9.7) — the official .NET wrapper around VideoLAN's LibVLC library. CodeBrix.Platform.MediaPlayerCore has no managed NuGet dependencies of its own; it requires the native `libvlc` library to be installed or referenced via one of the official `VideoLAN.LibVLC.*` runtime packages (`VideoLAN.LibVLC.Windows`, `VideoLAN.LibVLC.Linux`, etc.).

CodeBrix.Platform.MediaPlayerCore is provided as a .NET 10 library and associated `CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever` NuGet package.

CodeBrix.Platform.MediaPlayerCore supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## CodeBrix.Platform.MediaPlayerCore supports:

* Play every media file format, every codec, and every streaming protocol supported by libvlc
* Hardware-accelerated decoding up to 8K
* Network browsing for distant filesystems (SMB, FTP, SFTP, NFS) and servers (UPnP, DLNA)
* Playback of Audio CD, DVD, and Bluray with menu navigation
* HDR, including tonemapping for SDR streams
* Audio passthrough with SPDIF and HDMI, including HD codecs (DD+, TrueHD, DTS-HD)
* Video and audio filters
* 360 video and 3D audio playback, including Ambisonics
* Casting and streaming to distant renderers (Chromecast and UPnP renderers)
* A `MediaPlayerElement`-style management layer for building playback UIs

## Sample Code

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

### Enumerate audio output devices

```csharp
using CodeBrix.Platform.MediaPlayerCore;

using var libVLC = new LibVLC();

foreach (var device in libVLC.AudioOutputDevices())
{
    Console.WriteLine($"{device.Description} ({device.DeviceIdentifier})");
}
```

## License

Copyright (C) VideoLAN and the LibVLCSharp authors.
Copyright (c) 2026 Jeremy Ellis and contributors.

CodeBrix.Platform.MediaPlayerCore is licensed under the **GNU Lesser General
Public License, version 2.1 or later** (LGPL-2.1-or-later). See
https://en.wikipedia.org/wiki/GNU_Lesser_General_Public_License for an
overview. The full, verbatim LGPL-2.1 text is in `LICENSE` at the root of
this repository and ships inside the NuGet package.

This library is a port of LibVLCSharp version 3.9.7
(https://code.videolan.org/videolan/LibVLCSharp), originally authored by
VideoLAN and distributed under the LGPL-2.1-or-later license. All upstream
copyright and license notices are preserved in the ported source files.
Per-file modification notices dated 2026-04-18 appear above every ported
source file. See `THIRD-PARTY-NOTICES.txt` for the upstream attribution,
the source-availability statement, and the MIT notice covering the bundled
FontAwesome icon-codepoint file.

### Notes for consumers

CodeBrix.Platform.MediaPlayerCore is distributed as a standalone
`CodeBrix.Platform.MediaPlayerCore.dll` inside a NuGet package and is
intended to be consumed via `<PackageReference>`. This satisfies LGPL-2.1
§6 (relinkability): end users of your application can replace
`CodeBrix.Platform.MediaPlayerCore.dll` in your bin folder with a modified,
interface-compatible version at any time. Do not static-link
CodeBrix.Platform.MediaPlayerCore into your host assembly (e.g. via
ILMerge / ILRepack); doing so forfeits LGPL-2.1 §6 eligibility and extends
the library's copyleft to the combined work.

Consumers of CodeBrix.Platform.MediaPlayerCore are free to license their
own code under any license they choose, provided they (a) preserve this
notice, (b) allow end users to substitute a modified version of
`CodeBrix.Platform.MediaPlayerCore.dll`, and (c) include the LGPL-2.1
license text in their distribution (the `LICENSE` file ships inside the
NuGet package for this purpose).

### Trademark and non-affiliation

CodeBrix.Platform.MediaPlayerCore is an independent fork and is not
endorsed by, sponsored by, or affiliated with VideoLAN or the LibVLCSharp
project. "VideoLAN", "VLC", and "LibVLC" are trademarks of VideoLAN.
The native `libvlc` library is distributed separately by VideoLAN; consult
https://www.videolan.org/ for its terms (which include codec and patent
considerations not covered by this notice).
