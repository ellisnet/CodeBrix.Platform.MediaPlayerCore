================================================================================
AGENT-README: CodeBrix.Webcam
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Webcam.LgplLicenseForever NuGet package
================================================================================


OVERVIEW
========
CodeBrix.Webcam is a cross-platform webcam capture library for .NET 10 or
later, for Windows, Linux and macOS desktops:

  - rich async device enumeration: every camera with its full
    format x resolution x frame-rate capability matrix, adjustable
    controls (brightness, focus, exposure, zoom, ...), USB hardware ids
    and the microphone physically paired with the camera;
  - a live capture session delivering 32-bit BGRA frames to ANY UI stack
    — pushed through an event or pulled on demand;
  - frame-photo capture (tightly packed BGRA, optionally mirrored);
  - video recording to MP4/H.264 (with an in-file AAC track when a
    microphone is captured) or MJPEG-passthrough AVI;
  - a live-updatable transparent overlay burned into photos and
    recordings (and optionally the preview).

It is ORIGINAL CodeBrix code (no ported upstream sources). Under the hood
it talks to the operating system directly for enumeration and controls
(DirectShow on Windows, V4L2 on Linux, AVFoundation on macOS) and captures
through the built-in Media Foundation engine on Windows or through the
CodeBrix.MediaCore libvlc engine on Linux and macOS.

NO-LEAK RULE: the public API never exposes a CodeBrix.Platform.MediaPlayerCore
type — no parameter, return, property, event, base type or generic
argument. You use only CodeBrix.Webcam.* types and never need a `using`
for the engine; a reflection test in the repository enforces this
permanently.


INSTALLATION
============
    dotnet add package CodeBrix.Webcam.LgplLicenseForever

PackageId:    CodeBrix.Webcam.LgplLicenseForever
Assembly:     CodeBrix.Webcam.dll
Target:       .NET 10 or later
Dependencies: CodeBrix.MediaCore.LgplLicenseForever (pulled in
              automatically, always at the same version as this package —
              never pin the two differently). You do not reference its
              types.
License:      LGPL-2.1-or-later. Consume via <PackageReference>; never
              merge the DLL into your own assembly. LICENSE and
              THIRD-PARTY-NOTICES.txt ship inside the package.

NATIVE RUNTIME REQUIREMENTS (differ per platform — read carefully):

  Windows:  NOTHING to install and NO VideoLAN packages. Capture and
            recording use the operating system's built-in Media
            Foundation engine (MF and WASAPI ship with Windows; Windows
            'N' editions need the Media Feature Pack, and the failure
            message says so). Do not add VideoLAN.LibVLC.Windows for
            webcam work.
  Linux:    the native libvlc runtime must be installed for CAPTURE:
            `sudo apt install libvlc5 vlc-plugin-base` on Debian-based
            distributions (no `vlc` app, no `libvlc-dev` needed).
  macOS:    the VLC media player application must be installed
            (download from https://www.videolan.org/vlc/ and drag VLC
            into /Applications). The engine's loader finds
            /Applications/VLC.app and its plugins automatically. The
            VideoLAN.LibVLC.Mac NuGet package ships x64-only binaries, so
            installing VLC is the practical route on Apple Silicon.

Device ENUMERATION needs none of the above on any platform: the device
providers talk to the OS directly, so WebcamDevices.GetImagingMediaDeviceListAsync
returns full capability data on a machine with no libvlc — and it needs no
camera permission and works headless (safe at startup and in tests). Only
opening a WebcamSession needs the native engine (Linux/macOS) and, on
macOS, camera consent.

When the engine is missing on Linux/macOS, WebcamSession.Start() throws
CodeBrix.Webcam.WebcamException whose message states the per-platform fix
— you never catch an engine exception type.


KEY NAMESPACES / USINGS
=======================
    using CodeBrix.Webcam;           // WebcamDevices, WebcamSession,
                                     // WebcamSessionOptions, AudioCaptureMode,
                                     // WebcamException
    using CodeBrix.Webcam.Devices;   // IImagingMediaDevice and its parts
    using CodeBrix.Webcam.Capture;   // frames, photos, overlays, recording


CORE API REFERENCE
==================
All 19 public types, with signatures transcribed from source.

WebcamDevices (static, CodeBrix.Webcam)
---------------------------------------
    public static Task<IReadOnlyList<IImagingMediaDevice>> GetImagingMediaDeviceListAsync()
        Runs off the calling thread (real device I/O). Empty list when no
        camera is present. Throws PlatformNotSupportedException on
        operating systems other than Windows, Linux and macOS.

IImagingMediaDevice (CodeBrix.Webcam.Devices)
---------------------------------------------
    string Id { get; }
        Stable OS identity — persist this. Linux: the device node
        (/dev/video2); Windows: the DirectShow device path; macOS: the
        AVCaptureDevice uniqueID.
    string FriendlyName { get; }                 // "C922 Pro Stream Webcam"
    ImagingDeviceHardwareInfo Hardware { get; }
    IReadOnlyList<ImagingMediaCapability> Capabilities { get; }
        The full matrix; copy one entry's values into WebcamSessionOptions
        to open exactly that mode.
    IReadOnlyList<IImagingDeviceControl> Controls { get; }
    ImagingAudioPairing PairedMicrophone { get; } // null when the camera
                                                  // has no built-in mic

