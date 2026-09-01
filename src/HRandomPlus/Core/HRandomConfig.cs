using System.Text.Json;
using System.ComponentModel;

namespace HRandomPlus.Core;

public sealed class HRandomConfig
{
    public const int DefaultMaxCandidateSets = 4096;
    public const int MaximumCandidateSets = 8192;
    public const int TrillPauseMultiplier = 4;
    public const int DynamicThresholdPauseMultiplier = 8;
    public long? Seed { get; set; }
    public bool DynamicThreshold { get; set; } = true;
    public bool PreserveDualStages { get; set; }
    public int MinThresholdMs { get; set; } = 40;
    public int BaseThresholdMs { get; set; } = 100;
    public int MaxThresholdMs { get; set; } = 160;
    public int RecentUsageWindow { get; set; } = 24;
    public int PatternHistoryLength { get; set; } = 16;
    public int WeightedTopCandidates { get; set; } = 12;
    public double WeightedTemperature { get; set; } = 12;
    public int MaxCandidateSets { get; set; } = DefaultMaxCandidateSets;
    public bool RenameDifficulty { get; set; } = true;
    public string DifficultySuffix { get; set; } = " H-RANDOM+";
    public ScoringWeights Weights { get; set; } = new();

    public HRandomConfig Clone()
        => JsonSerializer.Deserialize<HRandomConfig>(JsonSerializer.Serialize(this))!;

    public static HRandomConfig Load(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        HRandomConfig config = JsonSerializer.Deserialize<HRandomConfig>(File.ReadAllText(path), options)
                               ?? throw new InvalidDataException("El archivo de configuración está vacío.");
        config.NormalizePersistedValues();
        return config;
    }

    public bool NormalizePersistedValues()
    {
        int maximum = Math.Clamp(MaxCandidateSets, 1, MaximumCandidateSets);
        int weighted = Math.Clamp(WeightedTopCandidates, 1, maximum);
        bool changed = maximum != MaxCandidateSets || weighted != WeightedTopCandidates;
        MaxCandidateSets = maximum;
        WeightedTopCandidates = weighted;
        return changed;
    }

    public void Validate()
    {
        if (MinThresholdMs < 0 || BaseThresholdMs < MinThresholdMs || MaxThresholdMs < BaseThresholdMs)
            throw new ArgumentException("Los thresholds deben cumplir 0 <= min <= base <= max.");
        if (RecentUsageWindow is < 4 or > 256)
            throw new ArgumentException("RecentUsageWindow debe estar entre 4 y 256.");
        if (PatternHistoryLength is < 4 or > 256)
            throw new ArgumentException("PatternHistoryLength debe estar entre 4 y 256.");
        if (MaxCandidateSets is < 1 or > MaximumCandidateSets)
            throw new ArgumentException($"MaxCandidateSets debe estar entre 1 y {MaximumCandidateSets}.");
        if (WeightedTopCandidates < 1 || WeightedTopCandidates > MaxCandidateSets)
            throw new ArgumentException("WeightedTopCandidates debe estar entre 1 y MaxCandidateSets.");
        if (WeightedTemperature <= 0 || !double.IsFinite(WeightedTemperature))
            throw new ArgumentException("WeightedTemperature debe ser mayor que cero.");
        if (Weights is null)
            throw new ArgumentException("Weights no puede ser null.");
        double[] weights =
        {
            Weights.TimeSinceLastUseBonus,
            Weights.HandBalanceBonus,
            Weights.DistributionBonus,
            Weights.JackPenalty,
            Weights.TrillPenalty,
            Weights.RepeatedPatternPenalty,
            Weights.SameHandPenalty,
            Weights.ExtremeJumpPenalty,
            Weights.RecentUsagePenalty
        };
        if (weights.Any(weight => !double.IsFinite(weight)))
            throw new ArgumentException("Los pesos de scoring deben ser números finitos.");
        PortableFileNames.ValidateDifficultySuffix(DifficultySuffix);
    }
}

public static class DualStageLayout
{
    public const int MinimumKeys = 10;

    public static bool IsEligible(int keys) => keys >= MinimumKeys;

    public static int StageOf(int column, int keys)
    {
        if (column < 0 || column >= keys) throw new ArgumentOutOfRangeException(nameof(column));
        int center = keys / 2;
        if (keys % 2 != 0 && column == center) return 1;
        return column < center ? 0 : keys % 2 == 0 ? 1 : 2;
    }
}

public static class PortableFileNames
{
    private const string InvalidCharacters = "<>:\"/\\|?*";

    public static void ValidateDifficultySuffix(string? suffix)
    {
        if (suffix is null) throw new ArgumentException("DifficultySuffix no puede ser null.");
        if (suffix.Any(character => char.IsControl(character) || InvalidCharacters.Contains(character)))
            throw new ArgumentException("DifficultySuffix contiene caracteres no válidos en nombres de archivo portables: < > : \" / \\ | ? * o controles.");
        if (suffix.EndsWith(' ') || suffix.EndsWith('.'))
            throw new ArgumentException("DifficultySuffix no puede terminar en espacio o punto.");
    }
}

public static class CombinationMath
{
    public static long CountBounded(int n, int k, long limit = long.MaxValue)
    {
        if (n < 0 || k < 0 || k > n || limit < 0) return 0;
        k = Math.Min(k, n - k);
        long result = 1;
        for (int i = 1; i <= k; i++)
        {
            long factor = n - k + i;
            long divisor = i;
            long common = GreatestCommonDivisor(factor, divisor);
            factor /= common;
            divisor /= common;
            common = GreatestCommonDivisor(result, divisor);
            result /= common;
            divisor /= common;
            if (divisor != 1 || result > limit / factor) return limit;
            result *= factor;
            if (result >= limit) return limit;
        }
        return result;
    }

    private static long GreatestCommonDivisor(long left, long right)
    {
        while (right != 0) (left, right) = (right, left % right);
        return left;
    }
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class ScoringWeights
{
    public double TimeSinceLastUseBonus { get; set; } = 18;
    public double HandBalanceBonus { get; set; } = 6;
    public double DistributionBonus { get; set; } = 5;
    public double JackPenalty { get; set; } = 80;
    public double TrillPenalty { get; set; } = 25;
    public double RepeatedPatternPenalty { get; set; } = 14;
    public double SameHandPenalty { get; set; } = 5;
    public double ExtremeJumpPenalty { get; set; } = 6;
    public double RecentUsagePenalty { get; set; } = 8;
    public override string ToString() => "Pesos de scoring";
}
