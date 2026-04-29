#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;

namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public class MediaDiscovererTests : BaseSetup
{
    [Fact]
    public void CreateStartAndStopDiscoverer()
    {
        var mds = _libVLC.MediaDiscoverers(MediaDiscovererCategory.Lan);
        var md = new MediaDiscoverer(_libVLC, mds.First().Name!);
        (md.Start()).Should().BeTrue();
        (md.IsRunning).Should().BeTrue();
        md.Stop();
        (md.IsRunning).Should().BeFalse();
    }

    [Fact]
    public async Task DisposeMediaDiscoverer()
    {
        var mds = _libVLC.MediaDiscoverers(MediaDiscovererCategory.Lan);
        var md = new MediaDiscoverer(_libVLC, mds.First().Name!);
        (md.Start()).Should().BeTrue();
        (md.IsRunning).Should().BeTrue();
        (md.MediaList).Should().NotBeNull();
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        foreach(var media in md.MediaList!)
        {
            Debug.WriteLine(media.Mrl);
        }
        md.Dispose();
        (md.MediaList).Should().BeNull();
        (md.IsRunning).Should().BeFalse();
        (IntPtr.Zero == md.NativeReference).Should().BeTrue();
    }
}
