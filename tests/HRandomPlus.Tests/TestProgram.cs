using System.Reflection;

namespace HRandomPlus.Tests;

public static class Program
{
    public static int Main()
    {
        int passed = 0;
        int failed = 0;
        Assembly assembly = typeof(Program).Assembly;
        foreach (Type type in assembly.GetTypes().Where(t => t.IsClass && t.Namespace == typeof(Program).Namespace))
        {
            object? instance = null;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                object?[][] cases;
                if (method.GetCustomAttribute<FactAttribute>() is not null)
                    cases = new[] { Array.Empty<object?>() };
                else if (method.GetCustomAttribute<TheoryAttribute>() is not null)
                    cases = method.GetCustomAttributes<InlineDataAttribute>().Select(a => a.Data).ToArray();
                else
                    continue;

                instance ??= Activator.CreateInstance(type);
                foreach (object?[] data in cases)
                {
                    string name = $"{type.Name}.{method.Name}" + (data.Length == 0 ? "" : $"({string.Join(",", data)})");
                    try
                    {
                        method.Invoke(instance, data);
                        Console.WriteLine($"PASS {name}");
                        passed++;
                    }
                    catch (TargetInvocationException ex)
                    {
                        Console.WriteLine($"FAIL {name}: {ex.InnerException?.Message ?? ex.Message}");
                        failed++;
                    }
                }
            }
        }
        Console.WriteLine($"\nResultado: {passed} pasaron, {failed} fallaron.");
        return failed == 0 ? 0 : 1;
    }
}
