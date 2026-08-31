using System.IO.Compression;
using System.Reflection;
using System.Text;
using HRandomPlus.Archives;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;

namespace HRandomPlus.Tests;

public class ArchiveIntegrationTests
{
    [Fact]
    public void ResolveInsideUsesPlatformPathCaseSemantics()
    {
        string parent = Path.Combine(Path.GetTempPath(), "HRandomPlusArchiveCase", Guid.NewGuid().ToString("N"));
        string root = Path.Combine(parent, "case-root");
        string differentlyCasedSibling = Path.Combine(parent, "CASE-ROOT", "outside.osu");
        string relative = Path.GetRelativePath(root, differentlyCasedSibling);
        MethodInfo method = typeof(OsuArchive).GetMethod("ResolveInside", BindingFlags.NonPublic | BindingFlags.Static)!;

        try
        {
            string resolved = (string)method.Invoke(null, new object[] { root, relative })!;
            Assert.True(OperatingSystem.IsWindows());
            Assert.True(resolved.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
        }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException)
        {
            Assert.True(!OperatingSystem.IsWindows());
        }
    }

    [Fact]
    public void ArchiveHashAndValidationPreserveCaseDistinctZipEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusArchiveCase", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string archivePath = Path.Combine(root, "case.osz");
        try
        {
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                Add(archive, "hit.wav", new byte[] { 1 });
                Add(archive, "Hit.wav", new byte[] { 2 });
            }

            MethodInfo hashMethod = typeof(OsuArchive).GetMethod("HashArchiveEntries", BindingFlags.NonPublic | BindingFlags.Static)!;
            var hashes = (Dictionary<string, string>)hashMethod.Invoke(null, new object[] { archivePath })!;
            Assert.Equal(2, hashes.Count);
            Assert.True(hashes.ContainsKey("hit.wav"));
            Assert.True(hashes.ContainsKey("Hit.wav"));

            MethodInfo validateMethod = typeof(OsuArchive).GetMethod("ValidateArchive", BindingFlags.NonPublic | BindingFlags.Static)!;
            validateMethod.Invoke(null, new object[]
            {
                archivePath,
                hashes,
                new HashSet<string>(StringComparer.Ordinal),
                new List<(OsuBeatmapDocument Original, string OutputRelativePath)>()
            });
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void RejectsArchiveWithExcessiveEntryCountBeforeExtraction()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusArchiveLimits", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string input = Path.Combine(root, "too-many.osz");
        string output = Path.Combine(root, "output.osz");
        try
        {
            using (ZipArchive archive = ZipFile.Open(input, ZipArchiveMode.Create))
                for (int index = 0; index <= 10_000; index++) archive.CreateEntry($"empty/{index}.bin");
            bool rejected = false;
            try
            {
                _ = new OsuArchive().Process(input, output, new HRandomConfig { Seed = 1 }, Array.Empty<string>(), false);
            }
            catch (InvalidDataException ex)
            {
                rejected = ex.Message.Contains("10", StringComparison.Ordinal);
            }
            Assert.True(rejected);
            Assert.True(!File.Exists(output));
        }
        finally { Directory.Delete(root, true); }
    }

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
