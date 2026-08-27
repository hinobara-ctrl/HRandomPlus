using System.Globalization;

namespace HRandomPlus.Beatmaps;

public sealed class ManiaHitObject
{
    public int LineIndex { get; }
    public string OriginalLine { get; }
    public string[] Fields { get; }
    public int OriginalX { get; }
    public int StartTime { get; }
    public int Type { get; }
    public int? EndTime { get; }
    public int OriginalColumn { get; }
    public int AssignedColumn { get; set; }
    public bool IsLongNote => (Type & 128) != 0;

    private ManiaHitObject(int lineIndex, string originalLine, string[] fields, int x, int startTime,
                           int type, int? endTime, int originalColumn)
    {
        LineIndex = lineIndex;
        OriginalLine = originalLine;
        Fields = fields;
        OriginalX = x;
        StartTime = startTime;
        Type = type;
        EndTime = endTime;
        OriginalColumn = originalColumn;
        AssignedColumn = originalColumn;
    }

    public static ManiaHitObject Parse(string line, int lineIndex, int keys)
    {
        string[] fields = line.Split(',');
        if (fields.Length < 5)
            throw new InvalidDataException($"HitObject inválido en la línea {lineIndex + 1}: faltan campos.");

        if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int time) ||
            !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type))
            throw new InvalidDataException($"HitObject inválido en la línea {lineIndex + 1}: x, time o type no es entero.");

        bool isCircle = (type & 1) != 0;
        bool isLong = (type & 128) != 0;
        if (!isCircle && !isLong)
            throw new InvalidDataException($"Tipo de objeto mania no soportado ({type}) en la línea {lineIndex + 1}.");

        int? endTime = null;
        if (isLong)
        {
            if (fields.Length < 6 || !int.TryParse(fields[5].Split(':')[0], NumberStyles.Integer,
                                                    CultureInfo.InvariantCulture, out int parsedEnd))
                throw new InvalidDataException($"Long Note inválida en la línea {lineIndex + 1}: falta endTime.");
            if (parsedEnd < time)
                throw new InvalidDataException($"Long Note inválida en la línea {lineIndex + 1}: endTime < startTime.");
            endTime = parsedEnd;
        }

        int column = Math.Clamp((int)Math.Floor(x * keys / 512.0), 0, keys - 1);
        return new ManiaHitObject(lineIndex, line, fields, x, time, type, endTime, column);
    }

    public string BuildLine(int keys)
    {
        string[] output = (string[])Fields.Clone();
        output[0] = ColumnToX(AssignedColumn, keys).ToString(CultureInfo.InvariantCulture);
        return string.Join(',', output);
    }

    public static int ColumnToX(int column, int keys)
        => (int)Math.Floor((column + 0.5) * 512.0 / keys);

    public bool NonPositionFieldsEqual(ManiaHitObject other)
        => Fields.Skip(1).SequenceEqual(other.Fields.Skip(1), StringComparer.Ordinal);
}
