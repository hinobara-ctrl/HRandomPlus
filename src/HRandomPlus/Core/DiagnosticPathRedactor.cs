namespace HRandomPlus.Core;

public static class DiagnosticPathRedactor
{
    public static string? Redact(string? value, string? homeDirectory = null)
    {
        if (string.IsNullOrEmpty(value)) return value;
        string? home = string.IsNullOrWhiteSpace(homeDirectory) ? ResolveHome() : homeDirectory;
        if (string.IsNullOrWhiteSpace(home)) return value;

        bool windowsStyle = IsWindowsStyle(value) || IsWindowsStyle(home);
        char[] separators = windowsStyle ? new[] { '\\', '/' } : new[] { '/' };
        string normalizedHome = home.TrimEnd(separators);
        StringComparison comparison = windowsStyle ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string replacement = windowsStyle ? "%USERPROFILE%" : "$HOME";
        int searchFrom = 0;
        while (searchFrom <= value.Length - normalizedHome.Length)
        {
            int index = value.IndexOf(normalizedHome, searchFrom, comparison);
            if (index < 0) break;
            int end = index + normalizedHome.Length;
            bool leftBoundary = index == 0 || !IsPathCharacter(value[index - 1]);
            bool rightBoundary = end == value.Length || separators.Contains(value[end]);
            if (leftBoundary && rightBoundary)
                return value[..index] + replacement + value[end..];
            searchFrom = index + 1;
        }
        return value;
    }

    private static bool IsPathCharacter(char value)
        => char.IsLetterOrDigit(value) || value is '_' or '-' or '.' or '\\' or '/';

    private static bool IsWindowsStyle(string value)
        => value.Contains('\\') || (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':');

    private static string? ResolveHome()
        => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: > 0 } home ? home : null;
}
