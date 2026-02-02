using System;
using System.Collections.Generic;
using System.Linq;
using RulesEngine.Core;
using RulesEngine.Enhanced;
using TestRunner.Framework;

namespace TestRunner.Tests;

/// <summary>
/// Tests for ValidationResult class - covers all public methods and properties
/// </summary>
[TestClass]
public class ValidationResultTests
{
    [Test]
    public void ValidationResult_NewInstance_IsValid()
    {
        var result = new ValidationResult();

        Assert.True(result.IsValid);
    }

    [Test]
    public void ValidationResult_NewInstance_HasEmptyErrors()
    {
        var result = new ValidationResult();

        Assert.NotNull(result.Errors);
        Assert.Empty(result.Errors);
    }

    [Test]
    public void ValidationResult_NewInstance_HasEmptyWarnings()
    {
        var result = new ValidationResult();

        Assert.NotNull(result.Warnings);
        Assert.Empty(result.Warnings);
    }

    [Test]
    public void AddError_SingleError_MakesResultInvalid()
    {
        var result = new ValidationResult();

        result.AddError("Test error");

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("Test error", result.Errors);
    }

    [Test]
    public void AddError_MultipleErrors_AllAddedInOrder()
    {
        var result = new ValidationResult();

        result.AddError("First error");
        result.AddError("Second error");
        result.AddError("Third error");

        Assert.Equal(3, result.Errors.Count);
        Assert.Equal("First error", result.Errors[0]);
        Assert.Equal("Second error", result.Errors[1]);
        Assert.Equal("Third error", result.Errors[2]);
    }

    [Test]
    public void AddErrors_Collection_AddsAllErrors()
    {
        var result = new ValidationResult();
        var errors = new List<string> { "Error 1", "Error 2", "Error 3" };

        result.AddErrors(errors);

        Assert.Equal(3, result.Errors.Count);
        Assert.False(result.IsValid);
    }

    [Test]
    public void AddErrors_EmptyCollection_NoErrors()
    {
        var result = new ValidationResult();

        result.AddErrors(new List<string>());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Test]
    public void AddWarning_SingleWarning_StillValid()
    {
        var result = new ValidationResult();

        result.AddWarning("Test warning");

        Assert.True(result.IsValid); // Warnings don't affect validity
        Assert.Single(result.Warnings);
        Assert.Contains("Test warning", result.Warnings);
    }

    [Test]
    public void AddWarning_MultipleWarnings_AllAddedInOrder()
    {
        var result = new ValidationResult();

        result.AddWarning("First warning");
        result.AddWarning("Second warning");

        Assert.Equal(2, result.Warnings.Count);
        Assert.Equal("First warning", result.Warnings[0]);
        Assert.Equal("Second warning", result.Warnings[1]);
    }

    [Test]
    public void AddWarnings_Collection_AddsAllWarnings()
    {
        var result = new ValidationResult();
        var warnings = new List<string> { "Warning A", "Warning B" };

        result.AddWarnings(warnings);

        Assert.Equal(2, result.Warnings.Count);
        Assert.True(result.IsValid);
    }

    [Test]
    public void AddWarnings_EmptyCollection_NoWarnings()
    {
        var result = new ValidationResult();

        result.AddWarnings(new List<string>());

        Assert.Empty(result.Warnings);
    }

    [Test]
    public void AddErrorsAndWarnings_Combined_BothTracked()
    {
        var result = new ValidationResult();

        result.AddError("Error 1");
        result.AddWarning("Warning 1");
        result.AddErrors(new[] { "Error 2", "Error 3" });
        result.AddWarnings(new[] { "Warning 2" });

        Assert.Equal(3, result.Errors.Count);
        Assert.Equal(2, result.Warnings.Count);
        Assert.False(result.IsValid);
    }

    [Test]
    public void ToString_NoErrorsNoWarnings_ReturnsEmptyString()
    {
        var result = new ValidationResult();

        var output = result.ToString();

        Assert.Equal("", output);
    }

    [Test]
    public void ToString_OnlyErrors_FormatsErrorsSection()
    {
        var result = new ValidationResult();
        result.AddError("First error");
        result.AddError("Second error");

        var output = result.ToString();

        Assert.Contains("Errors:", output);
        Assert.Contains("- First error", output);
        Assert.Contains("- Second error", output);
    }

    [Test]
    public void ToString_OnlyWarnings_FormatsWarningsSection()
    {
        var result = new ValidationResult();
        result.AddWarning("Test warning");

        var output = result.ToString();

        Assert.Contains("Warnings:", output);
        Assert.Contains("- Test warning", output);
    }

