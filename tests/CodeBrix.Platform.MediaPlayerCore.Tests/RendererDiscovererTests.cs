#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;
using static System.Diagnostics.Debug;
namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public class RendererDiscovererTests : BaseSetup
{
    // This test depends on both accepting the network access request made by the test runner 
    // and having a chromecast on the same local network.
    [Fact(Skip = "requires network calls that may fail when run from CI")]
    public async Task DiscoverItems()
    {
        var mp = new MediaPlayer(_libVLC)
        {
            Media = new Media(_libVLC, "http://www.quirksmode.org/html5/videos/big_buck_bunny.mp4",
                FromType.FromLocation)
        };

        (mp.Play()).Should().BeTrue();

        var rendererList = _libVLC.RendererList;
        (rendererList).Should().NotBeEmpty();

        var rendererDiscoverer = new RendererDiscoverer(_libVLC, _libVLC.RendererList.LastOrDefault().Name);
        var rendererItems = new List<RendererItem>();
        var tcs = new TaskCompletionSource<bool>();

        rendererDiscoverer.ItemAdded += (sender, args) =>
        {
            WriteLine($"New item discovered: {args.RendererItem.Name} of type {args.RendererItem.Type}");
            if (args.RendererItem.CanRenderVideo)
                WriteLine("Can render video");
            if (args.RendererItem.CanRenderAudio)
                WriteLine("Can render audio");

            rendererItems.Add(args.RendererItem);
            
            tcs.SetResult(true);
        };


        (rendererDiscoverer.Start()).Should().BeTrue();

        (await tcs.Task).Should().BeTrue();
        (rendererItems.Any()).Should().BeTrue();
        (mp.SetRenderer(rendererItems.First())).Should().BeTrue();

        await Task.Delay(10000, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void DisposeRendererDiscoverer()
    {
        var rendererDiscoverer = new RendererDiscoverer(_libVLC, _libVLC.RendererList.LastOrDefault().Name);
        rendererDiscoverer.Dispose();
        (IntPtr.Zero == rendererDiscoverer.NativeReference).Should().BeTrue();
    }
}
