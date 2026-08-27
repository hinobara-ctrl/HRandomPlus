using HRandomPlus.Archives;
using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Integration.Tosu;

namespace HRandomPlus.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                PrintHelp();
                return 0;
            }
            return args[0] == "--diagnose"
                ? await DiagnoseAsync(args[1..]).ConfigureAwait(false)
                : ProcessArchive(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> DiagnoseAsync(string[] args)
    {
        AppSettings settings = new SettingsStore().Load();
        string host = settings.TosuHost;
        int port = settings.TosuPort;
        string? osuRoot = OperatingSystem.IsWindows() ? settings.OsuPath : settings.LinuxOsuPath;
        for (int i = 0; i < args.Length; i++)
        {
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException($"Falta valor para {args[i - 1]}.");
            switch (args[i])
            {
                case "--host": host = Value(); break;
                case "--port": port = int.Parse(Value()); break;
                case "--osu-path": osuRoot = Value(); break;
                default: throw new ArgumentException($"Opción de diagnóstico desconocida: {args[i]}");
            }
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(750) };
        var source = new TosuBeatmapSource(new TosuClient(http, host, port), new BeatmapPathResolver(), () => osuRoot);
        BeatmapSourceResult result = await source.GetCurrentAsync().ConfigureAwait(false);
        Console.WriteLine($"tosu: http://{host}:{port}/json/v2");
        Console.WriteLine($"estado: {result.Status}");
        if (result.Selection is null) return result.IsAvailable ? 4 : 3;

        var map = result.Selection.Beatmap;
        Console.WriteLine($"beatmap: {map.Artist} - {map.Title} [{map.Difficulty}]");
        Console.WriteLine($"mapper: {map.Creator}");
        Console.WriteLine($"id/set: {map.Id}/{map.SetId}");
        Console.WriteLine($"checksum: {map.Checksum ?? "(sin checksum)"}");
        Console.WriteLine($"archivo: {result.Selection.NativePath}");
        return 0;
    }

    private static int ProcessArchive(string[] args)
    {
        string input = args[0];
        string? output = null;
        string? configPath = null;
        string? reportPath = null;
        long? seed = null;
        bool overwrite = false;
        var difficulties = new List<string>();
        for (int i = 1; i < args.Length; i++)
        {
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException($"Falta valor para {args[i - 1]}.");
            switch (args[i])
            {
                case "--output": case "-o": output = Value(); break;
                case "--config": configPath = Value(); break;
                case "--report": reportPath = Value(); break;
                case "--seed": seed = long.Parse(Value()); break;
                case "--difficulty": case "-d": difficulties.Add(Value()); break;
                case "--overwrite": overwrite = true; break;
                default: throw new ArgumentException($"Opción desconocida: {args[i]}");
            }
        }

        var config = configPath is null ? new HRandomConfig() : HRandomConfig.Load(configPath);
        if (seed.HasValue) config.Seed = seed;
        output ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(input))!, Path.GetFileNameWithoutExtension(input) + "_HRandom.osz");
        reportPath ??= Path.ChangeExtension(output, ".report.json");
        ArchiveReport report = new OsuArchive().Process(input, output, config, difficulties, overwrite);
        OsuArchive.SaveReport(report, reportPath);
        Console.WriteLine($"OSZ generado: {report.Output}");
        Console.WriteLine($"Seed: {report.Seed}");
        Console.WriteLine($"Reporte: {reportPath}");
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("HRandomPlus CLI");
        Console.WriteLine("  HRandomPlus.Cli --diagnose [--host 127.0.0.1] [--port 24050] [--osu-path RUTA]");
        Console.WriteLine("  HRandomPlus.Cli <beatmap.osz> [-o salida.osz] [--config config.json] [--seed N]");
    }
}
