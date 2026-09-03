using System.Security.Cryptography;
using System.Globalization;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Randomization;

namespace HRandomPlus.Tests;

public sealed class DeterminismBaselineTests
{
    [Fact]
    public void RepresentativeReferenceOutputsRemainByteIdentical()
    {
        string[] actual = Cases().Select(GenerateHash).ToArray();
        string[] expected =
        {
            "4k-small-stream:73c02d24a82bc3431f64e7f594e59f2e9c9f0ef8033131c9b78bb7bdafc2f282",
            "7k-ln-dynamic-chords:b82d26bd5430822ceeffc3aed5d29b44c7e24bc3a75ed7c75b903aae11a0f711",
            "10k-dual-stage:db6a3b02b061bd369c3e6e82a30f145831af27966d178fb060c5976ebb4075a7",
            "11k-shared-center:76888e857d5b4c861cebb5e73739da593080a92c5733c166af4ada8a2001198b",
            "18k-large-dense:659d886f1b058598122c011f903e6f3ad9f290cea2278f7dfe421af6672ae19d"
        };
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RepresentativeCandidateScoresRemainIdentical()
    {
        var state = new RandomState(7, 24, 16);
        state.RecordGroup(1000, new[] { 0, 3 });
        state.RecordGroup(1100, new[] { 1, 4 });
        state.RecordGroup(1200, new[] { 0, 3 });
        state.RecordGroup(1300, new[] { 1, 4 });
        var scorer = new CandidateScorer(new HRandomConfig());
        string[] actual = new[]
        {
            new[] { 0, 3 },
            new[] { 2, 5 },
            new[] { 6 },
            new[] { 0, 4, 6 }
        }.Select(columns => scorer.ScoreSet(state, columns, 1400, 100)
            .ToString("R", CultureInfo.InvariantCulture)).ToArray();

        Assert.Equal(new[] { "-34.833333333333336", "92", "46.5", "-51.08333333333334" }, actual);
    }

    [Fact]
    public void BinaryCandidateIdentityPreservesFirstOccurrenceAndOrder()
    {
        int[][] input =
        {
            new[] { 0, 2, 17 },
            new[] { 1, 3 },
            new[] { 0, 2, 17 },
            new[] { 4 },
            new[] { 1, 3 }
        };
        var seen = new HashSet<ulong>();
        var ordered = new List<int[]>();
        foreach (int[] candidate in input)
            CandidateSetIdentity.TryAdd(seen, ordered, candidate);

        Assert.Equal(new[] { "0,2,17", "1,3", "4" }, ordered.Select(candidate => string.Join(',', candidate)));
    }

    [Fact]
    public void StableTopKMatchesStableFullSortIncludingTies()
    {
        double[] scores = { 10, -2, 10, 7, 10, 7, 3, 10, -2, 9 };
        int[] items = Enumerable.Range(0, scores.Length).ToArray();
        for (int count = 1; count <= scores.Length; count++)
        {
            int[] expected = items.OrderByDescending(index => scores[index]).Take(count).ToArray();
            int[] actual = StableTopK.Select(items, count, index => scores[index])
                                     .Select(entry => entry.Item).ToArray();
            Assert.Equal(expected, actual);
        }
    }

    private static string GenerateHash(ReferenceCase reference)
    {
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse(
            reference.Name + ".osu", TestBeatmaps.Mania(reference.Keys, reference.HitObjects, reference.Name));
        new HRandomPlusEngine(reference.Config).Randomize(document.HitObjects, reference.Keys, reference.Seed);
        document.ApplyObjects();
        return $"{reference.Name}:{Convert.ToHexString(SHA256.HashData(document.ToBytes())).ToLowerInvariant()}";
    }

    private static IEnumerable<ReferenceCase> Cases()
    {
        yield return new ReferenceCase("4k-small-stream", 4, 101,
            new HRandomConfig(),
            Enumerable.Range(0, 48).Select(index => TestBeatmaps.Note(4, index % 4, 1000 + index * 73)).ToArray());

        var sevenKey = new List<string>
        {
            TestBeatmaps.LongNote(7, 0, 1000, 2600),
            TestBeatmaps.LongNote(7, 6, 1100, 2300)
        };
        for (int group = 0; group < 12; group++)
        for (int column = 1; column <= 4; column++)
            sevenKey.Add(TestBeatmaps.Note(7, column, 1200 + group * 90));
        for (int column = 0; column < 7; column++)
            sevenKey.Add(TestBeatmaps.Note(7, column, 2600));
        yield return new ReferenceCase("7k-ln-dynamic-chords", 7, 987654321,
            new HRandomConfig { DynamicThreshold = true }, sevenKey);

        yield return new ReferenceCase("10k-dual-stage", 10, 20260901,
            new HRandomConfig { PreserveDualStages = true, DynamicThreshold = false },
            DenseChords(10, 6, 24, 82));

        yield return new ReferenceCase("11k-shared-center", 11, -20260901,
            new HRandomConfig { PreserveDualStages = true, DynamicThreshold = true },
            DenseChords(11, 7, 20, 95));

        yield return new ReferenceCase("18k-large-dense", 18, 246813579,
            new HRandomConfig
            {
                DynamicThreshold = false,
                MaxCandidateSets = HRandomConfig.DefaultMaxCandidateSets,
                WeightedTopCandidates = 12
            },
            DenseChords(18, 9, 32, 125));
    }

    private static IReadOnlyList<string> DenseChords(int keys, int chordSize, int groups, int spacing)
    {
        var result = new List<string>(chordSize * groups);
        for (int group = 0; group < groups; group++)
        for (int column = 0; column < chordSize; column++)
            result.Add(TestBeatmaps.Note(keys, (column + group) % keys, 1000 + group * spacing));
        return result;
    }

    private sealed record ReferenceCase(string Name, int Keys, long Seed,
                                        HRandomConfig Config, IReadOnlyList<string> HitObjects);
}
