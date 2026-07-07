using System;
using System.IO;
using System.Runtime.Versioning;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// The Windows <see cref="IAudioSidecar"/>: records a microphone to a sidecar WAV file
/// (16-bit PCM) via WASAPI, alongside a frame-path video recording. The WAV header
/// sizes are patched on <see cref="Stop"/>, so the file is valid only after that.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WavSidecarRecorder : IAudioSidecar
{
    private readonly WasapiMicrophoneCapture _capture;
    private readonly FileStream _file;
    private readonly object _writeLock = new object();
    private long _dataBytes;
    private bool _stopped;
    private bool _disposed;

    /// <summary>Opens the WAV file and starts capturing the microphone into it.</summary>
    /// <param name="microphoneFriendlyName">The microphone's friendly name.</param>
    /// <param name="outputPath">The WAV file to write.</param>
    internal WavSidecarRecorder(string microphoneFriendlyName, string outputPath)
    {
        OutputPath = outputPath;
        _file = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        WriteHeader(0);
        try
        {
            _capture = new WasapiMicrophoneCapture(microphoneFriendlyName, OnSamples);
            StartedAtUtc = DateTime.UtcNow;
            _capture.Start();
        }
        catch
        {
            _file.Dispose();
            File.Delete(outputPath);
            throw;
        }
    }

    /// <inheritdoc/>
    public string OutputPath { get; }

    /// <inheritdoc/>
    public DateTime StartedAtUtc { get; private set; }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_writeLock)
        {
            if (_stopped)
            {
                return;
            }
            _stopped = true;
        }
        _capture.Stop();
        lock (_writeLock)
        {
            _file.Position = 0;
            WriteHeader(_dataBytes);
            _file.Flush();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
        _capture.Dispose();
        _file.Dispose();
    }

    private void OnSamples(byte[] buffer, int bytes)
    {
        lock (_writeLock)
        {
            if (_stopped)
            {
                return;
            }
            _file.Write(buffer, 0, bytes);
            _dataBytes += bytes;
        }
    }

    private void WriteHeader(long dataBytes)
    {
        Span<byte> header = stackalloc byte[44];
        "RIFF"u8.CopyTo(header);
        BitConverter.TryWriteBytes(header.Slice(4), (uint)(36 + dataBytes));
        "WAVE"u8.CopyTo(header.Slice(8));
        "fmt "u8.CopyTo(header.Slice(12));
        BitConverter.TryWriteBytes(header.Slice(16), 16u); // PCM fmt chunk size
        BitConverter.TryWriteBytes(header.Slice(20), (ushort)1); // PCM
        BitConverter.TryWriteBytes(header.Slice(22), WasapiMicrophoneCapture.ChannelCount);
        BitConverter.TryWriteBytes(header.Slice(24), WasapiMicrophoneCapture.SampleRate);
        BitConverter.TryWriteBytes(header.Slice(28),
            WasapiMicrophoneCapture.SampleRate * WasapiMicrophoneCapture.BytesPerFrame);
        BitConverter.TryWriteBytes(header.Slice(32), WasapiMicrophoneCapture.BytesPerFrame);
        BitConverter.TryWriteBytes(header.Slice(34), WasapiMicrophoneCapture.BitsPerSample);
        "data"u8.CopyTo(header.Slice(36));
        BitConverter.TryWriteBytes(header.Slice(40), (uint)dataBytes);
        _file.Write(header);
    }
}
