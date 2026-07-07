using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static CodeBrix.Webcam.Internal.Windows.MediaFoundationNativeMethods;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// Writes an MP4 file through Media Foundation's sink writer: BGRA video frames encoded
/// to H.264 by the in-box (hardware where available) encoder, plus an optional AAC
/// audio track fed with 48 kHz / 16-bit / stereo PCM (the
/// <see cref="WasapiMicrophoneCapture"/> format). Presentation timestamps are supplied
/// by the caller in 100-ns units on a common zero-based clock. Thread-safe: video,
/// audio, and finalization may arrive from different threads.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class MfSinkWriterRecorder : IDisposable
{
    private const uint AacBytesPerSecond = 16000; // 128 kbps — an AAC-encoder-approved rate

    private readonly object _writerLock = new object();
    private readonly uint _width;
    private readonly uint _height;
    private readonly uint _frameRate;
    private readonly bool _hasAudio;

    // Assigned by CreateWriterAndStreams, which the constructor routes through
    // MtaThread so the sink writer lives in the MTA (see MtaThread).
    private uint _videoStream;
    private uint _audioStream;
    private IMFSinkWriter _writer;
    private bool _writing;
    private bool _finalized;
    private long _audioFramesWritten;

    /// <summary>Creates the sink writer and declares its streams; call <see cref="Begin"/> to start accepting samples.</summary>
    /// <param name="outputPath">The MP4 file to write.</param>
    /// <param name="width">Video frame width in pixels.</param>
    /// <param name="height">Video frame height in pixels.</param>
    /// <param name="frameRate">Nominal frames per second (encoder setup; timing follows the supplied timestamps).</param>
    /// <param name="videoBitrateKbps">H.264 bitrate in kilobits per second.</param>
    /// <param name="includeAudio">True to add a 48 kHz stereo AAC audio track.</param>
    internal MfSinkWriterRecorder(string outputPath, uint width, uint height, uint frameRate,
        uint videoBitrateKbps, bool includeAudio)
    {
        _width = width;
        _height = height;
        _frameRate = frameRate == 0 ? 30 : frameRate;
        _hasAudio = includeAudio;

        MtaThread.Run(() => CreateWriterAndStreams(outputPath, videoBitrateKbps, includeAudio));
    }

    private void CreateWriterAndStreams(string outputPath, uint videoBitrateKbps, bool includeAudio)
    {
        var width = _width;
        ThrowOnFailure(MFCreateAttributes(out var attributes, 2), "MFCreateAttributes");
        try
        {
            var hardwareKey = MfReadWriteEnableHardwareTransforms;
            attributes.SetUINT32(ref hardwareKey, 1);
            var throttlingKey = MfSinkWriterDisableThrottling;
            attributes.SetUINT32(ref throttlingKey, 1);

            ThrowOnFailure(MFCreateSinkWriterFromURL(outputPath, IntPtr.Zero, attributes, out _writer),
                "MFCreateSinkWriterFromURL");
        }
        finally
        {
            Marshal.ReleaseComObject(attributes);
        }

        try
        {
            // Video: H.264 output fed with packed top-down RGB32 (BGRx) frames.
            var videoOut = CreateVideoType(MfVideoFormatH264);
            try
            {
                var bitrateKey = MfMtAvgBitrate;
                videoOut.SetUINT32(ref bitrateKey, videoBitrateKbps * 1000);
                ThrowOnFailure(_writer.AddStream(videoOut, out _videoStream), "AddStream(video)");
            }
            finally
            {
                Marshal.ReleaseComObject(videoOut);
            }

            var videoIn = CreateVideoType(MfVideoFormatRgb32);
            try
            {
                var strideKey = MfMtDefaultStride;
                videoIn.SetUINT32(ref strideKey, width * 4);
                var independentKey = MfMtAllSamplesIndependent;
                videoIn.SetUINT32(ref independentKey, 1);
                ThrowOnFailure(_writer.SetInputMediaType(_videoStream, videoIn, IntPtr.Zero),
                    "SetInputMediaType(video)");
            }
            finally
            {
                Marshal.ReleaseComObject(videoIn);
            }

            if (includeAudio)
            {
                var audioOut = CreateAudioType(MfAudioFormatAac);
                try
                {
                    var bytesKey = MfMtAudioAvgBytesPerSecond;
                    audioOut.SetUINT32(ref bytesKey, AacBytesPerSecond);
                    ThrowOnFailure(_writer.AddStream(audioOut, out _audioStream), "AddStream(audio)");
                }
                finally
                {
                    Marshal.ReleaseComObject(audioOut);
                }

                var audioIn = CreateAudioType(MfAudioFormatPcm);
                try
                {
                    var blockKey = MfMtAudioBlockAlignment;
                    audioIn.SetUINT32(ref blockKey, WasapiMicrophoneCapture.BytesPerFrame);
                    var avgKey = MfMtAudioAvgBytesPerSecond;
                    audioIn.SetUINT32(ref avgKey,
                        WasapiMicrophoneCapture.SampleRate * WasapiMicrophoneCapture.BytesPerFrame);
                    ThrowOnFailure(_writer.SetInputMediaType(_audioStream, audioIn, IntPtr.Zero),
                        "SetInputMediaType(audio)");
                }
                finally
                {
                    Marshal.ReleaseComObject(audioIn);
                }
            }
        }
        catch
        {
            Marshal.ReleaseComObject(_writer);
            _writer = null;
            throw;
        }
    }

    /// <summary>Starts accepting samples.</summary>
    internal void Begin()
    {
        MtaThread.Run(() =>
        {
            lock (_writerLock)
            {
                ThrowOnFailure(_writer.BeginWriting(), "BeginWriting");
                _writing = true;
            }
        });
    }

    /// <summary>
    /// Encodes one BGRA video frame. Copies the pixels before returning, so the
    /// caller's buffer can be reused immediately.
    /// </summary>
    /// <param name="pixels">Pointer to the top-left pixel (top-down rows).</param>
    /// <param name="sourcePitchBytes">The source's bytes per scanline (at least width * 4).</param>
    /// <param name="timestampHns">Presentation time in 100-ns units on the recording's zero-based clock.</param>
    /// <returns>True if the frame was written; false when the recorder is not accepting samples.</returns>
    internal bool WriteVideoFrame(IntPtr pixels, uint sourcePitchBytes, long timestampHns)
    {
        var packedPitch = _width * 4;
        var frameBytes = packedPitch * _height;

        lock (_writerLock)
        {
            if (!_writing || _finalized)
            {
                return false;
            }

            ThrowOnFailure(MFCreateMemoryBuffer(frameBytes, out var buffer), "MFCreateMemoryBuffer");
            try
            {
                ThrowOnFailure(buffer.Lock(out var target, out _, out _), "buffer.Lock");
                unsafe
                {
                    if (sourcePitchBytes == packedPitch)
                    {
                        Buffer.MemoryCopy((void*)pixels, (void*)target, frameBytes, frameBytes);
                    }
                    else
                    {
                        for (var y = 0; y < _height; y++)
                        {
                            Buffer.MemoryCopy(
                                (byte*)pixels + ((long)y * sourcePitchBytes),
                                (byte*)target + ((long)y * packedPitch),
                                packedPitch, packedPitch);
                        }
                    }
                }
                buffer.Unlock();
                buffer.SetCurrentLength(frameBytes);

                ThrowOnFailure(MFCreateSample(out var sample), "MFCreateSample");
                try
                {
                    sample.AddBuffer(buffer);
                    sample.SetSampleTime(timestampHns);
                    sample.SetSampleDuration(10_000_000L / _frameRate);
                    ThrowOnFailure(_writer.WriteSample(_videoStream, sample), "WriteSample(video)");
                }
                finally
                {
                    Marshal.ReleaseComObject(sample);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(buffer);
            }
            return true;
        }
    }

    /// <summary>
    /// Encodes a packet of PCM audio (the fixed WASAPI capture format). Timestamps are
    /// derived from the running sample count, which shares the zero-based clock with
    /// the video as long as capture starts when recording starts.
    /// </summary>
    /// <param name="samples">The PCM bytes.</param>
    /// <param name="byteCount">How many bytes of <paramref name="samples"/> are valid.</param>
    internal void WriteAudio(byte[] samples, int byteCount)
    {
        if (!_hasAudio || byteCount <= 0)
        {
            return;
        }
        var frames = byteCount / WasapiMicrophoneCapture.BytesPerFrame;

        lock (_writerLock)
        {
            if (!_writing || _finalized)
            {
                return;
            }

            ThrowOnFailure(MFCreateMemoryBuffer((uint)byteCount, out var buffer), "MFCreateMemoryBuffer");
            try
            {
                ThrowOnFailure(buffer.Lock(out var target, out _, out _), "buffer.Lock");
                Marshal.Copy(samples, 0, target, byteCount);
                buffer.Unlock();
                buffer.SetCurrentLength((uint)byteCount);

                ThrowOnFailure(MFCreateSample(out var sample), "MFCreateSample");
                try
                {
                    sample.AddBuffer(buffer);
                    sample.SetSampleTime(_audioFramesWritten * 10_000_000L / WasapiMicrophoneCapture.SampleRate);
                    sample.SetSampleDuration((long)frames * 10_000_000L / WasapiMicrophoneCapture.SampleRate);
                    ThrowOnFailure(_writer.WriteSample(_audioStream, sample), "WriteSample(audio)");
                    _audioFramesWritten += frames;
                }
                finally
                {
                    Marshal.ReleaseComObject(sample);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(buffer);
            }
        }
    }

    /// <summary>Drains the encoder and finalizes the MP4 (moov atom written here).</summary>
    /// <returns>True on clean finalization.</returns>
    internal bool Finish()
    {
        return MtaThread.Run(() =>
        {
            lock (_writerLock)
            {
                if (_finalized || !_writing)
                {
                    _finalized = true;
                    return false;
                }
                _finalized = true;
                var hr = _writer.Finalize_();
                return hr >= 0;
            }
        });
    }

    public void Dispose()
    {
        MtaThread.Run(() =>
        {
            lock (_writerLock)
            {
                if (_writer != null)
                {
                    if (_writing && !_finalized)
                    {
                        _writer.Finalize_();
                        _finalized = true;
                    }
                    Marshal.ReleaseComObject(_writer);
                    _writer = null;
                }
            }
        });
    }

    private IMFMediaType CreateVideoType(Guid subtype)
    {
        ThrowOnFailure(MFCreateMediaType(out var type), "MFCreateMediaType");
        var majorKey = MfMtMajorType;
        var videoMajor = MfMediaTypeVideo;
        type.SetGUID(ref majorKey, ref videoMajor);
        var subtypeKey = MfMtSubtype;
        type.SetGUID(ref subtypeKey, ref subtype);
        var sizeKey = MfMtFrameSize;
        type.SetUINT64(ref sizeKey, ((ulong)_width << 32) | _height);
        var rateKey = MfMtFrameRate;
        type.SetUINT64(ref rateKey, ((ulong)_frameRate << 32) | 1);
        var interlaceKey = MfMtInterlaceMode;
        type.SetUINT32(ref interlaceKey, MfVideoInterlaceProgressive);
        return type;
    }

    private static IMFMediaType CreateAudioType(Guid subtype)
    {
        ThrowOnFailure(MFCreateMediaType(out var type), "MFCreateMediaType");
        var majorKey = MfMtMajorType;
        var audioMajor = MfMediaTypeAudio;
        type.SetGUID(ref majorKey, ref audioMajor);
        var subtypeKey = MfMtSubtype;
        type.SetGUID(ref subtypeKey, ref subtype);
        var bitsKey = MfMtAudioBitsPerSample;
        type.SetUINT32(ref bitsKey, WasapiMicrophoneCapture.BitsPerSample);
        var ratesKey = MfMtAudioSamplesPerSecond;
        type.SetUINT32(ref ratesKey, WasapiMicrophoneCapture.SampleRate);
        var channelsKey = MfMtAudioNumChannels;
        type.SetUINT32(ref channelsKey, WasapiMicrophoneCapture.ChannelCount);
        return type;
    }
}