ImagingMediaCapability (sealed)
    public ImagingMediaCapability(ImagingPixelFormat pixelFormat, string fourCc,
        uint width, uint height, IReadOnlyList<double> frameRates,
        bool isFrameRateRange)
    public ImagingPixelFormat PixelFormat { get; }
    public string FourCc { get; }                // exact code: "MJPG", "YUYV"
    public uint Width { get; }
    public uint Height { get; }
    public IReadOnlyList<double> FrameRates { get; }   // highest first; or the
                                                       // two range endpoints
    public bool IsFrameRateRange { get; }
    public override string ToString()            // "MJPG 1920x1080 @ 30, 24, 15 fps"

ImagingPixelFormat (enum)
    Unknown = 0, Mjpeg, Yuyv, Nv12, H264, Rgb24, Rgb32, Grey
    (formats not named here still enumerate, with PixelFormat = Unknown
    and the real FourCc)

ImagingDeviceHardwareInfo (sealed)
    public ImagingDeviceHardwareInfo(ushort vendorId, ushort productId,
        string serialNumber, string busInfo, string driverName)
    public ushort VendorId { get; }              // 0x046D = Logitech; 0 unknown
    public ushort ProductId { get; }
    public string SerialNumber { get; }          // null when not exposed
    public string BusInfo { get; }               // "usb-0000:00:14.0-12"; null
    public string DriverName { get; }            // "uvcvideo"; null

ImagingAudioPairing (sealed)
    public ImagingAudioPairing(string deviceId, string friendlyName)
    public string DeviceId { get; }              // ALSA "hw:0,0" on Linux;
                                                 // DirectShow audio name on
                                                 // Windows; audio uniqueID on
                                                 // macOS
    public string FriendlyName { get; }

IImagingDeviceControl
    ImagingDeviceControlKind Kind { get; }
    string Name { get; }                         // driver name, "Brightness"
    int RawId { get; }                           // V4L2 CID / DirectShow id
    ImagingDeviceControlType ControlType { get; }
    int Minimum { get; }
    int Maximum { get; }
    int Step { get; }
    int DefaultValue { get; }
    bool SupportsAuto { get; }
    int GetValue();
    void SetValue(int value);                    // pass Minimum..Maximum
    bool GetAuto();                              // only if SupportsAuto
    void SetAuto(bool enabled);
    Platform note: Linux reads/writes controls fully while streaming.
    Windows changes while another component holds the graph are driver-
    dependent and best-effort; between sessions they always work. macOS
    exposes only focus / exposure / white-balance MODE selectors (see
    PLATFORM NOTES).

ImagingDeviceControlKind (enum)
    Unknown = 0, Brightness, Contrast, Saturation, Hue, Gamma, Sharpness,
    Gain, WhiteBalanceTemperature, AutoWhiteBalance, ExposureTime,
    AutoExposure, Focus, AutoFocus, Zoom, Pan, Tilt, BacklightCompensation,
    PowerLineFrequency (0 = off, 1 = 50 Hz, 2 = 60 Hz typically)

ImagingDeviceControlType (enum)
    Integer = 0, Boolean, Menu

WebcamSession : IDisposable (sealed, CodeBrix.Webcam)
-----------------------------------------------------
One live session on one camera. Bound to its device — create a new
session to switch cameras. Control methods are safe from any ONE thread at
a time, but must NOT be called from inside a FrameReceived handler.

    public WebcamSession(IImagingMediaDevice device)
    public WebcamSession(IImagingMediaDevice device, WebcamSessionOptions options)

    public IImagingMediaDevice Device { get; }
    public bool IsRunning { get; }               // between Start and Stop/Dispose
    public bool IsRecording { get; }
    public bool IsAudioCaptureActive { get; }    // a mic was resolved (Auto or
                                                 // SpecificDevice)
    public bool IsOverlayRecordingSupported { get; }
        True when overlay burn-in on RECORDINGS works on this machine
        (photos always can). May probe the engine's in-memory input on
        first call; cached for the process.
    public uint FrameWidth { get; }              // 0 until frames flow
    public uint FrameHeight { get; }
    public bool MonitorAudio { get; set; }       // default false; live mic
                                                 // through the default output
    public int MonitorVolume { get; set; }       // 0..100, default 100;
                                                 // recordings unaffected

    public event EventHandler<WebcamFrameEventArgs> FrameReceived
        Raised on an internal CAPTURE thread for every live frame; the
        pixel buffer is valid only until the handler returns.

    public void Start()
        Opens the camera and starts the stream. Throws WebcamException
        when the camera cannot be opened, capture permission was refused
        (macOS), or the native capture engine is not installed — the
        message states the per-platform fix.
    public void Stop()                           // also stops any recording

    public bool TryCopyLatestFrame(ref byte[] buffer, out int width, out int height)
        Pull model: copies the most recent frame (tightly packed BGRA)
        into `buffer`, reallocating it only when the size differs. Safe
        from any thread. The cache is OFF until the first call, so the
        first call (and any call before the next frame lands) returns
        false = "nothing to show yet".

    public WebcamPhoto CapturePhoto(TimeSpan timeout = default)     // 2 s default
    public WebcamPhoto CapturePhoto(bool mirrorHorizontally, TimeSpan timeout = default)
        Next live frame as a photo, overlay burned in if set. Throws
        WebcamException when not running or no frame arrived in time.

    public void SetOverlay(WebcamOverlay overlay)
        Must match FrameWidth x FrameHeight exactly once frames flow.
        Throws WebcamException on a size mismatch, or while a DIRECT
        recording is in progress (see StartRecording).
    public void ClearOverlay()

    public void StartRecording(WebcamRecordingOptions options)
        Mp4H264 with no overlay involvement = DIRECT pipeline: the backend
        records the camera stream itself, with in-file audio when a mic is
        captured; overlays cannot be introduced until it stops. With an
        overlay set — or options.AllowLiveOverlay = true — = FRAME-PATH
        pipeline: frames flow through managed compositing into the
        encoder without interrupting the preview, and captured audio
        arrives as a sidecar WAV (WebcamRecordingResult.AudioFilePath).
        Throws WebcamException when not running, already recording, the
        format+overlay combination is invalid, or overlay recording is
        unsupported on this machine.
    public WebcamRecordingResult StopRecording()
        Finalizes the file(s). Throws WebcamException when not recording.

    public void Dispose()                        // stops and releases

