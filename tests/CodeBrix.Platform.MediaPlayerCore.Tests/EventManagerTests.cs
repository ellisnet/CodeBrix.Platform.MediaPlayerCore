#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;

namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public class EventManagerTests : BaseSetup
{
    [Fact]
    public void MetaChangedEventSubscribe()
    {
        var media = new Media(_libVLC, Path.GetTempFileName());
        var eventHandlerCalled = false;
        const MetadataType description = MetadataType.Description;
        media.MetaChanged += (sender, args) =>
        {
            (description == args.MetadataType).Should().BeTrue();
            eventHandlerCalled = true;
        };
        media.SetMeta(MetadataType.Description, "test");
        (eventHandlerCalled).Should().BeTrue();
    }
    
    private async Task DurationChanged()
    {
        var media = new Media(_libVLC, RealMp3Path);
        var called = false;
        long duration = 0;

        media.DurationChanged += (sender, args) =>
        {
            called = true;
            duration = args.Duration;
        };

        await media.Parse(cancellationToken: TestContext.Current.CancellationToken);

        (called).Should().BeTrue();
        (duration).Should().NotBe(0);
    }

    [Fact]
    public void FreedMedia()
    {
        var media = new Media(_libVLC, RealMp3Path);
        var eventCalled = false;
        media.MediaFreed += (sender, args) =>
        {
            eventCalled = true;
        };

        media.Dispose();

        (eventCalled).Should().BeTrue();
    }

    [Fact]
    public async Task OpeningStateChanged()
    {
        var media = new Media(_libVLC, RealMp3Path);
        var tcs = new TaskCompletionSource<bool>();
        var openingCalled = false;
        media.StateChanged += (sender, args) =>
        {
            if (media.State == VLCState.Opening)
            {
                openingCalled = true;
                tcs.SetResult(true);
                return;
            }
        };

        var mp = new MediaPlayer(media);
        mp.Play();
        (await tcs.Task).Should().BeTrue();
        (openingCalled).Should().BeTrue();
    }
}
