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
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
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
}
