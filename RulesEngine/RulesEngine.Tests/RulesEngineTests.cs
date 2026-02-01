using Xunit;
using RulesEngine.Core;

namespace RulesEngine.Tests;

/// <summary>
/// Tests for the rules engine core functionality
/// </summary>
public class RulesEngineTests
{
    private class Account
    {
        public decimal Balance { get; set; }
        public string AccountType { get; set; } = "";
        public int YearsActive { get; set; }
        public List<string> Tags { get; set; } = new();
    }
    
    [Fact]
    public void RulesEngine_RegisterAndExecute_WorksCorrectly()
    {
        // Arrange
        var engine = new RulesEngineCore<Account>();
        
        var rule = new RuleBuilder<Account>()
            .WithName("High Balance")
            .When(acc => acc.Balance > 10000)
            .Then(acc => acc.Tags.Add("HighValue"))
            .Build();
        
        engine.RegisterRule(rule);
        
        var account = new Account { Balance = 15000 };
        
        // Act
        var result = engine.Execute(account);
        
        // Assert
        Assert.Equal(1, result.TotalRulesEvaluated);
        Assert.Equal(1, result.MatchedRules);
        Assert.Contains("HighValue", account.Tags);
    }
    
    [Fact]
    public void RulesEngine_MultipleRules_AllEvaluated()
    {
        // Arrange
        var engine = new RulesEngineCore<Account>(new RulesEngineOptions
        {
            StopOnFirstMatch = false
        });
        
        var rule1 = new RuleBuilder<Account>()
            .WithName("High Balance")
            .When(acc => acc.Balance > 10000)
            .Then(acc => acc.Tags.Add("HighValue"))
            .Build();
        
        var rule2 = new RuleBuilder<Account>()
            .WithName("Premium Account")
            .When(acc => acc.AccountType == "Premium")
            .Then(acc => acc.Tags.Add("Premium"))
            .Build();
        
        engine.RegisterRules(rule1, rule2);
        
        var account = new Account { Balance = 15000, AccountType = "Premium" };
        
        // Act
        var result = engine.Execute(account);
        
        // Assert
        Assert.Equal(2, result.TotalRulesEvaluated);
        Assert.Equal(2, result.MatchedRules);
        Assert.Contains("HighValue", account.Tags);
        Assert.Contains("Premium", account.Tags);
    }
    
    [Fact]
    public void RulesEngine_StopOnFirstMatch_StopsAfterFirstRule()
    {
        // Arrange
        var engine = new RulesEngineCore<Account>(new RulesEngineOptions
        {
            StopOnFirstMatch = true
        });
        
        var executionCount = 0;
        
        var rule1 = new RuleBuilder<Account>()
            .WithName("Rule 1")
            .WithPriority(100)
            .When(acc => acc.Balance > 5000)
            .Then(acc => executionCount++)
            .Build();
        
        var rule2 = new RuleBuilder<Account>()
            .WithName("Rule 2")
            .WithPriority(90)
            .When(acc => acc.Balance > 5000)
            .Then(acc => executionCount++)
            .Build();
        
        engine.RegisterRules(rule1, rule2);
        
        var account = new Account { Balance = 10000 };
        
        // Act
        var result = engine.Execute(account);
        
        // Assert
        Assert.Equal(1, result.MatchedRules);
        Assert.Equal(1, executionCount); // Only first rule should execute
    }
    
    [Fact]
    public void RulesEngine_Priority_ExecutesInOrder()
    {
        // Arrange
        var engine = new RulesEngineCore<Account>();
        var executionOrder = new List<string>();
        
        var lowPriority = new RuleBuilder<Account>()
            .WithName("Low Priority")
            .WithPriority(10)
            .When(acc => true)
            .Then(acc => executionOrder.Add("Low"))
            .Build();
        
        var highPriority = new RuleBuilder<Account>()
            .WithName("High Priority")
            .WithPriority(100)
            .When(acc => true)
            .Then(acc => executionOrder.Add("High"))
            .Build();
        
        var mediumPriority = new RuleBuilder<Account>()
            .WithName("Medium Priority")
            .WithPriority(50)
            .When(acc => true)
            .Then(acc => executionOrder.Add("Medium"))
            .Build();
        
        // Register in random order
        engine.RegisterRules(lowPriority, highPriority, mediumPriority);
        
        // Act
        engine.Execute(new Account());
        
        // Assert - should execute in priority order (high to low)
        Assert.Equal("High", executionOrder[0]);
        Assert.Equal("Medium", executionOrder[1]);
        Assert.Equal("Low", executionOrder[2]);
    }
    
