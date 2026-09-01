namespace HRandomPlus.Beatmaps;

public static class BeatSnapReference
{
    public static IReadOnlyList<int> CommonDivisors { get; } =
        new[] { 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64 };

    public static double Milliseconds(double bpm, int divisor)
    {
        if (!double.IsFinite(bpm) || bpm <= 0)
            throw new ArgumentOutOfRangeException(nameof(bpm), "BPM must be greater than zero.");
        if (divisor <= 0)
            throw new ArgumentOutOfRangeException(nameof(divisor), "Snap divisor must be greater than zero.");
        return 60000d / bpm / divisor;
    }

    public static string DescribeBpmRange(IReadOnlyCollection<double> bpms)
    {
        if (bpms.Count == 0) return "BPM: —";
        double minimum = bpms.Min();
        double maximum = bpms.Max();
        string Format(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        return minimum == maximum ? $"BPM: {Format(minimum)}" : $"BPM range: {Format(minimum)}–{Format(maximum)}";
    }
}
