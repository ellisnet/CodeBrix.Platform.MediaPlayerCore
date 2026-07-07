# WebcamViewer sample

A live webcam viewer demonstrating the `CodeBrix.Webcam.LgplLicenseForever` library
across every UI stack the CodeBrix family targets:

- **CodeBrix.Platform heads** (under `CodeBrixPlatform/`): Linux X11, Linux Wayland,
  Linux FrameBuffer, macOS, Windows Win32-Skia, and Windows WPF-Skia — one shared
  XAML UI (`WebcamViewer.UI`) and one shared core (`WebcamViewer.Core`).
- **Native WinUI 3** (`WebcamViewer.WinUI`) and **native WPF** (`WebcamViewer.Wpf`).

All heads share the same view model and the same Skia video canvas — single source
files under `Shared/`, compiled into each head, with `#if`/`#else`/`#endif` selecting
the per-stack base types. That is the whole point of the sample: the webcam library is
UI-stack-agnostic, and one screenful of rendering code per stack is enough.

What it shows:

- **Camera dropdown** populated by `WebcamDevices.GetImagingMediaDeviceListAsync()` —
  selecting a camera switches the live session.
- **Live preview**: `WebcamSession.FrameReceived` BGRA frames painted onto a
  SkiaSharp canvas (aspect-fit, black letterbox).
- **Audio monitoring**: a "Monitor audio" checkbox (enabled only when the camera has a
  paired microphone), muted by default.
- **Frame-photos**: choose a folder (text box + Browse button), then click **Photo** —
  the current frame is handed to CodeBrix.Imaging and saved as
  `frame_capture_{yyyyMMdd_HHmmss_fff}.png`. The Photo button stays disabled until a
  valid folder is set and frames are flowing.

Not in this sample (by design): overlay burn-in and video recording — see the
CodeBrix.Webcam API documentation in the repository's AGENT-README.txt for those.

Solutions:

- `WebcamViewer.slnx` — cross-platform: the CodeBrix.Platform heads (open anywhere).
- `WebcamViewer.Windows.slnx` — everything above PLUS the native WinUI 3 and WPF
  heads (open on Windows).

The native `libvlc` runtime must be present (`sudo apt install libvlc5 vlc-plugin-base`
on Debian/Ubuntu). On Windows, all applications that consume the CodeBrix.Webcam
library (or the `CodeBrix.Webcam.LgplLicenseForever` NuGet package) *must* have the
following two package references — every Windows head of this sample carries them:

```xml
<PackageReference Include="VideoLAN.LibVLC.Windows" Version="{latest version}" />
<!--
NOTE:
As of version 3.0.21 (September 2024) of the VideoLAN.LibVLC.Windows Nuget package
referenced above, a critical 'libdshow_plugin.dll' library is no longer included
with the package.  Instead, you also have to have the VideoLAN.LibVLC.Windows.GPL
package referenced below.  Note that this .GPL package carries a GPL-2.0-or-later
license, which likely has implications for the licensing of your application.
-->
<PackageReference Include="VideoLAN.LibVLC.Windows.GPL" Version="{latest version}" />
```

macOS notes (`WebcamViewer.MacOS`):

- The VLC media player application (`VLC.app`) must be installed: download it from
  [videolan.org/vlc](https://www.videolan.org/vlc/) and drag it into
  `/Applications` — the CodeBrix.MediaCore loader falls back to VLC.app's libvlc (and
  points it at the bundle's plugins) automatically. The `VideoLAN.LibVLC.Mac` NuGet
  package ships x64-only binaries, so installing VLC is the practical route on Apple
  Silicon.
- Camera permission: the first `WebcamSession.Start()` asks macOS for camera consent
  (and microphone consent when audio monitoring is used) and the system prompt
  appears. Run unbundled (`dotnet run`), the consent attaches to the hosting terminal
  application. If you package the head as a proper `.app` bundle, its `Info.plist`
  MUST declare `NSCameraUsageDescription` (and `NSMicrophoneUsageDescription`) —
  macOS refuses camera access to a bundled app without them. If consent was denied
  once, re-enable it under System Settings > Privacy & Security > Camera.
