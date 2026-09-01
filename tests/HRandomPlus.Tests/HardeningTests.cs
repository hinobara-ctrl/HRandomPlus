using System.Text;
using System.Text.Json;
using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Randomization;
using Xunit;

namespace HRandomPlus.Tests;

public sealed class HardeningTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(HRandomConfig.DefaultMaxCandidateSets)]
    [InlineData(HRandomConfig.MaximumCandidateSets)]
    public void CandidateSetLimitsAcceptSupportedValues(int maximum)
    {
        new HRandomConfig { MaxCandidateSets = maximum, WeightedTopCandidates = 1 }.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(HRandomConfig.MaximumCandidateSets + 1)]
    [InlineData(int.MaxValue)]
    public void CandidateSetLimitsRejectUnsupportedValues(int maximum)
        => AssertFails<ArgumentException>(() =>
            new HRandomConfig { MaxCandidateSets = maximum, WeightedTopCandidates = 1 }.Validate());

    [Fact]
    public void WeightedCandidatesCannotExceedCandidateSetLimit()
        => AssertFails<ArgumentException>(() =>
            new HRandomConfig { MaxCandidateSets = 10, WeightedTopCandidates = 11 }.Validate());

    [Fact]
    public void PersistedCandidateLimitsAreMigratedConservatively()
    {
        var settings = new AppSettings
        {
            CustomConfig = new HRandomConfig { MaxCandidateSets = int.MaxValue, WeightedTopCandidates = int.MaxValue },
            CustomProfiles = new List<RandomProfile>
            {
                new() { Id = Guid.NewGuid(), Name = "Legacy", Config = new HRandomConfig { MaxCandidateSets = -5, WeightedTopCandidates = -2 } }
            }
        };

        Assert.True(ProfileSettingsMigration.Apply(settings));
        Assert.Equal(HRandomConfig.MaximumCandidateSets, settings.CustomConfig!.MaxCandidateSets);
        Assert.Equal(HRandomConfig.MaximumCandidateSets, settings.CustomConfig.WeightedTopCandidates);
        Assert.Equal(1, settings.CustomProfiles[0].Config.MaxCandidateSets);
        Assert.Equal(1, settings.CustomProfiles[0].Config.WeightedTopCandidates);
    }

    [Fact]
    public void LegacyStandaloneConfigLoadsWithClampedCandidateLimits()
    {
        string path = Path.Combine(Path.GetTempPath(), $"HRandomPlus-config-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"maxCandidateSets\":2147483647,\"weightedTopCandidates\":2147483647}");
            HRandomConfig config = HRandomConfig.Load(path);
            Assert.Equal(HRandomConfig.MaximumCandidateSets, config.MaxCandidateSets);
            Assert.Equal(HRandomConfig.MaximumCandidateSets, config.WeightedTopCandidates);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void CombinationCountsAreExactForEverySupportedKeymodeAndNeverOverflow()
    {
        long maximum = 0;
        for (int keys = 1; keys <= 18; keys++)
        for (int notes = 0; notes <= keys; notes++)
        {
            long count = CombinationMath.CountBounded(keys, notes);
            Assert.True(count > 0);
            maximum = Math.Max(maximum, count);
        }
        Assert.Equal(48620L, maximum);
        Assert.Equal(100L, CombinationMath.CountBounded(1000, 500, 100));
        Assert.Equal(long.MaxValue, CombinationMath.CountBounded(int.MaxValue, int.MaxValue / 2));
    }

    [Theory]
    [InlineData("Custom")]
    [InlineData("Tech 7K")]
    [InlineData("Prueba áéí")]
    [InlineData("テスト")]
    [InlineData("테스트")]
    [InlineData("A_B-C (v2)")]
    public void PortableDifficultySuffixAcceptsUnicodeAndCommonText(string suffix)
        => new HRandomConfig { DifficultySuffix = suffix }.Validate();

    [Theory]
    [InlineData(":")]
    [InlineData("?")]
    [InlineData("*")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("|")]
    [InlineData("/")]
    [InlineData("\\")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    public void PortableDifficultySuffixRejectsCrossPlatformHazards(string suffix)
        => AssertFails<ArgumentException>(() => new HRandomConfig { DifficultySuffix = suffix }.Validate());

    [Fact]
    public void PortableDifficultySuffixRejectsControlCharacters()
        => AssertFails<ArgumentException>(() => new HRandomConfig { DifficultySuffix = "bad\u0001name" }.Validate());

    [Fact]
    public void ImportedProfileRejectsNonPortableDifficultySuffix()
    {
        var document = new ProfileTransferDocument
        {
            Format = ProfileTransfer.Format,
            FormatVersion = ProfileTransfer.FormatVersion,
            EngineVersion = ProfileTransfer.EngineVersion,
            ProfileId = Guid.NewGuid(),
            Name = "Portable name",
            Config = new HRandomConfig { DifficultySuffix = "bad:name" }
        };
        byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        InvalidDataException error = AssertFails<InvalidDataException>(() => ProfileTransfer.Deserialize(json));
        Assert.Contains("DifficultySuffix", error.Message);
    }

    [Fact]
    public void StableSelectorUsesTheOnlyCandidate()
    {
        StableProcessCandidate candidate = Candidate(10, "stable-a");
        StableProcessSelection result = StableProcessSelector.Select(new[] { candidate }, null, null);
        Assert.Equal(StableProcessSelectionStatus.Selected, result.Status);
        Assert.Equal(10, result.Candidate!.ProcessId);
    }

    [Fact]
    public void StableSelectorPrefersConfiguredInstallationIndependentlyOfOrder()
    {
        StableProcessCandidate a = Candidate(10, "stable-a");
        StableProcessCandidate b = Candidate(20, "stable-b");
        foreach (StableProcessCandidate[] order in new[] { new[] { a, b }, new[] { b, a } })
        {
            Assert.Equal(10, StableProcessSelector.Select(order, a.ExecutableDirectory, null).Candidate!.ProcessId);
            Assert.Equal(20, StableProcessSelector.Select(order, b.ExecutableDirectory, null).Candidate!.ProcessId);
        }
    }

    [Fact]
    public void StableSelectorReturnsNoneWithoutCandidates()
        => Assert.Equal(StableProcessSelectionStatus.None,
            StableProcessSelector.Select(Array.Empty<StableProcessCandidate>(), null, null).Status);

    [Fact]
    public void StableSelectorReportsAmbiguityWhenConfiguredPathMatchesNone()
    {
        StableProcessSelection result = StableProcessSelector.Select(
            new[] { Candidate(10, "stable-a"), Candidate(20, "stable-b") },
            Path.Combine(Path.GetTempPath(), "stable-missing"), null);
        Assert.Equal(StableProcessSelectionStatus.Ambiguous, result.Status);
    }

    [Fact]
    public void StableSelectorRetainsCurrentProcessWhileItRemainsValid()
    {
        StableProcessSelection result = StableProcessSelector.Select(
            new[] { Candidate(10, "stable-a"), Candidate(20, "stable-b") }, null, 10);
        Assert.Equal(10, result.Candidate!.ProcessId);
    }

    [Fact]
    public void StableSelectorReportsAmbiguityWhenCurrentProcessDisappears()
    {
        StableProcessSelection result = StableProcessSelector.Select(
            new[] { Candidate(20, "stable-a"), Candidate(30, "stable-b") }, null, 10);
        Assert.Equal(StableProcessSelectionStatus.Ambiguous, result.Status);
        Assert.True(result.Candidate is null);
    }

    [Fact]
    public void StableSelectorDoesNotRetainAReusedProcessId()
    {
        StableProcessCandidate previous = Candidate(10, "stable-a");
        StableProcessCandidate reused = previous with { StartTime = previous.StartTime.AddMinutes(1) };
        StableProcessSelection result = StableProcessSelector.Select(
            new[] { reused, Candidate(20, "stable-b") }, null, previous.ProcessId, previous.StartTime);

        Assert.Equal(StableProcessSelectionStatus.Ambiguous, result.Status);
        Assert.True(result.Candidate is null);
    }

    [Fact]
    public void StableSelectorIgnoresAnInvalidConfiguredPath()
    {
        StableProcessCandidate candidate = Candidate(10, "stable-a");
        StableProcessSelection result = StableProcessSelector.Select(new[] { candidate }, "invalid\0path", null);

        Assert.Equal(StableProcessSelectionStatus.Selected, result.Status);
        Assert.Equal(candidate, result.Candidate);
    }

    [Fact]
    public void StableSelectorDoesNotInventPreferenceForMultipleProcessesInConfiguredFolder()
    {
        StableProcessCandidate a = Candidate(10, "stable-a");
        StableProcessCandidate b = Candidate(20, "stable-a");
        StableProcessSelection result = StableProcessSelector.Select(new[] { a, b }, a.ExecutableDirectory, null);
        Assert.Equal(StableProcessSelectionStatus.Ambiguous, result.Status);
    }

    private static StableProcessCandidate Candidate(int id, string directory)
        => new(id, Path.Combine(Path.GetTempPath(), directory), DateTimeOffset.UnixEpoch.AddSeconds(id));

    [Theory]
    [InlineData("C:\\Users\\Benja", "C:\\Users\\Benja", "%USERPROFILE%")]
    [InlineData("C:\\Users\\Benja\\AppData\\Local", "C:\\Users\\Benja", "%USERPROFILE%\\AppData\\Local")]
    [InlineData("c:\\users\\BENJA\\Songs", "C:\\Users\\Benja", "%USERPROFILE%\\Songs")]
    [InlineData("D:\\Games\\osu", "C:\\Users\\Benja", "D:\\Games\\osu")]
    [InlineData("/home/benja", "/home/benja", "$HOME")]
    [InlineData("/home/benja/.local/share", "/home/benja", "$HOME/.local/share")]
    [InlineData("/home/Benja/data", "/home/benja", "/home/Benja/data")]
    [InlineData("/opt/osu", "/home/benja", "/opt/osu")]
    public void DiagnosticPathsRedactOnlyTheHomePrefix(string value, string home, string expected)
        => Assert.Equal(expected, DiagnosticPathRedactor.Redact(value, home));

    [Fact]
    public void DiagnosticPathRedactionPreservesNullAndEmpty()
    {
        Assert.True(DiagnosticPathRedactor.Redact(null, "/home/user") is null);
        Assert.Equal(string.Empty, DiagnosticPathRedactor.Redact(string.Empty, "/home/user"));
    }

    [Theory]
    [InlineData("Missing config: C:\\Users\\Benja\\.local", "C:\\Users\\Benja", "Missing config: %USERPROFILE%\\.local")]
    [InlineData("Missing config: /home/benja/.local", "/home/benja", "Missing config: $HOME/.local")]
    [InlineData("Prefix/home/benja/.local", "/home/benja", "Prefix/home/benja/.local")]
    [InlineData("C:\\Users\\Benjamín\\data", "C:\\Users\\Benja", "C:\\Users\\Benjamín\\data")]
    public void DiagnosticPathsRedactEmbeddedHomeOnlyAtAPathBoundary(string value, string home, string expected)
        => Assert.Equal(expected, DiagnosticPathRedactor.Redact(value, home));

    [Fact]
    public void ExtractedConfigurationParserBuildsAndValidatesTheUiModel()
    {
        var defaults = new HRandomConfig();
        var values = new Dictionary<string, string>
        {
            ["MinThresholdMs"] = "40", ["BaseThresholdMs"] = "100", ["MaxThresholdMs"] = "160",
            ["RecentUsageWindow"] = "24", ["PatternHistoryLength"] = "16",
            ["WeightedTopCandidates"] = "12", ["WeightedTemperature"] = "12.5",
            ["MaxCandidateSets"] = "4096", ["DifficultySuffix"] = " テスト",
            ["TimeSinceLastUseBonus"] = "18", ["HandBalanceBonus"] = "6",
            ["DistributionBonus"] = "5", ["JackPenalty"] = "80", ["TrillPenalty"] = "25",
            ["RepeatedPatternPenalty"] = "14", ["SameHandPenalty"] = "5",
            ["ExtremeJumpPenalty"] = "6", ["RecentUsagePenalty"] = "8"
        };
        HRandomConfig parsed = HRandomConfigInputParser.Parse(new("123", true, false, true, values));
        Assert.Equal(123L, parsed.Seed!.Value);
        Assert.Equal(12.5, parsed.WeightedTemperature);
        Assert.Equal(" テスト", parsed.DifficultySuffix);
        Assert.Equal(defaults.MaxCandidateSets, parsed.MaxCandidateSets);
        Assert.Equal(false, parsed.PreserveDualStages);
    }

    [Fact]
    public void ExtremeThresholdValuesDoNotOverflowScoringArithmetic()
    {
        var config = new HRandomConfig
        {
            MinThresholdMs = int.MaxValue - 2,
            BaseThresholdMs = int.MaxValue - 1,
            MaxThresholdMs = int.MaxValue
        };
        config.Validate();
        var state = new RandomState(4, config.RecentUsageWindow, config.PatternHistoryLength);
        state.RecordGroup(0, new[] { 0 });
        double score = new CandidateScorer(config).ScoreSet(state, new[] { 3 }, int.MaxValue, int.MaxValue);
        Assert.True(double.IsFinite(score));
    }

    private static T AssertFails<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T error) { return error; }
        throw new Xunit.TestException($"Se esperaba {typeof(T).Name}.");
    }
}
