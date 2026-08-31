using System.IO.Compression;
using System.Security.Cryptography;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Integration.Importing;
using HRandomPlus.Integration.Lazer;

namespace HRandomPlus.Tests;

public class LazerIntegrationTests
{
    [Fact]
    public void RuntimeParserReadsHistoricalGuidAndRuleset()
    {
        Guid id = Guid.NewGuid();
        LazerLogSelection? parsed = LazerRuntimeLogParser.ParseLine(
            $"2026-08-30 [verbose]: Song select updating selection with beatmap:{id} ruleset:mania");
        Assert.Equal(id, parsed!.BeatmapId);
        Assert.Equal("mania", parsed.Ruleset);
    }

    [Fact]
    public void RuntimeParserReadsCurrentWorkingBeatmapLine()
    {
        LazerLogSelection? parsed = LazerRuntimeLogParser.ParseLine(
            "2026-08-30 [verbose]: Game-wide working beatmap updated to Artist - Song (Mapper) [Hard]");
        Assert.Equal("Artist - Song (Mapper) [Hard]", parsed!.DisplayName);
    }

    [Fact]
    public void RuntimeParserIgnoresMalformedAndUnrelatedLines()
    {
        Assert.True(LazerRuntimeLogParser.ParseLine("Song select updating selection with beatmap:nope ruleset:mania") is null);
        Assert.True(LazerRuntimeLogParser.ParseLine("ordinary runtime message") is null);
    }

