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

public sealed record BeatmapSelection(BeatmapInfo Beatmap, string NativePath);

public enum BeatmapSelectionOrigin
{
    Automatic,
    Manual
}

public enum BeatmapDetectionSource
{
    WindowsMemory,
    Tosu
}

public sealed record BeatmapSourceResult(
    BeatmapSelection? Selection,
    string Status,
    bool IsAvailable,
    BeatmapSelectionOrigin? SelectionOrigin = null,
    BeatmapDetectionSource? DetectionSource = null)
{
    public bool Success => Selection is not null;

    public static BeatmapSourceResult Found(
        BeatmapSelection selection,
        string status = "Beatmap detected",
        BeatmapSelectionOrigin origin = BeatmapSelectionOrigin.Automatic,
        BeatmapDetectionSource? detectionSource = null)
        => new(selection, status, true, origin, detectionSource);

    public static BeatmapSourceResult Unavailable(string status)
        => new(null, status, false);

    public static BeatmapSourceResult Waiting(string status)
        => new(null, status, true);
}

public interface IBeatmapSource
{
    Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default);
}
