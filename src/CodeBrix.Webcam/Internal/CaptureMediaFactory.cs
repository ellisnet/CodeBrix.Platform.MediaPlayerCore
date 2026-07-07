using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Platform.MediaPlayerCore;
using CodeBrix.Webcam.Devices;

namespace CodeBrix.Webcam.Internal;

/// <summary>
/// Builds the libvlc capture <see cref="Media"/> for a camera: the per-platform MRL
/// (v4l2:// on Linux, dshow:// on Windows, avcapture:// on macOS), the mode options
/// from <see cref="WebcamSessionOptions"/>, the audio input-slave when a microphone is
/// captured, and an optional stream-output chain.
/// </summary>
internal static class CaptureMediaFactory
{
    internal static Media Build(IImagingMediaDevice device, WebcamSessionOptions options,
        string audioDeviceId, bool forceMjpeg, string soutChain)
    {
        var mediaOptions = new List<string>();
        string mrl;

        if (OperatingSystem.IsWindows())
        {
            mrl = "dshow://";
            mediaOptions.Add(":dshow-vdev=" + device.FriendlyName);
            mediaOptions.Add(":dshow-adev=" + (audioDeviceId ?? "none"));
            if (options.RequestedWidth > 0 && options.RequestedHeight > 0)
            {
                mediaOptions.Add(FormattableString.Invariant(
                    $":dshow-size={options.RequestedWidth}x{options.RequestedHeight}"));
            }
            if (options.RequestedFrameRate > 0)
            {
                mediaOptions.Add(":dshow-fps=" + options.RequestedFrameRate.ToString(CultureInfo.InvariantCulture));
            }
            var dshowChroma = forceMjpeg ? "MJPG" : FourCcForRequest(options.PreferredFormat);
            if (dshowChroma != null)
            {
                mediaOptions.Add(":dshow-chroma=" + dshowChroma);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            mrl = "v4l2://" + device.Id;
            if (options.RequestedWidth > 0)
            {
                mediaOptions.Add(":v4l2-width=" + options.RequestedWidth);
            }
            if (options.RequestedHeight > 0)
            {
                mediaOptions.Add(":v4l2-height=" + options.RequestedHeight);
            }
            if (options.RequestedFrameRate > 0)
            {
                mediaOptions.Add(":v4l2-fps=" + options.RequestedFrameRate.ToString(CultureInfo.InvariantCulture));
            }
            var chroma = forceMjpeg ? "MJPG" : FourCcForRequest(options.PreferredFormat);
            if (chroma != null)
            {
                mediaOptions.Add(":v4l2-chroma=" + chroma);
            }
            if (audioDeviceId != null)
            {
                mediaOptions.Add(":input-slave=alsa://" + audioDeviceId);
            }
        }
        else
        {
            // macOS: avcapture accepts the AVCaptureDevice uniqueID and negotiates the
            // native format itself — it has no size/fps/chroma options to pass.
            mrl = "avcapture://" + device.Id;
            if (audioDeviceId != null)
            {
                mediaOptions.Add(":input-slave=qtsound://" + audioDeviceId);
            }
        }

        mediaOptions.Add(":live-caching=" + Math.Max(0, options.LiveCachingMs));
        if (soutChain != null)
        {
            mediaOptions.Add(":sout=" + soutChain);
            // Keep the sout alive across the play item; without this some chains tear
            // down early on end-of-stream races.
            mediaOptions.Add(":sout-keep");
        }

        return new Media(WebcamEngine.Shared, mrl, FromType.FromLocation, mediaOptions.ToArray());
    }

    /// <summary>Resolves which audio device the session should capture, or null for none.</summary>
    internal static string ResolveAudioDevice(IImagingMediaDevice device, WebcamSessionOptions options)
        => options.AudioCapture switch
        {
            AudioCaptureMode.Off => null,
            AudioCaptureMode.SpecificDevice => string.IsNullOrEmpty(options.AudioDeviceId)
                ? throw new WebcamException(
                    "AudioCapture is SpecificDevice but WebcamSessionOptions.AudioDeviceId is empty.")
                : options.AudioDeviceId,
            _ => device.PairedMicrophone?.DeviceId, // Auto: camera's own mic or silently none
        };

    private static string FourCcForRequest(ImagingPixelFormat format) => format switch
    {
        ImagingPixelFormat.Mjpeg => "MJPG",
        ImagingPixelFormat.Yuyv => "YUY2",
        ImagingPixelFormat.Nv12 => "NV12",
        ImagingPixelFormat.H264 => "H264",
        _ => null,
    };
}
