using System.Diagnostics;
using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;
using OsuMemoryDataProvider;
using ProcessMemoryDataFinder;

namespace HRandomPlus.Desktop.Platform;

internal static partial class PlatformSourceFactory
{
    public static partial IBeatmapSource Create(AppSettings settings) => new WindowsMemoryBeatmapSource(settings);
}

internal sealed class WindowsMemoryBeatmapSource : IBeatmapSource
{
    private readonly AppSettings settings;
    private readonly StructuredOsuMemoryReader reader = StructuredOsuMemoryReader.GetInstance(
        new ProcessTargetOptions("osu!", null!, false));

    public WindowsMemoryBeatmapSource(AppSettings settings)
    {
        this.settings = settings;
        reader.ProcessWatcherDelayMs = 250;
    }

    public Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.Run(ReadCurrent, cancellationToken);

    private BeatmapSourceResult ReadCurrent()
    {
        Process? process = FindStableProcess();
        if (process is null) return BeatmapSourceResult.Unavailable("osu!stable not detected");
        try
        {
            if (!reader.CanRead)
                return BeatmapSourceResult.Unavailable("osu!stable detected, but its memory is not accessible");
            if (!reader.TryRead(reader.OsuMemoryAddresses.Beatmap))
                return BeatmapSourceResult.Waiting("Could not read current beatmap");
            var beatmap = reader.OsuMemoryAddresses.Beatmap;
            string folder = beatmap.FolderName ?? string.Empty;
            string file = beatmap.OsuFileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(file))
                return BeatmapSourceResult.Waiting("Current beatmap could not be resolved");
            string? root = ResolveOsuPath(process);
            if (root is null) return BeatmapSourceResult.Waiting("Configure the osu!stable folder in Settings");
            string path = Path.Combine(root, "Songs", folder.TrimEnd(), file);
            if (!File.Exists(path)) return BeatmapSourceResult.Waiting("Detected beatmap file does not exist");
            var info = new BeatmapInfo(beatmap.Id, beatmap.SetId, beatmap.Md5, "", "", "", "", folder, file, path);
            return BeatmapSourceResult.Found(new BeatmapSelection(info, path));
        }
        catch (Exception ex) { return BeatmapSourceResult.Unavailable($"Memory detection unavailable: {ex.Message}"); }
    }

    private Process? FindStableProcess()
    {
        var candidates = new List<(Process Process, string Directory)>();
        foreach (Process process in Process.GetProcessesByName("osu!"))
        {
            try
            {
                string? directory = Path.GetDirectoryName(process.MainModule?.FileName);
                if (directory is not null && Directory.Exists(Path.Combine(directory, "Songs")))
                    candidates.Add((process, Path.GetFullPath(directory)));
            }
            catch { }
        }
        if (!string.IsNullOrWhiteSpace(settings.OsuPath))
        {
            string configured = Path.GetFullPath(settings.OsuPath);
            Process? exact = candidates.FirstOrDefault(c => c.Directory.Equals(configured, StringComparison.OrdinalIgnoreCase)).Process;
            if (exact is not null) return exact;
        }
        return candidates.OrderByDescending(c => c.Process.StartTime).Select(c => c.Process).FirstOrDefault();
    }

    private string? ResolveOsuPath(Process process)
    {
        if (!string.IsNullOrWhiteSpace(settings.OsuPath) && Directory.Exists(Path.Combine(settings.OsuPath, "Songs")))
            return settings.OsuPath;
        try
        {
            string? path = Path.GetDirectoryName(process.MainModule?.FileName);
            if (path is not null && Directory.Exists(Path.Combine(path, "Songs"))) return settings.OsuPath = path;
        }
        catch { }
        return null;
    }
}
