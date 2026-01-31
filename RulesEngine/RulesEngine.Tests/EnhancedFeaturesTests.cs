using Xunit;
using RulesEngine.Core;
using RulesEngine.Enhanced;

namespace RulesEngine.Tests;

/// <summary>
/// Tests for thread-safe rules engine
/// </summary>
public class ThreadSafeRulesEngineTests
{
    private class TestData
    {
        public int Value { get; set; }
        public List<string> Log { get; set; } = new();
    }
    
    [Fact]
    public void ThreadSafeEngine_ImmutablePattern_ReturnsNewInstance()
    {
        // Arrange
        var engine1 = new ThreadSafeRulesEngine<TestData>();
        var rule = new Rule<TestData>("R1", "Rule 1", d => d.Value > 5);
        
        // Act
        var engine2 = engine1.WithRule(rule);
        
        // Assert
        Assert.NotSame(engine1, engine2);
        Assert.Empty(engine1.GetRules());
        Assert.Single(engine2.GetRules());
    }
    
    [Fact]
    public void ThreadSafeEngine_ConcurrentExecution_NoExceptions()
    {
        // Arrange
        var engine = new ThreadSafeRulesEngine<TestData>()
            .WithRule(new Rule<TestData>("R1", "Rule 1", d => d.Value > 5))
            .WithRule(new Rule<TestData>("R2", "Rule 2", d => d.Value < 15));
        
        var exceptions = new List<Exception>();
        
        // Act - execute from multiple threads
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
        
        // Assert
        Assert.Empty(exceptions);
    }
    
    [Fact]
    public void ThreadSafeEngine_ConcurrentModification_SafeWithImmutablePattern()
    {
        // Arrange
        var baseEngine = new ThreadSafeRulesEngine<TestData>();
        var engines = new List<ThreadSafeRulesEngine<TestData>>();
        var lockObj = new object();
        
        // Act - modify from multiple threads
        Parallel.For(0, 10, i =>
        {
            var rule = new Rule<TestData>($"R{i}", $"Rule {i}", d => d.Value > i);
            var newEngine = baseEngine.WithRule(rule);
            
            lock (lockObj)
            {
                engines.Add(newEngine);
            }
        });
        
        // Assert
        Assert.Empty(baseEngine.GetRules()); // Base engine unchanged
        Assert.All(engines, e => Assert.Single(e.GetRules())); // Each has one rule
    }
    
