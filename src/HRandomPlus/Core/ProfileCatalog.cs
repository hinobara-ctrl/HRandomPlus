using System.Text;
using System.Text.Json;

namespace HRandomPlus.Core;

public sealed class RandomProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Custom";
    public string Description { get; set; } = string.Empty;
    public bool BuiltIn { get; set; }
    public HRandomConfig Config { get; set; } = new();

    public RandomProfile Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        BuiltIn = BuiltIn,
        Config = Config.Clone()
    };
}

public static class ProfileCatalog
{
    public const string HRandomName = "H-Random";
    public const string SRandomName = "S-Random";
    public const string CustomName = "Custom";

    public static readonly Guid HRandomId = new("b20e6483-6bbd-47f7-886d-8ba494c83f31");
    public static readonly Guid SRandomId = new("0df02e16-c6e0-4210-a6b2-707ccfcd4f88");
    public static readonly Guid DefaultCustomId = new("473f32c5-29d6-4551-bf64-16ceecb40cc2");

    public static IReadOnlyList<RandomProfile> BuiltIns => CreateBuiltIns();

    public static IReadOnlyList<RandomProfile> CreateBuiltIns(HRandomConfig? customConfig = null, Guid customId = default) => new[]
    {
        new RandomProfile { Id = HRandomId, Name = HRandomName, BuiltIn = true, Config = new HRandomConfig { DifficultySuffix = " H-RANDOM+" } },
        new RandomProfile { Id = SRandomId, Name = SRandomName, BuiltIn = true, Config = SRandom() },
        new RandomProfile { Id = customId == Guid.Empty ? DefaultCustomId : customId, Name = CustomName, BuiltIn = true, Config = (customConfig ?? DefaultCustom()).Clone() }
    };

    public static HRandomConfig DefaultCustom() => new() { DifficultySuffix = " CUSTOM" };

