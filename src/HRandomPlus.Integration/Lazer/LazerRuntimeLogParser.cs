namespace HRandomPlus.Integration.Lazer;

public sealed record LazerLogSelection(Guid? BeatmapId, string? Ruleset, string? DisplayName, DateTimeOffset ObservedAt);

public static class LazerRuntimeLogParser
{
    private const string guid_marker = "Song select updating selection with beatmap:";
    private const string ruleset_marker = " ruleset:";
    private const string working_marker = "Game-wide working beatmap updated to ";

    public static LazerLogSelection? ParseLine(string? line, DateTimeOffset? observedAt = null)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        DateTimeOffset timestamp = observedAt ?? DateTimeOffset.UtcNow;

        int marker = line.IndexOf(guid_marker, StringComparison.Ordinal);
        if (marker >= 0)
        {
            string payload = line[(marker + guid_marker.Length)..];
            int rulesetAt = payload.IndexOf(ruleset_marker, StringComparison.Ordinal);
            string idText = rulesetAt >= 0 ? payload[..rulesetAt] : payload;
            if (!Guid.TryParse(idText.Trim(), out Guid id)) return null;
            string? ruleset = rulesetAt >= 0 ? payload[(rulesetAt + ruleset_marker.Length)..].Trim() : null;
            return new LazerLogSelection(id, string.IsNullOrWhiteSpace(ruleset) ? null : ruleset, null, timestamp);
        }

        marker = line.IndexOf(working_marker, StringComparison.Ordinal);
        if (marker < 0) return null;
        string display = line[(marker + working_marker.Length)..].Trim();
        return display.Length == 0 ? null : new LazerLogSelection(null, null, display, timestamp);
    }

    public static LazerLogSelection? LastValid(IEnumerable<string> lines, DateTimeOffset? observedAt = null)
        => lines.Select(line => ParseLine(line, observedAt)).LastOrDefault(selection => selection is not null);
}
