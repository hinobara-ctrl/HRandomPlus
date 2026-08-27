using System.Security.Cryptography;
using HRandomPlus.Core;
using HRandomPlus.Randomization;
using HRandomPlus.Validation;

namespace HRandomPlus.Beatmaps;

public sealed record GenerationResult(string OutputPath, string OutputVersion, long Seed, int RandomizedNotes);

public sealed class BeatmapGenerationService
{
    public GenerationResult Generate(string inputPath, HRandomConfig config, BeatmapRange? range)
        => Generate(inputPath, config, range, null);

    public GenerationResult Generate(string inputPath, HRandomConfig config, BeatmapRange? range, string? outputDirectory)
    {
        config.Validate();
        inputPath = Path.GetFullPath(inputPath);
        if (!File.Exists(inputPath)) throw new FileNotFoundException("No se encontró el beatmap.", inputPath);
        if (!Path.GetExtension(inputPath).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("El archivo debe tener extensión .osu.");

        byte[] originalBytes = File.ReadAllBytes(inputPath);
        byte[] originalHash = SHA256.HashData(originalBytes);
        OsuBeatmapDocument original = OsuBeatmapDocument.Parse(inputPath, originalBytes);
        if (original.Mode != 3) throw new InvalidDataException("El archivo seleccionado no es osu!mania (Mode:3).");
        if (original.HitObjects.Count == 0) throw new InvalidDataException("El beatmap no contiene HitObjects.");

        OsuBeatmapDocument output = OsuBeatmapDocument.Parse(inputPath, originalBytes);
        List<ManiaHitObject> selected = range is null
            ? output.HitObjects.ToList()
            : output.HitObjects.Where(h => range.Value.Contains(h.StartTime) &&
                                           (!h.IsLongNote || h.EndTime <= range.Value.EndMs)).ToList();
        if (selected.Count == 0) throw new InvalidDataException("El rango seleccionado no contiene notas.");

        IReadOnlyDictionary<int, int>? activeAtStart = range is null ? null : output.HitObjects
            .Where(h => h.IsLongNote && h.StartTime < range.Value.StartMs && h.EndTime >= range.Value.StartMs)
            .ToDictionary(h => h.OriginalColumn, h => h.EndTime!.Value);

        long seed = config.Seed ?? SeededRandom.CreateSeed();
        new HRandomPlusEngine(config).Randomize(selected, output.Keys, seed, activeAtStart);
        output.ApplyObjects();

        string suffix = FindUniqueSuffix(inputPath, output.Version, config.DifficultySuffix);
        if (config.RenameDifficulty) output.AppendVersionSuffix(suffix);
        output.SetBeatmapId(0);

        byte[] generated = output.ToBytes();
        OsuBeatmapDocument reparsed = OsuBeatmapDocument.Parse(inputPath, generated);
        BeatmapValidator.ValidateTransformation(original, reparsed);
        if (!SHA256.HashData(File.ReadAllBytes(inputPath)).SequenceEqual(originalHash))
            throw new IOException("El beatmap original cambió durante la operación; no se escribió ninguna salida.");

        string outputPath = FindUniquePath(inputPath, suffix, outputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, generated);
        return new GenerationResult(outputPath, output.Version, seed, selected.Count);
    }

    public static string FindUniquePath(string originalPath, string suffix, string? outputDirectory = null)
    {
        string directory = outputDirectory is null
            ? Path.GetDirectoryName(Path.GetFullPath(originalPath))!
            : Path.GetFullPath(outputDirectory);
        string safe = string.Concat(suffix.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        string baseName = Path.GetFileNameWithoutExtension(originalPath);
        string candidate = Path.Combine(directory, $"{baseName} {safe}.osu");
        for (int index = 2; File.Exists(candidate); index++)
            candidate = Path.Combine(directory, $"{baseName} {safe} {index}.osu");
        return candidate;
    }

    private static string FindUniqueSuffix(string originalPath, string version, string suffix)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(originalPath))!;
        var versions = Directory.EnumerateFiles(directory, "*.osu")
            .Select(path => { try { return OsuBeatmapDocument.Parse(path, File.ReadAllBytes(path)).Version; } catch { return string.Empty; } })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string candidate = suffix;
        for (int index = 2; versions.Contains(version + candidate); index++) candidate = suffix + " " + index;
        return candidate;
    }
}
