using TestRunner.Framework;
using System.Xml.Linq;

namespace TestRunner.Tests;

/// <summary>
/// Tests that validate code coverage meets minimum thresholds.
/// These tests read coverage XML files and verify coverage percentages.
/// </summary>
public class CoverageValidationTests
{
    private const string CoverageDir = "coverage";
    private const double MinLineRate = 0.70; // 70% minimum line coverage
    private const double MinBranchRate = 0.60; // 60% minimum branch coverage

    /// <summary>
    /// Validates that coverage data files exist.
    /// </summary>
    [Test]
    public void Coverage_DataFilesExist()
    {
        if (!Directory.Exists(CoverageDir))
        {
            Console.WriteLine("SKIP: Coverage directory not found. Run coverage first.");
            return;
        }

        var xmlFiles = Directory.GetFiles(CoverageDir, "*.xml");
        Console.WriteLine($"Found {xmlFiles.Length} coverage file(s):");
        foreach (var file in xmlFiles)
        {
            Console.WriteLine($"  - {Path.GetFileName(file)}");
        }

        Assert.True(xmlFiles.Length > 0, "No coverage XML files found");
    }

    /// <summary>
    /// Validates that RulesEngine meets minimum coverage thresholds.
    /// </summary>
    [Test]
    public void Coverage_RulesEngine_MeetsThreshold()
    {
        var coverageFile = Path.Combine(CoverageDir, "rulesengine.xml");
        if (!File.Exists(coverageFile))
        {
            Console.WriteLine("SKIP: rulesengine.xml not found. Run coverage first.");
            return;
        }

        var (lineRate, branchRate) = GetCoverageRates(coverageFile);
        Console.WriteLine($"RulesEngine coverage: {lineRate * 100:F1}% line, {branchRate * 100:F1}% branch");

        Assert.True(lineRate >= MinLineRate,
            $"RulesEngine line coverage ({lineRate * 100:F1}%) is below minimum ({MinLineRate * 100:F1}%)");
        Assert.True(branchRate >= MinBranchRate,
            $"RulesEngine branch coverage ({branchRate * 100:F1}%) is below minimum ({MinBranchRate * 100:F1}%)");
    }

    /// <summary>
    /// Validates that AgentRouting meets minimum coverage thresholds.
    /// </summary>
    [Test]
    public void Coverage_AgentRouting_MeetsThreshold()
    {
        var coverageFile = Path.Combine(CoverageDir, "agentrouting.xml");
        if (!File.Exists(coverageFile))
        {
            Console.WriteLine("SKIP: agentrouting.xml not found. Run coverage first.");
            return;
        }

        var (lineRate, branchRate) = GetCoverageRates(coverageFile);
        Console.WriteLine($"AgentRouting coverage: {lineRate * 100:F1}% line, {branchRate * 100:F1}% branch");

        Assert.True(lineRate >= MinLineRate,
            $"AgentRouting line coverage ({lineRate * 100:F1}%) is below minimum ({MinLineRate * 100:F1}%)");
        Assert.True(branchRate >= MinBranchRate,
            $"AgentRouting branch coverage ({branchRate * 100:F1}%) is below minimum ({MinBranchRate * 100:F1}%)");
    }

    /// <summary>
    /// Validates that MafiaDemo meets minimum coverage thresholds.
    /// </summary>
    [Test]
    public void Coverage_MafiaDemo_MeetsThreshold()
    {
        var coverageFile = Path.Combine(CoverageDir, "mafiademo.xml");
        if (!File.Exists(coverageFile))
        {
            Console.WriteLine("SKIP: mafiademo.xml not found. Run coverage first.");
            return;
        }

        var (lineRate, branchRate) = GetCoverageRates(coverageFile);
        Console.WriteLine($"MafiaDemo coverage: {lineRate * 100:F1}% line, {branchRate * 100:F1}% branch");

        Assert.True(lineRate >= MinLineRate,
            $"MafiaDemo line coverage ({lineRate * 100:F1}%) is below minimum ({MinLineRate * 100:F1}%)");
        Assert.True(branchRate >= MinBranchRate,
            $"MafiaDemo branch coverage ({branchRate * 100:F1}%) is below minimum ({MinBranchRate * 100:F1}%)");
    }

