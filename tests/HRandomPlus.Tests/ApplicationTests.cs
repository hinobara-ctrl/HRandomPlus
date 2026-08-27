using HRandomPlus.Beatmaps;
using HRandomPlus.Core;

namespace HRandomPlus.Tests;

public class ApplicationTests
{
    [Fact]
    public void ParsesSelectedRange()
    {
        BeatmapRange range = BeatmapRange.Parse("00:37:005 - 01:13:005 -");
        Assert.Equal(37005, range.StartMs);
        Assert.Equal(73005, range.EndMs);
        Assert.True(range.Contains(50000));
    }

    [Fact]
    public void CorruptSettingsRestoreDefaults()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusSettings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "config.json"), "{broken");
            AppSettings settings = new SettingsStore(root).Load();
            Assert.Equal("H-Random", settings.LastProfile);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void UnwritableSettingsDoNotPreventStartup()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusSettings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(root)!);
        File.WriteAllText(root, "this path is a file, not a writable directory");
        try
        {
            AppSettings settings = new SettingsStore(root).Load();
            Assert.Equal("H-Random", settings.LastProfile);
        }
        finally { File.Delete(root); }
    }

    [Fact]
    public void BuiltInProfilesAreIndependentData()
    {
        RandomProfile h = ProfileCatalog.BuiltIns.Single(p => p.Name == "H-Random");
        RandomProfile s = ProfileCatalog.BuiltIns.Single(p => p.Name == "S-Random");
        Assert.True(h.Config.Weights.JackPenalty > 0);
        Assert.Equal(0d, s.Config.Weights.JackPenalty);
        Assert.Equal(4096, s.Config.WeightedTopCandidates);
    }

    [Fact]
    public void DirectGenerationPreservesOriginalAndIsReproducible()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusDirect", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string input = Path.Combine(root, "map.osu");
            byte[] bytes = TestBeatmaps.Mania(4, Enumerable.Range(0, 20).Select(i => TestBeatmaps.Note(4, i % 4, 1000 + i * 100)));
            File.WriteAllBytes(input, bytes);
            var config = new HRandomConfig { Seed = 123, DifficultySuffix = " TEST" };
            var service = new BeatmapGenerationService();
            GenerationResult first = service.Generate(input, config, null);
            GenerationResult second = service.Generate(input, config, null);
            Assert.Equal(bytes, File.ReadAllBytes(input));
            OsuBeatmapDocument a = OsuBeatmapDocument.Parse(first.OutputPath, File.ReadAllBytes(first.OutputPath));
            OsuBeatmapDocument b = OsuBeatmapDocument.Parse(second.OutputPath, File.ReadAllBytes(second.OutputPath));
            Assert.Equal(a.HitObjects.Select(x => x.OriginalColumn), b.HitObjects.Select(x => x.OriginalColumn));
            Assert.True(first.OutputPath != second.OutputPath);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void DirectGenerationCanUseAnApplicationOutputDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusOutput", Guid.NewGuid().ToString("N"));
        string inputDirectory = Path.Combine(root, "Songs", "Map");
        string outputDirectory = Path.Combine(root, "Generated");
        Directory.CreateDirectory(inputDirectory);
        try
        {
            string input = Path.Combine(inputDirectory, "map.osu");
            byte[] bytes = TestBeatmaps.Mania(4, Enumerable.Range(0, 12).Select(i => TestBeatmaps.Note(4, i % 4, 1000 + i * 100)));
            File.WriteAllBytes(input, bytes);
            GenerationResult result = new BeatmapGenerationService().Generate(input,
                new HRandomConfig { Seed = 88, DifficultySuffix = " SAFE" }, null, outputDirectory);
            Assert.Equal(Path.GetFullPath(outputDirectory), Path.GetDirectoryName(result.OutputPath));
            Assert.Equal(bytes, File.ReadAllBytes(input));
            Assert.True(File.Exists(result.OutputPath));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SelectedRangeRespectsLongNotesCrossingItsBoundaries()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusRange", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string input = Path.Combine(root, "range.osu");
            byte[] bytes = TestBeatmaps.Mania(4, new[]
            {
                TestBeatmaps.LongNote(4, 0, 900, 1600),
                TestBeatmaps.Note(4, 1, 1100),
                TestBeatmaps.Note(4, 2, 1300),
                TestBeatmaps.LongNote(4, 3, 1400, 2200)
            });
            File.WriteAllBytes(input, bytes);
            GenerationResult result = new BeatmapGenerationService().Generate(input,
                new HRandomConfig { Seed = 44, DifficultySuffix = " RANGE" }, new BeatmapRange(1000, 1800));
            OsuBeatmapDocument output = OsuBeatmapDocument.Parse(result.OutputPath, File.ReadAllBytes(result.OutputPath));
            Assert.Equal(0, output.HitObjects[0].OriginalColumn);
            Assert.Equal(3, output.HitObjects[3].OriginalColumn);
            Assert.All(output.HitObjects.Where(h => h.StartTime is 1100 or 1300), h => Assert.True(h.OriginalColumn != 0));
        }
        finally { Directory.Delete(root, true); }
    }
}
