using System.Diagnostics;
using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Integration.Lazer;
using OsuMemoryDataProvider;
using ProcessMemoryDataFinder;

namespace HRandomPlus.Desktop.Platform;

internal static partial class PlatformSourceFactory
{
    public static partial IBeatmapSource Create(AppSettings settings) => new ArbitratingBeatmapSource(
        new WindowsMemoryBeatmapSource(settings), new LazerCurrentBeatmapSource());
}

internal sealed class WindowsMemoryBeatmapSource : IBeatmapSource, IDisposable
{
    private readonly AppSettings settings;
    private StructuredOsuMemoryReader? reader;
    private int? stableProcessId;
    private DateTimeOffset? stableProcessStartTime;
    private DateTimeOffset readerCreatedAt;
    private string? processSelectionMessage;

    public WindowsMemoryBeatmapSource(AppSettings settings)
    {
        this.settings = settings;
    }

    public Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.Run(ReadCurrent, cancellationToken);

    private BeatmapSourceResult ReadCurrent()
    {
        using Process? process = FindStableProcess();
        if (process is null)
        {
            ResetReader();
            return BeatmapSourceResult.Unavailable(processSelectionMessage ?? "osu!stable not detected");
        }
        try
        {
            EnsureReader(process.Id, process.StartTime);
            if (reader is null || !reader.CanRead)
            {
                if (DateTimeOffset.UtcNow - readerCreatedAt >= TimeSpan.FromSeconds(2))
                    RecreateReader(process.Id, process.StartTime);
                return BeatmapSourceResult.Waiting("osu!stable detected; connecting to its memory reader");
            }
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
            return BeatmapSourceResult.Found(
                new BeatmapSelection(info, path),
                string.Empty,
                detectionSource: BeatmapDetectionSource.WindowsMemory);
        }
        catch (Exception ex) { return BeatmapSourceResult.Unavailable($"Memory detection unavailable: {ex.Message}"); }
    }

    private void EnsureReader(int processId, DateTimeOffset processStartTime)
    {
        if (reader is not null && stableProcessId == processId && stableProcessStartTime == processStartTime) return;
        RecreateReader(processId, processStartTime);
    }

    private void RecreateReader(int processId, DateTimeOffset processStartTime)
    {
        ResetReader();
        reader = new StructuredOsuMemoryReader(new ProcessTargetOptions("osu!", null!, false));
        reader.ProcessWatcherDelayMs = 250;
        stableProcessId = processId;
        stableProcessStartTime = processStartTime;
        readerCreatedAt = DateTimeOffset.UtcNow;
    }

    private void ResetReader()
    {
        reader?.Dispose();
        reader = null;
        stableProcessId = null;
        stableProcessStartTime = null;
        readerCreatedAt = default;
    }

    private Process? FindStableProcess()
    {
        var candidates = new List<(Process Process, string Directory, DateTimeOffset StartTime)>();
        foreach (Process process in Process.GetProcessesByName("osu!"))
        {
            bool retained = false;
            try
            {
                string? directory = Path.GetDirectoryName(process.MainModule?.FileName);
                if (directory is not null && Directory.Exists(Path.Combine(directory, "Songs")))
                {
                    candidates.Add((process, Path.GetFullPath(directory), process.StartTime));
                    retained = true;
                }
            }
            catch { }
            finally
            {
                if (!retained) process.Dispose();
            }
        }
        Process? selected = null;
        try
        {
            StableProcessSelection choice = StableProcessSelector.Select(
                candidates.Select(candidate => new StableProcessCandidate(candidate.Process.Id,
                    candidate.Directory, candidate.StartTime)), settings.OsuPath, stableProcessId, stableProcessStartTime);
            processSelectionMessage = choice.Status == StableProcessSelectionStatus.Ambiguous ? choice.Message : null;
            if (choice.Candidate is not null)
                selected = candidates.First(candidate => candidate.Process.Id == choice.Candidate.ProcessId).Process;
            return selected;
        }
        finally
        {
            foreach ((Process process, _, _) in candidates)
                if (!ReferenceEquals(process, selected)) process.Dispose();
        }
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

    public void Dispose() => ResetReader();
}
