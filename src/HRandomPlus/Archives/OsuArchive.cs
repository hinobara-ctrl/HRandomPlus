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
        string tempRoot = Path.Combine(Path.GetTempPath(), "HRandomPlus", Guid.NewGuid().ToString("N"));
        string extractRoot = Path.Combine(tempRoot, "extracted");
        string pendingOutput = Path.Combine(tempRoot, "result.osz");
        Directory.CreateDirectory(extractRoot);

        try
        {
            ExtractSafely(inputPath, extractRoot);
            var originalHashes = HashArchiveEntries(inputPath);
            var expected = new List<(OsuBeatmapDocument Original, string OutputRelativePath)>();
            var processedOriginalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var report = new ArchiveReport { Input = inputPath, Output = outputPath, Seed = seed };

            string[] osuFiles = Directory.GetFiles(extractRoot, "*.osu", SearchOption.AllDirectories);
            if (osuFiles.Length == 0)
                throw new InvalidDataException("El OSZ no contiene archivos .osu.");

            foreach (string osuFile in osuFiles)
            {
                string relative = NormalizeRelative(Path.GetRelativePath(extractRoot, osuFile));
                byte[] originalBytes = File.ReadAllBytes(osuFile);
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
                if (!outputFile.Equals(osuFile, StringComparison.OrdinalIgnoreCase) && File.Exists(outputFile))
                    throw new IOException($"El nombre de dificultad generado colisiona con otro archivo: {outputRelative}");
                Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                File.WriteAllBytes(outputFile, outputBytes);
                if (!outputFile.Equals(osuFile, StringComparison.OrdinalIgnoreCase))
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
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
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
        var outputHashes = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name))
            .ToDictionary(e => NormalizeRelative(e.FullName), HashEntry, StringComparer.OrdinalIgnoreCase);

        // Every resulting difficulty must remain parseable, including untouched non-mania maps.
        foreach (ZipArchiveEntry osuEntry in archive.Entries.Where(e =>
                     e.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)))
        {
            using Stream osuStream = osuEntry.Open();
            using var osuMemory = new MemoryStream();
            osuStream.CopyTo(osuMemory);
            _ = OsuBeatmapDocument.Parse(osuEntry.FullName, osuMemory.ToArray());
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
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            OsuBeatmapDocument reparsed = OsuBeatmapDocument.Parse(outputRelative, memory.ToArray());
            BeatmapValidator.ValidateTransformation(original, reparsed);
        }
    }

    private static void ExtractSafely(string archivePath, string destination)
    {
        using var archive = ZipFile.OpenRead(archivePath);
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
            entry.ExtractToFile(target, overwrite: false);
        }
    }

    private static Dictionary<string, string> HashArchiveEntries(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        return archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name))
                      .ToDictionary(e => NormalizeRelative(e.FullName), HashEntry, StringComparer.OrdinalIgnoreCase);
    }

    private static string HashEntry(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static string ResolveInside(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Ruta insegura dentro del ZIP: {relative}");
        return fullPath;
    }

    private static string NormalizeRelative(string path) => path.Replace('\\', '/').TrimStart('/');
}