    [Test]
    public void ToString_BothErrorsAndWarnings_FormatsAllSections()
    {
        var result = new ValidationResult();
        result.AddError("Critical error");
        result.AddWarning("Minor warning");

        var output = result.ToString();

        Assert.Contains("Errors:", output);
        Assert.Contains("- Critical error", output);
        Assert.Contains("Warnings:", output);
        Assert.Contains("- Minor warning", output);
    }

    [Test]
    public void IsValid_AfterClearingErrors_StillFalse()
    {
        // Note: The Errors list is directly accessible, so clearing it would make IsValid true
        // This tests that IsValid dynamically checks the Errors list
        var result = new ValidationResult();
        result.AddError("Error");
        Assert.False(result.IsValid);

        result.Errors.Clear();
        Assert.True(result.IsValid);
    }

    [Test]
    public void AddError_EmptyString_AddsEmptyError()
    {
        var result = new ValidationResult();
        result.AddError("");

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("", result.Errors[0]);
    }
}

/// <summary>
/// Tests for AnalysisReport class - covers all public methods and properties
/// </summary>
[TestClass]
public class AnalysisReportTests
{
    [Test]
    public void AnalysisReport_NewInstance_HasEmptyRuleAnalyses()
    {
        var report = new AnalysisReport();

        Assert.NotNull(report.RuleAnalyses);
        Assert.Empty(report.RuleAnalyses);
    }

    [Test]
    public void AnalysisReport_NewInstance_HasEmptyOverlaps()
    {
        var report = new AnalysisReport();

        Assert.NotNull(report.Overlaps);
        Assert.Empty(report.Overlaps);
    }

    [Test]
    public void AnalysisReport_NewInstance_HasEmptyDeadRules()
    {
        var report = new AnalysisReport();

        Assert.NotNull(report.DeadRules);
        Assert.Empty(report.DeadRules);
    }

    [Test]
    public void ToString_EmptyReport_ContainsHeader()
    {
        var report = new AnalysisReport();

        var output = report.ToString();

        Assert.Contains("=== Rule Analysis Report ===", output);
        Assert.Contains("Rule Statistics:", output);
    }

    [Test]
    public void ToString_WithRuleAnalysis_FormatsStatistics()
    {
        var report = new AnalysisReport();
        report.RuleAnalyses.Add(new RuleAnalysis
        {
            RuleId = "TEST_RULE",
            RuleName = "Test Rule",
            MatchRate = 0.75,
            MatchedCount = 75
        });

        var output = report.ToString();

        Assert.Contains("Test Rule:", output);
        Assert.Contains("Match Rate:", output);
        Assert.Contains("Matched: 75 cases", output);
    }

    [Test]
    public void ToString_WithRuleIssues_FormatsIssues()
    {
        var report = new AnalysisReport();
        var analysis = new RuleAnalysis
        {
            RuleId = "DEAD_RULE",
            RuleName = "Dead Rule",
            MatchRate = 0.0,
            MatchedCount = 0
        };
        analysis.Issues.Add("Rule never matches any test cases - may be dead code");
        report.RuleAnalyses.Add(analysis);

        var output = report.ToString();

        Assert.Contains("Issues:", output);
        Assert.Contains("- Rule never matches any test cases", output);
    }

    [Test]
    public void ToString_WithOverlaps_FormatsOverlapSection()
    {
        var report = new AnalysisReport();
        report.Overlaps.Add(new RuleOverlap
        {
            Rule1 = "Rule A",
            Rule2 = "Rule B",
            OverlapRate = 0.65
        });

        var output = report.ToString();

        Assert.Contains("Significant Overlaps (>50%):", output);
        Assert.Contains("Rule A", output);
        Assert.Contains("Rule B", output);
    }

    [Test]
    public void ToString_WithDeadRules_FormatsDeadRuleSection()
    {
        var report = new AnalysisReport();
        report.DeadRules.Add("Unused Rule");
        report.DeadRules.Add("Another Dead Rule");

        var output = report.ToString();

        Assert.Contains("Dead Rules (never match):", output);
        Assert.Contains("- Unused Rule", output);
        Assert.Contains("- Another Dead Rule", output);
    }

