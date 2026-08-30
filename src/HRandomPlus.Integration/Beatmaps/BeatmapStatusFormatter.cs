namespace HRandomPlus.Integration.Beatmaps;

public static class BeatmapStatusFormatter
{
    public static string Format(BeatmapDetectionUpdate update, bool hasRetainedBeatmap)
    {
        if (update.Result.Selection is not null)
        {
            string source = update.EffectiveOrigin switch
            {
                BeatmapSelectionOrigin.Manual => "Manual beatmap selected",
                _ when update.Result.DetectionSource == BeatmapDetectionSource.WindowsMemory
                    => "Beatmap detected automatically from osu!stable",
                _ when update.Result.DetectionSource == BeatmapDetectionSource.Tosu
                    => "Beatmap detected automatically by tosu",
                _ => "Beatmap detected automatically"
            };
            return AppendDetail(source, update.Result.Status);
        }

        if (!hasRetainedBeatmap) return update.Result.Status;

        string retained = update.EffectiveOrigin switch
        {
            BeatmapSelectionOrigin.Automatic => "last automatically detected beatmap retained",
            BeatmapSelectionOrigin.Manual => "last manually selected beatmap retained",
            _ => "last beatmap retained"
        };
        return $"{update.Result.Status} — {retained}";
    }

    private static string AppendDetail(string source, string detail)
        => string.IsNullOrWhiteSpace(detail) ? source : $"{source} — {detail}";
}
