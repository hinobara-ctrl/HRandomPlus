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
        Task<BeatmapSourceResult> stableTask = stable.GetCurrentAsync(cancellationToken);
        Task<BeatmapSourceResult> lazerTask = lazer.GetCurrentAsync(cancellationToken);
        await Task.WhenAll(stableTask, lazerTask).ConfigureAwait(false);
        BeatmapSourceResult stableResult = stableTask.Result;
        BeatmapSourceResult lazerResult = lazerTask.Result;
        UpdateActivity(stableResult, ref stableIdentity, ref stableChanged);
        UpdateActivity(lazerResult, ref lazerIdentity, ref lazerChanged);

        if (stableResult.Success && lazerResult.Success)
        {
            if (active == BeatmapDetectionSource.Lazer && lazerChanged >= stableChanged) return lazerResult;
            if (active is BeatmapDetectionSource.WindowsMemory or BeatmapDetectionSource.Tosu && stableChanged >= lazerChanged) return stableResult;
            BeatmapSourceResult selected = lazerChanged > stableChanged ? lazerResult : stableResult;
            active = selected.DetectionSource;
            return selected;
        }
        if (lazerResult.Success) { active = BeatmapDetectionSource.Lazer; return lazerResult; }
        if (stableResult.Success) { active = stableResult.DetectionSource; return stableResult; }
        active = null;
        if (stableResult.IsAvailable) return stableResult;
        if (lazerResult.IsAvailable) return lazerResult;
        return BeatmapSourceResult.Unavailable($"{stableResult.Status}; {lazerResult.Status}");
    }

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