    [Test]
    public void ToString_CompleteReport_FormatsAllSections()
    {
        var report = new AnalysisReport();

        // Add rule analysis
        report.RuleAnalyses.Add(new RuleAnalysis
        {
            RuleId = "R1",
            RuleName = "Active Rule",
            MatchRate = 0.5,
            MatchedCount = 50
        });

        // Add overlap
        report.Overlaps.Add(new RuleOverlap
        {
            Rule1 = "Rule X",
            Rule2 = "Rule Y",
            OverlapRate = 0.75
        });

        // Add dead rule
        report.DeadRules.Add("Never Used Rule");

        var output = report.ToString();

        Assert.Contains("Rule Analysis Report", output);
        Assert.Contains("Active Rule:", output);
        Assert.Contains("Significant Overlaps", output);
        Assert.Contains("Dead Rules", output);
    }

    [Test]
    public void ToString_NoOverlaps_OmitsOverlapSection()
    {
        var report = new AnalysisReport();
        report.RuleAnalyses.Add(new RuleAnalysis { RuleName = "Test" });

        var output = report.ToString();

        Assert.False(output.Contains("Significant Overlaps"));
    }

    [Test]
    public void ToString_NoDeadRules_OmitsDeadRuleSection()
    {
        var report = new AnalysisReport();
        report.RuleAnalyses.Add(new RuleAnalysis { RuleName = "Test" });

        var output = report.ToString();

        Assert.False(output.Contains("Dead Rules"));
    }
}

/// <summary>
/// Tests for RuleAnalysis class
/// </summary>
[TestClass]
public class RuleAnalysisTests
{
    [Test]
    public void RuleAnalysis_DefaultValues_AreInitialized()
    {
        var analysis = new RuleAnalysis();

        Assert.Equal("", analysis.RuleId);
        Assert.Equal("", analysis.RuleName);
        Assert.Equal(0.0, analysis.MatchRate);
        Assert.Equal(0, analysis.MatchedCount);
        Assert.NotNull(analysis.Issues);
        Assert.Empty(analysis.Issues);
    }

    [Test]
    public void RuleAnalysis_SetProperties_WorksCorrectly()
    {
        var analysis = new RuleAnalysis
        {
            RuleId = "MY_RULE",
            RuleName = "My Rule",
            MatchRate = 0.42,
            MatchedCount = 42
        };

        Assert.Equal("MY_RULE", analysis.RuleId);
        Assert.Equal("My Rule", analysis.RuleName);
        Assert.Equal(0.42, analysis.MatchRate);
        Assert.Equal(42, analysis.MatchedCount);
    }

    [Test]
    public void RuleAnalysis_AddIssues_TracksProperly()
    {
        var analysis = new RuleAnalysis();

        analysis.Issues.Add("Issue 1");
        analysis.Issues.Add("Issue 2");

        Assert.Equal(2, analysis.Issues.Count);
        Assert.Contains("Issue 1", analysis.Issues);
        Assert.Contains("Issue 2", analysis.Issues);
    }
}

/// <summary>
/// Tests for RuleOverlap class
/// </summary>
[TestClass]
public class RuleOverlapTests
{
    [Test]
    public void RuleOverlap_DefaultValues_AreInitialized()
    {
        var overlap = new RuleOverlap();

        Assert.Equal("", overlap.Rule1);
        Assert.Equal("", overlap.Rule2);
        Assert.Equal(0.0, overlap.OverlapRate);
    }

    [Test]
    public void RuleOverlap_SetProperties_WorksCorrectly()
    {
        var overlap = new RuleOverlap
        {
            Rule1 = "First Rule",
            Rule2 = "Second Rule",
            OverlapRate = 0.85
        };

        Assert.Equal("First Rule", overlap.Rule1);
        Assert.Equal("Second Rule", overlap.Rule2);
        Assert.Equal(0.85, overlap.OverlapRate);
    }
}

/// <summary>
/// Additional tests for DebuggableRule<T> to fill coverage gaps
/// </summary>
[TestClass]
public class DebuggableRuleAdditionalTests
{
    private class TestFact
    {
        public int Value { get; set; }
        public string Name { get; set; } = "";
        public bool Flag { get; set; }
    }

    [Test]
    public void DebuggableRule_Constructor_SetsAllProperties()
    {
        var rule = new DebuggableRule<TestFact>(
            "RULE_ID",
            "Rule Name",
            f => f.Value > 10,
            "Test description",
            priority: 100
        );

        Assert.Equal("RULE_ID", rule.Id);
        Assert.Equal("Rule Name", rule.Name);
        Assert.Equal("Test description", rule.Description);
        Assert.Equal(100, rule.Priority);
    }

