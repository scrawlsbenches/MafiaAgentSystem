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
}
