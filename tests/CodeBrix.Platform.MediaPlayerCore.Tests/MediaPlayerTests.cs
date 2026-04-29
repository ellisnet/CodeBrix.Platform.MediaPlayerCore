#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;

namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public class MediaPlayerTests : BaseSetup
{
    [Fact]
    public void CreateAndDestroy()
    {
        var mp = new MediaPlayer(_libVLC);
        mp.Dispose();
        (IntPtr.Zero == mp.NativeReference).Should().BeTrue();
    }

    [Fact]
    public void OutputDeviceEnum()
    {
        var mp = new MediaPlayer(_libVLC);
        var t = mp.AudioOutputDeviceEnum;
        Debug.WriteLine(t);
    }
    
    [Fact]
    public async Task TrackDescription()
    {
        var mp = new MediaPlayer(_libVLC);
        var media = new Media(_libVLC, new Uri(RealStreamMediaPath));
        var tcs = new TaskCompletionSource<bool>();
        
        mp.Media = media;
        mp.Play();
        mp.Playing += (sender, args) =>
        {
            var description = mp.AudioTrackDescription;
            (mp.SetAudioTrack(description.First().Id)).Should().BeTrue();
            (description).Should().NotBeEmpty();
            tcs.SetResult(true);
        };
        (await tcs.Task).Should().BeTrue();
    }

    [Fact]
    public async Task ChapterDescriptions()
    {
        var mp = new MediaPlayer(_libVLC);
        var media = new Media(_libVLC, "https://auphonic.com/media/blog/auphonic_chapters_demo.m4a", FromType.FromLocation);
        var tcs = new TaskCompletionSource<bool>();

        mp.Media = media;
        mp.Play();
        mp.Playing += (sender, args) =>
        {
            var chapters = mp.FullChapterDescriptions(-1);
            (chapters).Should().NotBeEmpty();
            (chapters.Length == mp.ChapterCount).Should().BeTrue();
            tcs.SetResult(true);
        };
        (await tcs.Task).Should().BeTrue();
    }

    [Fact]
    public async Task Play()
    {
        var media = new Media(_libVLC, new Uri(RealStreamMediaPath));
        var mp = new MediaPlayer(media);
        var called = false;
        mp.Playing += (sender, args) =>
        {
            called = true;
        };
        mp.Play();
        await Task.Delay(5000, TestContext.Current.CancellationToken);
        (called).Should().BeTrue();
        //(mp.IsPlaying).Should().BeTrue();
    }

    int callCountRegisterOne = 0;
    int callCountRegisterTwo = 0;

    [Fact]
    public async Task EventFireOnceForeachRegistration()
    {
        try
        {
            var media = new Media(_libVLC, new Uri(RealStreamMediaPath));
            var mp = new MediaPlayer(media);
            
            mp.Playing += Mp_Playing;
            mp.Playing += Mp_Playing1;

            Debug.WriteLine("first play");

            mp.Play();
            await Task.Delay(2000, TestContext.Current.CancellationToken);
            (callCountRegisterOne == 1).Should().BeTrue();
            (callCountRegisterTwo == 1).Should().BeTrue();
        
            callCountRegisterOne = 0;
            callCountRegisterTwo = 0;

            mp.Stop();

            mp.Playing -= Mp_Playing;

        
            Debug.WriteLine("second play");

            mp.Play();
            await Task.Delay(2000, TestContext.Current.CancellationToken);

            (callCountRegisterOne == 0).Should().BeTrue();
            (callCountRegisterTwo == 1).Should().BeTrue();

          //  mp.Stop();

            mp.Playing -= Mp_Playing1; // native crash in detach?



            callCountRegisterOne = 0;
            callCountRegisterTwo = 0;


            Debug.WriteLine("third play");

            mp.Play();
            await Task.Delay(500, TestContext.Current.CancellationToken);

            (callCountRegisterOne == 0).Should().BeTrue();
            (callCountRegisterTwo == 0).Should().BeTrue();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    void Mp_Playing1(object sender, EventArgs e)
    {
        callCountRegisterTwo++;
        Debug.WriteLine($"Mp_Playing1 called with {callCountRegisterTwo}");

    }

    void Mp_Playing(object sender, EventArgs e)
    {
        callCountRegisterOne++;
        Debug.WriteLine($"Mp_Playing called with {callCountRegisterOne}");
    }

    [Fact]
    public async Task DisposeMediaPlayer()
    {
        var mp = new MediaPlayer(_libVLC);

        mp.Play(new Media(_libVLC, new Uri(RealStreamMediaPath)));

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        mp.Dispose();

        (IntPtr.Zero == mp.NativeReference).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateViewpoint()
    {
        var mp = new MediaPlayer(_libVLC);

        mp.Play(new Media(_libVLC, "https://streams.videolan.org/streams/360/eagle_360.mp4", FromType.FromLocation));

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        var result = mp.UpdateViewpoint(yaw: 0, pitch: 90, roll: 0, fov: 0);

        (result).Should().BeTrue();

        await Task.Delay(1000, TestContext.Current.CancellationToken);
        
        mp.Dispose();

        (IntPtr.Zero == mp.NativeReference).Should().BeTrue();
    }

    [Fact]
    public void GetMediaPlayerRole()
    {
        var mp = new MediaPlayer(_libVLC);
        (MediaPlayerRole.Video == mp.Role).Should().BeTrue();
    }

    [Fact]
    public void SetMediaPlayerRole()
    {
        var mp = new MediaPlayer(_libVLC);
        (MediaPlayerRole.Video == mp.Role).Should().BeTrue();

        (mp.SetRole(MediaPlayerRole.None)).Should().BeTrue();
        (MediaPlayerRole.None == mp.Role).Should().BeTrue();
    }
}
