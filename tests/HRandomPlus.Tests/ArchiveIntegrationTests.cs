using System.IO.Compression;
using System.Text;
using HRandomPlus.Archives;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;

namespace HRandomPlus.Tests;

public class ArchiveIntegrationTests
{
    [Fact]
    public void ProducesValidOszAndPreservesEveryUnmodifiedResource()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string input = Path.Combine(root, "beatmap.osz");
            string output = Path.Combine(root, "beatmap_HRandom.osz");
            byte[] audio = Enumerable.Range(0, 1024).Select(i => (byte)(i % 251)).ToArray();
            byte[] image = Encoding.UTF8.GetBytes("fake-image-data");
            byte[] mania = TestBeatmaps.Mania(4, Enumerable.Range(0, 20).Select(i => TestBeatmaps.Note(4, 0, 1000 + i * 60)), "Insane");
            byte[] standard = TestBeatmaps.Standard();

            using (ZipArchive archive = ZipFile.Open(input, ZipArchiveMode.Create))
            {
                Add(archive, "audio.mp3", audio);
                Add(archive, "bg/image.png", image);
                Add(archive, "Artist - Song [Insane].osu", mania);
                Add(archive, "Artist - Song [Standard].osu", standard);
            }

            ArchiveReport report = new OsuArchive().Process(input, output,
                new HRandomConfig { Seed = 123, RenameDifficulty = true }, Array.Empty<string>(), false);

            Assert.True(File.Exists(output));
            Assert.Equal(123, report.Seed);
            Assert.Single(report.Difficulties);
            using ZipArchive result = ZipFile.OpenRead(output);
            Assert.Equal(audio, Read(result, "audio.mp3"));
            Assert.Equal(image, Read(result, "bg/image.png"));
            Assert.Equal(standard, Read(result, "Artist - Song [Standard].osu"));
            ZipArchiveEntry generated = Assert.Single(result.Entries.Where(e => e.Name.EndsWith("H-RANDOM+.osu", StringComparison.Ordinal)));
            OsuBeatmapDocument parsed = OsuBeatmapDocument.Parse(generated.FullName, Read(result, generated.FullName));
            Assert.Equal("Insane H-RANDOM+", parsed.Version);
            Assert.Equal(mania.Length > 0 ? 20 : 0, parsed.HitObjects.Count);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static void Add(ZipArchive archive, string path, byte[] data)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using Stream stream = entry.Open();
        stream.Write(data);
    }

    private static byte[] Read(ZipArchive archive, string path)
    {
        using Stream stream = archive.GetEntry(path)!.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
