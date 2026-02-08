using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RulesEngine.Core;
using TestRunner.Framework;

namespace TestRunner.Tests;

[TestClass]
public class AsyncExecutionTests
{
    [Test]
    public async Task ExecuteAsync_ReturnsResults()
    {
        using var engine = new RulesEngineCore<int>();
        engine.AddRule("test", "Test Rule", x => x > 5, x => { });

        var results = await engine.ExecuteAsync(10);

        Assert.True(results.Any(), "Should have at least one result");
        Assert.Equal("test", results.First().RuleId);
    }

    [Test]
    public async Task ExecuteAsync_WithCancellation_ThrowsWhenCancelled()
    {
        using var engine = new RulesEngineCore<int>();
        // Add many rules to increase chance of cancellation mid-execution
        for (int i = 0; i < 100; i++)
        {
            engine.AddRule($"rule-{i}", $"Rule {i}", x => x > 0, x => { });
        }

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        bool threw = false;
        try
        {
            await engine.ExecuteAsync(10, cts.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Assert.True(threw, "Should throw OperationCanceledException");
    }

    [Test]
    public async Task ExecuteAsync_RuleThrowsException_ErrorCapturedInRuleResult()
    {
        using var engine = new RulesEngineCore<int>();

        // Use RuleBuilder to create a rule that throws during action
        // Note: Rule<T>.Execute() catches exceptions internally and returns RuleResult.Error
        var throwingRule = new RuleBuilder<int>()
            .WithId("throwing")
            .WithName("Throwing Rule")
            .When(x => true)
            .Then(x => throw new InvalidOperationException("Test exception"))
            .Build();
        engine.RegisterRule(throwingRule);

        var results = await engine.ExecuteAsync(10);

        Assert.True(results.Any(), "Should have at least one result");
        var result = results.First();
        // Sync rules catch exceptions internally, so Success is true (no exception propagated)
        // but the error is recorded in the RuleResult
        Assert.True(result.Success, "Result should be marked as success (exception handled internally)");
        Assert.NotNull(result.Result.ErrorMessage);
        Assert.Contains("Test exception", result.Result.ErrorMessage!);
    }

    [Test]
    public void AsyncRule_Registration_Works()
    {
        using var engine = new RulesEngineCore<int>();

        var asyncRule = new AsyncRuleBuilder<int>()
            .WithId("async-1")
            .WithName("Async Rule")
            .WithCondition(async x => { await Task.Delay(1); return x > 5; })
            .WithAction(async x => { await Task.Delay(1); return RuleResult.Success("async-1", "Async Rule"); })
            .Build();

        engine.RegisterAsyncRule(asyncRule);

        var asyncRules = engine.GetAsyncRules();
        Assert.Equal(1, asyncRules.Count);
    }

    [Test]
    public async Task AsyncRule_ExecuteAsync_ProcessesBothRuleTypes()
    {
        using var engine = new RulesEngineCore<int>();

        // Add sync rule
        engine.AddRule("sync-1", "Sync Rule", x => x > 0, x => { });

        // Add async rule
        var asyncRule = new AsyncRuleBuilder<int>()
            .WithId("async-1")
            .WithName("Async Rule")
            .WithCondition(async x => { await Task.Yield(); return x > 0; })
            .WithAction(async x => { await Task.Yield(); return RuleResult.Success("async-1", "Async Rule"); })
            .Build();
        engine.RegisterAsyncRule(asyncRule);

        var results = await engine.ExecuteAsync(10);

        // Should have results from both sync and async rules
        Assert.Equal(2, results.Count());
    }

    [Test]
    public void AsyncRuleBuilder_Validation_ThrowsOnMissingFields()
    {
        bool threw = false;
        try
        {
            new AsyncRuleBuilder<int>()
                .WithId("test")
                // Missing name, condition, action
                .Build();
        }
        catch (RuleValidationException)
        {
            threw = true;
        }

        Assert.True(threw, "Should throw RuleValidationException for missing fields");
    }

    [Test]
    public async Task ExecuteAsync_WithCancellation_DuringAsyncRule_Throws()
    {
        using var engine = new RulesEngineCore<int>();
        var cts = new CancellationTokenSource();

        // Create async rule that will check cancellation
        var asyncRule = new AsyncRuleBuilder<int>()
            .WithId("slow-async")
            .WithName("Slow Async Rule")
            .WithCondition(async x => { await Task.Yield(); return true; })
            .WithAction(async x => {
                await Task.Delay(100);
                return RuleResult.Success("slow-async", "Slow Async Rule");
            })
            .Build();
        engine.RegisterAsyncRule(asyncRule);

        // Cancel before execution
        cts.Cancel();

        var threw = false;
        try
        {
            await engine.ExecuteAsync(10, cts.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Assert.True(threw, "Should throw OperationCanceledException");
    }

    [Test]
    public async Task ExecuteAsync_StopOnFirstMatch_StopsAfterFirstRule()
    {
        using var engine = new RulesEngineCore<int>(new RulesEngineOptions
        {
            StopOnFirstMatch = true
        });

        engine.AddRule("rule-1", "Rule 1", x => x > 0, x => { }, priority: 100);
        engine.AddRule("rule-2", "Rule 2", x => x > 0, x => { }, priority: 90);

        var results = await engine.ExecuteAsync(10);

        // Should only have one result due to StopOnFirstMatch
        Assert.Equal(1, results.Count());
        Assert.Equal("rule-1", results.First().RuleId);
    }

    [Test]
    public async Task AsyncRule_WithPriority_ExecutesInOrder()
    {
        using var engine = new RulesEngineCore<int>();
        var executionOrder = new List<string>();

        var highPriorityRule = new AsyncRuleBuilder<int>()
            .WithId("high")
            .WithName("High Priority")
            .WithPriority(100)
            .WithCondition(async x => { await Task.Yield(); return true; })
            .WithAction(async x => {
                executionOrder.Add("high");
                await Task.Yield();
                return RuleResult.Success("high", "High Priority");
            })
            .Build();

        var lowPriorityRule = new AsyncRuleBuilder<int>()
            .WithId("low")
            .WithName("Low Priority")
            .WithPriority(10)
            .WithCondition(async x => { await Task.Yield(); return true; })
            .WithAction(async x => {
                executionOrder.Add("low");
                await Task.Yield();
                return RuleResult.Success("low", "Low Priority");
            })
            .Build();

        // Register in reverse order to verify sorting
        engine.RegisterAsyncRule(lowPriorityRule);
        engine.RegisterAsyncRule(highPriorityRule);

        await engine.ExecuteAsync(10);

        Assert.Equal(2, executionOrder.Count);
        Assert.Equal("high", executionOrder[0]);
        Assert.Equal("low", executionOrder[1]);
    }

    [Test]
    public async Task AsyncRule_ExceptionInCondition_CapturedInResult()
    {
        using var engine = new RulesEngineCore<int>();

        var failingRule = new AsyncRuleBuilder<int>()
            .WithId("failing")
            .WithName("Failing Rule")
            .WithCondition(async x => {
                await Task.Yield();
                throw new InvalidOperationException("Condition failed");
            })
            .WithAction(async x => {
                await Task.Yield();
                return RuleResult.Success("failing", "Failing Rule");
            })
            .Build();

        engine.RegisterAsyncRule(failingRule);

        var results = await engine.ExecuteAsync(10);

        Assert.Equal(1, results.Count());
        var result = results.First();
        Assert.False(result.Success);
        Assert.NotNull(result.Exception);
        Assert.Contains("Condition failed", result.Exception!.Message);
    }

    [Test]
    public void RegisterAsyncRules_Multiple_Works()
    {
        using var engine = new RulesEngineCore<int>();

        var rule1 = new AsyncRuleBuilder<int>()
            .WithId("async-1")
            .WithName("Async Rule 1")
            .WithCondition(async x => { await Task.Yield(); return true; })
            .WithAction(async x => { await Task.Yield(); return RuleResult.Success("async-1", "Async Rule 1"); })
            .Build();

        var rule2 = new AsyncRuleBuilder<int>()
            .WithId("async-2")
            .WithName("Async Rule 2")
            .WithCondition(async x => { await Task.Yield(); return true; })
            .WithAction(async x => { await Task.Yield(); return RuleResult.Success("async-2", "Async Rule 2"); })
            .Build();

        engine.RegisterAsyncRules(rule1, rule2);

        var asyncRules = engine.GetAsyncRules();
        Assert.Equal(2, asyncRules.Count);
    }

    [Test]
    public async Task ExecuteAsync_NoRules_ReturnsEmptyResults()
    {
        using var engine = new RulesEngineCore<int>();

        var results = await engine.ExecuteAsync(10);

        Assert.False(results.Any(), "Should return empty results when no rules registered");
    }

    [Test]
    public async Task ExecuteAsync_MaxRulesToExecute_LimitsExecution()
    {
        using var engine = new RulesEngineCore<int>(new RulesEngineOptions
        {
            MaxRulesToExecute = 2
        });

        for (int i = 0; i < 5; i++)
        {
            engine.AddRule($"rule-{i}", $"Rule {i}", x => true, x => { });
        }

        var results = await engine.ExecuteAsync(10);

        Assert.Equal(2, results.Count());
    }

    [Test]
    public async Task RuleExecutionResult_IsAsyncRule_CorrectlyIdentifiesRuleType()
    {
        using var engine = new RulesEngineCore<int>();

        // Add sync rule
        engine.AddRule("sync-1", "Sync Rule", x => true, x => { });

        // Add async rule
        var asyncRule = new AsyncRuleBuilder<int>()
            .WithId("async-1")
            .WithName("Async Rule")
            .WithCondition(async x => { await Task.Yield(); return true; })
            .WithAction(async x => { await Task.Yield(); return RuleResult.Success("async-1", "Async Rule"); })
            .Build();
        engine.RegisterAsyncRule(asyncRule);

        var results = (await engine.ExecuteAsync(10)).ToList();

        var syncResult = results.First(r => r.RuleId == "sync-1");
        var asyncResult = results.First(r => r.RuleId == "async-1");

        Assert.False(syncResult.IsAsyncRule, "Sync rule should not be marked as async");
        Assert.True(asyncResult.IsAsyncRule, "Async rule should be marked as async");
    }
}
