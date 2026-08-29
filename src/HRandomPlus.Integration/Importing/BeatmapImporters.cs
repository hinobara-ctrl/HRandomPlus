using System.IO.Compression;
using System.Security.Cryptography;

namespace HRandomPlus.Integration.Importing;

public sealed record BeatmapImportRequest(string OriginalPath, string GeneratedPath, string FallbackDirectory);

public sealed record BeatmapImportResult(string Strategy, bool AutomaticImportAttempted, bool Success,
                                         string PreservedOutputPath, string Message, string? ImportArchivePath = null,
                                         bool FallbackUsed = false, string? Diagnostics = null);

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

public static class BeatmapImportPolicy
{
    public static bool ShouldUseWineSide(bool isLinux, bool outputBesideBeatmap)
        => isLinux && outputBesideBeatmap;
}

public sealed class WineSideFileImporter : IBeatmapImporter
{
    private readonly IProcessRunner processRunner;
    private readonly string command;
    private readonly TimeSpan timeout;

    public WineSideFileImporter(IProcessRunner processRunner, string command = "osu-wine", TimeSpan? timeout = null)
    {
        this.processRunner = processRunner;
        this.command = command;
        this.timeout = timeout ?? TimeSpan.FromSeconds(15);
    }

    public async Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request,
        CancellationToken cancellationToken = default)
    {
        string original = Path.GetFullPath(request.OriginalPath);
        string generated = Path.GetFullPath(request.GeneratedPath);
        if (!File.Exists(generated))
            return new BeatmapImportResult("wine-side-copy", false, false, generated,
                "The generated beatmap could not be found.");

        string destination = FindUniqueDestination(original, generated);
        var diagnostics = new List<string>
        {
            $"sourceLinux={generated}",
            $"destinationLinux={destination}",
            $"command={command}"
        };

        try
        {
            (ProcessRunResult sourceResult, string? sourceWine) = await ConvertPathAsync(generated, cancellationToken)
                .ConfigureAwait(false);
            AppendProcessDiagnostics(diagnostics, "winepathSource", sourceResult);
            diagnostics.Add($"sourceWine={sourceWine ?? "<empty>"}");
            if (!sourceResult.Success || string.IsNullOrWhiteSpace(sourceWine))
                return NativeFallback(request, generated, destination, diagnostics,
                    FailureReason("source winepath", sourceResult, sourceWine));

            (ProcessRunResult destinationResult, string? destinationWine) = await ConvertPathAsync(destination, cancellationToken)
                .ConfigureAwait(false);
            AppendProcessDiagnostics(diagnostics, "winepathDestination", destinationResult);
            diagnostics.Add($"destinationWine={destinationWine ?? "<empty>"}");
            if (!destinationResult.Success || string.IsNullOrWhiteSpace(destinationWine))
                return NativeFallback(request, generated, destination, diagnostics,
                    FailureReason("destination winepath", destinationResult, destinationWine));

            ProcessRunResult copyResult = await processRunner.RunAsync(
                new ProcessRunRequest(command,
                    new[] { "--wine", "cmd", "/d", "/c", "copy", "/y", sourceWine, destinationWine }, timeout),
                cancellationToken).ConfigureAwait(false);
            AppendProcessDiagnostics(diagnostics, "wineCopy", copyResult);
            if (!copyResult.Success)
                return NativeFallback(request, generated, destination, diagnostics,
                    FailureReason("Wine-side copy", copyResult, "copy"));
            if (!File.Exists(destination) || !FilesMatch(generated, destination))
                return NativeFallback(request, generated, FindUniqueDestination(original, generated), diagnostics,
                    "Wine-side copy returned success, but the destination was missing or did not match the generated file.");

            TryDeleteStaging(generated, destination, diagnostics);
            diagnostics.Add("fallbackUsed=false");
            return new BeatmapImportResult("wine-side-copy", true, true, destination,
                "Difficulty copied through osu-winello; osu! should detect it without F5.",
                FallbackUsed: false, Diagnostics: string.Join("; ", diagnostics));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"unexpected={ex.GetType().Name}: {ex.Message}");
            return NativeFallback(request, generated, destination, diagnostics,
                $"Unexpected Wine-side import error: {ex.Message}");
        }
    }

    private async Task<(ProcessRunResult Result, string? WinePath)> ConvertPathAsync(string path,
        CancellationToken cancellationToken)
    {
        ProcessRunResult result = await processRunner.RunAsync(
            new ProcessRunRequest(command, new[] { "--wine", "winepath", "-w", path }, timeout),
            cancellationToken).ConfigureAwait(false);
        string converted = result.StandardOutput.Trim().TrimEnd('\r', '\n').Trim();
        return (result, converted.Length == 0 ? null : converted);
    }

    private static BeatmapImportResult NativeFallback(BeatmapImportRequest request, string generated,
        string destination, List<string> diagnostics, string wineFailure)
    {
        diagnostics.Add($"wineFailure={wineFailure}");
        diagnostics.Add("fallbackUsed=true");
        try
        {
            destination = EnsureUniqueDestination(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(generated, destination, overwrite: false);
            if (!FilesMatch(generated, destination))
                throw new IOException("The native fallback destination did not match the generated file.");
            TryDeleteStaging(generated, destination, diagnostics);
            return new BeatmapImportResult("wine-side-copy", true, true, destination,
                "Difficulty created correctly, but osu! may require F5 to detect it. " + wineFailure,
                FallbackUsed: true, Diagnostics: string.Join("; ", diagnostics));
        }
        catch (Exception fallbackError)
        {
            diagnostics.Add($"nativeFallbackError={fallbackError.GetType().Name}: {fallbackError.Message}");
            string preserved = PreserveGenerated(generated, request.FallbackDirectory, diagnostics);
            return new BeatmapImportResult("wine-side-copy", true, false, preserved,
                "Difficulty was generated and preserved, but automatic and native import failed. " +
                $"Import it manually or press F5 after copying it. {wineFailure} Native fallback: {fallbackError.Message}",
                FallbackUsed: true, Diagnostics: string.Join("; ", diagnostics));
        }
    }

    private static string FindUniqueDestination(string original, string generated)
    {
        string directory = Path.GetDirectoryName(original)
            ?? throw new InvalidDataException("The original beatmap has no parent directory.");
        return EnsureUniqueDestination(Path.Combine(directory, Path.GetFileName(generated)));
    }

    private static string EnsureUniqueDestination(string candidate)
    {
        if (!File.Exists(candidate)) return candidate;
        string directory = Path.GetDirectoryName(candidate)!;
        string baseName = Path.GetFileNameWithoutExtension(candidate);
        string extension = Path.GetExtension(candidate);
        for (int index = 2; ; index++)
        {
            string numbered = Path.Combine(directory, $"{baseName} {index}{extension}");
            if (!File.Exists(numbered)) return numbered;
        }
    }

    private static string PreserveGenerated(string generated, string fallbackDirectory, List<string> diagnostics)
    {
        if (!File.Exists(generated)) return generated;
        string directory = Path.GetFullPath(fallbackDirectory);
        string generatedDirectory = Path.GetDirectoryName(generated)!;
        if (Path.GetFullPath(generatedDirectory).Equals(directory, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            return generated;
        try
        {
            Directory.CreateDirectory(directory);
            string destination = EnsureUniqueDestination(Path.Combine(directory, Path.GetFileName(generated)));
            File.Copy(generated, destination, overwrite: false);
            diagnostics.Add($"preservedFallback={destination}");
            return destination;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"preserveFallbackError={ex.GetType().Name}: {ex.Message}");
            return generated;
        }
    }

    private static bool FilesMatch(string source, string destination)
    {
        var sourceInfo = new FileInfo(source);
        var destinationInfo = new FileInfo(destination);
        if (!sourceInfo.Exists || !destinationInfo.Exists || sourceInfo.Length != destinationInfo.Length) return false;
        using FileStream sourceStream = File.OpenRead(source);
        using FileStream destinationStream = File.OpenRead(destination);
        return SHA256.HashData(sourceStream).SequenceEqual(SHA256.HashData(destinationStream));
    }

    private static void TryDeleteStaging(string generated, string destination, List<string> diagnostics)
    {
        if (Path.GetFullPath(generated).Equals(Path.GetFullPath(destination), OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
        try
        {
            File.Delete(generated);
            diagnostics.Add("stagingDeleted=true");
        }
        catch (Exception ex)
        {
            diagnostics.Add($"stagingDeleteError={ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string FailureReason(string step, ProcessRunResult result, string? value)
    {
        if (!result.Started) return $"{step} could not start: {result.Error}";
        if (result.TimedOut) return $"{step} timed out.";
        if (result.ExitCode != 0) return $"{step} failed with exit code {result.ExitCode}: {result.StandardError}".Trim();
        if (string.IsNullOrWhiteSpace(value)) return $"{step} returned an empty path.";
        return $"{step} failed.";
    }

    private static void AppendProcessDiagnostics(List<string> diagnostics, string step, ProcessRunResult result)
    {
        diagnostics.Add($"{step}.started={result.Started}");
        diagnostics.Add($"{step}.timedOut={result.TimedOut}");
        diagnostics.Add($"{step}.exitCode={result.ExitCode?.ToString() ?? "none"}");
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            diagnostics.Add($"{step}.stdout={result.StandardOutput.Trim()}");
        if (!string.IsNullOrWhiteSpace(result.StandardError))
            diagnostics.Add($"{step}.stderr={result.StandardError.Trim()}");
        if (!string.IsNullOrWhiteSpace(result.Error)) diagnostics.Add($"{step}.error={result.Error}");
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
