using System;
using System.Linq;
using RulesEngine.Core;
using TestRunner.Framework;

namespace TestRunner.Tests;

[TestClass]
public class ValidationAndCacheTests
{
    // Validation Tests

    [Test]
    public void RegisterRule_NullRule_ThrowsArgumentNullException()
    {
        using var engine = new RulesEngineCore<int>();

        bool threw = false;
        try
        {
            engine.RegisterRule(null!);
        }
        catch (ArgumentNullException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Test]
    public void RegisterRule_EmptyId_ThrowsRuleValidationException()
    {
        using var engine = new RulesEngineCore<int>();
        var rule = new Rule<int>("", "Name", x => true);

        bool threw = false;
        string? message = null;
        try
        {
            engine.RegisterRule(rule);
        }
        catch (RuleValidationException ex)
        {
            threw = true;
            message = ex.Message;
        }

        Assert.True(threw);
        Assert.True(message?.Contains("ID") == true);
    }

    [Test]
    public void RegisterRule_EmptyName_ThrowsRuleValidationException()
    {
        using var engine = new RulesEngineCore<int>();
        var rule = new Rule<int>("valid-id", "", x => true);

        bool threw = false;
        try
        {
            engine.RegisterRule(rule);
        }
        catch (RuleValidationException ex)
        {
            threw = true;
            Assert.Equal("valid-id", ex.RuleId);
        }

        Assert.True(threw);
    }

    [Test]
    public void RegisterRule_DuplicateId_ThrowsWhenNotAllowed()
    {
        using var engine = new RulesEngineCore<int>(new RulesEngineOptions { AllowDuplicateRuleIds = false });
        engine.AddRule("dup-id", "First Rule", x => true, x => { });

        bool threw = false;
        try
        {
            engine.AddRule("dup-id", "Second Rule", x => true, x => { });
        }
        catch (RuleValidationException ex)
        {
            threw = true;
            Assert.True(ex.Message.Contains("already exists"));
        }

        Assert.True(threw);
    }

    [Test]
    public void RegisterRule_DuplicateId_AllowedWhenConfigured()
    {
        using var engine = new RulesEngineCore<int>(new RulesEngineOptions { AllowDuplicateRuleIds = true });
        engine.AddRule("dup-id", "First Rule", x => true, x => { });
        engine.AddRule("dup-id", "Second Rule", x => true, x => { });

        Assert.Equal(2, engine.GetRules().Count());
    }

    [Test]
    public void AddRule_NullCondition_ThrowsArgumentNullException()
    {
        using var engine = new RulesEngineCore<int>();

        bool threw = false;
        try
        {
            engine.AddRule("id", "name", null!, x => { });
        }
        catch (ArgumentNullException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    // Cache Tests

    [Test]
    public void Execute_MultipleCalls_UsesCachedSortedRules()
    {
        using var engine = new RulesEngineCore<int>();
        engine.AddRule("rule-1", "Rule 1", x => x > 0, x => { }, priority: 10);
        engine.AddRule("rule-2", "Rule 2", x => x > 0, x => { }, priority: 20);

        // First execution - builds cache
        var result1 = engine.Execute(5);

        // Second execution - should use cache (same result)
        var result2 = engine.Execute(5);

        // Both should succeed and have same rule order
        Assert.Equal(result1.TotalRulesEvaluated, result2.TotalRulesEvaluated);
    }

    [Test]
    public void RegisterRule_InvalidatesCache()
    {
        using var engine = new RulesEngineCore<int>();
        engine.AddRule("rule-1", "Rule 1", x => true, x => { });

        // Execute to populate cache
        engine.Execute(5);

        // Add new rule - should invalidate cache
        engine.AddRule("rule-2", "Rule 2", x => true, x => { });

        // Execute again - should see both rules
        var result = engine.Execute(5);
        Assert.Equal(2, result.TotalRulesEvaluated);
    }

    [Test]
    public void RemoveRule_InvalidatesCache()
    {
        using var engine = new RulesEngineCore<int>();
        engine.AddRule("rule-1", "Rule 1", x => true, x => { });
        engine.AddRule("rule-2", "Rule 2", x => true, x => { });

        // Execute to populate cache
        engine.Execute(5);

        // Remove a rule - should invalidate cache
        engine.RemoveRule("rule-1");

        // Execute again - should only see one rule
        var result = engine.Execute(5);
        Assert.Equal(1, result.TotalRulesEvaluated);
    }

    [Test]
    public void ClearRules_InvalidatesCache()
    {
        using var engine = new RulesEngineCore<int>();
        engine.AddRule("rule-1", "Rule 1", x => true, x => { });

        // Execute to populate cache
        engine.Execute(5);

        // Clear all rules
        engine.ClearRules();

        // Execute again - should have no rules
        var result = engine.Execute(5);
        Assert.Equal(0, result.TotalRulesEvaluated);
    }
}
