using HRandomPlus.Analysis;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Randomization;
using HRandomPlus.Validation;

namespace HRandomPlus.Tests;

public class RandomizerProductDecisionTests
{
    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(12, true)]
    [InlineData(13, true)]
    [InlineData(14, true)]
    [InlineData(15, true)]
    [InlineData(16, true)]
    [InlineData(17, true)]
    [InlineData(18, true)]
    public void DualStageEligibilityStartsAt10K(int keys, bool expected)
        => Assert.Equal(expected, DualStageLayout.IsEligible(keys));

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    public void EnabledDualStageKeepsNotesInsideTheirOriginalStage(int keys)
    {
        var lines = new List<string>();
        int rightStart = (keys + 1) / 2;
        for (int index = 0; index < 32; index++)
        {
            int time = 1000 + index * 90;
            lines.Add(TestBeatmaps.Note(keys, index % (keys / 2), time));
            lines.Add(TestBeatmaps.Note(keys, rightStart + index % (keys - rightStart), time));
            if (keys % 2 != 0) lines.Add(TestBeatmaps.Note(keys, keys / 2, time));
        }
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse("dual.osu", TestBeatmaps.Mania(keys, lines));

        new HRandomPlusEngine(new HRandomConfig { PreserveDualStages = true }).Randomize(document.HitObjects, keys, 12345);

        Assert.All(document.HitObjects, note => Assert.True(DoesNotCrossOppositeStage(note, keys)));
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(11, true)]
    [InlineData(12, false)]
    [InlineData(12, true)]
    [InlineData(13, false)]
    [InlineData(13, true)]
    [InlineData(14, false)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    [InlineData(16, true)]
    [InlineData(17, false)]
    [InlineData(17, true)]
    [InlineData(18, false)]
    [InlineData(18, true)]
    public void DualStageMatrixRemainsPlayableAndDeterministic(int keys, bool preserveStages)
    {
        byte[] source = TestBeatmaps.Mania(keys, BuildMatrixLines(keys));
        OsuBeatmapDocument first = OsuBeatmapDocument.Parse("matrix-a.osu", source);
        OsuBeatmapDocument second = OsuBeatmapDocument.Parse("matrix-b.osu", source);
        int[] originalTimes = first.HitObjects.Select(note => note.StartTime).ToArray();

        var config = new HRandomConfig { PreserveDualStages = preserveStages };
        new HRandomPlusEngine(config).Randomize(first.HitObjects, keys, 20260901);
        new HRandomPlusEngine(config).Randomize(second.HitObjects, keys, 20260901);

        BeatmapValidator.ValidatePlayableStructure(first.HitObjects, keys, assigned: true);
        Assert.Equal(originalTimes, first.HitObjects.Select(note => note.StartTime));
        Assert.Equal(first.HitObjects.Select(note => note.AssignedColumn),
            second.HitObjects.Select(note => note.AssignedColumn));
        if (preserveStages)
            Assert.All(first.HitObjects, note => Assert.True(DoesNotCrossOppositeStage(note, keys)));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    public void DisabledDualStageCanUseTheOppositeLateralStage(int keys)
    {
        bool crossed = false;
        byte[] source = TestBeatmaps.Mania(keys, new[] { TestBeatmaps.Note(keys, 0, 1000) });
        int oppositeStage = keys % 2 == 0 ? 1 : 2;

        for (long seed = 0; seed < 256 && !crossed; seed++)
        {
            OsuBeatmapDocument document = OsuBeatmapDocument.Parse("global.osu", source);
            new HRandomPlusEngine(new HRandomConfig { PreserveDualStages = false })
                .Randomize(document.HitObjects, keys, seed);
            crossed = DualStageLayout.StageOf(document.HitObjects[0].AssignedColumn, keys) == oppositeStage;
        }

        Assert.True(crossed);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(15)]
    [InlineData(17)]
    public void EnabledOddDualStageAllowsCenterToMoveIntoEitherStage(int keys)
    {
        bool reachedLeft = false;
        bool reachedRight = false;
        byte[] source = TestBeatmaps.Mania(keys, new[] { TestBeatmaps.Note(keys, keys / 2, 1000) });

        for (long seed = 0; seed < 128 && (!reachedLeft || !reachedRight); seed++)
        {
            OsuBeatmapDocument document = OsuBeatmapDocument.Parse("odd-dual.osu", source);
            new HRandomPlusEngine(new HRandomConfig { PreserveDualStages = true }).Randomize(document.HitObjects, keys, seed);
            int stage = DualStageLayout.StageOf(document.HitObjects[0].AssignedColumn, keys);
            reachedLeft |= stage == 0;
            reachedRight |= stage == 2;
        }

        Assert.True(reachedLeft);
        Assert.True(reachedRight);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(15)]
    [InlineData(17)]
    public void SharedCenterRemainsNeutralForHandBalance(int keys)
    {
        var state = new RandomState(keys, 24, 16);

        Assert.True(state.GetHand(keys / 2) is null);
        Assert.Equal(HandSide.Left, state.GetHand(keys / 2 - 1));
        Assert.Equal(HandSide.Right, state.GetHand(keys / 2 + 1));
    }

    [Fact]
    public void DualStageModePreservesLongNotesAndStageOwnership()
    {
        const int keys = 12;
        string[] lines =
        {
            TestBeatmaps.LongNote(keys, 0, 1000, 2000),
            TestBeatmaps.LongNote(keys, 6, 1000, 2000),
            TestBeatmaps.Note(keys, 1, 1200),
            TestBeatmaps.Note(keys, 7, 1200),
            TestBeatmaps.Note(keys, 2, 2000),
            TestBeatmaps.Note(keys, 8, 2000)
        };
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse("dual-ln.osu", TestBeatmaps.Mania(keys, lines));

        new HRandomPlusEngine(new HRandomConfig { PreserveDualStages = true }).Randomize(document.HitObjects, keys, 2026);

        BeatmapValidator.ValidatePlayableStructure(document.HitObjects, keys, assigned: true);
        Assert.All(document.HitObjects, note => Assert.Equal(
            DualStageLayout.StageOf(note.OriginalColumn, keys),
            DualStageLayout.StageOf(note.AssignedColumn, keys)));
    }

    [Fact]
    public void DualStageSettingIsAnExactNoOpBelow10K()
    {
        const int keys = 9;
        string[] lines = Enumerable.Range(0, 40).Select(index =>
            TestBeatmaps.Note(keys, index % keys, 1000 + index * 80)).ToArray();
        OsuBeatmapDocument disabled = OsuBeatmapDocument.Parse("disabled.osu", TestBeatmaps.Mania(keys, lines));
        OsuBeatmapDocument enabled = OsuBeatmapDocument.Parse("enabled.osu", TestBeatmaps.Mania(keys, lines));

        new HRandomPlusEngine(new HRandomConfig { PreserveDualStages = false }).Randomize(disabled.HitObjects, keys, 77);
        new HRandomPlusEngine(new HRandomConfig { PreserveDualStages = true }).Randomize(enabled.HitObjects, keys, 77);

        Assert.Equal(disabled.HitObjects.Select(note => note.AssignedColumn),
            enabled.HitObjects.Select(note => note.AssignedColumn));
    }

    [Fact]
    public void CompatibleChordContinuesTrillButChordWithBothAnchorsResetsIt()
    {
        var state = new RandomState(4, 16, 16);
        state.RecordGroup(1000, new[] { 0 });
        state.RecordGroup(1100, new[] { 1 });
        state.RecordGroup(1200, new[] { 0 });

        Assert.Equal(4, state.AlternationContinuationLength(new[] { 1, 2 }, 1300, 640));
        Assert.Equal(0, state.AlternationContinuationLength(new[] { 0, 1 }, 1300, 640));
    }

    [Fact]
    public void PreviousOnlyOrUnrelatedChordDoesNotContinueTrill()
    {
        var state = new RandomState(4, 16, 16);
        state.RecordGroup(1000, new[] { 0 });
        state.RecordGroup(1100, new[] { 1 });
        state.RecordGroup(1200, new[] { 0 });

        Assert.Equal(0, state.AlternationContinuationLength(new[] { 0, 2 }, 1300, 640));
        Assert.Equal(0, state.AlternationContinuationLength(new[] { 2, 3 }, 1300, 640));
    }

    [Fact]
    public void ConsecutiveCompatibleChordsAndLongNoteHeadsShareTrillSemantics()
    {
        var state = new RandomState(4, 16, 16);
        state.RecordGroup(1000, new[] { 0, 2 });
        state.RecordGroup(1100, new[] { 1, 3 });
        state.RecordGroup(1200, new[] { 0, 2 });
        Assert.Equal(4, state.AlternationContinuationLength(new[] { 1, 3 }, 1300, 640));

        OsuBeatmapDocument document = OsuBeatmapDocument.Parse("ln-trill.osu", TestBeatmaps.Mania(4, new[]
        {
            TestBeatmaps.LongNote(4, 0, 1000, 1050),
            TestBeatmaps.Note(4, 1, 1100),
            TestBeatmaps.LongNote(4, 0, 1200, 1250),
            TestBeatmaps.Note(4, 1, 1300),
            TestBeatmaps.Note(4, 2, 1300)
        }));
        Assert.Equal(1, PatternAnalyzer.Analyze(document.HitObjects, 4, 100, 160, false).Trills);
    }

    [Fact]
    public void CompatibleChordReceivesTheSameTrillPenaltyUsedByStatistics()
    {
        var state = new RandomState(4, 16, 16);
        state.RecordGroup(1000, new[] { 0 });
        state.RecordGroup(1100, new[] { 1 });
        state.RecordGroup(1200, new[] { 0 });
        var config = ZeroWeightConfig();
        config.Weights.TrillPenalty = 25;

        double score = new CandidateScorer(config).ScoreSet(state, new[] { 1, 2 }, 1300, 100);
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse("trill.osu", TestBeatmaps.Mania(4, new[]
        {
            TestBeatmaps.Note(4, 0, 1000),
            TestBeatmaps.Note(4, 1, 1100),
            TestBeatmaps.Note(4, 0, 1200),
            TestBeatmaps.Note(4, 1, 1300),
            TestBeatmaps.Note(4, 2, 1300)
        }));
        PatternStatistics statistics = PatternAnalyzer.Analyze(document.HitObjects, 4, 100, 160, false);

        Assert.Equal(-25d, score);
        Assert.Equal(1, statistics.Trills);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(160)]
    [InlineData(1000)]
    public void TrillUsesStrictFourTimesPauseBoundary(int maximumThreshold)
    {
        var state = new RandomState(4, 16, 16);
        state.RecordGroup(1000, new[] { 0 });
        state.RecordGroup(1000 + maximumThreshold, new[] { 1 });
        state.RecordGroup(1000 + maximumThreshold * 2, new[] { 0 });
        int latest = 1000 + maximumThreshold * 2;
        long boundary = (long)maximumThreshold * HRandomConfig.TrillPauseMultiplier;

        Assert.Equal(4, state.AlternationContinuationLength(new[] { 1 }, checked(latest + (int)boundary - 1), boundary));
        Assert.Equal(4, state.AlternationContinuationLength(new[] { 1 }, checked(latest + (int)boundary), boundary));
        Assert.Equal(0, state.AlternationContinuationLength(new[] { 1 }, checked(latest + (int)boundary + 1), boundary));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(160)]
    [InlineData(400)]
    public void DynamicThresholdUsesStrictEightTimesPauseBoundary(int maximumThreshold)
    {
        var state = new RandomState(4, 24, 16);
        for (int index = 0; index < 8; index++) state.RecordGroup(index * 100, new[] { index % 4 });
        var config = new HRandomConfig { MinThresholdMs = 1, BaseThresholdMs = 50, MaxThresholdMs = maximumThreshold };
        int latest = state.RecentPatterns[^1].Time;
        int boundary = maximumThreshold * HRandomConfig.DynamicThresholdPauseMultiplier;

        Assert.True(PatternAnalyzer.DynamicThreshold(state, latest + boundary - 1, 1, config) != config.BaseThresholdMs);
        Assert.True(PatternAnalyzer.DynamicThreshold(state, latest + boundary, 1, config) != config.BaseThresholdMs);
        Assert.Equal(config.BaseThresholdMs, PatternAnalyzer.DynamicThreshold(state, latest + boundary + 1, 1, config));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(160)]
    [InlineData(400)]
    public void DisabledDynamicThresholdAlwaysUsesBaseAcrossPauseBoundaries(int maximumThreshold)
    {
        var state = new RandomState(4, 24, 16);
        state.RecordGroup(1000, new[] { 0 });
        var config = new HRandomConfig
        {
            DynamicThreshold = false,
            MinThresholdMs = 1,
            BaseThresholdMs = 50,
            MaxThresholdMs = maximumThreshold
        };
        int boundary = maximumThreshold * HRandomConfig.DynamicThresholdPauseMultiplier;

        Assert.Equal(config.BaseThresholdMs, PatternAnalyzer.DynamicThreshold(state, 1000 + boundary - 1, 1, config));
        Assert.Equal(config.BaseThresholdMs, PatternAnalyzer.DynamicThreshold(state, 1000 + boundary, 1, config));
        Assert.Equal(config.BaseThresholdMs, PatternAnalyzer.DynamicThreshold(state, 1000 + boundary + 1, 1, config));
    }

    [Fact]
    public void BpmSummaryShowsOneValueOrOnlyTheRange()
    {
        Assert.Equal("BPM: —", BeatSnapReference.DescribeBpmRange(Array.Empty<double>()));
        Assert.Equal("BPM: 180", BeatSnapReference.DescribeBpmRange(new[] { 180d }));
        Assert.Equal("BPM range: 150–190", BeatSnapReference.DescribeBpmRange(new[] { 180d, 150d, 190d, 175d }));
    }

    private static HRandomConfig ZeroWeightConfig() => new()
    {
        DynamicThreshold = false,
        Weights = new ScoringWeights
        {
            TimeSinceLastUseBonus = 0,
            HandBalanceBonus = 0,
            DistributionBonus = 0,
            JackPenalty = 0,
            TrillPenalty = 0,
            RepeatedPatternPenalty = 0,
            SameHandPenalty = 0,
            ExtremeJumpPenalty = 0,
            RecentUsagePenalty = 0
        }
    };

    private static string[] BuildMatrixLines(int keys)
    {
        int rightStart = (keys + 1) / 2;
        var lines = new List<string>
        {
            TestBeatmaps.LongNote(keys, 0, 1000, 1400),
            TestBeatmaps.LongNote(keys, rightStart, 1000, 1400)
        };
        if (keys % 2 != 0) lines.Add(TestBeatmaps.Note(keys, keys / 2, 1000));
        for (int index = 0; index < 24; index++)
        {
            int time = 1100 + index * 100;
            lines.Add(TestBeatmaps.Note(keys, 1 + index % Math.Max(1, keys / 2 - 1), time));
            lines.Add(TestBeatmaps.Note(keys, rightStart + 1 + index % Math.Max(1, keys - rightStart - 1), time));
            if (keys % 2 != 0 && index % 3 == 0)
                lines.Add(TestBeatmaps.Note(keys, keys / 2, time));
        }
        return lines.ToArray();
    }

    private static bool DoesNotCrossOppositeStage(ManiaHitObject note, int keys)
    {
        int origin = DualStageLayout.StageOf(note.OriginalColumn, keys);
        int destination = DualStageLayout.StageOf(note.AssignedColumn, keys);
        if (keys % 2 == 0) return origin == destination;
        return origin == 1 || destination == 1 || origin == destination;
    }
}
