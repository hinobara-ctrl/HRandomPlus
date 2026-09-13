using System.Diagnostics;
using HRandomPlus.Integration.Beatmaps;

namespace HRandomPlus.Integration.Lazer;

public interface ILazerProcessDetector
{
    string? FindExecutablePath();
}

public interface ILazerResolutionInvalidator
{
    void InvalidateLazerResolution();
}

public sealed class LazerProcessDetector : ILazerProcessDetector
{
    public string? FindExecutablePath()
    {
        var executables = new List<string>();
        foreach (string name in new[] { "osu!", "osu" })
        foreach (Process process in Process.GetProcessesByName(name))
        {
            try
            {
                string? executable = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(executable)) continue;
                string? directory = Path.GetDirectoryName(executable);
                if (directory is not null && Directory.Exists(Path.Combine(directory, "Songs"))) continue;
                executables.Add(Path.GetFullPath(executable));
            }
            catch { }
            finally { process.Dispose(); }
        }
        return SelectExecutablePath(executables);
    }

    public static string? SelectExecutablePath(IEnumerable<string> executables)
        => executables.Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .OrderBy(path => path, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .FirstOrDefault();
}

public sealed class LazerCurrentBeatmapSource : IBeatmapSource, ILazerResolutionInvalidator
{
    private readonly ILazerStorageDiscovery discovery;
    private readonly ILazerProcessDetector processDetector;
    private readonly ILazerRuntimeLogMonitor monitor;
    private readonly ILazerBeatmapResolver resolver;
    private LazerStorage? storage;
    private DateTimeOffset nextDiscovery;
    private Guid? lastGuid;
    private string? lastDisplay;
    private DateTimeOffset? lastObservedAt;
    private BeatmapSourceResult? cached;
    private bool processWasAvailable;
    private string? activeExecutable;
    private bool storageAmbiguous;

    public LazerCurrentBeatmapSource(ILazerStorageDiscovery? discovery = null,
        ILazerProcessDetector? processDetector = null, ILazerRuntimeLogMonitor? monitor = null,
        ILazerBeatmapResolver? resolver = null)
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
        {
            if (processWasAvailable) ResetSession();
            processWasAvailable = false;
            return BeatmapSourceResult.Unavailable("osu!lazer not detected");
        }
        processWasAvailable = true;

        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(activeExecutable, executable, pathComparison))
        {
            ResetSession();
            processWasAvailable = true;
            activeExecutable = executable;
        }

        if (storage is null || DateTimeOffset.UtcNow >= nextDiscovery)
        {
            IReadOnlyList<LazerStorage> candidates = discovery.Discover(PortableStorageRoots(executable));
            LazerStorage? discovered = LazerStorageSelector.Select(
                candidates, executable);
            storageAmbiguous = discovered is null && candidates.Count > 1;
            if (discovered?.RootPath != storage?.RootPath)
            {
                storage = discovered;
                monitor.Reset();
                cached = null;
                lastGuid = null;
                lastDisplay = null;
                lastObservedAt = null;
            }
            nextDiscovery = DateTimeOffset.UtcNow.AddSeconds(5);
        }
        if (storage is null)
            return BeatmapSourceResult.Waiting(storageAmbiguous
                    ? "osu!lazer detected, but multiple storages could not be associated safely with its executable"
                    : "osu!lazer detected, but its storage could not be found",
                BeatmapDetectionSource.Lazer);

        try
        {
            LazerLogSelection? logSelection = monitor.ReadCurrent(storage);
            if (logSelection is null)
                return BeatmapSourceResult.Waiting("osu!lazer detected; open Song Select",
                    BeatmapDetectionSource.Lazer);
            if (cached is not null && logSelection.BeatmapId == lastGuid &&
                logSelection.DisplayName == lastDisplay && logSelection.ObservedAt == lastObservedAt)
                return cached;

            LazerResolution resolution = resolver.Resolve(storage, logSelection, executable);
            lastGuid = logSelection.BeatmapId;
            lastDisplay = logSelection.DisplayName;
            lastObservedAt = logSelection.ObservedAt;
            return cached = BeatmapSourceResult.Found(resolution.Selection, string.Empty,
                detectionSource: BeatmapDetectionSource.Lazer, observedAt: resolution.ObservedAt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or global::Realms.Exceptions.RealmException)
        {
            return BeatmapSourceResult.Waiting($"osu!lazer selection unresolved: {ex.Message}",
                BeatmapDetectionSource.Lazer);
        }
    }

    public void InvalidateLazerResolution()
    {
        cached = null;
        lastGuid = null;
        lastDisplay = null;
        lastObservedAt = null;
    }

    private void ResetSession()
    {
        storage = null;
        activeExecutable = null;
        storageAmbiguous = false;
        nextDiscovery = default;
        monitor.Reset();
        InvalidateLazerResolution();
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

public static class LazerStorageSelector
{
    public static LazerStorage? Select(IReadOnlyList<LazerStorage> candidates, string executable)
    {
        if (candidates.Count == 0) return null;
        string[] runtimeRoots = RuntimeRoots(executable).ToArray();
        LazerStorage[] associated = candidates.Where(candidate =>
            runtimeRoots.Any(root => IsAssociated(root, candidate.RootPath))).ToArray();
        if (associated.Length > 0) return Latest(associated);

        // A single standard/global storage is safe. Multiple unrelated storages are
        // ambiguous, so do not pair a process with whichever log happened to be newest.
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static LazerStorage Latest(IEnumerable<LazerStorage> candidates)
        => candidates.OrderByDescending(candidate =>
            LazerRuntimeLogMonitor.GetLatestRuntimeLogWriteTimeUtc(candidate.LogsPath)).First();

    private static IEnumerable<string> RuntimeRoots(string executable)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(executable));
        if (directory is null) yield break;
        yield return directory;
        string? parent = Directory.GetParent(directory)?.FullName;
        if (parent is not null) yield return parent;
    }

    private static bool IsAssociated(string runtimeRoot, string storageRoot)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string storage = Path.GetFullPath(storageRoot);
        if (Path.GetFullPath(runtimeRoot).Equals(storage, comparison)) return true;

        string storageIni = Path.Combine(runtimeRoot, "storage.ini");
        if (!File.Exists(storageIni)) return false;
        try
        {
            string? configured = File.ReadLines(storageIni)
                .Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2 && parts[0].Trim().Equals("FullPath", StringComparison.OrdinalIgnoreCase))
                .Select(parts => parts[1].Trim())
                .FirstOrDefault(value => value.Length > 0);
            return configured is not null && Path.GetFullPath(configured).Equals(storage, comparison);
        }
        catch
        {
            return false;
        }
    }
}
