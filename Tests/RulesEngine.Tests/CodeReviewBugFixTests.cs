using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RulesEngine.Core;
using TestRunner.Framework;

namespace TestRunner.Tests;

/// <summary>
/// Regression tests for bug fixes identified in the 2026-02-08 code review.
/// Each test validates a specific CR (Code Review) fix.
/// </summary>
[TestClass]
public class CodeReviewBugFixTests
{
    private class TestFact
    {
        public int Value { get; set; }
        public string Name { get; set; } = "";
        public List<string> Actions { get; set; } = new();
    }

    #region CR-01: ImmutableRulesEngine isolated metrics

    [Test]
    public void CR01_ImmutableEngine_WithRule_MetricsAreIsolated()
    {
        // Arrange: Two engines created from WithRule should have independent metrics
        var options = new RulesEngineOptions { TrackPerformance = true };
        var engine1 = new ImmutableRulesEngine<TestFact>(options);

        var rule1 = new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5);
        var rule2 = new Rule<TestFact>("R2", "Rule 2", f => f.Value > 10);

        var engine2 = engine1.WithRule(rule1);
        var engine3 = engine2.WithRule(rule2);

        // Act: Execute on engine2 and engine3 separately
        engine2.Execute(new TestFact { Value = 7 });
        engine3.Execute(new TestFact { Value = 15 });

        // Assert: Each engine's metrics should be independent
        var metrics2 = engine2.GetAllMetrics();
        var metrics3 = engine3.GetAllMetrics();

        // engine2 has R1 only, engine3 has R1+R2
        Assert.True(metrics2.ContainsKey("R1"), "engine2 should have R1 metrics");
        Assert.True(metrics3.ContainsKey("R1"), "engine3 should have R1 metrics");
        Assert.True(metrics3.ContainsKey("R2"), "engine3 should have R2 metrics");

