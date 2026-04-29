#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;

namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public class MediaTests : BaseSetup
{
    [Fact]
    public void CreateMedia()
    {
        using var media = new Media(_libVLC, Path.GetTempFileName());
        (media.NativeReference).Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void CreateMediaFromUri()
    {
        var media = new Media(_libVLC, new Uri(RealStreamMediaPath, UriKind.Absolute));
        (media.NativeReference).Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void CreateMediaFail()
    {
        ((Action)(() => new Media(null!, Path.GetTempFileName()))).Should().Throw<ArgumentNullException>();
        ((Action)(() => new Media(_libVLC, string.Empty))).Should().Throw<ArgumentNullException>();
        ((Action)(() => new Media(_libVLC, new Uri("/hello.mp4", UriKind.Relative)))).Should().Throw<InvalidOperationException>();
        ((Action)(() => new Media(_libVLC, uri: null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ReleaseMedia()
    {
        var media = new Media(_libVLC, Path.GetTempFileName());

        media.Dispose();

        (IntPtr.Zero == media.NativeReference).Should().BeTrue();
    }

    [Fact]
    public void CreateMediaFromStream()
    {
        using var stream = new FileStream(Path.GetTempFileName(), FileMode.OpenOrCreate);
        using var input = new StreamMediaInput(stream);
        using var media = new Media(_libVLC, input);
        (media.NativeReference).Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void AddOption()
    {
        using var stream = new FileStream(Path.GetTempFileName(), FileMode.OpenOrCreate);
        using var input = new StreamMediaInput(stream);
        using var media = new Media(_libVLC, input);
        media.AddOption("-sout-all");
    }

    [Fact]
    public async Task CreateRealMedia()
    {
        using (var media = new Media(_libVLC, RealStreamMediaPath, FromType.FromLocation))
        {
            (media.Duration).Should().NotBe(0);
            using (var mp = new MediaPlayer(media))
            {
                (mp.Play()).Should().BeTrue();
                await Task.Delay(4000, TestContext.Current.CancellationToken); // have to wait a bit for statistics to populate
                (media.Statistics.DemuxBitrate).Should().BeGreaterThan(0);
                mp.Stop();
            }
        }
    }

    [Fact]
    public async Task CreateRealMediaFromUri()
    {
        using (var media = new Media(_libVLC, new Uri(RealStreamMediaPath, UriKind.Absolute)))
        {
            (media.Duration).Should().NotBe(0);
            using (var mp = new MediaPlayer(media))
            {
                (mp.Play()).Should().BeTrue();
                await Task.Delay(4000, TestContext.Current.CancellationToken); // have to wait a bit for statistics to populate
                (media.Statistics.DemuxBitrate).Should().BeGreaterThan(0);
                mp.Stop();
            }
        }
    }

    [Fact]
    public void Duplicate()
    {
        using var stream = new FileStream(Path.GetTempFileName(), FileMode.OpenOrCreate);
        using var input = new StreamMediaInput(stream);
        using var media = new Media(_libVLC, input);
        var duplicate = media.Duplicate();
        (media.NativeReference).Should().NotBe(duplicate.NativeReference);
    }

    [Fact]
    public void CreateMediaFromFileStream()
    {
        using var stream = new FileStream(RealMp3Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var input = new StreamMediaInput(stream);
        using var media = new Media(_libVLC, input);
        (media.NativeReference).Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void SetMetadata()
    {
        using var media = new Media(_libVLC, Path.GetTempFileName());
        const string test = "test";
        media.SetMeta(MetadataType.ShowName, test);
        (media.SaveMeta()).Should().BeTrue();
        (test == media.Meta(MetadataType.ShowName)).Should().BeTrue();
    }

    [Fact(Skip = "Crashes test runner on CI")]
    public async Task GetTracks()
    {
        using var media = new Media(_libVLC, RealMp3Path);
        await media.Parse(cancellationToken: TestContext.Current.CancellationToken);
        (media.Tracks.Single().Data.Audio.Channels == 2).Should().BeTrue();
        (media.Tracks.Single().Data.Audio.Rate == 44100).Should().BeTrue();
    }

    [Fact]
    public async Task CreateRealMediaSpecialCharacters()
    {
        using (var media = new Media(_libVLC, RealMp3PathSpecialCharacter, FromType.FromPath))
        {
            (media.IsParsed).Should().BeFalse();

            await media.Parse(cancellationToken: TestContext.Current.CancellationToken);
            await Task.Delay(5000, TestContext.Current.CancellationToken);
            (media.IsParsed).Should().BeTrue();
            (MediaParsedStatus.Done == media.ParsedStatus).Should().BeTrue();
            using (var mp = new MediaPlayer(media))
            {
                (mp.Play()).Should().BeTrue();
                await Task.Delay(1000, TestContext.Current.CancellationToken);
                mp.Stop();
            }
        }
    }

    [Fact]
    public async Task CreateMediaFromStreamMultiplePlay()
    {
        using var mp = new MediaPlayer(_libVLC);
        using var stream = await GetStreamFromUrl(RealStreamMediaPath);
        using var mediaInput = new StreamMediaInput(stream);
        using var media = new Media(_libVLC, mediaInput);
        mp.Play(media);

        await Task.Delay(1000, TestContext.Current.CancellationToken);

        mp.Time = 60000;

        await Task.Delay(10000, TestContext.Current.CancellationToken); // end reached, rewind stream

        mp.Play(media);
    }

    [Fact]
    public async Task CreateMultipleMediaFromStreamPlay()
    {
        var libVLC1 = new LibVLC("--no-audio", "--no-video");
        var libVLC2 = new LibVLC("--no-audio", "--no-video");

        var mp1 = new MediaPlayer(libVLC1);
        var mp2 = new MediaPlayer(libVLC2);

        using var s1 = await GetStreamFromUrl(RealStreamMediaPath);
        using var s2 = await GetStreamFromUrl(RealStreamMediaPath);

        using var i1 = new StreamMediaInput(s1);
        using var i2 = new StreamMediaInput(s2);

        var m1 = new Media(libVLC1, i1);
        var m2 = new Media(libVLC2, i2);

        mp1.Play(m1);
        m1.Dispose();
        mp2.Play(m2);
        m2.Dispose();

        await Task.Delay(10000, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ParseShouldThrowIfCancelledOperation()
    {
        using var media = new Media(_libVLC, RealMp3Path);
        var cancellationToken = new CancellationToken(canceled: true);
        await ((Func<Task>)(async () => await media.Parse(cancellationToken: cancellationToken))).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(Skip = "Crashes test runner on CI")]
    public async Task ParseShouldTimeoutWith1MillisecondLimit()
    {
        using var media = new Media(_libVLC, RealMp3Path);
        var parseResult = await media.Parse(timeout: 1, cancellationToken: TestContext.Current.CancellationToken);
        (MediaParsedStatus.Timeout == parseResult).Should().BeTrue();
    }

    [Fact(Skip = "Crashes test runner on CI")]
    public async Task ParseShouldSucceed()
    {
        using var media = new Media(_libVLC, RealMp3Path);
        var parseResult = await media.Parse(cancellationToken: TestContext.Current.CancellationToken);
        (MediaParsedStatus.Done == parseResult).Should().BeTrue();
    }

    [Fact(Skip = "Crashes test runner on CI")]
    public async Task ParseShouldFailIfNotMediaFile()
    {
        using var media = new Media(_libVLC, Path.GetTempFileName());
        var parseResult = await media.Parse(cancellationToken: TestContext.Current.CancellationToken);
        (MediaParsedStatus.Failed == parseResult).Should().BeTrue();
    }

    [Fact]
    public async Task ParseShouldBeSkippedIfLocalParseSpecifiedAndRemoteUrlProvided()
    {
        using var media = new Media(_libVLC, RealStreamMediaPath, FromType.FromLocation);
        var parseResult = await media.Parse(MediaParseOptions.ParseLocal, cancellationToken: TestContext.Current.CancellationToken);
        (MediaParsedStatus.Skipped == parseResult).Should().BeTrue();
    }

    private async Task<Stream> GetStreamFromUrl(string url)
    {
        byte[] imageData;

        using (var client = new System.Net.Http.HttpClient())
            imageData = await client.GetByteArrayAsync(url);

        return new MemoryStream(imageData);
    }

    private void LibVLC_Log(object sender, LogEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(e.Message);
    }
}
