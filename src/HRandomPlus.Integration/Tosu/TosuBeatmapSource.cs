using HRandomPlus.Integration.Beatmaps;

namespace HRandomPlus.Integration.Tosu;

public sealed class TosuBeatmapSource : IBeatmapSource, IDisposable
{
    private readonly TosuClient client;
    private readonly BeatmapPathResolver resolver;
    private readonly Func<string?> configuredOsuRoot;

    public TosuBeatmapSource(TosuClient client, BeatmapPathResolver resolver, Func<string?>? configuredOsuRoot = null)
    {
        this.client = client;
        this.resolver = resolver;
        this.configuredOsuRoot = configuredOsuRoot ?? (() => null);
    }

    public async Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        TosuResult result = await client.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (result.Snapshot is null)
            return result.IsAvailable
                ? BeatmapSourceResult.Waiting(result.Status)
                : BeatmapSourceResult.Unavailable(result.Status);

        PathResolution path = resolver.Resolve(result.Snapshot.Beatmap, configuredOsuRoot());
        if (!path.Success)
            return BeatmapSourceResult.Waiting(path.Status);
        return BeatmapSourceResult.Found(
            new BeatmapSelection(result.Snapshot.Beatmap, path.Path!),
            path.Status,
            detectionSource: BeatmapDetectionSource.Tosu);
    }

    public void Dispose() => client.Dispose();
}