    public static bool IsReservedName(string? name)
    {
        string normalized = name?.Trim() ?? string.Empty;
        return normalized.Equals(HRandomName, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(SRandomName, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(CustomName, StringComparison.OrdinalIgnoreCase);
    }

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
    public Guid CustomProfileId { get; set; }
    public HRandomConfig? CustomConfig { get; set; }
    public List<RandomProfile> CustomProfiles { get; set; } = new();
}

public static class ProfileSettingsMigration
{
    public static bool Apply(AppSettings settings)
    {
        bool changed = false;
        settings.CustomProfiles ??= new List<RandomProfile>();

        List<RandomProfile> historicalCustom = settings.CustomProfiles
            .Where(profile => profile.Name?.Trim().Equals(ProfileCatalog.CustomName, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (settings.CustomConfig is null)
        {
            if (historicalCustom.Count > 0)
            {
                RandomProfile selected = historicalCustom[^1];
                settings.CustomConfig = (selected.Config ?? ProfileCatalog.DefaultCustom()).Clone();
                if (settings.CustomProfileId == Guid.Empty)
                    settings.CustomProfileId = selected.Id == Guid.Empty || selected.Id == ProfileCatalog.HRandomId || selected.Id == ProfileCatalog.SRandomId
                        ? Guid.NewGuid()
                        : selected.Id;
                settings.CustomProfiles.Remove(selected);
            }
            else
            {
                settings.CustomConfig = ProfileCatalog.DefaultCustom();
            }
            changed = true;
        }

        if (settings.CustomProfileId == Guid.Empty)
        {
            settings.CustomProfileId = Guid.NewGuid();
            changed = true;
        }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ProfileCatalog.HRandomName,
            ProfileCatalog.SRandomName,
            ProfileCatalog.CustomName
        };
        var usedIds = new HashSet<Guid>
        {
            ProfileCatalog.HRandomId,
            ProfileCatalog.SRandomId,
            settings.CustomProfileId
        };

        foreach (RandomProfile profile in settings.CustomProfiles)
        {
            if (profile.Id == Guid.Empty || !usedIds.Add(profile.Id))
            {
                do { profile.Id = Guid.NewGuid(); }
                while (!usedIds.Add(profile.Id));
                changed = true;
            }

            if (profile.Config is null)
            {
                profile.Config = ProfileCatalog.DefaultCustom();
                changed = true;
            }

            string originalName = profile.Name?.Trim() ?? string.Empty;
            string baseName = string.IsNullOrWhiteSpace(originalName) ? "Imported Profile" : originalName;
            if (ProfileCatalog.IsReservedName(baseName)) baseName += " (Imported)";
            string uniqueName = ProfileNames.MakeUnique(baseName, usedNames);
            if (!string.Equals(profile.Name, uniqueName, StringComparison.Ordinal))
            {
                profile.Name = uniqueName;
                changed = true;
            }
            usedNames.Add(uniqueName);

            string description = (profile.Description ?? string.Empty).Trim();
            if (!string.Equals(profile.Description, description, StringComparison.Ordinal))
            {
                profile.Description = description;
                changed = true;
            }
            if (profile.BuiltIn)
            {
                profile.BuiltIn = false;
                changed = true;
            }
        }

        return changed;
    }
}

public static class ProfileNames
{
    public const int MaximumLength = 80;
    private const string PortableInvalidCharacters = "<>:\"/\\|?*";

    public static string ValidatePersonalName(string? name)
    {
        string normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new ArgumentException("Profile name cannot be empty.");
        if (normalized.Length > MaximumLength) throw new ArgumentException($"Profile name cannot exceed {MaximumLength} characters.");
        if (ProfileCatalog.IsReservedName(normalized)) throw new ArgumentException($"'{normalized}' is a protected profile name.");
        if (HasInvalidCharacters(normalized)) throw new ArgumentException("Profile name contains invalid filename characters.");
        return normalized;
    }

    public static bool HasInvalidCharacters(string value)
        => value.Any(character => char.IsControl(character) || PortableInvalidCharacters.Contains(character));

    public static string SanitizeFileStem(string value)
        => string.Concat(value.Select(character => char.IsControl(character) || PortableInvalidCharacters.Contains(character) ? '_' : character));

    public static string MakeUnique(string requested, IEnumerable<string> existingNames)
    {
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string normalized = requested.Trim();
        if (normalized.Length == 0) normalized = "Imported Profile";
        if (normalized.Length > MaximumLength) normalized = normalized[..MaximumLength].TrimEnd();
        if (!existing.Contains(normalized)) return normalized;

        for (int suffix = 2; ; suffix++)
        {
            string marker = $" ({suffix})";
            int allowed = Math.Max(1, MaximumLength - marker.Length);
            string candidate = normalized.Length > allowed ? normalized[..allowed].TrimEnd() + marker : normalized + marker;
            if (!existing.Contains(candidate)) return candidate;
        }
    }
}

public static class ProfileOperations
{
    public static void Save(RandomProfile profile, AppSettings settings, HRandomConfig config)
    {
        config.Validate();
        if (profile.BuiltIn)
        {
            if (!profile.Name.Equals(ProfileCatalog.CustomName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("H-Random and S-Random are protected presets.");
            settings.CustomConfig = config.Clone();
        }
        profile.Config = config.Clone();
    }

    public static void ResetCustom(RandomProfile profile, AppSettings settings)
    {
        if (!profile.BuiltIn || !profile.Name.Equals(ProfileCatalog.CustomName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the persistent Custom profile can be reset.");
        settings.CustomConfig = ProfileCatalog.DefaultCustom();
        profile.Config = settings.CustomConfig.Clone();
    }

    public static RandomProfile Duplicate(
        RandomProfile source,
        HRandomConfig config,
        string name,
        string description,
        IEnumerable<string> existingNames)
    {
        if (source.Config is null) throw new ArgumentException("Source profile configuration is missing.");
        string requestedName = ProfileNames.ValidatePersonalName(name);
        string normalizedDescription = (description ?? string.Empty).Trim();
        if (normalizedDescription.Length > ProfileTransfer.MaximumDescriptionLength)
            throw new ArgumentException($"Description cannot exceed {ProfileTransfer.MaximumDescriptionLength} characters.");
        config.Validate();
        return new RandomProfile
        {
            Id = Guid.NewGuid(),
            Name = ProfileNames.MakeUnique(requestedName, existingNames),
            Description = normalizedDescription,
            BuiltIn = false,
            Config = config.Clone()
        };
    }
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
                ProfileSettingsMigration.Apply(defaults);
                Save(defaults);
                return defaults;
            }
            try
            {
                AppSettings settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Options) ?? new AppSettings();
                if (ProfileSettingsMigration.Apply(settings)) Save(settings);
                return settings;
            }
            catch (Exception ex)
            {
                Log($"Configuración corrupta; se restauraron defaults: {ex.Message}");
                var defaults = new AppSettings();
                ProfileSettingsMigration.Apply(defaults);
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
            AppSettings settings = !File.Exists(SettingsPath)
                ? new AppSettings()
                : JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Options) ?? new AppSettings();
            ProfileSettingsMigration.Apply(settings);
            return settings;
        }
        catch
        {
            var defaults = new AppSettings();
            ProfileSettingsMigration.Apply(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        string temporaryPath = Path.Combine(DirectoryPath, $".{Path.GetFileName(SettingsPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] json = new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(settings, Options));
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(json);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch { }
        }
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
