using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodeBrix.Platform.MediaPlayerCore;

/// <summary>
/// Renders a <see cref="MediaPlayer"/>'s decoded video into CPU memory buffers (libvlc's
/// "vmem" output) and raises <see cref="FrameReady"/> for every frame that is due for
/// display. Because no operating-system window is involved, this works identically on
/// every desktop platform and windowing system (including Wayland and bare-framebuffer
/// hosts, where libvlc 3.x has no window-embedding API at all).
/// <para/>
/// Frames are delivered as 32-bit BGRA pixels (byte order blue, green, red, alpha per
/// pixel, alpha always opaque for ordinary video), which maps directly onto common
/// bitmap formats such as SkiaSharp's Bgra8888.
/// <para/>
/// Usage rules:
/// <list type="bullet">
/// <item><description>Construct the sink BEFORE calling <see cref="MediaPlayer.Play()"/>.
/// Attaching the sink permanently switches the player to memory rendering; it cannot be
/// detached again for the lifetime of the <see cref="MediaPlayer"/>.</description></item>
/// <item><description><see cref="FrameReady"/> and <see cref="FormatChanged"/> are raised
/// on internal libvlc threads. Handlers must copy the pixel data (or forward it) and
/// return quickly; they must not touch UI objects directly and must not call back into
/// <see cref="MediaPlayer"/> members (re-entering libvlc from one of its own callbacks
/// can deadlock).</description></item>
/// <item><description>Dispose the sink only after the player has been stopped or
/// disposed; the buffers are in active use by libvlc while playback runs.</description></item>
/// </list>
/// </summary>
public sealed class VideoFrameSink : IDisposable
{
    private const int BytesPerPixel = 4;
    private const uint SizeAlignment = 32;
    private const int DefaultBufferCount = 3;
    private const int MaxBufferCount = 8;

    // "BGRA" fourcc, written into libvlc's chroma buffer by the format callback.
    private static readonly byte[] ChromaBgra = { (byte)'B', (byte)'G', (byte)'R', (byte)'A' };

    private readonly MediaPlayer _mediaPlayer;
    private readonly int _bufferCount;
    private readonly object _sync = new object();
    private readonly VideoFrameReadyEventArgs _frameReadyArgs = new VideoFrameReadyEventArgs();

    private IntPtr[] _buffers = Array.Empty<IntPtr>();
    private int _ringIndex;
    private uint _width;
    private uint _height;
    private uint _pitchBytes;
    private uint _lines;
    private bool _disposed;

    /// <summary>
    /// Raised (on a libvlc thread) each time a decoded frame is due for display. The
    /// pixel buffer exposed by the event args is only valid until the handler returns;
    /// copy it before returning. Handlers must not call back into <see cref="MediaPlayer"/>.
    /// </summary>
    public event EventHandler<VideoFrameReadyEventArgs> FrameReady;

    /// <summary>
    /// Raised (on a libvlc thread) when the video format has been negotiated — once when
    /// a video starts, and again whenever the source dimensions change (e.g. a new media).
    /// Handlers must not call back into <see cref="MediaPlayer"/>.
    /// </summary>
    public event EventHandler<VideoFrameFormatChangedEventArgs> FormatChanged;

    /// <summary>
    /// Attaches a memory frame sink to the given media player, using a default ring of
    /// three pixel buffers.
    /// </summary>
    /// <param name="mediaPlayer">The media player to render; must not have started playing yet.</param>
    public VideoFrameSink(MediaPlayer mediaPlayer)
        : this(mediaPlayer, DefaultBufferCount)
    {
    }

