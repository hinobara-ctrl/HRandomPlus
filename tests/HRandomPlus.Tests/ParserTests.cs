using System.Text;
using HRandomPlus.Beatmaps;

namespace HRandomPlus.Tests;

public class ParserTests
{
    [Fact]
    public void ParsesColumnsAndPreservesAllFieldsExceptX()
    {
        byte[] bytes = TestBeatmaps.Mania(4, new[]
        {
            "64,192,1000,1,12,2:3:4:80:hit.wav",
            "448,192,1200,128,8,1800:3:2:7:55:tail.wav"
        }, "Insane");
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse("map.osu", bytes);

        Assert.Equal(3, document.Mode);
        Assert.Equal(4, document.Keys);
        Assert.Equal(new[] { 0, 3 }, document.HitObjects.Select(h => h.OriginalColumn));
        Assert.Equal(1800, document.HitObjects[1].EndTime);

        document.HitObjects[0].AssignedColumn = 2;
        document.HitObjects[1].AssignedColumn = 1;
        document.ApplyObjects();
        document.AppendVersionSuffix(" H-RANDOM+");
        OsuBeatmapDocument reparsed = OsuBeatmapDocument.Parse("map.osu", document.ToBytes());

        Assert.Equal("Insane H-RANDOM+", reparsed.Version);
        Assert.Equal(new[] { 2, 1 }, reparsed.HitObjects.Select(h => h.OriginalColumn));
        Assert.True(document.HitObjects[0].NonPositionFieldsEqual(reparsed.HitObjects[0]));
        Assert.True(document.HitObjects[1].NonPositionFieldsEqual(reparsed.HitObjects[1]));
        Assert.Contains("[TimingPoints]\n0,500,4,2,0,100,1,0", Encoding.UTF8.GetString(document.ToBytes()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    public void CenterCoordinateRoundTripsEveryColumn(int keys)
    {
        for (int column = 0; column < keys; column++)
        {
            int x = ManiaHitObject.ColumnToX(column, keys);
            var document = OsuBeatmapDocument.Parse("map.osu",
                TestBeatmaps.Mania(keys, new[] { $"{x},192,1000,1,0,0:0:0:0:" }));
            Assert.Equal(column, document.HitObjects[0].OriginalColumn);
        }
    }

    [Fact]
    public void ReadsDistinctUninheritedBpmsInTimingOrder()
    {
        string text = Encoding.UTF8.GetString(TestBeatmaps.Mania(4, new[] { TestBeatmaps.Note(4, 0, 1000) }))
            .Replace("0,500,4,2,0,100,1,0",
                "0,500,4,2,0,100,1,0\n1000,-50,4,2,0,100,0,0\n2000,333.333333,4,2,0,100,1,0\n3000,500,4,2,0,100,1,0",
                StringComparison.Ordinal);
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse("timing.osu", Encoding.UTF8.GetBytes(text));

        IReadOnlyList<double> bpms = document.GetBpms();
        Assert.Equal(2, bpms.Count);
        Assert.InRange(bpms[0], 119.999, 120.001);
        Assert.InRange(bpms[1], 179.999, 180.001);
    }

    [Fact]
    public void CalculatesMillisecondsForCommonBeatSnaps()
    {
        Assert.Equal(new[] { 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64 },
            BeatSnapReference.CommonDivisors);
        Assert.InRange(BeatSnapReference.Milliseconds(180, 1), 333.332, 333.334);
        Assert.InRange(BeatSnapReference.Milliseconds(180, 4), 83.332, 83.334);
        Assert.InRange(BeatSnapReference.Milliseconds(180, 8), 41.666, 41.667);
    }
}
