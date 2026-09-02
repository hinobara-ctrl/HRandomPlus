namespace HRandomPlus.Integration.Beatmaps;

public sealed record StableProcessIdentity(int ProcessId, string ExecutableDirectory, DateTimeOffset StartTime)
{
    public string SongsRoot => Path.Combine(ExecutableDirectory, "Songs");
}

public enum StableProcessSelectionStatus
{
    None,
    Selected,
    Ambiguous
}

public sealed record StableProcessSelection(StableProcessSelectionStatus Status,
    StableProcessIdentity? Identity, string Message);

public static class StableProcessSelector
{
    public static StableProcessSelection Select(IEnumerable<StableProcessIdentity> source,
        string? configuredPath, int? currentProcessId, DateTimeOffset? currentProcessStartTime = null,
        bool readerCanBindToIdentity = false, int? readerTargetProcessCount = null)
    {
        StableProcessIdentity[] candidates = source
            .GroupBy(candidate => (candidate.ProcessId, candidate.StartTime))
            .Select(group => group.First())
            .OrderBy(candidate => candidate.ExecutableDirectory, PathComparer)
            .ThenBy(candidate => candidate.ProcessId)
            .ToArray();
        if (candidates.Length == 0)
            return new(StableProcessSelectionStatus.None, null, "osu!stable not detected");

        int matchingProcesses = readerTargetProcessCount ?? candidates.Length;
        if (!readerCanBindToIdentity && matchingProcesses != 1)
            return new(StableProcessSelectionStatus.Ambiguous, null,
                "Multiple x86 processes eligible as osu!stable are running; close the other instances or select a .osu file manually");

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string? configured = TryNormalize(configuredPath);
            if (configured is not null)
            {
                StableProcessIdentity[] matches = candidates
                    .Where(candidate => PathComparer.Equals(TryNormalize(candidate.ExecutableDirectory), configured))
                    .ToArray();
                StableProcessSelection configuredSelection = Resolve(matches, currentProcessId, currentProcessStartTime,
                    "Multiple osu!stable processes match the configured folder");
                if (configuredSelection.Status != StableProcessSelectionStatus.None)
                    return configuredSelection;
            }
        }

        StableProcessIdentity? current = candidates.FirstOrDefault(candidate =>
            candidate.ProcessId == currentProcessId &&
            (currentProcessStartTime is null || candidate.StartTime == currentProcessStartTime));
        if (current is not null)
            return new(StableProcessSelectionStatus.Selected, current, string.Empty);
        return Resolve(candidates, currentProcessId, currentProcessStartTime,
            "Multiple osu!stable installations are available; configure the intended osu!stable folder");
    }

    private static StableProcessSelection Resolve(StableProcessIdentity[] candidates, int? currentProcessId,
        DateTimeOffset? currentProcessStartTime, string ambiguousMessage)
    {
        if (candidates.Length == 0) return new(StableProcessSelectionStatus.None, null, string.Empty);
        if (currentProcessId is int current)
        {
            StableProcessIdentity? retained = candidates.FirstOrDefault(candidate => candidate.ProcessId == current &&
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

public sealed class StableReaderSession<TReader> : IDisposable where TReader : class, IDisposable
{
    public StableProcessIdentity? Identity { get; private set; }
    public TReader? Reader { get; private set; }

    public TReader GetOrCreate(StableProcessIdentity identity, Func<TReader> factory)
    {
        if (Reader is not null && Identity == identity) return Reader;
        Invalidate();
        TReader created = factory();
        Reader = created;
        Identity = identity;
        return created;
    }

    public void Invalidate()
    {
        Reader?.Dispose();
        Reader = null;
        Identity = null;
    }

    public void Dispose() => Invalidate();
}