    /// <summary>
    /// Lists classes with zero coverage (needs tests).
    /// </summary>
    [Test]
    public void Coverage_IdentifyZeroCoverageClasses()
    {
        if (!Directory.Exists(CoverageDir))
        {
            Console.WriteLine("SKIP: Coverage directory not found. Run coverage first.");
            return;
        }

        var zeroCoverageClasses = new List<(string Module, string ClassName)>();

        foreach (var xmlFile in Directory.GetFiles(CoverageDir, "*.xml"))
        {
            var module = Path.GetFileNameWithoutExtension(xmlFile);

            try
            {
                var doc = XDocument.Load(xmlFile);
                var classes = doc.Descendants("class")
                    .Where(c => c.Attribute("line-rate")?.Value == "0")
                    .Select(c => c.Attribute("name")?.Value ?? "Unknown")
                    .Where(name => !IsExcluded(name));

                foreach (var className in classes)
                {
                    zeroCoverageClasses.Add((module, CleanClassName(className)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not parse {xmlFile}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nClasses with 0% coverage ({zeroCoverageClasses.Count}):");
        foreach (var (module, className) in zeroCoverageClasses.Take(20))
        {
            Console.WriteLine($"  [{module}] {className}");
        }

        if (zeroCoverageClasses.Count > 20)
        {
            Console.WriteLine($"  ... and {zeroCoverageClasses.Count - 20} more");
        }

        // This is informational, not a failure
        Assert.True(true, "Zero coverage classes identified");
    }

    /// <summary>
    /// Lists classes close to threshold (quick wins).
    /// </summary>
    [Test]
    public void Coverage_IdentifyQuickWins()
    {
        if (!Directory.Exists(CoverageDir))
        {
            Console.WriteLine("SKIP: Coverage directory not found. Run coverage first.");
            return;
        }

        var quickWins = new List<(string Module, string ClassName, double Coverage)>();

        foreach (var xmlFile in Directory.GetFiles(CoverageDir, "*.xml"))
        {
            var module = Path.GetFileNameWithoutExtension(xmlFile);

            try
            {
                var doc = XDocument.Load(xmlFile);
                var classes = doc.Descendants("class")
                    .Select(c => new
                    {
                        Name = c.Attribute("name")?.Value ?? "Unknown",
                        LineRate = double.TryParse(c.Attribute("line-rate")?.Value, out var lr) ? lr : 0
                    })
                    .Where(c => c.LineRate >= 0.50 && c.LineRate < 0.80)
                    .Where(c => !IsExcluded(c.Name));

                foreach (var c in classes)
                {
                    quickWins.Add((module, CleanClassName(c.Name), c.LineRate * 100));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not parse {xmlFile}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nQuick wins (50-79% coverage, close to 80% target) ({quickWins.Count}):");
        foreach (var (module, className, coverage) in quickWins.OrderByDescending(x => x.Coverage).Take(15))
        {
            Console.WriteLine($"  [{module}] {className}: {coverage:F1}%");
        }

        if (quickWins.Count > 15)
        {
            Console.WriteLine($"  ... and {quickWins.Count - 15} more");
        }

        // This is informational, not a failure
        Assert.True(true, "Quick wins identified");
    }

    /// <summary>
    /// Generates a summary report of overall coverage.
    /// </summary>
    [Test]
    public void Coverage_GenerateSummaryReport()
    {
        if (!Directory.Exists(CoverageDir))
        {
            Console.WriteLine("SKIP: Coverage directory not found. Run coverage first.");
            return;
        }

        Console.WriteLine("\n════════════════════════════════════════════════════════");
        Console.WriteLine("COVERAGE SUMMARY REPORT");
        Console.WriteLine("════════════════════════════════════════════════════════\n");

        var totalLineHits = 0;
        var totalLineTotal = 0;
        var totalBranchHits = 0;
        var totalBranchTotal = 0;

        Console.WriteLine($"{"Module",-15} {"Line",-10} {"Branch",-10} {"Status",-15}");
        Console.WriteLine(new string('-', 50));

        foreach (var xmlFile in Directory.GetFiles(CoverageDir, "*.xml").OrderBy(f => f))
        {
            var module = Path.GetFileNameWithoutExtension(xmlFile);
            var (lineRate, branchRate) = GetCoverageRates(xmlFile);
            var (lineHits, lineTotal, branchHits, branchTotal) = GetCoverageCounts(xmlFile);

            totalLineHits += lineHits;
            totalLineTotal += lineTotal;
            totalBranchHits += branchHits;
            totalBranchTotal += branchTotal;

            var status = lineRate >= 0.80 ? "✓ Good" :
                         lineRate >= 0.70 ? "○ Fair" : "✗ Needs Work";

            Console.WriteLine($"{module,-15} {lineRate * 100,7:F1}%  {branchRate * 100,7:F1}%  {status,-15}");
        }

        Console.WriteLine(new string('-', 50));

        var overallLine = totalLineTotal > 0 ? (double)totalLineHits / totalLineTotal : 0;
        var overallBranch = totalBranchTotal > 0 ? (double)totalBranchHits / totalBranchTotal : 0;
        var overallStatus = overallLine >= 0.80 ? "✓ Good" :
                           overallLine >= 0.70 ? "○ Fair" : "✗ Needs Work";

        Console.WriteLine($"{"OVERALL",-15} {overallLine * 100,7:F1}%  {overallBranch * 100,7:F1}%  {overallStatus,-15}");

        Console.WriteLine("\n════════════════════════════════════════════════════════");
        Console.WriteLine($"Target: ≥70% line coverage, ≥60% branch coverage");
        Console.WriteLine("════════════════════════════════════════════════════════\n");

        Assert.True(true, "Summary report generated");
    }

    private static (double LineRate, double BranchRate) GetCoverageRates(string xmlFile)
    {
        try
        {
            var doc = XDocument.Load(xmlFile);
            var coverage = doc.Element("coverage");

            var lineRate = double.TryParse(coverage?.Attribute("line-rate")?.Value, out var lr) ? lr : 0;
            var branchRate = double.TryParse(coverage?.Attribute("branch-rate")?.Value, out var br) ? br : 0;

            return (lineRate, branchRate);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static (int LineHits, int LineTotal, int BranchHits, int BranchTotal) GetCoverageCounts(string xmlFile)
    {
        try
        {
            var doc = XDocument.Load(xmlFile);
            var coverage = doc.Element("coverage");

            var linesCovered = int.TryParse(coverage?.Attribute("lines-covered")?.Value, out var lc) ? lc : 0;
            var linesValid = int.TryParse(coverage?.Attribute("lines-valid")?.Value, out var lv) ? lv : 0;
            var branchesCovered = int.TryParse(coverage?.Attribute("branches-covered")?.Value, out var bc) ? bc : 0;
            var branchesValid = int.TryParse(coverage?.Attribute("branches-valid")?.Value, out var bv) ? bv : 0;

            return (linesCovered, linesValid, branchesCovered, branchesValid);
        }
        catch
        {
            return (0, 0, 0, 0);
        }
    }

    private static bool IsExcluded(string className)
    {
        // Exclude async state machines, closures, display classes, and test infrastructure
        return className.Contains("/<") ||
               className.Contains(">d__") ||
               className.Contains("__c__") ||
               className.Contains("DisplayClass") ||
               className.Contains("TestRunner.Framework") ||
               className.Contains(".Program");
    }

    private static string CleanClassName(string className)
    {
        // Remove generic type parameters and async markers
        return className
            .Replace("`1", "")
            .Replace("`2", "");
    }
}
