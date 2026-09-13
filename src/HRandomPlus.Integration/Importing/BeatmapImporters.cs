using System.IO.Compression;
using System.Security.Cryptography;
using System.Diagnostics;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;

namespace HRandomPlus.Integration.Importing;

public sealed record BeatmapImportRequest(string OriginalPath, string GeneratedPath, string FallbackDirectory,
                                          LazerBeatmapSelectionContext? LazerContext = null);

public sealed record BeatmapImportResult(string Strategy, bool AutomaticImportAttempted, bool Success,
                                         string PreservedOutputPath, string Message, string? ImportArchivePath = null,
                                         bool FallbackUsed = false, string? Diagnostics = null);

public static class BeatmapImportStatus
{
    public static string Format(string outputVersion, long seed, BeatmapImportResult import)
    {
        string outcome = !import.Success
            ? "Import: FAILED - manual import required"
            : import.FallbackUsed
                ? "Import: NATIVE FALLBACK - press F5 in osu!stable"
                : import.Strategy switch
                {
                    "wine-side-copy" => "Import: WINE-SIDE COPY COMPLETED",
                    "lazer-osz" => "Import: SENT TO osu!lazer - confirm in Song Select",
                    "winello-osz" => "Import: SENT TO osu!stable THROUGH WINELLO",
                    "native-side-copy" => "Import: NATIVE COPY COMPLETED",
                    _ => "Import: OUTPUT CREATED"
                };
        return $"{outcome}\nMap generated: {outputVersion}\nSeed: {seed}\nOutput: {import.PreservedOutputPath}\n{import.Message}";
    }
}

public interface IBeatmapImporter
{
    Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request, CancellationToken cancellationToken = default);
}

public interface ITemporaryDirectoryCleaner
{
    bool TryDelete(string directory, string expectedRoot, out string? warning);
}

public sealed class SafeTemporaryDirectoryCleaner : ITemporaryDirectoryCleaner
{
    private readonly Action<string, bool> delete;

    public SafeTemporaryDirectoryCleaner(Action<string, bool>? delete = null)
        => this.delete = delete ?? Directory.Delete;

    public bool TryDelete(string directory, string expectedRoot, out string? warning)
    {
        warning = null;
        try
        {
            string root = Path.GetFullPath(expectedRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                          + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(directory);
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!target.StartsWith(root, comparison))
            {
                warning = $"Temporary cleanup refused an unexpected path: {target}";
                return false;
            }
            if (!Directory.Exists(target)) return true;
            delete(target, true);
            return true;
        }
        catch (Exception ex)
        {
            warning = $"Temporary cleanup failed: {ex.Message}";
            return false;
        }
    }
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

public sealed class NativeSideFileImporter : IBeatmapImporter
{
    public Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string generated = Path.GetFullPath(request.GeneratedPath);
        if (!File.Exists(generated))
            return Task.FromResult(new BeatmapImportResult("native-side-copy", false, false, generated,
                "The generated beatmap could not be found."));

        try
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(request.OriginalPath))
                ?? throw new InvalidDataException("The original beatmap has no parent directory.");
            string destination;
            using (FileStream source = File.OpenRead(generated))
            {
                destination = UniqueFile.Write(Path.Combine(directory, Path.GetFileName(generated)), output =>
                {
                    source.CopyTo(output);
                    output.Flush();
                    source.Position = 0;
                    output.Position = 0;
                    if (!SHA256.HashData(source).SequenceEqual(SHA256.HashData(output)))
                        throw new IOException("The copied beatmap does not match the generated file.");
                });
            }
            try { File.Delete(generated); } catch { }
            return Task.FromResult(new BeatmapImportResult("native-side-copy", true, true, destination,
                "Difficulty copied beside the original beatmap."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new BeatmapImportResult("native-side-copy", true, false, generated,
                $"The difficulty was generated, but it could not be copied beside the original beatmap: {ex.Message}"));
        }
    }
}

public sealed class PortableFallbackArchiveImporter : IBeatmapImporter
{
    private readonly IBeatmapImporter inner;
    private readonly PortableArchiveLimits limits;

