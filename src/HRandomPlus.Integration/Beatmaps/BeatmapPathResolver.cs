using HRandomPlus.Integration.Linux;

namespace HRandomPlus.Integration.Beatmaps;

public sealed record PathResolution(string? Path, string Status)
{
    public bool Success => Path is not null;
}

public sealed class BeatmapPathResolver
{
    private readonly WinelloLocator winelloLocator;

    public BeatmapPathResolver(WinelloLocator? winelloLocator = null)
        => this.winelloLocator = winelloLocator ?? new WinelloLocator();

    public PathResolution Resolve(BeatmapInfo beatmap, string? configuredOsuRoot = null)
    {
        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(beatmap.DirectBeatmapPath) && File.Exists(beatmap.DirectBeatmapPath))
            return new PathResolution(Path.GetFullPath(beatmap.DirectBeatmapPath), "Ruta directa de tosu");

        string? osuRoot = configuredOsuRoot;
        string rootStatus = "Ruta configurada manualmente";
        if (string.IsNullOrWhiteSpace(osuRoot) && !winelloLocator.TryLocate(out osuRoot, out rootStatus))
            return new PathResolution(null, rootStatus);
        if (string.IsNullOrWhiteSpace(osuRoot))
            return new PathResolution(null, "Configura la ruta nativa de osu!stable");

        string fullRoot;
        try { fullRoot = Path.GetFullPath(osuRoot); }
        catch (Exception ex) { return new PathResolution(null, $"Ruta de osu! no válida: {ex.Message}"); }

        string songsRoot = Directory.Exists(Path.Combine(fullRoot, "Songs"))
            ? Path.Combine(fullRoot, "Songs")
            : fullRoot;
        if (!Directory.Exists(songsRoot))
            return new PathResolution(null, $"No existe la carpeta Songs: {songsRoot}");
        if (string.IsNullOrWhiteSpace(beatmap.FolderName) || string.IsNullOrWhiteSpace(beatmap.OsuFileName))
            return new PathResolution(null, "tosu no informó la carpeta o el archivo del beatmap");

        try
        {
            string candidate = Path.GetFullPath(Path.Combine(songsRoot, beatmap.FolderName, beatmap.OsuFileName));
            string safeRoot = Path.GetFullPath(songsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!candidate.StartsWith(safeRoot, comparison))
                return new PathResolution(null, "tosu informó una ruta fuera de la carpeta Songs");
            if (!File.Exists(candidate))
                return new PathResolution(null, $"El beatmap detectado no existe en la ruta nativa: {candidate}");
            return new PathResolution(candidate, rootStatus);
        }
        catch (Exception ex)
        {
            return new PathResolution(null, $"No se pudo resolver el beatmap: {ex.Message}");
        }
    }
}
