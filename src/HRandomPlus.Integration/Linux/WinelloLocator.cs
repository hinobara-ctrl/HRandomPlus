namespace HRandomPlus.Integration.Linux;

public sealed class WinelloLocator
{
    private readonly Func<string, string?> getEnvironmentVariable;
    private readonly string userProfile;

    public WinelloLocator()
        : this(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) { }

    public WinelloLocator(Func<string, string?> getEnvironmentVariable, string userProfile)
    {
        this.getEnvironmentVariable = getEnvironmentVariable;
        this.userProfile = userProfile;
    }

    public string ConfigurationPath
    {
        get
        {
            string? xdgData = getEnvironmentVariable("XDG_DATA_HOME");
            string dataRoot = !string.IsNullOrWhiteSpace(xdgData) && Path.IsPathFullyQualified(xdgData)
                ? xdgData
                : Path.Combine(userProfile, ".local", "share");
            return Path.Combine(dataRoot, "osuconfig", "osupath");
        }
    }

    public bool TryLocate(out string? osuRoot, out string status)
    {
        osuRoot = null;
        try
        {
            if (!File.Exists(ConfigurationPath))
            {
                status = $"No se encontró la configuración de osu-winello: {ConfigurationPath}";
                return false;
            }

            string configured = File.ReadAllText(ConfigurationPath).Trim().Trim('"').Trim();
            if (string.IsNullOrWhiteSpace(configured))
            {
                status = $"La configuración de osu-winello está vacía: {ConfigurationPath}";
                return false;
            }
            if (configured.StartsWith("~/", StringComparison.Ordinal))
                configured = Path.Combine(userProfile, configured[2..]);
            string candidate = Path.GetFullPath(configured);
            if (!Directory.Exists(Path.Combine(candidate, "Songs")))
            {
                status = $"La ruta de osu-winello no contiene Songs: {candidate}";
                return false;
            }

            osuRoot = candidate;
            status = "native osu-winello path detected";
            return true;
        }
        catch (Exception ex)
        {
            status = $"No se pudo leer la configuración de osu-winello: {ex.Message}";
            return false;
        }
    }
}
