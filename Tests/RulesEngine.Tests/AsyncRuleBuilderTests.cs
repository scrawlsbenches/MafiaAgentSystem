using System;
using System.Threading;
using System.Threading.Tasks;
using RulesEngine.Core;
using TestRunner.Framework;

namespace TestRunner.Tests;

/// <summary>
/// Tests for AsyncRuleBuilder<T> fluent API and AsyncRule<T> class
/// </summary>
[TestClass]
public class AsyncRuleBuilderTests
{
    // Test fact class
    private class TestFact
    {
        public int Value { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }

    #region AsyncRuleBuilder Fluent API Tests

    [Test]
    public void WithId_ReturnsBuilder_ForChaining()
    {
        var builder = new AsyncRuleBuilder<TestFact>();

        var result = builder.WithId("test-id");

        Assert.Same(builder, result);
    }

    [Test]
    public void WithName_ReturnsBuilder_ForChaining()
    {
        var builder = new AsyncRuleBuilder<TestFact>();

        var result = builder.WithName("Test Name");

        Assert.Same(builder, result);
    }

    [Test]
    public void WithPriority_ReturnsBuilder_ForChaining()
    {
        var builder = new AsyncRuleBuilder<TestFact>();

        var result = builder.WithPriority(100);

        Assert.Same(builder, result);
    }

    [Test]
    public void WithCondition_WithCancellationToken_ReturnsBuilder_ForChaining()
    {
        var builder = new AsyncRuleBuilder<TestFact>();

        var result = builder.WithCondition(async (fact, ct) => { await Task.Yield(); return true; });

        Assert.Same(builder, result);
    }

    [Test]
    public void WithCondition_Simplified_ReturnsBuilder_ForChaining()
    {
        var builder = new AsyncRuleBuilder<TestFact>();

        var result = builder.WithCondition(async fact => { await Task.Yield(); return true; });

        Assert.Same(builder, result);
    }

    [Test]
    public void WithAction_WithCancellationToken_ReturnsBuilder_ForChaining()
    {
        var builder = new AsyncRuleBuilder<TestFact>();

        var result = builder.WithAction(async (fact, ct) =>
        {
            await Task.Yield();
            return RuleResult.Success("test", "Test");
        });

        Assert.Same(builder, result);
    }

    [Test]
    public void WithAction_Simplified_ReturnsBuilder_ForChaining()
    {
        var builder = new AsyncRuleBuilder<TestFact>();

        var result = builder.WithAction(async fact =>
        {
            await Task.Yield();
            return RuleResult.Success("test", "Test");
        });

        Assert.Same(builder, result);
    }

    [Test]
    public void FullFluentChain_BuildsSuccessfully()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("fluent-test")
            .WithName("Fluent Test Rule")
            .WithPriority(50)
            .WithCondition(async fact => { await Task.Yield(); return fact.Value > 10; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("fluent-test", "Fluent Test Rule"); })
            .Build();

        Assert.NotNull(rule);
        Assert.Equal("fluent-test", rule.Id);
        Assert.Equal("Fluent Test Rule", rule.Name);
        Assert.Equal(50, rule.Priority);
    }

    #endregion

    #region Build Validation Tests

    [Test]
    public void Build_WithoutId_ThrowsInvalidOperationException()
    {
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithName("Test Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("test", "Test"); });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("ID", ex.Message);
    }

    [Test]
    public void Build_WithEmptyId_ThrowsInvalidOperationException()
    {
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithId("")
            .WithName("Test Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("test", "Test"); });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("ID", ex.Message);
    }

    [Test]
    public void Build_WithoutName_ThrowsInvalidOperationException()
    {
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithId("test-id")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("test", "Test"); });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("name", ex.Message);
    }

