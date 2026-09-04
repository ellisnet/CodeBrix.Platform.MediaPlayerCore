================================================================================
AGENT-README: CodeBrix.Platform.MediaPlayerCore
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever NuGet package
================================================================================


OVERVIEW
========
CodeBrix.Platform.MediaPlayerCore is a small .NET 10 or later companion to
the CodeBrix.MediaCore engine. Its PUBLIC surface is exactly two types:

    IVideoView      the contract a video-hosting control implements so a
                    playback layer can hand it a MediaPlayer
    MediaPosition   an immutable value object that turns a playback
                    position + media length into elapsed/remaining times
                    and ready-to-display "mm:ss" strings

That is the whole consumable API. The assembly ALSO contains a
MediaPlayerElement-style management layer (state, seek bar, volume,
audio/video/subtitle track, aspect-ratio, auto-hide, buffering-progress,
cast-renderer and device-awakening managers, plus the IVideoControl
interface and a FontAwesome icon-codepoint table) — but every one of
those types is `internal`, visible only to the repository's own test
project. A consumer of the NuGet package cannot reference them, and this
file does not pretend otherwise. What the package gives a consumer is the
SEAM: the shared type identity of IVideoView (so a control library and a
player library agree on one interface without referencing each other) and
the MediaPosition helper. The CodeBrix.Platform MediaPlayer add-in is the
consumer this seam was kept for.

Provenance: a port of LibVLCSharp's IVideoView and
Shared/MediaPlayerElement sources. Upstream namespaces LibVLCSharp.Shared
and LibVLCSharp.Shared.MediaPlayerElement became
CodeBrix.Platform.MediaPlayerCore and
CodeBrix.Platform.MediaPlayerCore.MediaPlayerElement; do not write the
upstream namespaces. The ported files keep `#nullable enable annotations`,
so `?` below marks nullable members exactly as the source does.


INSTALLATION
============
    dotnet add package CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever

PackageId:    CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever
Assembly:     CodeBrix.Platform.MediaPlayerCore.dll
Target:       .NET 10 or later
Dependencies: CodeBrix.MediaCore.LgplLicenseForever — ALWAYS at the same
              version as this package. The two are built and published
              together; never pin them to different versions. Referencing
              this package pulls the engine in automatically.
License:      LGPL-2.1-or-later. Consume via <PackageReference>; never
              merge the DLL into your own assembly. LICENSE and
              THIRD-PARTY-NOTICES.txt ship inside the package.
Native:       nothing of its own. Whatever MediaPlayer you assign to an
              IVideoView comes from the engine, which needs native libvlc
              at runtime — see the engine's AGENT-README (INSTALLATION)
              for the Windows / Linux / macOS story.

WHICH PACKAGE DO I REFERENCE?
  - You are writing a control that hosts video, or a player layer that
    talks to such controls through an interface: reference THIS package.
  - You just want to play media or grab frames: reference only
    CodeBrix.MediaCore.LgplLicenseForever; you do not need this one.
  - You want a ready-made XAML MediaPlayerElement: use the CodeBrix.Platform
    MediaPlayer add-in (see WORKING EXAMPLES ON GITHUB), which references
    this package for you.


KEY NAMESPACES / USINGS
=======================
    using CodeBrix.Platform.MediaPlayerCore;                    // IVideoView
    using CodeBrix.Platform.MediaPlayerCore.MediaPlayerElement; // MediaPosition

Both namespaces are ALSO used by the engine assembly (the engine's types
live in CodeBrix.Platform.MediaPlayerCore even though its package is
CodeBrix.MediaCore) — that mismatch is deliberate and permanent. A third
namespace, `FontAwesome`, exists in this assembly but contains only an
internal class.


CORE API REFERENCE
==================
Every public member of the package is listed here; there are no others.

