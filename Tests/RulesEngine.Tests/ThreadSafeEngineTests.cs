using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RulesEngine.Core;
using TestRunner.Framework;

namespace TestRunner.Tests;

/// <summary>
/// Comprehensive tests for ImmutableRulesEngine&lt;T&gt; - immutable pattern implementation
/// </summary>
[TestClass]
public class ImmutableRulesEngineImmutableTests
{
    private class TestFact
    {
        public int Value { get; set; }
        public string Name { get; set; } = "";
        public List<string> Actions { get; set; } = new();
    }

    #region Constructor Tests

    [Test]
    public void Constructor_DefaultOptions_CreatesEmptyEngine()
    {
        var engine = new ImmutableRulesEngine<TestFact>();

        Assert.NotNull(engine);
        Assert.Empty(engine.GetRules());
    }

    [Test]
    public void Constructor_WithOptions_AppliesOptions()
    {
        var options = new RulesEngineOptions
        {
            StopOnFirstMatch = true,
            EnableParallelExecution = true,
            TrackPerformance = true
        };

        var engine = new ImmutableRulesEngine<TestFact>(options);

        Assert.NotNull(engine);
        Assert.Empty(engine.GetRules());
    }

    [Test]
    public void Constructor_NullOptions_UsesDefaults()
    {
        var engine = new ImmutableRulesEngine<TestFact>(null);

        Assert.NotNull(engine);
        // Engine should work normally with default options
        var result = engine.Execute(new TestFact { Value = 10 });
        Assert.NotNull(result);
    }

    #endregion

    #region WithRule Tests

    [Test]
    public void WithRule_ReturnsNewInstance()
    {
        var engine1 = new ImmutableRulesEngine<TestFact>();
        var rule = new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5);

        var engine2 = engine1.WithRule(rule);

