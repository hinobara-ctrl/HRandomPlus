namespace HRandomPlus.Integration.Beatmaps;

public sealed record StableProcessCandidate(int ProcessId, string ExecutableDirectory, DateTimeOffset StartTime);

public enum StableProcessSelectionStatus
{
    None,
    Selected,
    Ambiguous
}

public sealed record StableProcessSelection(StableProcessSelectionStatus Status,
    StableProcessCandidate? Candidate, string Message);

public static class StableProcessSelector
{
    public static StableProcessSelection Select(IEnumerable<StableProcessCandidate> source,
        string? configuredPath, int? currentProcessId, DateTimeOffset? currentProcessStartTime = null)
    {
        StableProcessCandidate[] candidates = source
            .GroupBy(candidate => candidate.ProcessId)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.ExecutableDirectory, PathComparer)
            .ThenBy(candidate => candidate.ProcessId)
            .ToArray();
        if (candidates.Length == 0)
            return new(StableProcessSelectionStatus.None, null, "osu!stable not detected");

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string? configured = TryNormalize(configuredPath);
            if (configured is not null)
            {
                StableProcessCandidate[] matches = candidates
                    .Where(candidate => PathComparer.Equals(TryNormalize(candidate.ExecutableDirectory), configured))
                    .ToArray();
                StableProcessSelection configuredSelection = Resolve(matches, currentProcessId, currentProcessStartTime,
                    "Multiple osu!stable processes match the configured folder");
                if (configuredSelection.Status != StableProcessSelectionStatus.None)
                    return configuredSelection;
            }
        }

        StableProcessCandidate? current = candidates.FirstOrDefault(candidate =>
            candidate.ProcessId == currentProcessId &&
            (currentProcessStartTime is null || candidate.StartTime == currentProcessStartTime));
        if (current is not null)
            return new(StableProcessSelectionStatus.Selected, current, string.Empty);
        return Resolve(candidates, currentProcessId, currentProcessStartTime,
            "Multiple osu!stable installations are available; configure the intended osu!stable folder");
    }

    private static StableProcessSelection Resolve(StableProcessCandidate[] candidates, int? currentProcessId,
        DateTimeOffset? currentProcessStartTime, string ambiguousMessage)
    {
        if (candidates.Length == 0) return new(StableProcessSelectionStatus.None, null, string.Empty);
        if (currentProcessId is int current)
        {
            StableProcessCandidate? retained = candidates.FirstOrDefault(candidate => candidate.ProcessId == current &&
                (currentProcessStartTime is null || candidate.StartTime == currentProcessStartTime));
            if (retained is not null)
                return new(StableProcessSelectionStatus.Selected, retained, string.Empty);
        }
        if (candidates.Length == 1)
            return new(StableProcessSelectionStatus.Selected, candidates[0], string.Empty);
        return new(StableProcessSelectionStatus.Ambiguous, null, ambiguousMessage);
    }

    private static string? TryNormalize(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
