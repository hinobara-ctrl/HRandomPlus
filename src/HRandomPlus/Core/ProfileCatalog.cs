using System.Text.Json;

namespace HRandomPlus.Core;

public sealed class RandomProfile
{
    public string Name { get; set; } = "Custom";
    public bool BuiltIn { get; set; }
    public HRandomConfig Config { get; set; } = new();
}

public static class ProfileCatalog
{
    public static IReadOnlyList<RandomProfile> BuiltIns => new[]
    {
        new RandomProfile { Name = "H-Random", BuiltIn = true, Config = new HRandomConfig { DifficultySuffix = " H-RANDOM+" } },
        new RandomProfile { Name = "S-Random", BuiltIn = true, Config = SRandom() },
        new RandomProfile { Name = "Custom", BuiltIn = true, Config = new HRandomConfig { DifficultySuffix = " CUSTOM" } }
    };

    private static HRandomConfig SRandom() => new()
    {
        DynamicThreshold = false,
        MinThresholdMs = 0,
        BaseThresholdMs = 0,
        MaxThresholdMs = 0,
        WeightedTopCandidates = 4096,
        WeightedTemperature = 1,
        MaxCandidateSets = 4096,
        DifficultySuffix = " S-RANDOM",
        Weights = new ScoringWeights
        {
            TimeSinceLastUseBonus = 0, HandBalanceBonus = 0, DistributionBonus = 0,
            JackPenalty = 0, TrillPenalty = 0, RepeatedPatternPenalty = 0,
            SameHandPenalty = 0, ExtremeJumpPenalty = 0, RecentUsagePenalty = 0
        }
    };
}

public sealed class AppSettings
{
    public string? OsuPath { get; set; }
    public string TosuHost { get; set; } = "127.0.0.1";
    public int TosuPort { get; set; } = 24050;
    public string? LinuxOsuPath { get; set; }
    public string? LastManualDirectory { get; set; }
    public bool OutputToBeatmapFolder { get; set; } = true;
    public string LastProfile { get; set; } = "H-Random";
    public bool WholeMap { get; set; } = true;
    public List<RandomProfile> CustomProfiles { get; set; } = new();
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string DirectoryPath { get; }
    public string SettingsPath => Path.Combine(DirectoryPath, "config.json");
    public string LogPath => Path.Combine(DirectoryPath, "logs", "latest.log");

    public SettingsStore(string? directory = null)
    {
        DirectoryPath = directory ?? AppPaths.ConfigDirectory;
    }

    public AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            if (!File.Exists(SettingsPath))
            {
                var defaults = new AppSettings();
                Save(defaults);
                return defaults;
            }
            try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Options) ?? new AppSettings(); }
            catch (Exception ex)
            {
                Log($"Configuración corrupta; se restauraron defaults: {ex.Message}");
                var defaults = new AppSettings();
                Save(defaults);
                return defaults;
            }
        }
        catch (Exception ex)
        {
            // Settings are optional. A locked or read-only LocalAppData directory must
            // never prevent the UI from starting.
            Log($"No se pudo cargar la configuración; se usarán defaults en memoria: {ex.Message}");
            return new AppSettings();
        }
    }

    public AppSettings LoadReadOnly()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Options) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Options));
    }

    public void Log(string message)
    {
        try
        {
            string logPath = DirectoryPath == AppPaths.ConfigDirectory
                ? Path.Combine(AppPaths.StateDirectory, "logs", "latest.log")
                : LogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging is best-effort and must never crash the UI or the polling loop.
        }
    }
}
