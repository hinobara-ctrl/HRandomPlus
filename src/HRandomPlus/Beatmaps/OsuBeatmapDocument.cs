using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HRandomPlus.Beatmaps;

public sealed class OsuBeatmapDocument
{
    private readonly List<string> lines;
    private readonly Encoding encoding;
    private readonly byte[] bom;
    private readonly string newline;

    public string SourcePath { get; }
    public int Mode { get; }
    public int Keys { get; }
    public string Version { get; private set; }
    public string Artist { get; }
    public string Title { get; }
    public string Creator { get; }
    public int BeatmapId { get; private set; }
    public int BeatmapSetId { get; private set; }
    public string AudioFilename { get; }
    public IReadOnlyList<ManiaHitObject> HitObjects { get; }

    private OsuBeatmapDocument(string sourcePath, List<string> lines, Encoding encoding, byte[] bom,
                               string newline, int mode, int keys, string version, string artist,
                               string title, string creator, int beatmapId, int beatmapSetId,
                               string audioFilename, IReadOnlyList<ManiaHitObject> hitObjects)
    {
        SourcePath = sourcePath;
        this.lines = lines;
        this.encoding = encoding;
        this.bom = bom;
        this.newline = newline;
        Mode = mode;
        Keys = keys;
        Version = version;
        Artist = artist;
        Title = title;
        Creator = creator;
        BeatmapId = beatmapId;
        BeatmapSetId = beatmapSetId;
        AudioFilename = audioFilename;
        HitObjects = hitObjects;
    }

