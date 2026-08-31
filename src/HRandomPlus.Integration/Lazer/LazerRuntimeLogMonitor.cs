using System.Text;

namespace HRandomPlus.Integration.Lazer;

public interface ILazerRuntimeLogMonitor
{
    LazerLogSelection? ReadCurrent(LazerStorage storage);
    void Reset();
}

public sealed class LazerRuntimeLogMonitor : ILazerRuntimeLogMonitor
{
    private readonly Decoder decoder = new UTF8Encoding(false, false).GetDecoder();
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
        decoder.Reset();
    }

    private void Initialize(string path, FileInfo info)
    {
        currentPath = path;
        creationTimeUtc = info.CreationTimeUtc;
        const int scan_block = 2 * 1024 * 1024;
        const int maximum_initial_scan = 32 * 1024 * 1024;
        using FileStream stream = OpenShared(path);
        long scanLength = Math.Min(stream.Length, scan_block);
        while (true)
        {
            pending = string.Empty;
            current = null;
            decoder.Reset();
            long start = Math.Max(0, stream.Length - scanLength);
            stream.Position = start;
            string text = Decode(stream);
            if (start > 0)
            {
                int firstBreak = text.IndexOf('\n');
                text = firstBreak >= 0 ? text[(firstBreak + 1)..] : string.Empty;
            }
            ProcessText(text, info.LastWriteTimeUtc);
            if (current is not null || start == 0 || scanLength >= maximum_initial_scan) break;
            scanLength = Math.Min(maximum_initial_scan, scanLength + scan_block);
        }
        position = stream.Length;
    }

    private void ReadAppend(string path, DateTime observedAt)
    {
        using FileStream stream = OpenShared(path);
        stream.Position = position;
        string appended = Decode(stream);
        position = stream.Length;
        ProcessText(appended, observedAt);
    }

    private string Decode(Stream stream)
    {
        var text = new StringBuilder();
        byte[] bytes = new byte[8192];
        char[] chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        int read;
        while ((read = stream.Read(bytes, 0, bytes.Length)) > 0)
        {
            int count = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
            text.Append(chars, 0, count);
        }
        return text.ToString();
    }

    private void ProcessText(string appended, DateTime observedAt)
    {
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

    private static FileStream OpenShared(string path)
        => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    public static DateTime GetLatestRuntimeLogWriteTimeUtc(string logsPath)
    {
        string? path = FindCurrentLog(logsPath);
        return path is null ? DateTime.MinValue : File.GetLastWriteTimeUtc(path);
    }

    private static string? FindCurrentLog(string logsPath)
    {
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
