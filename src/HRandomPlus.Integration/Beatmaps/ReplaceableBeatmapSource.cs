using HRandomPlus.Integration.Lazer;

namespace HRandomPlus.Integration.Beatmaps;

/// <summary>
/// Keeps replaced sources alive until every read that acquired them has finished.
/// </summary>
public sealed class ReplaceableBeatmapSource : IBeatmapSource, ILazerResolutionInvalidator, IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<IBeatmapSource, int> activeReads = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<IBeatmapSource> retired = new(ReferenceEqualityComparer.Instance);
    private IBeatmapSource current;
    private bool disposed;

    public ReplaceableBeatmapSource(IBeatmapSource initial)
        => current = initial ?? throw new ArgumentNullException(nameof(initial));

    public async Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        IBeatmapSource acquired = Acquire();
        try
        {
            return await acquired.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Release(acquired);
        }
    }

    public void Replace(IBeatmapSource replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        IBeatmapSource? disposeNow = null;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (ReferenceEquals(current, replacement)) return;
            IBeatmapSource previous = current;
            current = replacement;
            if (activeReads.ContainsKey(previous)) retired.Add(previous);
            else disposeNow = previous;
        }
        DisposeSource(disposeNow);
    }

    public void InvalidateLazerResolution()
    {
        IBeatmapSource acquired = Acquire();
        try
        {
            if (acquired is ILazerResolutionInvalidator invalidator)
                invalidator.InvalidateLazerResolution();
        }
        finally
        {
            Release(acquired);
        }
    }

    public void Dispose()
    {
        IBeatmapSource? disposeNow = null;
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            if (activeReads.ContainsKey(current)) retired.Add(current);
            else disposeNow = current;
        }
        DisposeSource(disposeNow);
    }

    private IBeatmapSource Acquire()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            activeReads.TryGetValue(current, out int count);
            activeReads[current] = count + 1;
            return current;
        }
    }

    private void Release(IBeatmapSource acquired)
    {
        IBeatmapSource? disposeNow = null;
        lock (sync)
        {
            int remaining = activeReads[acquired] - 1;
            if (remaining == 0)
            {
                activeReads.Remove(acquired);
                if (retired.Remove(acquired)) disposeNow = acquired;
            }
            else activeReads[acquired] = remaining;
        }
        DisposeSource(disposeNow);
    }

    private static void DisposeSource(IBeatmapSource? source)
    {
        if (source is not IDisposable disposable) return;
        try { disposable.Dispose(); }
        catch { }
    }
}
