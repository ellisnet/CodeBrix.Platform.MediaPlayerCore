================================================================================
EXTRAS-README: CodeBrix.Platform.MediaPlayerCore
Samples, tools and other content in this repository that is not part of a NuGet package
================================================================================


samples/WebcamViewer — live webcam viewer for every CodeBrix UI stack
=====================================================================
Path:  samples/WebcamViewer/

What it is: a live webcam viewer demonstrating the
CodeBrix.Webcam.LgplLicenseForever package across every UI stack the
CodeBrix family targets, with ONE shared view model and ONE shared Skia
video canvas compiled into each head:

  CodeBrixPlatform/                 CodeBrix.Platform heads
      WebcamViewer.Core/            shared core project: links the Shared/
                                    sources, references CodeBrix.Platform,
                                    CodeBrix.Platform.SkiaSharp.Views
                                    (SKXamlCanvas), CodeBrix.Platform.Fonts.OpenSans,
                                    CodeBrix.Imaging (PNG encoding), and the
                                    CodeBrix.Webcam PROJECT (not the package)
      WebcamViewer.UI/              shared XAML (App.xaml, Views/MainPage.xaml)
                                    as a shared project (.shproj/.projitems)
      WebcamViewer.LinuxX11/        Program.cs + csproj per head
      WebcamViewer.LinuxWayland/
      WebcamViewer.LinuxFrameBuffer/
      WebcamViewer.MacOS/
      WebcamViewer.Win32Skia/
      WebcamViewer.WinWpfSkia/
  WebcamViewer.WinUI/               native WinUI 3 head (SkiaSharp.Views.WinUI,
                                    Windows App SDK, MSIX tooling)
  WebcamViewer.Wpf/                 native WPF head (SkiaSharp.Views.WPF;
                                    EnableWindowsTargeting so it compiles on
                                    Linux/macOS build hosts)
  Shared/
      ViewModels/MainViewModel.cs   camera list, session lifetime, audio
                                    monitor, photo command
      Video/VideoCanvas.cs          `#if` selects the base: SKXamlCanvas for
                                    CodeBrix.Platform/WinUI, SKElement for WPF
      Helpers/HostHelper.cs
  WebcamViewer.slnx                 cross-platform: the CodeBrix.Platform heads
  WebcamViewer.Windows.slnx         the above PLUS the native WinUI 3 and WPF
                                    heads (open on Windows)
  README.md                         the sample's own notes

What it demonstrates:
  - Camera dropdown populated by WebcamDevices.GetImagingMediaDeviceListAsync();
    selecting a camera disposes the old WebcamSession and starts a new one.
  - Live preview: WebcamSession.FrameReceived BGRA frames painted onto a
    SkiaSharp canvas (aspect-fit, black letterbox). The sample predates
    TryCopyLatestFrame and pulls from an app-side cache; the renderer in
    the Webcam AGENT-README (example 2) is the current recommended form.
  - Audio monitoring: a "Monitor audio" checkbox (enabled only when the
    camera has a paired microphone), muted by default.
  - Frame-photos: choose a folder (text box + Browse), click Photo — the
    current frame goes through CodeBrix.Imaging and is saved as
    frame_capture_{yyyyMMdd_HHmmss_fff}.png. The button stays disabled
    until a valid folder is set and frames are flowing.
  - NOT in the sample by design: overlay burn-in and video recording (see
    the Webcam AGENT-README for those).

How to run:
    cd samples/WebcamViewer
    dotnet run --project CodeBrixPlatform/WebcamViewer.LinuxX11        # or
    dotnet run --project CodeBrixPlatform/WebcamViewer.LinuxWayland
    dotnet run --project CodeBrixPlatform/WebcamViewer.LinuxFrameBuffer
    dotnet run --project CodeBrixPlatform/WebcamViewer.MacOS
    dotnet run --project CodeBrixPlatform/WebcamViewer.Win32Skia
    dotnet run --project CodeBrixPlatform/WebcamViewer.WinWpfSkia
    dotnet run --project WebcamViewer.Wpf                                # Windows
    (WebcamViewer.WinUI: open WebcamViewer.Windows.slnx in Visual Studio)

Requirements: Windows needs nothing extra (Media Foundation). Linux needs
the native libvlc runtime (`sudo apt install libvlc5 vlc-plugin-base` on
Debian-based distributions). macOS needs VLC.app installed and will show
the camera-consent prompt on the first WebcamSession.Start(); when run
unbundled (`dotnet run`) consent attaches to the hosting terminal, and a
packaged .app must declare NSCameraUsageDescription (and
NSMicrophoneUsageDescription) in Info.plist. The sample's package
references are version-pinned in its csproj files and are updated
independently of the library packages.


Test fixture media (optional test data)
=======================================
Path:  tests/CodeBrix.Platform.MediaPlayerCore.Tests/

  sample.mp3, motörhead.mp3   audio fixtures (the second exercises a
                              special-character path); referenced by
                              BaseSetup.RealMp3Path / RealMp3PathSpecialCharacter
  sample.mp4                  short clip WITH a video track, used by
                              VideoFrameSinkTests to prove FormatChanged /
                              FrameReady deliver valid BGRA frames

They are copied to the test output folder and located via
Assembly.Location at runtime. They ship in no package.

There are no other tools, demos or scripts in the repository; the tests
folders are the only remaining non-package content.
