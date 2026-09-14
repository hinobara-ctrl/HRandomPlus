using System.IO.Compression;
using System.Text.Json;
using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Integration.Importing;
using HRandomPlus.Integration.Lazer;

namespace HRandomPlus.Tests;

public class PendingClosureTests
{
    [Theory]
    [InlineData("input-output")]
    [InlineData("input-report")]
    [InlineData("output-report")]
    public void CliRejectsCollidingPathsBeforeChangingInput(string collision)
    {
        WithDirectory(root =>
        {
            string input = Path.Combine(root, "input.osz");
            CreateArchive(input);
            byte[] original = File.ReadAllBytes(input);
            string output = collision == "input-output" ? Path.Combine(root, ".", "input.osz") : Path.Combine(root, "output.osz");
            string report = collision == "input-report" ? Path.Combine(root, ".", "input.osz")
                : collision == "output-report" ? Path.Combine(root, ".", "output.osz")
                : Path.Combine(root, "report.json");

            int code = HRandomPlus.Cli.Program.Main(new[]
            {
                input, "--output", output, "--report", report, "--overwrite", "--seed", "123"
            }).GetAwaiter().GetResult();

            Assert.Equal(2, code);
            Assert.Equal(original, File.ReadAllBytes(input));
            if (collision != "input-output") Assert.True(!File.Exists(output));
            if (collision == "input-output") Assert.True(!File.Exists(Path.Combine(root, "report.json")));
        });
    }

