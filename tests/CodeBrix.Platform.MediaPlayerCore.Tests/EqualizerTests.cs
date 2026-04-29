#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;
using System;

namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public class EqualizerTests : BaseSetup
{
    [Fact]
    public void BasicNativeCallTest()
    {
        var equalizer = new Equalizer();
        equalizer.SetAmp(-1, 1);
        (-1 == equalizer.Amp(1)).Should().BeTrue();
    }

    [Fact]
    public void DisposeEqualizer()
    {
        var equalizer = new Equalizer();
        equalizer.SetAmp(-1, 1);
        equalizer.Dispose();
        (IntPtr.Zero == equalizer.NativeReference).Should().BeTrue();
    }
}
