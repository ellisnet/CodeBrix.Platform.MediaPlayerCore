using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CodeBrix.Platform.MediaPlayerCore;

/// <summary>
/// Feeds caller-supplied video frames INTO libvlc — the push-model mirror of
/// <see cref="VideoFrameSink"/>. Raw 32-bit BGRA frames pushed via <see cref="PushFrame(IntPtr, uint, long)"/>
/// enter libvlc through its in-memory input ("imem") and flow into whatever stream-output
/// chain the caller configured — typically a transcode-to-file chain such as
/// <c>:sout=#transcode{vcodec=h264,vb=2000}:standard{access=file,mux=mp4,dst=out.mp4}</c>.
/// This is the building block for recording programmatically generated or modified video
/// (composited overlays, computer-generated frames, processed camera streams).
/// <para/>
/// Pixels are interpreted as 32 bits per pixel in blue, green, red, x byte order (the
/// fourth byte is ignored — supply opaque alpha or anything else). This matches the
/// buffers produced by <see cref="VideoFrameSink"/> and SkiaSharp's Bgra8888.
/// <para/>
/// Usage rules:
/// <list type="bullet">
/// <item><description>An instance is ONE-SHOT: construct, <see cref="Start"/>, push frames,
/// <see cref="Complete"/>, <see cref="WaitForCompletion"/>, dispose. Create a new instance
/// for the next encode.</description></item>
/// <item><description><see cref="PushFrame(IntPtr, uint, long)"/> copies the pixels before returning,
/// so the caller's buffer can be reused immediately. When the internal frame queue is full
/// (the encoder is behind), the call blocks until space frees up — this back-pressure is
/// deliberate, so no frame is ever silently dropped.</description></item>
/// <item><description>The availability of the underlying libvlc "imem" plugin depends on
/// how libvlc was packaged. Call <see cref="IsSupported"/> to probe it (the result is
/// cached for the process lifetime).</description></item>
/// <item><description>IMPORTANT — encoder settings for short or live streams: with x264's
/// defaults, libvlc's transcode chain buffers roughly 40 frames inside the encoder's
/// rate-control lookahead and DOES NOT drain that buffer when the stream ends — a stream
/// shorter than the lookahead produces an EMPTY output file. Always configure the encoder
/// for live capture in the sout chain, e.g.
/// <c>:sout=#transcode{vcodec=h264,vb=2000,venc=x264{tune=zerolatency}}:standard{access=file,mux=mp4,dst=out.mp4}</c>
/// (or <c>venc=x264{bframes=0,lookahead=0}</c>); at most one trailing frame is then lost
/// at end of stream, regardless of stream length.</description></item>
/// </list>
/// </summary>
public sealed class VideoFrameSource : IDisposable
{
    private const int BytesPerPixel = 4;
    private const int InternalBufferCount = 4;
    private const string ImemChroma = "RV32"; // 32-bit RGB, B-G-R-X byte order == BGRA-with-ignored-alpha