WebcamSessionOptions (sealed, CodeBrix.Webcam)
----------------------------------------------
Zero/default = "let the camera choose"; copy a capability's values to open
a specific mode.
    public uint RequestedWidth { get; set; }
    public uint RequestedHeight { get; set; }
    public double RequestedFrameRate { get; set; }
    public ImagingPixelFormat PreferredFormat { get; set; }   // Unknown = any;
                                                  // Mjpeg is the usual choice
                                                  // for high res at full fps
    public AudioCaptureMode AudioCapture { get; set; } = AudioCaptureMode.Auto;
    public string AudioDeviceId { get; set; }     // for SpecificDevice: ALSA
                                                  // "hw:1,0" / DirectShow name
    public bool CompositeOverlayOnPreview { get; set; }   // default false:
                                                  // preview stays raw
    public int LiveCachingMs { get; set; } = 100; // lower = snappier preview

AudioCaptureMode (enum, CodeBrix.Webcam)
    Auto = 0          camera's own mic when it has one, else silent video
    Off               never capture audio
    SpecificDevice    the device named by WebcamSessionOptions.AudioDeviceId

WebcamFrameEventArgs : EventArgs (sealed, CodeBrix.Webcam.Capture)
------------------------------------------------------------------
    public uint Width { get; }
    public uint Height { get; }
    public uint PitchBytes { get; }              // bytes per scanline, >= Width*4
    public IntPtr PixelPlane { get; }            // top-left pixel; valid only
                                                 // until the handler returns
    public DateTime TimestampUtc { get; }
    public void CopyTo(byte[] destination)       // tightly packed BGRA copy;
                                                 // destination >= W*H*4 bytes
    Pixels are BGRA (alpha opaque) — SkiaSharp Bgra8888, WPF Bgra32.

WebcamPhoto (sealed, CodeBrix.Webcam.Capture)
---------------------------------------------
    public byte[] PixelsBgra32 { get; }          // tightly packed, W*H*4
    public int Width { get; }
    public int Height { get; }
    public int StrideBytes { get; }              // always Width*4
    public DateTime CapturedAtUtc { get; }
    public WebcamPhoto FlipHorizontal()          // NEW mirrored photo; original
                                                 // untouched; dims/time kept
    Ready for an image library — e.g. CodeBrix.Imaging's
    Image.LoadPixelData<Bgra32> — for PNG/JPEG encoding.

WebcamOverlay (sealed, CodeBrix.Webcam.Capture)
-----------------------------------------------
    public WebcamOverlay(byte[] pixelsBgra32, int width, int height,
                         int strideBytes = 0)    // 0 = tightly packed
    public int Width { get; }
    public int Height { get; }
    STRAIGHT (non-premultiplied) alpha BGRA, sized exactly to the video.
    The ctor COPIES the pixels, so the source buffer can be reused; an
    instance is immutable and shareable. Throws ArgumentNullException /
    ArgumentOutOfRangeException for null pixels, zero dimensions, a stride
    below Width*4, or a buffer too small for the dimensions and stride.
    Producing the buffer: CodeBrix.Imaging Bgra32 + CopyPixelDataTo is
    straight alpha already; from SkiaSharp render to Bgra8888 and read the
    pixels with SKAlphaType.Unpremul — Skia's default is PREMULTIPLIED and
    blends wrongly here.

