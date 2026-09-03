using HRandomPlus.Core;

namespace HRandomPlus.Randomization;

public sealed class CandidateScorer
{
    private readonly HRandomConfig config;

    public CandidateScorer(HRandomConfig config) => this.config = config;

    public double ScoreSet(RandomState state, IReadOnlyList<int> columns, int time, int threshold)
    {
        CandidateScoringContext context = CreateContext(state, time, threshold);
        int[] sorted = columns.OrderBy(column => column).ToArray();
        return ScoreSet(context, columns, sorted);
    }

    internal CandidateScoringContext CreateContext(RandomState state, int time, int threshold)
        => new(state, time, threshold, state.RecentColumnUsage.Max(), state.RecentPatterns.LastOrDefault(),
            Math.Min(int.MaxValue, (long)config.MaxThresholdMs * HRandomConfig.TrillPauseMultiplier));

    internal double ScoreSortedSet(CandidateScoringContext context, int[] sortedColumns)
        => ScoreSet(context, sortedColumns, sortedColumns);

    private double ScoreSet(CandidateScoringContext context, IReadOnlyList<int> columns, int[] sorted)
    {
        RandomState state = context.State;
        double score = 0;
        foreach (int column in columns)
            score += ScoreColumn(context, column);

        if (sorted.Length > 1)
        {
            HandSide? first = state.GetHand(sorted[0]);
            if (first is not null && sorted.All(c => state.GetHand(c) == first))
                score -= config.Weights.SameHandPenalty * sorted.Length;
        }

        int repeats = state.RecentPatterns.Count(p => p.Columns.SequenceEqual(sorted));
        score -= repeats * config.Weights.RepeatedPatternPenalty;
        int alternationLength = state.AlternationContinuationLength(sorted, context.Time, context.TrillTimeout);
        if (alternationLength >= 4)
            score -= config.Weights.TrillPenalty * (alternationLength - 3);
        return score;
    }

    private double ScoreColumn(CandidateScoringContext context, int column)
    {
        RandomState state = context.State;
        ScoringWeights weights = config.Weights;
        long last = state.LastNoteTime[column];
        double delta = last < -1_000_000 ? config.MaxThresholdMs * 2.0 : Math.Max(0, context.Time - last);
        double score = Math.Min(2, delta / Math.Max(1, config.MaxThresholdMs)) * weights.TimeSinceLastUseBonus;

        if (delta <= context.Threshold)
            score -= weights.JackPenalty * (1 + (context.Threshold - delta) / Math.Max(1, context.Threshold));

        score += (context.MaximumRecentUse - state.RecentColumnUsage[column]) * weights.DistributionBonus;
        score -= state.RecentColumnUsage[column] * weights.RecentUsagePenalty /
                 Math.Max(1, config.RecentUsageWindow);

        HandSide? hand = state.GetHand(column);
        if (hand == HandSide.Left)
            score += (state.RightHandUsage - state.LeftHandUsage) * weights.HandBalanceBonus /
                     Math.Max(1, config.RecentUsageWindow);
        else if (hand == HandSide.Right)
            score += (state.LeftHandUsage - state.RightHandUsage) * weights.HandBalanceBonus /
                     Math.Max(1, config.RecentUsageWindow);

        PatternSnapshot? lastPattern = context.LastPattern;
        if (lastPattern is { Columns.Length: 1 } && context.Time - (long)lastPattern.Time <= (long)context.Threshold * 2L && state.Keys > 1)
        {
            double distance = Math.Abs(column - lastPattern.Columns[0]) / (double)(state.Keys - 1);
            if (distance >= 0.75)
                score -= weights.ExtremeJumpPenalty * distance;
        }

        return score;
    }
}

internal readonly record struct CandidateScoringContext(RandomState State, int Time, int Threshold,
                                                        int MaximumRecentUse, PatternSnapshot? LastPattern,
                                                        long TrillTimeout);