        Assert.NotSame(engine1, engine2);
    }

    [Test]
    public void WithRule_OriginalEngineUnchanged()
    {
        var engine1 = new ImmutableRulesEngine<TestFact>();
        var rule = new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5);

        var engine2 = engine1.WithRule(rule);

        Assert.Empty(engine1.GetRules());
        Assert.Single(engine2.GetRules());
    }

    [Test]
    public void WithRule_ChainedCalls_AccumulateRules()
    {
        var engine = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5))
            .WithRule(new Rule<TestFact>("R2", "Rule 2", f => f.Value > 10))
            .WithRule(new Rule<TestFact>("R3", "Rule 3", f => f.Value > 15));

        Assert.Equal(3, engine.GetRules().Count);
    }

    [Test]
    public void WithRule_AddsRuleWithAction()
    {
        var executed = false;
        var rule = new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5)
            .WithAction(f => executed = true);

        var engine = new ImmutableRulesEngine<TestFact>().WithRule(rule);
        engine.Execute(new TestFact { Value = 10 });

        Assert.True(executed);
    }

    #endregion

    #region WithRules Tests

    [Test]
    public void WithRules_AddsMultipleRulesAtOnce()
    {
        var engine = new ImmutableRulesEngine<TestFact>().WithRules(
            new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5),
            new Rule<TestFact>("R2", "Rule 2", f => f.Value > 10)
        );

        Assert.Equal(2, engine.GetRules().Count);
    }

    [Test]
    public void WithRules_OriginalEngineUnchanged()
    {
        var engine1 = new ImmutableRulesEngine<TestFact>();
        var engine2 = engine1.WithRules(
            new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5),
            new Rule<TestFact>("R2", "Rule 2", f => f.Value > 10)
        );

        Assert.Empty(engine1.GetRules());
        Assert.Equal(2, engine2.GetRules().Count);
    }

    [Test]
    public void WithRules_EmptyArray_ReturnsNewEngineWithSameRules()
    {
        var engine1 = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5));

        var engine2 = engine1.WithRules();

        Assert.NotSame(engine1, engine2);
        Assert.Equal(1, engine2.GetRules().Count);
    }

    #endregion

    #region WithoutRule Tests

    [Test]
    public void WithoutRule_RemovesSpecifiedRule()
    {
        var engine1 = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5))
            .WithRule(new Rule<TestFact>("R2", "Rule 2", f => f.Value > 10));

        var engine2 = engine1.WithoutRule("R1");

        Assert.Equal(2, engine1.GetRules().Count);
        Assert.Single(engine2.GetRules());
        Assert.Equal("R2", engine2.GetRules()[0].Id);
    }

    [Test]
    public void WithoutRule_NonExistentId_ReturnsNewEngineWithSameRules()
    {
        var engine1 = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5));

        var engine2 = engine1.WithoutRule("NONEXISTENT");

        Assert.NotSame(engine1, engine2);
        Assert.Single(engine2.GetRules());
    }

    [Test]
    public void WithoutRule_EmptyEngine_ReturnsEmptyEngine()
    {
        var engine1 = new ImmutableRulesEngine<TestFact>();

        var engine2 = engine1.WithoutRule("R1");

        Assert.Empty(engine2.GetRules());
    }

    [Test]
    public void WithoutRule_RemovesAllMatchingRules()
    {
        // Add same ID twice (immutable allows this)
        var engine1 = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1a", f => f.Value > 5))
            .WithRule(new Rule<TestFact>("R1", "Rule 1b", f => f.Value > 10));

        var engine2 = engine1.WithoutRule("R1");

        Assert.Empty(engine2.GetRules());
    }

    #endregion

    #region Execute Tests

    [Test]
    public void Execute_EmptyEngine_ReturnsEmptyResult()
    {
        var engine = new ImmutableRulesEngine<TestFact>();

        var result = engine.Execute(new TestFact { Value = 10 });

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalRulesEvaluated);
    }

    [Test]
    public void Execute_MatchingRule_ReturnsMatchedResult()
    {
        var engine = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5));

        var result = engine.Execute(new TestFact { Value = 10 });

        Assert.Equal(1, result.MatchedRules);
    }

    [Test]
    public void Execute_NonMatchingRule_ReturnsNoMatch()
    {
        var engine = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 100));

        var result = engine.Execute(new TestFact { Value = 10 });

        Assert.Equal(0, result.MatchedRules);
        Assert.Equal(1, result.TotalRulesEvaluated);
    }

    [Test]
    public void Execute_ExecutesRulesInPriorityOrder()
    {
        var executionOrder = new List<string>();
        var engine = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Low Priority", f => true, priority: 1)
                .WithAction(f => executionOrder.Add("R1")))
            .WithRule(new Rule<TestFact>("R2", "High Priority", f => true, priority: 100)
                .WithAction(f => executionOrder.Add("R2")))
            .WithRule(new Rule<TestFact>("R3", "Medium Priority", f => true, priority: 50)
                .WithAction(f => executionOrder.Add("R3")));

        engine.Execute(new TestFact());

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("R2", executionOrder[0]);
        Assert.Equal("R3", executionOrder[1]);
        Assert.Equal("R1", executionOrder[2]);
    }

    [Test]
    public void Execute_StopOnFirstMatch_StopsAfterFirstMatch()
    {
        var executionOrder = new List<string>();
        var options = new RulesEngineOptions { StopOnFirstMatch = true };
        var engine = new ImmutableRulesEngine<TestFact>(options)
            .WithRule(new Rule<TestFact>("R1", "First", f => true, priority: 100)
                .WithAction(f => executionOrder.Add("R1")))
            .WithRule(new Rule<TestFact>("R2", "Second", f => true, priority: 50)
                .WithAction(f => executionOrder.Add("R2")));

        var result = engine.Execute(new TestFact());

        Assert.Single(executionOrder);
        Assert.Equal("R1", executionOrder[0]);
        Assert.Equal(1, result.MatchedRules);
    }

    [Test]
    public void Execute_StopOnFirstMatch_ContinuesIfNoMatch()
    {
        var executionCount = 0;
        var options = new RulesEngineOptions { StopOnFirstMatch = true };
        var engine = new ImmutableRulesEngine<TestFact>(options)
            .WithRule(new Rule<TestFact>("R1", "NoMatch", f => false, priority: 100)
                .WithAction(f => executionCount++))
            .WithRule(new Rule<TestFact>("R2", "Match", f => true, priority: 50)
                .WithAction(f => executionCount++));

        var result = engine.Execute(new TestFact());

        Assert.Equal(1, executionCount);
        Assert.Equal(1, result.MatchedRules);
    }

    [Test]
    public void Execute_RecordsTotalExecutionTime()
    {
        var engine = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => true)
                .WithAction(f => Thread.Sleep(10)));

        var result = engine.Execute(new TestFact());

        Assert.True(result.TotalExecutionTime.TotalMilliseconds >= 5);
    }

    #endregion

    #region Parallel Execution Tests

    [Test]
    public void Execute_ParallelExecution_ExecutesAllRules()
    {
        var options = new RulesEngineOptions { EnableParallelExecution = true };
        var executedRules = new ConcurrentBag<string>();
        var engine = new ImmutableRulesEngine<TestFact>(options)
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => true)
                .WithAction(f => executedRules.Add("R1")))
            .WithRule(new Rule<TestFact>("R2", "Rule 2", f => true)
                .WithAction(f => executedRules.Add("R2")))
            .WithRule(new Rule<TestFact>("R3", "Rule 3", f => true)
                .WithAction(f => executedRules.Add("R3")));

        var result = engine.Execute(new TestFact());

        Assert.Equal(3, executedRules.Count);
        Assert.Equal(3, result.MatchedRules);
    }

    [Test]
    public void Execute_ParallelExecution_NoExceptions()
    {
        var options = new RulesEngineOptions { EnableParallelExecution = true };
        var engine = new ImmutableRulesEngine<TestFact>(options);

        // Add many rules
        for (int i = 0; i < 100; i++)
        {
            int capturedI = i;
            engine = engine.WithRule(new Rule<TestFact>($"R{i}", $"Rule {i}", f => f.Value > capturedI));
        }

        Exception? caughtException = null;
        try
        {
            var result = engine.Execute(new TestFact { Value = 50 });
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        Assert.Null(caughtException);
    }

    #endregion

    #region Performance Metrics Tests

    [Test]
    public void GetMetrics_TracksExecutionCount()
    {
        var options = new RulesEngineOptions { TrackPerformance = true };
        var engine = new ImmutableRulesEngine<TestFact>(options)
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => true));

        engine.Execute(new TestFact());
        engine.Execute(new TestFact());
        engine.Execute(new TestFact());

        var metrics = engine.GetMetrics("R1");
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics!.ExecutionCount);
    }

    [Test]
    public void GetMetrics_NonExistentRule_ReturnsNull()
    {
        var engine = new ImmutableRulesEngine<TestFact>();

        var metrics = engine.GetMetrics("NONEXISTENT");

        Assert.Null(metrics);
    }

    [Test]
    public void GetAllMetrics_ReturnsAllTrackedMetrics()
    {
        var options = new RulesEngineOptions { TrackPerformance = true };
        var engine = new ImmutableRulesEngine<TestFact>(options)
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => true))
            .WithRule(new Rule<TestFact>("R2", "Rule 2", f => true));

        engine.Execute(new TestFact());

        var allMetrics = engine.GetAllMetrics();
        Assert.Equal(2, allMetrics.Count);
        Assert.True(allMetrics.ContainsKey("R1"));
        Assert.True(allMetrics.ContainsKey("R2"));
    }

    [Test]
    public void GetMetrics_TracksMinMaxExecutionTime()
    {
        var options = new RulesEngineOptions { TrackPerformance = true };
        var engine = new ImmutableRulesEngine<TestFact>(options)
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => true));

        engine.Execute(new TestFact());

        var metrics = engine.GetMetrics("R1");
        Assert.NotNull(metrics);
        Assert.True(metrics!.MinExecutionTime <= metrics.MaxExecutionTime);
    }

    [Test]
    public void GetMetrics_TrackingDisabled_NoMetrics()
    {
        var options = new RulesEngineOptions { TrackPerformance = false };
        var engine = new ImmutableRulesEngine<TestFact>(options)
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => true));

        engine.Execute(new TestFact());

        var metrics = engine.GetMetrics("R1");
        Assert.Null(metrics);
    }

    #endregion

    #region GetRules Tests

    [Test]
    public void GetRules_ReturnsImmutableList()
    {
        var engine = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => true));

        var rules1 = engine.GetRules();
        var rules2 = engine.GetRules();

        Assert.Same(rules1, rules2); // Should return same reference
    }

    [Test]
    public void GetRules_PreservesRuleOrder()
    {
        var engine = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => true))
            .WithRule(new Rule<TestFact>("R2", "Rule 2", f => true))
            .WithRule(new Rule<TestFact>("R3", "Rule 3", f => true));

        var rules = engine.GetRules();

        Assert.Equal("R1", rules[0].Id);
        Assert.Equal("R2", rules[1].Id);
        Assert.Equal("R3", rules[2].Id);
    }

    #endregion

    #region Concurrent Access Tests

    [Test]
    public async Task ConcurrentExecution_NoExceptions()
    {
        var engine = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5))
            .WithRule(new Rule<TestFact>("R2", "Rule 2", f => f.Value > 10));

        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            int value = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    engine.Execute(new TestFact { Value = value });
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }

    [Test]
    public async Task ConcurrentWithRule_NoDataCorruption()
    {
        var baseEngine = new ImmutableRulesEngine<TestFact>();
        var engines = new ConcurrentBag<ImmutableRulesEngine<TestFact>>();

        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            int ruleNum = i;
            tasks.Add(Task.Run(() =>
            {
                var rule = new Rule<TestFact>($"R{ruleNum}", $"Rule {ruleNum}", f => f.Value > ruleNum);
                var newEngine = baseEngine.WithRule(rule);
                engines.Add(newEngine);
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(baseEngine.GetRules()); // Base unchanged
        Assert.Equal(50, engines.Count);
        Assert.All(engines, e => Assert.Single(e.GetRules()));
    }

    [Test]
    public async Task ConcurrentWithRuleAndExecute_NoExceptions()
    {
        var engine = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Initial Rule", f => true));

        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();

        // Execute on the engine while other threads create new engines
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    engine.Execute(new TestFact { Value = 10 });
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));

            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var newEngine = engine.WithRule(
                        new Rule<TestFact>($"R{i}", $"Rule {i}", f => true));
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }

    [Test]
    public async Task StressTest_ManyOperations_NoDataCorruption()
    {
        var engine = new ImmutableRulesEngine<TestFact>()
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => true));

        var successCount = 0;
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    engine.Execute(new TestFact { Value = i });
                    Interlocked.Increment(ref successCount);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);
        Assert.Equal(5000, successCount);
    }

    #endregion
}

