using System.Diagnostics;
using System.IO.Compression;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Integration.Importing;

namespace HRandomPlus.Tests;

public class FileSafetyTests
{
    [Fact]
    public void FailedWriteDeletesOnlyItsOwnPartialFile()
    {
        WithDirectory(root =>
        {
            string existing = Path.Combine(root, "map.osu");
            File.WriteAllText(existing, "other writer");
            bool failed = false;
            try
            {
                UniqueFile.Write(existing, stream =>
                {
                    stream.WriteByte(42);
                    throw new IOException("Injected write failure");
                });
            }
            catch (IOException) { failed = true; }
            Assert.True(failed);
            Assert.Equal("other writer", File.ReadAllText(existing));
            Assert.Equal(new[] { existing }, Directory.GetFiles(root));
        });
    }

    [Fact]
    public void LockedDestinationIsSkippedWithoutDeletingIt()
    {
        WithDirectory(root =>
        {
            string existing = Path.Combine(root, "map.osu");
            using (var owner = new FileStream(existing, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                owner.WriteByte(7);
                string created = UniqueFile.Write(existing, stream => stream.WriteByte(8));
                Assert.Equal(Path.Combine(root, "map 2.osu"), created);
            }
            Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(existing));
        });
    }

    [Theory]
    [InlineData("generate")]
    [InlineData("native")]
    [InlineData("portable")]
    [InlineData("wine")]
    public void TwoProcessesKeepEveryOutputAndTheOriginal(string mode)
    {
        WithDirectory(root =>
        {
            byte[] original = Map();
            byte[] foreign = "pre-existing foreign file"u8.ToArray();
            File.WriteAllBytes(Path.Combine(root, "original.osu"), original);
            string outputDirectory = Path.Combine(root, "output");
            Directory.CreateDirectory(outputDirectory);
            string foreignPath = Path.Combine(outputDirectory, mode switch
            {
                "generate" => "original H-RANDOM+.osu",
                "portable" => "generated.osz",
                _ => "generated.osu"
            });
            File.WriteAllBytes(foreignPath, foreign);
            var processes = new List<Process>();
            try
            {
                for (int index = 0; index < 2; index++)
                {
                    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
                    foreach (string arg in new[] { typeof(Program).Assembly.Location, "--file-worker", mode, root, index.ToString() })
                        start.ArgumentList.Add(arg);
                    processes.Add(Process.Start(start)!);
                }
                Assert.True(SpinWait.SpinUntil(() => File.Exists(Path.Combine(root, "ready0")) &&
                    File.Exists(Path.Combine(root, "ready1")), TimeSpan.FromSeconds(20)), "Workers did not become ready.");
                File.WriteAllText(Path.Combine(root, "start"), "start");
                foreach (Process process in processes)
                {
                    Assert.True(process.WaitForExit(30000), "Worker timed out.");
                    Assert.Equal(0, process.ExitCode);
                }
                string[] outputs = Directory.GetFiles(outputDirectory);
                Assert.Equal(17, outputs.Length);
                Assert.Equal(foreign, File.ReadAllBytes(foreignPath));
                foreach (string path in outputs.Where(path => !path.Equals(foreignPath,
                             OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
                {
                    if (mode == "portable")
                    {
                        using ZipArchive archive = ZipFile.OpenRead(path);
                        Assert.Equal(1, archive.Entries.Count);
                        using var bytes = new MemoryStream();
                        using Stream entry = archive.Entries[0].Open();
                        entry.CopyTo(bytes);
                        Assert.Equal(original, bytes.ToArray());
                    }
                    else if (mode is "native" or "wine") Assert.Equal(original, File.ReadAllBytes(path));
                    else
                    {
                        string text = File.ReadAllText(path);
                        Assert.Contains("BeatmapID:0", text.Replace("BeatmapID: 0", "BeatmapID:0"));
                        Assert.Equal(2, OsuBeatmapDocument.Parse(path, File.ReadAllBytes(path)).HitObjects.Count);
                    }
                }
                Assert.Equal(original, File.ReadAllBytes(Path.Combine(root, "original.osu")));
            }
            finally
            {
                foreach (Process process in processes)
                {
                    if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(5000); }
                    process.Dispose();
                }
            }
        });
    }

    internal static int RunWorker(string mode, string root, string id)
    {
        string input = Path.Combine(root, "original.osu");
        string output = Path.Combine(root, "output");
        string staging = Path.Combine(root, "staging" + id);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(root, "ready" + id), "ready");
        if (!SpinWait.SpinUntil(() => File.Exists(Path.Combine(root, "start")), TimeSpan.FromSeconds(30))) return 2;
        for (int index = 0; index < 8; index++)
        {
            if (mode == "generate")
                new BeatmapGenerationService().Generate(input, new HRandomConfig { Seed = 123 }, null, output);
            else
            {
                string generated = Path.Combine(staging, "generated.osu");
                File.Copy(input, generated, overwrite: true);
                IBeatmapImporter importer = mode == "native" ? new NativeSideFileImporter()
                    : mode == "wine" ? new WineSideFileImporter(new SimulatedWineRunner())
                    : new PortableFallbackArchiveImporter(new FailedImporter());
                BeatmapImportResult result = importer.ImportAsync(new BeatmapImportRequest(
                    mode is "native" or "wine" ? Path.Combine(output, "original.osu") : generated, generated, output)).Result;
                if (mode is "native" or "wine" ? !result.Success : result.ImportArchivePath is null) return 3;
                if (mode == "wine" && result.FallbackUsed) return 4;
            }
        }
        return 0;
    }

    [Fact]
    public void PartialWinelloZipAlsoAllowsPortableFallback()
    {
        WithDirectory(root =>
        {
            string songs = Path.Combine(root, "songs");
            Directory.CreateDirectory(songs);
            string original = Path.Combine(songs, "original.osu");
            string generated = Path.Combine(root, "generated.osu");
            string audio = Path.Combine(songs, "audio.mp3");
            File.WriteAllBytes(original, Map());
            File.WriteAllBytes(generated, Map());
            File.WriteAllBytes(audio, new byte[] { 42 });
            using var lockedAudio = new FileStream(audio, FileMode.Open, FileAccess.Read, FileShare.None);
            string temporary = Path.Combine(root, "temporary");
            var inner = new WinelloArchiveImporter(new SimulatedWineRunner(), temporaryBase: temporary);
            var observer = new ObservingImporter(inner, result =>
            {
                Assert.True(!result.Success);
                Assert.True(result.ImportArchivePath is null);
                Assert.Equal(0, Directory.GetFiles(temporary, "*", SearchOption.AllDirectories).Length);
                lockedAudio.Dispose();
            });
            BeatmapImportResult result = new PortableFallbackArchiveImporter(observer).ImportAsync(
                new BeatmapImportRequest(original, generated, Path.Combine(root, "fallback"))).Result;
            Assert.True(result.FallbackUsed);
            using ZipArchive archive = ZipFile.OpenRead(result.ImportArchivePath!);
            Assert.True(archive.GetEntry("audio.mp3") is not null);
            Assert.True(archive.GetEntry("generated.osu") is not null);
        });
    }

    [Fact]
    public void PartialLazerZipIsRemovedAndOuterFallbackCanBuildACleanArchive()
    {
        WithDirectory(root =>
        {
            BeatmapImportRequest request = LazerRequest(root);
            string temporary = Path.Combine(root, "temporary");
            var launcher = new FailingLauncher(false);
            using var lockedAudio = new FileStream(request.LazerContext!.SetResources[0].BlobPath,
                FileMode.Open, FileAccess.Read, FileShare.None);
            var observer = new ObservingImporter(new LazerArchiveImporter(launcher, temporary), result =>
            {
                Assert.True(!result.Success);
                Assert.True(result.ImportArchivePath is null);
                Assert.Equal(0, Directory.GetFiles(temporary).Length);
                Assert.True(!launcher.Called);
                lockedAudio.Dispose();
            });
            BeatmapImportResult fallback = new PortableFallbackArchiveImporter(observer).ImportAsync(request).Result;
            Assert.True(fallback.FallbackUsed);
            Assert.True(fallback.ImportArchivePath is not null);
            using ZipArchive archive = ZipFile.OpenRead(fallback.ImportArchivePath!);
            Assert.True(archive.GetEntry("audio.mp3") is not null);
            Assert.True(archive.GetEntry("generated.osu") is not null);
            using Stream audio = archive.GetEntry("audio.mp3")!.Open();
            Assert.Equal(42, audio.ReadByte());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedLazerZipSurvivesLauncherFailure(bool throws)
    {
        WithDirectory(root =>
        {
            BeatmapImportRequest request = LazerRequest(root);
            var launcher = new FailingLauncher(throws);
            BeatmapImportResult result = new PortableFallbackArchiveImporter(
                new LazerArchiveImporter(launcher, Path.Combine(root, "temporary"))).ImportAsync(request).Result;
            Assert.True(!result.Success && launcher.Called);
            Assert.True(!result.FallbackUsed);
            Assert.Equal(result.ImportArchivePath, result.PreservedOutputPath);
            Assert.Contains(result.ImportArchivePath!, result.Message);
            string status = BeatmapImportStatus.Format("Test H-RANDOM+", 123, result);
            Assert.Contains($"Output: {result.ImportArchivePath}", status);
            Assert.Contains($"Archive preserved at: {result.ImportArchivePath}", status);
            Assert.Equal(request.FallbackDirectory, Path.GetDirectoryName(result.ImportArchivePath!));
            using ZipArchive archive = ZipFile.OpenRead(result.ImportArchivePath!);
            Assert.True(archive.GetEntry("audio.mp3") is not null);
            using var reader = new StreamReader(archive.GetEntry("generated.osu")!.Open());
            string map = reader.ReadToEnd();
            Assert.Contains("BeatmapID:0", map);
            Assert.Contains("BeatmapSetID:0", map);
        });
    }

    [Fact]
    public void CompletedLazerZipRemainsAvailableIfFallbackDirectoryIsUnwritable()
    {
        WithDirectory(root =>
        {
            BeatmapImportRequest request = LazerRequest(root);
            File.WriteAllText(request.FallbackDirectory, "blocks directory creation");
            string temporary = Path.Combine(root, "temporary");
            BeatmapImportResult result = new LazerArchiveImporter(new FailingLauncher(false), temporary)
                .ImportAsync(request).Result;
            Assert.True(!result.Success);
            Assert.Equal(temporary, Path.GetDirectoryName(result.ImportArchivePath!));
            using ZipArchive archive = ZipFile.OpenRead(result.ImportArchivePath!);
            Assert.Equal(2, archive.Entries.Count);
        });
    }

    [Fact]
    public void UnwritableStableDestinationAndFallbackKeepTheGeneratedOsu()
    {
        WithDirectory(root =>
        {
            string songDirectory = Path.Combine(root, "set");
            Directory.CreateDirectory(songDirectory);
            string original = Path.Combine(songDirectory, "original.osu");
            string generated = Path.Combine(root, "generated.osu");
            string fallback = Path.Combine(root, "Failed Imports");
            byte[] generatedBytes = Map();
            File.WriteAllBytes(original, generatedBytes);
            File.WriteAllBytes(generated, generatedBytes);
            Directory.Delete(songDirectory, recursive: true);
            File.WriteAllText(songDirectory, "blocks stable destination directory");
            File.WriteAllText(fallback, "blocks fallback directory");

            var failedWine = new WineSideFileImporter(new AlwaysFailingRunner());
            BeatmapImportResult result = new PortableFallbackArchiveImporter(failedWine).ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(!result.Success);
            Assert.Equal(generated, result.PreservedOutputPath);
            Assert.Equal(generatedBytes, File.ReadAllBytes(generated));
            Assert.True(!Directory.Exists(fallback));
            Assert.Contains("portable .osz fallback also failed", result.Message);
        });
    }

    private static BeatmapImportRequest LazerRequest(string root)
    {
        string generated = Path.Combine(root, "generated.osu");
        string audio = Path.Combine(root, "audio-blob");
        File.WriteAllBytes(generated, Map());
        File.WriteAllBytes(audio, new byte[] { 42 });
        return new BeatmapImportRequest(generated, generated, Path.Combine(root, "fallback"),
            new LazerBeatmapSelectionContext(Guid.NewGuid(), root,
                new[] { new BeatmapResource("audio.mp3", audio) }, null));
    }

    private static byte[] Map() => TestBeatmaps.Mania(4,
        new[] { TestBeatmaps.Note(4, 0, 1000), TestBeatmaps.Note(4, 1, 1100) });

    private static void WithDirectory(Action<string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusFileTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { Directory.Delete(root, true); }
    }

    private sealed class FailedImporter : IBeatmapImporter
    {
        public Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new BeatmapImportResult("test", true, false, request.GeneratedPath, "Import unavailable"));
    }

    private sealed class SimulatedWineRunner : IProcessRunner
    {
        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Arguments.Contains("winepath"))
                return Task.FromResult(new ProcessRunResult(true, false, 0, request.Arguments[^1], "", null));
            string destination = request.Environment!["HRANDOMPLUS_DESTINATION"];
            Assert.True(!File.Exists(destination));
            File.Copy(request.Environment["HRANDOMPLUS_SOURCE"], destination, overwrite: false);
            return Task.FromResult(new ProcessRunResult(true, false, 0, "copied", "", null));
        }
    }

    private sealed class AlwaysFailingRunner : IProcessRunner
    {
        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProcessRunResult(false, false, null, "", "", "Injected Wine failure"));
    }

    private sealed class ObservingImporter(IBeatmapImporter inner, Action<BeatmapImportResult> observe) : IBeatmapImporter
    {
        public async Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request, CancellationToken cancellationToken = default)
        {
            BeatmapImportResult result = await inner.ImportAsync(request, cancellationToken);
            observe(result);
            return result;
        }
    }

    private sealed class FailingLauncher(bool throws) : IExternalFileLauncher
    {
        public bool Called { get; private set; }
        public bool Launch(string filePath, string? executablePath, out string? error)
        {
            Called = true;
            if (throws) throw new IOException("Injected launcher failure");
            error = "Injected launcher failure";
            return false;
        }
    }
}