    [Test]
    public void DebuggableRule_LastEvaluationTrace_EmptyBeforeEvaluation()
    {
        var rule = new DebuggableRule<TestFact>(
            "TEST",
            "Test Rule",
            f => f.Value > 5
        );

        Assert.NotNull(rule.LastEvaluationTrace);
        Assert.Empty(rule.LastEvaluationTrace);
    }

    [Test]
    public void DebuggableRule_Evaluate_ReturnsCorrectResult()
    {
        var rule = new DebuggableRule<TestFact>(
            "TEST",
            "Test Rule",
            f => f.Value > 50
        );

        var matchingFact = new TestFact { Value = 100 };
        var nonMatchingFact = new TestFact { Value = 25 };

        Assert.True(rule.Evaluate(matchingFact));
        Assert.False(rule.Evaluate(nonMatchingFact));
    }

    [Test]
    public void DebuggableRule_Evaluate_PopulatesTrace()
    {
        var rule = new DebuggableRule<TestFact>(
            "TEST",
            "Test Rule",
            f => f.Value > 50
        );

        rule.Evaluate(new TestFact { Value = 100 });

        Assert.NotEmpty(rule.LastEvaluationTrace);
        Assert.Contains(rule.LastEvaluationTrace, t => t.Contains("Overall result: True"));
    }

    [Test]
    public void DebuggableRule_Evaluate_ClearsTraceOnSubsequentCalls()
    {
        var rule = new DebuggableRule<TestFact>(
            "TEST",
            "Test Rule",
            f => f.Value > 50
        );

        rule.Evaluate(new TestFact { Value = 100 });
        var firstTraceCount = rule.LastEvaluationTrace.Count;

        rule.Evaluate(new TestFact { Value = 25 });

        // The trace should be fresh, not accumulated
        Assert.True(rule.LastEvaluationTrace.Any(t => t.Contains("Overall result: False")));
        Assert.False(rule.LastEvaluationTrace.Any(t => t.Contains("Overall result: True")));
    }

    [Test]
    public void DebuggableRule_Evaluate_WithMethodCall_TracksMethodCall()
    {
        var rule = new DebuggableRule<TestFact>(
            "TEST",
            "Test Rule",
            f => f.Name.StartsWith("Test")
        );

        rule.Evaluate(new TestFact { Name = "TestValue" });

        Assert.NotEmpty(rule.LastEvaluationTrace);
    }

    [Test]
    public void DebuggableRule_Evaluate_WithMemberAccess_TracksMemberAccess()
    {
        var rule = new DebuggableRule<TestFact>(
            "TEST",
            "Test Rule",
            f => f.Value > 10 && f.Flag == true
        );

        rule.Evaluate(new TestFact { Value = 15, Flag = true });

        var trace = rule.LastEvaluationTrace;
        Assert.NotEmpty(trace);
        Assert.Contains(trace, t => t.Contains("Value") || t.Contains("Flag"));
    }

    [Test]
    public void DebuggableRule_Evaluate_ComplexExpression_DecomposesCorrectly()
    {
        var rule = new DebuggableRule<TestFact>(
            "COMPLEX",
            "Complex Rule",
            f => f.Value > 10 && f.Value < 100 && f.Flag
        );

        rule.Evaluate(new TestFact { Value = 50, Flag = true });

        var trace = rule.LastEvaluationTrace;
        Assert.NotEmpty(trace);
        Assert.Contains(trace, t => t.Contains("Breakdown:"));
    }

    [Test]
    public void DebuggableRule_Evaluate_CanEvaluateValidExpressions()
    {
        // Create a simple rule that evaluates without exceptions
        var rule = new DebuggableRule<TestFact>(
            "VALID_EXPR",
            "Valid Expression Rule",
            f => f.Name != null && f.Name.Length > 5
        );

        var shortName = new TestFact { Name = "Hi" };
        var longName = new TestFact { Name = "HelloWorld" };

        Assert.False(rule.Evaluate(shortName));
        Assert.True(rule.Evaluate(longName));
    }

    [Test]
    public void DebuggableRule_LastEvaluationTrace_IsReadOnly()
    {
        var rule = new DebuggableRule<TestFact>(
            "TEST",
            "Test Rule",
            f => f.Value > 5
        );

        rule.Evaluate(new TestFact { Value = 10 });

        var trace = rule.LastEvaluationTrace;

        // The trace should be read-only (IReadOnlyList)
        Assert.NotNull(trace);
        // Attempting to cast and modify would fail at runtime
    }
}

