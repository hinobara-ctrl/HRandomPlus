namespace HRandomPlus.Randomization;

internal static class StableTopK
{
    public static (T Item, double Score)[] Select<T>(IReadOnlyList<T> items, int maximum,
                                                     Func<T, double> scoreSelector)
    {
        int count = Math.Min(maximum, items.Count);
        if (count == 0) return Array.Empty<(T, double)>();

        if (count == items.Count)
        {
            return items.Select(item => (Item: item, Score: scoreSelector(item)))
                        .OrderByDescending(entry => entry.Score)
                        .ToArray();
        }

        var queue = new PriorityQueue<Entry<T>, Entry<T>>(count, WorstFirstComparer<T>.Instance);
        for (int index = 0; index < items.Count; index++)
        {
            var entry = new Entry<T>(items[index], scoreSelector(items[index]), index);
            if (queue.Count < count)
            {
                queue.Enqueue(entry, entry);
                continue;
            }

            queue.TryPeek(out _, out Entry<T> worst);
            if (!IsBetter(entry, worst)) continue;
            queue.Dequeue();
            queue.Enqueue(entry, entry);
        }

        var selected = new Entry<T>[count];
        int output = 0;
        foreach ((Entry<T> element, _) in queue.UnorderedItems)
            selected[output++] = element;

        Array.Sort(selected, BestFirstComparer<T>.Instance);
        var result = new (T, double)[selected.Length];
        for (int index = 0; index < selected.Length; index++)
            result[index] = (selected[index].Item, selected[index].Score);
        return result;
    }

    private static bool IsBetter<T>(Entry<T> candidate, Entry<T> other)
    {
        int score = candidate.Score.CompareTo(other.Score);
        return score > 0 || score == 0 && candidate.Index < other.Index;
    }

    private readonly record struct Entry<T>(T Item, double Score, int Index);

    private sealed class BestFirstComparer<T> : IComparer<Entry<T>>
    {
        public static readonly BestFirstComparer<T> Instance = new();

        public int Compare(Entry<T> left, Entry<T> right)
        {
            int score = right.Score.CompareTo(left.Score);
            return score != 0 ? score : left.Index.CompareTo(right.Index);
        }
    }

    private sealed class WorstFirstComparer<T> : IComparer<Entry<T>>
    {
        public static readonly WorstFirstComparer<T> Instance = new();

        public int Compare(Entry<T> left, Entry<T> right)
        {
            int score = left.Score.CompareTo(right.Score);
            return score != 0 ? score : right.Index.CompareTo(left.Index);
        }
    }
}
