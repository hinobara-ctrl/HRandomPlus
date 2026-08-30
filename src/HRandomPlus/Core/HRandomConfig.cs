using System.Text.Json;
using System.ComponentModel;

namespace HRandomPlus.Core;

public sealed class HRandomConfig
{
    public long? Seed { get; set; }
    public bool DynamicThreshold { get; set; } = true;
    public int MinThresholdMs { get; set; } = 40;
    public int BaseThresholdMs { get; set; } = 100;
    public int MaxThresholdMs { get; set; } = 160;
    public int RecentUsageWindow { get; set; } = 24;
    public int PatternHistoryLength { get; set; } = 16;
    public int WeightedTopCandidates { get; set; } = 12;
    public double WeightedTemperature { get; set; } = 12;
    public int MaxCandidateSets { get; set; } = 4096;
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
        return JsonSerializer.Deserialize<HRandomConfig>(File.ReadAllText(path), options)
               ?? throw new InvalidDataException("El archivo de configuración está vacío.");
    }

    public void Validate()
    {
        if (MinThresholdMs < 0 || BaseThresholdMs < MinThresholdMs || MaxThresholdMs < BaseThresholdMs)
            throw new ArgumentException("Los thresholds deben cumplir 0 <= min <= base <= max.");
        if (RecentUsageWindow is < 4 or > 256)
            throw new ArgumentException("RecentUsageWindow debe estar entre 4 y 256.");
        if (PatternHistoryLength is < 4 or > 256)
            throw new ArgumentException("PatternHistoryLength debe estar entre 4 y 256.");
        if (WeightedTopCandidates < 1 || MaxCandidateSets < WeightedTopCandidates)
            throw new ArgumentException("La cantidad de candidatos ponderados no es válida.");
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
        if (DifficultySuffix is null)
            throw new ArgumentException("DifficultySuffix no puede ser null.");
        if (DifficultySuffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("DifficultySuffix contiene caracteres no válidos para nombres de archivo.");
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
