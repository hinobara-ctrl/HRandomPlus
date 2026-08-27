using System.Text.Json;
using HRandomPlus.Integration.Beatmaps;

namespace HRandomPlus.Integration.Tosu;

public sealed record TosuSnapshot(BeatmapInfo Beatmap, string? SongsFolder)
{
    public static TosuSnapshot Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement beatmap = Object(root, "beatmap");
        JsonElement metadata = Object(beatmap, "metadata");
        JsonElement folders = Object(root, "folders");
        JsonElement files = Object(root, "files");
        JsonElement directPath = Object(root, "directPath");

        string folderName = Text(folders, "beatmap") ?? Text(directPath, "beatmapFolder") ?? string.Empty;
        string fileName = Text(files, "beatmap") ?? FileName(Text(directPath, "beatmapFile")) ?? string.Empty;
        var info = new BeatmapInfo(
            Number(beatmap, "id"),
            Number(beatmap, "set"),
            Text(beatmap, "checksum") ?? Text(beatmap, "md5"),
            Text(metadata, "artist") ?? Text(beatmap, "artist") ?? string.Empty,
            Text(metadata, "title") ?? Text(beatmap, "title") ?? string.Empty,
            Text(metadata, "mapper") ?? Text(metadata, "creator") ?? Text(beatmap, "mapper") ?? string.Empty,
            Text(metadata, "difficulty") ?? Text(metadata, "version") ?? Text(beatmap, "version") ?? string.Empty,
            folderName,
            fileName,
            Text(directPath, "beatmapFile"));
        return new TosuSnapshot(info, Text(folders, "songs"));
    }

    private static JsonElement Object(JsonElement parent, string name)
        => TryProperty(parent, name, out JsonElement value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string? Text(JsonElement parent, string name)
    {
        if (!TryProperty(parent, name, out JsonElement value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int Number(JsonElement parent, string name)
    {
        if (!TryProperty(parent, name, out JsonElement value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)) return number;
        return int.TryParse(value.ToString(), out number) ? number : 0;
    }

    private static bool TryProperty(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        if (parent.ValueKind != JsonValueKind.Object) return false;
        foreach (JsonProperty property in parent.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        return false;
    }

    private static string? FileName(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/').Split('/').LastOrDefault();
}
