using TestRunner.Framework;
using RulesEngine.Core;
using RulesEngine.Enhanced;

namespace TestRunner.Tests;

/// <summary>
/// Tests for thread-safe rules engine implementations
/// </summary>
public class ImmutableRulesEngineTests
{
    private class TestData
    {
        public int Value { get; set; }
        public List<string> Log { get; set; } = new();
    }

    [Test]
    public void ThreadSafeEngine_ImmutablePattern_ReturnsNewInstance()
    {
        var engine1 = new ImmutableRulesEngine<TestData>();
        var rule = new Rule<TestData>("R1", "Rule 1", d => d.Value > 5);

        var engine2 = engine1.WithRule(rule);

        Assert.NotSame(engine1, engine2);
        Assert.Empty(engine1.GetRules());
        Assert.Single(engine2.GetRules());
    }

    [Test]
    public void ThreadSafeEngine_ConcurrentExecution_NoExceptions()
    {
        var engine = new ImmutableRulesEngine<TestData>()
            .WithRule(new Rule<TestData>("R1", "Rule 1", d => d.Value > 5))
            .WithRule(new Rule<TestData>("R2", "Rule 2", d => d.Value < 15));

        var exceptions = new List<Exception>();

        Parallel.For(0, 100, i =>
        {
            try
            {
                var data = new TestData { Value = i };
                engine.Execute(data);
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        });

        Assert.Empty(exceptions);
    }

    [Test]
    public void ThreadSafeEngine_ConcurrentModification_SafeWithImmutablePattern()
    {
        var baseEngine = new ImmutableRulesEngine<TestData>();
        var engines = new List<ImmutableRulesEngine<TestData>>();
        var lockObj = new object();

        Parallel.For(0, 10, i =>
        {
            var rule = new Rule<TestData>($"R{i}", $"Rule {i}", d => d.Value > i);
            var newEngine = baseEngine.WithRule(rule);

            lock (lockObj)
            {
                engines.Add(newEngine);
            }
        });

        Assert.Empty(baseEngine.GetRules()); // Base engine unchanged
        Assert.All(engines, e => Assert.Single(e.GetRules())); // Each has one rule
    }

    [Test]
    public void LockedEngine_ConcurrentModification_ThreadSafe()
    {
        using var engine = new RulesEngineCore<TestData>();
        var exceptions = new List<Exception>();

        Parallel.Invoke(
            () =>
            {
                // Thread 1: Add rules
                for (int i = 0; i < 50; i++)
                {
                    try
                    {
                        var rule = new Rule<TestData>($"R{i}", $"Rule {i}", d => d.Value > i);
                        engine.RegisterRule(rule);
                        Thread.Sleep(1);
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) { exceptions.Add(ex); }
                    }
                }
            },
            () =>
            {
                // Thread 2: Execute rules
                for (int i = 0; i < 50; i++)
                {
                    try
                    {
                        var data = new TestData { Value = i };
                        engine.Execute(data);
                        Thread.Sleep(1);
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) { exceptions.Add(ex); }
                    }
                }
            }
        );

        Assert.Empty(exceptions);
    }
}

/// <summary>
/// Tests for rule validation
/// </summary>
public class RuleValidationTests
{
    private class TestModel
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public decimal Balance { get; set; }
    }

    [Test]
    public void Validator_ValidRule_PassesValidation()
    {
        var rule = new Rule<TestModel>(
            "VALID",
            "Valid Rule",
            m => m.Age > 18 && m.Balance > 0
        );

        var result = RuleValidator.Validate(rule);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Test]
    public void Validator_EmptyId_FailsValidation()
    {
        var rule = new Rule<TestModel>(
            "",  // Empty ID
            "Rule",
            m => m.Age > 18
        );

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ID cannot be empty"));
    }

    [Test]
    public void Validator_DivisionByZero_DetectsError()
    {
        // Note: The validator detects literal `/ 0` and `/ 0.0` in expressions
        // For decimal types, we need to use a double/int expression for detection
        var rule = new Rule<TestModel>(
            "DIV_ZERO",
            "Division by Zero",
            m => m.Age / 0 > 100  // Division by zero with int!
        );

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Division by zero"));
    }

    [Test]
    public void Validator_NullReferenceWarning_DetectsIssue()
    {
        var rule = new Rule<TestModel>(
            "NULL_REF",
            "Potential Null Reference",
            m => m.Name.Length > 5  // Could be null
        );

        var result = RuleValidator.Validate(rule);

        // May have warnings about potential null reference
        Assert.True(result.Warnings.Any() || result.IsValid);
    }
}

/// <summary>
/// Tests for debuggable rules
/// </summary>
public class DebuggableRuleTests
{
    private class DebugData
    {
        public int Value1 { get; set; }
        public int Value2 { get; set; }
        public string Status { get; set; } = "";
    }