    public PortableFallbackArchiveImporter(IBeatmapImporter inner, PortableArchiveLimits? limits = null)
    {
        this.inner = inner;
        this.limits = limits ?? PortableArchiveLimits.Default;
        this.limits.Validate();
    }

    public async Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request,
        CancellationToken cancellationToken = default)
    {
        BeatmapImportResult result = await inner.ImportAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.Success || result.ImportArchivePath is not null) return result;

        try
        {
            string archive = CreateArchive(request, limits);
            return result with
            {
                ImportArchivePath = archive,
                PreservedOutputPath = archive,
                FallbackUsed = true,
                Message = result.Message + $" A portable .osz was preserved at: {archive}"
            };
        }
        catch (Exception ex)
        {
            return result with
            {
                Message = result.Message + $" The portable .osz fallback also failed: {ex.Message}"
            };
        }
    }

    private static string CreateArchive(BeatmapImportRequest request, PortableArchiveLimits limits)
    {
        string generated = Path.GetFullPath(request.GeneratedPath);
        if (!File.Exists(generated)) throw new FileNotFoundException("The generated beatmap no longer exists.", generated);
        string fallbackDirectory = Path.GetFullPath(request.FallbackDirectory);
        Directory.CreateDirectory(fallbackDirectory);
        string archivePath = Path.Combine(fallbackDirectory, Path.GetFileNameWithoutExtension(generated) + ".osz");

        var resources = new List<(string SourcePath, string EntryName)>();
        if (request.LazerContext is not null)
        {
            foreach (BeatmapResource resource in request.LazerContext.SetResources)
            {
                if (!File.Exists(resource.BlobPath) ||
                    resource.LogicalName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)) continue;
                resources.Add((resource.BlobPath, SafeEntryName(resource.LogicalName)));
            }
        }
        else
        {
            string sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(request.OriginalPath))
                ?? throw new InvalidDataException("The original beatmap has no parent directory.");
            StringComparison pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string fallbackPrefix = fallbackDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            foreach (string resourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string fullResourcePath = Path.GetFullPath(resourcePath);
                string extension = Path.GetExtension(fullResourcePath);
                if (fullResourcePath.StartsWith(fallbackPrefix, pathComparison) ||
                    extension.Equals(".osu", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".osz", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                resources.Add((fullResourcePath,
                    SafeEntryName(Path.GetRelativePath(sourceDirectory, fullResourcePath))));
            }
        }

        resources = resources
            .Where(resource => File.Exists(resource.SourcePath))
            .DistinctBy(resource => resource.EntryName, StringComparer.Ordinal)
            .ToList();
        ValidateLimits(generated, resources, limits);

        return UniqueFile.Write(archivePath, stream =>
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
            long expandedBytes = 0;
            foreach ((string sourcePath, string entryName) in resources)
                AddFile(archive, sourcePath, entryName, limits.MaximumEntryBytes,
                    limits.MaximumExpandedBytes, ref expandedBytes);

            string generatedName = SafeEntryName(Path.GetFileName(generated));
            ZipArchiveEntry generatedEntry = archive.CreateEntry(generatedName, CompressionLevel.Optimal);
            using Stream destination = generatedEntry.Open();
            if (request.LazerContext is null)
            {
                using FileStream source = File.OpenRead(generated);
                CopyLimited(source, destination, limits.MaximumBeatmapBytes,
                    limits.MaximumExpandedBytes, ref expandedBytes, generatedName);
            }
            else
            {
                OsuBeatmapDocument document = OsuBeatmapDocument.Parse(generated, File.ReadAllBytes(generated));
                document.SetBeatmapId(0);
                document.SetBeatmapSetId(0);
                byte[] bytes = document.ToBytes();
                if (bytes.LongLength > limits.MaximumBeatmapBytes)
                    throw new InvalidDataException($"Generated beatmap exceeds the {limits.MaximumBeatmapBytes}-byte fallback limit.");
                if (expandedBytes + bytes.LongLength > limits.MaximumExpandedBytes)
                    throw new InvalidDataException($"Portable fallback exceeds the {limits.MaximumExpandedBytes}-byte expanded-size limit.");
                destination.Write(bytes);
                expandedBytes += bytes.LongLength;
            }
        });
    }

    private static void ValidateLimits(string generated,
        IReadOnlyCollection<(string SourcePath, string EntryName)> resources, PortableArchiveLimits limits)
    {
        if (resources.Count + 1 > limits.MaximumEntries)
            throw new InvalidDataException($"Portable fallback exceeds the {limits.MaximumEntries}-entry limit.");
        long generatedBytes = new FileInfo(generated).Length;
        if (generatedBytes > limits.MaximumBeatmapBytes)
            throw new InvalidDataException($"Generated beatmap exceeds the {limits.MaximumBeatmapBytes}-byte fallback limit.");

        long total = generatedBytes;
        foreach ((string sourcePath, _) in resources)
        {
            long length = new FileInfo(sourcePath).Length;
            if (length > limits.MaximumEntryBytes)
                throw new InvalidDataException($"Fallback resource exceeds the {limits.MaximumEntryBytes}-byte entry limit: {Path.GetFileName(sourcePath)}");
            if (total > limits.MaximumExpandedBytes - length)
                throw new InvalidDataException($"Portable fallback exceeds the {limits.MaximumExpandedBytes}-byte expanded-size limit.");
            total += length;
        }
    }

    private static void AddFile(ZipArchive archive, string sourcePath, string entryName,
        long maximumEntryBytes, long maximumExpandedBytes, ref long expandedBytes)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream destination = entry.Open();
        using FileStream source = File.OpenRead(sourcePath);
        CopyLimited(source, destination, maximumEntryBytes, maximumExpandedBytes,
            ref expandedBytes, entryName);
    }

    private static void CopyLimited(Stream source, Stream destination, long maximumEntryBytes,
        long maximumExpandedBytes, ref long expandedBytes, string entryName)
    {
        byte[] buffer = new byte[81920];
        long entryBytes = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (entryBytes > maximumEntryBytes - read)
                throw new InvalidDataException($"Fallback entry exceeds the {maximumEntryBytes}-byte limit: {entryName}");
            if (expandedBytes > maximumExpandedBytes - read)
                throw new InvalidDataException($"Portable fallback exceeds the {maximumExpandedBytes}-byte expanded-size limit.");
            destination.Write(buffer, 0, read);
            entryBytes += read;
            expandedBytes += read;
        }
    }

    private static string SafeEntryName(string value)
    {
        string[] parts = value.Replace('\\', '/').TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
            throw new InvalidDataException($"Unsafe archive resource name: {value}");
        return string.Join('/', parts);
    }
}

