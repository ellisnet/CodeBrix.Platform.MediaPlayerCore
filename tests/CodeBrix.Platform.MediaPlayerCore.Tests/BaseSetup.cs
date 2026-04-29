#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
}
