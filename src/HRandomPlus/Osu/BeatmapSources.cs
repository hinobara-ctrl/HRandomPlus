using System.Diagnostics;
using HRandomPlus.Beatmaps;
using OsuMemoryDataProvider;
using ProcessMemoryDataFinder;

namespace HRandomPlus.Osu;

public sealed record BeatmapSelection(string Path, string FolderName, string OsuFileName, int Id = 0, int SetId = 0, string? Md5 = null);

public interface IBeatmapSource
{
    bool TryGetCurrent(out BeatmapSelection? selection, out string status);
}

public sealed class ManualBeatmapSource
{
    public BeatmapSelection Select(IWin32Window owner)
    {
        using var dialog = new OpenFileDialog { Filter = "Beatmaps osu!mania (*.osu)|*.osu", CheckFileExists = true };
        if (dialog.ShowDialog(owner) != DialogResult.OK) throw new OperationCanceledException();
        return new BeatmapSelection(dialog.FileName, Path.GetFileName(Path.GetDirectoryName(dialog.FileName)) ?? "", Path.GetFileName(dialog.FileName));
    }
}

public sealed class OsuMemoryBeatmapSource : IBeatmapSource, IDisposable
{
    // osu!stable is a 32-bit process. Explicitly filtering by architecture prevents the
    // provider from attaching to osu!lazer when both executables are named "osu!".
    private readonly StructuredOsuMemoryReader reader = StructuredOsuMemoryReader.GetInstance(
        new ProcessTargetOptions("osu!", null!, false));
    private string? configuredOsuPath;

    public OsuMemoryBeatmapSource(string? osuPath)
    {
        configuredOsuPath = osuPath;
        reader.ProcessWatcherDelayMs = 250;
    }
    public void SetOsuPath(string? path) => configuredOsuPath = path;

    public bool TryGetCurrent(out BeatmapSelection? selection, out string status)
    {
        selection = null;
        Process? process = FindStableProcess();
        if (process is null) { status = "osu! not detected"; return false; }
        try
        {
            if (!reader.CanRead)
            {
                status = "osu!stable detected, but its memory is not accessible. Run HRandomPlus with the same permissions as osu!";
                return false;
            }
            if (!reader.TryRead(reader.OsuMemoryAddresses.Beatmap)) { status = "Could not read current beatmap"; return false; }
            var beatmap = reader.OsuMemoryAddresses.Beatmap;
            string folderName = beatmap.FolderName ?? string.Empty;
            string osuFileName = beatmap.OsuFileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(osuFileName))
            { status = "Current beatmap could not be resolved"; return false; }

            string? root = ResolveOsuPath(process);
            if (root is null) { status = "Configure the osu! folder in Settings"; return false; }
            string path = Path.Combine(root, "Songs", folderName.TrimEnd(), osuFileName);
            if (!File.Exists(path)) { status = "Detected beatmap file does not exist"; return false; }
            selection = new BeatmapSelection(path, folderName, osuFileName, beatmap.Id, beatmap.SetId, beatmap.Md5);
            status = "Beatmap detected";
            return true;
        }
        catch (Exception ex) { status = $"Memory detection unavailable: {ex.Message}"; return false; }
    }

    private Process? FindStableProcess()
    {
        var candidates = new List<(Process Process, string Directory)>();
        foreach (Process process in Process.GetProcessesByName("osu!"))
        {
            try
            {
                string? directory = Path.GetDirectoryName(process.MainModule?.FileName);
                if (directory is null || !Directory.Exists(Path.Combine(directory, "Songs")))
                    continue;
                candidates.Add((process, Path.GetFullPath(directory)));
            }
            catch { }
        }

        if (!string.IsNullOrWhiteSpace(configuredOsuPath))
        {
            string configured = Path.GetFullPath(configuredOsuPath);
            Process? exact = candidates.FirstOrDefault(c => c.Directory.Equals(configured, StringComparison.OrdinalIgnoreCase)).Process;
            if (exact is not null) return exact;
        }
        return candidates.OrderByDescending(c => c.Process.StartTime).Select(c => c.Process).FirstOrDefault();
    }


    private string? ResolveOsuPath(Process process)
    {
        if (!string.IsNullOrWhiteSpace(configuredOsuPath) && Directory.Exists(Path.Combine(configuredOsuPath, "Songs")))
            return configuredOsuPath;
        try
        {
            string? path = Path.GetDirectoryName(process.MainModule?.FileName);
            if (path is not null && Directory.Exists(Path.Combine(path, "Songs"))) return configuredOsuPath = path;
        }
        catch { }
        return null;
    }

    public void Dispose() { /* singleton is shared by the provider */ }
}