    // 0 = probed & unsupported, 1 = probed & supported, -1 = not probed yet.
    private static int _supportProbeResult = -1;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ImemGetDelegate(IntPtr data, IntPtr cookie, ref long dts, ref long pts,
        ref uint flags, ref UIntPtr size, ref IntPtr buffer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ImemReleaseDelegate(IntPtr data, IntPtr cookie, UIntPtr size, IntPtr buffer);

    private readonly LibVLC _libVLC;
    private readonly uint _width;
    private readonly uint _height;
    private readonly uint _frameRate;
    private readonly string[] _mediaOptions;
    private readonly int _frameBytes;
    private readonly object _sync = new object();
    private readonly Stack<IntPtr> _freeBuffers = new Stack<IntPtr>();
    private readonly Queue<QueuedFrame> _pendingFrames = new Queue<QueuedFrame>();
    private readonly ManualResetEventSlim _finished = new ManualResetEventSlim(false);
    private readonly Stopwatch _clock = new Stopwatch();

    // These delegate instances root the marshaled native thunks that libvlc calls on its
    // input thread; they must live exactly as long as the native side can call them.
    private ImemGetDelegate _getDelegate;
    private ImemReleaseDelegate _releaseDelegate;
    private GCHandle _selfHandle;

    private IntPtr[] _allBuffers = Array.Empty<IntPtr>();
    private Media _media;
    private MediaPlayer _player;
    private bool _started;
    private bool _completed;
    private bool _finalized;
    private bool _errored;
    private bool _disposed;
    private long _framesPushed;

    private readonly struct QueuedFrame
    {
        public QueuedFrame(IntPtr buffer, long presentationTimeUs)
        {
            Buffer = buffer;
            PresentationTimeUs = presentationTimeUs;
        }

        public IntPtr Buffer { get; }
        public long PresentationTimeUs { get; }
    }

    /// <summary>
    /// Creates a frame source that will feed frames of the given fixed dimensions into
    /// the given stream-output chain.
    /// </summary>
    /// <param name="libVLC">The libvlc instance to encode with.</param>
    /// <param name="width">Frame width in pixels (must be greater than zero; even values
    /// are strongly recommended — most video encoders reject odd dimensions).</param>
    /// <param name="height">Frame height in pixels (same recommendation as <paramref name="width"/>).</param>
    /// <param name="frameRate">The nominal frames-per-second of the stream (used for
    /// encoder setup; actual frame timing follows the pushed presentation timestamps).</param>
    /// <param name="mediaOptions">Additional libvlc media options, each starting with
    /// <c>:</c> — almost always including a <c>:sout=...</c> chain that says where the
    /// encoded video goes.</param>
    public VideoFrameSource(LibVLC libVLC, uint width, uint height, uint frameRate, params string[] mediaOptions)
    {
        if (libVLC == null)
        {
            throw new ArgumentNullException(nameof(libVLC));
        }
        if (width == 0 || height == 0)
        {
            throw new ArgumentOutOfRangeException(width == 0 ? nameof(width) : nameof(height),
                "Frame dimensions must be greater than zero");
        }
        if (frameRate == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameRate), "Frame rate must be greater than zero");
        }
        long frameBytes = (long)width * height * BytesPerPixel;
        if (frameBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Frame dimensions are too large");
        }

