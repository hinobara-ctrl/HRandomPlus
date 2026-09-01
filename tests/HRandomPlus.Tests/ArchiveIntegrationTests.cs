using System.IO.Compression;
using System.Reflection;
using System.Text;
using HRandomPlus.Archives;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;

namespace HRandomPlus.Tests;

public class ArchiveIntegrationTests
{
    [Theory]
    [InlineData(true, "none")]
    [InlineData(true, "io")]
    [InlineData(true, "unauthorized")]
    [InlineData(false, "none")]
    [InlineData(false, "io")]
    [InlineData(false, "unauthorized")]
    public void CleanupIsBestEffortAndNeverMasksTheArchiveResult(bool validArchive, string cleanupFailure)
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusCleanup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string input = Path.Combine(root, "input.osz");
        string output = Path.Combine(root, "output.osz");
        using (ZipArchive archive = ZipFile.Open(input, ZipArchiveMode.Create))
        {
            if (validArchive)
                Add(archive, "map.osu", TestBeatmaps.Mania(4,
                    Enumerable.Range(0, 8).Select(i => TestBeatmaps.Note(4, i % 4, 1000 + i * 100))));
            else
                Add(archive, "audio.mp3", new byte[] { 1, 2, 3 });
        }

        var cleaner = new InjectedCleanupFailure(cleanupFailure);
        var warnings = new List<string>();
        Exception? operationFailure = null;
        try
        {
            try
            {
                _ = new OsuArchive(cleaner, warnings.Add).Process(input, output,
                    new HRandomConfig { Seed = 1 }, Array.Empty<string>(), false);
            }
            catch (Exception ex)
            {
                operationFailure = ex;
            }

            Assert.Equal(validArchive, File.Exists(output));
            if (validArchive) Assert.True(operationFailure is null);
            else Assert.True(operationFailure is InvalidDataException &&
                             operationFailure.Message.Contains("no contiene archivos .osu", StringComparison.Ordinal));
            Assert.Equal(cleanupFailure == "none" ? 0 : 1, warnings.Count);
            Assert.Equal(1, cleaner.Calls);
        }
        finally
        {
            foreach (string path in cleaner.Paths)
                if (Directory.Exists(path)) Directory.Delete(path, true);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DefaultCleanupAcceptsAnAlreadyMissingTemporaryDirectory()
    {
        string parent = Path.Combine(Path.GetTempPath(), "HRandomPlusCleanup", Guid.NewGuid().ToString("N"));
        new ArchiveTemporaryDirectoryCleaner().Delete(Path.Combine(parent, "missing"), parent);
    }

    [Fact]
    public void DefaultCleanupRejectsAPathOutsideItsExpectedParent()
    {
        string parent = Path.Combine(Path.GetTempPath(), "HRandomPlusCleanup", Guid.NewGuid().ToString("N"));
        bool rejected = false;
        try { new ArchiveTemporaryDirectoryCleaner().Delete(Path.GetTempPath(), parent); }
        catch (InvalidDataException) { rejected = true; }
        Assert.True(rejected);
    }

    [Fact]
    public void DirectAndArchiveInputsApplyTheSamePlayableStructureValidation()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusValidation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] invalid = TestBeatmaps.Mania(4, new[]
            {
                TestBeatmaps.Note(4, 0, 1000),
                TestBeatmaps.Note(4, 0, 1000)
            });
            string direct = Path.Combine(root, "invalid.osu");
            File.WriteAllBytes(direct, invalid);
            string archivePath = Path.Combine(root, "invalid.osz");
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) Add(archive, "invalid.osu", invalid);

            Exception? directFailure = Capture(() => new BeatmapGenerationService().Generate(
                direct, new HRandomConfig { Seed = 1 }, null));
            Exception? archiveFailure = Capture(() => new OsuArchive().Process(
                archivePath, Path.Combine(root, "invalid-output.osz"), new HRandomConfig { Seed = 1 }, Array.Empty<string>(), false));

            Assert.True(directFailure is InvalidDataException);
            Assert.True(archiveFailure is InvalidDataException);
            Assert.Equal(directFailure!.Message, archiveFailure!.Message);
            Assert.True(!Directory.EnumerateFiles(root, "*output*", SearchOption.TopDirectoryOnly).Any());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void EquivalentValidDirectAndArchiveInputsBothGenerateOutputs()
    {
        string root = Path.Combine(Path.GetTempPath(), "HRandomPlusValidation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] valid = TestBeatmaps.Mania(4,
                Enumerable.Range(0, 8).Select(i => TestBeatmaps.Note(4, i % 4, 1000 + i * 100)));
            string direct = Path.Combine(root, "valid.osu");
            File.WriteAllBytes(direct, valid);
            string archivePath = Path.Combine(root, "valid.osz");
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) Add(archive, "valid.osu", valid);

            GenerationResult directResult = new BeatmapGenerationService().Generate(
                direct, new HRandomConfig { Seed = 1 }, null);
            string archiveOutput = Path.Combine(root, "valid-output.osz");
            ArchiveReport archiveResult = new OsuArchive().Process(
                archivePath, archiveOutput, new HRandomConfig { Seed = 1 }, Array.Empty<string>(), false);

            Assert.True(File.Exists(directResult.OutputPath));
            Assert.True(File.Exists(archiveOutput));
            Assert.Single(archiveResult.Difficulties);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

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

    private static Exception? Capture(Action action)
    {
        try { action(); return null; }
        catch (Exception ex) { return ex; }
    }

    private sealed class InjectedCleanupFailure(string failure) : IArchiveTemporaryDirectoryCleaner
    {
        public int Calls { get; private set; }
        public List<string> Paths { get; } = new();

        public void Delete(string temporaryPath, string expectedParent)
        {
            Calls++;
            Paths.Add(temporaryPath);
            if (failure == "io") throw new IOException("injected cleanup failure");
            if (failure == "unauthorized") throw new UnauthorizedAccessException("injected cleanup failure");
            new ArchiveTemporaryDirectoryCleaner().Delete(temporaryPath, expectedParent);
        }
    }
}