    [Test]
    public void Build_WithEmptyName_ThrowsInvalidOperationException()
    {
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithId("test-id")
            .WithName("")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("test", "Test"); });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("name", ex.Message);
    }

    [Test]
    public void Build_WithoutCondition_ThrowsInvalidOperationException()
    {
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithId("test-id")
            .WithName("Test Rule")
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("test", "Test"); });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("condition", ex.Message);
    }

    [Test]
    public void Build_WithoutAction_ThrowsInvalidOperationException()
    {
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithId("test-id")
            .WithName("Test Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("action", ex.Message);
    }

    [Test]
    public void Build_WithAllRequiredProperties_Succeeds()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("complete-rule")
            .WithName("Complete Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("complete-rule", "Complete Rule"); })
            .Build();

        Assert.NotNull(rule);
    }

    [Test]
    public void Build_WithDefaultPriority_SetsZeroPriority()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("default-priority")
            .WithName("Default Priority Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("default-priority", "Default Priority Rule"); })
            .Build();

        Assert.Equal(0, rule.Priority);
    }

    #endregion

    #region AsyncRule Execution Tests

    [Test]
    public async Task EvaluateAsync_ConditionTrue_ReturnsTrue()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("eval-true")
            .WithName("Evaluate True Rule")
            .WithCondition(async fact => { await Task.Yield(); return fact.Value > 5; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("eval-true", "Evaluate True Rule"); })
            .Build();

        var result = await rule.EvaluateAsync(new TestFact { Value = 10 });

        Assert.True(result);
    }

    [Test]
    public async Task EvaluateAsync_ConditionFalse_ReturnsFalse()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("eval-false")
            .WithName("Evaluate False Rule")
            .WithCondition(async fact => { await Task.Yield(); return fact.Value > 5; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("eval-false", "Evaluate False Rule"); })
            .Build();

        var result = await rule.EvaluateAsync(new TestFact { Value = 3 });

        Assert.False(result);
    }

    [Test]
    public async Task ExecuteAsync_ReturnsRuleResult()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("execute-test")
            .WithName("Execute Test Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact =>
            {
                await Task.Yield();
                return RuleResult.Success("execute-test", "Execute Test Rule");
            })
            .Build();

        var result = await rule.ExecuteAsync(new TestFact { Value = 10 });

        Assert.NotNull(result);
        Assert.Equal("execute-test", result.RuleId);
        Assert.True(result.Matched);
    }

    [Test]
    public async Task ExecuteAsync_WithCancellationToken_PassesTokenToAction()
    {
        var tokenReceived = false;
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("token-test")
            .WithName("Token Test Rule")
            .WithCondition(async (fact, ct) =>
            {
                await Task.Yield();
                return true;
            })
            .WithAction(async (fact, ct) =>
            {
                tokenReceived = ct.CanBeCanceled;
                await Task.Yield();
                return RuleResult.Success("token-test", "Token Test Rule");
            })
            .Build();

        using var cts = new CancellationTokenSource();
        await rule.ExecuteAsync(new TestFact { Value = 10 }, cts.Token);

        Assert.True(tokenReceived);
    }

    [Test]
    public async Task EvaluateAsync_WithCancellationToken_PassesTokenToCondition()
    {
        var tokenReceived = false;
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("cond-token-test")
            .WithName("Condition Token Test Rule")
            .WithCondition(async (fact, ct) =>
            {
                tokenReceived = ct.CanBeCanceled;
                await Task.Yield();
                return true;
            })
            .WithAction(async (fact, ct) =>
            {
                await Task.Yield();
                return RuleResult.Success("cond-token-test", "Condition Token Test Rule");
            })
            .Build();

        using var cts = new CancellationTokenSource();
        await rule.EvaluateAsync(new TestFact { Value = 10 }, cts.Token);

        Assert.True(tokenReceived);
    }

    [Test]
    public async Task SimplifiedCondition_WorksWithoutCancellationToken()
    {
        var conditionCalled = false;
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("simple-cond")
            .WithName("Simple Condition Rule")
            .WithCondition(async fact =>
            {
                conditionCalled = true;
                await Task.Yield();
                return fact.Value > 5;
            })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("simple-cond", "Simple Condition Rule"); })
            .Build();

        var result = await rule.EvaluateAsync(new TestFact { Value = 10 });

        Assert.True(conditionCalled);
        Assert.True(result);
    }

    [Test]
    public async Task SimplifiedAction_WorksWithoutCancellationToken()
    {
        var actionCalled = false;
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("simple-action")
            .WithName("Simple Action Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact =>
            {
                actionCalled = true;
                await Task.Yield();
                return RuleResult.Success("simple-action", "Simple Action Rule");
            })
            .Build();

        await rule.ExecuteAsync(new TestFact { Value = 10 });

        Assert.True(actionCalled);
    }

    #endregion

    #region AsyncRule Constructor Tests

    [Test]
    public void AsyncRule_Constructor_WithNullId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncRule<TestFact>(
            null!,
            "Test Name",
            (fact, ct) => Task.FromResult(true),
            (fact, ct) => Task.FromResult(RuleResult.Success("test", "Test"))
        ));
    }

    [Test]
    public void AsyncRule_Constructor_WithNullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncRule<TestFact>(
            "test-id",
            null!,
            (fact, ct) => Task.FromResult(true),
            (fact, ct) => Task.FromResult(RuleResult.Success("test", "Test"))
        ));
    }

    [Test]
    public void AsyncRule_Constructor_WithNullCondition_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncRule<TestFact>(
            "test-id",
            "Test Name",
            null!,
            (fact, ct) => Task.FromResult(RuleResult.Success("test", "Test"))
        ));
    }

    [Test]
    public void AsyncRule_Constructor_WithNullAction_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncRule<TestFact>(
            "test-id",
            "Test Name",
            (fact, ct) => Task.FromResult(true),
            null!
        ));
    }

    [Test]
    public void AsyncRule_Constructor_SetsProperties()
    {
        var rule = new AsyncRule<TestFact>(
            "direct-construct",
            "Direct Construction Rule",
            (fact, ct) => Task.FromResult(true),
            (fact, ct) => Task.FromResult(RuleResult.Success("direct-construct", "Direct Construction Rule")),
            priority: 75
        );

        Assert.Equal("direct-construct", rule.Id);
        Assert.Equal("Direct Construction Rule", rule.Name);
        Assert.Equal(75, rule.Priority);
    }

    [Test]
    public void AsyncRule_Constructor_DefaultPriority_IsZero()
    {
        var rule = new AsyncRule<TestFact>(
            "default-pri",
            "Default Priority",
            (fact, ct) => Task.FromResult(true),
            (fact, ct) => Task.FromResult(RuleResult.Success("default-pri", "Default Priority"))
        );

        Assert.Equal(0, rule.Priority);
    }

    #endregion

    #region Priority Tests

    [Test]
    public void Build_WithPositivePriority_SetsPriority()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("pos-pri")
            .WithName("Positive Priority Rule")
            .WithPriority(100)
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("pos-pri", "Positive Priority Rule"); })
            .Build();

        Assert.Equal(100, rule.Priority);
    }

    [Test]
    public void Build_WithNegativePriority_SetsPriority()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("neg-pri")
            .WithName("Negative Priority Rule")
            .WithPriority(-50)
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("neg-pri", "Negative Priority Rule"); })
            .Build();

        Assert.Equal(-50, rule.Priority);
    }

    [Test]
    public void Build_WithMaxPriority_SetsPriority()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("max-pri")
            .WithName("Max Priority Rule")
            .WithPriority(int.MaxValue)
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("max-pri", "Max Priority Rule"); })
            .Build();

        Assert.Equal(int.MaxValue, rule.Priority);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task ExecuteAsync_ActionReturnsError_ReturnsErrorResult()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("error-action")
            .WithName("Error Action Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact =>
            {
                await Task.Yield();
                return RuleResult.Error("error-action", "Error Action Rule", "Something went wrong");
            })
            .Build();

        var result = await rule.ExecuteAsync(new TestFact { Value = 10 });

        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Something went wrong", result.ErrorMessage!);
    }

    [Test]
    public async Task ExecuteAsync_ActionReturnsNotMatched_ReturnsNotMatchedResult()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("not-matched")
            .WithName("Not Matched Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact =>
            {
                await Task.Yield();
                return RuleResult.NotMatched("not-matched", "Not Matched Rule");
            })
            .Build();

        var result = await rule.ExecuteAsync(new TestFact { Value = 10 });

        Assert.False(result.Matched);
    }

    [Test]
    public async Task EvaluateAsync_WithNullFact_PassesNullToCondition()
    {
        TestFact? receivedFact = new TestFact(); // Initialize to non-null to verify it gets set to null
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("null-fact")
            .WithName("Null Fact Rule")
            .WithCondition(async fact =>
            {
                receivedFact = fact;
                await Task.Yield();
                return fact == null;
            })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("null-fact", "Null Fact Rule"); })
            .Build();

        var result = await rule.EvaluateAsync(null!);

        Assert.Null(receivedFact);
        Assert.True(result);
    }

    [Test]
    public async Task Build_OverwriteProperties_UsesLatestValues()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("first-id")
            .WithId("second-id")
            .WithName("First Name")
            .WithName("Second Name")
            .WithPriority(10)
            .WithPriority(20)
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("test", "Test"); })
            .Build();

        Assert.Equal("second-id", rule.Id);
        Assert.Equal("Second Name", rule.Name);
        Assert.Equal(20, rule.Priority);
    }

    [Test]
    public async Task Build_OverwriteCondition_UsesLatestCondition()
    {
        var firstConditionCalled = false;
        var secondConditionCalled = false;

        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("overwrite-cond")
            .WithName("Overwrite Condition Rule")
            .WithCondition(async fact => { firstConditionCalled = true; await Task.Yield(); return true; })
            .WithCondition(async fact => { secondConditionCalled = true; await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("overwrite-cond", "Overwrite Condition Rule"); })
            .Build();

        await rule.EvaluateAsync(new TestFact { Value = 10 });

        Assert.False(firstConditionCalled);
        Assert.True(secondConditionCalled);
    }

    [Test]
    public async Task Build_OverwriteAction_UsesLatestAction()
    {
        var firstActionCalled = false;
        var secondActionCalled = false;

        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("overwrite-action")
            .WithName("Overwrite Action Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { firstActionCalled = true; await Task.Yield(); return RuleResult.Success("test", "Test"); })
            .WithAction(async fact => { secondActionCalled = true; await Task.Yield(); return RuleResult.Success("test", "Test"); })
            .Build();

        await rule.ExecuteAsync(new TestFact { Value = 10 });

        Assert.False(firstActionCalled);
        Assert.True(secondActionCalled);
    }

    #endregion

    #region Interface Compliance Tests

    [Test]
    public void Build_ReturnsIAsyncRule()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("interface-test")
            .WithName("Interface Test Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("interface-test", "Interface Test Rule"); })
            .Build();

        Assert.IsType<AsyncRule<TestFact>>(rule);
    }

    [Test]
    public void AsyncRule_ImplementsIAsyncRule()
    {
        var rule = new AsyncRule<TestFact>(
            "implements-test",
            "Implements Test",
            async (fact, ct) => true,
            async (fact, ct) => RuleResult.Success("implements-test", "Implements Test")
        );

        Assert.True(rule is IAsyncRule<TestFact>);
    }

    #endregion

    #region Complex Scenario Tests

    [Test]
    public async Task AsyncRule_ComplexCondition_EvaluatesCorrectly()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("complex-cond")
            .WithName("Complex Condition Rule")
            .WithCondition(async fact =>
            {
                await Task.Yield();
                return fact.Value > 10 && fact.IsActive && fact.Name.StartsWith("Test");
            })
            .WithAction(async fact => { await Task.Yield(); return RuleResult.Success("complex-cond", "Complex Condition Rule"); })
            .Build();

        var matching = new TestFact { Value = 15, IsActive = true, Name = "Test User" };
        var notMatching1 = new TestFact { Value = 5, IsActive = true, Name = "Test User" };
        var notMatching2 = new TestFact { Value = 15, IsActive = false, Name = "Test User" };
        var notMatching3 = new TestFact { Value = 15, IsActive = true, Name = "User" };

        Assert.True(await rule.EvaluateAsync(matching));
        Assert.False(await rule.EvaluateAsync(notMatching1));
        Assert.False(await rule.EvaluateAsync(notMatching2));
        Assert.False(await rule.EvaluateAsync(notMatching3));
    }

    [Test]
    public async Task AsyncRule_ActionWithOutputs_ReturnsOutputs()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("output-action")
            .WithName("Output Action Rule")
            .WithCondition(async fact => { await Task.Yield(); return true; })
            .WithAction(async fact =>
            {
                await Task.Yield();
                var outputs = new Dictionary<string, object>
                {
                    ["ProcessedValue"] = fact.Value * 2,
                    ["ProcessedAt"] = DateTime.UtcNow
                };
                return RuleResult.Success("output-action", "Output Action Rule", outputs);
            })
            .Build();

        var result = await rule.ExecuteAsync(new TestFact { Value = 5 });

        Assert.True(result.Outputs.ContainsKey("ProcessedValue"));
        Assert.Equal(10, result.Outputs["ProcessedValue"]);
    }

    [Test]
    public async Task AsyncRule_LongRunningOperation_Completes()
    {
        var rule = new AsyncRuleBuilder<TestFact>()
            .WithId("long-running")
            .WithName("Long Running Rule")
            .WithCondition(async fact =>
            {
                await Task.Delay(10); // Simulate some async work
                return true;
            })
            .WithAction(async fact =>
            {
                await Task.Delay(10); // Simulate some async work
                return RuleResult.Success("long-running", "Long Running Rule");
            })
            .Build();

        var result = await rule.ExecuteAsync(new TestFact { Value = 10 });

        Assert.True(result.Matched);
    }

    #endregion
}
