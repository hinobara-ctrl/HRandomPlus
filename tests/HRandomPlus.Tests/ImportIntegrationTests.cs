using System.IO.Compression;
using HRandomPlus.Integration.Importing;

namespace HRandomPlus.Tests;

public class ImportIntegrationTests
{
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
                    "--wine", "cmd", "/d", "/c", "copy", "/y",
                    "WINE|" + generated, "WINE|" + nativeDestination
                }, request.Arguments);
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
    public void ImportPolicyUsesWineOnlyForLinuxBesideBeatmap()
    {
        Assert.True(BeatmapImportPolicy.ShouldUseWineSide(true, true));
        Assert.True(!BeatmapImportPolicy.ShouldUseWineSide(true, false));
        Assert.True(!BeatmapImportPolicy.ShouldUseWineSide(false, true));
        Assert.True(!BeatmapImportPolicy.ShouldUseWineSide(false, false));
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
}