public sealed record PortableArchiveLimits(
    int MaximumEntries = 10_000,
    long MaximumExpandedBytes = 8L * 1024 * 1024 * 1024,
    long MaximumEntryBytes = 2L * 1024 * 1024 * 1024,
    long MaximumBeatmapBytes = 64L * 1024 * 1024)
{
    public static PortableArchiveLimits Default { get; } = new();

    internal void Validate()
    {
        if (MaximumEntries < 1 || MaximumExpandedBytes < 1 || MaximumEntryBytes < 1 || MaximumBeatmapBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(PortableArchiveLimits), "Portable archive limits must be positive.");
    }
}

public interface IExternalFileLauncher
{
    bool Launch(string filePath, string? executablePath, out string? error);
}

public sealed class SystemExternalFileLauncher : IExternalFileLauncher
{
    public bool Launch(string filePath, string? executablePath, out string? error)
    {
        try
        {
            var start = new ProcessStartInfo { UseShellExecute = true };
            if (string.IsNullOrWhiteSpace(executablePath)) start.FileName = filePath;
            else
            {
                start.FileName = executablePath;
                start.ArgumentList.Add(filePath);
            }
            using Process? process = Process.Start(start);
            error = process is null ? "The operating system did not start an importer." : null;
            return process is not null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

public sealed class LazerArchiveImporter : IBeatmapImporter
{
    private readonly IExternalFileLauncher launcher;
    private readonly string temporaryRoot;

    public LazerArchiveImporter(IExternalFileLauncher? launcher = null, string? temporaryRoot = null)
    {
        this.launcher = launcher ?? new SystemExternalFileLauncher();
        this.temporaryRoot = temporaryRoot ?? Path.Combine(Path.GetTempPath(), "HRandomPlus", "lazer-imports");
    }

    public Task<BeatmapImportResult> ImportAsync(BeatmapImportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.LazerContext is null)
            return Task.FromResult(new BeatmapImportResult("lazer-osz", false, false, request.GeneratedPath,
                "No osu!lazer beatmap context was available."));
        if (!File.Exists(request.GeneratedPath))
            return Task.FromResult(new BeatmapImportResult("lazer-osz", false, false, request.GeneratedPath,
                "The generated beatmap could not be found."));

        string archivePath = Path.Combine(temporaryRoot, $"HRandomPlus-{Guid.NewGuid():N}.osz");
        bool archiveComplete = false;
        try
        {
            Directory.CreateDirectory(temporaryRoot);
            CleanupOldArchives();
            OsuBeatmapDocument generated = OsuBeatmapDocument.Parse(
                request.GeneratedPath, File.ReadAllBytes(request.GeneratedPath));
            var resources = request.LazerContext.SetResources
                .Where(resource => !resource.LogicalName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                .Select(resource => (Resource: resource, EntryName: SafeArchivePath(resource.LogicalName)))
                .ToArray();
            ValidateRequiredAudio(generated, resources);

            archivePath = UniqueFile.Write(archivePath, stream =>
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
                var addedEntries = new HashSet<string>(StringComparer.Ordinal);
                foreach ((BeatmapResource resource, string entryName) in resources)
                {
                    if (!File.Exists(resource.BlobPath)) continue;
                    if (addedEntries.Add(entryName))
                        archive.CreateEntryFromFile(resource.BlobPath, entryName, CompressionLevel.Optimal);
                }

                generated.SetBeatmapId(0);
                generated.SetBeatmapSetId(0);
                ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(request.GeneratedPath), CompressionLevel.Optimal);
                using Stream output = entry.Open();
                output.Write(generated.ToBytes());
            });
            archiveComplete = true;

            bool launched = launcher.Launch(archivePath, request.LazerContext.LazerExecutablePath, out string? error);
            if (!launched)
            {
                string preserved = PreserveArchive(archivePath, request.FallbackDirectory);
                return Task.FromResult(new BeatmapImportResult("lazer-osz", true, false, preserved,
                    $"The local variant was generated, but lazer import could not start: {error}. Archive preserved at: {preserved}", preserved));
            }

            _ = DeleteLaterAsync(archivePath);
            return Task.FromResult(new BeatmapImportResult("lazer-osz", true, true, request.GeneratedPath,
                "The randomised local variant was sent to osu!lazer without modifying its storage directly.", archivePath));
        }
        catch (Exception ex)
        {
            string? preserved = archiveComplete ? PreserveArchive(archivePath, request.FallbackDirectory) : null;
            string message = $"The local variant was generated, but its lazer archive failed: {ex.Message}";
            if (preserved is not null) message += $" Archive preserved at: {preserved}";
            return Task.FromResult(new BeatmapImportResult("lazer-osz", true, false, preserved ?? request.GeneratedPath,
                message, preserved));
        }
    }

    private static void ValidateRequiredAudio(OsuBeatmapDocument generated,
        IReadOnlyList<(BeatmapResource Resource, string EntryName)> resources)
    {
        if (string.IsNullOrWhiteSpace(generated.AudioFilename))
            throw new InvalidDataException("The generated beatmap does not identify its required main audio resource.");

        string audioEntry = SafeArchivePath(generated.AudioFilename);
        (BeatmapResource Resource, string EntryName)? match = resources
            .Where(item => item.EntryName.Equals(audioEntry, StringComparison.Ordinal))
            .Select(item => ((BeatmapResource Resource, string EntryName)?)item)
            .FirstOrDefault();
        if (match is null)
        {
            var insensitiveMatches = resources
                .Where(item => item.EntryName.Equals(audioEntry, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (insensitiveMatches.Length == 1) match = insensitiveMatches[0];
        }

        if (match is null || !File.Exists(match.Value.Resource.BlobPath))
            throw new FileNotFoundException(
                $"Required lazer audio resource '{generated.AudioFilename}' is missing from local storage.");
    }

    private static string SafeArchivePath(string logicalName)
    {
        string path = logicalName.Replace('\\', '/').TrimStart('/');
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
            throw new InvalidDataException($"Unsafe lazer resource name: {logicalName}");
        return string.Join('/', parts);
    }

    private static string PreserveArchive(string source, string fallbackDirectory)
    {
        try
        {
            Directory.CreateDirectory(fallbackDirectory);
            for (int index = 1; ; index++)
            {
                string destination = Path.Combine(fallbackDirectory, index == 1
                    ? "HRandomPlus-lazer-import.osz" : $"HRandomPlus-lazer-import-{index}.osz");
                try { File.Move(source, destination); return destination; }
                catch (IOException) when (File.Exists(destination) || Directory.Exists(destination)) { }
            }
        }
        catch
        {
            // A completed archive is still usable at its temporary path if preservation fails.
            return source;
        }
    }

    private static async Task DeleteLaterAsync(string path)
    {
        await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        try { File.Delete(path); } catch { }
    }

    private void CleanupOldArchives()
    {
        try
        {
            foreach (string path in Directory.EnumerateFiles(temporaryRoot, "HRandomPlus-*.osz"))
                if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-1)) File.Delete(path);
        }
        catch { }
    }
}

public static class BeatmapImportPolicy
{
    public static bool ShouldUseWineSide(bool isLinux) => isLinux;
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