    [Fact]
    public void RulesEngine_GetMatchingRules_ReturnsOnlyMatches()
    {
        // Arrange
        var engine = new RulesEngineCore<Account>();
        
        var rule1 = new Rule<Account>("R1", "Rule 1", acc => acc.Balance > 10000);
        var rule2 = new Rule<Account>("R2", "Rule 2", acc => acc.Balance > 5000);
        var rule3 = new Rule<Account>("R3", "Rule 3", acc => acc.AccountType == "Premium");
        
        engine.RegisterRules(rule1, rule2, rule3);
        
        var account = new Account { Balance = 7000, AccountType = "Standard" };
        
        // Act
        var matches = engine.GetMatchingRules(account);
        
        // Assert
        Assert.Single(matches);
        Assert.Equal("R2", matches[0].Id);
    }
    
    [Fact]
    public void RulesEngine_RemoveRule_RemovesSuccessfully()
    {
        // Arrange
        var engine = new RulesEngineCore<Account>();
        var rule = new Rule<Account>("R1", "Rule 1", acc => true);
        
        engine.RegisterRule(rule);
        Assert.Single(engine.GetRules());
        
        // Act
        var removed = engine.RemoveRule("R1");
        
        // Assert
        Assert.True(removed);
        Assert.Empty(engine.GetRules());
    }
    
    [Fact]
    public void RulesEngine_ClearRules_RemovesAll()
    {
        // Arrange
        var engine = new RulesEngineCore<Account>();
        
        engine.RegisterRules(
            new Rule<Account>("R1", "Rule 1", acc => true),
            new Rule<Account>("R2", "Rule 2", acc => true),
            new Rule<Account>("R3", "Rule 3", acc => true)
        );
        
        Assert.Equal(3, engine.GetRules().Count);
        
        // Act
        engine.ClearRules();
        
        // Assert
        Assert.Empty(engine.GetRules());
    }
    
    [Fact]
    public void RulesEngine_TrackPerformance_RecordsMetrics()
    {
        // Arrange
        var engine = new RulesEngineCore<Account>(new RulesEngineOptions
        {
            TrackPerformance = true
        });
        
        var rule = new RuleBuilder<Account>()
            .WithId("PERF_TEST")
            .WithName("Performance Test")
            .When(acc => acc.Balance > 1000)
            .Build();
        
        engine.RegisterRule(rule);
        
        // Act - execute multiple times
        for (int i = 0; i < 5; i++)
        {
            engine.Execute(new Account { Balance = 5000 });
        }
        
        // Assert
        var metrics = engine.GetMetrics("PERF_TEST");
        Assert.NotNull(metrics);
        Assert.Equal(5, metrics.ExecutionCount);
        Assert.True(metrics.AverageExecutionTime.TotalMilliseconds >= 0);
    }
    
    [Fact]
    public void RulesEngine_MaxRulesToExecute_LimitsExecution()
    {
        // Arrange
        var engine = new RulesEngineCore<Account>(new RulesEngineOptions
        {
            MaxRulesToExecute = 2
        });
        
        for (int i = 0; i < 5; i++)
        {
            engine.RegisterRule(new Rule<Account>($"R{i}", $"Rule {i}", acc => true));
        }
        
        // Act
        var result = engine.Execute(new Account());
        
        // Assert
        Assert.Equal(2, result.TotalRulesEvaluated);
    }
    
    [Fact]
    public void RulesEngine_ErrorInRule_RecordsError()
    {
        // Arrange
        var engine = new RulesEngineCore<Account>();
        
        var rule = new RuleBuilder<Account>()
            .WithName("Error Rule")
            .When(acc => true)
            .Then(acc => throw new InvalidOperationException("Test error"))
            .Build();
        
        engine.RegisterRule(rule);
        
        // Act
        var result = engine.Execute(new Account());
        
        // Assert
        Assert.Equal(1, result.Errors);
        var errorResults = result.GetErrors();
        Assert.Single(errorResults);
        Assert.Contains("Test error", errorResults[0].ErrorMessage);
    }
}