WebcamRecordingOptions (sealed, CodeBrix.Webcam.Capture)
--------------------------------------------------------
    public WebcamRecordingOptions(string outputPath)   // directory created
    public string OutputPath { get; }
    public WebcamVideoFormat Format { get; set; }      // default Mp4H264
    public uint VideoBitrateKbps { get; set; } = 4000; // ignored for MJPEG
    public bool AllowLiveOverlay { get; set; }         // force the frame path so
                                                       // overlays can change
                                                       // DURING the recording
                                                       // (Mp4H264 only)

WebcamVideoFormat (enum, CodeBrix.Webcam.Capture)
    Mp4H264 = 0    H.264 in MP4 (default): universal, supports overlay
                   burn-in, in-file AAC audio when a mic is captured and
                   no overlay is in use.
    MjpegAvi       the camera's native MJPEG muxed into AVI, no
                   transcoding, near-zero CPU. Needs an MJPEG-streaming
                   camera; no overlay; no audio track (sidecar instead).
                   LINUX/macOS ONLY: on Windows StartRecording throws a
                   WebcamException directing you to Mp4H264.

WebcamRecordingResult (sealed, CodeBrix.Webcam.Capture)
    public string VideoFilePath { get; }         // has the audio track in the
                                                 // direct MP4 pipeline
    public string AudioFilePath { get; }         // sidecar WAV, or null when
                                                 // audio was off / already muxed
    public TimeSpan? EstimatedAudioOffset { get; }   // audio-start minus first
                                                 // video frame (positive =
                                                 // audio first); null without
                                                 // a sidecar
    public TimeSpan Duration { get; }            // wall clock
    public long FramesRecorded { get; }          // frame-path count; 0 for the
                                                 // direct pipeline
    Combine a sidecar with the video by a stream-copy mux (e.g.
    CodeBrix.VideoProcessing), passing EstimatedAudioOffset if lip-sync
    needs it.

WebcamException : Exception (CodeBrix.Webcam)
    public WebcamException(string message)
    public WebcamException(string message, Exception innerException)
    Thrown for every webcam-specific failure: device cannot be opened,
    pipeline cannot be built, overlay size mismatch, permission refused,
    engine missing, recording misuse.


CONSUMING LIVE FRAMES: PUSH OR PULL
==================================
Two ways — pick per consumer, mix freely:

  1. PUSH — subscribe FrameReceived. Raised on an internal CAPTURE thread;
     the args' Width/Height/PitchBytes/PixelPlane (plus CopyTo(byte[]))
     are valid only until the handler returns. Copy what you need and get
     out fast; never touch UI objects or call session methods from inside
     the handler. Right choice for pipelines that want EVERY frame
     (encoders, vision processing).

  2. PULL — call TryCopyLatestFrame from wherever repainting happens (a UI
     paint handler, a render loop, a poll). It copies the most recent
     frame — tightly packed BGRA, the same pixels a FrameReceived handler
     sees — into your reusable buffer, reallocating it only on size
     changes. Safe from any thread. The internal cache is OFF until the
     first call, so sessions that never pull pay no per-frame copy; the
     first call (and any call before the next frame lands) returns false
     — treat false as "nothing to show yet". Right choice for previews,
     which only ever want the newest frame. A typical page wires:
         session.FrameReceived += (_, _) => canvas.Invalidate();  // pacing
         // paint handler: session.TryCopyLatestFrame(ref _buffer, out w, out h)

