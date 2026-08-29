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
    public void NewSettingsWriteBesideBeatmapByDefault()
    {
        Assert.True(new AppSettings().OutputToBeatmapFolder);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutputLocationPreferenceRoundTrips(bool expected)
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusOutputSetting", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(root);
            store.Save(new AppSettings { OutputToBeatmapFolder = expected });
            Assert.Equal(expected, store.Load().OutputToBeatmapFolder);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReadOnlySettingsLoadDoesNotCreateFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusReadOnlySettings", Guid.NewGuid().ToString("N"));
        AppSettings settings = new SettingsStore(root).LoadReadOnly();
        Assert.True(settings.OutputToBeatmapFolder);
        Assert.True(!Directory.Exists(root));
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
    public void CustomProfileRoundTripPreservesSeed()
    {
        AppSettings loaded = RoundTripSettings(new RandomProfile
        {
            Name = "Seeded",
            Config = new HRandomConfig { Seed = 987654321, DifficultySuffix = " SEEDED" }
        });

        Assert.Equal(987654321L, Assert.Single(loaded.CustomProfiles).Config.Seed);
    }

    [Fact]
    public void CustomProfileRoundTripPreservesEmptySeedAsRandom()
    {
        AppSettings loaded = RoundTripSettings(new RandomProfile
        {
            Name = "Random seed",
            Config = new HRandomConfig { Seed = null, DifficultySuffix = " RANDOM" }
        });

        Assert.Equal<long?>(null, Assert.Single(loaded.CustomProfiles).Config.Seed);
    }

    [Fact]
    public void CustomProfileRoundTripPreservesEveryConfigField()
    {
        var expected = new HRandomConfig
        {
            Seed = -123456789,
            DynamicThreshold = false,
            MinThresholdMs = 11,
            BaseThresholdMs = 22,
            MaxThresholdMs = 33,
            RecentUsageWindow = 44,
            PatternHistoryLength = 55,
            WeightedTopCandidates = 7,
            WeightedTemperature = 6.5,
            MaxCandidateSets = 777,
            RenameDifficulty = false,
            DifficultySuffix = " ROUNDTRIP",
            Weights = new ScoringWeights
            {
                TimeSinceLastUseBonus = 1,
                HandBalanceBonus = 2,
                DistributionBonus = 3,
                JackPenalty = 4,
                TrillPenalty = 5,
                RepeatedPatternPenalty = 6,
                SameHandPenalty = 7,
                ExtremeJumpPenalty = 8,
                RecentUsagePenalty = 9
            }
        };

        HRandomConfig actual = Assert.Single(RoundTripSettings(new RandomProfile
        {
            Name = "All fields",
            Config = expected
        }).CustomProfiles).Config;

        Assert.Equal(expected.Seed, actual.Seed);
        Assert.Equal(expected.DynamicThreshold, actual.DynamicThreshold);
        Assert.Equal(expected.MinThresholdMs, actual.MinThresholdMs);
        Assert.Equal(expected.BaseThresholdMs, actual.BaseThresholdMs);
        Assert.Equal(expected.MaxThresholdMs, actual.MaxThresholdMs);
        Assert.Equal(expected.RecentUsageWindow, actual.RecentUsageWindow);
        Assert.Equal(expected.PatternHistoryLength, actual.PatternHistoryLength);
        Assert.Equal(expected.WeightedTopCandidates, actual.WeightedTopCandidates);
        Assert.Equal(expected.WeightedTemperature, actual.WeightedTemperature);
        Assert.Equal(expected.MaxCandidateSets, actual.MaxCandidateSets);
        Assert.Equal(expected.RenameDifficulty, actual.RenameDifficulty);
        Assert.Equal(expected.DifficultySuffix, actual.DifficultySuffix);
        Assert.Equal(expected.Weights.TimeSinceLastUseBonus, actual.Weights.TimeSinceLastUseBonus);
        Assert.Equal(expected.Weights.HandBalanceBonus, actual.Weights.HandBalanceBonus);
        Assert.Equal(expected.Weights.DistributionBonus, actual.Weights.DistributionBonus);
        Assert.Equal(expected.Weights.JackPenalty, actual.Weights.JackPenalty);
        Assert.Equal(expected.Weights.TrillPenalty, actual.Weights.TrillPenalty);
        Assert.Equal(expected.Weights.RepeatedPatternPenalty, actual.Weights.RepeatedPatternPenalty);
        Assert.Equal(expected.Weights.SameHandPenalty, actual.Weights.SameHandPenalty);
        Assert.Equal(expected.Weights.ExtremeJumpPenalty, actual.Weights.ExtremeJumpPenalty);
        Assert.Equal(expected.Weights.RecentUsagePenalty, actual.Weights.RecentUsagePenalty);
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

    [Theory]
    [InlineData(" S-Random")]
    [InlineData(" H-Random")]
    [InlineData(" Custom 日本語!")]
    public void RepeatedGenerationUsesUniqueVersionAndFilenameOutsideSongs(string suffix)
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlus Repeated 'ü!", Guid.NewGuid().ToString("N"));
        string songDirectory = Path.Combine(root, "Songs", "123 Artist's - Song!");
        string outputDirectory = Path.Combine(root, "Generated 日本語");
        Directory.CreateDirectory(songDirectory);
        try
        {
            string input = Path.Combine(songDirectory, "Artist - Song [Life].osu");
            byte[] original = TestBeatmaps.Mania(4,
                Enumerable.Range(0, 12).Select(i => TestBeatmaps.Note(4, i % 4, 1000 + i * 100)), "Life");
            File.WriteAllBytes(input, original);
            var service = new BeatmapGenerationService();
            var config = new HRandomConfig { Seed = 123, DifficultySuffix = suffix };

            GenerationResult first = service.Generate(input, config, null, outputDirectory);
            GenerationResult second = service.Generate(input, config, null, outputDirectory);
            GenerationResult third = service.Generate(input, config, null, outputDirectory);

            Assert.Equal("Life" + suffix, first.OutputVersion);
            Assert.Equal("Life" + suffix + " 2", second.OutputVersion);
            Assert.Equal("Life" + suffix + " 3", third.OutputVersion);
            Assert.True(first.OutputPath != second.OutputPath && second.OutputPath != third.OutputPath);
            Assert.True(File.Exists(first.OutputPath) && File.Exists(second.OutputPath) && File.Exists(third.OutputPath));
            Assert.Equal(original, File.ReadAllBytes(input));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ExistingFilenameCollisionIsNeverOverwritten()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusFilenameCollision", Guid.NewGuid().ToString("N"));
        string songDirectory = Path.Combine(root, "Songs", "Map");
        string outputDirectory = Path.Combine(root, "Generated");
        Directory.CreateDirectory(songDirectory);
        Directory.CreateDirectory(outputDirectory);
        try
        {
            string input = Path.Combine(songDirectory, "map.osu");
            File.WriteAllBytes(input, TestBeatmaps.Mania(4,
                Enumerable.Range(0, 12).Select(i => TestBeatmaps.Note(4, i % 4, 1000 + i * 100)), "Life"));
            string collision = Path.Combine(outputDirectory, "map S-Random.osu");
            File.WriteAllText(collision, "do not overwrite");

            GenerationResult result = new BeatmapGenerationService().Generate(input,
                new HRandomConfig { Seed = 123, DifficultySuffix = " S-Random" }, null, outputDirectory);

            Assert.True(result.OutputPath != collision);
            Assert.Equal("do not overwrite", File.ReadAllText(collision));
            Assert.Equal("Life S-Random", result.OutputVersion);
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

    private static AppSettings RoundTripSettings(RandomProfile profile)
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusProfile", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(root);
            store.Save(new AppSettings { CustomProfiles = new List<RandomProfile> { profile } });
            return store.Load();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
