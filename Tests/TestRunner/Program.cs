using System.Reflection;
using System.Runtime.Loader;
using TestRunner.Framework;

// Parse command-line arguments
var showCoverage = args.Contains("--coverage") || args.Contains("-c");
var showDetailedCoverage = args.Contains("--coverage-detailed") || args.Contains("-cd");
var showHelp = args.Contains("--help") || args.Contains("-h");
var discoverArg = args.FirstOrDefault(a => a.StartsWith("--discover="));

// Get assembly paths (non-flag arguments)
var assemblyPaths = args
    .Where(a => !a.StartsWith("-") && !a.StartsWith("--"))
    .ToList();

if (showHelp)
{
    Console.WriteLine("TestRunner - Zero-dependency test framework");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run --project Tests/TestRunner/ -- [options] [assembly-paths...]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  assembly-paths          Paths to test assembly DLLs to load and run");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --discover=<dir>        Discover *.Tests.dll files in directory");
    Console.WriteLine("  --coverage, -c          Show API coverage summary");
    Console.WriteLine("  --coverage-detailed, -cd Show detailed API coverage by type");
    Console.WriteLine("  --help, -h              Show this help message");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  # Run tests from specific assemblies");
    Console.WriteLine("  dotnet run --project Tests/TestRunner/ -- path/to/MyTests.dll");
    Console.WriteLine();
    Console.WriteLine("  # Discover and run all test assemblies in a directory");
    Console.WriteLine("  dotnet run --project Tests/TestRunner/ -- --discover=Tests/bin");
    Console.WriteLine();
    Console.WriteLine("  # Run with coverage report");
    Console.WriteLine("  dotnet run --project Tests/TestRunner/ -- --coverage --discover=Tests/bin");
    return 0;
}