Mirrored ("selfie") UX: users expect a live preview to behave like a
mirror, and a captured still to match what they saw. Mirror the preview at
RENDER time (negative-X canvas transform — see the renderer below) and
mirror stills with CapturePhoto(mirrorHorizontally: true) (or
photo.FlipHorizontal() later). Computer-vision results computed on
UNMIRRORED frames must be mirrored too (x' = 1 - x) before they are
compared against anything the user sees.


COMPLETE EXAMPLES
=================

1. Enumerate cameras and open a specific mode
---------------------------------------------
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using CodeBrix.Webcam;
    using CodeBrix.Webcam.Devices;

    var devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
    if (devices.Count == 0) { throw new InvalidOperationException("no camera"); }

    foreach (IImagingMediaDevice cam in devices)
    {
        Console.WriteLine($"{cam.FriendlyName}  [{cam.Id}]  " +
            $"vid={cam.Hardware.VendorId:X4} pid={cam.Hardware.ProductId:X4} " +
            $"mic={cam.PairedMicrophone?.FriendlyName ?? "none"}");
        foreach (ImagingMediaCapability cap in cam.Capabilities)
        {
            Console.WriteLine($"   {cap}");        // "MJPG 1920x1080 @ 30 fps"
        }
        foreach (IImagingDeviceControl c in cam.Controls)
        {
            Console.WriteLine($"   {c.Kind} '{c.Name}' {c.Minimum}..{c.Maximum} " +
                $"step {c.Step} default {c.DefaultValue} auto={c.SupportsAuto}");
        }
    }

    // Pick the highest-resolution MJPEG mode and open exactly that.
    IImagingMediaDevice device = devices[0];
    ImagingMediaCapability best = device.Capabilities
        .Where(c => c.PixelFormat == ImagingPixelFormat.Mjpeg)
        .OrderByDescending(c => c.Width * c.Height)
        .First();
    var options = new WebcamSessionOptions
    {
        RequestedWidth = best.Width,
        RequestedHeight = best.Height,
        RequestedFrameRate = best.FrameRates[0],          // highest first
        PreferredFormat = best.PixelFormat,
        AudioCapture = AudioCaptureMode.Auto,
    };
    using var session = new WebcamSession(device, options);
    session.Start();                                       // may throw WebcamException
    Console.WriteLine($"streaming {session.FrameWidth}x{session.FrameHeight}");

2. Canonical SkiaSharp frame renderer (aspect-fit, centered, optional mirror)
-----------------------------------------------------------------------------
Copy this class into Skia-based apps; it is the same rendering approach the
WebcamViewer sample uses. Create ONE instance per canvas (it caches
buffers; UI-thread use only):

    using System;
    using CodeBrix.Webcam;
    using SkiaSharp;

    public sealed class WebcamFrameRenderer
    {
        private byte[] _frameBuffer;
        private SKBitmap _bitmap;

        public void Render(SKSurface surface, SKImageInfo info, WebcamSession session, bool mirror)
        {
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);
            if (session == null
                || !session.TryCopyLatestFrame(ref _frameBuffer, out int width, out int height)
                || width <= 0 || height <= 0) { return; }

            if (_bitmap == null || _bitmap.Width != width || _bitmap.Height != height)
            {
                _bitmap?.Dispose();
                _bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
            }
            System.Runtime.InteropServices.Marshal.Copy(_frameBuffer, 0, _bitmap.GetPixels(), width * height * 4);

            float scale = Math.Min((float)info.Width / width, (float)info.Height / height);
            float destWidth = width * scale;
            float destHeight = height * scale;
            float destX = (info.Width - destWidth) / 2f;
            float destY = (info.Height - destHeight) / 2f;

            int restoreTo = canvas.Save();
            if (mirror) { canvas.Scale(-1, 1, destX + (destWidth / 2f), 0); }
            canvas.DrawBitmap(_bitmap, new SKRect(destX, destY, destX + destWidth, destY + destHeight),
                new SKSamplingOptions(SKFilterMode.Linear));
            canvas.RestoreToCount(restoreTo);
        }
    }

    Wiring: session.FrameReceived += (_, _) => canvas.Invalidate();
            in the paint handler: renderer.Render(surface, info, session, mirror: true);

3. Feed a vision pipeline (latest-wins)
---------------------------------------
Pull the frame on the capture event and hand it to a worker with latest-
wins dropping, so slow inference never blocks capture:

    session.FrameReceived += (_, _) =>
    {
        if (session.TryCopyLatestFrame(ref _visionBuffer, out int w, out int h))
        {
            tracker.SubmitFrame(_visionBuffer, w, h);   // worker copies + signals; drops stale
        }
    };

4. Photo, mirrored, saved as PNG
--------------------------------
    using CodeBrix.Webcam.Capture;

    WebcamPhoto photo = session.CapturePhoto(mirrorHorizontally: true);
    // photo.PixelsBgra32 is tightly packed BGRA, photo.StrideBytes == Width*4.
    // Hand it to any image library that loads raw Bgra32 pixels, e.g.
    // CodeBrix.Imaging: Image.LoadPixelData<Bgra32>(photo.PixelsBgra32,
    //                   photo.Width, photo.Height) then save as PNG.
    File.WriteAllBytes("frame.bgra", photo.PixelsBgra32);   // raw dump fallback

5. Record to MP4 (direct pipeline, in-file audio)
-------------------------------------------------
    using CodeBrix.Webcam.Capture;

    // session was opened with AudioCaptureMode.Auto and the camera has a mic
    session.StartRecording(new WebcamRecordingOptions("/videos/clip.mp4")
    {
        Format = WebcamVideoFormat.Mp4H264,      // the default
        VideoBitrateKbps = 6000,
    });
    // ... preview keeps running; IsRecording == true ...
    WebcamRecordingResult result = session.StopRecording();
    Console.WriteLine($"{result.VideoFilePath}  {result.Duration}  " +
        $"audio in file: {result.AudioFilePath == null}");

6. Record with a live-updatable overlay (frame path, sidecar audio)
-------------------------------------------------------------------
    using CodeBrix.Webcam.Capture;
    using SkiaSharp;

    // Build a straight-alpha overlay the size of the video with Skia.
    static WebcamOverlay MakeOverlay(uint width, uint height, string caption)
    {
        var info = new SKImageInfo((int)width, (int)height, SKColorType.Bgra8888,
                                   SKAlphaType.Unpremul);          // STRAIGHT alpha
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = SKColors.White, TextSize = 48 };
        surface.Canvas.DrawText(caption, 24, height - 24, paint);
        using var image = surface.Snapshot();
        var pixels = new byte[info.BytesSize];
        unsafe
        {
            fixed (byte* p = pixels)
            {
                image.ReadPixels(info, (IntPtr)p, info.RowBytes, 0, 0);
            }
        }
        return new WebcamOverlay(pixels, info.Width, info.Height, info.RowBytes);
    }

    if (!session.IsOverlayRecordingSupported)
    {
        throw new InvalidOperationException("overlay recording unavailable here");
    }
    session.SetOverlay(MakeOverlay(session.FrameWidth, session.FrameHeight, "REC 00:00"));
    session.StartRecording(new WebcamRecordingOptions("/videos/captioned.mp4")
    {
        AllowLiveOverlay = true,                 // keep the frame path even if
    });                                          // the overlay is cleared later
    // every second:
    session.SetOverlay(MakeOverlay(session.FrameWidth, session.FrameHeight, $"REC {elapsed:mm\\:ss}"));
    // ...
    WebcamRecordingResult result = session.StopRecording();
    // result.AudioFilePath is a sidecar WAV when a mic was captured;
    // result.EstimatedAudioOffset is the offset to apply when muxing.
    // result.FramesRecorded counts the composited frames.

