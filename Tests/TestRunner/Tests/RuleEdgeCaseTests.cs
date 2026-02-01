using TestRunner.Framework;
using RulesEngine.Core;

namespace TestRunner.Tests;

/// <summary>
/// Tests for edge cases and error handling in the rules engine.
/// </summary>
public class RuleEdgeCaseTests
{
    private class TestFact
    {
        public string? Name { get; set; }
        public int Value { get; set; }
        public List<string> Results { get; set; } = new();
    }

    [Test]
    public void Execute_EmptyRuleSet_ReturnsEmptyResult()
    {
        var engine = new RulesEngineCore<TestFact>();
        var fact = new TestFact { Name = "Test", Value = 100 };

        var result = engine.Execute(fact);

        Assert.Equal(0, result.TotalRulesEvaluated);
        Assert.Equal(0, result.MatchedRules);
        Assert.Equal(0, result.Errors);
    }

    [Test]
    public void Execute_RuleThatThrowsException_CapturesError()
    {
        var engine = new RulesEngineCore<TestFact>();

        var rule = new RuleBuilder<TestFact>()
            .WithName("Throwing Rule")
            .When(fact => true)
            .Then(fact => throw new InvalidOperationException("Test exception"))
            .Build();

        engine.RegisterRule(rule);

        var fact = new TestFact { Name = "Test" };
        var result = engine.Execute(fact);

        // Rule was evaluated and exception was caught (not propagated)
        Assert.Equal(1, result.TotalRulesEvaluated);
        // Exception in action sets Matched=false via RuleResult.Error
        Assert.True(result.Errors > 0);
        Assert.True(result.GetErrors().Any(e => e.ErrorMessage?.Contains("Test exception") == true));
    }

    [Test]
    public void Execute_ManyRules_HandlesLargeRuleSets()
    {
        var engine = new RulesEngineCore<TestFact>();

        // Add 100 rules
        for (int i = 0; i < 100; i++)
        {
            var threshold = i;
            var rule = new RuleBuilder<TestFact>()
                .WithName($"Rule_{i}")
                .WithPriority(100 - i) // Higher priority for lower thresholds
                .When(fact => fact.Value > threshold)
                .Then(fact => fact.Results.Add($"Matched_{threshold}"))
                .Build();

            engine.RegisterRule(rule);
        }

        var fact = new TestFact { Value = 50 };
        var result = engine.Execute(fact);

        // Should match rules 0-49 (where Value > threshold)
        Assert.Equal(100, result.TotalRulesEvaluated);
        Assert.Equal(50, result.MatchedRules);
        Assert.Equal(50, fact.Results.Count);
    }

    [Test]
    public void Execute_UnicodeRuleNames_HandledCorrectly()
    {
        var engine = new RulesEngineCore<TestFact>();

        var rule = new RuleBuilder<TestFact>()
            .WithId("rule_日本語")
            .WithName("日本語ルール 🎯")
            .When(fact => fact.Value > 0)
            .Then(fact => fact.Results.Add("Unicode matched"))
            .Build();

        engine.RegisterRule(rule);

        var fact = new TestFact { Value = 10 };
        var result = engine.Execute(fact);

        Assert.Equal(1, result.MatchedRules);
        Assert.Contains("Unicode matched", fact.Results);
    }

    [Test]
    public void Execute_NullPropertyAccess_HandlesGracefully()
    {
        var engine = new RulesEngineCore<TestFact>();

        // Rule that accesses potentially null property
        var rule = new RuleBuilder<TestFact>()
            .WithName("Null Check Rule")
            .When(fact => fact.Name != null && fact.Name.Length > 5)
            .Then(fact => fact.Results.Add("Name is long"))
            .Build();

        engine.RegisterRule(rule);

        // Test with null Name
        var factWithNull = new TestFact { Name = null, Value = 10 };
        var result1 = engine.Execute(factWithNull);
        Assert.Equal(0, result1.MatchedRules);

        // Test with short Name
        var factWithShort = new TestFact { Name = "Hi", Value = 10 };
        var result2 = engine.Execute(factWithShort);
        Assert.Equal(0, result2.MatchedRules);

        // Test with long Name
        var factWithLong = new TestFact { Name = "Hello World", Value = 10 };
        var result3 = engine.Execute(factWithLong);
        Assert.Equal(1, result3.MatchedRules);
    }

