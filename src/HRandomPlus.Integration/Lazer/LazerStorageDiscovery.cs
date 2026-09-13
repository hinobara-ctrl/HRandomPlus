namespace HRandomPlus.Integration.Lazer;

public sealed record LazerStorage(string RootPath, string RealmPath, string FilesPath, string LogsPath);

public interface ILazerStorageDiscovery
{
    IReadOnlyList<LazerStorage> Discover();

    IReadOnlyList<LazerStorage> Discover(IEnumerable<string> runtimeRoots) => Discover();
}

public sealed class LazerStorageDiscovery : ILazerStorageDiscovery
{
    private readonly Func<string?> appData;
    private readonly Func<string?> userProfile;
    private readonly IReadOnlyList<string> additionalRoots;

    public LazerStorageDiscovery(Func<string?>? appData = null, Func<string?>? userProfile = null,
        IEnumerable<string>? additionalRoots = null)
    {
        this.appData = appData ?? (() => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        this.userProfile = userProfile ?? (() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        this.additionalRoots = additionalRoots?.ToArray() ?? Array.Empty<string>();
    }

    public IReadOnlyList<LazerStorage> Discover() => Discover(Array.Empty<string>());

    public IReadOnlyList<LazerStorage> Discover(IEnumerable<string> runtimeRoots)
    {
        var roots = new List<string>();
        roots.AddRange(additionalRoots);
        roots.AddRange(runtimeRoots);
        string? profile = userProfile();
        string? roaming = appData();
        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(roaming)) roots.Add(Path.Combine(roaming, "osu"));
        if (OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(profile)) roots.Add(Path.Combine(profile, ".local", "share", "osu"));

        var discovered = new List<LazerStorage>();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (string candidate in roots.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            string defaultRoot;
            try { defaultRoot = Path.GetFullPath(candidate); }
            catch { continue; }

            string storageIni = Path.Combine(defaultRoot, "storage.ini");
            if (File.Exists(storageIni))
            {
                try
                {
                    string? configured = File.ReadLines(storageIni)
                        .Select(line => line.Split('=', 2))
                        .Where(parts => parts.Length == 2 && parts[0].Trim().Equals("FullPath", StringComparison.OrdinalIgnoreCase))
                        .Select(parts => parts[1].Trim())
                        .FirstOrDefault(value => value.Length > 0);
                    if (configured is not null && TryAdd(configured)) continue;
                }
                catch { }
            }
            TryAdd(defaultRoot);
        }
        return discovered;

        bool TryAdd(string root)
        {
            try { root = Path.GetFullPath(root); }
            catch { return false; }
            if (!seen.Add(root)) return discovered.Any(storage =>
                storage.RootPath.Equals(root, OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal));
            string realm = Path.Combine(root, "client.realm");
            string files = Path.Combine(root, "files");
            string logs = Path.Combine(root, "logs");
            if (File.Exists(realm) && Directory.Exists(files) && Directory.Exists(logs))
            {
                discovered.Add(new LazerStorage(root, realm, files, logs));
                return true;
            }
            return false;
        }
    }
}
