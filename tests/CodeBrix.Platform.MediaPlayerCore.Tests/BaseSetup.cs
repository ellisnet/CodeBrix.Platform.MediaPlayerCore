#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;

namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public abstract class BaseSetup
{
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable. It is initialized in the SetUp, so before the tests take place.
    protected LibVLC _libVLC;
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

    protected BaseSetup()
    {
        _libVLC = new LibVLC("--no-audio", "--no-video");
    }

    protected string RealStreamMediaPath => "http://streams.videolan.org/streams/mp3/Owner-MPEG2.5.mp3";

    protected string RealMp3Path => Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "sample.mp3");

    protected string RealMp3PathSpecialCharacter => Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "motörhead.mp3");

    // Used by tests that are only meaningful on Windows (e.g. tests that query
    // the `mmdevice` audio output, which only exists in libvlc's Windows build).
    // Apply via: [Fact(SkipUnless = nameof(IsWindows), SkipType = typeof(BaseSetup))]
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    // Used by tests that drive REAL media playback, pull LIVE network streams, or
    // use libvlc's LAN/network discovery. Those tests need a working audio/video
    // output pipeline and/or outbound network + discovery services. On a headless
    // host (CI, headless macOS/Linux) libvlc's
    // Playing/EndReached/StateChanged events may never fire -- and because the
    // event-driven tests wait on those events, they would otherwise BLOCK FOREVER
    // (a single such test hung a clone-and-test run for ~3 hours). They are
    // therefore opt-in: set the environment variable below to "1" (or "true") on
    // a machine that has a desktop session AND network before running them.
    //   MEDIAPLAYERCORE_RUN_PLAYBACK_TESTS=1
    // Apply via:
    //   [Fact(Skip = "Needs real A/V output + network; set MEDIAPLAYERCORE_RUN_PLAYBACK_TESTS=1 to run",
    //         SkipUnless = nameof(BaseSetup.CanRunMediaPlaybackTests), SkipType = typeof(BaseSetup))]
    public static bool CanRunMediaPlaybackTests
        => Environment.GetEnvironmentVariable("MEDIAPLAYERCORE_RUN_PLAYBACK_TESTS") is "1" or "true";

    // Awaits a libvlc event (signalled via a TaskCompletionSource) with a HARD
    // timeout, so a stalled playback pipeline surfaces as a fast, clear failure
    // instead of hanging the whole test run. Use this in place of a bare
    // `await tcs.Task` for any wait that depends on a media/playback event.
    protected static async Task AwaitMediaEventAsync(Task<bool> eventSignal, int timeoutSeconds = 15)
    {
        var winner = await Task.WhenAny(eventSignal, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
        if (winner != eventSignal)
        {
            throw new TimeoutException(
                $"Expected libvlc media event did not fire within {timeoutSeconds}s " +
                "(no playback pipeline / network on this host?).");
        }
        (await eventSignal).Should().BeTrue();
    }
}
