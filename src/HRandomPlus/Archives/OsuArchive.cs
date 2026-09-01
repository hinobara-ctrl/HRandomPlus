using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Randomization;
using HRandomPlus.Validation;

namespace HRandomPlus.Archives;

public sealed class OsuArchive
{
    private const int MaximumArchiveEntries = 10_000;
    private const long MaximumExpandedBytes = 8L * 1024 * 1024 * 1024;
    private const long MaximumEntryBytes = 2L * 1024 * 1024 * 1024;
    private const long MaximumBeatmapBytes = 64L * 1024 * 1024;
    private readonly IArchiveTemporaryDirectoryCleaner temporaryDirectoryCleaner;
    private readonly Action<string>? cleanupWarning;

    public OsuArchive(IArchiveTemporaryDirectoryCleaner? temporaryDirectoryCleaner = null,
                      Action<string>? cleanupWarning = null)
    {
        this.temporaryDirectoryCleaner = temporaryDirectoryCleaner ?? new ArchiveTemporaryDirectoryCleaner();
        this.cleanupWarning = cleanupWarning;
    }

    public ArchiveReport Process(string inputPath, string outputPath, HRandomConfig config,
                                 IReadOnlyCollection<string> difficultyFilters, bool overwrite)
    {
        config.Validate();
        inputPath = Path.GetFullPath(inputPath);
        outputPath = Path.GetFullPath(outputPath);
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("No se encontró el archivo OSZ.", inputPath);
        if (!Path.GetExtension(inputPath).Equals(".osz", StringComparison.OrdinalIgnoreCase) &&
            !Path.GetExtension(inputPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("La entrada debe ser un archivo .osz o .zip.");
        if (File.Exists(outputPath) && !overwrite)
            throw new IOException($"El archivo de salida ya existe: {outputPath}. Usa --overwrite para reemplazarlo.");

        long seed = config.Seed ?? SeededRandom.CreateSeed();
        string temporaryParent = Path.Combine(Path.GetTempPath(), "HRandomPlus");
        string tempRoot = Path.Combine(temporaryParent, Guid.NewGuid().ToString("N"));
        string extractRoot = Path.Combine(tempRoot, "extracted");
        string pendingOutput = Path.Combine(tempRoot, "result.osz");
        Directory.CreateDirectory(extractRoot);

        try
        {
            ExtractSafely(inputPath, extractRoot);
            var originalHashes = HashArchiveEntries(inputPath);
            var expected = new List<(OsuBeatmapDocument Original, string OutputRelativePath)>();
            var processedOriginalPaths = new HashSet<string>(StringComparer.Ordinal);
            var report = new ArchiveReport { Input = inputPath, Output = outputPath, Seed = seed };

            string[] osuFiles = Directory.GetFiles(extractRoot, "*.osu", SearchOption.AllDirectories);
            if (osuFiles.Length == 0)
                throw new InvalidDataException("El OSZ no contiene archivos .osu.");

            foreach (string osuFile in osuFiles)
            {
                string relative = NormalizeRelative(Path.GetRelativePath(extractRoot, osuFile));
                byte[] originalBytes = ReadBeatmapFile(osuFile);
                OsuBeatmapDocument document = OsuBeatmapDocument.Parse(relative, originalBytes);
                if (document.Mode != 3 || !Matches(document, relative, difficultyFilters))
                    continue;

                BeatmapValidator.ValidatePlayableStructure(document.HitObjects, document.Keys, assigned: false);
                string originalVersion = document.Version;
                var engine = new HRandomPlusEngine(config);
                RandomizationResult result = engine.Randomize(document.HitObjects, document.Keys, seed);
                BeatmapValidator.ValidatePlayableStructure(document.HitObjects, document.Keys, assigned: true);
                document.ApplyObjects();
                if (config.RenameDifficulty)
                    document.AppendVersionSuffix(config.DifficultySuffix);

                byte[] outputBytes = document.ToBytes();
                string outputRelative = config.RenameDifficulty
                    ? RenameDifficultyFile(relative, config.DifficultySuffix)
                    : relative;
                string outputFile = ResolveInside(extractRoot, outputRelative);
                if (!outputFile.Equals(osuFile, FileSystemPathComparison) && File.Exists(outputFile))
                    throw new IOException($"El nombre de dificultad generado colisiona con otro archivo: {outputRelative}");
                Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                File.WriteAllBytes(outputFile, outputBytes);
                if (!outputFile.Equals(osuFile, FileSystemPathComparison))
                    File.Delete(osuFile);

                expected.Add((OsuBeatmapDocument.Parse(relative, originalBytes), outputRelative));
                processedOriginalPaths.Add(relative);
                report.Difficulties.Add(new DifficultyReport
                {
                    OriginalFile = relative,
                    OutputFile = outputRelative,
                    OriginalVersion = originalVersion,
                    OutputVersion = document.Version,
                    Before = result.Before,
                    After = result.After
                });
            }

            if (report.Difficulties.Count == 0)
            {
                string selector = difficultyFilters.Count == 0 ? "ninguna dificultad mania" : "ninguna dificultad seleccionada";
                throw new InvalidDataException($"No se procesó {selector}.");
            }

            ZipFile.CreateFromDirectory(extractRoot, pendingOutput, CompressionLevel.Optimal, includeBaseDirectory: false);
            ValidateArchive(pendingOutput, originalHashes, processedOriginalPaths, expected);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            File.Move(pendingOutput, outputPath);
            return report;
        }
        finally
        {
            TryCleanupTemporaryDirectory(tempRoot, temporaryParent);
        }
    }

    public static void SaveReport(ArchiveReport report, string path)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(report, options));
    }

