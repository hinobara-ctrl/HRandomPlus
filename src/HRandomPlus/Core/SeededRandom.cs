using System.Security.Cryptography;

namespace HRandomPlus.Core;

/// <summary>A runtime-independent SplitMix64 PRNG.</summary>
public sealed class SeededRandom
{
    private ulong state;

    public long Seed { get; }

    public SeededRandom(long seed)
    {
        Seed = seed;
        state = unchecked((ulong)seed);
    }

    public static long CreateSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToInt64(bytes);
    }

    public ulong NextUInt64()
    {
        ulong z = (state += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public int NextInt(int exclusiveMax)
    {
        if (exclusiveMax <= 0)
            throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
        return (int)(NextDouble() * exclusiveMax);
    }

    public void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = NextInt(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