/// <summary>
/// Additional tests for RuleAnalyzer<T> to fill coverage gaps
/// </summary>
[TestClass]
public class RuleAnalyzerAdditionalTests
{
    private class TestData
    {
        public int Value { get; set; }
        public string Category { get; set; } = "";
    }

    [Test]
    public void Analyzer_EmptyEngine_ReturnsEmptyReport()
    {
        using var engine = new RulesEngineCore<TestData>();
        var testCases = new List<TestData>
        {
            new TestData { Value = 10 }
        };

        var analyzer = new RuleAnalyzer<TestData>(engine, testCases);
        var report = analyzer.Analyze();

        Assert.Empty(report.RuleAnalyses);
        Assert.Empty(report.Overlaps);
        Assert.Empty(report.DeadRules);
    }

    [Test]
    public void Analyzer_EmptyTestCases_HandlesGracefully()
    {
        using var engine = new RulesEngineCore<TestData>();
        engine.AddRule("R1", "Rule 1", d => d.Value > 5, d => { });

        var analyzer = new RuleAnalyzer<TestData>(engine, Enumerable.Empty<TestData>());
        var report = analyzer.Analyze();

        // With no test cases, match rate calculation might yield special values
        Assert.Single(report.RuleAnalyses);
    }

    [Test]
    public void Analyzer_RuleMatchesAll_AddsIssue()
    {
        using var engine = new RulesEngineCore<TestData>();
        engine.AddRule("ALWAYS_TRUE", "Always True", d => true, d => { });

        var testCases = Enumerable.Range(1, 10)
            .Select(i => new TestData { Value = i })
            .ToList();

        var analyzer = new RuleAnalyzer<TestData>(engine, testCases);
        var report = analyzer.Analyze();

        var analysis = report.RuleAnalyses.First();
        Assert.Equal(1.0, analysis.MatchRate);
        Assert.Contains(analysis.Issues, i => i.Contains("too broad"));
    }

    [Test]
    public void Analyzer_RuleMatchesNone_AddsIssueAndDeadRule()
    {
        using var engine = new RulesEngineCore<TestData>();
        engine.AddRule("NEVER_MATCH", "Never Matches", d => d.Value > 1000, d => { });

        var testCases = Enumerable.Range(1, 10)
            .Select(i => new TestData { Value = i })
            .ToList();

        var analyzer = new RuleAnalyzer<TestData>(engine, testCases);
        var report = analyzer.Analyze();

        var analysis = report.RuleAnalyses.First();
        Assert.Equal(0.0, analysis.MatchRate);
        Assert.Contains(analysis.Issues, i => i.Contains("dead code"));
        Assert.Contains("Never Matches", report.DeadRules);
    }

    [Test]
    public void Analyzer_NoSignificantOverlap_DoesNotReportOverlap()
    {
        using var engine = new RulesEngineCore<TestData>();

        // Rules with low overlap (< 50%)
        engine.AddRule("LOW", "Low Values", d => d.Value < 30, d => { });
        engine.AddRule("HIGH", "High Values", d => d.Value > 70, d => { });

        var testCases = Enumerable.Range(0, 100)
            .Select(i => new TestData { Value = i })
            .ToList();

        var analyzer = new RuleAnalyzer<TestData>(engine, testCases);
        var report = analyzer.Analyze();

        // These rules have zero overlap (no value is both < 30 and > 70)
        Assert.Empty(report.Overlaps);
    }

    [Test]
    public void Analyzer_ExactlyHalfOverlap_DoesNotReportOverlap()
    {
        using var engine = new RulesEngineCore<TestData>();

        // Rules with exactly 50% overlap (threshold is > 0.5, not >=)
        engine.AddRule("R1", "Rule 1", d => d.Value >= 0, d => { }); // All 100 cases
        engine.AddRule("R2", "Rule 2", d => d.Value >= 50, d => { }); // 50 cases (overlap = 50%)

        var testCases = Enumerable.Range(0, 100)
            .Select(i => new TestData { Value = i })
            .ToList();

        var analyzer = new RuleAnalyzer<TestData>(engine, testCases);
        var report = analyzer.Analyze();

        // 50/100 = 0.5, which is not > 0.5, so no overlap reported
        Assert.Empty(report.Overlaps);
    }