        string destination = DestinationCandidate(original, generated);
        var diagnostics = new List<string>
        {
            $"sourceLinux={generated}",
            $"command={command}"
        };

        FileStream? nameReservation = null;
        string? reservationPath = null;
        try
        {
            (destination, reservationPath, nameReservation) = ReserveDestinationName(destination);
            // The reservation is deliberately not the final .osu. Wine must create the
            // final file so osu!stable receives the same creation event as a direct Wine copy.
            diagnostics.Add($"destinationLinux={destination}");
            diagnostics.Add($"nameReservation={reservationPath}");
            (ProcessRunResult sourceResult, string? sourceWine) = await ConvertPathAsync(generated, cancellationToken)
                .ConfigureAwait(false);
            AppendProcessDiagnostics(diagnostics, "winepathSource", sourceResult);
            diagnostics.Add($"sourceWine={sourceWine ?? "<empty>"}");
            if (!sourceResult.Success || string.IsNullOrWhiteSpace(sourceWine))
                return Fallback(
                    FailureReason("source winepath", sourceResult, sourceWine));

            (ProcessRunResult destinationResult, string? destinationWine) = await ConvertPathAsync(destination, cancellationToken)
                .ConfigureAwait(false);
            AppendProcessDiagnostics(diagnostics, "winepathDestination", destinationResult);
            diagnostics.Add($"destinationWine={destinationWine ?? "<empty>"}");
            if (!destinationResult.Success || string.IsNullOrWhiteSpace(destinationWine))
                return Fallback(
                    FailureReason("destination winepath", destinationResult, destinationWine));

            ProcessRunResult copyResult = await processRunner.RunAsync(
                new ProcessRunRequest(command,
                    new[]
                    {
                        "--wine", "cmd", "/d", "/v:off", "/s", "/c",
                        "copy /b /-y \"%HRANDOMPLUS_SOURCE%\" \"%HRANDOMPLUS_DESTINATION%\" <nul"
                    }, timeout,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["HRANDOMPLUS_SOURCE"] = sourceWine,
                        ["HRANDOMPLUS_DESTINATION"] = destinationWine
                    }),
                cancellationToken).ConfigureAwait(false);
            AppendProcessDiagnostics(diagnostics, "wineCopy", copyResult);
            if (!copyResult.Success)
                return Fallback(
                    FailureReason("Wine-side copy", copyResult, "copy"));
            if (!File.Exists(destination) || !FilesMatch(generated, destination))
                return Fallback(
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
            return Fallback(
                $"Unexpected Wine-side import error: {ex.Message}");
        }
        finally
        {
            CleanupReservation();
        }

