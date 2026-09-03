using System.IO.Compression;
using System.Diagnostics;
using HRandomPlus.Integration.Importing;

namespace HRandomPlus.Tests;

public class ImportIntegrationTests
{
    [Fact]
    public void SystemProcessRunnerReturnsPromptlyAfterTimeout()
    {
        ProcessRunRequest request = OperatingSystem.IsWindows()
            ? new ProcessRunRequest("cmd", new[] { "/d", "/c", "ping 127.0.0.1 -n 30 > nul" }, TimeSpan.FromMilliseconds(200))
            : new ProcessRunRequest("/bin/sh", new[] { "-c", "sleep 30" }, TimeSpan.FromMilliseconds(200));
        var elapsed = Stopwatch.StartNew();
        ProcessRunResult result = new SystemProcessRunner().RunAsync(request).GetAwaiter().GetResult();

        Assert.True(result.Started);
        Assert.True(result.TimedOut);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ProcessTerminationWaitIsBoundedWhenExitNeverCompletes()
    {
        var neverExits = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var elapsed = Stopwatch.StartNew();
        bool exited = SystemProcessRunner.WaitForExitWithinAsync(neverExits.Task, TimeSpan.FromMilliseconds(50))
            .GetAwaiter().GetResult();
        Assert.True(!exited);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SystemProcessRunnerCancelsLongRunningChildPromptly()
    {
        ProcessRunRequest request = OperatingSystem.IsWindows()
            ? new ProcessRunRequest("cmd", new[] { "/d", "/c", "ping 127.0.0.1 -n 30 > nul" }, TimeSpan.FromMinutes(1))
            : new ProcessRunRequest("/bin/sh", new[] { "-c", "sleep 30" }, TimeSpan.FromMinutes(1));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var elapsed = Stopwatch.StartNew();
        bool cancelled = false;
        try
        {
            _ = new SystemProcessRunner().RunAsync(request, cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.True(cancelled);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void WineSideImporterUsesWinepathAndSeparatedArgumentsForComplexPaths()
    {
        WithImportLayout((original, generated, fallback) =>
        {
            int call = 0;
            string? nativeDestination = null;
            var runner = new FakeRunner((request, _) =>
            {
                call++;
                Assert.Equal("osu-wine", request.FileName);
                if (call <= 2)
                {
                    Assert.Equal(new[] { "--wine", "winepath", "-w", request.Arguments[3] }, request.Arguments);
                    if (call == 1) Assert.Equal(generated, request.Arguments[3]);
                    else nativeDestination = request.Arguments[3];
                    return new ProcessRunResult(true, false, 0, "WINE|" + request.Arguments[3] + "\r\n", "", null);
                }

                Assert.Equal(new[]
                {
                    "--wine", "cmd", "/d", "/v:off", "/s", "/c",
                    "copy /y \"%HRANDOMPLUS_SOURCE%\" \"%HRANDOMPLUS_DESTINATION%\""
                }, request.Arguments);
                Assert.Equal("WINE|" + generated, request.Environment!["HRANDOMPLUS_SOURCE"]);
                Assert.Equal("WINE|" + nativeDestination, request.Environment["HRANDOMPLUS_DESTINATION"]);
                File.Copy(generated, nativeDestination!, overwrite: false);
                return new ProcessRunResult(true, false, 0, "1 file copied", "", null);
            });

            BeatmapImportResult result = new WineSideFileImporter(runner).ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(result.Success);
            Assert.True(result.AutomaticImportAttempted);
            Assert.True(!result.FallbackUsed);
            Assert.Equal(3, call);
            Assert.True(File.Exists(result.PreservedOutputPath));
            Assert.True(!File.Exists(generated));
            Assert.Contains("sourceWine=WINE|", result.Diagnostics!);
            Assert.True(!result.Diagnostics!.Contains("Z:\\", StringComparison.Ordinal));
        });
    }

    [Theory]
    [InlineData("space path")]
    [InlineData("apostrophe's")]
    [InlineData("bang!")]
    [InlineData("ampersand&")]
    [InlineData("pipe|")]
    [InlineData("percent%")]
    [InlineData("caret^")]
    [InlineData("less<greater>")]
    [InlineData("canción-日本語")]
    public void WineSideCopyKeepsSpecialWinePathsOutOfTheCommand(string specialPath)
    {
        WithImportLayout((original, generated, fallback) =>
        {
            int call = 0;
            string? destination = null;
            var runner = new FakeRunner((request, _) =>
            {
                call++;
                if (call <= 2)
                {
                    if (call == 2) destination = request.Arguments[3];
                    return new ProcessRunResult(true, false, 0, $"Z:\\{specialPath}\\item{call}.osu", "", null);
                }

                string commandLine = request.Arguments[^1];
                Assert.True(!commandLine.Contains(specialPath, StringComparison.Ordinal));
                Assert.Equal($"Z:\\{specialPath}\\item1.osu", request.Environment!["HRANDOMPLUS_SOURCE"]);
                Assert.Equal($"Z:\\{specialPath}\\item2.osu", request.Environment["HRANDOMPLUS_DESTINATION"]);
                File.Copy(generated, destination!, overwrite: false);
                return new ProcessRunResult(true, false, 0, "copied", "", null);
            });

            BeatmapImportResult result = new WineSideFileImporter(runner).ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(result.Success, result.Message);
            Assert.True(!result.FallbackUsed);
        });
    }

    [Theory]
    [InlineData("source-exit")]
    [InlineData("destination-exit")]
    [InlineData("source-empty")]
    [InlineData("copy-exit")]
    [InlineData("copy-timeout")]
    [InlineData("destination-missing")]
    public void WineSideFailuresUseNativeFallbackAndRecommendF5(string failure)
    {
        WithImportLayout((original, generated, fallback) =>
        {
            int call = 0;
            var runner = new FakeRunner((request, _) =>
            {
                call++;
                if (call == 1)
                {
                    if (failure == "source-exit") return new ProcessRunResult(true, false, 4, "", "source failed", null);
                    if (failure == "source-empty") return new ProcessRunResult(true, false, 0, "\r\n", "", null);
                    return new ProcessRunResult(true, false, 0, "WINE|" + request.Arguments[3], "", null);
                }
                if (call == 2)
                {
                    if (failure == "destination-exit") return new ProcessRunResult(true, false, 5, "", "destination failed", null);
                    return new ProcessRunResult(true, false, 0, "WINE|" + request.Arguments[3], "", null);
                }
                if (failure == "copy-exit") return new ProcessRunResult(true, false, 6, "", "copy failed", null);
                if (failure == "copy-timeout") return new ProcessRunResult(true, true, null, "", "", "timeout");
                return new ProcessRunResult(true, false, 0, "copied", "", null);
            });

            BeatmapImportResult result = new WineSideFileImporter(runner).ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(result.Success);
            Assert.True(result.AutomaticImportAttempted);
            Assert.True(result.FallbackUsed);
            Assert.Contains("F5", result.Message);
            Assert.True(File.Exists(result.PreservedOutputPath));
            Assert.True(!File.Exists(generated));
        });
    }

    [Fact]
    public void WineAndNativeFailurePreserveGeneratedInFallbackDirectory()
    {
        WithImportLayout((original, generated, fallback) =>
        {
            string songDirectory = Path.GetDirectoryName(original)!;
            Directory.Delete(songDirectory, recursive: true);
            File.WriteAllText(songDirectory, "blocks directory recreation");
            var runner = new FakeRunner((_, _) =>
                new ProcessRunResult(false, false, null, "", "", "osu-wine missing"));

            BeatmapImportResult result = new WineSideFileImporter(runner).ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(!result.Success);
            Assert.True(result.FallbackUsed);
            Assert.True(File.Exists(result.PreservedOutputPath));
            Assert.Equal(Path.GetFullPath(fallback), Path.GetDirectoryName(result.PreservedOutputPath));
            Assert.Contains("preserved", result.Message);
        });
    }

    [Fact]
    public void ImportPolicyUsesWineOnlyOnLinux()
    {
        Assert.True(BeatmapImportPolicy.ShouldUseWineSide(true));
        Assert.True(!BeatmapImportPolicy.ShouldUseWineSide(false));
    }

    [Fact]
    public void NativeSideImporterCopiesBesideOriginalAndRemovesStaging()
    {
        WithImportLayout((original, generated, fallback) =>
        {
            BeatmapImportResult result = new NativeSideFileImporter().ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(result.Success, result.Message);
            Assert.Equal(Path.GetDirectoryName(original), Path.GetDirectoryName(result.PreservedOutputPath));
            Assert.True(File.Exists(result.PreservedOutputPath));
            Assert.True(!File.Exists(generated));
        });
    }

    [Fact]
    public void NativeSideImporterNeverOverwritesAnExistingDifficulty()
    {
        WithImportLayout((original, generated, fallback) =>
        {
            string existing = Path.Combine(Path.GetDirectoryName(original)!, Path.GetFileName(generated));
            File.WriteAllText(existing, "existing difficulty");

            BeatmapImportResult result = new NativeSideFileImporter().ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(result.Success, result.Message);
            Assert.Equal("existing difficulty", File.ReadAllText(existing));
            Assert.True(!Path.GetFullPath(existing).Equals(Path.GetFullPath(result.PreservedOutputPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            Assert.True(File.Exists(result.PreservedOutputPath));
        });
    }

    [Fact]
    public void FailedImportPreservesAPortableArchiveInTheRequestedDirectory()
    {
        WithImportLayout((original, generated, fallback) =>
        {
            var importer = new PortableFallbackArchiveImporter(new FailedImporter());
            BeatmapImportResult result = importer.ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(!result.Success);
            Assert.True(result.ImportArchivePath is not null && File.Exists(result.ImportArchivePath));
            string archivePath = result.ImportArchivePath
                ?? throw new InvalidOperationException("The fallback archive path was not returned.");
            Assert.True(result.FallbackUsed);
            Assert.Equal(archivePath, result.PreservedOutputPath);
            Assert.Equal(Path.GetFullPath(fallback), Path.GetDirectoryName(archivePath));
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            Assert.True(archive.Entries.Any(entry => entry.FullName == "audio file.mp3"));
            Assert.True(archive.Entries.Any(entry => entry.FullName == Path.GetFileName(generated)));
            Assert.True(!archive.Entries.Any(entry => entry.FullName == Path.GetFileName(original)));
        });
    }

    [Fact]
    public void PortableFallbackUsesUniqueNamesAndDoesNotIncludeItsOwnDirectory()
    {
        WithImportLayout((original, generated, _) =>
        {
            string fallback = Path.Combine(Path.GetDirectoryName(original)!, "Failed Imports");
            Directory.CreateDirectory(fallback);
            File.WriteAllText(Path.Combine(fallback, "old.osz"), "old fallback");
            var importer = new PortableFallbackArchiveImporter(new FailedImporter());

            BeatmapImportResult first = importer.ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();
            BeatmapImportResult second = importer.ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(first.ImportArchivePath is not null && second.ImportArchivePath is not null);
            Assert.True(!first.ImportArchivePath!.Equals(second.ImportArchivePath, StringComparison.Ordinal));
            using ZipArchive firstArchive = ZipFile.OpenRead(first.ImportArchivePath);
            Assert.True(!firstArchive.Entries.Any(entry => entry.FullName.StartsWith("Failed Imports/", StringComparison.Ordinal)));
            Assert.Equal("old fallback", File.ReadAllText(Path.Combine(fallback, "old.osz")));
        });
    }

    [Fact]
    public void PortableFallbackRejectsTraversalAndPreservesTheOriginalFailure()
    {
        WithImportLayout((original, generated, fallback) =>
        {
            string resource = Path.Combine(Path.GetDirectoryName(original)!, "audio file.mp3");
            var context = new HRandomPlus.Integration.Beatmaps.LazerBeatmapSelectionContext(Guid.NewGuid(), fallback,
                new[] { new HRandomPlus.Integration.Beatmaps.BeatmapResource("../unsafe.mp3", resource) }, null);
            var importer = new PortableFallbackArchiveImporter(new FailedImporter());

            BeatmapImportResult result = importer.ImportAsync(
                new BeatmapImportRequest(original, generated, fallback, context)).GetAwaiter().GetResult();

            Assert.True(!result.Success);
            Assert.True(result.ImportArchivePath is null);
            Assert.Contains("All normal import methods failed", result.Message);
            Assert.Contains("Unsafe archive resource name", result.Message);
            Assert.True(File.Exists(generated));
            Assert.True(!Directory.Exists(fallback) || Directory.GetFiles(fallback, "*.osz").Length == 0);
        });
    }

    [Fact]
    public void PortableLazerFallbackNeutralizesBeatmapIdentifiers()
    {
        WithImportLayout((original, generated, fallback) =>
        {
            string beatmap = System.Text.Encoding.UTF8.GetString(TestBeatmaps.Mania(4,
                    new[] { TestBeatmaps.Note(4, 0, 1000) }))
                .Replace("Version:Test", "Version:Test\nBeatmapID:123\nBeatmapSetID:456", StringComparison.Ordinal);
            File.WriteAllText(generated, beatmap);
            string audio = Path.Combine(Path.GetDirectoryName(original)!, "audio file.mp3");
            var context = new HRandomPlus.Integration.Beatmaps.LazerBeatmapSelectionContext(Guid.NewGuid(), fallback,
                new[] { new HRandomPlus.Integration.Beatmaps.BeatmapResource("audio.mp3", audio) }, null);
            var importer = new PortableFallbackArchiveImporter(new FailedImporter());

            BeatmapImportResult result = importer.ImportAsync(
                new BeatmapImportRequest(original, generated, fallback, context)).GetAwaiter().GetResult();

            string archivePath = result.ImportArchivePath
                ?? throw new InvalidOperationException("The lazer fallback archive was not returned.");
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            ZipArchiveEntry map = archive.Entries.Single(entry => entry.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase));
            using var reader = new StreamReader(map.Open());
            string archivedBeatmap = reader.ReadToEnd();
            Assert.Contains("BeatmapID:0", archivedBeatmap);
            Assert.Contains("BeatmapSetID:0", archivedBeatmap);
        });
    }

    [Fact]
    public void DirectImporterLeavesCentralOutputInPlaceWithoutWine()
    {
        WithImportLayout((original, generated, fallback) =>
        {
            BeatmapImportResult result = new DirectFileImporter().ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();
            Assert.True(result.Success);
            Assert.True(!result.AutomaticImportAttempted);
            Assert.True(File.Exists(generated));
            Assert.Equal(generated, result.PreservedOutputPath);
        });
    }

    [Fact]
    public void WinelloArchiveImporterUsesSafeArgumentsAndIncludesResources()
    {
        WithImportLayout((original, generated, fallback) =>
        {
            var runner = new FakeRunner((request, _) =>
            {
                Assert.Equal("osu-wine", request.FileName);
                Assert.Equal(new[] { "--osuhandler", request.Arguments[1] }, request.Arguments);
                Assert.True(File.Exists(request.Arguments[1]));
                using ZipArchive archive = ZipFile.OpenRead(request.Arguments[1]);
                string[] entries = archive.Entries.Select(e => e.FullName).ToArray();
                Assert.True(entries.Contains("audio file.mp3"));
                Assert.True(entries.Contains(Path.GetFileName(generated)));
                return new ProcessRunResult(true, false, 0, "ok", "", null);
            });

            BeatmapImportResult result = new WinelloArchiveImporter(runner).ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(result.Success);
            Assert.True(result.AutomaticImportAttempted);
            Assert.Equal<string?>(null, result.ImportArchivePath);
            Assert.True(File.Exists(generated));
        });
    }

    [Fact]
    public void WinelloArchiveImporterPreservesOutputAndArchiveOnNonzeroExit()
        => VerifyImportFailure(new ProcessRunResult(true, false, 7, "", "handler failed", "exit 7"), "exit code 7");

    [Fact]
    public void WinelloArchiveImporterPreservesOutputAndArchiveOnTimeout()
        => VerifyImportFailure(new ProcessRunResult(true, true, null, "", "", "timeout"), "timed out");

    [Fact]
    public void WinelloArchiveImporterPreservesOutputAndArchiveWhenProcessIsMissing()
        => VerifyImportFailure(new ProcessRunResult(false, false, null, "", "", "not found"), "could not be started");

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void WinelloCleanupNeverMasksThePrimaryResult(bool operationSucceeds, bool cleanupSucceeds)
    {
        WithImportLayout((original, generated, fallback) =>
        {
            string temporaryBase = Path.Combine(Path.GetDirectoryName(fallback)!, "temporary-base");
            var cleaner = new FakeCleaner(cleanupSucceeds);
            var warnings = new List<string>();
            var runner = new FakeRunner((_, _) => operationSucceeds
                ? new ProcessRunResult(true, false, 0, "ok", "", null)
                : throw new IOException("primary operation failed"));

            BeatmapImportResult result = new WinelloArchiveImporter(runner, temporaryBase: temporaryBase,
                    cleaner: cleaner, warningSink: warnings.Add)
                .ImportAsync(new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.Equal(operationSucceeds, result.Success);
            if (!operationSucceeds) Assert.Contains("primary operation failed", result.Message);
            Assert.Equal(1, cleaner.Calls);
            Assert.Equal(cleanupSucceeds ? 0 : 1, warnings.Count);
        });
    }

    [Fact]
    public void SafeTemporaryCleanerAcceptsMissingChildAndRejectsUnexpectedPath()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusCleanup", Guid.NewGuid().ToString("N"));
        string child = Path.Combine(root, "missing");
        string outside = Path.Combine(Path.GetTempPath(), "outside", Guid.NewGuid().ToString("N"));
        var cleaner = new SafeTemporaryDirectoryCleaner();
        Assert.True(cleaner.TryDelete(child, root, out string? missingWarning));
        Assert.True(missingWarning is null);
        Assert.True(!cleaner.TryDelete(outside, root, out string? warning));
        Assert.Contains("unexpected path", warning!);
    }

    [Fact]
    public void SafeTemporaryCleanerReportsIoFailureWithoutThrowing()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusCleanup", Guid.NewGuid().ToString("N"));
        string child = Path.Combine(root, "child");
        Directory.CreateDirectory(child);
        try
        {
            var cleaner = new SafeTemporaryDirectoryCleaner((_, _) => throw new IOException("locked"));
            Assert.True(!cleaner.TryDelete(child, root, out string? warning));
            Assert.Contains("locked", warning!);
        }
        finally { Directory.Delete(root, true); }
    }

    private static void VerifyImportFailure(ProcessRunResult processResult, string expectedMessage)
    {
        WithImportLayout((original, generated, fallback) =>
        {
            var runner = new FakeRunner((_, _) => processResult);
            BeatmapImportResult result = new WinelloArchiveImporter(runner).ImportAsync(
                new BeatmapImportRequest(original, generated, fallback)).GetAwaiter().GetResult();

            Assert.True(!result.Success);
            Assert.True(result.AutomaticImportAttempted);
            Assert.Contains(expectedMessage, result.Message);
            Assert.True(File.Exists(generated));
            Assert.True(result.ImportArchivePath is not null && File.Exists(result.ImportArchivePath));
        });
    }

    private static void WithImportLayout(Action<string, string, string> test)
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlus Import ü!", Guid.NewGuid().ToString("N"));
        string song = Path.Combine(root, "Songs", "123 Petit Rabbit's - 日本語!");
        string generatedDirectory = Path.Combine(root, "Generated Beatmaps");
        string fallback = Path.Combine(root, "Fallback Archives");
        Directory.CreateDirectory(song);
        Directory.CreateDirectory(generatedDirectory);
        string original = Path.Combine(song, "original.osu");
        string generated = Path.Combine(generatedDirectory, "generated canción 日本語!.osu");
        File.WriteAllText(original, "osu file format v14");
        File.WriteAllText(generated, "osu file format v14");
        File.WriteAllText(Path.Combine(song, "audio file.mp3"), "audio");
        try { test(original, generated, fallback); }
        finally { Directory.Delete(root, true); }
    }

    private sealed class FakeRunner(Func<ProcessRunRequest, CancellationToken, ProcessRunResult> run) : IProcessRunner
    {
        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(run(request, cancellationToken));
    }

    private sealed class FailedImporter : IBeatmapImporter
    {
        public Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BeatmapImportResult("failed", true, false, request.GeneratedPath,
                "All normal import methods failed."));
    }

    private sealed class FakeCleaner(bool succeeds) : ITemporaryDirectoryCleaner
    {
        public int Calls { get; private set; }
        public bool TryDelete(string directory, string expectedRoot, out string? warning)
        {
            Calls++;
            warning = succeeds ? null : "cleanup failed";
            return succeeds;
        }
    }
}