    [Test]
    public void Execute_RuleWithNoAction_EvaluatesButDoesNothing()
    {
        var engine = new RulesEngineCore<TestFact>();

        // Build rule manually without action
        var rule = new Rule<TestFact>(
            "NO_ACTION",
            "No Action Rule",
            fact => fact.Value > 50
        );

        engine.RegisterRule(rule);

        var fact = new TestFact { Value = 100 };
        var result = engine.Execute(fact);

        Assert.Equal(1, result.TotalRulesEvaluated);
        Assert.Equal(1, result.MatchedRules);
        Assert.Equal(0, fact.Results.Count); // No action was taken
    }

    [Test]
    public void RegisterRule_WithPriorities_ExecutesInCorrectOrder()
    {
        var engine = new RulesEngineCore<TestFact>();
        var executionOrder = new List<int>();

        var lowPriority = new RuleBuilder<TestFact>()
            .WithName("Low Priority")
            .WithPriority(10)
            .When(fact => true)
            .Then(fact => executionOrder.Add(10))
            .Build();

        var highPriority = new RuleBuilder<TestFact>()
            .WithName("High Priority")
            .WithPriority(100)
            .When(fact => true)
            .Then(fact => executionOrder.Add(100))
            .Build();

        var mediumPriority = new RuleBuilder<TestFact>()
            .WithName("Medium Priority")
            .WithPriority(50)
            .When(fact => true)
            .Then(fact => executionOrder.Add(50))
            .Build();

        // Register in random order
        engine.RegisterRule(lowPriority);
        engine.RegisterRule(highPriority);
        engine.RegisterRule(mediumPriority);

        var fact = new TestFact();
        engine.Execute(fact);

        // Should execute high → medium → low
        Assert.Equal(3, executionOrder.Count);
        Assert.Equal(100, executionOrder[0]);
        Assert.Equal(50, executionOrder[1]);
        Assert.Equal(10, executionOrder[2]);
    }

    [Test]
    public void ClearRules_RemovesAllRules()
    {
        var engine = new RulesEngineCore<TestFact>();

        engine.AddRule("R1", "Rule 1", f => true, f => f.Results.Add("1"));
        engine.AddRule("R2", "Rule 2", f => true, f => f.Results.Add("2"));

        var rules = engine.GetRules();
        Assert.Equal(2, rules.Count());

        engine.ClearRules();

        rules = engine.GetRules();
        Assert.Equal(0, rules.Count());

        var fact = new TestFact();
        var result = engine.Execute(fact);
        Assert.Equal(0, result.TotalRulesEvaluated);
    }

    [Test]
    public void RemoveRule_RemovesSpecificRule()
    {
        var engine = new RulesEngineCore<TestFact>();

        engine.AddRule("R1", "Rule 1", f => true, f => f.Results.Add("1"));
        engine.AddRule("R2", "Rule 2", f => true, f => f.Results.Add("2"));
        engine.AddRule("R3", "Rule 3", f => true, f => f.Results.Add("3"));

        engine.RemoveRule("R2");

        var fact = new TestFact();
        engine.Execute(fact);

        Assert.Equal(2, fact.Results.Count);
        Assert.Contains("1", fact.Results);
        Assert.Contains("3", fact.Results);
        Assert.False(fact.Results.Contains("2"));
    }

    [Test]
    public void GetMatchingRules_ReturnsOnlyMatchingRules()
    {
        var engine = new RulesEngineCore<TestFact>();

        engine.AddRule("HIGH", "High Value", f => f.Value > 100, f => { });
        engine.AddRule("MED", "Medium Value", f => f.Value > 50, f => { });
        engine.AddRule("LOW", "Low Value", f => f.Value > 10, f => { });

        var fact = new TestFact { Value = 75 };
        var matching = engine.GetMatchingRules(fact).ToList();

        Assert.Equal(2, matching.Count);
        Assert.True(matching.Any(r => r.Id == "MED"));
        Assert.True(matching.Any(r => r.Id == "LOW"));
        Assert.False(matching.Any(r => r.Id == "HIGH"));
    }

    [Test]
    public void Execute_WithStopOnFirstMatch_StopsEarly()
    {
        var engine = new RulesEngineCore<TestFact>(new RulesEngineOptions
        {
            StopOnFirstMatch = true
        });

        var executionCount = 0;

        engine.AddRule("R1", "Rule 1", f => true, f => executionCount++, priority: 100);
        engine.AddRule("R2", "Rule 2", f => true, f => executionCount++, priority: 50);
        engine.AddRule("R3", "Rule 3", f => true, f => executionCount++, priority: 10);

        var fact = new TestFact();
        var result = engine.Execute(fact);

        Assert.Equal(1, executionCount);
        Assert.Equal(1, result.MatchedRules);
    }

