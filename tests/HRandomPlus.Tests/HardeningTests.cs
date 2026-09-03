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
        StableProcessIdentity candidate = Candidate(10, "stable-a");
        StableProcessSelection result = StableProcessSelector.Select(new[] { candidate }, null, null);
        Assert.Equal(StableProcessSelectionStatus.Selected, result.Status);
        Assert.Equal(candidate, result.Identity);
        Assert.Equal(candidate.ExecutableDirectory, result.Identity!.ExecutableDirectory);
    }

    [Fact]
    public void StableSelectorCanSelectAnExactIdentityOnlyWhenReaderBindingIsAvailable()
    {
        StableProcessIdentity a = Candidate(10, "stable-a");
        StableProcessIdentity b = Candidate(20, "stable-b");
        foreach (StableProcessIdentity[] order in new[] { new[] { a, b }, new[] { b, a } })
        {
            StableProcessSelection selected = StableProcessSelector.Select(order, b.ExecutableDirectory, null,
                readerCanBindToIdentity: true, readerTargetProcessCount: 2);
            Assert.Equal(StableProcessSelectionStatus.Selected, selected.Status);
            Assert.Equal(b, selected.Identity);
            Assert.Equal(b.ExecutableDirectory, selected.Identity!.ExecutableDirectory);
        }
    }

    [Fact]
    public void StableSelectorFailsClosedWhenNameBasedReaderSeesMultipleProcesses()
    {
        StableProcessIdentity a = Candidate(10, "stable-a");
        StableProcessIdentity b = Candidate(20, "stable-b");
        StableProcessSelection result = StableProcessSelector.Select(new[] { a, b }, b.ExecutableDirectory, null,
            readerCanBindToIdentity: false, readerTargetProcessCount: 2);
        Assert.Equal(StableProcessSelectionStatus.Ambiguous, result.Status);
        Assert.True(result.Identity is null);
        Assert.Contains("select a .osu file manually", result.Message);
    }

    [Fact]
    public void StableSelectorFailsClosedWhenOnlyOneStableCandidateButReaderSeesAnotherEligibleX86Process()
    {
        StableProcessSelection result = StableProcessSelector.Select(new[] { Candidate(10, "stable-a") }, null, null,
            readerCanBindToIdentity: false, readerTargetProcessCount: 2);
        Assert.Equal(StableProcessSelectionStatus.Ambiguous, result.Status);
        Assert.True(result.Identity is null);
    }

    [Fact]
    public void StableSelectorAllowsOneEligibleX86ReaderTarget()
    {
        StableProcessIdentity stable = Candidate(10, "stable-a");
        StableProcessSelection result = StableProcessSelector.Select(new[] { stable }, null, null,
            readerCanBindToIdentity: false, readerTargetProcessCount: 1);
        Assert.Equal(StableProcessSelectionStatus.Selected, result.Status);
        Assert.Equal(stable, result.Identity);
    }

    [Fact]
    public void StableSelectorRecoversAfterASecondReaderTargetCloses()
    {
        StableProcessIdentity stable = Candidate(10, "stable-a");
        StableProcessIdentity second = Candidate(20, "stable-b");

        StableProcessSelection initial = StableProcessSelector.Select(new[] { stable }, null, null,
            readerCanBindToIdentity: false, readerTargetProcessCount: 1);
        StableProcessSelection ambiguous = StableProcessSelector.Select(new[] { stable, second }, null,
            initial.Identity?.ProcessId, initial.Identity?.StartTime,
            readerCanBindToIdentity: false, readerTargetProcessCount: 2);
        StableProcessSelection recovered = StableProcessSelector.Select(new[] { stable }, null, null,
            readerCanBindToIdentity: false, readerTargetProcessCount: 1);

        Assert.Equal(StableProcessSelectionStatus.Selected, initial.Status);
        Assert.Equal(StableProcessSelectionStatus.Ambiguous, ambiguous.Status);
        Assert.True(ambiguous.Identity is null);
        Assert.Equal(StableProcessSelectionStatus.Selected, recovered.Status);
        Assert.Equal(stable, recovered.Identity);
    }

    [Fact]
    public void StableSelectorReturnsNoneWithoutCandidates()
        => Assert.Equal(StableProcessSelectionStatus.None,
            StableProcessSelector.Select(Array.Empty<StableProcessIdentity>(), null, null).Status);

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
            new[] { Candidate(10, "stable-a"), Candidate(20, "stable-b") }, null, 10,
            readerCanBindToIdentity: true, readerTargetProcessCount: 2);
        Assert.Equal(10, result.Identity!.ProcessId);
    }

    [Fact]
    public void StableSelectorReportsAmbiguityWhenCurrentProcessDisappears()
    {
        StableProcessSelection result = StableProcessSelector.Select(
            new[] { Candidate(20, "stable-a"), Candidate(30, "stable-b") }, null, 10,
            readerCanBindToIdentity: true, readerTargetProcessCount: 2);
        Assert.Equal(StableProcessSelectionStatus.Ambiguous, result.Status);
        Assert.True(result.Identity is null);
    }

    [Fact]
    public void StableSelectorDoesNotRetainAReusedProcessId()
    {
        StableProcessIdentity previous = Candidate(10, "stable-a");
        StableProcessIdentity reused = previous with { StartTime = previous.StartTime.AddMinutes(1) };
        StableProcessSelection result = StableProcessSelector.Select(
            new[] { reused }, null, previous.ProcessId, previous.StartTime,
            readerCanBindToIdentity: false, readerTargetProcessCount: 1);

        Assert.Equal(StableProcessSelectionStatus.Selected, result.Status);
        Assert.Equal(reused, result.Identity);
    }

    [Fact]
    public void StableSelectorIgnoresAnInvalidConfiguredPath()
    {
        StableProcessIdentity candidate = Candidate(10, "stable-a");
        StableProcessSelection result = StableProcessSelector.Select(new[] { candidate }, "invalid\0path", null);

        Assert.Equal(StableProcessSelectionStatus.Selected, result.Status);
        Assert.Equal(candidate, result.Identity);
    }

    [Fact]
    public void StableSelectorDoesNotInventPreferenceForMultipleProcessesInConfiguredFolder()
    {
        StableProcessIdentity a = Candidate(10, "stable-a");
        StableProcessIdentity b = Candidate(20, "stable-a");
        StableProcessSelection result = StableProcessSelector.Select(new[] { a, b }, a.ExecutableDirectory, null,
            readerCanBindToIdentity: true, readerTargetProcessCount: 2);
        Assert.Equal(StableProcessSelectionStatus.Ambiguous, result.Status);
    }

    [Fact]
    public void ConfiguredOldInstallationCannotReplaceTheOnlyRunningIdentityRoot()
    {
        StableProcessIdentity running = Candidate(20, "stable-current");
        StableProcessSelection result = StableProcessSelector.Select(new[] { running },
            Candidate(10, "stable-old").ExecutableDirectory, null,
            readerCanBindToIdentity: false, readerTargetProcessCount: 1);
        Assert.Equal(running, result.Identity);
        Assert.Equal(running.ExecutableDirectory, result.Identity!.ExecutableDirectory);
        Assert.Equal(Path.Combine(running.ExecutableDirectory, "Songs"), result.Identity.SongsRoot);
    }

    [Fact]
    public void StableReaderSessionRecreatesOnPidReuseAndInstanceSwitch()
    {
        StableProcessIdentity a = Candidate(10, "stable-a");
        StableProcessIdentity reused = a with { StartTime = a.StartTime.AddMinutes(1) };
        StableProcessIdentity b = Candidate(20, "stable-b");
        using var session = new StableReaderSession<FakeStableReader>();
        FakeStableReader first = session.GetOrCreate(a, () => new FakeStableReader("A"));
        Assert.Equal(first, session.GetOrCreate(a, () => new FakeStableReader("unexpected")));

        FakeStableReader second = session.GetOrCreate(reused, () => new FakeStableReader("reused"));
        Assert.True(first.Disposed);
        Assert.Equal(reused, session.Identity);
        FakeStableReader third = session.GetOrCreate(b, () => new FakeStableReader("B"));
        Assert.True(second.Disposed);
        Assert.Equal(b, session.Identity);
        Assert.Equal("B", third.Name);
    }

    [Fact]
    public void StableReaderSessionInvalidatesWhenSelectedProcessTerminates()
    {
        using var session = new StableReaderSession<FakeStableReader>();
        FakeStableReader reader = session.GetOrCreate(Candidate(10, "stable-a"), () => new FakeStableReader("A"));
        session.Invalidate();
        Assert.True(reader.Disposed);
        Assert.True(session.Reader is null && session.Identity is null);
    }

    private static StableProcessIdentity Candidate(int id, string directory)
        => new(id, Path.Combine(Path.GetTempPath(), directory), DateTimeOffset.UnixEpoch.AddSeconds(id));

    private sealed class FakeStableReader(string name) : IDisposable
    {
        public string Name { get; } = name;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    [Theory]
    [InlineData("C:\\Users\\Alice", "C:\\Users\\Alice", "%USERPROFILE%")]
    [InlineData("C:\\Users\\Alice\\AppData\\Local", "C:\\Users\\Alice", "%USERPROFILE%\\AppData\\Local")]
    [InlineData("c:\\users\\ALICE\\Songs", "C:\\Users\\Alice", "%USERPROFILE%\\Songs")]
    [InlineData("D:\\Games\\osu", "C:\\Users\\Alice", "D:\\Games\\osu")]
    [InlineData("/home/alice", "/home/alice", "$HOME")]
    [InlineData("/home/alice/.local/share", "/home/alice", "$HOME/.local/share")]
    [InlineData("/home/Alice/data", "/home/alice", "/home/Alice/data")]
    [InlineData("/opt/osu", "/home/alice", "/opt/osu")]
    public void DiagnosticPathsRedactOnlyTheHomePrefix(string value, string home, string expected)
        => Assert.Equal(expected, DiagnosticPathRedactor.Redact(value, home));

    [Fact]
    public void DiagnosticPathRedactionPreservesNullAndEmpty()
    {
        Assert.True(DiagnosticPathRedactor.Redact(null, "/home/user") is null);
        Assert.Equal(string.Empty, DiagnosticPathRedactor.Redact(string.Empty, "/home/user"));
        Assert.Equal("plain text", DiagnosticPathRedactor.Redact("plain text", "/home/user"));
    }

    [Theory]
    [InlineData("Missing config: C:\\Users\\Alice\\.local", "C:\\Users\\Alice", "Missing config: %USERPROFILE%\\.local")]
    [InlineData("Missing config: /home/alice/.local", "/home/alice", "Missing config: $HOME/.local")]
    [InlineData("Prefix/home/alice/.local", "/home/alice", "Prefix/home/alice/.local")]
    [InlineData("C:\\Users\\Alicia\\data", "C:\\Users\\Alice", "C:\\Users\\Alicia\\data")]
    public void DiagnosticPathsRedactEmbeddedHomeOnlyAtAPathBoundary(string value, string home, string expected)
        => Assert.Equal(expected, DiagnosticPathRedactor.Redact(value, home));

    [Theory]
    [InlineData("C:\\Users\\Alice, C:\\Users\\Alice\\Songs)", "C:\\Users\\Alice", "%USERPROFILE%, %USERPROFILE%\\Songs)")]
    [InlineData("/home/alice, then /home/alice/Songs)", "/home/alice", "$HOME, then $HOME/Songs)")]
    [InlineData("C:\\Users\\Alicia and C:\\Users\\Alice_extra", "C:\\Users\\Alice", "C:\\Users\\Alicia and C:\\Users\\Alice_extra")]
    [InlineData("/home/alicia and /home/alice-extra", "/home/alice", "/home/alicia and /home/alice-extra")]
    public void DiagnosticPathsRedactEveryOccurrenceWithPunctuationAndRejectFalsePrefixes(
        string value, string home, string expected)
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