// Discover test assemblies if --discover is specified
if (discoverArg != null)
{
    var discoverDir = discoverArg.Substring("--discover=".Length);
    if (Directory.Exists(discoverDir))
    {
        var discovered = Directory.GetFiles(discoverDir, "*.Tests.dll", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/ref/")) // Exclude reference assemblies
            .ToList();
        assemblyPaths.AddRange(discovered);
    }
    else
    {
        Console.WriteLine($"Warning: Discovery directory not found: {discoverDir}");
    }
}

// If no assemblies specified, try to find them in common locations
if (assemblyPaths.Count == 0)
{
    Console.WriteLine("No test assemblies specified. Searching in default locations...");

    var searchPaths = new[]
    {
        "Tests/RulesEngine.Tests/bin/Debug/net8.0",
        "Tests/AgentRouting.Tests/bin/Debug/net8.0",
        "Tests/MafiaDemo.Tests/bin/Debug/net8.0"
    };

    foreach (var searchPath in searchPaths)
    {
        if (Directory.Exists(searchPath))
        {
            var dlls = Directory.GetFiles(searchPath, "*.Tests.dll")
                .Where(f => !f.Contains("/ref/"))
                .ToList();
            assemblyPaths.AddRange(dlls);
        }
    }
}

if (assemblyPaths.Count == 0)
{
    Console.WriteLine();
    Console.WriteLine("No test assemblies found. Please build the test projects first:");
    Console.WriteLine("  dotnet build Tests/RulesEngine.Tests/");
    Console.WriteLine("  dotnet build Tests/AgentRouting.Tests/");
    Console.WriteLine("  dotnet build Tests/MafiaDemo.Tests/");
    Console.WriteLine();
    Console.WriteLine("Or specify assembly paths directly:");
    Console.WriteLine("  dotnet run --project Tests/TestRunner/ -- path/to/Tests.dll");
    return 1;
}

// Load and run tests from each assembly
var runner = new SimpleTestRunner();
var loadedAssemblies = new List<Assembly>();

// Collect all directories containing test assemblies for dependency resolution
var assemblyDirectories = assemblyPaths
    .Select(p => Path.GetDirectoryName(Path.GetFullPath(p)))
    .Where(d => d != null)
    .Distinct()
    .Cast<string>()
    .ToList();

// Create a single shared load context for all test assemblies
var loadContext = new TestAssemblyLoadContext(assemblyDirectories);

Console.WriteLine();
Console.WriteLine($"Loading {assemblyPaths.Count} test assembly(ies)...");
Console.WriteLine();

foreach (var assemblyPath in assemblyPaths.Distinct())
{
    var fullPath = Path.GetFullPath(assemblyPath);

    if (!File.Exists(fullPath))
    {
        Console.WriteLine($"Warning: Assembly not found: {fullPath}");
        continue;
    }

    try
    {
        var assembly = loadContext.LoadFromAssemblyPath(fullPath);

        Console.WriteLine($"Loaded: {Path.GetFileName(fullPath)}");
        loadedAssemblies.Add(assembly);
        runner.DiscoverTests(assembly);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading {fullPath}: {ex.Message}");
    }
}

// Also discover tests in the TestRunner assembly itself (for TheoryTests, etc.)
var testRunnerAssembly = Assembly.GetExecutingAssembly();
runner.DiscoverTests(testRunnerAssembly);

var failedCount = await runner.RunAllAsync();

// Setup coverage tracking if requested
if (showCoverage || showDetailedCoverage)
{
    var coverageTracker = new CoverageTracker();

    // Add target assemblies to track (find them from the loaded assemblies' references)
    var targetAssemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a =>
        {
            var name = a.GetName().Name;
            return name == "RulesEngine" || name == "AgentRouting" || name == "AgentRouting.MafiaDemo";
        })
        .ToList();

    foreach (var assembly in targetAssemblies)
    {
        coverageTracker.AddAssembly(assembly);
    }

    if (targetAssemblies.Count == 0)
    {
        Console.WriteLine("Warning: No target assemblies (RulesEngine, AgentRouting) found for coverage tracking");
    }

    coverageTracker.StartTracking();

    // Analyze all loaded test assemblies
    foreach (var assembly in loadedAssemblies)
    {
        coverageTracker.AnalyzeTestAssembly(assembly);
    }
    coverageTracker.AnalyzeTestAssembly(testRunnerAssembly);

    coverageTracker.StopTracking();

    var report = coverageTracker.GenerateReport();

    if (showDetailedCoverage)
    {
        report.PrintDetailed();
    }

    report.PrintSummary();
}

return failedCount;

/// <summary>
/// Custom AssemblyLoadContext that resolves dependencies from multiple test assembly directories,
/// while sharing the TestRunner.Framework assembly to ensure type identity for attributes.
/// </summary>
class TestAssemblyLoadContext : AssemblyLoadContext
{
    private readonly List<string> _assemblyDirectories;
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private static readonly HashSet<string> SharedAssemblies = new()
    {
        "TestRunner.Framework"
    };

    public TestAssemblyLoadContext(List<string> assemblyDirectories) : base(isCollectible: true)
    {
        _assemblyDirectories = assemblyDirectories;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? "";

        // Check if we've already loaded this assembly in this context
        if (_loadedAssemblies.TryGetValue(name, out var cached))
        {
            return cached;
        }

        // Share TestRunner.Framework to ensure attribute type identity
        if (SharedAssemblies.Contains(name))
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name);
            if (loaded != null)
            {
                return loaded;
            }
        }

        // Search all known directories for the assembly
        foreach (var dir in _assemblyDirectories)
        {
            var assemblyPath = Path.Combine(dir, $"{name}.dll");
            if (File.Exists(assemblyPath))
            {
                var assembly = LoadFromAssemblyPath(assemblyPath);
                _loadedAssemblies[name] = assembly;
                return assembly;
            }
        }

        // Fall back to default resolution (shared framework, etc.)
        return null;
    }
}