    /// <summary>
    /// Attaches a memory frame sink to the given media player.
    /// </summary>
    /// <param name="mediaPlayer">The media player to render; must not have started playing yet.</param>
    /// <param name="bufferCount">The number of pixel buffers in the ring (1 to 8). More
    /// buffers reduce the chance of libvlc writing the next frame while a handler is
    /// still reading the previous one; three is a safe default.</param>
    public VideoFrameSink(MediaPlayer mediaPlayer, int bufferCount)
    {
        if (mediaPlayer == null)
        {
            throw new ArgumentNullException(nameof(mediaPlayer));
        }
        if (bufferCount < 1 || bufferCount > MaxBufferCount)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferCount), bufferCount,
                $"bufferCount must be between 1 and {MaxBufferCount}");
        }

        _mediaPlayer = mediaPlayer;
        _bufferCount = bufferCount;

        // The MediaPlayer keeps strong references to these instance-method delegates,
        // so the player roots this sink (and its buffers) for as long as it lives.
        mediaPlayer.SetVideoFormatCallbacks(OnVideoFormat, OnVideoCleanup);
        mediaPlayer.SetVideoCallbacks(OnVideoLock, null, OnVideoDisplay);
    }

    /// <summary>
    /// Finalizer; releases the unmanaged pixel buffers if <see cref="Dispose"/> was never called.
    /// </summary>
    ~VideoFrameSink() => FreeBuffers();

    /// <summary>
    /// The media player this sink is attached to.
    /// </summary>
    public MediaPlayer MediaPlayer => _mediaPlayer;

    /// <summary>
    /// The number of pixel buffers in the ring.
    /// </summary>
    public int BufferCount => _bufferCount;

    /// <summary>
    /// Pixel width of the current video, or 0 if no format has been negotiated yet.
    /// </summary>
    public uint Width => _width;

    /// <summary>
    /// Pixel height of the current video, or 0 if no format has been negotiated yet.
    /// </summary>
    public uint Height => _height;

    /// <summary>
    /// Bytes per scanline of the current video (at least <see cref="Width"/> * 4, rounded
    /// up to a 32-byte multiple), or 0 if no format has been negotiated yet.
    /// </summary>
    public uint PitchBytes => _pitchBytes;

    /// <summary>
    /// Releases the unmanaged pixel buffers. Only call this after the attached player has
    /// been stopped or disposed — the buffers are in active use while playback runs.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        FreeBuffers();
        GC.SuppressFinalize(this);
    }

    private static uint AlignUp(uint value) => (value + (SizeAlignment - 1)) & ~(SizeAlignment - 1);

    private uint OnVideoFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height,
        ref uint pitches, ref uint lines)
    {
        Marshal.Copy(ChromaBgra, 0, chroma, ChromaBgra.Length);

        var pitch = AlignUp(width * BytesPerPixel);
        var lineCount = AlignUp(height);
        pitches = pitch;
        lines = lineCount;

        lock (_sync)
        {
            FreeBuffersLocked();
            var buffers = new IntPtr[_bufferCount];
            for (var i = 0; i < buffers.Length; i++)
            {
                buffers[i] = Marshal.AllocHGlobal((int)(pitch * lineCount));
            }
            _buffers = buffers;
            _ringIndex = 0;
            _width = width;
            _height = height;
            _pitchBytes = pitch;
            _lines = lineCount;
        }

        RaiseFormatChanged(new VideoFrameFormatChangedEventArgs(width, height, pitch, lineCount));

        return (uint)_bufferCount;
    }

    private IntPtr OnVideoLock(IntPtr opaque, IntPtr planes)
    {
        lock (_sync)
        {
            if (_buffers.Length == 0)
            {
                // Should not happen (libvlc calls the format callback first), but never
                // hand libvlc a null plane pointer.
                _buffers = new[] { Marshal.AllocHGlobal((int)Math.Max(1, _pitchBytes * _lines)) };
            }
            var index = _ringIndex;
            _ringIndex = (_ringIndex + 1) % _buffers.Length;
            Marshal.WriteIntPtr(planes, _buffers[index]);
            // The returned "picture identifier" is the 1-based buffer index, so that the
            // display callback can find the buffer again (and so it is never IntPtr.Zero).
            return (IntPtr)(index + 1);
        }
    }

    private void OnVideoDisplay(IntPtr opaque, IntPtr picture)
    {
        IntPtr buffer;
        uint width, height, pitch;
        lock (_sync)
        {
            var index = (int)picture - 1;
            if (index < 0 || index >= _buffers.Length)
            {
                return;
            }
            buffer = _buffers[index];
            width = _width;
            height = _height;
            pitch = _pitchBytes;
        }

        if (buffer == IntPtr.Zero || width == 0 || height == 0)
        {
            return;
        }

        var handler = FrameReady;
        if (handler != null)
        {
            _frameReadyArgs.Update(buffer, width, height, pitch);
            try
            {
                handler(this, _frameReadyArgs);
            }
            catch (Exception e)
            {
                // An exception escaping into native libvlc code would crash the process.
                Trace.WriteLine($"VideoFrameSink.FrameReady handler threw: {e}");
            }
        }
    }

    private void OnVideoCleanup(ref IntPtr opaque)
    {
        // Called by libvlc when its video output closes; no lock/display calls are in
        // flight past this point, so the buffers can be released.
        FreeBuffers();
    }

    private void RaiseFormatChanged(VideoFrameFormatChangedEventArgs args)
    {
        var handler = FormatChanged;
        if (handler != null)
        {
            try
            {
                handler(this, args);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"VideoFrameSink.FormatChanged handler threw: {e}");
            }
        }
    }

    private void FreeBuffers()
    {
        lock (_sync)
        {
            FreeBuffersLocked();
        }
    }

    private void FreeBuffersLocked()
    {
        var buffers = _buffers;
        _buffers = Array.Empty<IntPtr>();
        _ringIndex = 0;
        for (var i = 0; i < buffers.Length; i++)
        {
            if (buffers[i] != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffers[i]);
            }
        }
    }
}
