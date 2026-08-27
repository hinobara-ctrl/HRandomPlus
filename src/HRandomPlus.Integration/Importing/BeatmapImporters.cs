using System.IO.Compression;

namespace HRandomPlus.Integration.Importing;

public sealed record BeatmapImportRequest(string OriginalPath, string GeneratedPath, string FallbackDirectory);

public sealed record BeatmapImportResult(string Strategy, bool AutomaticImportAttempted, bool Success,
                                         string PreservedOutputPath, string Message, string? ImportArchivePath = null);

public interface IBeatmapImporter
{
    Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request, CancellationToken cancellationToken = default);
}

public sealed class DirectFileImporter : IBeatmapImporter
{
    public Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request, CancellationToken cancellationToken = default)
    {
        string generated = Path.GetFullPath(request.GeneratedPath);
        if (!File.Exists(generated))
            return Task.FromResult(new BeatmapImportResult("direct-file", false, false, generated,
                "The generated beatmap could not be found."));
        return Task.FromResult(new BeatmapImportResult("direct-file", false, true, generated,
            "The generated beatmap was preserved at the output path."));
    }
}

public sealed class WinelloArchiveImporter : IBeatmapImporter
{
    private readonly IProcessRunner processRunner;
    private readonly string command;
    private readonly TimeSpan timeout;

    public WinelloArchiveImporter(IProcessRunner processRunner, string command = "osu-wine", TimeSpan? timeout = null)
    {
        this.processRunner = processRunner;
        this.command = command;
        this.timeout = timeout ?? TimeSpan.FromSeconds(20);
    }

    public async Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request, CancellationToken cancellationToken = default)
    {
        string original = Path.GetFullPath(request.OriginalPath);
        string generated = Path.GetFullPath(request.GeneratedPath);
        if (!File.Exists(generated))
            return new BeatmapImportResult("winello-osz", false, false, generated,
                "The generated beatmap could not be found.");

        string sourceDirectory = Path.GetDirectoryName(original)
            ?? throw new InvalidDataException("The original beatmap has no parent directory.");
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "HRandomPlus", "imports", Guid.NewGuid().ToString("N"));
        string temporaryArchive = Path.Combine(temporaryRoot, "HRandomPlus-import.osz");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            ZipFile.CreateFromDirectory(sourceDirectory, temporaryArchive, CompressionLevel.Optimal, includeBaseDirectory: false);
            EnsureGeneratedBeatmapIsIncluded(temporaryArchive, sourceDirectory, generated);
            var run = new ProcessRunRequest(command, new[] { "--osuhandler", temporaryArchive }, timeout);
            ProcessRunResult result = await processRunner.RunAsync(run, cancellationToken).ConfigureAwait(false);
            if (result.Success)
                return new BeatmapImportResult("winello-osz", true, true, generated,
                    "osu-winello accepted the import request.");

            string preservedArchive = PreserveArchive(temporaryArchive, request.FallbackDirectory);
            string reason = result.TimedOut ? "osu-winello timed out."
                : !result.Started ? $"osu-winello could not be started: {result.Error}"
                : $"osu-winello failed with exit code {result.ExitCode}: {result.StandardError}";
            return new BeatmapImportResult("winello-osz", true, false, generated,
                reason.Trim(), preservedArchive);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            string? preservedArchive = File.Exists(temporaryArchive)
                ? PreserveArchive(temporaryArchive, request.FallbackDirectory)
                : null;
            return new BeatmapImportResult("winello-osz", true, false, generated,
                $"Automatic import failed: {ex.Message}", preservedArchive);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static string PreserveArchive(string source, string fallbackDirectory)
    {
        string directory = Path.GetFullPath(fallbackDirectory);
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "HRandomPlus-import.osz");
        for (int index = 2; File.Exists(destination); index++)
            destination = Path.Combine(directory, $"HRandomPlus-import-{index}.osz");
        File.Move(source, destination);
        return destination;
    }

    private static void EnsureGeneratedBeatmapIsIncluded(string archivePath, string sourceDirectory, string generatedPath)
    {
        string sourceRoot = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;
        string entryName = generatedPath.StartsWith(sourceRoot, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            ? Path.GetRelativePath(sourceDirectory, generatedPath)
            : Path.GetFileName(generatedPath);
        entryName = entryName.Replace('\\', '/');
        using ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        if (archive.Entries.Any(entry => entry.FullName.Equals(entryName, StringComparison.OrdinalIgnoreCase))) return;
        archive.CreateEntryFromFile(generatedPath, entryName, CompressionLevel.Optimal);
    }
}
