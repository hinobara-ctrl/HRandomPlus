using System.Text;
using HRandomPlus.Beatmaps;

namespace HRandomPlus.Tests;

internal static class TestBeatmaps
{
    public static byte[] Mania(int keys, IEnumerable<string> hitObjects, string version = "Test")
        => Encoding.UTF8.GetBytes(
            "osu file format v14\n\n" +
            "[General]\n" +
            "AudioFilename: audio.mp3\n" +
            "Mode: 3\n\n" +
            "[Metadata]\n" +
            "Title:Unit Test\n" +
            "Artist:Test\n" +
            "Creator:Codex\n" +
            $"Version:{version}\n\n" +
            "[Difficulty]\n" +
            "HPDrainRate:5\n" +
            $"CircleSize:{keys}\n" +
            "OverallDifficulty:8\n\n" +
            "[TimingPoints]\n" +
            "0,500,4,2,0,100,1,0\n\n" +
            "[HitObjects]\n" +
            string.Join("\n", hitObjects) + "\n");

    public static byte[] Standard()
        => Encoding.UTF8.GetBytes("osu file format v14\n\n[General]\nMode:0\n\n[Metadata]\nVersion:Standard\n\n[Difficulty]\nCircleSize:4\n\n[HitObjects]\n256,192,1000,1,0,0:0:0:0:\n");

    public static string Note(int keys, int column, int time)
        => $"{ManiaHitObject.ColumnToX(column, keys)},192,{time},1,0,0:0:0:0:";

    public static string LongNote(int keys, int column, int start, int end)
        => $"{ManiaHitObject.ColumnToX(column, keys)},192,{start},128,0,{end}:0:0:0:0:";
}
