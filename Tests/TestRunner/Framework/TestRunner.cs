using System.Diagnostics;
using System.Reflection;

namespace TestRunner.Framework;

/// <summary>
/// Discovers and runs tests marked with [Test] attribute.
/// </summary>
public class SimpleTestRunner
{
    private readonly List<Type> _testClasses = new();
    private int _passed;
    private int _failed;
    private int _skipped;
    private readonly List<(string Name, string Error)> _failures = new();

    public void AddTestClass<T>() => _testClasses.Add(typeof(T));

    public void AddTestClass(Type type) => _testClasses.Add(type);

    public void DiscoverTests(Assembly assembly)
    {
        var testClasses = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetMethods().Any(m => m.GetCustomAttribute<TestAttribute>() != null));

        foreach (var type in testClasses)
        {
            _testClasses.Add(type);
        }
    }

    public async Task<int> RunAllAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                     TEST RUNNER                              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        foreach (var testClass in _testClasses)
        {
            await RunTestClassAsync(testClass);
        }

        stopwatch.Stop();
        PrintSummary(stopwatch.Elapsed);

        return _failed;
    }

    private async Task RunTestClassAsync(Type testClass)
    {
        Console.WriteLine($"┌─ {testClass.Name}");

        var methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<TestAttribute>() != null)
            .ToList();

        object? instance = null;
        try
        {
            instance = Activator.CreateInstance(testClass);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"│  ✗ Failed to create instance: {ex.Message}");
            Console.ResetColor();
            _failed += methods.Count;
            return;
        }

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<TestAttribute>()!;
            var testName = attr.Name ?? method.Name;

            if (attr.Skip != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"│  ○ {testName} (skipped: {attr.Skip})");
                Console.ResetColor();
                _skipped++;
                continue;
            }

            await RunTestMethodAsync(instance!, method, testName);
        }

        Console.WriteLine("└─");
        Console.WriteLine();
    }

    private async Task RunTestMethodAsync(object instance, MethodInfo method, string testName)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = method.Invoke(instance, null);

            // Handle async methods
            if (result is Task task)
            {
                await task;
            }

            stopwatch.Stop();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"│  ✓ {testName} ({stopwatch.ElapsedMilliseconds}ms)");
            Console.ResetColor();
            _passed++;
        }
        catch (TargetInvocationException tie)
        {
            stopwatch.Stop();
            var ex = tie.InnerException ?? tie;
            RecordFailure(testName, ex, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordFailure(testName, ex, stopwatch.ElapsedMilliseconds);
        }
    }

    private void RecordFailure(string testName, Exception ex, long elapsedMs)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"│  ✗ {testName} ({elapsedMs}ms)");
        Console.WriteLine($"│    {ex.Message}");
        Console.ResetColor();
        _failed++;
        _failures.Add((testName, ex.Message));
    }

    private void PrintSummary(TimeSpan elapsed)
    {
        Console.WriteLine("══════════════════════════════════════════════════════════════════");
        Console.WriteLine();

        if (_failures.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILURES:");
            foreach (var (name, error) in _failures)
            {
                Console.WriteLine($"  • {name}");
                Console.WriteLine($"    {error}");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        Console.Write("Results: ");

        if (_passed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{_passed} passed");
            Console.ResetColor();
        }

        if (_failed > 0)
        {
            if (_passed > 0) Console.Write(", ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"{_failed} failed");
            Console.ResetColor();
        }

        if (_skipped > 0)
        {
            if (_passed > 0 || _failed > 0) Console.Write(", ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{_skipped} skipped");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine($"Duration: {elapsed.TotalSeconds:F2}s");
        Console.WriteLine();

        if (_failed == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("All tests passed!");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Some tests failed.");
            Console.ResetColor();
        }
    }
}
