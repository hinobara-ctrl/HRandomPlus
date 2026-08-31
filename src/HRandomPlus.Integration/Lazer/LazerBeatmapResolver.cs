using System.Security.Cryptography;
using HRandomPlus.Integration.Beatmaps;
using Realms;

namespace HRandomPlus.Integration.Lazer;

public sealed record LazerCatalogBeatmap(
    Guid Id,
    int OnlineId,
    int SetOnlineId,
    string Difficulty,
    string Hash,
    string Artist,
    string ArtistUnicode,
    string Title,
    string TitleUnicode,
    string Creator,
    IReadOnlyList<BeatmapResource> Resources);

public interface ILazerBeatmapCatalog
{
    LazerCatalogBeatmap? FindById(LazerStorage storage, Guid id);
    IReadOnlyList<LazerCatalogBeatmap> FindByDisplayName(LazerStorage storage, string displayName);
}

public sealed class RealmLazerBeatmapCatalog : ILazerBeatmapCatalog
{
    public LazerCatalogBeatmap? FindById(LazerStorage storage, Guid id)
    {
        using Realm realm = Open(storage);
        IRealmObjectBase? record = realm.DynamicApi.Find("Beatmap", (Guid?)id);
        return record is null ? null : Detach(storage, record);
    }

    public IReadOnlyList<LazerCatalogBeatmap> FindByDisplayName(LazerStorage storage, string displayName)
    {
        using Realm realm = Open(storage);
        return realm.DynamicApi.All("Beatmap")
            .AsEnumerable()
            .Where(record => DisplayNames(record).Contains(displayName, StringComparer.Ordinal))
            .Select(record => Detach(storage, record))
            .ToArray();
    }

    private static Realm Open(LazerStorage storage)
    {
        string pipePath = Path.Combine(Path.GetTempPath(), "HRandomPlus", "realm-pipes");
        Directory.CreateDirectory(pipePath);
        var configuration = new RealmConfiguration(storage.RealmPath)
        {
            IsReadOnly = true,
            IsDynamic = true,
            FallbackPipePath = pipePath,
        };
        return Realm.GetInstance(configuration);
    }

    private static LazerCatalogBeatmap Detach(LazerStorage storage, IRealmObjectBase record)
    {
        IRealmObjectBase? metadata = GetObject(record, "Metadata");
        IRealmObjectBase? set = GetObject(record, "BeatmapSet");
        var resources = set?.DynamicApi.GetList<IRealmObjectBase>("Files")
            .Select(file => (Usage: file, Blob: GetObject(file, "File")))
            .Where(pair => pair.Blob is not null && !string.IsNullOrWhiteSpace(GetString(pair.Usage, "Filename")))
            .Select(pair => new BeatmapResource(GetString(pair.Usage, "Filename"),
                LazerBlobPath.GetFullPath(storage.RootPath, GetString(pair.Blob!, "Hash"))))
            .ToArray() ?? Array.Empty<BeatmapResource>();
        IRealmObjectBase? author = metadata is null ? null : GetObject(metadata, "Author");
        return new LazerCatalogBeatmap(record.DynamicApi.Get<Guid>("ID"), GetInt(record, "OnlineID"),
            set is null ? -1 : GetInt(set, "OnlineID"), GetString(record, "DifficultyName"), GetString(record, "Hash"),
            metadata is null ? string.Empty : GetString(metadata, "Artist"),
            metadata is null ? string.Empty : GetString(metadata, "ArtistUnicode"),
            metadata is null ? string.Empty : GetString(metadata, "Title"),
            metadata is null ? string.Empty : GetString(metadata, "TitleUnicode"),
            author is null ? string.Empty : GetString(author, "Username"), resources);
    }

    private static IEnumerable<string> DisplayNames(IRealmObjectBase record)
    {
        IRealmObjectBase? metadata = GetObject(record, "Metadata");
        if (metadata is null) yield break;
        IRealmObjectBase? author = GetObject(metadata, "Author");
        string creator = author is null ? string.Empty : GetString(author, "Username");
        string difficulty = GetString(record, "DifficultyName");
        foreach (string artist in new[] { GetString(metadata, "Artist"), GetString(metadata, "ArtistUnicode") }.Where(value => value.Length > 0).Distinct())
        foreach (string title in new[] { GetString(metadata, "Title"), GetString(metadata, "TitleUnicode") }.Where(value => value.Length > 0).Distinct())
        {
            yield return $"{artist} - {title} ({creator}) [{difficulty}]";
            yield return $"{artist} - {title} ({creator})";
        }
    }

