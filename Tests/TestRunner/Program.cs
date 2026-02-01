using System.Reflection;
using TestRunner.Framework;

// Parse command-line arguments
var showCoverage = args.Contains("--coverage") || args.Contains("-c");
var showDetailedCoverage = args.Contains("--coverage-detailed") || args.Contains("-cd");
var showHelp = args.Contains("--help") || args.Contains("-h");

if (showHelp)
{
    Console.WriteLine("TestRunner - Zero-dependency test framework");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --coverage, -c           Show code coverage summary");
    Console.WriteLine("  --coverage-detailed, -cd Show detailed code coverage by type");
    Console.WriteLine("  --help, -h               Show this help message");
    return 0;
}

// Run tests
var runner = new SimpleTestRunner();
runner.DiscoverTests(Assembly.GetExecutingAssembly());
var failedCount = await runner.RunAllAsync();

// Setup coverage tracking after tests run (assemblies are now loaded)
CoverageTracker? coverageTracker = null;
if (showCoverage || showDetailedCoverage)
{
    coverageTracker = new CoverageTracker();

    // Add target assemblies to track
    // These are now loaded because the tests have run and referenced them
    var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

    var rulesEngine = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "RulesEngine");
    var agentRouting = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "AgentRouting");

    if (rulesEngine != null)
        coverageTracker.AddAssembly(rulesEngine);
    else
        Console.WriteLine("Warning: RulesEngine assembly not loaded");

    if (agentRouting != null)
        coverageTracker.AddAssembly(agentRouting);
    else
        Console.WriteLine("Warning: AgentRouting assembly not loaded");

    coverageTracker.StartTracking();
}

// Generate coverage report if tracking
if (coverageTracker != null)
{
    coverageTracker.StopTracking();

    // Analyze test assembly to find which types were referenced
    coverageTracker.AnalyzeTestAssembly(Assembly.GetExecutingAssembly());

    var report = coverageTracker.GenerateReport();

    if (showDetailedCoverage)
    {
        report.PrintDetailed();
    }

    report.PrintSummary();
}

return failedCount;