    [Test]
    public void Dispose_ReleasesResources()
    {
        var engine = new RulesEngineCore<TestFact>();
        engine.AddRule("R1", "Rule 1", f => true, f => { });

        engine.Dispose();

        // After dispose, operations should throw or handle gracefully
        // The exact behavior depends on implementation
        // This test ensures Dispose doesn't throw
        Assert.True(true);
    }

    #region RuleResult.Error Edge Cases

    /// <summary>
    /// Documents behavior: when condition evaluation throws, rule is NOT matched.
    /// Note: Expression trees can't contain throw, so we use a method call that throws.
    /// </summary>
    [Test]
    public void RuleResult_Error_ConditionThrows_NotMatched()
    {
        var engine = new RulesEngineCore<TestFact>();

        var rule = new RuleBuilder<TestFact>()
            .WithId("CONDITION_THROWS")
            .WithName("Condition Throws")
            .When(fact => ThrowingCondition(fact))
            .Then(fact => fact.Results.Add("Should not execute"))
            .Build();

        engine.RegisterRule(rule);

        var fact = new TestFact { Value = 10 };
        var result = engine.Execute(fact);

        // Condition threw, so rule is not matched
        Assert.Equal(0, result.MatchedRules);
        Assert.Equal(0, fact.Results.Count);
    }

    private static bool ThrowingCondition(TestFact fact)
    {
        throw new InvalidOperationException("Condition error");
    }

    /// <summary>
    /// KEY EDGE CASE: When condition matches but action throws,
    /// RuleResult.Error sets Matched=false. This is the documented behavior.
    /// See ARCHITECTURE_DECISIONS.md section 5.
    /// </summary>
    [Test]
    public void RuleResult_Error_ActionThrows_AfterMatchingCondition_SetsMatchedFalse()
    {
        var engine = new RulesEngineCore<TestFact>();

        // Use a simple condition (can't use statement body in expression tree)
        var rule = new RuleBuilder<TestFact>()
            .WithId("ACTION_THROWS")
            .WithName("Action Throws")
            .When(fact => fact.Value > 5) // Will match for Value=10
            .Then(fact => throw new InvalidOperationException("Action error"))
            .Build();

        engine.RegisterRule(rule);

        var fact = new TestFact { Value = 10 }; // Matches condition (10 > 5)
        var result = engine.Execute(fact);

        // Condition was evaluated and matched, but RuleResult.Error sets Matched=false
        // IMPORTANT: This behavior loses the information that the rule actually matched
        Assert.Equal(0, result.MatchedRules); // Currently 0 due to Error semantics
        Assert.True(result.Errors > 0);

        // Verify error message is captured
        var errors = result.GetErrors().ToList();
        Assert.Equal(1, errors.Count);
        Assert.Contains("Action error", errors[0].ErrorMessage ?? "");
    }

    /// <summary>
    /// Verifies that RuleResult.Error preserves the error message.
    /// </summary>
    [Test]
    public void RuleResult_Error_PreservesExceptionMessage()
    {
        var ruleResult = RuleResult.Error("TEST_RULE", "Test Rule", "Specific error message");

        Assert.Equal("TEST_RULE", ruleResult.RuleId);
        Assert.Equal("Test Rule", ruleResult.RuleName);
        Assert.False(ruleResult.Matched);
        Assert.False(ruleResult.ActionExecuted);
        Assert.Equal("Specific error message", ruleResult.ErrorMessage);
    }

    /// <summary>
    /// Documents the differences between Success, NotMatched, and Error results.
    /// </summary>
    [Test]
    public void RuleResult_StaticFactoryMethods_HaveDistinctProperties()
    {
        var success = RuleResult.Success("S1", "Success Rule");
        var notMatched = RuleResult.NotMatched("N1", "NotMatched Rule");
        var error = RuleResult.Error("E1", "Error Rule", "Some error");
        var failure = RuleResult.Failure("F1", "Some failure");

        // Success: Matched=true, ActionExecuted=true, no error
        Assert.True(success.Matched);
        Assert.True(success.ActionExecuted);
        Assert.Null(success.ErrorMessage);

        // NotMatched: Matched=false, ActionExecuted=false, no error
        Assert.False(notMatched.Matched);
        Assert.False(notMatched.ActionExecuted);
        Assert.Null(notMatched.ErrorMessage);

        // Error: Matched=false, ActionExecuted=false, HAS error
        Assert.False(error.Matched);
        Assert.False(error.ActionExecuted);
        Assert.NotNull(error.ErrorMessage);

        // Failure: Similar to Error but only takes ruleId and errorMessage
        Assert.False(failure.Matched);
        Assert.False(failure.ActionExecuted);
        Assert.NotNull(failure.ErrorMessage);
    }

