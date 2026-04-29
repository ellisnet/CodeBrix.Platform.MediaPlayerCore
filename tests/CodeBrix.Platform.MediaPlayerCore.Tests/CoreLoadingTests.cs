#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;
using System;
using System.IO;

namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public class CoreLoadingTests
{
    // Windows-only: the test path uses a `libvlc/win-x86` subfolder, which is
    // the Windows libvlc layout. On Linux/macOS, Core.Initialize(path) throws
    // InvalidOperationException for any non-null path argument.
    [Fact(Skip = "Windows-only: uses a Windows libvlc/win-x86 directory layout",
          SkipUnless = nameof(BaseSetup.IsWindows), SkipType = typeof(BaseSetup))]
    public void LoadLibVLCFromSpecificPath()
    {
        var dirPath = Path.GetDirectoryName(typeof(CoreLoadingTests).Assembly.Location)!;
        var finalPath = Path.Combine(dirPath, "libvlc", "win-x86");

        ((Action)(() => Core.Initialize(finalPath))).Should().NotThrow();
        var libVLC = new LibVLC("--no-audio", "--no-video");
    }

    [Fact]
    public void LoadLibVLCFromInferredPath()
    {
        ((Action)(() => Core.Initialize())).Should().NotThrow();
        var libVLC = new LibVLC("--no-audio", "--no-video");
    }
}
