using System.IO.Compression;
using HRandomPlus.Integration.Importing;

namespace HRandomPlus.Tests;

public class ImportIntegrationTests
{
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
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlus Import ü", Guid.NewGuid().ToString("N"));
        string song = Path.Combine(root, "Songs", "123 Artist - Song");
        string generatedDirectory = Path.Combine(root, "Generated Beatmaps");
        string fallback = Path.Combine(root, "Fallback Archives");
        Directory.CreateDirectory(song);
        Directory.CreateDirectory(generatedDirectory);
        string original = Path.Combine(song, "original.osu");
        string generated = Path.Combine(generatedDirectory, "generated ü.osu");
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