        BeatmapImportResult Fallback(string reason)
        {
            CleanupReservation();
            return NativeFallback(request, generated, destination, diagnostics, reason);
        }

        void CleanupReservation()
        {
            nameReservation?.Dispose();
            nameReservation = null;
            if (reservationPath is not null)
                try { File.Delete(reservationPath); } catch { }
            reservationPath = null;
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
            destination = UniqueFile.Write(destination, stream =>
            {
                using FileStream source = File.OpenRead(generated);
                source.CopyTo(stream);
            });
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

    private static string DestinationCandidate(string original, string generated)
    {
        string directory = Path.GetDirectoryName(original)
            ?? throw new InvalidDataException("The original beatmap has no parent directory.");
        return Path.Combine(directory, Path.GetFileName(generated));
    }

    private static (string Destination, string ReservationPath, FileStream Reservation) ReserveDestinationName(
        string candidate)
    {
        string directory = Path.GetDirectoryName(candidate)!;
        Directory.CreateDirectory(directory);
        string baseName = Path.GetFileNameWithoutExtension(candidate);
        string extension = Path.GetExtension(candidate);
        for (int index = 1; ; index++)
        {
            string destination = index == 1
                ? candidate
                : Path.Combine(directory, $"{baseName} {index}{extension}");
            if (File.Exists(destination) || Directory.Exists(destination)) continue;
            string reservationPath = destination + ".hrandomplus-reservation";
            try
            {
                var reservation = new FileStream(reservationPath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.Read, 1, FileOptions.WriteThrough);
                if (!File.Exists(destination) && !Directory.Exists(destination))
                    return (destination, reservationPath, reservation);
                reservation.Dispose();
                try { File.Delete(reservationPath); } catch { }
            }
            catch (IOException ex) when (IsNameCollision(ex, reservationPath))
            {
                // Another HRandomPlus process owns this candidate; try the next name.
            }
        }
    }

    private static bool IsNameCollision(IOException exception, string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) return true;
        int nativeCode = exception.HResult & 0xffff;
        return nativeCode is 17 or 32 or 80 or 183;
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
            destination = UniqueFile.Write(destination, stream =>
            {
                using FileStream source = File.OpenRead(generated);
                source.CopyTo(stream);
            });
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
    private readonly string temporaryBase;
    private readonly ITemporaryDirectoryCleaner cleaner;
    private readonly Action<string> warningSink;

    public WinelloArchiveImporter(IProcessRunner processRunner, string command = "osu-wine", TimeSpan? timeout = null,
        string? temporaryBase = null, ITemporaryDirectoryCleaner? cleaner = null, Action<string>? warningSink = null)
    {
        this.processRunner = processRunner;
        this.command = command;
        this.timeout = timeout ?? TimeSpan.FromSeconds(20);
        this.temporaryBase = temporaryBase ?? Path.Combine(Path.GetTempPath(), "HRandomPlus", "imports");
        this.cleaner = cleaner ?? new SafeTemporaryDirectoryCleaner();
        this.warningSink = warningSink ?? (message => Trace.TraceWarning("Winello: {0}", message));
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
        string temporaryRoot = Path.Combine(temporaryBase, Guid.NewGuid().ToString("N"));
        string temporaryArchive = Path.Combine(temporaryRoot, "HRandomPlus-import.osz");
        Directory.CreateDirectory(temporaryRoot);
        bool archiveComplete = false;
        try
        {
            ZipFile.CreateFromDirectory(sourceDirectory, temporaryArchive, CompressionLevel.Optimal, includeBaseDirectory: false);
            EnsureGeneratedBeatmapIsIncluded(temporaryArchive, sourceDirectory, generated);
            archiveComplete = true;
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
            string? preservedArchive = archiveComplete
                ? PreserveArchive(temporaryArchive, request.FallbackDirectory)
                : null;
            return new BeatmapImportResult("winello-osz", true, false, generated,
                $"Automatic import failed: {ex.Message}", preservedArchive);
        }
        finally
        {
            if (!cleaner.TryDelete(temporaryRoot, temporaryBase, out string? warning) && warning is not null)
                warningSink(warning);
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
