namespace HRandomPlus.Core;

using System.Text;

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
        int copiedThrough = 0;
        StringBuilder? redacted = null;
        while (searchFrom <= value.Length - normalizedHome.Length)
        {
            int index = value.IndexOf(normalizedHome, searchFrom, comparison);
            if (index < 0) break;
            int end = index + normalizedHome.Length;
            bool leftBoundary = index == 0 || !IsPathCharacter(value[index - 1]);
            bool rightBoundary = end == value.Length || separators.Contains(value[end]) || !IsFileNameCharacter(value[end]);
            if (leftBoundary && rightBoundary)
            {
                redacted ??= new StringBuilder(value.Length);
                redacted.Append(value, copiedThrough, index - copiedThrough);
                redacted.Append(replacement);
                copiedThrough = end;
                searchFrom = end;
            }
            else
            {
                searchFrom = index + 1;
            }
        }
        if (redacted is null) return value;
        redacted.Append(value, copiedThrough, value.Length - copiedThrough);
        return redacted.ToString();
    }

    private static bool IsPathCharacter(char value)
        => char.IsLetterOrDigit(value) || value is '_' or '-' or '.' or '\\' or '/';

    private static bool IsFileNameCharacter(char value)
        => char.IsLetterOrDigit(value) || value is '_' or '-' or '.';

    private static bool IsWindowsStyle(string value)
        => value.Contains('\\') || (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':');

    private static string? ResolveHome()
        => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: > 0 } home ? home : null;
}
