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
    private readonly StableReaderSession<StructuredOsuMemoryReader> readerSession = new();
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
        using SelectedStableProcess? selected = FindStableProcess();
        if (selected is null)
        {
            ResetReader();
            return BeatmapSourceResult.Unavailable(processSelectionMessage ?? "osu!stable not detected");
        }
        try
        {
            if (!IsSoleReaderTarget(selected.Identity))
            {
                ResetReader();
                return BeatmapSourceResult.Unavailable("osu!stable process identity changed before memory could be read");
            }
            StructuredOsuMemoryReader reader = EnsureReader(selected.Identity);
            if (!reader.CanRead)
            {
                if (DateTimeOffset.UtcNow - readerCreatedAt >= TimeSpan.FromSeconds(2))
                    reader = RecreateReader(selected.Identity);
                return BeatmapSourceResult.Waiting("osu!stable detected; connecting to its memory reader");
            }
            if (!reader.TryRead(reader.OsuMemoryAddresses.Beatmap))
                return BeatmapSourceResult.Waiting("Could not read current beatmap");
            if (!IsSoleReaderTarget(selected.Identity))
            {
                ResetReader();
                return BeatmapSourceResult.Unavailable("osu!stable process identity changed while memory was being read");
            }
            var beatmap = reader.OsuMemoryAddresses.Beatmap;
            string folder = beatmap.FolderName ?? string.Empty;
            string file = beatmap.OsuFileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(file))
                return BeatmapSourceResult.Waiting("Current beatmap could not be resolved");
            settings.OsuPath = selected.Identity.ExecutableDirectory;
            string path = Path.Combine(selected.Identity.SongsRoot, folder.TrimEnd(), file);
            if (!File.Exists(path)) return BeatmapSourceResult.Waiting("Detected beatmap file does not exist");
            var info = new BeatmapInfo(beatmap.Id, beatmap.SetId, beatmap.Md5, "", "", "", "", folder, file, path);
            return BeatmapSourceResult.Found(
                new BeatmapSelection(info, path),
                string.Empty,
                detectionSource: BeatmapDetectionSource.WindowsMemory);
        }
        catch (Exception ex) { return BeatmapSourceResult.Unavailable($"Memory detection unavailable: {ex.Message}"); }
    }

    private StructuredOsuMemoryReader EnsureReader(StableProcessIdentity identity)
    {
        if (readerSession.Reader is not null && readerSession.Identity == identity)
            return readerSession.Reader;
        return RecreateReader(identity);
    }

    private StructuredOsuMemoryReader RecreateReader(StableProcessIdentity identity)
    {
        ResetReader();
        StructuredOsuMemoryReader reader = readerSession.GetOrCreate(identity,
            () => new StructuredOsuMemoryReader(new ProcessTargetOptions("osu!", null!, false)));
        reader.ProcessWatcherDelayMs = 250;
        readerCreatedAt = DateTimeOffset.UtcNow;
        return reader;
    }

    private void ResetReader()
    {
        readerSession.Invalidate();
        readerCreatedAt = default;
    }

    private SelectedStableProcess? FindStableProcess()
    {
        Process[] readerTargets = Process.GetProcessesByName("osu!");
        var candidates = new List<(Process Process, StableProcessIdentity Identity)>();
        foreach (Process process in readerTargets)
        {
            bool retained = false;
            try
            {
                string? directory = Path.GetDirectoryName(process.MainModule?.FileName);
                if (directory is not null && Directory.Exists(Path.Combine(directory, "Songs")))
                {
                    candidates.Add((process, new StableProcessIdentity(process.Id,
                        Path.GetFullPath(directory), process.StartTime)));
                    retained = true;
                }
            }
            catch { }
            finally
            {
                if (!retained) process.Dispose();
            }
        }
        SelectedStableProcess? selected = null;
        try
        {
            StableProcessSelection choice = StableProcessSelector.Select(
                candidates.Select(candidate => candidate.Identity), settings.OsuPath,
                readerSession.Identity?.ProcessId, readerSession.Identity?.StartTime,
                readerCanBindToIdentity: false, readerTargetProcessCount: readerTargets.Length);
            processSelectionMessage = choice.Status == StableProcessSelectionStatus.Ambiguous ? choice.Message : null;
            if (choice.Identity is not null)
            {
                (Process process, StableProcessIdentity identity) = candidates.First(candidate =>
                    candidate.Identity == choice.Identity);
                selected = new SelectedStableProcess(process, identity);
            }
            return selected;
        }
        finally
        {
            foreach ((Process process, _) in candidates)
                if (!ReferenceEquals(process, selected?.Process)) process.Dispose();
        }
    }

    private static bool IsSoleReaderTarget(StableProcessIdentity expected)
    {
        Process[] processes = Process.GetProcessesByName("osu!");
        try
        {
            if (processes.Length != 1) return false;
            Process process = processes[0];
            string? directory = Path.GetDirectoryName(process.MainModule?.FileName);
            return process.Id == expected.ProcessId && process.StartTime == expected.StartTime &&
                   directory is not null && PathEquals(directory, expected.ExecutableDirectory);
        }
        catch { return false; }
        finally { foreach (Process process in processes) process.Dispose(); }
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        ResetReader();
        readerSession.Dispose();
    }

    private sealed record SelectedStableProcess(Process Process, StableProcessIdentity Identity) : IDisposable
    {
        public void Dispose() => Process.Dispose();
    }
}
