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
            .Where(t => t.GetMethods().Any(m =>
                m.GetCustomAttribute<TestAttribute>() != null ||
                m.GetCustomAttribute<TheoryAttribute>() != null));

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

        var allMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        var factMethods = allMethods
            .Where(m => m.GetCustomAttribute<TestAttribute>() != null)
            .ToList();

        var theoryMethods = allMethods
            .Where(m => m.GetCustomAttribute<TheoryAttribute>() != null)
            .ToList();

        // Discover lifecycle methods
        var setUpMethod = allMethods.FirstOrDefault(m => m.GetCustomAttribute<SetUpAttribute>() != null);
        var tearDownMethod = allMethods.FirstOrDefault(m => m.GetCustomAttribute<TearDownAttribute>() != null);
        var oneTimeSetUpMethod = allMethods.FirstOrDefault(m => m.GetCustomAttribute<OneTimeSetUpAttribute>() != null);
        var oneTimeTearDownMethod = allMethods.FirstOrDefault(m => m.GetCustomAttribute<OneTimeTearDownAttribute>() != null);

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
            _failed += factMethods.Count + theoryMethods.Count;
            return;
        }

        try
        {
            // Run [OneTimeSetUp] before any tests
            if (oneTimeSetUpMethod != null)
            {
                await InvokeLifecycleMethodAsync(instance!, oneTimeSetUpMethod, "OneTimeSetUp");
            }

            // Run [Test] methods
            foreach (var method in factMethods)
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

                await RunTestWithLifecycleAsync(instance!, method, testName, setUpMethod, tearDownMethod);
            }

            // Run [Theory] methods
            foreach (var method in theoryMethods)
            {
                await RunTheoryMethodAsync(instance!, method, testClass, setUpMethod, tearDownMethod);
            }
        }
        finally
        {
            // Run [OneTimeTearDown] after all tests (even if tests failed)
            if (oneTimeTearDownMethod != null)
            {
                try
                {
                    await InvokeLifecycleMethodAsync(instance!, oneTimeTearDownMethod, "OneTimeTearDown");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"│  ✗ OneTimeTearDown failed: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        Console.WriteLine("└─");
        Console.WriteLine();
    }

    private async Task InvokeLifecycleMethodAsync(object instance, MethodInfo method, string methodType)
    {
        try
        {
            var result = method.Invoke(instance, null);
            if (result is Task task)
            {
                await task;
            }
        }
        catch (TargetInvocationException tie)
        {
            throw new Exception($"{methodType} failed: {tie.InnerException?.Message ?? tie.Message}", tie.InnerException ?? tie);
        }
    }

    private async Task RunTestWithLifecycleAsync(object instance, MethodInfo method, string testName,
        MethodInfo? setUpMethod, MethodInfo? tearDownMethod)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? testException = null;

        try
        {
            // Run [SetUp] before test
            if (setUpMethod != null)
            {
                await InvokeLifecycleMethodAsync(instance, setUpMethod, "SetUp");
            }

            // Run the test
            var result = method.Invoke(instance, null);
            if (result is Task task)
            {
                await task;
            }
        }
        catch (TargetInvocationException tie)
        {
            testException = tie.InnerException ?? tie;
        }
        catch (Exception ex)
        {
            testException = ex;
        }
        finally
        {
            // Run [TearDown] after test (even if test failed)
            if (tearDownMethod != null)
            {
                try
                {
                    await InvokeLifecycleMethodAsync(instance, tearDownMethod, "TearDown");
                }
                catch (Exception tearDownEx)
                {
                    // If test already failed, report teardown failure separately
                    // If test passed but teardown failed, use teardown exception
                    if (testException == null)
                    {
                        testException = tearDownEx;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"│    (TearDown also failed: {tearDownEx.Message})");
                        Console.ResetColor();
                    }
                }
            }
        }

        stopwatch.Stop();

        if (testException == null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"│  ✓ {testName} ({stopwatch.ElapsedMilliseconds}ms)");
            Console.ResetColor();
            _passed++;
        }
        else
        {
            RecordFailure(testName, testException, stopwatch.ElapsedMilliseconds);
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

    private async Task RunTheoryMethodAsync(object instance, MethodInfo method, Type testClass,
        MethodInfo? setUpMethod, MethodInfo? tearDownMethod)
    {
        var attr = method.GetCustomAttribute<TheoryAttribute>()!;
        var baseName = attr.Name ?? method.Name;

        if (attr.Skip != null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"│  ○ {baseName} (skipped: {attr.Skip})");
            Console.ResetColor();
            _skipped++;
            return;
        }

        // Collect all test data from [InlineData] and [MemberData] attributes
        var allTestData = new List<object?[]>();

        // Get [InlineData] attributes
        var inlineDataAttrs = method.GetCustomAttributes<InlineDataAttribute>();
        foreach (var inlineData in inlineDataAttrs)
        {
            allTestData.Add(inlineData.Data);
        }

        // Get [MemberData] attributes
        var memberDataAttrs = method.GetCustomAttributes<MemberDataAttribute>();
        foreach (var memberData in memberDataAttrs)
        {
            var data = GetMemberData(memberData, testClass);
            allTestData.AddRange(data);
        }

        if (allTestData.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"│  ○ {baseName} (no test data provided)");
            Console.ResetColor();
            _skipped++;
            return;
        }

        // Run test for each data set
        var caseNumber = 0;
        foreach (var testData in allTestData)
        {
            caseNumber++;
            var dataDisplay = FormatTestData(testData);
            var testName = $"{baseName}({dataDisplay})";

            await RunTheoryTestCaseAsync(instance, method, testName, testData, setUpMethod, tearDownMethod);
        }
    }

    private async Task RunTheoryTestCaseAsync(object instance, MethodInfo method, string testName, object?[] args,
        MethodInfo? setUpMethod, MethodInfo? tearDownMethod)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? testException = null;

        try
        {
            // Run [SetUp] before test
            if (setUpMethod != null)
            {
                await InvokeLifecycleMethodAsync(instance, setUpMethod, "SetUp");
            }

            // Convert arguments to match parameter types
            var parameters = method.GetParameters();
            var convertedArgs = ConvertArguments(args, parameters);

            var result = method.Invoke(instance, convertedArgs);

            // Handle async methods
            if (result is Task task)
            {
                await task;
            }
        }
        catch (TargetInvocationException tie)
        {
            testException = tie.InnerException ?? tie;
        }
        catch (Exception ex)
        {
            testException = ex;
        }
        finally
        {
            // Run [TearDown] after test (even if test failed)
            if (tearDownMethod != null)
            {
                try
                {
                    await InvokeLifecycleMethodAsync(instance, tearDownMethod, "TearDown");
                }
                catch (Exception tearDownEx)
                {
                    if (testException == null)
                    {
                        testException = tearDownEx;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"│    (TearDown also failed: {tearDownEx.Message})");
                        Console.ResetColor();
                    }
                }
            }
        }

        stopwatch.Stop();

        if (testException == null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"│  ✓ {testName} ({stopwatch.ElapsedMilliseconds}ms)");
            Console.ResetColor();
            _passed++;
        }
        else
        {
            RecordFailure(testName, testException, stopwatch.ElapsedMilliseconds);
        }
    }

    private static object?[] ConvertArguments(object?[] args, ParameterInfo[] parameters)
    {
        if (args.Length != parameters.Length)
        {
            throw new ArgumentException(
                $"Argument count mismatch: expected {parameters.Length}, got {args.Length}");
        }

        var result = new object?[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            result[i] = ConvertValue(args[i], parameters[i].ParameterType);
        }
        return result;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
        {
            return null;
        }

        var valueType = value.GetType();
        if (targetType.IsAssignableFrom(valueType))
        {
            return value;
        }

        // Handle numeric conversions (e.g., int to double, long to int)
        if (IsNumericType(targetType) && IsNumericType(valueType))
        {
            return Convert.ChangeType(value, targetType);
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            return ConvertValue(value, underlyingType);
        }

        return value;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }

    private static IEnumerable<object?[]> GetMemberData(MemberDataAttribute attr, Type testClass)
    {
        var type = attr.MemberType ?? testClass;
        var memberName = attr.MemberName;

        // Try to find a static method
        var method = type.GetMethod(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (method != null)
        {
            var result = method.Invoke(null, null);
            if (result is IEnumerable<object?[]> enumerable)
            {
                return enumerable;
            }
            throw new InvalidOperationException(
                $"MemberData method '{memberName}' must return IEnumerable<object?[]>");
        }

        // Try to find a static property
        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (property != null)
        {
            var result = property.GetValue(null);
            if (result is IEnumerable<object?[]> enumerable)
            {
                return enumerable;
            }
            throw new InvalidOperationException(
                $"MemberData property '{memberName}' must return IEnumerable<object?[]>");
        }

        // Try to find a static field
        var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null)
        {
            var result = field.GetValue(null);
            if (result is IEnumerable<object?[]> enumerable)
            {
                return enumerable;
            }
            throw new InvalidOperationException(
                $"MemberData field '{memberName}' must return IEnumerable<object?[]>");
        }

        throw new InvalidOperationException(
            $"Could not find static member '{memberName}' on type '{type.Name}'");
    }

    private static string FormatTestData(object?[]? data)
    {
        if (data == null || data.Length == 0)
        {
            return "";
        }

        var parts = new List<string>();
        foreach (var item in data)
        {
            if (item == null)
            {
                parts.Add("null");
            }
            else if (item is string s)
            {
                parts.Add($"\"{s}\"");
            }
            else if (item is bool b)
            {
                parts.Add(b ? "true" : "false");
            }
            else
            {
                parts.Add(item.ToString() ?? "null");
            }
        }
        return string.Join(", ", parts);
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
