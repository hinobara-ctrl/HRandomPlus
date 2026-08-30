using System.Diagnostics;
using HRandomPlus.Integration.Beatmaps;

namespace HRandomPlus.Integration.Lazer;

public interface ILazerProcessDetector
{
    string? FindExecutablePath();
}

public sealed class LazerProcessDetector : ILazerProcessDetector
{
    public string? FindExecutablePath()
    {
        foreach (string name in new[] { "osu!", "osu" })
        foreach (Process process in Process.GetProcessesByName(name))
        {
            try
            {
                string? executable = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(executable)) continue;
                string? directory = Path.GetDirectoryName(executable);
                if (directory is not null && Directory.Exists(Path.Combine(directory, "Songs"))) continue;
                return executable;
            }
            catch { }
            finally { process.Dispose(); }
        }
        return null;
    }
}

public sealed class LazerCurrentBeatmapSource : IBeatmapSource
{
    private readonly ILazerStorageDiscovery discovery;
    private readonly ILazerProcessDetector processDetector;
    private readonly LazerRuntimeLogMonitor monitor;
    private readonly LazerBeatmapResolver resolver;
    private LazerStorage? storage;
    private DateTimeOffset nextDiscovery;
    private Guid? lastGuid;
    private string? lastDisplay;
    private BeatmapSourceResult? cached;

    public LazerCurrentBeatmapSource(ILazerStorageDiscovery? discovery = null,
        ILazerProcessDetector? processDetector = null, LazerRuntimeLogMonitor? monitor = null,
        LazerBeatmapResolver? resolver = null)
    {
        this.discovery = discovery ?? new LazerStorageDiscovery();
        this.processDetector = processDetector ?? new LazerProcessDetector();
        this.monitor = monitor ?? new LazerRuntimeLogMonitor();
        this.resolver = resolver ?? new LazerBeatmapResolver();
    }

    public Task<BeatmapSourceResult> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.Run(ReadCurrent, cancellationToken);

    private BeatmapSourceResult ReadCurrent()
    {
        string? executable = processDetector.FindExecutablePath();
        if (executable is null)
            return BeatmapSourceResult.Unavailable("osu!lazer not detected");

        if (storage is null || DateTimeOffset.UtcNow >= nextDiscovery)
        {
            LazerStorage? discovered = discovery.Discover(PortableStorageRoots(executable)).OrderByDescending(candidate =>
                LazerRuntimeLogMonitor.GetLatestRuntimeLogWriteTimeUtc(candidate.LogsPath)).FirstOrDefault();
            if (discovered?.RootPath != storage?.RootPath)
            {
                storage = discovered;
                monitor.Reset();
                cached = null;
                lastGuid = null;
                lastDisplay = null;
            }
            nextDiscovery = DateTimeOffset.UtcNow.AddSeconds(5);
        }
        if (storage is null)
            return BeatmapSourceResult.Waiting("osu!lazer detected, but its storage could not be found",
                BeatmapDetectionSource.Lazer);

        try
        {
            LazerLogSelection? logSelection = monitor.ReadCurrent(storage);
            if (logSelection is null)
                return BeatmapSourceResult.Waiting("osu!lazer detected; open Song Select",
                    BeatmapDetectionSource.Lazer);
            if (cached is not null && logSelection.BeatmapId == lastGuid && logSelection.DisplayName == lastDisplay)
                return cached;

            LazerResolution resolution = resolver.Resolve(storage, logSelection, executable);
            lastGuid = logSelection.BeatmapId;
            lastDisplay = logSelection.DisplayName;
            return cached = BeatmapSourceResult.Found(resolution.Selection, string.Empty,
                detectionSource: BeatmapDetectionSource.Lazer, observedAt: resolution.ObservedAt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or global::Realms.Exceptions.RealmException)
        {
            return BeatmapSourceResult.Waiting($"osu!lazer selection unresolved: {ex.Message}",
                BeatmapDetectionSource.Lazer);
        }
    }

    private static IEnumerable<string> PortableStorageRoots(string executable)
    {
        string? directory = Path.GetDirectoryName(executable);
        if (directory is null) yield break;
        yield return directory;
        string? parent = Directory.GetParent(directory)?.FullName;
        if (parent is not null) yield return parent;
    }
}