IVideoView
----------
    namespace CodeBrix.Platform.MediaPlayerCore;

    public interface IVideoView
    {
        MediaPlayer? MediaPlayer { get; set; }
    }

  - `MediaPlayer` is CodeBrix.Platform.MediaPlayerCore.MediaPlayer from
    the engine package.
  - The interface defines only the property. What a setter DOES —
    attaching the player's video output to the control's surface,
    detaching the previous player, raising a "player changed" event — is
    entirely the implementer's responsibility. The engine ships two
    event-args classes for exactly that purpose (they are not raised by
    anything in either package):
        MediaPlayerChangedEventArgs(MediaPlayer? oldMediaPlayer,
                                    MediaPlayer? newMediaPlayer)
        MediaPlayerChangingEventArgs(same shape)
    both exposing `MediaPlayer? OldMediaPlayer` and `MediaPlayer? NewMediaPlayer`.
  - Nothing in this package calls the setter for you; a player layer that
    receives an IVideoView is expected to assign its MediaPlayer once and
    to set it to null (or a new player) when it swaps players.

MediaPosition
-------------
    namespace CodeBrix.Platform.MediaPlayerCore.MediaPlayerElement;

    public class MediaPosition
    {
        public MediaPosition(float position, double seekBarPosition, long length);
        public float    Position          { get; }   // 0.0 .. 1.0, as given
        public double   SeekBarPosition   { get; }   // as given (your slider scale)
        public TimeSpan ElapsedTime       { get; }   // position * length ms
        public TimeSpan RemainingTime     { get; }   // (length - elapsed) ms
        public string   ElapsedTimeText   { get; }   // formatted, see below
        public string   RemainingTimeText { get; }
    }

  - `position` is the fraction of the media played (MediaPlayer.Position
    or MediaPlayerPositionChangedEventArgs.Position).
  - `seekBarPosition` is whatever your slider uses (e.g. position * 100
    for a 0..100 slider); it is stored, not interpreted.
  - `length` is the media length in MILLISECONDS (MediaPlayer.Length or
    MediaPlayerLengthChangedEventArgs.Length).
  - ElapsedTimeText / RemainingTimeText format the TimeSpans as:
        "mm:ss"         when under one hour
        "hh:mm:ss"      when Hours != 0
        "d.hh:mm:ss"    when Days != 0
    (the formatting helper is internal; these are its exact patterns).
  - The object is immutable; construct a new one per position update.
    With length 0 both times are zero; with position > 1 RemainingTime
    is negative — clamp your inputs.


COMPLETE EXAMPLES
=================

1. Implement IVideoView on a Skia-drawing host control
------------------------------------------------------
The host renders through the engine's VideoFrameSink so it works on every
windowing system. Only the IVideoView parts come from this package; the
rest is engine API and your UI toolkit's canvas.

    using System;
    using System.Runtime.InteropServices;
    using CodeBrix.Platform.MediaPlayerCore;
    using SkiaSharp;

    public sealed class SkiaVideoHost : IVideoView, IDisposable
    {
        private readonly object _sync = new object();
        private MediaPlayer _player;
        private VideoFrameSink _sink;
        private byte[] _packed;
        private int _width, _height;

        public event EventHandler<MediaPlayerChangedEventArgs> MediaPlayerChanged;
        public event EventHandler FrameArrived;      // UI: call Invalidate()

        public MediaPlayer MediaPlayer
        {
            get => _player;
            set
            {
                if (ReferenceEquals(_player, value)) { return; }
                MediaPlayer old = _player;
                Detach();
                _player = value;
                if (value != null)
                {
                    // must happen BEFORE the player's first Play()
                    _sink = new VideoFrameSink(value);
                    _sink.FormatChanged += OnFormatChanged;
                    _sink.FrameReady += OnFrameReady;
                }
                MediaPlayerChanged?.Invoke(this,
                    new MediaPlayerChangedEventArgs(old, value));
            }
        }

        private void OnFormatChanged(object sender, VideoFrameFormatChangedEventArgs e)
        {
            lock (_sync)
            {
                _width = (int)e.Width;
                _height = (int)e.Height;
                _packed = new byte[_width * _height * 4];
            }
        }

        private void OnFrameReady(object sender, VideoFrameReadyEventArgs e)
        {
            lock (_sync)                       // libvlc thread: copy and leave
            {
                if (_packed == null) { return; }
                int row = (int)e.Width * 4;
                for (int y = 0; y < e.Height; y++)
                {
                    Marshal.Copy(e.Plane + (int)(y * e.PitchBytes), _packed, y * row, row);
                }
            }
            FrameArrived?.Invoke(this, EventArgs.Empty);   // marshal to UI there
        }

        public void Paint(SKCanvas canvas, int viewWidth, int viewHeight)
        {
            lock (_sync)
            {
                canvas.Clear(SKColors.Black);
                if (_packed == null) { return; }
                using var bitmap = new SKBitmap(new SKImageInfo(_width, _height,
                    SKColorType.Bgra8888, SKAlphaType.Opaque));
                Marshal.Copy(_packed, 0, bitmap.GetPixels(), _packed.Length);
                float scale = Math.Min((float)viewWidth / _width, (float)viewHeight / _height);
                float w = _width * scale, h = _height * scale;
                canvas.DrawBitmap(bitmap, new SKRect((viewWidth - w) / 2, (viewHeight - h) / 2,
                    (viewWidth + w) / 2, (viewHeight + h) / 2));
            }
        }

        private void Detach()
        {
            if (_sink == null) { return; }
            _sink.FrameReady -= OnFrameReady;
            _sink.FormatChanged -= OnFormatChanged;
            _player?.Stop();                   // stop before disposing the sink
            _sink.Dispose();
            _sink = null;
        }

        public void Dispose() => Detach();
    }

    A player layer then only needs the interface:

        void Attach(IVideoView view, MediaPlayer player) => view.MediaPlayer = player;