    public static OsuBeatmapDocument Parse(string sourcePath, byte[] data)
    {
        (Encoding enc, byte[] prefix, int skip) = DetectEncoding(data);
        string text = enc.GetString(data, skip, data.Length - skip);
        string nl = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n"
            : text.Contains('\n') ? "\n" : text.Contains('\r') ? "\r" : Environment.NewLine;
        var lineList = Regex.Split(text, "\r\n|\n|\r", RegexOptions.CultureInvariant).ToList();

        var values = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string section = string.Empty;
        int hitObjectsStart = -1;

        for (int i = 0; i < lineList.Count; i++)
        {
            string trimmed = lineList[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1];
                if (!values.ContainsKey(section))
                    values[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (section.Equals("HitObjects", StringComparison.OrdinalIgnoreCase))
                    hitObjectsStart = i + 1;
                continue;
            }

            int colon = lineList[i].IndexOf(':');
            if (colon > 0 && values.TryGetValue(section, out var sectionValues))
                sectionValues[lineList[i][..colon].Trim()] = lineList[i][(colon + 1)..].Trim();
        }

        int mode = ReadInt(values, "General", "Mode", 0);
        string version = ReadString(values, "Metadata", "Version", Path.GetFileNameWithoutExtension(sourcePath));
        string artist = ReadString(values, "Metadata", "Artist", "Unknown Artist");
        string title = ReadString(values, "Metadata", "Title", "Unknown Title");
        string creator = ReadString(values, "Metadata", "Creator", "Unknown Mapper");
        int beatmapId = ReadInt(values, "Metadata", "BeatmapID", 0);
        int beatmapSetId = ReadInt(values, "Metadata", "BeatmapSetID", -1);
        string audioFilename = ReadString(values, "General", "AudioFilename", string.Empty);
        if (mode != 3)
            return new OsuBeatmapDocument(sourcePath, lineList, enc, prefix, nl, mode, 0, version,
                artist, title, creator, beatmapId, beatmapSetId, audioFilename, Array.Empty<ManiaHitObject>());

        double circleSize = ReadDouble(values, "Difficulty", "CircleSize");
        int keys = (int)Math.Round(circleSize, MidpointRounding.AwayFromZero);
        if (keys is < 1 or > 18 || Math.Abs(circleSize - keys) > 0.001)
            throw new InvalidDataException($"CircleSize no representa un keymode válido en '{sourcePath}': {circleSize}.");
        if (hitObjectsStart < 0)
            throw new InvalidDataException($"Falta la sección [HitObjects] en '{sourcePath}'.");

        var objects = new List<ManiaHitObject>();
        for (int i = hitObjectsStart; i < lineList.Count; i++)
        {
            string trimmed = lineList[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                break;
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;
            objects.Add(ManiaHitObject.Parse(lineList[i], i, keys));
        }

        return new OsuBeatmapDocument(sourcePath, lineList, enc, prefix, nl, mode, keys, version,
            artist, title, creator, beatmapId, beatmapSetId, audioFilename, objects);
    }

    public void ApplyObjects()
    {
        foreach (ManiaHitObject hitObject in HitObjects)
            lines[hitObject.LineIndex] = hitObject.BuildLine(Keys);
    }

    public void AppendVersionSuffix(string suffix)
    {
        if (suffix.Length == 0 || Version.EndsWith(suffix, StringComparison.Ordinal))
            return;

        SetVersion(Version + suffix);
    }

    public void SetVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("La dificultad no puede estar vacía.", nameof(version));

        string section = string.Empty;
        for (int i = 0; i < lines.Count; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1];
                continue;
            }
            if (!section.Equals("Metadata", StringComparison.OrdinalIgnoreCase))
                continue;
            int colon = lines[i].IndexOf(':');
            if (colon < 0 || !lines[i][..colon].Trim().Equals("Version", StringComparison.OrdinalIgnoreCase))
                continue;
            string valuePart = lines[i][(colon + 1)..];
            int spaces = valuePart.Length - valuePart.TrimStart().Length;
            Version = version;
            lines[i] = lines[i][..(colon + 1)] + new string(' ', spaces) + Version;
            return;
        }
        throw new InvalidDataException($"No se encontró Metadata/Version en '{SourcePath}'.");
    }

    public void SetBeatmapId(int value)
    {
        SetKeyValue("Metadata", "BeatmapID", value.ToString(CultureInfo.InvariantCulture));
        BeatmapId = value;
    }

    public void SetBeatmapSetId(int value)
    {
        SetKeyValue("Metadata", "BeatmapSetID", value.ToString(CultureInfo.InvariantCulture));
        BeatmapSetId = value;
    }

    public IReadOnlyList<double> GetBpms()
    {
        var bpms = new List<double>();
        string section = string.Empty;
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1];
                continue;
            }
            if (!section.Equals("TimingPoints", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;

            string[] fields = trimmed.Split(',');
            if (fields.Length < 2 ||
                !double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double beatLength) ||
                beatLength <= 0)
                continue;
            if (fields.Length > 6 && fields[6].Trim() != "1")
                continue;

            double bpm = 60000d / beatLength;
            if (double.IsFinite(bpm) && bpm > 0 && bpms.All(value => Math.Abs(value - bpm) > 0.0001))
                bpms.Add(bpm);
        }
        return bpms;
    }

    private void SetKeyValue(string targetSection, string key, string value)
    {
        string section = string.Empty;
        int insertAt = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (section.Equals(targetSection, StringComparison.OrdinalIgnoreCase))
                {
                    insertAt = i;
                    break;
                }
                section = trimmed[1..^1];
                continue;
            }
            if (!section.Equals(targetSection, StringComparison.OrdinalIgnoreCase))
                continue;
            int colon = lines[i].IndexOf(':');
            if (colon > 0 && lines[i][..colon].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = lines[i][..(colon + 1)] + value;
                return;
            }
        }
        if (insertAt < 0 && section.Equals(targetSection, StringComparison.OrdinalIgnoreCase))
            insertAt = lines.Count;
        if (insertAt < 0)
            throw new InvalidDataException($"No se encontró [{targetSection}] en '{SourcePath}'.");
        lines.Insert(insertAt, $"{key}:{value}");
    }

    public byte[] ToBytes()
    {
        byte[] content = encoding.GetBytes(string.Join(newline, lines));
        if (bom.Length == 0)
            return content;
        byte[] result = new byte[bom.Length + content.Length];
        Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
        Buffer.BlockCopy(content, 0, result, bom.Length, content.Length);
        return result;
    }

    private static (Encoding encoding, byte[] bom, int skip) DetectEncoding(byte[] data)
    {
        if (data.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            return (new UTF8Encoding(false, true), new byte[] { 0xEF, 0xBB, 0xBF }, 3);
        if (data.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
            return (new UnicodeEncoding(false, false, true), new byte[] { 0xFF, 0xFE }, 2);
        if (data.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
            return (new UnicodeEncoding(true, false, true), new byte[] { 0xFE, 0xFF }, 2);
        return (new UTF8Encoding(false, true), Array.Empty<byte>(), 0);
    }

    private static string ReadString(Dictionary<string, Dictionary<string, string>> values,
                                     string section, string key, string? defaultValue = null)
    {
        if (values.TryGetValue(section, out var s) && s.TryGetValue(key, out string? value))
            return value;
        return defaultValue ?? throw new InvalidDataException($"Falta [{section}] {key}.");
    }

    private static int ReadInt(Dictionary<string, Dictionary<string, string>> values,
                               string section, string key, int defaultValue)
        => int.TryParse(ReadString(values, section, key, defaultValue.ToString(CultureInfo.InvariantCulture)),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : defaultValue;

    private static double ReadDouble(Dictionary<string, Dictionary<string, string>> values,
                                     string section, string key)
        => double.TryParse(ReadString(values, section, key), NumberStyles.Float,
                           CultureInfo.InvariantCulture, out double value)
            ? value
            : throw new InvalidDataException($"[{section}] {key} no es un número válido.");
}
