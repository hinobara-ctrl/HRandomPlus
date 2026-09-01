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
            const string corrupt = "{broken";
            File.WriteAllText(Path.Combine(root, "config.json"), corrupt);
            AppSettings settings = new SettingsStore(root).Load();
            Assert.Equal("H-Random", settings.LastProfile);
            string backup = Assert.Single(Directory.GetFiles(root, "config.corrupt-*.json"));
            Assert.Equal(corrupt, File.ReadAllText(backup));
            _ = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "config.json")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void TransientSettingsReadFailureDoesNotReplaceOriginal()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusSettings", Guid.NewGuid().ToString("N"));
        string settingsPath = Path.Combine(root, "config.json");
        Directory.CreateDirectory(settingsPath);
        try
        {
            AppSettings settings = new SettingsStore(root).Load();
            Assert.Equal("H-Random", settings.LastProfile);
            Assert.True(Directory.Exists(settingsPath));
            Assert.True(Directory.GetFiles(root, "config.corrupt-*.json").Length == 0);
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
            PreserveDualStages = true,
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
        Assert.Equal(expected.PreserveDualStages, actual.PreserveDualStages);
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

    [Fact]
    public void PersistentCustomRoundTripPreservesEveryField()
    {
        HRandomConfig expected = CompleteConfig();
        string root = TemporaryDirectory("PersistentCustom");
        try
        {
            var store = new SettingsStore(root);
            store.Save(new AppSettings { CustomConfig = expected });
            AppSettings loaded = store.Load();
            AssertConfigEqual(expected, loaded.CustomConfig!);
            Assert.True(loaded.CustomProfileId != Guid.Empty);
            RandomProfile custom = ProfileCatalog.CreateBuiltIns(loaded.CustomConfig, loaded.CustomProfileId)
                .Single(profile => profile.Name == ProfileCatalog.CustomName);
            AssertConfigEqual(expected, custom.Config);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void BuiltInProfilesRemainImmutableAcrossCatalogInstances()
    {
        RandomProfile firstH = ProfileCatalog.BuiltIns.Single(profile => profile.Name == ProfileCatalog.HRandomName);
        RandomProfile firstS = ProfileCatalog.BuiltIns.Single(profile => profile.Name == ProfileCatalog.SRandomName);
        firstH.Config.Weights.JackPenalty = -999;
        firstS.Config.WeightedTopCandidates = 1;

        RandomProfile nextH = ProfileCatalog.BuiltIns.Single(profile => profile.Name == ProfileCatalog.HRandomName);
        RandomProfile nextS = ProfileCatalog.BuiltIns.Single(profile => profile.Name == ProfileCatalog.SRandomName);
        Assert.Equal(80d, nextH.Config.Weights.JackPenalty);
        Assert.Equal(4096, nextS.Config.WeightedTopCandidates);
        Assert.Equal(" H-RANDOM+", nextH.Config.DifficultySuffix);
        Assert.Equal(" S-RANDOM", nextS.Config.DifficultySuffix);
    }

    [Fact]
    public void SaveCustomRepeatedlyUpdatesOnePersistentSlotAndResetRestoresDefaults()
    {
        var settings = new AppSettings { CustomConfig = ProfileCatalog.DefaultCustom(), CustomProfileId = Guid.NewGuid() };
        RandomProfile custom = ProfileCatalog.CreateBuiltIns(settings.CustomConfig, settings.CustomProfileId)
            .Single(profile => profile.Name == ProfileCatalog.CustomName);
        ProfileOperations.Save(custom, settings, new HRandomConfig { Seed = 10, DifficultySuffix = " ONE" });
        ProfileOperations.Save(custom, settings, new HRandomConfig { Seed = 20, DifficultySuffix = " TWO" });
        Assert.Equal(20L, settings.CustomConfig!.Seed);
        Assert.Equal(" TWO", custom.Config.DifficultySuffix);
        Assert.Equal(0, settings.CustomProfiles.Count);

        ProfileOperations.ResetCustom(custom, settings);
        Assert.Equal<long?>(null, settings.CustomConfig!.Seed);
        Assert.Equal(" CUSTOM", settings.CustomConfig.DifficultySuffix);
        Assert.Equal(" CUSTOM", custom.Config.DifficultySuffix);
    }

    [Fact]
    public void ProtectedPresetsCannotBeSavedOrReset()
    {
        var settings = new AppSettings();
        RandomProfile h = ProfileCatalog.BuiltIns.Single(profile => profile.Name == ProfileCatalog.HRandomName);
        RandomProfile s = ProfileCatalog.BuiltIns.Single(profile => profile.Name == ProfileCatalog.SRandomName);
        AssertFails<InvalidOperationException>(() => ProfileOperations.Save(h, settings, new HRandomConfig { DifficultySuffix = " CHANGED" }));
        AssertFails<InvalidOperationException>(() => ProfileOperations.Save(s, settings, new HRandomConfig { DifficultySuffix = " CHANGED" }));
        AssertFails<InvalidOperationException>(() => ProfileOperations.ResetCustom(h, settings));
        Assert.Equal(" H-RANDOM+", h.Config.DifficultySuffix);
        Assert.Equal(" S-RANDOM", s.Config.DifficultySuffix);
    }

    [Fact]
    public void LegacyCustomMigrationUsesLastCustomAndPreservesEarlierEntries()
    {
        var settings = new AppSettings
        {
            CustomProfiles = new List<RandomProfile>
            {
                new() { Name = " Custom ", Config = new HRandomConfig { Seed = 1, DifficultySuffix = " FIRST" } },
                new() { Name = "Legacy", Config = new HRandomConfig { Seed = 3, DifficultySuffix = " LEGACY" } },
                new() { Name = "custom", Config = new HRandomConfig { Seed = 2, DifficultySuffix = " LAST" } }
            }
        };

        Assert.True(ProfileSettingsMigration.Apply(settings));
        Assert.Equal(2L, settings.CustomConfig!.Seed);
        Assert.Equal(2, settings.CustomProfiles.Count);
        Assert.True(settings.CustomProfiles.All(profile => profile.Id != Guid.Empty));
        Assert.True(settings.CustomProfiles.All(profile => !ProfileCatalog.IsReservedName(profile.Name)));
        Assert.True(settings.CustomProfiles.Any(profile => profile.Config.Seed == 1));
        Assert.True(settings.CustomProfiles.Any(profile => profile.Config.Seed == 3));
        Assert.True(!ProfileSettingsMigration.Apply(settings));
    }

    [Fact]
    public void MigrationRenamesReservedProfilesDeterministicallyAndOnlyOnce()
    {
        var settings = new AppSettings
        {
            CustomConfig = ProfileCatalog.DefaultCustom(),
            CustomProfileId = Guid.NewGuid(),
            CustomProfiles = new List<RandomProfile>
            {
                new() { Name = "H-Random", Config = new HRandomConfig() },
                new() { Name = " h-random ", Config = new HRandomConfig() },
                new() { Name = "S-Random", Config = new HRandomConfig() }
            }
        };
        Assert.True(ProfileSettingsMigration.Apply(settings));
        string[] names = settings.CustomProfiles.Select(profile => profile.Name).ToArray();
        Assert.Equal(new[] { "H-Random (Imported)", "h-random (Imported) (2)", "S-Random (Imported)" }, names);
        Assert.True(!ProfileSettingsMigration.Apply(settings));
        Assert.Equal(names, settings.CustomProfiles.Select(profile => profile.Name));
    }

    [Fact]
    public void DuplicateCreatesIndependentGuidAndUniqueName()
    {
        RandomProfile source = ProfileCatalog.BuiltIns.Single(profile => profile.Name == ProfileCatalog.HRandomName);
        RandomProfile copy = ProfileOperations.Duplicate(source, source.Config, "Training", "  shared settings  ", new[] { "Training" });
        Assert.True(copy.Id != Guid.Empty && copy.Id != source.Id);
        Assert.Equal("Training (2)", copy.Name);
        Assert.Equal("shared settings", copy.Description);
        copy.Config.Weights.JackPenalty = 1;
        Assert.Equal(80d, source.Config.Weights.JackPenalty);
    }

    [Theory]
    [InlineData("H-Random")]
    [InlineData(" h-random ")]
    [InlineData("S-RANDOM")]
    [InlineData(" custom ")]
    public void ReservedPersonalNamesAreRejected(string name)
    {
        AssertFails<ArgumentException>(() => ProfileNames.ValidatePersonalName(name));
    }

    [Fact]
    public void ProfileExportImportRoundTripPreservesAllFieldsSeedAndUnicode()
    {
        var expected = new RandomProfile
        {
            Id = Guid.NewGuid(),
            Name = "Jacks moderados 日本語",
            Description = "Perfil compartido — 1/4",
            Config = CompleteConfig()
        };
        byte[] serialized = ProfileTransfer.Serialize(expected);
        RandomProfile actual = ProfileTransfer.Deserialize(serialized);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Description, actual.Description);
        AssertConfigEqual(expected.Config, actual.Config);
        Assert.True(serialized.Length < 3 || serialized[0] != 0xEF || serialized[1] != 0xBB || serialized[2] != 0xBF);
    }

    [Fact]
    public void ExportContainsOnlyProfileDataAndNoGlobalSettings()
    {
        var profile = new RandomProfile { Id = Guid.NewGuid(), Name = "Safe", Config = CompleteConfig() };
        string json = System.Text.Encoding.UTF8.GetString(ProfileTransfer.Serialize(profile));
        foreach (string forbidden in new[] { "osuPath", "linuxOsuPath", "lastManualDirectory", "tosuHost", "tosuPort", "outputToBeatmapFolder", "beatmapPath", "logs" })
            Assert.True(!json.Contains(forbidden, StringComparison.OrdinalIgnoreCase), forbidden);
        Assert.Contains("\"format\": \"HRandomPlus.Profile\"", json);
        Assert.Contains("\"profileId\"", json);
    }

    [Fact]
    public void InvalidAndOversizedProfileFilesAreRejected()
    {
        AssertFails<InvalidDataException>(() => ProfileTransfer.Deserialize(System.Text.Encoding.UTF8.GetBytes("{broken")));
        AssertFails<InvalidDataException>(() => ProfileTransfer.Deserialize(new byte[ProfileTransfer.MaximumFileBytes + 1]));
    }

    [Fact]
    public void MissingRequiredProfileFieldsAreRejected()
    {
        AssertFails<InvalidDataException>(() => ProfileTransfer.Deserialize(System.Text.Encoding.UTF8.GetBytes("{}")));
        string missingConfig = $$"""
        {
          "format": "{{ProfileTransfer.Format}}",
          "formatVersion": {{ProfileTransfer.FormatVersion}},
          "profileId": "{{Guid.NewGuid()}}",
          "name": "Missing config",
          "description": "",
          "engineVersion": {{ProfileTransfer.EngineVersion}}
        }
        """;
        AssertFails<InvalidDataException>(() => ProfileTransfer.Deserialize(System.Text.Encoding.UTF8.GetBytes(missingConfig)));
    }

    [Fact]
    public void FutureFormatAndIncompatibleEngineAreRejected()
    {
        var profile = new RandomProfile { Id = Guid.NewGuid(), Name = "Versioned", Config = CompleteConfig() };
        string json = System.Text.Encoding.UTF8.GetString(ProfileTransfer.Serialize(profile));
        AssertFails<InvalidDataException>(() => ProfileTransfer.Deserialize(System.Text.Encoding.UTF8.GetBytes(json.Replace("\"formatVersion\": 1", "\"formatVersion\": 99", StringComparison.Ordinal))));
        AssertFails<InvalidDataException>(() => ProfileTransfer.Deserialize(System.Text.Encoding.UTF8.GetBytes(json.Replace("\"engineVersion\": 1", "\"engineVersion\": 99", StringComparison.Ordinal))));
    }

    [Fact]
    public void InvalidConfigAndNonFiniteNumbersAreRejected()
    {
        var invalid = new RandomProfile { Id = Guid.NewGuid(), Name = "Invalid", Config = CompleteConfig() };
        invalid.Config.MinThresholdMs = 100;
        invalid.Config.BaseThresholdMs = 50;
        AssertFails<InvalidDataException>(() => ProfileTransfer.Serialize(invalid));

        invalid.Config = CompleteConfig();
        invalid.Config.Weights.JackPenalty = double.PositiveInfinity;
        AssertFails<InvalidDataException>(() => ProfileTransfer.Serialize(invalid));
    }

    [Fact]
    public void SameGuidCanUpdateOrImportAsIndependentCopy()
    {
        Guid id = Guid.NewGuid();
        var profiles = new List<RandomProfile>
        {
            new() { Id = id, Name = "Existing", Description = "old", Config = new HRandomConfig { Seed = 1 } }
        };
        var incoming = new RandomProfile { Id = id, Name = "Updated", Description = "new", Config = new HRandomConfig { Seed = 2 } };
        RandomProfile updated = ProfileTransfer.Import(profiles, incoming, ProfileImportDecision.Update)!;
        Assert.Equal(1, profiles.Count);
        Assert.Equal(id, updated.Id);
        Assert.Equal(2L, updated.Config.Seed);

        RandomProfile copy = ProfileTransfer.Import(profiles, incoming, ProfileImportDecision.ImportAsCopy)!;
        Assert.Equal(2, profiles.Count);
        Assert.True(copy.Id != id && copy.Id != Guid.Empty);
    }

    [Fact]
    public void ImportResolvesDuplicateAndReservedNamesWithoutReplacingPresets()
    {
        var profiles = new List<RandomProfile>
        {
            new() { Id = Guid.NewGuid(), Name = "Shared", Config = new HRandomConfig() }
        };
        RandomProfile duplicate = ProfileTransfer.Import(profiles,
            new RandomProfile { Id = Guid.NewGuid(), Name = "Shared", Config = new HRandomConfig() },
            ProfileImportDecision.Update)!;
        Assert.Equal("Shared (2)", duplicate.Name);

        RandomProfile reserved = ProfileTransfer.Import(profiles,
            new RandomProfile { Id = ProfileCatalog.HRandomId, Name = "H-Random", Config = new HRandomConfig() },
            ProfileImportDecision.Update)!;
        Assert.Equal("H-Random (Imported)", reserved.Name);
        Assert.True(profiles.All(profile => !profile.BuiltIn));
    }

    [Fact]
    public void CancelledImportLeavesProfilesAndGlobalSettingsUntouched()
    {
        var settings = new AppSettings
        {
            OsuPath = "private-osu-path",
            TosuHost = "10.0.0.5",
            TosuPort = 12345,
            OutputToBeatmapFolder = false,
            CustomProfiles = new List<RandomProfile>
            {
                new() { Id = Guid.NewGuid(), Name = "Existing", Config = new HRandomConfig() }
            }
        };
        int before = settings.CustomProfiles.Count;
        RandomProfile? result = ProfileTransfer.Import(settings.CustomProfiles,
            new RandomProfile { Id = Guid.NewGuid(), Name = "Incoming", Config = new HRandomConfig() },
            ProfileImportDecision.Cancel);
        Assert.True(result is null);
        Assert.Equal(before, settings.CustomProfiles.Count);
        Assert.Equal("private-osu-path", settings.OsuPath);
        Assert.Equal("10.0.0.5", settings.TosuHost);
        Assert.Equal(12345, settings.TosuPort);
        Assert.True(!settings.OutputToBeatmapFolder);
    }

    [Fact]
    public void SettingsWritesAtomicallyWithoutLeavingTemporaryFiles()
    {
        string root = TemporaryDirectory("AtomicSettings");
        try
        {
            var store = new SettingsStore(root);
            store.Save(new AppSettings { CustomConfig = CompleteConfig() });
            store.Save(new AppSettings { CustomConfig = new HRandomConfig { Seed = 77 } });
            Assert.Equal(77L, store.Load().CustomConfig!.Seed);
            Assert.Equal(0, Directory.GetFiles(root, "*.tmp").Length);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void PortableProfileFileRoundTripsThroughDisk()
    {
        string root = TemporaryDirectory("PortableProfile");
        try
        {
            string path = Path.Combine(root, "跨平台.hrp-profile.json");
            var expected = new RandomProfile { Id = Guid.NewGuid(), Name = "Windows ↔ Linux", Description = "UTF-8", Config = CompleteConfig() };
            ProfileTransfer.Export(path, expected);
            RandomProfile actual = ProfileTransfer.Read(path);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Name, actual.Name);
            AssertConfigEqual(expected.Config, actual.Config);
            Assert.Equal(0, Directory.GetFiles(root, "*.tmp").Length);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static HRandomConfig CompleteConfig() => new()
    {
        Seed = -123456789,
        DynamicThreshold = false,
        PreserveDualStages = true,
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

    private static void AssertConfigEqual(HRandomConfig expected, HRandomConfig actual)
    {
        Assert.Equal(expected.Seed, actual.Seed);
        Assert.Equal(expected.DynamicThreshold, actual.DynamicThreshold);
        Assert.Equal(expected.PreserveDualStages, actual.PreserveDualStages);
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

    private static void AssertFails<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        catch (Exception ex) { throw new Xunit.TestException($"Expected {typeof(TException).Name}, received {ex.GetType().Name}."); }
        throw new Xunit.TestException($"Expected {typeof(TException).Name}.");
    }

    private static string TemporaryDirectory(string name)
        => Path.Combine(Path.GetTempPath(), "HRandomPlusTests", name, Guid.NewGuid().ToString("N"));

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