        // Critically: engine2 should NOT have R2 metrics (proves isolation)
        Assert.False(metrics2.ContainsKey("R2"),
            "engine2 must NOT have R2 metrics - proves metrics are isolated between instances");
    }

    [Test]
    public void CR01_ImmutableEngine_WithoutRule_MetricsAreIsolated()
    {
        var options = new RulesEngineOptions { TrackPerformance = true };
        var rule1 = new Rule<TestFact>("R1", "Rule 1", f => f.Value > 5);
        var rule2 = new Rule<TestFact>("R2", "Rule 2", f => f.Value > 10);

        var engine1 = new ImmutableRulesEngine<TestFact>(options)
            .WithRules(rule1, rule2);
        var engine2 = engine1.WithoutRule("R2");

        // Execute on both
        engine1.Execute(new TestFact { Value = 15 });
        engine2.Execute(new TestFact { Value = 7 });

        var metrics1 = engine1.GetAllMetrics();
        var metrics2 = engine2.GetAllMetrics();

        // engine1 should have both R1+R2, engine2 should have R1 only
        Assert.True(metrics1.ContainsKey("R2"), "engine1 should have R2 metrics");
        Assert.False(metrics2.ContainsKey("R2"), "engine2 should not have R2 metrics after removal");
    }

    #endregion

    #region CR-05b: ImmutableRulesEngine MaxRulesToExecute

    [Test]
    public void CR05b_ImmutableEngine_MaxRulesToExecute_LimitsExecution()
    {
        var options = new RulesEngineOptions { MaxRulesToExecute = 2 };
        var executionOrder = new List<string>();

        var engine = new ImmutableRulesEngine<TestFact>(options)
            .WithRule(new Rule<TestFact>("R1", "Rule 1", f => true, priority: 30))
            .WithRule(new Rule<TestFact>("R2", "Rule 2", f => true, priority: 20))
            .WithRule(new Rule<TestFact>("R3", "Rule 3", f => true, priority: 10));

        var result = engine.Execute(new TestFact { Value = 1 });

        // Only 2 rules should be evaluated (MaxRulesToExecute = 2)
        Assert.True(result.TotalRulesEvaluated <= 2,
            $"Expected at most 2 rules evaluated, got {result.TotalRulesEvaluated}");
    }

    #endregion

    #region CR-05/06: ExecuteAsync MaxRulesToExecute for async rules + perf tracking

    [Test]
    public async Task CR05_ExecuteAsync_MaxRulesToExecute_LimitsAsyncRules()
    {
        // Arrange: 2 sync rules + 2 async rules, limit to 3 total
        var options = new RulesEngineOptions { MaxRulesToExecute = 3 };
        using var engine = new RulesEngineCore<TestFact>(options);

        engine.AddRule("sync-1", "Sync 1", f => true, f => { });
        engine.AddRule("sync-2", "Sync 2", f => true, f => { });

        var asyncRule1 = new AsyncRuleBuilder<TestFact>()
            .WithId("async-1")
            .WithName("Async 1")
            .WithPriority(0)
            .WithCondition(async f => { await Task.Yield(); return true; })
            .WithAction(async f => { await Task.Yield(); return RuleResult.Success("async-1", "Async 1"); })
            .Build();

        var asyncRule2 = new AsyncRuleBuilder<TestFact>()
            .WithId("async-2")
            .WithName("Async 2")
            .WithPriority(0)
            .WithCondition(async f => { await Task.Yield(); return true; })
            .WithAction(async f => { await Task.Yield(); return RuleResult.Success("async-2", "Async 2"); })
            .Build();

        engine.RegisterAsyncRule(asyncRule1);
        engine.RegisterAsyncRule(asyncRule2);

        // Act
        var results = (await engine.ExecuteAsync(new TestFact { Value = 1 })).ToList();

        // Assert: At most 3 rules should execute (2 sync + 1 async, or less)
        Assert.True(results.Count <= 3,
            $"Expected at most 3 results with MaxRulesToExecute=3, got {results.Count}");
    }

    [Test]
    public async Task CR06_ExecuteAsync_TrackPerformance_RecordsMetrics()
    {
        // Arrange: Enable performance tracking
        var options = new RulesEngineOptions { TrackPerformance = true };
        using var engine = new RulesEngineCore<TestFact>(options);

        engine.AddRule("sync-perf", "Sync Perf", f => true, f => { });

        var asyncRule = new AsyncRuleBuilder<TestFact>()
            .WithId("async-perf")
            .WithName("Async Perf")
            .WithCondition(async f => { await Task.Yield(); return true; })
            .WithAction(async f => { await Task.Yield(); return RuleResult.Success("async-perf", "Async Perf"); })
            .Build();
        engine.RegisterAsyncRule(asyncRule);

        // Act
        await engine.ExecuteAsync(new TestFact { Value = 1 });

        // Assert: Both sync and async rules should have metrics
        var allMetrics = engine.GetAllMetrics();
        Assert.True(allMetrics.ContainsKey("sync-perf"),
            "Sync rule should have performance metrics after ExecuteAsync");
        Assert.True(allMetrics.ContainsKey("async-perf"),
            "Async rule should have performance metrics after ExecuteAsync");
    }

    #endregion

    #region CR-07: AsyncRuleBuilder throws RuleValidationException

    [Test]
    public void CR07_AsyncRuleBuilder_MissingId_ThrowsRuleValidationException()
    {
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithName("Test")
            .WithCondition(async f => { await Task.Yield(); return true; })
            .WithAction(async f => { await Task.Yield(); return RuleResult.Success("test", "Test"); });

        var ex = Assert.Throws<RuleValidationException>(() => builder.Build());
        Assert.NotNull(ex);
    }

    [Test]
    public void CR07_AsyncRuleBuilder_MissingName_ThrowsRuleValidationException()
    {
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithId("test-id")
            .WithCondition(async f => { await Task.Yield(); return true; })
            .WithAction(async f => { await Task.Yield(); return RuleResult.Success("test", "Test"); });

        var ex = Assert.Throws<RuleValidationException>(() => builder.Build());
        Assert.NotNull(ex);
    }

    [Test]
    public void CR07_AsyncRuleBuilder_MissingCondition_ThrowsRuleValidationException()
    {
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithId("test-id")
            .WithName("Test")
            .WithAction(async f => { await Task.Yield(); return RuleResult.Success("test", "Test"); });

        var ex = Assert.Throws<RuleValidationException>(() => builder.Build());
        Assert.NotNull(ex);
    }

    [Test]
    public void CR07_AsyncRuleBuilder_MissingAction_ThrowsRuleValidationException()
    {
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithId("test-id")
            .WithName("Test")
            .WithCondition(async f => { await Task.Yield(); return true; });

        var ex = Assert.Throws<RuleValidationException>(() => builder.Build());
        Assert.NotNull(ex);
    }

    #endregion

    #region CR-36: ImmutableRulesEngine options isolation

    [Test]
    public void CR36_ImmutableEngine_WithRule_OptionsAreIsolated()
    {
        // Arrange: Create engine with specific options
        var options = new RulesEngineOptions { MaxRulesToExecute = 5 };
        var engine1 = new ImmutableRulesEngine<int>(options);

        var rule = new RuleBuilder<int>()
            .WithId("test-rule")
            .WithName("Test Rule")
            .When(x => x > 0)
            .Build();

        var engine2 = engine1.WithRule(rule);

        // Act: Mutate the original options object
        options.MaxRulesToExecute = 999;

        // Assert: engine2 should NOT see the mutation
        // Execute with facts [1,2,3,4,5,6,7,8,9,10] — if limit is 5, only 5 results
        var result = engine2.Execute(1);

        // The engine should have its own copy of options, not the mutated reference.
        // If options are shared, MaxRulesToExecute would be 999 (mutated).
        // If isolated, it should still be 5.
        // We test this indirectly: add 10 rules, execute, and check result count.
        var engineWith10Rules = new ImmutableRulesEngine<int>(new RulesEngineOptions { MaxRulesToExecute = 2 });
        for (int i = 0; i < 10; i++)
        {
            engineWith10Rules = engineWith10Rules.WithRule(
                new RuleBuilder<int>()
                    .WithId($"rule-{i}")
                    .WithName($"Rule {i}")
                    .When(x => true)
                    .Build());
        }

        var originalOptions = new RulesEngineOptions { MaxRulesToExecute = 2 };
        var baseEngine = new ImmutableRulesEngine<int>(originalOptions);
        for (int i = 0; i < 10; i++)
        {
            baseEngine = baseEngine.WithRule(
                new RuleBuilder<int>()
                    .WithId($"rule-{i}")
                    .WithName($"Rule {i}")
                    .When(x => true)
                    .Build());
        }

        // Mutate AFTER creating derived engines
        originalOptions.MaxRulesToExecute = 100;

        var result2 = baseEngine.Execute(42);
        // If options are isolated: limit stays 2, so MatchedCount should be 2
        // If options are shared: limit becomes 100, MatchedCount would be 10
        Assert.Equal(2, result2.MatchedRules);
    }

    #endregion
}
