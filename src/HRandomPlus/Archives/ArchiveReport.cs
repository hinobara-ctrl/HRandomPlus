using HRandomPlus.Analysis;

namespace HRandomPlus.Archives;

public sealed class ArchiveReport
{
    public string Input { get; init; } = string.Empty;
    public string Output { get; init; } = string.Empty;
    public long Seed { get; init; }
    public List<DifficultyReport> Difficulties { get; init; } = new();
}

public sealed class DifficultyReport
{
    public string OriginalFile { get; init; } = string.Empty;
    public string OutputFile { get; init; } = string.Empty;
    public string OriginalVersion { get; init; } = string.Empty;
    public string OutputVersion { get; init; } = string.Empty;
    public PatternStatistics Before { get; init; } = new();
    public PatternStatistics After { get; init; } = new();
}
