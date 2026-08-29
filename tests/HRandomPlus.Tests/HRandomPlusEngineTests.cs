using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Randomization;
using HRandomPlus.Validation;

namespace HRandomPlus.Tests;

public class HRandomPlusEngineTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void RandomizesStreamsJacksJumpstreamsHandstreamsChordjacksAndDensePatterns(int keys)
    {
        var lines = new List<string>();
        int time = 1000;

        // Stream.
        for (int i = 0; i < 16; i++, time += 80)
            lines.Add(TestBeatmaps.Note(keys, i % keys, time));
        // Jack.
        for (int i = 0; i < 10; i++, time += 55)
            lines.Add(TestBeatmaps.Note(keys, 0, time));
        // Jumpstream / handstream.
        for (int i = 0; i < 10; i++, time += 90)
        {
            lines.Add(TestBeatmaps.Note(keys, i % 2 == 0 ? 0 : keys - 1, time));
            if (keys >= 5)
                lines.Add(TestBeatmaps.Note(keys, keys / 2, time));
        }
        // Chordjack and dense chords.
        int chordSize = Math.Min(keys, 4);
        for (int i = 0; i < 8; i++, time += 100)
            for (int column = 0; column < chordSize; column++)
                lines.Add(TestBeatmaps.Note(keys, column, time));

        OsuBeatmapDocument document = OsuBeatmapDocument.Parse("patterns.osu", TestBeatmaps.Mania(keys, lines));
        var config = new HRandomConfig { DynamicThreshold = true };
        var result = new HRandomPlusEngine(config).Randomize(document.HitObjects, keys, 123456789);

        BeatmapValidator.ValidatePlayableStructure(document.HitObjects, keys, assigned: true);
        Assert.Equal(lines.Count, result.After.TotalNotes);
        Assert.Equal(result.Before.Chords, result.After.Chords);
        Assert.All(document.HitObjects, h => Assert.InRange(h.AssignedColumn, 0, keys - 1));
    }

    [Fact]
    public void SameSeedProducesIdenticalOutput()
    {
        string[] lines = Enumerable.Range(0, 50).Select(i => TestBeatmaps.Note(7, i % 7, 1000 + i * 73)).ToArray();
        OsuBeatmapDocument first = OsuBeatmapDocument.Parse("a.osu", TestBeatmaps.Mania(7, lines));
        OsuBeatmapDocument second = OsuBeatmapDocument.Parse("a.osu", TestBeatmaps.Mania(7, lines));
        var config = new HRandomConfig();

        new HRandomPlusEngine(config).Randomize(first.HitObjects, 7, 987654321);
        new HRandomPlusEngine(config).Randomize(second.HitObjects, 7, 987654321);

        Assert.Equal(first.HitObjects.Select(h => h.AssignedColumn), second.HitObjects.Select(h => h.AssignedColumn));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentAssignments()
    {
        string[] lines = Enumerable.Range(0, 80).Select(i => TestBeatmaps.Note(7, i % 7, 1000 + i * 73)).ToArray();
        OsuBeatmapDocument first = OsuBeatmapDocument.Parse("a.osu", TestBeatmaps.Mania(7, lines));
        OsuBeatmapDocument second = OsuBeatmapDocument.Parse("a.osu", TestBeatmaps.Mania(7, lines));
        var config = new HRandomConfig();

        new HRandomPlusEngine(config).Randomize(first.HitObjects, 7, 111);
        new HRandomPlusEngine(config).Randomize(second.HitObjects, 7, 222);

        Assert.True(!first.HitObjects.Select(h => h.AssignedColumn)
            .SequenceEqual(second.HitObjects.Select(h => h.AssignedColumn)));
    }

    [Fact]
    public void LongNotesRemainLockedAndChordsAvoidTheirColumns()
    {
        string[] lines =
        {
            TestBeatmaps.LongNote(7, 0, 1000, 2000),
            TestBeatmaps.LongNote(7, 1, 1000, 1800),
            TestBeatmaps.Note(7, 2, 1100),
            TestBeatmaps.Note(7, 3, 1100),
            TestBeatmaps.Note(7, 4, 1500),
            TestBeatmaps.Note(7, 5, 1800),
            TestBeatmaps.Note(7, 6, 2000)
        };
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse("ln.osu", TestBeatmaps.Mania(7, lines));
        new HRandomPlusEngine(new HRandomConfig()).Randomize(document.HitObjects, 7, 42);

        BeatmapValidator.ValidatePlayableStructure(document.HitObjects, 7, assigned: true);
        int[] locked = document.HitObjects.Take(2).Select(h => h.AssignedColumn).ToArray();
        Assert.All(document.HitObjects.Where(h => h.StartTime == 1100),
            h => Assert.DoesNotContain(h.AssignedColumn, locked));
        Assert.All(document.HitObjects.Where(h => h.StartTime is > 1000 and < 1800),
            h => Assert.DoesNotContain(h.AssignedColumn, locked));
    }

    [Fact]
    public void FastJackSequenceIsNotLeftAsAJack()
    {
        string[] lines = Enumerable.Range(0, 20).Select(i => TestBeatmaps.Note(4, 0, 1000 + i * 50)).ToArray();
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse("jacks.osu", TestBeatmaps.Mania(4, lines));
        RandomizationResult result = new HRandomPlusEngine(new HRandomConfig { DynamicThreshold = false })
            .Randomize(document.HitObjects, 4, 99);

        Assert.Equal(19, result.Before.QuickJacks);
        Assert.Equal(0, result.After.QuickJacks);
    }

    [Fact]
    public void AvoidsLongNoteTailHeadContactWhenAnotherColumnExists()
    {
        string[] lines =
        {
            TestBeatmaps.LongNote(4, 0, 1000, 2000),
            TestBeatmaps.Note(4, 1, 2000)
        };

        for (long seed = 0; seed < 128; seed++)
        {
            OsuBeatmapDocument document = OsuBeatmapDocument.Parse("tail-contact.osu", TestBeatmaps.Mania(4, lines));
            new HRandomPlusEngine(new HRandomConfig()).Randomize(document.HitObjects, 4, seed);
            Assert.True(document.HitObjects[0].AssignedColumn != document.HitObjects[1].AssignedColumn,
                $"La seed {seed} creó un contacto cola/cabeza evitable.");
        }
    }

    [Fact]
    public void AllowsTailReuseOnlyWhenRequiredForSolvability()
    {
        var lines = new List<string> { TestBeatmaps.LongNote(4, 0, 1000, 2000) };
        for (int column = 0; column < 4; column++)
            lines.Add(TestBeatmaps.Note(4, column, 2000));

        OsuBeatmapDocument document = OsuBeatmapDocument.Parse("required-tail.osu", TestBeatmaps.Mania(4, lines));
        new HRandomPlusEngine(new HRandomConfig()).Randomize(document.HitObjects, 4, 7);

        Assert.Equal(4, document.HitObjects.Where(h => h.StartTime == 2000)
                                           .Select(h => h.AssignedColumn).Distinct().Count());
    }
}
