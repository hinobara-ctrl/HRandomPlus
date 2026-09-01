using System.Globalization;

namespace HRandomPlus.Core;

public sealed record HRandomConfigInput(string Seed, bool DynamicThreshold, bool PreserveDualStages, bool RenameDifficulty,
    IReadOnlyDictionary<string, string> Values);

public static class HRandomConfigInputParser
{
    public static HRandomConfig Parse(HRandomConfigInput input)
    {
        string Value(string key) => input.Values.TryGetValue(key, out string? value)
            ? value
            : throw new ArgumentException($"Falta el parámetro {key}.");
        int Integer(string key) => int.Parse(Value(key), CultureInfo.InvariantCulture);
        double Number(string key) => double.Parse(Value(key), CultureInfo.InvariantCulture);

        var config = new HRandomConfig
        {
            Seed = string.IsNullOrWhiteSpace(input.Seed) ? null : long.Parse(input.Seed, CultureInfo.InvariantCulture),
            DynamicThreshold = input.DynamicThreshold,
            PreserveDualStages = input.PreserveDualStages,
            RenameDifficulty = input.RenameDifficulty,
            MinThresholdMs = Integer("MinThresholdMs"),
            BaseThresholdMs = Integer("BaseThresholdMs"),
            MaxThresholdMs = Integer("MaxThresholdMs"),
            RecentUsageWindow = Integer("RecentUsageWindow"),
            PatternHistoryLength = Integer("PatternHistoryLength"),
            WeightedTopCandidates = Integer("WeightedTopCandidates"),
            WeightedTemperature = Number("WeightedTemperature"),
            MaxCandidateSets = Integer("MaxCandidateSets"),
            DifficultySuffix = Value("DifficultySuffix"),
            Weights = new ScoringWeights
            {
                TimeSinceLastUseBonus = Number("TimeSinceLastUseBonus"),
                HandBalanceBonus = Number("HandBalanceBonus"),
                DistributionBonus = Number("DistributionBonus"),
                JackPenalty = Number("JackPenalty"),
                TrillPenalty = Number("TrillPenalty"),
                RepeatedPatternPenalty = Number("RepeatedPatternPenalty"),
                SameHandPenalty = Number("SameHandPenalty"),
                ExtremeJumpPenalty = Number("ExtremeJumpPenalty"),
                RecentUsagePenalty = Number("RecentUsagePenalty")
            }
        };
        config.Validate();
        return config;
    }
}
