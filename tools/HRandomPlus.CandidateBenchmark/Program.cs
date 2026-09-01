using System.Diagnostics;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Randomization;

const int repetitions = 5;
const int keys = 18;
const int groups = 64;
const long seed = 246813579;

foreach (int maximum in new[] { 4096, 8192, 16384 })
{
    var elapsed = new List<double>();
    var allocated = new List<long>();
    for (int run = 0; run < repetitions + 1; run++)
    {
        List<ManiaHitObject> map = CreateMap();
        var config = new HRandomConfig
        {
            DynamicThreshold = false,
            MinThresholdMs = 0,
            BaseThresholdMs = 0,
            MaxThresholdMs = 0,
            MaxCandidateSets = maximum,
            WeightedTopCandidates = Math.Min(4096, maximum)
        };
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        _ = new HRandomPlusEngine(config).Randomize(map, keys, seed);
        watch.Stop();
        if (run == 0) continue; // warm-up
        elapsed.Add(watch.Elapsed.TotalMilliseconds);
        allocated.Add(GC.GetAllocatedBytesForCurrentThread() - before);
    }
    elapsed.Sort();
    allocated.Sort();
    Console.WriteLine($"{maximum}: median={elapsed[elapsed.Count / 2]:0.###} ms; " +
                      $"min={elapsed[0]:0.###} ms; max={elapsed[^1]:0.###} ms; " +
                      $"allocated-median={allocated[allocated.Count / 2]} bytes");
}

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
