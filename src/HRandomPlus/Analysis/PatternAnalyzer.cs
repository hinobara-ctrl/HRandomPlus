using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Randomization;

namespace HRandomPlus.Analysis;

public sealed class PatternStatistics
{
    public int Keymode { get; init; }
    public int TotalNotes { get; init; }
    public int LongNotes { get; init; }
    public int Chords { get; init; }
    public int QuickJacks { get; init; }
    public int Trills { get; init; }
    public int[] ColumnUsage { get; init; } = Array.Empty<int>();
}

public static class PatternAnalyzer
{
    public static int DynamicThreshold(RandomState state, int currentTime, int currentNoteCount, HRandomConfig config)
    {
        if (!config.DynamicThreshold || state.RecentPatterns.Count == 0)
            return config.BaseThresholdMs;

        PatternSnapshot[] window = state.RecentPatterns.TakeLast(8).ToArray();
        int elapsed = currentTime - window[0].Time;
        int events = window.Sum(p => p.Columns.Length) + currentNoteCount;
        if (elapsed <= 0 || events <= 1)
            return config.MaxThresholdMs;

        double average = elapsed / (double)(events - 1);
        if (average <= 50)
            return config.MaxThresholdMs;
        if (average >= 250)
            return config.MinThresholdMs;
        double ratio = (average - 50) / 200.0;
        return (int)Math.Round(config.MaxThresholdMs + ratio * (config.MinThresholdMs - config.MaxThresholdMs));
    }

    public static PatternStatistics Analyze(IReadOnlyList<ManiaHitObject> objects, int keys,
                                            int jackThreshold, bool assigned)
    {
        int columnOf(ManiaHitObject h) => assigned ? h.AssignedColumn : h.OriginalColumn;
        int[] usage = new int[keys];
        long[] last = Enumerable.Repeat(long.MinValue / 4, keys).ToArray();
        int jacks = 0;
        foreach (ManiaHitObject hitObject in objects.OrderBy(h => h.StartTime).ThenBy(h => h.LineIndex))
        {
            int column = columnOf(hitObject);
            usage[column]++;
            if (hitObject.StartTime - last[column] <= jackThreshold)
                jacks++;
            last[column] = hitObject.StartTime;
        }

        int trills = 0;
        var sequence = new List<(int Time, int Column)>();
        foreach (var group in objects.GroupBy(h => h.StartTime).OrderBy(g => g.Key))
        {
            if (group.Count() != 1)
            {
                sequence.Clear();
                continue;
            }
            var current = (Time: group.Key, Column: columnOf(group.Single()));
            sequence.Add(current);
            if (sequence.Count >= 4 &&
                sequence[^1].Column == sequence[^3].Column &&
                sequence[^2].Column == sequence[^4].Column &&
                sequence[^1].Column != sequence[^2].Column &&
                sequence[^1].Time - sequence[^4].Time <= 1000)
                trills++;
            if (sequence.Count > 16)
                sequence.RemoveAt(0);
        }

        return new PatternStatistics
        {
            Keymode = keys,
            TotalNotes = objects.Count,
            LongNotes = objects.Count(h => h.IsLongNote),
            Chords = objects.GroupBy(h => h.StartTime).Count(g => g.Count() > 1),
            QuickJacks = jacks,
            Trills = trills,
            ColumnUsage = usage
        };
    }
}