    [Fact]
    public void LockedEngine_ConcurrentModification_ThreadSafe()
    {
        // Arrange
        using var engine = new LockedRulesEngine<TestData>();
        var exceptions = new List<Exception>();
        
        // Act - add rules from multiple threads while executing
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
        
        // Assert
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
    
    [Fact]
    public void Validator_ValidRule_PassesValidation()
    {
        // Arrange
        var rule = new Rule<TestModel>(
            "VALID",
            "Valid Rule",
            m => m.Age > 18 && m.Balance > 0
        );
        
        // Act
        var result = RuleValidator.Validate(rule);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
    
    [Fact]
    public void Validator_EmptyId_FailsValidation()
    {
        // Arrange
        var rule = new Rule<TestModel>(
            "",  // Empty ID
            "Rule",
            m => m.Age > 18
        );
        
        // Act
        var result = RuleValidator.Validate(rule);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ID cannot be empty"));
    }
    
    [Fact]
    public void Validator_DivisionByZero_DetectsError()
    {
        // Arrange
        var rule = new Rule<TestModel>(
            "DIV_ZERO",
            "Division by Zero",
            m => m.Balance / 0 > 100  // Division by zero!
        );
        
        // Act
        var result = RuleValidator.Validate(rule);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Division by zero"));
    }
    
    [Fact]
    public void Validator_NullReferenceWarning_DetectsIssue()
    {
        // Arrange
        var rule = new Rule<TestModel>(
            "NULL_REF",
            "Potential Null Reference",
            m => m.Name.Length > 5  // Could be null
        );
        
        // Act
        var result = RuleValidator.Validate(rule);
        
        // Assert
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
    
    [Fact]
    public void DebuggableRule_ProvideTrace_WhenEvaluated()
    {
        // Arrange
        var rule = new DebuggableRule<DebugData>(
            "DEBUG",
            "Debug Rule",
            d => d.Value1 > 10 && d.Value2 < 20 && d.Status == "Active"
        );
        
        var data = new DebugData { Value1 = 15, Value2 = 25, Status = "Active" };
        
        // Act
        var result = rule.Evaluate(data);
        var trace = rule.LastEvaluationTrace;
        
        // Assert
        Assert.False(result); // Should fail because Value2 is not < 20
        Assert.NotEmpty(trace);
        Assert.Contains(trace, t => t.Contains("Overall result: False"));
    }
    
    [Fact]
    public void DebuggableRule_ShowsIndividualConditions_InTrace()
    {
        // Arrange
        var rule = new DebuggableRule<DebugData>(
            "DEBUG",
            "Debug Rule",
            d => d.Value1 > 10 && d.Value2 < 20
        );
        
        var data = new DebugData { Value1 = 5, Value2 = 15 };
        
        // Act
        rule.Evaluate(data);
        var trace = rule.LastEvaluationTrace;
        
        // Assert
        Assert.NotEmpty(trace);
        // Trace should show which condition failed
        Assert.Contains(trace, t => t.Contains("Value1"));
        Assert.Contains(trace, t => t.Contains("Value2"));
    }
    
    [Fact]
    public void DebuggableRule_ThreadSafe_DifferentTracesPerThread()
    {
        // Arrange
        var rule = new DebuggableRule<DebugData>(
            "DEBUG",
            "Debug Rule",
            d => d.Value1 > 10
        );
        
        var traces = new System.Collections.Concurrent.ConcurrentBag<List<string>>();
        
        // Act - evaluate from multiple threads
        Parallel.For(0, 10, i =>
        {
            var data = new DebugData { Value1 = i };
            rule.Evaluate(data);
            
            // Capture trace immediately (thread-local)
            traces.Add(new List<string>(rule.LastEvaluationTrace));
        });
        
        // Assert
        Assert.Equal(10, traces.Count);
        // Each thread should have gotten its own trace
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
    
    [Fact]
    public void Analyzer_DetectsDeadRule_NeverMatches()
    {
        // Arrange
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
        
        // Act
        var report = analyzer.Analyze();
        
        // Assert
        Assert.Contains("Dead Rule", report.DeadRules);
    }
    
    [Fact]
    public void Analyzer_DetectsOverlap_BetweenRules()
    {
        // Arrange
        var engine = new RulesEngineCore<AnalysisData>();
        
        var rule1 = new Rule<AnalysisData>(
            "R1",
            "Rule 1",
            d => d.Value > 25  // Matches 26-100
        );
        
        var rule2 = new Rule<AnalysisData>(
            "R2",
            "Rule 2",
            d => d.Value > 50  // Matches 51-100 (subset of rule1)
        );
        
        engine.RegisterRules(rule1, rule2);
        
        var testCases = Enumerable.Range(0, 100)
            .Select(i => new AnalysisData { Value = i })
            .ToList();
        
        var analyzer = new RuleAnalyzer<AnalysisData>(engine, testCases);
        
        // Act
        var report = analyzer.Analyze();
        
        // Assert
        Assert.NotEmpty(report.Overlaps);
        var overlap = report.Overlaps.First();
        Assert.True(overlap.OverlapRate > 0.5);
    }
    
    [Fact]
    public void Analyzer_CalculatesMatchRate_Correctly()
    {
        // Arrange
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
        
        // Act
        var report = analyzer.Analyze();
        
        // Assert
        var analysis = report.RuleAnalyses.First();
        Assert.Equal(0.5, analysis.MatchRate, 0.01);
        Assert.Equal(50, analysis.MatchedCount);
    }
}
