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
        PatternStatistics before = PatternAnalyzer.Analyze(objects, keys, config.BaseThresholdMs, config.MaxThresholdMs, false);
        bool preserveStages = config.PreserveDualStages && DualStageLayout.IsEligible(keys);

        foreach (IGrouping<int, ManiaHitObject> group in objects.GroupBy(h => h.StartTime).OrderBy(g => g.Key))
        {
            int time = group.Key;
            ManiaHitObject[] notes = group.OrderByDescending(h => h.IsLongNote)
                                          .ThenByDescending(h => h.EndTime ?? h.StartTime)
                                          .ThenBy(h => h.LineIndex)
                                          .ToArray();
            state.ReleaseLongNotesBefore(time);
            int threshold = PatternAnalyzer.DynamicThreshold(state, time, notes.Length, config);
            List<int[]> candidateSets = preserveStages
                ? BuildStageCandidates(notes, keys, state, time, threshold, rng)
                : CandidateSetsForRegion(Enumerable.Range(0, keys).ToArray(), notes.Length, state, time, threshold, rng);

            if (candidateSets.Count == 0)
                throw new InvalidOperationException($"No se generaron candidatos para {time} ms.");

            int[] selected = WeightedChoice(candidateSets, state, time, threshold, rng);
            state.ReleaseLongNotesAt(time);
            if (preserveStages)
            {
                if (keys % 2 != 0)
                {
                    AssignWithSharedCenter(notes, selected, keys, state, rng);
                }
                else
                {
                    foreach (IGrouping<int, ManiaHitObject> stageNotes in notes.GroupBy(note => DualStageLayout.StageOf(note.OriginalColumn, keys)))
                    {
                        var assignments = selected.Where(column => DualStageLayout.StageOf(column, keys) == stageNotes.Key).ToList();
                        rng.Shuffle(assignments);
                        Assign(stageNotes.ToArray(), assignments, state);
                    }
                }
            }
            else
            {
                var assignments = selected.ToList();
                rng.Shuffle(assignments);
                Assign(notes, assignments, state);
            }
            state.RecordGroup(time, selected);
        }

        PatternStatistics after = PatternAnalyzer.Analyze(objects, keys, config.BaseThresholdMs, config.MaxThresholdMs, true);
        return new RandomizationResult(seed, before, after);
    }

    private static void Assign(IReadOnlyList<ManiaHitObject> notes, IReadOnlyList<int> assignments, RandomState state)
    {
        for (int index = 0; index < notes.Count; index++)
        {
            notes[index].AssignedColumn = assignments[index];
            if (notes[index].IsLongNote)
                state.ActivateLongNote(assignments[index], notes[index].EndTime!.Value);
        }
    }

    private List<int[]> BuildStageCandidates(ManiaHitObject[] notes, int keys, RandomState state,
                                              int time, int threshold, SeededRandom rng)
    {
        if (keys % 2 == 0)
        {
            return CombineStageCandidates(notes.GroupBy(note => DualStageLayout.StageOf(note.OriginalColumn, keys))
                .OrderBy(grouping => grouping.Key)
                .Select(grouping => CandidateSetsForRegion(
                    Enumerable.Range(0, keys).Where(column => DualStageLayout.StageOf(column, keys) == grouping.Key).ToArray(),
                    grouping.Count(), state, time, threshold, rng))
                .ToArray(), rng);
        }

        int[] originCounts = Enumerable.Range(0, 3)
            .Select(stage => notes.Count(note => DualStageLayout.StageOf(note.OriginalColumn, keys) == stage))
            .ToArray();
        int[][] regions = Enumerable.Range(0, 3)
            .Select(stage => Enumerable.Range(0, keys)
                .Where(column => DualStageLayout.StageOf(column, keys) == stage).ToArray())
            .ToArray();
        var candidates = new Dictionary<string, int[]>(StringComparer.Ordinal);

        for (int centerDestinations = 0; centerDestinations <= 1; centerDestinations++)
        for (int leftDestinations = 0; leftDestinations <= regions[0].Length; leftDestinations++)
        {
            int rightDestinations = notes.Length - centerDestinations - leftDestinations;
            if (rightDestinations < 0 || rightDestinations > regions[2].Length)
                continue;
            if (!SharedCenterAssignmentExists(originCounts, leftDestinations, centerDestinations, rightDestinations))
                continue;

            List<int[]> combined = CombineStageCandidates(new[]
            {
                CandidateSetsForRegion(regions[0], leftDestinations, state, time, threshold, rng),
                CandidateSetsForRegion(regions[1], centerDestinations, state, time, threshold, rng),
                CandidateSetsForRegion(regions[2], rightDestinations, state, time, threshold, rng)
            }, rng);
            foreach (int[] candidate in combined)
                candidates.TryAdd(string.Join(',', candidate), candidate);
        }

        var result = candidates.Values.ToList();
        if (result.Count <= config.MaxCandidateSets) return result;
        rng.Shuffle(result);
        return result.Take(config.MaxCandidateSets).ToList();
    }

    private static bool SharedCenterAssignmentExists(IReadOnlyList<int> originCounts,
                                                      int leftDestinations, int centerDestinations, int rightDestinations)
    {
        int totalDestinations = leftDestinations + centerDestinations + rightDestinations;
        if (totalDestinations != originCounts.Sum()) return false;
        return originCounts[0] <= leftDestinations + centerDestinations
               && originCounts[2] <= rightDestinations + centerDestinations;
    }

    private static void AssignWithSharedCenter(ManiaHitObject[] notes, int[] selected, int keys,
                                               RandomState state, SeededRandom rng)
    {
        var noteOrder = Enumerable.Range(0, notes.Length).ToList();
        rng.Shuffle(noteOrder);
        noteOrder = noteOrder.OrderBy(index => DualStageLayout.StageOf(notes[index].OriginalColumn, keys) == 1 ? 1 : 0)
                             .ToList();
        var available = selected.ToList();
        rng.Shuffle(available);
        var assignments = Enumerable.Repeat(-1, notes.Length).ToArray();

        if (!Match(0))
            throw new InvalidOperationException("No se pudo asignar el centro compartido sin cruzar stages.");
        Assign(notes, assignments, state);
        return;

        bool Match(int position)
        {
            if (position == noteOrder.Count) return true;
            int noteIndex = noteOrder[position];
            int originStage = DualStageLayout.StageOf(notes[noteIndex].OriginalColumn, keys);
            for (int index = 0; index < available.Count; index++)
            {
                int destination = available[index];
                int destinationStage = DualStageLayout.StageOf(destination, keys);
                if (originStage != 1 && destinationStage != 1 && destinationStage != originStage)
                    continue;
                available.RemoveAt(index);
                assignments[noteIndex] = destination;
                if (Match(position + 1)) return true;
                assignments[noteIndex] = -1;
                available.Insert(index, destination);
            }
            return false;
        }
    }

    private List<int[]> CandidateSetsForRegion(int[] regionColumns, int noteCount, RandomState state,
                                                int time, int threshold, SeededRandom rng)
    {
        int[] preferred = regionColumns.Where(column => !state.ActiveLongNotes.ContainsKey(column)).ToArray();
        int[] endingNow = state.LongNotesEndingAt(time).Intersect(regionColumns).ToArray();
        bool requiresTailReuse = noteCount > preferred.Length;
        int[] available = requiresTailReuse ? preferred.Concat(endingNow).ToArray() : preferred;
        if (noteCount > available.Length)
            return new List<int[]>();

        // Tail columns at this exact timestamp are a last-resort fallback.
        if (requiresTailReuse)
        {
            int neededTailColumns = noteCount - preferred.Length;
            return GenerateCombinations(endingNow, neededTailColumns, rng)
                .Select(tails => preferred.Concat(tails).OrderBy(column => column).ToArray())
                .ToList();
        }

        int[] primary = available.Where(column => (long)time - state.LastNoteTime[column] > threshold).ToArray();
        if (primary.Length >= noteCount)
            return GenerateCombinations(primary, noteCount, rng);

        int needed = noteCount - primary.Length;
        int[] inferior = available.Except(primary).OrderBy(column => state.LastNoteTime[column]).ToArray();
        long cutoff = state.LastNoteTime[inferior[Math.Min(needed - 1, inferior.Length - 1)]];
        int[] oldest = inferior.Where(column => state.LastNoteTime[column] <= cutoff).ToArray();
        return GenerateCombinations(oldest, needed, rng)
            .Select(extra => primary.Concat(extra).OrderBy(column => column).ToArray())
            .ToList();
    }

    private List<int[]> CombineStageCandidates(IReadOnlyList<List<int[]>> stages, SeededRandom rng)
    {
        if (stages.Any(stage => stage.Count == 0)) return new List<int[]>();
        long total = 1;
        foreach (List<int[]> stage in stages)
            total = total > config.MaxCandidateSets / stage.Count ? config.MaxCandidateSets + 1L : total * stage.Count;

        if (total <= config.MaxCandidateSets)
        {
            var combinations = new List<int[]>((int)total);
            Build(0, new List<int>());
            return combinations;

            void Build(int index, List<int> columns)
            {
                if (index == stages.Count)
                {
                    combinations.Add(columns.OrderBy(column => column).ToArray());
                    return;
                }
                foreach (int[] candidate in stages[index])
                {
                    columns.AddRange(candidate);
                    Build(index + 1, columns);
                    columns.RemoveRange(columns.Count - candidate.Length, candidate.Length);
                }
            }
        }

        var sampled = new Dictionary<string, int[]>(StringComparer.Ordinal);
        long attempts = 0;
        long attemptLimit = Math.Min((long)config.MaxCandidateSets * 20L, int.MaxValue);
        while (sampled.Count < config.MaxCandidateSets && attempts++ < attemptLimit)
        {
            int[] candidate = stages.SelectMany(stage => stage[rng.NextInt(stage.Count)])
                                    .OrderBy(column => column).ToArray();
            sampled.TryAdd(string.Join(',', candidate), candidate);
        }
        return sampled.Values.ToList();
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

        long total = CombinationMath.CountBounded(columns.Length, count, config.MaxCandidateSets + 1L);
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
        long attempts = 0;
        long attemptLimit = Math.Min((long)config.MaxCandidateSets * 20L, int.MaxValue);
        while (sampled.Count < config.MaxCandidateSets && attempts++ < attemptLimit)
        {
            var shuffled = columns.ToList();
            rng.Shuffle(shuffled);
            int[] candidate = shuffled.Take(count).OrderBy(c => c).ToArray();
            sampled.TryAdd(string.Join(',', candidate), candidate);
        }
        return sampled.Values.ToList();
    }

}