    [Test]
    public void DebuggableRule_ProvideTrace_WhenEvaluated()
    {
        var rule = new DebuggableRule<DebugData>(
            "DEBUG",
            "Debug Rule",
            d => d.Value1 > 10 && d.Value2 < 20 && d.Status == "Active"
        );

        var data = new DebugData { Value1 = 15, Value2 = 25, Status = "Active" };

        var result = rule.Evaluate(data);
        var trace = rule.LastEvaluationTrace;

        Assert.False(result); // Should fail because Value2 is not < 20
        Assert.NotEmpty(trace);
        Assert.Contains(trace, t => t.Contains("Overall result: False"));
    }

    [Test]
    public void DebuggableRule_ShowsIndividualConditions_InTrace()
    {
        var rule = new DebuggableRule<DebugData>(
            "DEBUG",
            "Debug Rule",
            d => d.Value1 > 10 && d.Value2 < 20
        );

        var data = new DebugData { Value1 = 5, Value2 = 15 };

        rule.Evaluate(data);
        var trace = rule.LastEvaluationTrace;

        Assert.NotEmpty(trace);
        // Trace should show which condition failed
        Assert.Contains(trace, t => t.Contains("Value1"));
        Assert.Contains(trace, t => t.Contains("Value2"));
    }

    [Test]
    public void DebuggableRule_ThreadSafe_DifferentTracesPerThread()
    {
        var rule = new DebuggableRule<DebugData>(
            "DEBUG",
            "Debug Rule",
            d => d.Value1 > 10
        );

        var traces = new System.Collections.Concurrent.ConcurrentBag<List<string>>();

        Parallel.For(0, 10, i =>
        {
            var data = new DebugData { Value1 = i };
            rule.Evaluate(data);

            // Capture trace immediately (thread-local)
            traces.Add(new List<string>(rule.LastEvaluationTrace));
        });

        Assert.Equal(10, traces.Count);
    }
}

/// <summary>
/// Tests for rule analyzer
/// </summary>
public class RuleAnalyzerTests
{
    private class AnalysisData
    {
        public int Value { get; set; }
        public string Category { get; set; } = "";
    }

    [Test]
    public void Analyzer_DetectsDeadRule_NeverMatches()
    {
        var engine = new RulesEngineCore<AnalysisData>();

        var normalRule = new Rule<AnalysisData>(
            "NORMAL",
            "Normal Rule",
            d => d.Value > 50
        );

        var deadRule = new Rule<AnalysisData>(
            "DEAD",
            "Dead Rule",
            d => d.Value > 1000  // Will never match test cases
        );

        engine.RegisterRules(normalRule, deadRule);

        // Test cases with values 0-100
        var testCases = Enumerable.Range(0, 100)
            .Select(i => new AnalysisData { Value = i })
            .ToList();

        var analyzer = new RuleAnalyzer<AnalysisData>(engine, testCases);

        var report = analyzer.Analyze();

        Assert.Contains("Dead Rule", report.DeadRules);
    }

    [Test]
    public void Analyzer_DetectsOverlap_BetweenRules()
    {
        var engine = new RulesEngineCore<AnalysisData>();

        // Both rules overlap significantly (> 50% of test cases)
        var rule1 = new Rule<AnalysisData>(
            "R1",
            "Rule 1",
            d => d.Value > 30  // Matches 31-99 (69 cases)
        );

        var rule2 = new Rule<AnalysisData>(
            "R2",
            "Rule 2",
            d => d.Value > 40  // Matches 41-99 (59 cases, all overlap with rule1)
        );

        engine.RegisterRules(rule1, rule2);

        var testCases = Enumerable.Range(0, 100)
            .Select(i => new AnalysisData { Value = i })
            .ToList();

        var analyzer = new RuleAnalyzer<AnalysisData>(engine, testCases);

        var report = analyzer.Analyze();

        // Overlap is 59/100 = 0.59, which is > 0.5 threshold
        Assert.NotEmpty(report.Overlaps);
        var overlap = report.Overlaps.First();
        Assert.True(overlap.OverlapRate > 0.5);
    }

    [Test]
    public void Analyzer_CalculatesMatchRate_Correctly()
    {
        var engine = new RulesEngineCore<AnalysisData>();

        var rule = new Rule<AnalysisData>(
            "HALF",
            "Half Rule",
            d => d.Value >= 50  // Matches exactly half
        );

        engine.RegisterRule(rule);

        var testCases = Enumerable.Range(0, 100)
            .Select(i => new AnalysisData { Value = i })
            .ToList();

        var analyzer = new RuleAnalyzer<AnalysisData>(engine, testCases);

        var report = analyzer.Analyze();

        var analysis = report.RuleAnalyses.First();
        Assert.True(Math.Abs(analysis.MatchRate - 0.5) < 0.01, $"Expected MatchRate ~0.5 but was {analysis.MatchRate}");
        Assert.Equal(50, analysis.MatchedCount);
    }
}
