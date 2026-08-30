namespace HRandomPlus.Integration.Beatmaps;

public sealed record BeatmapInfo(
    int Id,
    int SetId,
    string? Checksum,
    string Artist,
    string Title,
    string Creator,
    string Difficulty,
    string FolderName,
    string OsuFileName,
    string? DirectBeatmapPath)
{
    public string Identity => !string.IsNullOrWhiteSpace(Checksum)
        ? Checksum
        : $"{SetId}:{Id}:{FolderName}:{OsuFileName}";
}

public sealed record BeatmapSelection(
    BeatmapInfo Beatmap,
    string NativePath,
    LazerBeatmapSelectionContext? LazerContext = null);

public sealed record BeatmapResource(string LogicalName, string BlobPath);

public sealed record LazerBeatmapSelectionContext(
    Guid BeatmapId,
    string StorageRoot,
    IReadOnlyList<BeatmapResource> SetResources,
    string? LazerExecutablePath);

public enum BeatmapSelectionOrigin
{
    Automatic,
    Manual
}

public enum BeatmapDetectionSource
{
    WindowsMemory,
    Tosu,
    Lazer
}

public sealed record BeatmapSourceResult(
    BeatmapSelection? Selection,
    string Status,
    bool IsAvailable,
    BeatmapSelectionOrigin? SelectionOrigin = null,
    BeatmapDetectionSource? DetectionSource = null,
    DateTimeOffset? ObservedAt = null)
{
    public bool Success => Selection is not null;

    public static BeatmapSourceResult Found(
        BeatmapSelection selection,
        string status = "Beatmap detected",
        BeatmapSelectionOrigin origin = BeatmapSelectionOrigin.Automatic,
        BeatmapDetectionSource? detectionSource = null,
        DateTimeOffset? observedAt = null)
        => new(selection, status, true, origin, detectionSource, observedAt);

    public static BeatmapSourceResult Unavailable(string status, BeatmapDetectionSource? detectionSource = null)
        => new(null, status, false, DetectionSource: detectionSource);

    public static BeatmapSourceResult Waiting(string status, BeatmapDetectionSource? detectionSource = null)
        => new(null, status, true, DetectionSource: detectionSource);
}

public interface IBeatmapSource
{
    Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default);
}
