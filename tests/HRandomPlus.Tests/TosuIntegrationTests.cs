using System.Net;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Integration.Linux;
using HRandomPlus.Integration.Tosu;

namespace HRandomPlus.Tests;

public class TosuIntegrationTests
{
    [Fact]
    public void ParsesCurrentTosuV2Shape()
    {
        TosuSnapshot snapshot = TosuSnapshot.Parse("""
        {
          "beatmap": { "id": 123, "set": 456, "checksum": "abc", "metadata": {
            "artist": "Artist", "title": "Title", "mapper": "Mapper", "difficulty": "Insane" } },
          "folders": { "songs": "D:\\Songs", "beatmap": "456 Artist - Title" },
          "files": { "beatmap": "Artist - Title (Mapper) [Insane].osu" },
          "directPath": { "beatmapFile": "D:\\Songs\\456 Artist - Title\\Artist - Title (Mapper) [Insane].osu" }
        }
        """);

        Assert.Equal(123, snapshot.Beatmap.Id);
        Assert.Equal(456, snapshot.Beatmap.SetId);
        Assert.Equal("abc", snapshot.Beatmap.Checksum);
        Assert.Equal("456 Artist - Title", snapshot.Beatmap.FolderName);
        Assert.Equal("Artist - Title (Mapper) [Insane].osu", snapshot.Beatmap.OsuFileName);
    }

    [Fact]
    public void TosuClientReportsUnavailableWithoutThrowing()
    {
        var http = new HttpClient(new StubHandler(_ => throw new HttpRequestException("connection refused")))
        { BaseAddress = new Uri("http://127.0.0.1:24050/") };
        TosuResult result = new TosuClient(http).GetCurrentAsync().GetAwaiter().GetResult();
        Assert.True(!result.Success);
        Assert.True(!result.IsAvailable);
        Assert.Contains("no está disponible", result.Status);
    }

    [Fact]
    public void ResolvesNativeBeatmapAndRejectsTraversal()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusTosu", Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "Songs", "123 Test");
        Directory.CreateDirectory(folder);
        try
        {
            string map = Path.Combine(folder, "map.osu");
            File.WriteAllText(map, "osu file format v14");
            var resolver = new BeatmapPathResolver();
            BeatmapInfo valid = Info("123 Test", "map.osu");
            Assert.Equal(Path.GetFullPath(map), resolver.Resolve(valid, root).Path);
            Assert.True(!resolver.Resolve(Info("..", "outside.osu"), root).Success);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ReadsWinelloXdgConfiguration()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusWinello", Guid.NewGuid().ToString("N"));
        string data = Path.Combine(root, "data");
        string osu = Path.Combine(root, "osu");
        Directory.CreateDirectory(Path.Combine(data, "osuconfig"));
        Directory.CreateDirectory(Path.Combine(osu, "Songs"));
        try
        {
            File.WriteAllText(Path.Combine(data, "osuconfig", "osupath"), osu);
            var locator = new WinelloLocator(name => name == "XDG_DATA_HOME" ? data : null, root);
            Assert.True(locator.TryLocate(out string? found, out _));
            Assert.Equal(Path.GetFullPath(osu), found);
        }
        finally { Directory.Delete(root, true); }
    }

    private static BeatmapInfo Info(string folder, string file)
        => new(1, 2, null, "", "", "", "", folder, file, null);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