    [Fact]
    public void RuntimeParserReturnsLastValidSelection()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        LazerLogSelection? parsed = LazerRuntimeLogParser.LastValid(new[]
        {
            $"Song select updating selection with beatmap:{first} ruleset:osu",
            "unrelated",
            $"Song select updating selection with beatmap:{second} ruleset:mania"
        });
        Assert.Equal(second, parsed!.BeatmapId);
        Assert.Equal("mania", parsed.Ruleset);
    }

    [Fact]
    public void RuntimeMonitorReadsExistingStateAndAppends()
    {
        string root = TempRoot();
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);
        string log = Path.Combine(logs, "runtime.log");
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        File.WriteAllText(log, $"Song select updating selection with beatmap:{first} ruleset:osu\n");
        var storage = new LazerStorage(root, Path.Combine(root, "client.realm"), Path.Combine(root, "files"), logs);
        var monitor = new LazerRuntimeLogMonitor();
        Assert.Equal(first, monitor.ReadCurrent(storage)!.BeatmapId);
        File.AppendAllText(log, $"Song select updating selection with beatmap:{second} ruleset:mania\n");
        Assert.Equal(second, monitor.ReadCurrent(storage)!.BeatmapId);
        Directory.Delete(root, true);
    }

    [Fact]
    public void RuntimeMonitorHandlesLogTruncation()
    {
        string root = TempRoot();
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);
        string log = Path.Combine(logs, "runtime.log");
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        File.WriteAllText(log, $"Song select updating selection with beatmap:{first} ruleset:mania\nextra padding\n");
        var storage = new LazerStorage(root, Path.Combine(root, "client.realm"), Path.Combine(root, "files"), logs);
        var monitor = new LazerRuntimeLogMonitor();
        Assert.Equal(first, monitor.ReadCurrent(storage)!.BeatmapId);
        File.WriteAllText(log, $"Song select updating selection with beatmap:{second} ruleset:mania\n");
        Assert.Equal(second, monitor.ReadCurrent(storage)!.BeatmapId);
        Directory.Delete(root, true);
    }

    [Fact]
    public void RuntimeMonitorReadsTimestampPrefixedLazerLog()
    {
        string root = TempRoot();
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);
        string log = Path.Combine(logs, "1788121867.runtime.log");
        File.WriteAllText(log, "Game-wide working beatmap updated to Artist - Song (Mapper) [Hard]\n");
        var storage = new LazerStorage(root, Path.Combine(root, "client.realm"), Path.Combine(root, "files"), logs);

        LazerLogSelection? selection = new LazerRuntimeLogMonitor().ReadCurrent(storage);

        Assert.Equal("Artist - Song (Mapper) [Hard]", selection!.DisplayName);
        Directory.Delete(root, true);
    }

    [Fact]
    public void RuntimeMonitorPrefersNewTimestampLogOverOldRuntimeLog()
    {
        string root = TempRoot();
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);
        string runtime = Path.Combine(logs, "runtime.log");
        string timestamped = Path.Combine(logs, "1788121867.runtime.log");
        WriteSelection(runtime, "old", DateTime.UtcNow.AddMinutes(-2));
        WriteSelection(timestamped, "new", DateTime.UtcNow);

        LazerLogSelection? selection = new LazerRuntimeLogMonitor().ReadCurrent(Storage(root, logs));

        Assert.Equal("new", selection!.DisplayName);
        Directory.Delete(root, true);
    }

    [Fact]
    public void RuntimeMonitorPrefersNewRuntimeLogOverOldTimestampLog()
    {
        string root = TempRoot();
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);
        string runtime = Path.Combine(logs, "runtime.log");
        string timestamped = Path.Combine(logs, "1788121867.runtime.log");
        WriteSelection(timestamped, "old", DateTime.UtcNow.AddMinutes(-2));
        WriteSelection(runtime, "new", DateTime.UtcNow);

        LazerLogSelection? selection = new LazerRuntimeLogMonitor().ReadCurrent(Storage(root, logs));

        Assert.Equal("new", selection!.DisplayName);
        Directory.Delete(root, true);
    }

    [Fact]
    public void RuntimeMonitorChoosesNewestOfTwoTimestampLogs()
    {
        string root = TempRoot();
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);
        WriteSelection(Path.Combine(logs, "100.runtime.log"), "old", DateTime.UtcNow.AddMinutes(-2));
        WriteSelection(Path.Combine(logs, "200.runtime.log"), "new", DateTime.UtcNow);

        LazerLogSelection? selection = new LazerRuntimeLogMonitor().ReadCurrent(Storage(root, logs));

        Assert.Equal("new", selection!.DisplayName);
        Directory.Delete(root, true);
    }

    [Fact]
    public void BlobPathUsesOfficialHashFanout()
    {
        string hash = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
        Assert.Equal(Path.Combine("a", "ab", hash.ToLowerInvariant()), LazerBlobPath.GetRelativePath(hash));
    }

    [Fact]
    public void BlobPathRejectsInvalidHash()
    {
        bool threw = false;
        try { _ = LazerBlobPath.GetRelativePath("not-a-hash"); }
        catch (ArgumentException) { threw = true; }
        Assert.True(threw);
    }

    [Fact]
    public void StorageDiscoveryFollowsCustomStorageIni()
    {
        string root = TempRoot();
        string defaultRoot = Path.Combine(root, "osu");
        string custom = Path.Combine(root, "custom-lazer");
        Directory.CreateDirectory(defaultRoot);
        File.WriteAllText(Path.Combine(defaultRoot, "storage.ini"), $"FullPath = {custom}\n");
        CreateStorage(custom);
        var discovery = new LazerStorageDiscovery(() => root, () => root,
            OperatingSystem.IsWindows() ? null : new[] { defaultRoot });
        LazerStorage found = Assert.Single(discovery.Discover());
        Assert.Equal(Path.GetFullPath(custom), found.RootPath);
        Directory.Delete(root, true);
    }

    [Fact]
    public void StorageDiscoveryAcceptsPortableRuntimeRoot()
    {
        string root = TempRoot();
        CreateStorage(root);
        var discovery = new LazerStorageDiscovery(() => null, () => null);
        LazerStorage found = Assert.Single(discovery.Discover(new[] { root }));
        Assert.Equal(Path.GetFullPath(root), found.RootPath);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ResolverUsesGuidAndMaterializesValidatedOsuBlob()
    {
        string root = TempRoot();
        CreateStorage(root);
        byte[] osu = ValidBeatmap();
        string hash = Convert.ToHexString(SHA256.HashData(osu)).ToLowerInvariant();
        string blob = LazerBlobPath.GetFullPath(root, hash);
        Directory.CreateDirectory(Path.GetDirectoryName(blob)!);
        File.WriteAllBytes(blob, osu);
        Guid id = Guid.NewGuid();
        var beatmap = new LazerCatalogBeatmap(id, 12, 34, "Test", hash, "Artist", "", "Title", "", "Mapper",
            new[] { new BeatmapResource("map.osu", blob) });
        var resolver = new LazerBeatmapResolver(new FakeCatalog(beatmap), Path.Combine(root, "materialized"));
        LazerResolution result = resolver.Resolve(new LazerStorage(root, Path.Combine(root, "client.realm"),
            Path.Combine(root, "files"), Path.Combine(root, "logs")), new LazerLogSelection(id, "mania", null, DateTimeOffset.UtcNow));
        Assert.True(File.Exists(result.Selection.NativePath));
        Assert.Equal(id, result.Selection.LazerContext!.BeatmapId);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ResolverRejectsAmbiguousTextFallback()
    {
        string root = TempRoot();
        CreateStorage(root);
        var catalog = new AmbiguousCatalog();
        bool threw = false;
        try
        {
            _ = new LazerBeatmapResolver(catalog, Path.Combine(root, "out")).Resolve(
                new LazerStorage(root, Path.Combine(root, "client.realm"), Path.Combine(root, "files"), Path.Combine(root, "logs")),
                new LazerLogSelection(null, null, "same", DateTimeOffset.UtcNow));
        }
        catch (InvalidDataException ex) { threw = ex.Message.Contains("ambiguous", StringComparison.OrdinalIgnoreCase); }
        Assert.True(threw);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ArbitrationSwitchesToMostRecentlyChangedSource()
    {
        var stable = new MutableSource(Result("stable", BeatmapDetectionSource.WindowsMemory, DateTimeOffset.UtcNow));
        var lazer = new MutableSource(Result("lazer", BeatmapDetectionSource.Lazer, DateTimeOffset.UtcNow.AddSeconds(-2)));
        var source = new ArbitratingBeatmapSource(stable, lazer);
        Assert.Equal(BeatmapDetectionSource.WindowsMemory, source.GetCurrentAsync().Result.DetectionSource);
        lazer.Result = Result("lazer-2", BeatmapDetectionSource.Lazer, DateTimeOffset.UtcNow.AddSeconds(2));
        Assert.Equal(BeatmapDetectionSource.Lazer, source.GetCurrentAsync().Result.DetectionSource);
    }

    [Fact]
    public void NewEventForSameLazerSelectionRefreshesRealmResolution()
    {
        string root = TempRoot();
        CreateStorage(root);
        var storage = new LazerStorage(root, Path.Combine(root, "client.realm"), Path.Combine(root, "files"), Path.Combine(root, "logs"));
        Guid id = Guid.NewGuid();
        var monitor = new MutableLazerMonitor(new LazerLogSelection(id, "mania", "same", DateTimeOffset.UtcNow));
        var resolver = new CountingLazerResolver();
        var source = new LazerCurrentBeatmapSource(new FixedStorageDiscovery(storage), new FixedLazerProcessDetector(root), monitor, resolver);

        BeatmapSourceResult first = source.GetCurrentAsync().Result;
        monitor.Selection = monitor.Selection! with { ObservedAt = monitor.Selection.ObservedAt.AddSeconds(1) };
        BeatmapSourceResult second = source.GetCurrentAsync().Result;

        Assert.Equal(2, resolver.Calls);
        Assert.Equal("hash-1", first.Selection!.Beatmap.Checksum);
        Assert.Equal("hash-2", second.Selection!.Beatmap.Checksum);
        Directory.Delete(root, true);
    }

    [Fact]
    public void UnchangedLazerEventKeepsCachedResolution()
    {
        string root = TempRoot();
        CreateStorage(root);
        var storage = new LazerStorage(root, Path.Combine(root, "client.realm"), Path.Combine(root, "files"), Path.Combine(root, "logs"));
        var monitor = new MutableLazerMonitor(new LazerLogSelection(Guid.NewGuid(), "mania", "same", DateTimeOffset.UtcNow));
        var resolver = new CountingLazerResolver();
        var source = new LazerCurrentBeatmapSource(new FixedStorageDiscovery(storage), new FixedLazerProcessDetector(root), monitor, resolver);

        _ = source.GetCurrentAsync().Result;
        _ = source.GetCurrentAsync().Result;

        Assert.Equal(1, resolver.Calls);
        Directory.Delete(root, true);
    }

    [Fact]
    public void SuccessfulLazerImportInvalidationForcesOneFreshResolution()
    {
        string root = TempRoot();
        CreateStorage(root);
        var storage = new LazerStorage(root, Path.Combine(root, "client.realm"), Path.Combine(root, "files"), Path.Combine(root, "logs"));
        var monitor = new MutableLazerMonitor(new LazerLogSelection(Guid.NewGuid(), "mania", "same", DateTimeOffset.UtcNow));
        var resolver = new CountingLazerResolver();
        var source = new LazerCurrentBeatmapSource(new FixedStorageDiscovery(storage), new FixedLazerProcessDetector(root), monitor, resolver);

        _ = source.GetCurrentAsync().Result;
        source.InvalidateLazerResolution();
        _ = source.GetCurrentAsync().Result;
        _ = source.GetCurrentAsync().Result;

        Assert.Equal(2, resolver.Calls);
        Directory.Delete(root, true);
    }

    [Fact]
    public void StatusFormatterNamesLazerExplicitly()
    {
        BeatmapSourceResult result = Result("lazer", BeatmapDetectionSource.Lazer, DateTimeOffset.UtcNow);
        BeatmapDetectionUpdate update = new DetectionStateTracker().Observe(result);
        Assert.True(BeatmapStatusFormatter.Format(update, false).Contains("osu!lazer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LazerArchiveContainsResourcesAndDetachedGeneratedDifficulty()
    {
        string root = TempRoot();
        string generated = Path.Combine(root, "generated.osu");
        string textInput = System.Text.Encoding.UTF8.GetString(ValidBeatmap())
            .Replace("Version:Test", "Version:Test\nBeatmapID:123\nBeatmapSetID:456", StringComparison.Ordinal);
        File.WriteAllText(generated, textInput);
        string audio = Path.Combine(root, "audio.mp3");
        File.WriteAllBytes(audio, new byte[] { 1, 2, 3 });
        var context = new LazerBeatmapSelectionContext(Guid.NewGuid(), root,
            new[] { new BeatmapResource("audio.mp3", audio) }, null);
        var launcher = new CapturingLauncher();
        BeatmapImportResult result = new LazerArchiveImporter(launcher, Path.Combine(root, "temp"))
            .ImportAsync(new BeatmapImportRequest(generated, generated, root, context)).Result;
        Assert.True(result.Success, result.Message);
        using (ZipArchive archive = ZipFile.OpenRead(launcher.Path!))
        {
            Assert.True(archive.GetEntry("audio.mp3") is not null);
            ZipArchiveEntry map = archive.Entries.Single(entry => entry.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase));
            using var reader = new StreamReader(map.Open());
            string text = reader.ReadToEnd();
            Assert.Contains("BeatmapID:0", text);
            Assert.Contains("BeatmapSetID:0", text);
        }
        Directory.Delete(root, true);
    }

    [Fact]
    public void LazerArchiveRejectsTraversalResourceName()
    {
        string root = TempRoot();
        string generated = Path.Combine(root, "generated.osu");
        File.WriteAllBytes(generated, ValidBeatmap());
        string resource = Path.Combine(root, "resource.bin");
        File.WriteAllBytes(resource, new byte[] { 1, 2, 3 });
        var context = new LazerBeatmapSelectionContext(Guid.NewGuid(), root,
            new[] { new BeatmapResource("../../outside.bin", resource) }, null);
        var launcher = new CapturingLauncher();

        BeatmapImportResult result = new LazerArchiveImporter(launcher, Path.Combine(root, "temp"))
            .ImportAsync(new BeatmapImportRequest(generated, generated, root, context)).Result;

        Assert.True(!result.Success);
        Assert.True(launcher.Path is null);
        Assert.Contains("Unsafe lazer resource name", result.Message);
        Assert.True(!File.Exists(Path.Combine(Directory.GetParent(root)!.FullName, "outside.bin")));
        Directory.Delete(root, true);
    }

    [Fact]
    public void LazerArchiveReportsMissingRequiredAudioBeforeLaunching()
    {
        string root = TempRoot();
        string generated = Path.Combine(root, "generated.osu");
        File.WriteAllBytes(generated, ValidBeatmap());
        var context = new LazerBeatmapSelectionContext(Guid.NewGuid(), root,
            new[] { new BeatmapResource("audio.mp3", Path.Combine(root, "missing-audio-blob")) }, null);
        var launcher = new CapturingLauncher();

        BeatmapImportResult result = new LazerArchiveImporter(launcher, Path.Combine(root, "temp"))
            .ImportAsync(new BeatmapImportRequest(generated, generated, root, context)).Result;

        Assert.True(!result.Success);
        Assert.True(launcher.Path is null);
        Assert.Contains("audio.mp3", result.Message);
        Assert.Contains("missing", result.Message);
        Directory.Delete(root, true);
    }

    [Fact]
    public void LazerArchivePreservesResourcesThatDifferOnlyByCase()
    {
        string root = TempRoot();
        string generated = Path.Combine(root, "generated.osu");
        File.WriteAllBytes(generated, ValidBeatmap());
        string audio = Path.Combine(root, "audio-blob");
        string lower = Path.Combine(root, "lower-blob");
        string upper = Path.Combine(root, "upper-blob");
        File.WriteAllBytes(audio, new byte[] { 1 });
        File.WriteAllBytes(lower, new byte[] { 2 });
        File.WriteAllBytes(upper, new byte[] { 3 });
        var context = new LazerBeatmapSelectionContext(Guid.NewGuid(), root,
            new[]
            {
                new BeatmapResource("audio.mp3", audio),
                new BeatmapResource("hit.wav", lower),
                new BeatmapResource("Hit.wav", upper)
            }, null);
        var launcher = new CapturingLauncher();

        BeatmapImportResult result = new LazerArchiveImporter(launcher, Path.Combine(root, "temp"))
            .ImportAsync(new BeatmapImportRequest(generated, generated, root, context)).Result;

        Assert.True(result.Success, result.Message);
        using (ZipArchive archive = ZipFile.OpenRead(launcher.Path!))
        {
            Assert.True(archive.GetEntry("hit.wav") is not null);
            Assert.True(archive.GetEntry("Hit.wav") is not null);
        }
        Directory.Delete(root, true);
    }

    private static BeatmapSourceResult Result(string identity, BeatmapDetectionSource source, DateTimeOffset observed)
    {
        var info = new BeatmapInfo(0, 0, identity, "", "", "", "", "", identity + ".osu", identity);
        return BeatmapSourceResult.Found(new BeatmapSelection(info, identity), detectionSource: source, observedAt: observed);
    }

    private static string TempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusLazerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static byte[] ValidBeatmap() => TestBeatmaps.Mania(4, new[]
    {
        TestBeatmaps.Note(4, 0, 1000), TestBeatmaps.Note(4, 1, 1100)
    });

    private static void CreateStorage(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "files"));
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        File.WriteAllBytes(Path.Combine(root, "client.realm"), new byte[] { 1 });
    }

    private static LazerStorage Storage(string root, string logs)
        => new(root, Path.Combine(root, "client.realm"), Path.Combine(root, "files"), logs);

    private static void WriteSelection(string path, string displayName, DateTime lastWriteUtc)
    {
        File.WriteAllText(path, $"Game-wide working beatmap updated to {displayName}\n");
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
    }

    private sealed class FakeCatalog : ILazerBeatmapCatalog
    {
        private readonly LazerCatalogBeatmap beatmap;
        public FakeCatalog(LazerCatalogBeatmap beatmap) => this.beatmap = beatmap;
        public LazerCatalogBeatmap? FindById(LazerStorage storage, Guid id) => id == beatmap.Id ? beatmap : null;
        public IReadOnlyList<LazerCatalogBeatmap> FindByDisplayName(LazerStorage storage, string displayName) => new[] { beatmap };
    }

    private sealed class AmbiguousCatalog : ILazerBeatmapCatalog
    {
        public LazerCatalogBeatmap? FindById(LazerStorage storage, Guid id) => null;
        public IReadOnlyList<LazerCatalogBeatmap> FindByDisplayName(LazerStorage storage, string displayName)
            => new[] { EmptyBeatmap(), EmptyBeatmap() };
        private static LazerCatalogBeatmap EmptyBeatmap() => new(Guid.NewGuid(), 0, 0, "", new string('a', 64), "", "", "", "", "", Array.Empty<BeatmapResource>());
    }

    private sealed class MutableSource : IBeatmapSource
    {
        public BeatmapSourceResult Result { get; set; }
        public MutableSource(BeatmapSourceResult result) => Result = result;
        public Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result);
    }

    private sealed class CapturingLauncher : IExternalFileLauncher
    {
        public string? Path { get; private set; }
        public bool Launch(string filePath, string? executablePath, out string? error)
        {
            Path = filePath;
            error = null;
            return true;
        }
    }

    private sealed class FixedStorageDiscovery(LazerStorage storage) : ILazerStorageDiscovery
    {
        public IReadOnlyList<LazerStorage> Discover() => new[] { storage };
    }

    private sealed class FixedLazerProcessDetector(string root) : ILazerProcessDetector
    {
        public string? FindExecutablePath() => Path.Combine(root, "osu!");
    }

    private sealed class MutableLazerMonitor(LazerLogSelection? selection) : ILazerRuntimeLogMonitor
    {
        public LazerLogSelection? Selection { get; set; } = selection;
        public LazerLogSelection? ReadCurrent(LazerStorage storage) => Selection;
        public void Reset() { }
    }

    private sealed class CountingLazerResolver : ILazerBeatmapResolver
    {
        public int Calls { get; private set; }

        public LazerResolution Resolve(LazerStorage storage, LazerLogSelection logSelection, string? executablePath = null)
        {
            Calls++;
            string hash = $"hash-{Calls}";
            var beatmap = new BeatmapInfo(Calls, 0, hash, "", "", "", "", "", "map.osu", "map.osu");
            return new LazerResolution(new BeatmapSelection(beatmap, "map.osu"), logSelection.ObservedAt);
        }
    }
}