    [Fact]
    public void CliAllowsOverwriteOfUnrelatedOutputAndWritesValidReport()
    {
        WithDirectory(root =>
        {
            string input = Path.Combine(root, "input.osz");
            string output = Path.Combine(root, "output.osz");
            string report = Path.Combine(root, "result.json");
            string config = Path.Combine(root, "config.json");
            CreateArchive(input);
            byte[] original = File.ReadAllBytes(input);
            File.WriteAllText(output, "replace me");
            File.WriteAllText(config, JsonSerializer.Serialize(new HRandomConfig
            {
                Seed = 999,
                DifficultySuffix = " CLI-CUSTOM"
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            int code = HRandomPlus.Cli.Program.Main(new[]
            {
                input, "--output", output, "--report", report, "--overwrite", "--config", config,
                "--difficulty", "Test", "--seed", "123"
            }).GetAwaiter().GetResult();

            Assert.Equal(0, code);
            Assert.Equal(original, File.ReadAllBytes(input));
            using ZipArchive archive = ZipFile.OpenRead(output);
            ZipArchiveEntry map = Assert.Single(archive.Entries.Where(entry => entry.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)));
            using var reader = new StreamReader(map.Open());
            string generated = reader.ReadToEnd();
            Assert.Contains("BeatmapID:0", generated);
            Assert.Contains("BeatmapSetID:456", generated);
            Assert.Contains("Version:Test CLI-CUSTOM", generated);
            using JsonDocument reportJson = JsonDocument.Parse(File.ReadAllText(report));
            Assert.Equal(123L, reportJson.RootElement.GetProperty("Seed").GetInt64());
            Assert.Equal("Test", reportJson.RootElement.GetProperty("Difficulties")[0]
                .GetProperty("OriginalVersion").GetString());
        });
    }

    [Fact]
    public void ReplacedSourceIsDisposedOnlyAfterItsInflightReadFinishes()
    {
        var first = new BlockingSource();
        var second = new DisposableSource();
        var slot = new ReplaceableBeatmapSource(first);

        Task<BeatmapSourceResult> read = slot.GetCurrentAsync();
        first.Started.Task.GetAwaiter().GetResult();
        slot.Replace(second);
        Assert.Equal(0, first.DisposeCalls);

        first.Release.SetResult();
        Assert.True(read.GetAwaiter().GetResult().IsAvailable);
        Assert.Equal(1, first.DisposeCalls);
        Assert.Equal(0, second.DisposeCalls);
        slot.Dispose();
        Assert.Equal(1, second.DisposeCalls);
    }

    [Fact]
    public void StaleSuccessfulReadIsDiscardedAfterSourceReplacement()
    {
        var first = new ControlledSource();
        var second = new ControlledSource();
        var slot = new ReplaceableBeatmapSource(first);

        Task<BeatmapSourceResult> read = slot.GetCurrentAsync();
        first.Started.Task.GetAwaiter().GetResult();
        slot.Replace(second);
        first.Completion.SetResult(BeatmapSourceResult.Waiting("stale result"));
        second.Started.Task.GetAwaiter().GetResult();
        second.Completion.SetResult(BeatmapSourceResult.Waiting("current result"));

        Assert.Equal("current result", read.GetAwaiter().GetResult().Status);
        Assert.Equal(1, first.DisposeCalls);
        Assert.Equal(0, second.DisposeCalls);
        slot.Dispose();
        Assert.Equal(1, second.DisposeCalls);
    }

    [Fact]
    public void StaleReadExceptionIsDiscardedAfterSourceReplacement()
    {
        var first = new ControlledSource();
        var second = new ControlledSource();
        var slot = new ReplaceableBeatmapSource(first);

        Task<BeatmapSourceResult> read = slot.GetCurrentAsync();
        first.Started.Task.GetAwaiter().GetResult();
        slot.Replace(second);
        first.Completion.SetException(new InvalidOperationException("stale failure"));
        second.Started.Task.GetAwaiter().GetResult();
        second.Completion.SetResult(BeatmapSourceResult.Waiting("current result"));

        Assert.Equal("current result", read.GetAwaiter().GetResult().Status);
        Assert.Equal(1, first.DisposeCalls);
        slot.Dispose();
        Assert.Equal(1, second.DisposeCalls);
    }

    [Fact]
    public void StableReaderWorkerCompletesBeforeItsTaskBackedOwnerIsDisposed()
    {
        var stopRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new TaskBackedOwner(stopRequested.Task);

        StableReaderWorkerLifetime.StopThenDispose(stopRequested.SetResult, stopRequested.Task, owner);

        Assert.True(stopRequested.Task.IsCompletedSuccessfully);
        Assert.Equal(1, owner.DisposeCalls);
    }

    [Fact]
    public void RepeatedInflightSourceReplacementDisposesEverySourceExactlyOnce()
    {
        var initial = new DisposableSource();
        var replacements = new List<DisposableSource>();
        var slot = new ReplaceableBeatmapSource(initial);

        for (int index = 0; index < 64; index++)
        {
            var active = new BlockingSource();
            slot.Replace(active);
            Task<BeatmapSourceResult> read = slot.GetCurrentAsync();
            active.Started.Task.GetAwaiter().GetResult();

            var next = new DisposableSource();
            replacements.Add(next);
            slot.Replace(next);
            Assert.Equal(0, active.DisposeCalls);
            active.Release.SetResult();
            Assert.True(read.GetAwaiter().GetResult().IsAvailable);
            Assert.Equal(1, active.DisposeCalls);
        }

        slot.Dispose();
        Assert.Equal(1, initial.DisposeCalls);
        Assert.All(replacements, source => Assert.Equal(1, source.DisposeCalls));
    }

    [Fact]
    public void LazerStorageSelectionKeepsExecutableAndConfiguredStorageTogether()
    {
        WithDirectory(root =>
        {
            string installationA = Path.Combine(root, "install-a");
            string installationB = Path.Combine(root, "install-b");
            string storageA = Path.Combine(root, "storage-a");
            string storageB = Path.Combine(root, "storage-b");
            Directory.CreateDirectory(installationA);
            Directory.CreateDirectory(installationB);
            File.WriteAllText(Path.Combine(installationA, "storage.ini"), $"FullPath={storageA}");
            LazerStorage first = Storage(storageA, DateTime.UtcNow.AddMinutes(-10));
            LazerStorage newerButUnrelated = Storage(storageB, DateTime.UtcNow);

            LazerStorage? selected = LazerStorageSelector.Select(
                new[] { newerButUnrelated, first }, Path.Combine(installationA, "osu!.exe"));

            Assert.Equal(storageA, selected!.RootPath);
            Assert.True(LazerStorageSelector.Select(new[] { first, newerButUnrelated },
                Path.Combine(root, "unknown", "osu!.exe")) is null);
            Assert.Equal(Path.Combine(installationA, "osu!.exe"), LazerProcessDetector.SelectExecutablePath(new[]
            {
                Path.Combine(installationB, "osu!.exe"), Path.Combine(installationA, "osu!.exe")
            }));
        });
    }

    [Fact]
    public void PortableFallbackRejectsResourceLimitsWithoutLeavingAPartialArchive()
    {
        WithDirectory(root =>
        {
            string set = Path.Combine(root, "set");
            string fallback = Path.Combine(set, "Failed Imports");
            Directory.CreateDirectory(set);
            string original = Path.Combine(set, "original.osu");
            string generated = Path.Combine(root, "generated.osu");
            File.WriteAllBytes(original, Map());
            File.WriteAllBytes(generated, Map());
            File.WriteAllBytes(Path.Combine(set, "one.bin"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(set, "two.bin"), new byte[] { 2 });
            File.WriteAllText(Path.Combine(set, "old.osz"), "must be excluded");
            var limits = new PortableArchiveLimits(MaximumEntries: 2, MaximumExpandedBytes: 16_384,
                MaximumEntryBytes: 8_192, MaximumBeatmapBytes: 8_192);

            BeatmapImportResult result = new PortableFallbackArchiveImporter(new FailedImporter(), limits)
                .ImportAsync(new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(result.ImportArchivePath is null);
            Assert.Contains("entry limit", result.Message);
            Assert.True(!Directory.Exists(fallback) || Directory.GetFiles(fallback, "*.osz").Length == 0);
            Assert.True(File.Exists(generated));
        });
    }

    [Fact]
    public void PortableFallbackExcludesExistingArchivesAndHonoursByteLimits()
    {
        WithDirectory(root =>
        {
            string set = Path.Combine(root, "set");
            string fallback = Path.Combine(set, "Failed Imports");
            Directory.CreateDirectory(set);
            string original = Path.Combine(set, "original.osu");
            string generated = Path.Combine(root, "generated.osu");
            File.WriteAllBytes(original, Map());
            File.WriteAllBytes(generated, Map());
            File.WriteAllBytes(Path.Combine(set, "audio.bin"), new byte[] { 1, 2, 3, 4, 5 });
            File.WriteAllText(Path.Combine(set, "previous.osz"), "excluded");
            var limits = new PortableArchiveLimits(MaximumEntries: 2, MaximumExpandedBytes: 16_384,
                MaximumEntryBytes: 5, MaximumBeatmapBytes: 8_192);

            BeatmapImportResult success = new PortableFallbackArchiveImporter(new FailedImporter(), limits)
                .ImportAsync(new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();
            Assert.True(success.ImportArchivePath is not null);
            using (ZipArchive archive = ZipFile.OpenRead(success.ImportArchivePath!))
            {
                Assert.True(archive.GetEntry("audio.bin") is not null);
                Assert.True(archive.GetEntry("previous.osz") is null);
            }

            File.WriteAllBytes(Path.Combine(set, "audio.bin"), new byte[] { 1, 2, 3, 4, 5, 6 });
            BeatmapImportResult rejected = new PortableFallbackArchiveImporter(new FailedImporter(), limits)
                .ImportAsync(new BeatmapImportRequest(original, generated, Path.Combine(root, "second"))).GetAwaiter().GetResult();
            Assert.True(rejected.ImportArchivePath is null);
            Assert.Contains("entry limit", rejected.Message);
            Assert.True(!Directory.Exists(Path.Combine(root, "second")) ||
                Directory.GetFiles(Path.Combine(root, "second"), "*.osz").Length == 0);
        });
    }

    private static LazerStorage Storage(string root, DateTime timestamp)
    {
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);
        string log = Path.Combine(logs, "runtime.log");
        File.WriteAllText(log, "log");
        File.SetLastWriteTimeUtc(log, timestamp);
        return new LazerStorage(root, Path.Combine(root, "client.realm"), Path.Combine(root, "files"), logs);
    }

    private static void CreateArchive(string path)
    {
        byte[] beatmap = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(Map())
            .Replace("Version:Test", "Version:Test\nBeatmapID:123\nBeatmapSetID:456", StringComparison.Ordinal));
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        ZipArchiveEntry entry = archive.CreateEntry("map.osu");
        using Stream stream = entry.Open();
        stream.Write(beatmap);
    }

    private static byte[] Map() => TestBeatmaps.Mania(4,
        Enumerable.Range(0, 8).Select(index => TestBeatmaps.Note(4, index % 4, 1000 + index * 100)));

    private static void WithDirectory(Action<string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusPending", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class FailedImporter : IBeatmapImporter
    {
        public Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new BeatmapImportResult("test", true, false, request.GeneratedPath, "Import unavailable."));
    }

    private sealed class BlockingSource : IBeatmapSource, IDisposable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisposeCalls { get; private set; }

        public async Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return BeatmapSourceResult.Waiting("ready");
        }

        public void Dispose() => DisposeCalls++;
    }

    private sealed class DisposableSource : IBeatmapSource, IDisposable
    {
        public int DisposeCalls { get; private set; }
        public Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(BeatmapSourceResult.Waiting("ready"));
        public void Dispose() => DisposeCalls++;
    }

    private sealed class ControlledSource : IBeatmapSource, IDisposable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<BeatmapSourceResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisposeCalls { get; private set; }

        public async Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return await Completion.Task.WaitAsync(cancellationToken);
        }

        public void Dispose() => DisposeCalls++;
    }

    private sealed class TaskBackedOwner(Task worker) : IDisposable
    {
        public int DisposeCalls { get; private set; }

        public void Dispose()
        {
            worker.Dispose();
            DisposeCalls++;
        }
    }
}
