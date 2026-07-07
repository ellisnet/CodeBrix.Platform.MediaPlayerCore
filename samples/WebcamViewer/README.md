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
on Debian/Ubuntu; the `VideoLAN.LibVLC.Windows` NuGet package is referenced
automatically on Windows builds via CodeBrix.Webcam's requirements — see the repo README).
