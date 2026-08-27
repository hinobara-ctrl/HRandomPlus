using System.Globalization;
using System.Text.RegularExpressions;

namespace HRandomPlus.Core;

public readonly record struct BeatmapRange(int StartMs, int EndMs)
{
    private static readonly Regex Pattern = new(@"^\s*(\d{1,3}):(\d{2}):(\d{3})\s*-\s*(\d{1,3}):(\d{2}):(\d{3})\s*-?\s*$", RegexOptions.Compiled);

    public static BeatmapRange Parse(string text)
    {
        Match match = Pattern.Match(text);
        if (!match.Success) throw new FormatException("Usa el formato 00:37:005 - 01:13:005 -");
        int value(int offset)
        {
            int minutes = int.Parse(match.Groups[offset].Value, CultureInfo.InvariantCulture);
            int seconds = int.Parse(match.Groups[offset + 1].Value, CultureInfo.InvariantCulture);
            int milliseconds = int.Parse(match.Groups[offset + 2].Value, CultureInfo.InvariantCulture);
            if (seconds > 59) throw new FormatException("Los segundos deben estar entre 00 y 59.");
            return checked((minutes * 60 + seconds) * 1000 + milliseconds);
        }
        var range = new BeatmapRange(value(1), value(4));
        if (range.StartMs >= range.EndMs) throw new FormatException("El inicio debe ser menor que el final.");
        return range;
    }

    public bool Contains(int time) => time >= StartMs && time <= EndMs;
}
