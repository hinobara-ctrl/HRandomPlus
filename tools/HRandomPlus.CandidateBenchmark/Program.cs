using System.Diagnostics;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Randomization;

const int repetitions = 5;
const int keys = 18;
const int groups = 64;
const long seed = 246813579;

BenchmarkScenario[] scenarios =
{
    new("user-4096-top12", 4096, 12, "supported-user"),
    new("user-8192-top12", 8192, 12, "supported-user"),
    new("diagnostic-16384-top12", 16384, 12, "out-of-contract"),
    new("stress-4096-top4096", 4096, 4096, "stress"),
    new("stress-8192-top4096", 8192, 4096, "stress")
};

Console.WriteLine("scenario,category,max_candidates,top_candidates,candidates_per_timestamp,groups,iterations,total_ms,mean_ms,median_ms,allocated_total_bytes,allocated_mean_bytes,gen0,gen1,gen2");
foreach (BenchmarkScenario scenario in scenarios)
{
    RunOnce(scenario); // JIT and data-path warm-up.

    var elapsed = new double[repetitions];
    var allocated = new long[repetitions];
    int[] collections = new int[3];
    for (int run = 0; run < repetitions; run++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        int gen0 = GC.CollectionCount(0);
        int gen1 = GC.CollectionCount(1);
        int gen2 = GC.CollectionCount(2);
        List<ManiaHitObject> map = CreateMap();
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        _ = new HRandomPlusEngine(CreateConfig(scenario)).Randomize(map, keys, seed);
        watch.Stop();
        elapsed[run] = watch.Elapsed.TotalMilliseconds;
        allocated[run] = GC.GetAllocatedBytesForCurrentThread() - before;
        collections[0] += GC.CollectionCount(0) - gen0;
        collections[1] += GC.CollectionCount(1) - gen1;
        collections[2] += GC.CollectionCount(2) - gen2;
    }

    double[] orderedElapsed = elapsed.OrderBy(value => value).ToArray();
    Console.WriteLine(string.Join(',',
        scenario.Name,
        scenario.Category,
        scenario.Maximum,
        scenario.Top,
        scenario.Maximum,
        groups,
        repetitions,
        elapsed.Sum().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        elapsed.Average().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        orderedElapsed[orderedElapsed.Length / 2].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        allocated.Sum(),
        (long)allocated.Average(),
        collections[0],
        collections[1],
        collections[2]));
}

static void RunOnce(BenchmarkScenario scenario)
{
    List<ManiaHitObject> map = CreateMap();
    _ = new HRandomPlusEngine(CreateConfig(scenario)).Randomize(map, keys, seed);
}

static HRandomConfig CreateConfig(BenchmarkScenario scenario) => new()
{
    DynamicThreshold = false,
    MinThresholdMs = 0,
    BaseThresholdMs = 0,
    MaxThresholdMs = 0,
    MaxCandidateSets = scenario.Maximum,
    WeightedTopCandidates = scenario.Top
};

static List<ManiaHitObject> CreateMap()
{
    var result = new List<ManiaHitObject>();
    int line = 0;
    for (int group = 0; group < groups; group++)
    for (int column = 0; column < keys / 2; column++)
    {
        int x = ManiaHitObject.ColumnToX(column, keys);
        int time = group * 125;
        result.Add(ManiaHitObject.Parse($"{x},192,{time},1,0,0:0:0:0:", line++, keys));
    }
    return result;
}

internal sealed record BenchmarkScenario(string Name, int Maximum, int Top, string Category);
