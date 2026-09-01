using System.Text;
using System.Text.Json;

namespace HRandomPlus.Core;

public sealed class ProfileTransferDocument
{
    public string Format { get; set; } = string.Empty;
    public int FormatVersion { get; set; }
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EngineVersion { get; set; }
    public HRandomConfig? Config { get; set; }
}

public enum ProfileImportDecision
{
    Cancel,
    Update,
    ImportAsCopy
}

public static class ProfileTransfer
{
    public const string Format = "HRandomPlus.Profile";
    public const int FormatVersion = 1;
    public const int EngineVersion = 1;
    public const int MaximumFileBytes = 256 * 1024;
    public const int MaximumDescriptionLength = 500;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static byte[] Serialize(RandomProfile profile)
    {
        if (profile.Id == Guid.Empty) throw new ArgumentException("Profile ID cannot be empty.");
        ValidateExternalName(profile.Name);
        ValidateDescription(profile.Description);
        ValidateConfig(profile.Config);

        var document = new ProfileTransferDocument
        {
            Format = Format,
            FormatVersion = FormatVersion,
            ProfileId = profile.Id,
            Name = profile.Name.Trim(),
            Description = profile.Description.Trim(),
            EngineVersion = EngineVersion,
            Config = profile.Config.Clone()
        };
        return new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(document, Options));
    }

    public static void Export(string path, RandomProfile profile)
    {
        byte[] contents = Serialize(profile);
        WriteAtomically(path, contents);
    }

    public static RandomProfile Read(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("Profile file was not found.", path);
        if (info.Length > MaximumFileBytes) throw new InvalidDataException($"Profile file exceeds the {MaximumFileBytes / 1024} KB limit.");
        return Deserialize(File.ReadAllBytes(path));
    }

    public static RandomProfile Deserialize(ReadOnlySpan<byte> contents)
    {
        if (contents.Length > MaximumFileBytes) throw new InvalidDataException($"Profile file exceeds the {MaximumFileBytes / 1024} KB limit.");

        ProfileTransferDocument document;
        try
        {
            document = JsonSerializer.Deserialize<ProfileTransferDocument>(contents, Options)
                       ?? throw new InvalidDataException("Profile file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Profile file contains invalid JSON.", ex);
        }

        if (!string.Equals(document.Format, Format, StringComparison.Ordinal))
            throw new InvalidDataException("File is not an HRandomPlus profile.");
        if (document.FormatVersion != FormatVersion)
            throw new InvalidDataException($"Unsupported profile format version {document.FormatVersion}; this build supports version {FormatVersion}.");
        if (document.EngineVersion != EngineVersion)
            throw new InvalidDataException($"Incompatible randomizer engine version {document.EngineVersion}; this build supports version {EngineVersion}.");
        if (document.ProfileId == Guid.Empty) throw new InvalidDataException("Profile ID is missing or empty.");

        ValidateExternalName(document.Name);
        ValidateDescription(document.Description);
        ValidateConfig(document.Config);

        return new RandomProfile
        {
            Id = document.ProfileId,
            Name = document.Name.Trim(),
            Description = document.Description.Trim(),
            BuiltIn = false,
            Config = document.Config!.Clone()
        };
    }

    public static RandomProfile? Import(
        IList<RandomProfile> personalProfiles,
        RandomProfile incoming,
        ProfileImportDecision decision)
    {
        if (decision == ProfileImportDecision.Cancel) return null;
        if (incoming.Id == Guid.Empty) throw new ArgumentException("Imported profile ID cannot be empty.");
        ValidateExternalName(incoming.Name);
        ValidateDescription(incoming.Description);
        ValidateConfig(incoming.Config);

        RandomProfile? existingById = personalProfiles.FirstOrDefault(profile => profile.Id == incoming.Id);
        if (decision == ProfileImportDecision.Update && existingById is not null)
        {
            string updatedName = MakeImportName(incoming.Name, personalProfiles.Where(profile => !ReferenceEquals(profile, existingById)).Select(profile => profile.Name));
            existingById.Name = updatedName;
            existingById.Description = incoming.Description.Trim();
            existingById.Config = incoming.Config.Clone();
            existingById.BuiltIn = false;
            return existingById;
        }

        var imported = incoming.Clone();
        imported.BuiltIn = false;
        if (decision == ProfileImportDecision.ImportAsCopy || existingById is not null)
            imported.Id = Guid.NewGuid();
        imported.Name = MakeImportName(imported.Name, personalProfiles.Select(profile => profile.Name));
        personalProfiles.Add(imported);
        return imported;
    }

    public static string SuggestedFileName(RandomProfile profile)
    {
        string sanitized = ProfileNames.SanitizeFileStem(profile.Name.Trim());
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "profile";
        if (ProfileNames.IsWindowsReservedFileStem(sanitized)) sanitized = "_" + sanitized;
        return sanitized + ".hrp-profile.json";
    }

    private static string MakeImportName(string requested, IEnumerable<string> existingNames)
    {
        string baseName = requested.Trim();
        if (ProfileCatalog.IsReservedName(baseName)) baseName += " (Imported)";
        return ProfileNames.MakeUnique(baseName, existingNames.Concat(new[]
        {
            ProfileCatalog.HRandomName,
            ProfileCatalog.SRandomName,
            ProfileCatalog.CustomName
        }));
    }

    private static void ValidateExternalName(string? name)
    {
        string normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new InvalidDataException("Profile name cannot be empty.");
        if (normalized.Length > ProfileNames.MaximumLength) throw new InvalidDataException($"Profile name cannot exceed {ProfileNames.MaximumLength} characters.");
        if (ProfileNames.HasInvalidCharacters(normalized)) throw new InvalidDataException("Profile name contains invalid filename characters.");
    }

    private static void ValidateDescription(string? description)
    {
        if ((description?.Length ?? 0) > MaximumDescriptionLength)
            throw new InvalidDataException($"Profile description cannot exceed {MaximumDescriptionLength} characters.");
    }

    private static void ValidateConfig(HRandomConfig? config)
    {
        if (config is null) throw new InvalidDataException("Profile configuration is missing.");
        try { config.Validate(); }
        catch (Exception ex) when (ex is ArgumentException or NullReferenceException)
        {
            throw new InvalidDataException($"Profile configuration is invalid: {ex.Message}", ex);
        }
    }

    private static void WriteAtomically(string path, byte[] contents)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidDataException("Profile destination directory is invalid.");
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch { }
        }
    }
}