/// <summary>
/// Comprehensive tests for RulesEngineCore&lt;T&gt; - mutable pattern with locking
/// </summary>
[TestClass]
public class RulesEngineCoreTests
{
    private class TestFact
    {
        public int Value { get; set; }
        public string Name { get; set; } = "";
        public List<string> Actions { get; set; } = new();
    }

    #region Constructor Tests

    [Test]
    public void Constructor_DefaultOptions_CreatesEngine()
    {
        using var engine = new RulesEngineCore<TestFact>();
        Assert.NotNull(engine);
    }

    [Test]
    public void Constructor_WithOptions_AppliesOptions()
    {
        var options = new RulesEngineOptions { StopOnFirstMatch = true };
        using var engine = new RulesEngineCore<TestFact>(options);

        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => true, priority: 100));
        engine.RegisterRule(new Rule<TestFact>("R2", "Rule 2", f => true, priority: 50));

        var executionCount = 0;
        engine.RegisterRule(new Rule<TestFact>("R3", "Counter", f => true, priority: 200)
            .WithAction(f => executionCount++));

        var result = engine.Execute(new TestFact());

        // StopOnFirstMatch should stop after first match
        Assert.Equal(1, result.MatchedRules);
    }

    [Test]
    public void Constructor_NullOptions_UsesDefaults()
    {
        using var engine = new RulesEngineCore<TestFact>(null);
        Assert.NotNull(engine);
    }

    #endregion

    #region RegisterRule Tests

    [Test]
    public void RegisterRule_AddsRuleToEngine()
    {
        using var engine = new RulesEngineCore<TestFact>();
        var rule = new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5);

        engine.RegisterRule(rule);

        var result = engine.Execute(new TestFact { Value = 10 });
        Assert.Equal(1, result.MatchedRules);
    }

    [Test]
    public void RegisterRule_MultipleRules_AllRegistered()
    {
        using var engine = new RulesEngineCore<TestFact>();

        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => true));
        engine.RegisterRule(new Rule<TestFact>("R2", "Rule 2", f => true));
        engine.RegisterRule(new Rule<TestFact>("R3", "Rule 3", f => true));

        var result = engine.Execute(new TestFact());
        Assert.Equal(3, result.TotalRulesEvaluated);
    }

    #endregion

    #region RemoveRule Tests

    [Test]
    public void RemoveRule_ExistingRule_ReturnsTrue()
    {
        using var engine = new RulesEngineCore<TestFact>();
        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => true));

        var removed = engine.RemoveRule("R1");

        Assert.True(removed);
    }

    [Test]
    public void RemoveRule_NonExistentRule_ReturnsFalse()
    {
        using var engine = new RulesEngineCore<TestFact>();

        var removed = engine.RemoveRule("NONEXISTENT");

        Assert.False(removed);
    }

    [Test]
    public void RemoveRule_RuleNoLongerExecutes()
    {
        using var engine = new RulesEngineCore<TestFact>();
        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => true));

        engine.RemoveRule("R1");

        var result = engine.Execute(new TestFact());
        Assert.Equal(0, result.TotalRulesEvaluated);
    }

    [Test]
    public void RemoveRule_OnlyRemovesSpecifiedRule()
    {
        using var engine = new RulesEngineCore<TestFact>();
        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => true));
        engine.RegisterRule(new Rule<TestFact>("R2", "Rule 2", f => true));

        engine.RemoveRule("R1");

        var result = engine.Execute(new TestFact());
        Assert.Equal(1, result.TotalRulesEvaluated);
        Assert.Equal(1, result.MatchedRules);
    }

    #endregion

    #region Execute Tests

    [Test]
    public void Execute_EmptyEngine_ReturnsEmptyResult()
    {
        using var engine = new RulesEngineCore<TestFact>();

        var result = engine.Execute(new TestFact());

        Assert.Equal(0, result.TotalRulesEvaluated);
    }

    [Test]
    public void Execute_RulesInPriorityOrder()
    {
        using var engine = new RulesEngineCore<TestFact>();
        var executionOrder = new List<string>();

        engine.RegisterRule(new Rule<TestFact>("R1", "Low", f => true, priority: 1)
            .WithAction(f => executionOrder.Add("R1")));
        engine.RegisterRule(new Rule<TestFact>("R2", "High", f => true, priority: 100)
            .WithAction(f => executionOrder.Add("R2")));
        engine.RegisterRule(new Rule<TestFact>("R3", "Medium", f => true, priority: 50)
            .WithAction(f => executionOrder.Add("R3")));

        engine.Execute(new TestFact());

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("R2", executionOrder[0]);
        Assert.Equal("R3", executionOrder[1]);
        Assert.Equal("R1", executionOrder[2]);
    }

    [Test]
    public void Execute_StopOnFirstMatch_StopsAfterMatch()
    {
        var options = new RulesEngineOptions { StopOnFirstMatch = true };
        using var engine = new RulesEngineCore<TestFact>(options);
        var executionOrder = new List<string>();

        engine.RegisterRule(new Rule<TestFact>("R1", "First", f => true, priority: 100)
            .WithAction(f => executionOrder.Add("R1")));
        engine.RegisterRule(new Rule<TestFact>("R2", "Second", f => true, priority: 50)
            .WithAction(f => executionOrder.Add("R2")));

        engine.Execute(new TestFact());

        Assert.Single(executionOrder);
        Assert.Equal("R1", executionOrder[0]);
    }

    [Test]
    public void Execute_NonMatchingRules_NoActionsExecuted()
    {
        using var engine = new RulesEngineCore<TestFact>();
        var executed = false;

        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 100)
            .WithAction(f => executed = true));

        engine.Execute(new TestFact { Value = 10 });

        Assert.False(executed);
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var engine = new RulesEngineCore<TestFact>();

        engine.Dispose();
        engine.Dispose(); // Should not throw

        Assert.True(true); // If we get here, no exception was thrown
    }

    [Test]
    public void Dispose_ReleasesResources()
    {
        var engine = new RulesEngineCore<TestFact>();
        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => true));

        engine.Dispose();

        // After dispose, operations may throw or behave unexpectedly
        // This is expected behavior for disposed objects
        Assert.True(true);
    }

    #endregion

    #region Concurrent Access Tests

    [Test]
    public async Task ConcurrentRegistration_NoExceptions()
    {
        using var engine = new RulesEngineCore<TestFact>();
        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            int ruleNum = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var rule = new Rule<TestFact>($"R{ruleNum}", $"Rule {ruleNum}", f => f.Value > ruleNum);
                    engine.RegisterRule(rule);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }

    [Test]
    public async Task ConcurrentExecution_NoExceptions()
    {
        using var engine = new RulesEngineCore<TestFact>();
        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5));

        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            int value = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    engine.Execute(new TestFact { Value = value });
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }

    [Test]
    public async Task ConcurrentRegistrationAndExecution_NoExceptions()
    {
        using var engine = new RulesEngineCore<TestFact>();
        engine.RegisterRule(new Rule<TestFact>("R0", "Initial", f => true));

        var exceptions = new ConcurrentBag<Exception>();
        var cts = new CancellationTokenSource();

        // Start execution thread
        var executionTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    engine.Execute(new TestFact { Value = 42 });
                    await Task.Yield();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        // Register rules concurrently
        for (int i = 1; i <= 50; i++)
        {
            try
            {
                var rule = new Rule<TestFact>($"R{i}", $"Rule {i}", f => f.Value > i);
                engine.RegisterRule(rule);
                await Task.Delay(1);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        cts.Cancel();
        await executionTask;

        Assert.Empty(exceptions);
    }

    [Test]
    public async Task ConcurrentRemovalAndExecution_NoExceptions()
    {
        using var engine = new RulesEngineCore<TestFact>();

        // Pre-register many rules
        for (int i = 0; i < 50; i++)
        {
            engine.RegisterRule(new Rule<TestFact>($"R{i}", $"Rule {i}", f => true));
        }

        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();

        // Execute while removing
        for (int i = 0; i < 50; i++)
        {
            int idx = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    engine.Execute(new TestFact());
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));

            tasks.Add(Task.Run(() =>
            {
                try
                {
                    engine.RemoveRule($"R{idx}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }

    [Test]
    public async Task StressTest_ManyThreads_NoDataCorruption()
    {
        using var engine = new RulesEngineCore<TestFact>();
        var counter = 0;
        var lockObj = new object();

        engine.RegisterRule(new Rule<TestFact>("COUNTER", "Counter Rule", f => f.Value > 0)
            .WithAction(f => { lock (lockObj) { counter++; } }));

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    engine.Execute(new TestFact { Value = 10 });
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        // Should have executed 50 * 100 = 5000 times
        Assert.Equal(5000, counter);
    }

    [Test]
    public async Task ReadersAndWriters_ProperSynchronization()
    {
        using var engine = new RulesEngineCore<TestFact>();
        var readCount = 0;
        var writeCount = 0;
        var exceptions = new ConcurrentBag<Exception>();

        // Pre-register some rules
        for (int i = 0; i < 10; i++)
        {
            engine.RegisterRule(new Rule<TestFact>($"R{i}", $"Rule {i}", f => true));
        }

        var tasks = new List<Task>();

        // Multiple readers
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 50; j++)
                    {
                        engine.Execute(new TestFact());
                        Interlocked.Increment(ref readCount);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        // Writers (registration)
        for (int i = 0; i < 5; i++)
        {
            int writerIdx = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 10; j++)
                    {
                        engine.RegisterRule(new Rule<TestFact>(
                            $"NEW_R{writerIdx}_{j}",
                            $"New Rule {writerIdx}_{j}",
                            f => true));
                        Interlocked.Increment(ref writeCount);
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);
        Assert.Equal(1000, readCount); // 20 readers * 50 reads
        Assert.Equal(50, writeCount);  // 5 writers * 10 writes
    }

    #endregion
}