2. Drive a seek bar and time labels with MediaPosition
------------------------------------------------------
    using System;
    using CodeBrix.Platform.MediaPlayerCore;
    using CodeBrix.Platform.MediaPlayerCore.MediaPlayerElement;

    public sealed class TransportViewModel
    {
        private long _lengthMs;
        public MediaPosition Current { get; private set; } =
            new MediaPosition(0f, 0d, 0L);
        public event EventHandler PositionUpdated;        // bind to UI

        public void Bind(MediaPlayer player)
        {
            player.LengthChanged += (_, e) => _lengthMs = e.Length;
            player.PositionChanged += (_, e) =>
            {
                // libvlc thread: compute the immutable snapshot, post to UI
                float p = Math.Clamp(e.Position, 0f, 1f);
                Current = new MediaPosition(p, p * 100d, _lengthMs);  // 0..100 slider
                PositionUpdated?.Invoke(this, EventArgs.Empty);
            };
        }

        // UI: slider.Value = Current.SeekBarPosition;
        //     elapsed.Text = Current.ElapsedTimeText;      e.g. "03:07"
        //     remaining.Text = "-" + Current.RemainingTimeText;
        // Seeking back: player.Position = (float)(slider.Value / 100d);
    }


MINIMUM VIABLE PROJECT
======================
A class library that publishes a video-host control and a transport view
model against the seam. The application that hosts it must ALSO make
native libvlc available (VideoLAN.LibVLC.Windows in the Windows app
project; `sudo apt install libvlc5 vlc-plugin-base` on Debian-based Linux;
VLC.app on macOS).

    <!-- VideoHost.csproj -->
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
      </PropertyGroup>
      <ItemGroup>
        <!-- use the latest published version; the engine comes along at the
             same version automatically -->
        <PackageReference Include="CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever"
                          Version="x.y.z" />
      </ItemGroup>
    </Project>

    // MinimalVideoView.cs
    using CodeBrix.Platform.MediaPlayerCore;

    public sealed class MinimalVideoView : IVideoView
    {
        public MediaPlayer MediaPlayer { get; set; }   // attach rendering in the
                                                       // setter as in example 1
    }

    // Usage from any consumer that only knows the interface:
    IVideoView view = new MinimalVideoView();
    Core.Initialize();
    var libVLC = new LibVLC();
    view.MediaPlayer = new MediaPlayer(new Media(libVLC, "/path/clip.mp4"));
    view.MediaPlayer.Play();


PERFORMANCE TIPS
================
  - MediaPosition is a small immutable class; one allocation per
    PositionChanged event is negligible, but PositionChanged fires many
    times per second — coalesce UI updates (one repaint per frame) rather
    than binding every event straight to the dispatcher.
  - Compute the MediaPosition on the libvlc event thread (cheap, no
    engine calls) and post only the finished object to the UI thread.
  - Everything else that costs time lives in the engine: see the
    PERFORMANCE TIPS of the CodeBrix.MediaCore AGENT-README (one LibVLC
    per process, memory rendering costs, FrameReady handlers as a memcpy).


