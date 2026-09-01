namespace HRandomPlus.Randomization;

public enum HandSide
{
    Left,
    Right
}

public sealed record PatternSnapshot(int Time, int[] Columns);

public sealed class RandomState
{
    private readonly int recentUsageWindow;
    private readonly int patternHistoryLength;
    private readonly Queue<int> recentColumnUses = new();
    private readonly Queue<HandSide> recentHandUses = new();
    private readonly List<PatternSnapshot> recentPatterns = new();

    public int Keys { get; }
    public long[] LastNoteTime { get; }
    public int[] RecentColumnUsage { get; }
    public int LeftHandUsage { get; private set; }
    public int RightHandUsage { get; private set; }
    public IReadOnlyList<PatternSnapshot> RecentPatterns => recentPatterns;
    public IReadOnlyList<int> LastAssignedColumns => recentPatterns.Count == 0
        ? Array.Empty<int>()
        : recentPatterns[^1].Columns;
    public IReadOnlyDictionary<int, int> ActiveLongNotes => activeLongNotes;

    private readonly Dictionary<int, int> activeLongNotes = new();

    public RandomState(int keys, int recentUsageWindow, int patternHistoryLength)
    {
        Keys = keys;
        this.recentUsageWindow = recentUsageWindow;
        this.patternHistoryLength = patternHistoryLength;
        LastNoteTime = Enumerable.Repeat(long.MinValue / 4, keys).ToArray();
        RecentColumnUsage = new int[keys];
    }

    /// <summary>
    /// Releases LN destinations whose tails are strictly before the current timestamp.
    /// Destinations ending exactly now remain reserved as a preference, avoiding generated
    /// head/tail contacts that can look like overlaps in osu!mania.
    /// </summary>
    public void ReleaseLongNotesBefore(int currentTime)
    {
        foreach (int column in activeLongNotes.Where(pair => pair.Value < currentTime).Select(pair => pair.Key).ToArray())
            activeLongNotes.Remove(column);
    }

    public int[] LongNotesEndingAt(int currentTime)
        => activeLongNotes.Where(pair => pair.Value == currentTime).Select(pair => pair.Key).ToArray();

    public void ReleaseLongNotesAt(int currentTime)
    {
        foreach (int column in LongNotesEndingAt(currentTime))
            activeLongNotes.Remove(column);
    }

    public void ActivateLongNote(int column, int endTime)
    {
        if (!activeLongNotes.TryAdd(column, endTime))
            throw new InvalidOperationException($"La columna {column + 1} ya contiene una LN activa.");
    }

    public HandSide? GetHand(int column)
    {
        int leftEnd = Keys / 2;
        int rightStart = (Keys + 1) / 2;
        if (column < leftEnd)
            return HandSide.Left;
        if (column >= rightStart)
            return HandSide.Right;
        return null;
    }

    public void RecordGroup(int time, IReadOnlyCollection<int> columns)
    {
        int[] sorted = columns.OrderBy(c => c).ToArray();
        foreach (int column in sorted)
        {
            LastNoteTime[column] = time;
            recentColumnUses.Enqueue(column);
            RecentColumnUsage[column]++;
            if (recentColumnUses.Count > recentUsageWindow)
                RecentColumnUsage[recentColumnUses.Dequeue()]--;

            HandSide? hand = GetHand(column);
            if (hand is null)
                continue;
            recentHandUses.Enqueue(hand.Value);
            if (hand == HandSide.Left)
                LeftHandUsage++;
            else
                RightHandUsage++;
            if (recentHandUses.Count > recentUsageWindow)
            {
                if (recentHandUses.Dequeue() == HandSide.Left)
                    LeftHandUsage--;
                else
                    RightHandUsage--;
            }
        }

        recentPatterns.Add(new PatternSnapshot(time, sorted));
        if (recentPatterns.Count > patternHistoryLength)
            recentPatterns.RemoveAt(0);
    }

    public int AlternationContinuationLength(IReadOnlyCollection<int> candidateColumns, int time, long maximumGap)
    {
        if (recentPatterns.Count == 0 || maximumGap < 0) return 0;
        int[] current = candidateColumns.Distinct().ToArray();
        PatternSnapshot previous = recentPatterns[^1];
        if ((long)time - previous.Time > maximumGap) return 0;

        int best = 0;
        foreach (int currentAnchor in current)
        foreach (int previousAnchor in previous.Columns)
        {
            if (currentAnchor == previousAnchor || current.Contains(previousAnchor) || previous.Columns.Contains(currentAnchor))
                continue;

            int length = 2;
            int expected = currentAnchor;
            int other = previousAnchor;
            int laterTime = previous.Time;
            for (int index = recentPatterns.Count - 2; index >= 0; index--)
            {
                PatternSnapshot pattern = recentPatterns[index];
                if ((long)laterTime - pattern.Time > maximumGap)
                    break;

                if (!pattern.Columns.Contains(expected) || pattern.Columns.Contains(other))
                    break;
                length++;
                laterTime = pattern.Time;
                (expected, other) = (other, expected);
            }
            best = Math.Max(best, length);
        }
        return best >= 4 ? best : 0;
    }
}
