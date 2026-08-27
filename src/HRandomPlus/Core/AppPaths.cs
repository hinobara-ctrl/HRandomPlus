namespace HRandomPlus.Core;

public static class AppPaths
{
    public static string ConfigDirectory => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HRandomPlus")
        : Path.Combine(GetXdgDirectory("XDG_CONFIG_HOME", ".config"), "HRandomPlus");

    public static string DataDirectory => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HRandomPlus")
        : Path.Combine(GetXdgDirectory("XDG_DATA_HOME", Path.Combine(".local", "share")), "HRandomPlus");

    public static string StateDirectory => OperatingSystem.IsWindows()
        ? ConfigDirectory
        : Path.Combine(GetXdgDirectory("XDG_STATE_HOME", Path.Combine(".local", "state")), "HRandomPlus");

    public static string OutputDirectory => Path.Combine(DataDirectory, "Generated Beatmaps");

    private static string GetXdgDirectory(string variable, string fallback)
    {
        string? configured = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured))
            return configured;

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
            throw new InvalidOperationException("No se pudo determinar la carpeta personal del usuario.");
        return Path.Combine(userProfile, fallback);
    }
}
