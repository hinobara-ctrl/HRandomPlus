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
    private long generation;
    private bool disposed;

    public ReplaceableBeatmapSource(IBeatmapSource initial)
        => current = initial ?? throw new ArgumentNullException(nameof(initial));

    public async Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        return (await GetCurrentSnapshotAsync(cancellationToken).ConfigureAwait(false)).Result;
    }

    public async Task<BeatmapSourceSnapshot> GetCurrentSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceLease lease = Acquire();
            BeatmapSourceResult? result = null;
            Exception? failure = null;
            bool currentAfterRead;
            try
            {
                result = await lease.Source.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                currentAfterRead = IsCurrent(lease);
                Release(lease.Source);
            }

            if (!currentAfterRead)
                continue;
            if (failure is not null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return new BeatmapSourceSnapshot(result!, lease.Generation);
        }
    }

    public bool IsCurrent(long snapshotGeneration)
    {
        lock (sync) return !disposed && generation == snapshotGeneration;
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
            generation++;
            if (activeReads.ContainsKey(previous)) retired.Add(previous);
            else disposeNow = previous;
        }
        DisposeSource(disposeNow);
    }

    public void InvalidateLazerResolution()
    {
        SourceLease lease = Acquire();
        try
        {
            if (lease.Source is ILazerResolutionInvalidator invalidator)
                invalidator.InvalidateLazerResolution();
        }
        finally
        {
            Release(lease.Source);
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

    private SourceLease Acquire()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            activeReads.TryGetValue(current, out int count);
            activeReads[current] = count + 1;
            return new SourceLease(current, generation);
        }
    }

    private bool IsCurrent(SourceLease lease)
    {
        lock (sync)
            return !disposed && generation == lease.Generation && ReferenceEquals(current, lease.Source);
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
        disposable.Dispose();
    }

    private sealed record SourceLease(IBeatmapSource Source, long Generation);
}

public sealed record BeatmapSourceSnapshot(BeatmapSourceResult Result, long Generation);