    private static IRealmObjectBase? GetObject(IRealmObjectBase value, string property)
        => value.DynamicApi.Get<IRealmObjectBase>(property);

    private static string GetString(IRealmObjectBase value, string property)
        => value.DynamicApi.Get<string>(property) ?? string.Empty;

    private static int GetInt(IRealmObjectBase value, string property)
        => checked((int)value.DynamicApi.Get<long>(property));
}

public sealed record LazerResolution(BeatmapSelection Selection, DateTimeOffset ObservedAt);

public interface ILazerBeatmapResolver
{
    LazerResolution Resolve(LazerStorage storage, LazerLogSelection logSelection, string? executablePath = null);
}

public sealed class LazerBeatmapResolver : ILazerBeatmapResolver
{
    private readonly ILazerBeatmapCatalog catalog;
    private readonly string materializedRoot;

    public LazerBeatmapResolver(ILazerBeatmapCatalog? catalog = null, string? materializedRoot = null)
    {
        this.catalog = catalog ?? new RealmLazerBeatmapCatalog();
        this.materializedRoot = materializedRoot ?? Path.Combine(Path.GetTempPath(), "HRandomPlus", "lazer-beatmaps");
        CleanupOldMaterializations();
    }

    public LazerResolution Resolve(LazerStorage storage, LazerLogSelection logSelection, string? executablePath = null)
    {
        LazerCatalogBeatmap beatmap;
        if (logSelection.BeatmapId is Guid id)
        {
            beatmap = catalog.FindById(storage, id)
                ?? throw new InvalidDataException($"The lazer beatmap GUID {id} was not found in client.realm.");
        }
        else if (!string.IsNullOrWhiteSpace(logSelection.DisplayName))
        {
            IReadOnlyList<LazerCatalogBeatmap> matches = catalog.FindByDisplayName(storage, logSelection.DisplayName);
            if (matches.Count == 0)
                throw new InvalidDataException("The beatmap named by lazer could not be resolved in client.realm.");
            if (matches.Count > 1)
                throw new InvalidDataException("The textual lazer selection is ambiguous and cannot be resolved automatically.");
            beatmap = matches[0];
        }
        else throw new InvalidDataException("The lazer log entry has no resolvable beatmap identity.");

        BeatmapResource osuResource = beatmap.Resources.SingleOrDefault(resource =>
            resource.LogicalName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(resource.BlobPath).Equals(beatmap.Hash, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("The selected lazer difficulty has no matching .osu file usage.");
        ValidateBlob(osuResource.BlobPath, beatmap.Hash);

        string directory = Path.Combine(materializedRoot, beatmap.Id.ToString("N"));
        Directory.CreateDirectory(directory);
        string filename = SafeLeafName(osuResource.LogicalName, "selected.osu");
        string materialized = Path.Combine(directory, filename);
        File.Copy(osuResource.BlobPath, materialized, overwrite: true);

        var info = new BeatmapInfo(beatmap.OnlineId, beatmap.SetOnlineId, beatmap.Hash,
            beatmap.Artist, beatmap.Title, beatmap.Creator, beatmap.Difficulty,
            string.Empty, filename, materialized);
        var context = new LazerBeatmapSelectionContext(beatmap.Id, storage.RootPath, beatmap.Resources, executablePath);
        return new LazerResolution(new BeatmapSelection(info, materialized, context), logSelection.ObservedAt);
    }

    internal static string SafeLeafName(string logicalName, string fallback)
    {
        string normalized = logicalName.Replace('\\', '/');
        string leaf = Path.GetFileName(normalized);
        return string.IsNullOrWhiteSpace(leaf) ? fallback : leaf;
    }

    private static void ValidateBlob(string path, string expectedHash)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length == 0) throw new InvalidDataException("The selected lazer .osu blob is missing or empty.");
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        string actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The selected lazer .osu blob failed SHA-256 validation.");
    }

    private void CleanupOldMaterializations()
    {
        if (!Directory.Exists(materializedRoot)) return;
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(materializedRoot))
                if (Directory.GetLastWriteTimeUtc(directory) < DateTime.UtcNow.AddDays(-7))
                    Directory.Delete(directory, recursive: true);
        }
        catch { }
    }
}
