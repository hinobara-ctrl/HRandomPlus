using HRandomPlus.Archives;
using HRandomPlus.Core;
using HRandomPlus.UI;

namespace HRandomPlus;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainWindow());
            return 0;
        }
        try
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                PrintHelp();
                return args.Length == 0 ? 1 : 0;
            }

            string input = args[0];
            string? output = null;
            string? configPath = null;
            string? reportPath = null;
            long? seed = null;
            bool overwrite = false;
            bool? dynamicThreshold = null;
            bool? rename = null;
            string? suffix = null;
            var difficulties = new List<string>();

            for (int i = 1; i < args.Length; i++)
            {
                string value() => ++i < args.Length ? args[i] : throw new ArgumentException($"Falta valor para {args[i - 1]}.");
                switch (args[i])
                {
                    case "--output": case "-o": output = value(); break;
                    case "--config": configPath = value(); break;
                    case "--report": reportPath = value(); break;
                    case "--seed": seed = long.Parse(value(), System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--difficulty": case "-d": difficulties.Add(value()); break;
                    case "--suffix": suffix = value(); break;
                    case "--overwrite": overwrite = true; break;
                    case "--fixed-threshold": dynamicThreshold = false; break;
                    case "--dynamic-threshold": dynamicThreshold = true; break;
                    case "--no-rename": rename = false; break;
                    default: throw new ArgumentException($"Opción desconocida: {args[i]}");
                }
            }

            var config = configPath is null ? new HRandomConfig() : HRandomConfig.Load(configPath);
            if (seed.HasValue) config.Seed = seed;
            if (dynamicThreshold.HasValue) config.DynamicThreshold = dynamicThreshold.Value;
            if (rename.HasValue) config.RenameDifficulty = rename.Value;
            if (suffix is not null) config.DifficultySuffix = suffix;

            output ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(input))!,
                Path.GetFileNameWithoutExtension(input) + "_HRandom.osz");
            reportPath ??= Path.ChangeExtension(output, ".report.json");

            ArchiveReport report = new OsuArchive().Process(input, output, config, difficulties, overwrite);
            OsuArchive.SaveReport(report, reportPath);
            PrintReport(report, reportPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 2;
        }
    }

    private static void PrintReport(ArchiveReport report, string reportPath)
    {
        Console.WriteLine($"OSZ generado: {report.Output}");
        Console.WriteLine($"Seed: {report.Seed}");
        foreach (DifficultyReport diff in report.Difficulties)
        {
            Console.WriteLine();
            Console.WriteLine($"{diff.OriginalVersion} -> {diff.OutputVersion} ({diff.Before.Keymode}K)");
            Console.WriteLine($"  Notas: {diff.Before.TotalNotes} | LN: {diff.Before.LongNotes} | Acordes: {diff.Before.Chords}");
            Console.WriteLine($"  Jacks rápidos: {diff.Before.QuickJacks} -> {diff.After.QuickJacks}");
            Console.WriteLine($"  Trills: {diff.Before.Trills} -> {diff.After.Trills}");
            Console.WriteLine($"  Uso antes:   [{string.Join(", ", diff.Before.ColumnUsage)}]");
            Console.WriteLine($"  Uso después: [{string.Join(", ", diff.After.ColumnUsage)}]");
        }
        Console.WriteLine($"Reporte JSON: {reportPath}");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("HRandomPlus - H-RANDOM+ para archivos osu!mania .osz");
        Console.WriteLine();
        Console.WriteLine("Uso:");
        Console.WriteLine("  HRandomPlus <beatmap.osz> [opciones]");
        Console.WriteLine();
        Console.WriteLine("Opciones:");
        Console.WriteLine("  -o, --output <ruta>        OSZ de salida (por defecto *_HRandom.osz)");
        Console.WriteLine("  --seed <entero>            Seed reproducible");
        Console.WriteLine("  -d, --difficulty <nombre>  Procesar sólo una dificultad; repetible");
        Console.WriteLine("  --config <json>            Configuración y pesos personalizados");
        Console.WriteLine("  --report <json>            Ruta del reporte estadístico");
        Console.WriteLine("  --fixed-threshold          Usar threshold fijo");
        Console.WriteLine("  --dynamic-threshold        Usar threshold dinámico (predeterminado)");
        Console.WriteLine("  --no-rename                No añadir sufijo al nombre de dificultad");
        Console.WriteLine("  --suffix <texto>           Sufijo de dificultad");
        Console.WriteLine("  --overwrite                Reemplazar una salida existente");
    }
}