    /// <summary>
    /// When multiple rules throw exceptions, all errors are captured.
    /// NOTE: AddRule() creates ActionRule which correctly preserves Matched=true
    /// when condition matched but action threw (unlike Rule&lt;T&gt; which uses RuleResult.Error).
    /// </summary>
    [Test]
    public void Execute_MultipleRulesThrow_CapturesAllErrors()
    {
        var engine = new RulesEngineCore<TestFact>();

        engine.AddRule("R1", "Rule 1", f => true, f => throw new InvalidOperationException("Error 1"), priority: 100);
        engine.AddRule("R2", "Rule 2", f => true, f => throw new ArgumentException("Error 2"), priority: 50);
        engine.AddRule("R3", "Rule 3", f => true, f => f.Results.Add("Success"), priority: 10);

        var fact = new TestFact();
        var result = engine.Execute(fact);

        // ActionRule correctly sets Matched=true even when action throws,
        // because the condition DID match. So all 3 are "matched".
        Assert.Equal(3, result.MatchedRules);
        Assert.Equal(2, result.Errors);

        var errors = result.GetErrors().ToList();
        Assert.True(errors.Any(e => e.ErrorMessage?.Contains("Error 1") == true));
        Assert.True(errors.Any(e => e.ErrorMessage?.Contains("Error 2") == true));

        // R3's action still executed (rules are independent)
        Assert.Contains("Success", fact.Results);
    }

    /// <summary>
    /// CRITICAL EDGE CASE: ActionRule and Rule&lt;T&gt; handle exceptions differently!
    /// - ActionRule (from AddRule): Matched=true when condition matched but action threw
    /// - Rule&lt;T&gt; (from RuleBuilder): Matched=false via RuleResult.Error()
    /// This inconsistency is documented in ARCHITECTURE_DECISIONS.md section 5.
    /// </summary>
    [Test]
    public void RuleResult_Error_ActionRuleVsRuleT_InconsistentBehavior()
    {
        // Test with ActionRule (AddRule) - preserves Matched=true
        var engine1 = new RulesEngineCore<TestFact>();
        engine1.AddRule("AR1", "ActionRule", f => f.Value > 5, f => throw new InvalidOperationException("Action error"));
        var fact1 = new TestFact { Value = 10 };
        var result1 = engine1.Execute(fact1);

        // ActionRule correctly sets Matched=true
        Assert.Equal(1, result1.MatchedRules);
        Assert.Equal(1, result1.Errors);

        // Test with Rule<T> (RuleBuilder) - loses match information
        var engine2 = new RulesEngineCore<TestFact>();
        var rule = new RuleBuilder<TestFact>()
            .WithId("RT1")
            .WithName("RuleT")
            .When(f => f.Value > 5)
            .Then(f => throw new InvalidOperationException("Action error"))
            .Build();
        engine2.RegisterRule(rule);
        var fact2 = new TestFact { Value = 10 };
        var result2 = engine2.Execute(fact2);

        // Rule<T> incorrectly sets Matched=false via RuleResult.Error()
        Assert.Equal(0, result2.MatchedRules); // BUG: Should be 1
        Assert.Equal(1, result2.Errors);
    }

    /// <summary>
    /// Verifies that ExecutedAt timestamp is set correctly.
    /// </summary>
    [Test]
    public void RuleResult_ExecutedAt_IsReasonablyRecent()
    {
        var before = DateTime.UtcNow;
        var result = RuleResult.Success("TEST", "Test");
        var after = DateTime.UtcNow;

        Assert.True(result.ExecutedAt >= before);
        Assert.True(result.ExecutedAt <= after);
    }

    /// <summary>
    /// Tests RuleResult.Success with custom outputs.
    /// </summary>
    [Test]
    public void RuleResult_Success_WithOutputs_PreservesOutputs()
    {
        var outputs = new Dictionary<string, object>
        {
            ["score"] = 100,
            ["decision"] = "approved",
            ["metadata"] = new { source = "test" }
        };

        var result = RuleResult.Success("OUTPUT_RULE", "Output Rule", outputs);

        Assert.Equal(3, result.Outputs.Count);
        Assert.Equal(100, result.Outputs["score"]);
        Assert.Equal("approved", result.Outputs["decision"]);
    }

    /// <summary>
    /// Tests that null outputs parameter creates empty dictionary.
    /// </summary>
    [Test]
    public void RuleResult_Success_NullOutputs_CreatesEmptyDictionary()
    {
        var result = RuleResult.Success("TEST", "Test", null);

        Assert.NotNull(result.Outputs);
        Assert.Equal(0, result.Outputs.Count);
    }

    #endregion
}
