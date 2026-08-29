namespace HRandomPlus.Integration.Beatmaps;

public sealed record BeatmapDetectionUpdate(
    BeatmapSourceResult Result,
    bool SelectionChanged,
    bool ConnectivityChanged,
    bool StatusChanged,
    bool OriginChanged,
    BeatmapSelectionOrigin? EffectiveOrigin)
{
    public bool ShouldUpdateUi => SelectionChanged || ConnectivityChanged || StatusChanged || OriginChanged;
}

public sealed class DetectionStateTracker
{
    private bool initialized;
    private string? lastIdentity;
    private bool lastAvailable;
    private string? lastStatus;
    private BeatmapSelectionOrigin? lastOrigin;
    private bool manualOverrideActive;
    private string? manualAutomaticIdentity;

    public BeatmapDetectionUpdate Observe(BeatmapSourceResult result)
    {
        string? identity = result.Selection?.Beatmap.Identity;
        bool selectionChanged = identity is not null && identity != lastIdentity;
        bool connectivityChanged = !initialized || result.IsAvailable != lastAvailable;
        bool statusChanged = !initialized || result.Status != lastStatus;
        BeatmapSelectionOrigin? observedOrigin = result.Selection is null
            ? lastOrigin
            : result.SelectionOrigin ?? BeatmapSelectionOrigin.Automatic;

        if (identity is not null && manualOverrideActive)
        {
            manualAutomaticIdentity ??= identity;
            if (identity == manualAutomaticIdentity)
            {
                selectionChanged = false;
                observedOrigin = BeatmapSelectionOrigin.Manual;
            }
            else
            {
                manualOverrideActive = false;
                manualAutomaticIdentity = null;
            }
        }

        bool originChanged = result.Selection is not null && observedOrigin != lastOrigin;

        if (identity is not null)
        {
            lastIdentity = identity;
            lastOrigin = observedOrigin;
        }
        lastAvailable = result.IsAvailable;
        lastStatus = result.Status;
        initialized = true;

        return new BeatmapDetectionUpdate(
            result,
            selectionChanged,
            connectivityChanged,
            statusChanged,
            originChanged,
            lastOrigin);
    }

    public void MarkManualSelection()
    {
        lastOrigin = BeatmapSelectionOrigin.Manual;
        manualOverrideActive = true;
        manualAutomaticIdentity = lastIdentity;
    }

    public void Reset()
    {
        initialized = false;
        lastIdentity = null;
        lastAvailable = false;
        lastStatus = null;
        lastOrigin = null;
        manualOverrideActive = false;
        manualAutomaticIdentity = null;
    }
}
