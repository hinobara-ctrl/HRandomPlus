using HRandomPlus.Archives;
using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Integration.Linux;
using HRandomPlus.Integration.Tosu;
using HRandomPlus.Beatmaps;
using System.Globalization;

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
        AppSettings settings = new SettingsStore().LoadReadOnly();
        string host = settings.TosuHost;
        int port = settings.TosuPort;
        string? osuRoot = OperatingSystem.IsWindows() ? settings.OsuPath : settings.LinuxOsuPath;
        for (int i = 0; i < args.Length; i++)
        {
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException($"Falta valor para {args[i - 1]}.");
            switch (args[i])
            {
                case "--host": host = Value(); break;
                case "--port": port = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--osu-path": osuRoot = Value(); break;
                default: throw new ArgumentException($"Opción de diagnóstico desconocida: {args[i]}");
            }
        }

        string tosuUrl = $"http://{host}:{port}/json/v2";
        Console.WriteLine($"Platform: {PlatformName()}");
        Console.WriteLine($"Client: osu!stable");
        Console.WriteLine($"Desktop source: {(OperatingSystem.IsWindows() ? "Windows memory" : "tosu + osu-winello")}");
        Console.WriteLine($"Diagnostic source: tosu HTTP");
        Console.WriteLine($"Tosu URL: {tosuUrl}");

        string? locatedRoot = osuRoot;
        string winelloStatus = OperatingSystem.IsWindows() ? "Not available on Windows" : "Not detected";
        if (!OperatingSystem.IsWindows())
        {
            var locator = new WinelloLocator();
            if (string.IsNullOrWhiteSpace(locatedRoot)) locator.TryLocate(out locatedRoot, out winelloStatus);
            else winelloStatus = "Manual native osu! path configured";
        }
        Console.WriteLine($"Winello status: {DiagnosticPathRedactor.Redact(winelloStatus)}");
        Console.WriteLine($"osu root: {DisplayPath(locatedRoot, "Not detected")}");
        string? songsPath = ResolveSongsPath(locatedRoot);
        Console.WriteLine($"Songs path: {DisplayPath(songsPath, "Not resolved")}");

        using var http = new HttpClient();
        using var client = new TosuClient(http, host, port);
        TosuResult tosu = await client.GetCurrentAsync().ConfigureAwait(false);
        Console.WriteLine($"Tosu status: {tosu.Status}");
        if (tosu.Snapshot is null)
        {
            Console.WriteLine("Current beatmap: Not detected");
            Console.WriteLine("Resolved .osu path: Not resolved");
            Console.WriteLine("Exists: No");
            Console.WriteLine($"Output path: {DiagnosticPathRedactor.Redact(AppPaths.OutputDirectory)}");
            Console.WriteLine("Output writable: Not verified (read-only diagnostic)");
            return tosu.IsAvailable ? 4 : 3;
        }

        BeatmapInfo map = tosu.Snapshot.Beatmap;
        Console.WriteLine($"Current beatmap: {map.Artist} - {map.Title} [{map.Difficulty}]");
        var resolver = new BeatmapPathResolver();
        PathResolution resolution = resolver.Resolve(map, locatedRoot);
        Console.WriteLine($"Resolved .osu path: {DisplayPath(resolution.Path, "Not resolved")}");
        Console.WriteLine($"Exists: {(resolution.Path is not null && File.Exists(resolution.Path) ? "Yes" : "No")}");
        string outputPath = resolution.Path is null
            ? AppPaths.OutputDirectory
            : BeatmapGenerationService.FindUniquePath(resolution.Path, " H-RANDOM+", outputDirectory: null);
        Console.WriteLine($"Output path: {DiagnosticPathRedactor.Redact(outputPath)}");
        Console.WriteLine("Output writable: Not verified (read-only diagnostic)");
        return resolution.Success ? 0 : 4;
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
                case "--seed": seed = long.Parse(Value(), CultureInfo.InvariantCulture); break;
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
        Console.WriteLine("Uso:");
        Console.WriteLine("  HRandomPlus.Cli <beatmap.osz|beatmap.zip> [opciones]");
        Console.WriteLine("  HRandomPlus.Cli --diagnose [opciones]");
        Console.WriteLine();
        Console.WriteLine("Opciones de archivo:");
        Console.WriteLine("  -o, --output <RUTA>       Ruta del archivo OSZ generado.");
        Console.WriteLine("      --config <RUTA>       Configuración JSON que se aplicará.");
        Console.WriteLine("      --report <RUTA>       Ruta del reporte JSON generado.");
        Console.WriteLine("      --seed <N>            Seed reproducible.");
        Console.WriteLine("  -d, --difficulty <FILTRO> Procesa sólo la versión, archivo o ruta indicada; puede repetirse.");
        Console.WriteLine("      --overwrite           Permite reemplazar el archivo de salida existente.");
        Console.WriteLine();
        Console.WriteLine("Opciones de diagnóstico:");
        Console.WriteLine("      --host <HOST>         Host de tosu.");
        Console.WriteLine("      --port <PUERTO>       Puerto de tosu.");
        Console.WriteLine("      --osu-path <RUTA>     Raíz nativa de osu!stable.");
        Console.WriteLine();
        Console.WriteLine("Opciones generales:");
        Console.WriteLine("  -h, --help                Muestra esta ayuda.");
        Console.WriteLine();
        Console.WriteLine("Ejemplos:");
        Console.WriteLine("  HRandomPlus.Cli mapa.osz -o mapa-random.osz --seed 12345");
        Console.WriteLine("  HRandomPlus.Cli mapa.osz -d \"Insane\" --report resultado.json");
        Console.WriteLine("  HRandomPlus.Cli --diagnose --host 127.0.0.1 --port 24050");
    }

    private static string PlatformName()
        => OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : Environment.OSVersion.Platform.ToString();

    private static string DisplayPath(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : DiagnosticPathRedactor.Redact(value) ?? fallback;

    private static string? ResolveSongsPath(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return null;
        try
        {
            string full = Path.GetFullPath(root);
            string songs = Path.Combine(full, "Songs");
            return Directory.Exists(songs) ? songs : null;
        }
        catch { return null; }
    }
}