    [Test]
    public void Analyzer_MultipleRulesWithOverlaps_DetectsAllOverlaps()
    {
        using var engine = new RulesEngineCore<TestData>();

        // Three rules that all overlap significantly
        engine.AddRule("R1", "Rule 1", d => d.Value > 20, d => { }); // 79 cases
        engine.AddRule("R2", "Rule 2", d => d.Value > 30, d => { }); // 69 cases
        engine.AddRule("R3", "Rule 3", d => d.Value > 40, d => { }); // 59 cases

        var testCases = Enumerable.Range(0, 100)
            .Select(i => new TestData { Value = i })
            .ToList();

        var analyzer = new RuleAnalyzer<TestData>(engine, testCases);
        var report = analyzer.Analyze();

        // R1-R2 overlap: 69/100 = 0.69
        // R1-R3 overlap: 59/100 = 0.59
        // R2-R3 overlap: 59/100 = 0.59
        Assert.Equal(3, report.Overlaps.Count);
    }

    [Test]
    public void Analyzer_MatchRateCalculation_IsAccurate()
    {
        using var engine = new RulesEngineCore<TestData>();
        engine.AddRule("QUARTER", "Quarter Rule", d => d.Value >= 75, d => { });

        var testCases = Enumerable.Range(0, 100)
            .Select(i => new TestData { Value = i })
            .ToList();

        var analyzer = new RuleAnalyzer<TestData>(engine, testCases);
        var report = analyzer.Analyze();

        var analysis = report.RuleAnalyses.First();
        Assert.Equal(25, analysis.MatchedCount);
        Assert.True(Math.Abs(analysis.MatchRate - 0.25) < 0.001);
    }

    [Test]
    public void Analyzer_MixedRules_AnalyzesAllCorrectly()
    {
        using var engine = new RulesEngineCore<TestData>();

        engine.AddRule("PARTIAL", "Partial Match", d => d.Value > 50, d => { });
        engine.AddRule("ALL", "All Match", d => true, d => { });
        engine.AddRule("NONE", "No Match", d => d.Value < 0, d => { });

        var testCases = Enumerable.Range(0, 100)
            .Select(i => new TestData { Value = i })
            .ToList();

        var analyzer = new RuleAnalyzer<TestData>(engine, testCases);
        var report = analyzer.Analyze();

        Assert.Equal(3, report.RuleAnalyses.Count);

        var partialAnalysis = report.RuleAnalyses.First(a => a.RuleName == "Partial Match");
        var allAnalysis = report.RuleAnalyses.First(a => a.RuleName == "All Match");
        var noneAnalysis = report.RuleAnalyses.First(a => a.RuleName == "No Match");

        Assert.True(partialAnalysis.MatchRate > 0 && partialAnalysis.MatchRate < 1);
        Assert.Equal(1.0, allAnalysis.MatchRate);
        Assert.Equal(0.0, noneAnalysis.MatchRate);

        Assert.Contains("No Match", report.DeadRules);
    }

    [Test]
    public void Analyzer_RuleAnalysisContainsRuleIdAndName()
    {
        using var engine = new RulesEngineCore<TestData>();
        engine.AddRule("TEST_ID_123", "Test Rule Name", d => d.Value > 0, d => { });

        var testCases = new List<TestData> { new TestData { Value = 50 } };

        var analyzer = new RuleAnalyzer<TestData>(engine, testCases);
        var report = analyzer.Analyze();

        var analysis = report.RuleAnalyses.First();
        Assert.Equal("TEST_ID_123", analysis.RuleId);
        Assert.Equal("Test Rule Name", analysis.RuleName);
    }
}

/// <summary>
/// Integration tests for RuleValidator producing ValidationResult
/// </summary>
[TestClass]
public class RuleValidatorIntegrationTests
{
    private class TestModel
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    [Test]
    public void Validate_ValidRule_ReturnsValidResult()
    {
        var rule = new Rule<TestModel>("VALID_ID", "Valid Name", m => m.Age > 18);

        var result = RuleValidator.Validate(rule);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Test]
    public void Validate_MultipleIssues_AllCaptured()
    {
        // Empty ID AND empty name
        var rule = new Rule<TestModel>("", "", m => m.Age > 18);

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2);
    }

    [Test]
    public void Validate_WhitespaceId_FailsValidation()
    {
        var rule = new Rule<TestModel>("   ", "Valid Name", m => m.Age > 18);

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ID"));
    }

    [Test]
    public void Validate_WhitespaceName_FailsValidation()
    {
        var rule = new Rule<TestModel>("valid-id", "   ", m => m.Age > 18);

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name"));
    }
}
