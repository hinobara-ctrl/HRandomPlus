using System.Collections;

namespace Xunit;

[AttributeUsage(AttributeTargets.Method)]
public sealed class FactAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public sealed class TheoryAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class InlineDataAttribute : Attribute
{
    public object?[] Data { get; }
    public InlineDataAttribute(params object?[] data) => Data = data;
}

public sealed class TestException : Exception
{
    public TestException(string message) : base(message) { }
}

public static class Assert
{
    public static void True(bool condition, string? message = null)
    {
        if (!condition) throw new TestException(message ?? "Se esperaba true.");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (expected is not string && expected is IEnumerable expectedEnumerable && actual is IEnumerable actualEnumerable)
        {
            object?[] left = expectedEnumerable.Cast<object?>().ToArray();
            object?[] right = actualEnumerable.Cast<object?>().ToArray();
            if (!left.SequenceEqual(right))
                throw new TestException($"Secuencias diferentes: [{string.Join(", ", left)}] != [{string.Join(", ", right)}]");
            return;
        }
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new TestException($"Valores diferentes: {expected} != {actual}");
    }

    public static void Contains(string expectedSubstring, string actual)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
            throw new TestException($"No se encontró '{expectedSubstring}'.");
    }

    public static void InRange<T>(T actual, T low, T high) where T : IComparable<T>
    {
        if (actual.CompareTo(low) < 0 || actual.CompareTo(high) > 0)
            throw new TestException($"{actual} está fuera de [{low}, {high}].");
    }

    public static void All<T>(IEnumerable<T> values, Action<T> assertion)
    {
        foreach (T value in values) assertion(value);
    }

    public static void DoesNotContain<T>(T value, IEnumerable<T> values)
    {
        if (values.Contains(value)) throw new TestException($"La colección contiene {value}.");
    }

    public static T Single<T>(IEnumerable<T> values)
    {
        T[] array = values.ToArray();
        if (array.Length != 1) throw new TestException($"Se esperaba un elemento y se obtuvieron {array.Length}.");
        return array[0];
    }
}
