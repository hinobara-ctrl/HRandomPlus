using HRandomPlus.Integration.Lazer;

namespace HRandomPlus.Integration.Beatmaps;

public sealed class ArbitratingBeatmapSource : IBeatmapSource, IDisposable, ILazerResolutionInvalidator
{
    private readonly IBeatmapSource stable;
    private readonly IBeatmapSource lazer;
    private string? stableIdentity;
    private string? lazerIdentity;
    private DateTimeOffset stableChanged;
    private DateTimeOffset lazerChanged;
    private BeatmapDetectionSource? active;

    public ArbitratingBeatmapSource(IBeatmapSource stable, IBeatmapSource lazer)
    {
        this.stable = stable;
        this.lazer = lazer;
    }

    public async Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        Task<SourceRead> stableTask = ReadSourceAsync(stable, "osu!stable", cancellationToken);
        Task<SourceRead> lazerTask = ReadSourceAsync(lazer, "osu!lazer", cancellationToken);
        await Task.WhenAll(stableTask, lazerTask).ConfigureAwait(false);
        SourceRead stableRead = stableTask.Result;
        SourceRead lazerRead = lazerTask.Result;
        BeatmapSourceResult stableResult = stableRead.Result;
        BeatmapSourceResult lazerResult = lazerRead.Result;
        UpdateActivity(stableResult, ref stableIdentity, ref stableChanged);
        UpdateActivity(lazerResult, ref lazerIdentity, ref lazerChanged);

        BeatmapSourceResult selected;
        if (stableResult.Success && lazerResult.Success)
        {
            if (active == BeatmapDetectionSource.Lazer && lazerChanged >= stableChanged) selected = lazerResult;
            else if (active is BeatmapDetectionSource.WindowsMemory or BeatmapDetectionSource.Tosu && stableChanged >= lazerChanged) selected = stableResult;
            else selected = lazerChanged > stableChanged ? lazerResult : stableResult;
            active = selected.DetectionSource;
        }
        else if (lazerResult.Success) { active = BeatmapDetectionSource.Lazer; selected = lazerResult; }
        else if (stableResult.Success) { active = stableResult.DetectionSource; selected = stableResult; }
        else
        {
            active = null;
            selected = stableResult.IsAvailable ? stableResult
                : lazerResult.IsAvailable ? lazerResult
                : BeatmapSourceResult.Unavailable($"{stableResult.Status}; {lazerResult.Status}");
        }

        return AppendFailures(selected, stableRead.Failure, lazerRead.Failure);
    }

    private static async Task<SourceRead> ReadSourceAsync(IBeatmapSource source, string name,
        CancellationToken cancellationToken)
    {
        try
        {
            return new SourceRead(await source.GetCurrentAsync(cancellationToken).ConfigureAwait(false), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            string failure = $"{name} source failed unexpectedly: {ex.GetType().Name}: {ex.Message}";
            return new SourceRead(BeatmapSourceResult.Unavailable(failure), failure);
        }
    }

    private static BeatmapSourceResult AppendFailures(BeatmapSourceResult result, params string?[] failures)
    {
        string[] details = failures.Where(value => !string.IsNullOrWhiteSpace(value) &&
            !result.Status.Contains(value, StringComparison.Ordinal)).Cast<string>().Distinct().ToArray();
        return details.Length == 0 ? result : result with
        {
            Status = string.IsNullOrWhiteSpace(result.Status)
                ? string.Join("; ", details)
                : $"{result.Status}; {string.Join("; ", details)}"
        };
    }

    private sealed record SourceRead(BeatmapSourceResult Result, string? Failure);

    private static void UpdateActivity(BeatmapSourceResult result, ref string? identity, ref DateTimeOffset changed)
    {
        string? current = result.Selection?.Beatmap.Identity;
        if (current is null || current == identity) return;
        identity = current;
        changed = result.ObservedAt ?? DateTimeOffset.UtcNow;
    }

    public void Dispose()
    {
        if (stable is IDisposable stableDisposable) stableDisposable.Dispose();
        if (lazer is IDisposable lazerDisposable) lazerDisposable.Dispose();
    }

    public void InvalidateLazerResolution()
    {
        if (lazer is ILazerResolutionInvalidator invalidator)
            invalidator.InvalidateLazerResolution();
        lazerIdentity = null;
        lazerChanged = default;
        if (active == BeatmapDetectionSource.Lazer) active = null;
    }
}