        _libVLC = libVLC;
        _width = width;
        _height = height;
        _frameRate = frameRate;
        _frameBytes = (int)frameBytes;
        _mediaOptions = mediaOptions ?? Array.Empty<string>();
    }

    /// <summary>
    /// Finalizer; releases the unmanaged frame buffers if <see cref="Dispose"/> was never called.
    /// </summary>
    ~VideoFrameSource() => FreeBuffers();

    /// <summary>Frame width in pixels, as passed to the constructor.</summary>
    public uint Width => _width;

    /// <summary>Frame height in pixels, as passed to the constructor.</summary>
    public uint Height => _height;

    /// <summary>Nominal frames-per-second, as passed to the constructor.</summary>
    public uint FrameRate => _frameRate;

    /// <summary>The bytes per scanline this source expects from tightly-packed input: <see cref="Width"/> * 4.</summary>
    public uint PitchBytes => _width * BytesPerPixel;

    /// <summary>The number of frames accepted by <see cref="PushFrame(IntPtr, uint, long)"/> so far.</summary>
    public long FramesPushed => Interlocked.Read(ref _framesPushed);

    /// <summary>
    /// True once the stream-output chain has finished after <see cref="Complete"/> —
    /// i.e. the output file (if any) has been finalized.
    /// </summary>
    public bool IsFinished => _finished.IsSet;

    /// <summary>
    /// Probes whether the underlying libvlc installation can supply frames through its
    /// in-memory input (the "imem" plugin) — some distro packagings omit optional plugins.
    /// The first call performs a real (tiny, sub-second) encode probe; the result is
    /// cached for the process lifetime, so subsequent calls are free.
    /// </summary>
    /// <param name="libVLC">The libvlc instance to probe.</param>
    /// <returns>True if <see cref="VideoFrameSource"/> is usable on this installation.</returns>
    public static bool IsSupported(LibVLC libVLC)
    {
        if (libVLC == null)
        {
            throw new ArgumentNullException(nameof(libVLC));
        }

        var cached = Volatile.Read(ref _supportProbeResult);
        if (cached >= 0)
        {
            return cached == 1;
        }

        var ok = false;
        try
        {
            using var probe = new VideoFrameSource(libVLC, 32, 32, 30, ":sout=#dummy");
            if (probe.Start())
            {
                var blackFrame = Marshal.AllocHGlobal(32 * 32 * BytesPerPixel);
                try
                {
                    unsafe
                    {
                        new Span<byte>((void*)blackFrame, 32 * 32 * BytesPerPixel).Clear();
                    }
                    probe.PushFrame(blackFrame);
                }
                finally
                {
                    Marshal.FreeHGlobal(blackFrame);
                }
                probe.Complete();
                ok = probe.WaitForCompletion(TimeSpan.FromSeconds(10));
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine($"VideoFrameSource support probe failed: {e}");
            ok = false;
        }

        Volatile.Write(ref _supportProbeResult, ok ? 1 : 0);
        return ok;
    }

    /// <summary>
    /// Throws if <see cref="IsSupported"/> is false, with a diagnosis that names the
    /// missing libvlc piece; otherwise does nothing.
    /// </summary>
    /// <param name="libVLC">The libvlc instance to probe.</param>
    /// <exception cref="VLCException">The libvlc installation cannot supply in-memory frames.</exception>
    public static void EnsureSupported(LibVLC libVLC)
    {
        if (!IsSupported(libVLC))
        {
            throw new VLCException(
                "This libvlc installation cannot accept in-memory video frames: the \"imem\" " +
                "input plugin is missing or non-functional. On Debian/Ubuntu the plugin ships " +
                "in vlc-plugin-base; verify the libvlc plugin directory contains libimem_plugin.so.");
        }
    }

    /// <summary>
    /// Opens the imem input and starts the stream-output chain. Call once, before the
    /// first <see cref="PushFrame(IntPtr, uint, long)"/>.
    /// </summary>
    /// <returns>True if playback of the input started; false if libvlc refused to start.</returns>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Start"/> was already called.</exception>
    public bool Start()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_started)
            {
                throw new InvalidOperationException("A VideoFrameSource is one-shot; Start() can only be called once.");
            }

            _getDelegate = OnImemGet;
            _releaseDelegate = OnImemRelease;
            // Root this instance for the duration of the encode: libvlc's input thread
            // holds raw function pointers into the delegates above, so neither they nor
            // this instance may be collected while the input can still call them.
            _selfHandle = GCHandle.Alloc(this);

            var buffers = new IntPtr[InternalBufferCount];
            for (var i = 0; i < buffers.Length; i++)
            {
                buffers[i] = Marshal.AllocHGlobal(_frameBytes);
                _freeBuffers.Push(buffers[i]);
            }
            _allBuffers = buffers;

            var options = new List<string>
            {
                ":imem-get=" + Marshal.GetFunctionPointerForDelegate(_getDelegate).ToInt64(),
                ":imem-release=" + Marshal.GetFunctionPointerForDelegate(_releaseDelegate).ToInt64(),
                ":imem-cat=2",
                ":imem-codec=" + ImemChroma,
                ":imem-width=" + _width,
                ":imem-height=" + _height,
                ":imem-fps=" + _frameRate,
            };
            options.AddRange(_mediaOptions);

            _media = new Media(_libVLC, "imem://", FromType.FromLocation, options.ToArray());
            _player = new MediaPlayer(_media);
            _player.EndReached += OnPlayerFinished;
            _player.EncounteredError += OnPlayerError;

            _started = true;
            _clock.Restart();

            if (_player.Play())
            {
                return true;
            }

            _errored = true;
            _finished.Set();
            return false;
        }
    }

    /// <summary>
    /// Copies one frame into the source. Blocks while the internal queue is full (the
    /// encoder is applying back-pressure). Safe to call from any single producer thread.
    /// </summary>
    /// <param name="pixels">Pointer to the top-left pixel of a BGRx frame of exactly
    /// <see cref="Width"/> × <see cref="Height"/> pixels.</param>
    /// <param name="sourcePitchBytes">The source's bytes-per-scanline; pass 0 for tightly
    /// packed input (<see cref="Width"/> * 4). Pass the real pitch when the source rows are
    /// padded — e.g. frames from <see cref="VideoFrameSink"/>, whose pitch is 32-byte aligned.</param>
    /// <param name="presentationTimeUs">The frame's presentation timestamp in microseconds,
    /// or -1 to stamp automatically with elapsed wall-clock time since <see cref="Start"/>.</param>
    /// <returns>True if the frame was queued; false if the source has completed, errored,
    /// or been disposed (the frame was not taken).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pixels"/> is zero.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Start"/> has not been called.</exception>
    public bool PushFrame(IntPtr pixels, uint sourcePitchBytes = 0, long presentationTimeUs = -1)
    {
        if (pixels == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(pixels));
        }
        if (sourcePitchBytes != 0 && sourcePitchBytes < PitchBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePitchBytes), sourcePitchBytes,
                $"sourcePitchBytes must be 0 (tightly packed) or at least {PitchBytes}");
        }

        IntPtr target;
        lock (_sync)
        {
            if (!_started)
            {
                throw new InvalidOperationException("Call Start() before pushing frames.");
            }
            while (_freeBuffers.Count == 0 && !_completed && !_errored && !_disposed)
            {
                Monitor.Wait(_sync, 250);
            }
            if (_completed || _errored || _disposed)
            {
                return false;
            }
            target = _freeBuffers.Pop();
        }

        CopyFrame(pixels, sourcePitchBytes == 0 ? PitchBytes : sourcePitchBytes, target);

        var pts = presentationTimeUs >= 0
            ? presentationTimeUs
            : _clock.ElapsedTicks / (Stopwatch.Frequency / 1000000L);

        lock (_sync)
        {
            if (_completed || _errored || _disposed)
            {
                _freeBuffers.Push(target);
                return false;
            }
            _pendingFrames.Enqueue(new QueuedFrame(target, pts));
            Interlocked.Increment(ref _framesPushed);
            Monitor.PulseAll(_sync);
        }
        return true;
    }

    /// <summary>
    /// Copies one frame from a managed buffer. See <see cref="PushFrame(IntPtr, uint, long)"/>.
    /// </summary>
    /// <param name="pixels">A BGRx frame of exactly <see cref="Width"/> × <see cref="Height"/>
    /// tightly packed pixels (length at least <see cref="Width"/> * <see cref="Height"/> * 4).</param>
    /// <param name="presentationTimeUs">The frame's presentation timestamp in microseconds,
    /// or -1 to stamp automatically with elapsed wall-clock time since <see cref="Start"/>.</param>
    /// <returns>True if the frame was queued; false if the source has completed, errored,
    /// or been disposed (the frame was not taken).</returns>
    public unsafe bool PushFrame(ReadOnlySpan<byte> pixels, long presentationTimeUs = -1)
    {
        if (pixels.Length < _frameBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(pixels), pixels.Length,
                $"Frame buffer must hold at least {_frameBytes} bytes");
        }
        fixed (byte* p = pixels)
        {
            return PushFrame((IntPtr)p, 0, presentationTimeUs);
        }
    }

    /// <summary>
    /// Declares that no more frames will be pushed. The queued frames drain into the
    /// encoder, the stream-output chain finalizes its output, and <see cref="IsFinished"/>
    /// becomes true. Use <see cref="WaitForCompletion"/> to wait for that moment.
    /// </summary>
    public void Complete()
    {
        lock (_sync)
        {
            _completed = true;
            Monitor.PulseAll(_sync);
        }
    }

    /// <summary>
    /// Blocks until the input has consumed all frames after <see cref="Complete"/>, then
    /// closes the stream-output chain so the output is finalized — for a file output, the
    /// file is complete and safe to read once this returns true.
    /// </summary>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>True if the source finished successfully; false on timeout or error.</returns>
    public bool WaitForCompletion(TimeSpan timeout)
    {
        if (!_finished.Wait(timeout))
        {
            return false;
        }

        // End-of-input has been reached, but muxers (mp4 in particular) only write their
        // buffered data when the stream-output chain closes — which happens on Stop().
        // Stop here, on the caller's thread (never on a libvlc event thread), so that
        // "completed" genuinely means "output finalized".
        MediaPlayer player;
        lock (_sync)
        {
            player = _finalized || _disposed ? null : _player;
            _finalized = true;
        }
        player?.Stop();

        return !_errored;
    }

    /// <summary>
    /// Stops the input (abandoning any un-drained frames if <see cref="Complete"/> +
    /// <see cref="WaitForCompletion"/> were not used), tears down the player, and releases
    /// the unmanaged frame buffers.
    /// </summary>
    public void Dispose()
    {
        MediaPlayer player;
        Media media;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _completed = true;
            // Unblock the imem-get callback (libvlc's input thread) BEFORE stopping the
            // player: Stop() joins the input thread, and the input thread may be parked
            // inside OnImemGet waiting for a frame — releasing it first prevents deadlock.
            Monitor.PulseAll(_sync);
            player = _player;
            media = _media;
            _player = null;
            _media = null;
        }

        if (player != null)
        {
            player.EndReached -= OnPlayerFinished;
            player.EncounteredError -= OnPlayerError;
            player.Stop();
            player.Dispose();
        }
        media?.Dispose();

        FreeBuffers();
        // Deliberately not disposing _finished: another thread may be blocked in
        // WaitForCompletion right now, and Set() on a disposed event would throw there.
        _finished.Set();
        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }
        GC.SuppressFinalize(this);
    }

    private unsafe void CopyFrame(IntPtr source, uint sourcePitch, IntPtr target)
    {
        var packedPitch = (int)PitchBytes;
        if (sourcePitch == packedPitch)
        {
            Buffer.MemoryCopy((void*)source, (void*)target, _frameBytes, _frameBytes);
            return;
        }
        var src = (byte*)source;
        var dst = (byte*)target;
        for (var y = 0; y < _height; y++)
        {
            Buffer.MemoryCopy(src + (y * sourcePitch), dst + (y * (long)packedPitch), packedPitch, packedPitch);
        }
    }

    private int OnImemGet(IntPtr data, IntPtr cookie, ref long dts, ref long pts,
        ref uint flags, ref UIntPtr size, ref IntPtr buffer)
    {
        lock (_sync)
        {
            while (_pendingFrames.Count == 0)
            {
                if (_completed || _errored || _disposed)
                {
                    return -1; // end of stream: queue drained and no more frames coming
                }
                Monitor.Wait(_sync, 250);
            }
            var frame = _pendingFrames.Dequeue();
            dts = frame.PresentationTimeUs;
            pts = frame.PresentationTimeUs;
            flags = 0;
            size = (UIntPtr)_frameBytes;
            buffer = frame.Buffer;
            return 0;
        }
    }

    private void OnImemRelease(IntPtr data, IntPtr cookie, UIntPtr size, IntPtr buffer)
    {
        lock (_sync)
        {
            if (!_disposed && buffer != IntPtr.Zero)
            {
                _freeBuffers.Push(buffer);
                Monitor.PulseAll(_sync);
            }
        }
    }

    private void OnPlayerFinished(object sender, EventArgs e)
    {
        _finished.Set();
        lock (_sync)
        {
            Monitor.PulseAll(_sync);
        }
    }

    private void OnPlayerError(object sender, EventArgs e)
    {
        _errored = true;
        _finished.Set();
        lock (_sync)
        {
            Monitor.PulseAll(_sync);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VideoFrameSource));
        }
    }

    private void FreeBuffers()
    {
        lock (_sync)
        {
            var buffers = _allBuffers;
            _allBuffers = Array.Empty<IntPtr>();
            _freeBuffers.Clear();
            _pendingFrames.Clear();
            for (var i = 0; i < buffers.Length; i++)
            {
                if (buffers[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffers[i]);
                }
            }
        }
    }
}