COMMON PITFALLS TO AVOID
========================
  - Expecting the managers to be public. MediaPlayerElementManager,
    StateManager, SeekBarManager, VolumeManager, AspectRatioManager,
    AudioTracksManager, VideoTracksManager, SubtitlesTracksManager,
    AutoHideNotifier, BufferingProgressNotifier, CastRenderersDiscoverer,
    DeviceAwakeningManager, TracksManager, IDispatcher, IDisplayRequest,
    IDisplayInformation, IVideoControl, AspectRatio and FontAwesomeIcons
    are all `internal`. Code that names them will not compile against the
    NuGet package. Build your transport logic on the engine's events (as
    in example 2) or use the CodeBrix.Platform MediaPlayer add-in.
  - Assigning MediaPlayer after Play() when the host uses VideoFrameSink:
    the sink must be constructed before the player's first Play(). Assign
    the player to the view first, then Play().
  - Swapping players without detaching: your setter must stop the old
    player's rendering (dispose its sink / clear its window handle) before
    the field changes, or frames from the old player keep arriving.
  - `MediaPlayer?` in the interface is an annotation from the ported
    source, not a different type; with nullable reference types off you
    implement it as `public MediaPlayer MediaPlayer { get; set; }`.
  - MediaPosition takes MILLISECONDS for `length` (what MediaPlayer.Length
    reports); passing seconds makes ElapsedTime a thousand times too small.
  - Version skew: this package and CodeBrix.MediaCore.LgplLicenseForever
    must be the same version. Updating one without the other produces a
    MissingMethodException or TypeLoadException at run time.
  - The namespace is CodeBrix.Platform.MediaPlayerCore for BOTH packages;
    `using CodeBrix.MediaCore;` does not exist.


WHAT THIS PACKAGE DOES NOT DO
=============================
  - It does not render video, create windows or draw controls. IVideoView
    is a contract; the rendering is yours (or the add-in's).
  - It does not expose the MediaPlayerElement management layer; that code
    is internal implementation detail.
  - It does not bundle native libvlc (see the engine's AGENT-README).
  - It adds no playback API of its own — every Play/Pause/Seek call is on
    the engine's MediaPlayer.


WORKING EXAMPLES ON GITHUB
==========================
  https://github.com/ellisnet/CodeBrix.Platform/tree/main/src/AddIns/Platform.UI.MediaPlayer.Skia
      The CodeBrix.Platform MediaPlayer add-in: the production consumer of
      this package. Read its AGENT-README for a complete XAML
      MediaPlayerElement built on IVideoView + the engine.

  https://github.com/ellisnet/CodeBrix.Platform.MediaPlayerCore/tree/main/tests/CodeBrix.Platform.MediaPlayerCore.Tests
      The test project references this package's project and exercises
      the engine through it (playback, VideoFrameSink, discovery, dialogs,
      equalizer); it is the reference for the MediaPlayer calls your
      IVideoView implementation will make.


QUICK REFERENCE CARD
====================
Install:      dotnet add package CodeBrix.Platform.MediaPlayerCore.LgplLicenseForever
              (pulls CodeBrix.MediaCore.LgplLicenseForever at the SAME version)
Namespaces:   using CodeBrix.Platform.MediaPlayerCore;                    // IVideoView
              using CodeBrix.Platform.MediaPlayerCore.MediaPlayerElement; // MediaPosition
Public API:   interface IVideoView { MediaPlayer? MediaPlayer { get; set; } }
              class MediaPosition(float position, double seekBarPosition, long lengthMs)
                  .Position .SeekBarPosition .ElapsedTime .RemainingTime
                  .ElapsedTimeText .RemainingTimeText   ("mm:ss" / "hh:mm:ss")
Internal:     every *Manager, *Notifier, IVideoControl, IDispatcher,
              IDisplayRequest, IDisplayInformation, FontAwesomeIcons
Host pattern: set view.MediaPlayer BEFORE Play(); attach a VideoFrameSink
              (or Hwnd/XWindow/NsObject) in the setter; detach on swap
Transport:    player.PositionChanged + LengthChanged -> new MediaPosition(...)
Add-in:       https://github.com/ellisnet/CodeBrix.Platform/tree/main/src/AddIns/Platform.UI.MediaPlayer.Skia
Engine docs:  AGENT-README.txt at the repository root (CodeBrix.MediaCore)
