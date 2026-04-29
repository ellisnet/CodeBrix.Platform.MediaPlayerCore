#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;

namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public class LibVLCTests : BaseSetup
{
    [Fact]
    public void DisposeInstanceNativeRelease()
    {
        _libVLC.Dispose();
        (IntPtr.Zero == _libVLC.NativeReference).Should().BeTrue();
    }

    [Fact]
    public void AudioFilters()
    {
        var audioFilters = _libVLC.AudioFilters;
        foreach (var filter in audioFilters)
        {
            Debug.WriteLine(filter.Help);
            Debug.WriteLine(filter.LongName);
            Debug.WriteLine(filter.Name);
            Debug.WriteLine(filter.ShortName);
        }
    }

    [Fact]
    public void VideoFilters()
    {
        var videoFilters = _libVLC.VideoFilters;
        foreach (var filter in videoFilters)
        {
            Debug.WriteLine(filter.LongName);
            Debug.WriteLine(filter.Name);
            Debug.WriteLine(filter.ShortName);
        }
    }

    [Fact]
    public void AudioOutputs()
    {
        var audioOuputs = _libVLC.AudioOutputs;
        foreach (var audioOutput in audioOuputs)
        {
            Debug.WriteLine(audioOutput.Name);
            Debug.WriteLine(audioOutput.Description);
        }
    }

    // "mmdevice" is libvlc's Windows Media Device (WASAPI) audio output;
    // it does not exist on Linux / macOS builds of libvlc, so this test
    // is Windows-only.
    [Fact(Skip = "Windows-only: requires libvlc's Windows 'mmdevice' (WASAPI) audio output",
          SkipUnless = nameof(BaseSetup.IsWindows), SkipType = typeof(BaseSetup))]
    public void AudioOutputDevices()
    {
        var outputs = _libVLC.AudioOutputs;
        var name = outputs.First(output => output.Name.Equals("mmdevice")).Name;
        var audioOutputDevices = _libVLC.AudioOutputDevices(name);

        foreach (var audioOutputDevice in audioOutputDevices)
        {
            Debug.WriteLine(audioOutputDevice.Description);
            Debug.WriteLine(audioOutputDevice.DeviceIdentifier);
        }
    }

    [Fact]
    public void EqualityTests()
    {
        (new LibVLC("--no-audio")).Should().NotBeSameAs(new LibVLC("--no-audio"));
    }

    [Fact]
    public void Categories()
    {
        var md1 = _libVLC.MediaDiscoverers(MediaDiscovererCategory.Devices);
        var md2 = _libVLC.MediaDiscoverers(MediaDiscovererCategory.Lan);
        var md3 = _libVLC.MediaDiscoverers(MediaDiscovererCategory.Localdirs);
    }

    [Fact]
    public void SetExitHandler()
    {
        var exitCb = new ExitCallback(() =>
        {
        });

        _libVLC.SetExitHandler(exitCb);
    }

    [Fact]
    public void SetLogFile()
    {
        var path = Path.GetTempFileName();
        _libVLC.SetLogFile(path);
        _libVLC.CloseLogFile();
        var logs = File.ReadAllText(path);
        (logs.StartsWith("VLC media player")).Should().BeTrue();
    }

    [Fact]
    public void DisposeLibVLC()
    {
        _libVLC.SetDialogHandlers((title, text) => Task.CompletedTask,
            (dialog, title, text, defaultUsername, askStore, token) => Task.CompletedTask,
            (dialog, title, text, type, cancelText, firstActionText, secondActonText, token) => Task.CompletedTask,
            (dialog, title, text, indeterminate, position, cancelText, token) => Task.CompletedTask,
            (dialog, position, text) => Task.CompletedTask);

        (_libVLC.DialogHandlersSet).Should().BeTrue();

        _libVLC.Dispose();

        (IntPtr.Zero == _libVLC.NativeReference).Should().BeTrue();
        (_libVLC.DialogHandlersSet).Should().BeFalse();
    }

    [Fact]
    public void LibVLCVersion()
    {
        (_libVLC.Version.StartsWith("3")).Should().BeTrue();
    }

    [Fact]
    public void LibVLCChangeset()
    {
        (_libVLC.Changeset).Should().NotBeNull();
    }
}
