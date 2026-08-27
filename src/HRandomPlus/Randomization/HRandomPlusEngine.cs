using HRandomPlus.Analysis;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;

namespace HRandomPlus.Randomization;

public sealed record RandomizationResult(long Seed, PatternStatistics Before, PatternStatistics After);

public sealed class HRandomPlusEngine
{
    private readonly HRandomConfig config;
    private readonly CandidateScorer scorer;

    public HRandomPlusEngine(HRandomConfig config)
    {
        this.config = config;
        scorer = new CandidateScorer(config);
    }

    public RandomizationResult Randomize(IReadOnlyList<ManiaHitObject> objects, int keys, long seed,
                                         IReadOnlyDictionary<int, int>? initialActiveLongNotes = null)
    {
        var rng = new SeededRandom(seed);
        var state = new RandomState(keys, config.RecentUsageWindow, config.PatternHistoryLength);
        if (initialActiveLongNotes is not null)
            foreach ((int column, int endTime) in initialActiveLongNotes)
                state.ActivateLongNote(column, endTime);
        PatternStatistics before = PatternAnalyzer.Analyze(objects, keys, config.BaseThresholdMs, false);

        foreach (IGrouping<int, ManiaHitObject> group in objects.GroupBy(h => h.StartTime).OrderBy(g => g.Key))
        {
            int time = group.Key;
            ManiaHitObject[] notes = group.OrderByDescending(h => h.IsLongNote)
                                          .ThenByDescending(h => h.EndTime ?? h.StartTime)
                                          .ThenBy(h => h.LineIndex)
                                          .ToArray();
            state.ReleaseLongNotesBefore(time);
            int[] preferredAvailable = Enumerable.Range(0, keys)
                                                 .Where(c => !state.ActiveLongNotes.ContainsKey(c))
                                                 .ToArray();
            int[] endingNow = state.LongNotesEndingAt(time);
            // Tail columns at this exact timestamp are a last-resort fallback. This preserves
            // solvability for charts that already require release+press on every available key,
            // while preventing H-RANDOM+ from creating such contacts unnecessarily.
            bool requiresTailReuse = notes.Length > preferredAvailable.Length;
            int[] available = requiresTailReuse
                ? preferredAvailable.Concat(endingNow).ToArray()
                : preferredAvailable;
            if (notes.Length > available.Length)
                throw new InvalidDataException($"No hay columnas suficientes en {time} ms: {notes.Length} notas y {available.Length} libres por LN activas.");

            int threshold = PatternAnalyzer.DynamicThreshold(state, time, notes.Length, config);
            List<int[]> candidateSets;
            if (requiresTailReuse)
            {
                int neededTailColumns = notes.Length - preferredAvailable.Length;
                candidateSets = GenerateCombinations(endingNow, neededTailColumns, rng)
                    .Select(tails => preferredAvailable.Concat(tails).OrderBy(c => c).ToArray())
                    .ToList();
            }
            else
            {
                int[] primary = available.Where(c => time - state.LastNoteTime[c] > threshold).ToArray();
                if (primary.Length >= notes.Length)
                {
                    candidateSets = GenerateCombinations(primary, notes.Length, rng);
                }
                else
                {
                    int needed = notes.Length - primary.Length;
                    int[] inferior = available.Except(primary)
                                                .OrderBy(c => state.LastNoteTime[c])
                                                .ToArray();
                    long cutoff = state.LastNoteTime[inferior[Math.Min(needed - 1, inferior.Length - 1)]];
                    int[] oldest = inferior.Where(c => state.LastNoteTime[c] <= cutoff).ToArray();
                    candidateSets = GenerateCombinations(oldest, needed, rng)
                        .Select(extra => primary.Concat(extra).OrderBy(c => c).ToArray())
                        .ToList();
                }
            }

            if (candidateSets.Count == 0)
                throw new InvalidOperationException($"No se generaron candidatos para {time} ms.");

            int[] selected = WeightedChoice(candidateSets, state, time, threshold, rng);
            var assignments = selected.ToList();
            rng.Shuffle(assignments);
            state.ReleaseLongNotesAt(time);
            for (int i = 0; i < notes.Length; i++)
            {
                notes[i].AssignedColumn = assignments[i];
                if (notes[i].IsLongNote)
                    state.ActivateLongNote(assignments[i], notes[i].EndTime!.Value);
            }
            state.RecordGroup(time, selected);
        }

        PatternStatistics after = PatternAnalyzer.Analyze(objects, keys, config.BaseThresholdMs, true);
        return new RandomizationResult(seed, before, after);
    }

    private int[] WeightedChoice(List<int[]> candidates, RandomState state, int time, int threshold, SeededRandom rng)
    {
        var ranked = candidates.Select(c => (Columns: c, Score: scorer.ScoreSet(state, c, time, threshold)))
                               .OrderByDescending(c => c.Score)
                               .Take(config.WeightedTopCandidates)
                               .ToArray();
        double max = ranked[0].Score;
        double[] weights = ranked.Select(c => Math.Exp((c.Score - max) / config.WeightedTemperature)).ToArray();
        double target = rng.NextDouble() * weights.Sum();
        for (int i = 0; i < ranked.Length; i++)
        {
            target -= weights[i];
            if (target <= 0)
                return ranked[i].Columns;
        }
        return ranked[^1].Columns;
    }

    private List<int[]> GenerateCombinations(int[] columns, int count, SeededRandom rng)
    {
        if (count == 0)
            return new List<int[]> { Array.Empty<int>() };
        if (count > columns.Length)
            return new List<int[]>();

        long total = CombinationCount(columns.Length, count);
        if (total <= config.MaxCandidateSets)
        {
            var result = new List<int[]>((int)total);
            build(0, new List<int>());
            return result;

            void build(int start, List<int> current)
            {
                if (current.Count == count)
                {
                    result.Add(current.ToArray());
                    return;
                }
                for (int i = start; i <= columns.Length - (count - current.Count); i++)
                {
                    current.Add(columns[i]);
                    build(i + 1, current);
                    current.RemoveAt(current.Count - 1);
                }
            }
        }

        var sampled = new Dictionary<string, int[]>(StringComparer.Ordinal);
        int attempts = 0;
        while (sampled.Count < config.MaxCandidateSets && attempts++ < config.MaxCandidateSets * 20)
        {
            var shuffled = columns.ToList();
            rng.Shuffle(shuffled);
            int[] candidate = shuffled.Take(count).OrderBy(c => c).ToArray();
            sampled.TryAdd(string.Join(',', candidate), candidate);
        }
        return sampled.Values.ToList();
    }

    private static long CombinationCount(int n, int k)
    {
        k = Math.Min(k, n - k);
        long result = 1;
        for (int i = 1; i <= k; i++)
        {
            if (result > long.MaxValue / (n - k + i))
                return long.MaxValue;
            result = result * (n - k + i) / i;
        }
        return result;
    }
}