    private static bool Matches(OsuBeatmapDocument document, string relative,
                                IReadOnlyCollection<string> filters)
        => filters.Count == 0 || filters.Any(filter =>
            document.Version.Equals(filter, StringComparison.OrdinalIgnoreCase) ||
            relative.Equals(filter.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileNameWithoutExtension(relative).Equals(filter, StringComparison.OrdinalIgnoreCase));

    private static string RenameDifficultyFile(string relative, string suffix)
    {
        string directory = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? string.Empty;
        string safeSuffix = string.Concat(suffix.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        string name = Path.GetFileNameWithoutExtension(relative) + safeSuffix + ".osu";
        return string.IsNullOrEmpty(directory) ? name : directory + "/" + name;
    }

    private static void ValidateArchive(string archivePath, Dictionary<string, string> originalHashes,
                                        HashSet<string> processedOriginalPaths,
                                        List<(OsuBeatmapDocument Original, string OutputRelativePath)> expected)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        ValidateArchiveLimits(archive);
        var outputHashes = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name))
            .ToDictionary(e => NormalizeRelative(e.FullName), HashEntry, StringComparer.Ordinal);

        // Every resulting difficulty must remain parseable, including untouched non-mania maps.
        foreach (ZipArchiveEntry osuEntry in archive.Entries.Where(e =>
                     e.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)))
        {
            using Stream osuStream = osuEntry.Open();
            _ = OsuBeatmapDocument.Parse(osuEntry.FullName, ReadBeatmapEntry(osuEntry, osuStream));
        }

        foreach ((string path, string hash) in originalHashes)
        {
            if (processedOriginalPaths.Contains(path))
                continue;
            if (!outputHashes.TryGetValue(path, out string? outputHash) || outputHash != hash)
                throw new InvalidDataException($"El recurso sin modificar cambió o desapareció: {path}");
        }

        foreach ((OsuBeatmapDocument original, string outputRelative) in expected)
        {
            ZipArchiveEntry entry = archive.GetEntry(outputRelative.Replace('\\', '/'))
                ?? throw new InvalidDataException($"Falta la dificultad generada: {outputRelative}");
            using Stream stream = entry.Open();
            OsuBeatmapDocument reparsed = OsuBeatmapDocument.Parse(outputRelative, ReadBeatmapEntry(entry, stream));
            BeatmapValidator.ValidateTransformation(original, reparsed);
        }
    }

    private static void ExtractSafely(string archivePath, string destination)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        ValidateArchiveLimits(archive);
        long totalWritten = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string relative = NormalizeRelative(entry.FullName);
            string target = ResolveInside(destination, relative);
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using Stream input = entry.Open();
            using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[81920];
            long entryWritten = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                entryWritten += read;
                totalWritten += read;
                if (entryWritten > MaximumEntryBytes || totalWritten > MaximumExpandedBytes)
                    throw new InvalidDataException("El OSZ excede los límites seguros de extracción.");
                output.Write(buffer, 0, read);
            }
        }
    }

    private static Dictionary<string, string> HashArchiveEntries(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        ValidateArchiveLimits(archive);
        return archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name))
                      .ToDictionary(e => NormalizeRelative(e.FullName), HashEntry, StringComparer.Ordinal);
    }

    private static string HashEntry(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static void ValidateArchiveLimits(ZipArchive archive)
    {
        if (archive.Entries.Count > MaximumArchiveEntries)
            throw new InvalidDataException($"El OSZ contiene más de {MaximumArchiveEntries:N0} entradas.");
        long total = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (entry.Length > MaximumEntryBytes)
                throw new InvalidDataException($"La entrada '{entry.FullName}' supera el límite expandido de 2 GiB.");
            try { total = checked(total + entry.Length); }
            catch (OverflowException) { throw new InvalidDataException("El tamaño expandido declarado del OSZ no es válido."); }
            if (total > MaximumExpandedBytes)
                throw new InvalidDataException("El OSZ supera el límite expandido total de 8 GiB.");
        }
    }

    private static byte[] ReadBeatmapFile(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > MaximumBeatmapBytes)
            throw new InvalidDataException($"La dificultad '{Path.GetFileName(path)}' supera el límite de 64 MiB.");
        return File.ReadAllBytes(path);
    }

    private static byte[] ReadBeatmapEntry(ZipArchiveEntry entry, Stream stream)
    {
        if (entry.Length > MaximumBeatmapBytes)
            throw new InvalidDataException($"La dificultad '{entry.FullName}' supera el límite de 64 MiB.");
        using var memory = new MemoryStream((int)entry.Length);
        byte[] buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (memory.Length + read > MaximumBeatmapBytes)
                throw new InvalidDataException($"La dificultad '{entry.FullName}' supera el límite de 64 MiB.");
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }

    private static string ResolveInside(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(fullRoot, comparison))
            throw new InvalidDataException($"Ruta insegura dentro del ZIP: {relative}");
        return fullPath;
    }

    private static string NormalizeRelative(string path) => path.Replace('\\', '/').TrimStart('/');

    private static StringComparison FileSystemPathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private void TryCleanupTemporaryDirectory(string temporaryPath, string expectedParent)
    {
        try
        {
            temporaryDirectoryCleaner.Delete(temporaryPath, expectedParent);
        }
        catch (Exception ex)
        {
            try { cleanupWarning?.Invoke($"No se pudo limpiar el directorio temporal '{temporaryPath}': {ex.Message}"); }
            catch { }
        }
    }
}

public interface IArchiveTemporaryDirectoryCleaner
{
    void Delete(string temporaryPath, string expectedParent);
}

public sealed class ArchiveTemporaryDirectoryCleaner : IArchiveTemporaryDirectoryCleaner
{
    public void Delete(string temporaryPath, string expectedParent)
    {
        string fullParent = Path.GetFullPath(expectedParent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;
        string fullTemporaryPath = Path.GetFullPath(temporaryPath);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullTemporaryPath.StartsWith(fullParent, comparison))
            throw new InvalidDataException($"La ruta temporal está fuera del directorio esperado: {temporaryPath}");
        if (Directory.Exists(fullTemporaryPath))
            Directory.Delete(fullTemporaryPath, recursive: true);
    }
}
