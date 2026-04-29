#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.IO;
using System.Linq;
using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;

namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public class MediaListTests : BaseSetup
{
    [Fact]
    public void AddAndRemoveMediaFromMediaList()
    {
        var mediaList = new MediaList(_libVLC);
        var media = new Media(_libVLC, Path.GetTempFileName());
        var itemAdded = false;
        var itemDeleted = false;
        mediaList.ItemAdded += (sender, args) => itemAdded = true;
        mediaList.ItemDeleted += (sender, args) => itemDeleted = true;
        mediaList.AddMedia(media);
        (media.NativeReference == mediaList.First().NativeReference).Should().BeTrue();
        (1 == mediaList.Count).Should().BeTrue();
        (itemAdded).Should().BeTrue();
        (mediaList.IndexOf(media)).Should().Be(0);
        mediaList.RemoveIndex(0);
        (mediaList.Count).Should().Be(0);
        (itemDeleted).Should().BeTrue();
    }

    [Fact]
    public void DisposeMediaList()
    {
        var mediaList = new MediaList(_libVLC);
        mediaList.Dispose();
        (IntPtr.Zero == mediaList.NativeReference).Should().BeTrue();
    }
}
