using HRandomPlus.Core;

namespace HRandomPlus.Randomization;

public sealed class CandidateScorer
{
    private readonly HRandomConfig config;

    public CandidateScorer(HRandomConfig config) => this.config = config;

    public double ScoreSet(RandomState state, IReadOnlyList<int> columns, int time, int threshold)
    {
        double score = columns.Sum(column => ScoreColumn(state, column, time, threshold));
        int[] sorted = columns.OrderBy(c => c).ToArray();

        if (sorted.Length > 1)
        {
            HandSide? first = state.GetHand(sorted[0]);
            if (first is not null && sorted.All(c => state.GetHand(c) == first))
                score -= config.Weights.SameHandPenalty * sorted.Length;
        }

        int repeats = state.RecentPatterns.Count(p => p.Columns.SequenceEqual(sorted));
        score -= repeats * config.Weights.RepeatedPatternPenalty;
        long trillTimeout = Math.Min(int.MaxValue, (long)config.MaxThresholdMs * HRandomConfig.TrillPauseMultiplier);
        int alternationLength = state.AlternationContinuationLength(sorted, time, trillTimeout);
        if (alternationLength >= 4)
            score -= config.Weights.TrillPenalty * (alternationLength - 3);
        return score;
    }

    private double ScoreColumn(RandomState state, int column, int time, int threshold)
    {
        ScoringWeights weights = config.Weights;
        long last = state.LastNoteTime[column];
        double delta = last < -1_000_000 ? config.MaxThresholdMs * 2.0 : Math.Max(0, time - last);
        double score = Math.Min(2, delta / Math.Max(1, config.MaxThresholdMs)) * weights.TimeSinceLastUseBonus;

        if (delta <= threshold)
            score -= weights.JackPenalty * (1 + (threshold - delta) / Math.Max(1, threshold));

        int maxUse = state.RecentColumnUsage.Max();
        score += (maxUse - state.RecentColumnUsage[column]) * weights.DistributionBonus;
        score -= state.RecentColumnUsage[column] * weights.RecentUsagePenalty /
                 Math.Max(1, config.RecentUsageWindow);

        HandSide? hand = state.GetHand(column);
        if (hand == HandSide.Left)
            score += (state.RightHandUsage - state.LeftHandUsage) * weights.HandBalanceBonus /
                     Math.Max(1, config.RecentUsageWindow);
        else if (hand == HandSide.Right)
            score += (state.LeftHandUsage - state.RightHandUsage) * weights.HandBalanceBonus /
                     Math.Max(1, config.RecentUsageWindow);

        PatternSnapshot? lastPattern = state.RecentPatterns.LastOrDefault();
        if (lastPattern is { Columns.Length: 1 } && time - (long)lastPattern.Time <= (long)threshold * 2L && state.Keys > 1)
        {
            double distance = Math.Abs(column - lastPattern.Columns[0]) / (double)(state.Keys - 1);
            if (distance >= 0.75)
                score -= weights.ExtremeJumpPenalty * distance;
        }

        return score;
    }
}