7. Adjust a camera control
--------------------------
    IImagingDeviceControl focus = device.Controls
        .FirstOrDefault(c => c.Kind == ImagingDeviceControlKind.Focus);
    if (focus != null)
    {
        if (focus.SupportsAuto) { focus.SetAuto(false); }
        focus.SetValue(Math.Clamp(120, focus.Minimum, focus.Maximum));
    }
    Do this from your own thread, never from inside FrameReceived.


PLATFORM NOTES
==============

Windows
-------
  - Capture and recording use Media Foundation; enumeration and controls
    use DirectShow. No native runtime, no VideoLAN packages.
  - Every camera format (YUY2, NV12, MJPEG, H.264-only cameras) is
    converted to BGRA by the in-box converters; requested modes are
    negotiated against the camera's native type list.
  - Recording: MP4/H.264 (hardware-accelerated where available) with an
    in-file AAC track when a mic is captured; frame-path (overlay)
    recordings deliver audio as the sidecar WAV. Starting/stopping a
    recording never interrupts the preview.
  - LIMITATION: WebcamVideoFormat.MjpegAvi is not supported;
    StartRecording throws a WebcamException pointing at Mp4H264.
  - Threading: MF/WASAPI objects are not apartment-agile. The library
    keeps the capture on its own thread and routes control calls through
    an internal MTA thread, so calling from an STA UI thread is fine — you
    do not need to create an MTA thread yourself.

Linux
-----
  - Enumeration and controls use V4L2 ioctls; Id is the /dev/videoN node.
    Controls read and write fully while streaming.
  - Capture uses the libvlc engine (`sudo apt install libvlc5
    vlc-plugin-base`). Overlay recording additionally needs libvlc's
    in-memory input plugin (also in vlc-plugin-base);
    IsOverlayRecordingSupported probes for it.
  - Paired microphones are ALSA devices ("hw:0,0"); AudioDeviceId takes
    the same form.

macOS
-----
  - Enumeration and controls use AVFoundation directly through the
    Objective-C runtime — nothing extra is shipped. Built-in cameras,
    external USB/Thunderbolt cameras and iPhone Continuity Cameras are all
    discovered. Id is the AVCaptureDevice uniqueID.
  - Controls are sparser than on Windows/Linux BY DESIGN: AVFoundation
    exposes only focus / exposure / white-balance MODE selectors, never
    UVC processing controls (brightness, contrast, zoom, ...). Many
    cameras enumerate with few or zero controls on macOS; that is correct.
  - Capture uses the libvlc engine from /Applications/VLC.app (or dylibs
    the app bundles itself, which win).
  - Hardware quirk seen in the field: a USB 3 camera attached at USB 2.0
    speed (e.g. through a USB 2.0 hub) may present NO USB audio interface,
    so macOS sees no microphone and PairedMicrophone is legitimately null.
    Reconnect the camera to a USB 3 port to restore the mic.

  PERMISSIONS (TCC) — the part that surprises everyone: macOS gates camera
  and microphone capture behind per-application user consent. The
  underlying capture modules only CHECK the authorization status — they
  never trigger the prompt — so WebcamSession.Start() requests consent
  itself: on first use the system prompt appears, and a denial surfaces as
  a WebcamException pointing at System Settings > Privacy & Security >
  Camera (or > Microphone). Consequences:
    - Consent attaches to the RESPONSIBLE application. A bundled .app
      MUST declare NSCameraUsageDescription (and
      NSMicrophoneUsageDescription when audio is captured) in its
      Info.plist, or macOS refuses access outright. A bare `dotnet run`
      process attaches consent to the hosting terminal application and
      needs no usage-description string.
    - Enumeration does NOT require consent; only opening a session does.
    - Non-interactive contexts (CI runners, ssh sessions, AI-agent shells)
      CANNOT show the prompt: the request is denied instantly and the
      status stays "not determined". No retry helps — a human must run
      live capture once from an interactive session (e.g. Terminal) and
      click Allow; after that, capture works in that context.


