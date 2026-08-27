using System.Net;
using System.Text;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
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
    public void TosuClientReplacesOnlyUnsafeDefaultTimeout()
    {
        using var defaultHttp = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        _ = new TosuClient(defaultHttp);
        Assert.Equal(TosuClient.DefaultTimeout, defaultHttp.Timeout);

        using var explicitHttp = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        _ = new TosuClient(explicitHttp);
        Assert.Equal(TimeSpan.FromSeconds(2), explicitHttp.Timeout);
    }

    [Fact]
    public void TosuSourceCanDisconnectAndReconnect()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusReconnect", Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "Songs", "456 Artist - Title");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "map.osu"), "osu file format v14");
        var responses = new Queue<Func<HttpResponseMessage>>();
        responses.Enqueue(() => JsonResponse(TosuJson("map.osu")));
        responses.Enqueue(() => throw new HttpRequestException("connection refused"));
        responses.Enqueue(() => JsonResponse(TosuJson("map.osu")));
        using var http = new HttpClient(new StubHandler(_ => responses.Dequeue()()))
        {
            BaseAddress = new Uri("http://127.0.0.1:24050/")
        };
        using var source = new TosuBeatmapSource(new TosuClient(http), new BeatmapPathResolver(), () => root);
        try
        {
            Assert.True(source.GetCurrentAsync().GetAwaiter().GetResult().Success);
            Assert.True(!source.GetCurrentAsync().GetAwaiter().GetResult().IsAvailable);
            Assert.True(source.GetCurrentAsync().GetAwaiter().GetResult().Success);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void TosuMockEndToEndGeneratesSafeReproducibleOutput()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusTosuE2E", Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "Songs", "456 Artist - Title");
        Directory.CreateDirectory(folder);
        string input = Path.Combine(folder, "map.osu");
        byte[] template = TestBeatmaps.Mania(4, new[]
        {
            TestBeatmaps.Note(4, 0, 1000),
            TestBeatmaps.LongNote(4, 1, 1100, 1500),
            TestBeatmaps.Note(4, 2, 1200),
            TestBeatmaps.Note(4, 3, 1600)
        });
        byte[] originalBytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(template)
            .Replace("Version:Test", "Version:Test\nBeatmapID:123\nBeatmapSetID:456", StringComparison.Ordinal));
        File.WriteAllBytes(input, originalBytes);
        using var http = new HttpClient(new StubHandler(_ => JsonResponse(TosuJson("map.osu"))))
        {
            BaseAddress = new Uri("http://127.0.0.1:24050/")
        };
        using var source = new TosuBeatmapSource(new TosuClient(http), new BeatmapPathResolver(), () => root);
        try
        {
            BeatmapSourceResult detected = source.GetCurrentAsync().GetAwaiter().GetResult();
            Assert.True(detected.Success);
            Assert.Equal(Path.GetFullPath(input), detected.Selection!.NativePath);

            var service = new BeatmapGenerationService();
            var config = new HRandomConfig { Seed = 24680, DifficultySuffix = " E2E" };
            GenerationResult first = service.Generate(detected.Selection.NativePath, config, null);
            GenerationResult second = service.Generate(detected.Selection.NativePath, config, null);
            OsuBeatmapDocument original = OsuBeatmapDocument.Parse(input, originalBytes);
            OsuBeatmapDocument outputA = OsuBeatmapDocument.Parse(first.OutputPath, File.ReadAllBytes(first.OutputPath));
            OsuBeatmapDocument outputB = OsuBeatmapDocument.Parse(second.OutputPath, File.ReadAllBytes(second.OutputPath));

            Assert.Equal(originalBytes, File.ReadAllBytes(input));
            Assert.Equal(original.HitObjects.Count, outputA.HitObjects.Count);
            Assert.Equal(original.HitObjects.Select(note => note.StartTime), outputA.HitObjects.Select(note => note.StartTime));
            Assert.Equal(original.HitObjects.Select(note => note.EndTime), outputA.HitObjects.Select(note => note.EndTime));
            Assert.Equal(0, outputA.BeatmapId);
            Assert.Equal(456, outputA.BeatmapSetId);
            Assert.Equal(outputA.HitObjects.Select(note => note.OriginalColumn),
                outputB.HitObjects.Select(note => note.OriginalColumn));
        }
        finally { Directory.Delete(root, true); }
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

    [Fact]
    public void WinelloLocatorReportsMissingConfiguration()
    {
        WithWinelloLayout((root, data, _) =>
        {
            var locator = new WinelloLocator(name => name == "XDG_DATA_HOME" ? data : null, root);
            Assert.True(!locator.TryLocate(out string? found, out string status));
            Assert.Equal<string?>(null, found);
            Assert.Contains("No se encontró", status);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n")]
    [InlineData("  \"  \"  \r\n")]
    public void WinelloLocatorRejectsEmptyConfiguration(string contents)
    {
        WithWinelloLayout((root, data, _) =>
        {
            File.WriteAllText(Path.Combine(data, "osuconfig", "osupath"), contents);
            var locator = new WinelloLocator(name => name == "XDG_DATA_HOME" ? data : null, root);
            Assert.True(!locator.TryLocate(out string? found, out string status));
            Assert.Equal<string?>(null, found);
            Assert.Contains("está vacía", status);
        });
    }

    [Fact]
    public void WinelloLocatorRejectsNonexistentPath()
    {
        WithWinelloLayout((root, data, _) =>
        {
            File.WriteAllText(Path.Combine(data, "osuconfig", "osupath"), Path.Combine(root, "missing"));
            var locator = new WinelloLocator(name => name == "XDG_DATA_HOME" ? data : null, root);
            Assert.True(!locator.TryLocate(out string? _, out string? status));
            Assert.Contains("no contiene Songs", status!);
        });
    }

    [Fact]
    public void WinelloLocatorTrimsNewlineAndQuotesFromValidPath()
    {
        WithWinelloLayout((root, data, osu) =>
        {
            Directory.CreateDirectory(Path.Combine(osu, "Songs"));
            File.WriteAllText(Path.Combine(data, "osuconfig", "osupath"), $"  \"{osu}\"  \r\n");
            var locator = new WinelloLocator(name => name == "XDG_DATA_HOME" ? data : null, root);
            Assert.True(locator.TryLocate(out string? found, out _));
            Assert.Equal(Path.GetFullPath(osu), found);
        });
    }

    [Fact]
    public void WinelloLocatorUsesLocalShareFallback()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusWinelloFallback", Guid.NewGuid().ToString("N"));
        string data = Path.Combine(root, ".local", "share");
        string osu = Path.Combine(root, "osu");
        Directory.CreateDirectory(Path.Combine(data, "osuconfig"));
        Directory.CreateDirectory(Path.Combine(osu, "Songs"));
        try
        {
            File.WriteAllText(Path.Combine(data, "osuconfig", "osupath"), "~/osu");
            var locator = new WinelloLocator(_ => null, root);
            Assert.Equal(Path.Combine(data, "osuconfig", "osupath"), locator.ConfigurationPath);
            Assert.True(locator.TryLocate(out string? found, out _));
            Assert.Equal(Path.GetFullPath(osu), found);
        }
        finally { Directory.Delete(root, true); }
    }

    private static BeatmapInfo Info(string folder, string file)
        => new(1, 2, null, "", "", "", "", folder, file, null);

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private static string TosuJson(string file) => $$"""
        {
          "beatmap": { "id": 123, "set": 456, "checksum": "abc", "metadata": {
            "artist": "Artist", "title": "Title", "mapper": "Mapper", "difficulty": "Insane" } },
          "folders": { "beatmap": "456 Artist - Title" },
          "files": { "beatmap": "{{file}}" }
        }
        """;

    private static void WithWinelloLayout(Action<string, string, string> test)
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusWinello", Guid.NewGuid().ToString("N"));
        string data = Path.Combine(root, "data");
        string osu = Path.Combine(root, "osu");
        Directory.CreateDirectory(Path.Combine(data, "osuconfig"));
        try { test(root, data, osu); }
        finally { Directory.Delete(root, true); }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
