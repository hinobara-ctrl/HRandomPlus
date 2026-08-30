using System.Text;

namespace HRandomPlus.Integration.Lazer;

public sealed class LazerRuntimeLogMonitor
{
    private string? currentPath;
    private long position;
    private DateTime creationTimeUtc;
    private string pending = string.Empty;
    private LazerLogSelection? current;

    public LazerLogSelection? ReadCurrent(LazerStorage storage)
    {
        string? path = FindCurrentLog(storage.LogsPath);
        if (path is null)
        {
            Reset();
            return null;
        }

        var info = new FileInfo(path);
        if (!path.Equals(currentPath, PathComparison) || info.Length < position ||
            (creationTimeUtc != default && info.CreationTimeUtc != creationTimeUtc))
            Initialize(path, info);
        else if (info.Length > position)
            ReadAppend(path, info.LastWriteTimeUtc);
        return current;
    }

    public void Reset()
    {
        currentPath = null;
        position = 0;
        creationTimeUtc = default;
        pending = string.Empty;
        current = null;
    }

    private void Initialize(string path, FileInfo info)
    {
        currentPath = path;
        creationTimeUtc = info.CreationTimeUtc;
        pending = string.Empty;
        const int scan_limit = 2 * 1024 * 1024;
        using FileStream stream = OpenShared(path);
        long start = Math.Max(0, stream.Length - scan_limit);
        stream.Position = start;
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        string text = reader.ReadToEnd();
        if (start > 0)
        {
            int firstBreak = text.IndexOf('\n');
            text = firstBreak >= 0 ? text[(firstBreak + 1)..] : string.Empty;
        }
        current = LazerRuntimeLogParser.LastValid(SplitCompleteLines(text), info.LastWriteTimeUtc);
        position = stream.Length;
    }

    private void ReadAppend(string path, DateTime observedAt)
    {
        using FileStream stream = OpenShared(path);
        stream.Position = position;
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        string appended = reader.ReadToEnd();
        position = stream.Length;
        string combined = pending + appended;
        bool complete = combined.EndsWith('\n');
        string[] lines = combined.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        pending = complete ? string.Empty : lines[^1];
        int count = complete ? lines.Length : lines.Length - 1;
        for (int i = 0; i < count; i++)
        {
            LazerLogSelection? parsed = LazerRuntimeLogParser.ParseLine(lines[i], observedAt);
            if (parsed is not null) current = parsed;
        }
    }

    private static IEnumerable<string> SplitCompleteLines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static FileStream OpenShared(string path)
        => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    public static DateTime GetLatestRuntimeLogWriteTimeUtc(string logsPath)
    {
        string? path = FindCurrentLog(logsPath);
        return path is null ? DateTime.MinValue : File.GetLastWriteTimeUtc(path);
    }

    private static string? FindCurrentLog(string logsPath)
    {
        string runtime = Path.Combine(logsPath, "runtime.log");
        if (File.Exists(runtime)) return runtime;
        try
        {
            return Directory.EnumerateFiles(logsPath, "*.log")
                .Where(path =>
                {
                    string name = Path.GetFileName(path);
                    return name.StartsWith("runtime", StringComparison.OrdinalIgnoreCase) ||
                           name.EndsWith(".runtime.log", StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