MINIMUM VIABLE PROJECT
======================
A console program that lists cameras, opens the first one, and writes one
raw BGRA frame-photo to disk. On Windows nothing else is needed; on
Debian-based Linux run `sudo apt install libvlc5 vlc-plugin-base` first;
on macOS install VLC.app and expect the camera-consent prompt.

    <!-- Snap.csproj -->
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
      </PropertyGroup>
      <ItemGroup>
        <!-- use the latest published version; the engine dependency comes
             along automatically at the same version -->
        <PackageReference Include="CodeBrix.Webcam.LgplLicenseForever"
                          Version="x.y.z" />
      </ItemGroup>
    </Project>

    // Program.cs
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using CodeBrix.Webcam;
    using CodeBrix.Webcam.Capture;

    var devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
    if (devices.Count == 0) { Console.WriteLine("no camera"); return; }
    Console.WriteLine($"using {devices[0].FriendlyName}");

    using var session = new WebcamSession(devices[0]);
    session.Start();
    WebcamPhoto photo = session.CapturePhoto(TimeSpan.FromSeconds(5));
    File.WriteAllBytes($"photo_{photo.Width}x{photo.Height}.bgra", photo.PixelsBgra32);
    Console.WriteLine($"saved {photo.Width}x{photo.Height} at {photo.CapturedAtUtc:O}");

Run: `dotnet run`


PERFORMANCE TIPS
================
  - The pull cache is lazy: TryCopyLatestFrame costs one frame copy per
    frame ONLY after the first call. Sessions that only push pay nothing.
  - Keep FrameReceived handlers to a copy. Everything on the capture
    thread delays the next frame; do decoding, drawing and inference
    elsewhere.
  - Previews want the newest frame, not every frame: use the pull model
    and let the UI's own repaint cadence drive it; use FrameReceived only
    as an Invalidate() trigger.
  - Vision pipelines: latest-wins dropping (example 3) keeps capture at
    full rate while inference runs slower.
  - Request MJPEG (PreferredFormat = ImagingPixelFormat.Mjpeg) for high
    resolutions at full frame rate over USB; uncompressed YUYV saturates
    USB 2 at 720p and above.
  - LiveCachingMs (default 100) trades preview latency for tolerance of
    scheduling hiccups; lower it for snappier previews on quiet machines.
  - Direct MP4 recording (no overlay, AllowLiveOverlay false) is the
    lowest-overhead recording path and the only one with in-file audio;
    the frame path composites every frame in managed code.
  - MjpegAvi (Linux/macOS) is near-zero CPU when you need raw capture and
    can re-encode later.
  - WebcamOverlay copies its pixels once in the constructor — build a new
    overlay only when its content changes, not per frame.
  - One WebcamFrameRenderer / bitmap per canvas; reallocate only on size
    change (example 2).


COMMON PITFALLS TO AVOID
========================
  - Doing work in FrameReceived: it runs on the capture thread and the
    PixelPlane dies when the handler returns. Copy (CopyTo or
    TryCopyLatestFrame) and leave; never touch UI objects or call session
    methods from inside it.
  - First TryCopyLatestFrame returns false by design — the cache switches
    on with that call. Treat false as "nothing yet", not as an error.
  - Unmirrored preview: users expect a mirror. Mirror at render time and
    use CapturePhoto(mirrorHorizontally: true) so stills match the
    preview; mirror vision coordinates (x' = 1 - x) too.
  - Overlay alpha: WebcamOverlay wants STRAIGHT alpha. Skia's default is
    premultiplied; read pixels with SKAlphaType.Unpremul or edges blend
    wrong.
  - Overlay size: it must equal FrameWidth x FrameHeight exactly once
    frames flow; SetOverlay throws WebcamException otherwise. Read
    FrameWidth/FrameHeight after Start() (they are 0 before frames flow).
  - SetOverlay during a DIRECT recording throws: start the recording with
    the overlay already set or AllowLiveOverlay = true.
  - MjpegAvi on Windows throws: use Mp4H264.
  - Missing native engine on Linux/macOS: Start() throws WebcamException
    with the fix in the message. Enumeration still works, so "I can list
    cameras but cannot open one" usually means libvlc/VLC.app is missing.
  - macOS consent: a bundled app without NSCameraUsageDescription is
    refused outright; a non-interactive shell is denied instantly with no
    prompt. A human must click Allow once from an interactive session.
  - Windows controls while another app holds the camera are best-effort;
    adjust between sessions if a driver ignores live changes.
  - Audio monitoring feedback: MonitorAudio = true with camera and
    speakers in the same room feeds back audibly. Leave it false by
    default; recordings capture the mic regardless.
  - Sidecar audio: frame-path recordings (overlay / AllowLiveOverlay) put
    audio in a WAV next to the video (AudioFilePath); mux it yourself
    with EstimatedAudioOffset. Only the direct MP4 pipeline has in-file
    audio.
  - Session switching: a session is bound to one device. Dispose it and
    create a new one to switch cameras; do not reuse across devices.
  - Never reference CodeBrix.Platform.MediaPlayerCore types for webcam
    work; nothing in this API takes or returns one.


WHAT THIS PACKAGE DOES NOT DO
=============================
  - It does not encode images: WebcamPhoto is raw BGRA; PNG/JPEG encoding
    is your image library's job (e.g. CodeBrix.Imaging).
  - It does not mux sidecar audio into frame-path recordings; combine the
    WAV and MP4 with a stream-copy mux (e.g. CodeBrix.VideoProcessing).
  - It does not ship UI: no camera-preview control for any XAML/Skia
    stack; the renderer in example 2 is the pattern to copy.
  - It does not support MJPEG-passthrough AVI on Windows.
  - It does not fabricate camera controls on macOS: AVFoundation exposes
    only mode selectors, and so does this package.
  - It does not play media files or stream from the network — that is the
    CodeBrix.MediaCore engine package.
  - It does not run on mobile or on operating systems other than Windows,
    Linux and macOS (GetImagingMediaDeviceListAsync throws
    PlatformNotSupportedException elsewhere).
  - It does not bundle libvlc for Linux/macOS capture.


WORKING EXAMPLES ON GITHUB
==========================
  https://github.com/ellisnet/CodeBrix.Platform.MediaPlayerCore/tree/main/samples/WebcamViewer
      Live webcam viewer for eight UI heads (CodeBrix.Platform on Linux
      X11 / Wayland / FrameBuffer, macOS, Win32-Skia and WPF-Skia; native
      WinUI 3; native WPF): camera dropdown from
      GetImagingMediaDeviceListAsync, live preview on a Skia canvas,
      audio-monitor toggle, frame-photos to PNG. One shared view model and
      one shared VideoCanvas under Shared/.

  https://github.com/ellisnet/CodeBrix.Platform.MediaPlayerCore/tree/main/tests/CodeBrix.Webcam.Tests
      LiveCameraTests.cs        -- enumerate, live frames + photo, latest-frame
                                   cache + mirrored photo, overlay recording to
                                   MP4 via the frame path (opt-in; see the
                                   MAINTAINER notes for the env var)
      WebcamDevicesTests.cs     -- enumeration on any host
      WebcamSessionOptionsTests.cs, WebcamOverlayTests.cs, WebcamPhotoTests.cs
                                -- option defaults, overlay validation and
                                   repacking, FlipHorizontal semantics
      OverlayCompositorTests.cs -- straight-alpha blend expectations
      PublicApiLeakTests.cs     -- the no-leak rule as a reflection test

  The WebcamPainter sample in the CodeBrix.Samples repository shows the
  two-mode capture-then-paint flow with mirrored UX and hand tracking
  (MediaPipe through CodeBrix.VideoProcessing.OpenCV5).


QUICK REFERENCE CARD
====================
Install:      dotnet add package CodeBrix.Webcam.LgplLicenseForever
              Windows: nothing else.  Linux: apt libvlc5 vlc-plugin-base.
              macOS: install VLC.app; expect the camera-consent prompt.
Namespaces:   using CodeBrix.Webcam; using CodeBrix.Webcam.Devices;
              using CodeBrix.Webcam.Capture;
Enumerate:    var cams = await WebcamDevices.GetImagingMediaDeviceListAsync();
              cam.Id / FriendlyName / Hardware / Capabilities / Controls /
              PairedMicrophone
Open:         using var s = new WebcamSession(cam[, new WebcamSessionOptions {
                  RequestedWidth, RequestedHeight, RequestedFrameRate,
                  PreferredFormat = ImagingPixelFormat.Mjpeg,
                  AudioCapture = AudioCaptureMode.Auto|Off|SpecificDevice,
                  AudioDeviceId, CompositeOverlayOnPreview, LiveCachingMs }]);
              s.Start();   s.FrameWidth x s.FrameHeight
Push:         s.FrameReceived += (_, e) => e.CopyTo(buf);   // capture thread!
Pull:         if (s.TryCopyLatestFrame(ref buf, out w, out h)) draw(buf)
Photo:        WebcamPhoto p = s.CapturePhoto(mirrorHorizontally: true);
              p.PixelsBgra32 / Width / Height / StrideBytes / FlipHorizontal()
Overlay:      s.SetOverlay(new WebcamOverlay(bgraStraightAlpha, w, h[, stride]));
              s.ClearOverlay();   s.IsOverlayRecordingSupported
Record:       s.StartRecording(new WebcamRecordingOptions(path) { Format =
                  WebcamVideoFormat.Mp4H264|MjpegAvi, VideoBitrateKbps,
                  AllowLiveOverlay });
              WebcamRecordingResult r = s.StopRecording();
              r.VideoFilePath / AudioFilePath / EstimatedAudioOffset /
              Duration / FramesRecorded
Audio:        s.IsAudioCaptureActive; s.MonitorAudio (default off);
              s.MonitorVolume 0..100
Controls:     ctl.GetValue()/SetValue(v)/GetAuto()/SetAuto(b);
              Kind/Name/RawId/ControlType/Minimum/Maximum/Step/DefaultValue
Errors:       WebcamException only (never an engine type)
Threading:    FrameReceived = capture thread: copy and leave; no session
              calls inside it. Session methods: one thread at a time.
Windows:      Media Foundation; no MjpegAvi. macOS: TCC consent +
              Info.plist keys for bundled apps; sparse controls by design.
Engine docs:  AGENT-README.txt at the repository root (not needed for
              webcam work)
