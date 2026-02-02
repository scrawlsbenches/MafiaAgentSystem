using TestRunner.Framework;
using RulesEngine.Core;

namespace TestRunner.Tests;

/// <summary>
/// Comprehensive tests for CompositeRule and CompositeRuleBuilder classes.
/// Tests cover AND/OR/NOT logic, nested composites, edge cases, and the fluent builder API.
/// </summary>
public class CompositeRuleCoverageTests
{
    #region Test Helper Classes

    private class Order
    {
        public decimal Amount { get; set; }
        public string Category { get; set; } = "";
        public bool IsPremium { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = "";
    }

    private class Customer
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public decimal Balance { get; set; }
        public bool IsVerified { get; set; }
        public string Tier { get; set; } = "Standard";
    }

    #endregion

    #region CompositeRule Constructor Tests

    [Test]
    public void CompositeRule_Constructor_SetsAllProperties()
    {
        var rule1 = new Rule<Order>("R1", "Rule 1", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Rule 2", o => o.Quantity > 5);

        var composite = new CompositeRule<Order>(
            "COMP_1",
            "Test Composite",
            CompositeOperator.And,
            new[] { rule1, rule2 },
            "A composite rule for testing",
            50
        );

        Assert.Equal("COMP_1", composite.Id);
        Assert.Equal("Test Composite", composite.Name);
        Assert.Equal("A composite rule for testing", composite.Description);
        Assert.Equal(50, composite.Priority);
        Assert.Equal(2, composite.Rules.Count);
    }

    [Test]
    public void CompositeRule_Constructor_WithDefaultOptionalParameters()
    {
        var rule = new Rule<Order>("R1", "Rule 1", o => o.Amount > 100);

        var composite = new CompositeRule<Order>(
            "COMP_1",
            "Test Composite",
            CompositeOperator.Or,
            new[] { rule }
        );

        Assert.Equal("", composite.Description);
        Assert.Equal(0, composite.Priority);
    }

    [Test]
    public void CompositeRule_Rules_IsReadOnly()
    {
        var rule = new Rule<Order>("R1", "Rule 1", o => o.Amount > 100);

        var composite = new CompositeRule<Order>(
            "COMP_1",
            "Test",
            CompositeOperator.And,
            new[] { rule }
        );

        // Rules property returns IReadOnlyList
        var rules = composite.Rules;
        Assert.Equal(1, rules.Count);
        Assert.Equal("R1", rules[0].Id);
    }

    #endregion

    #region CompositeRule AND Operator Tests

    [Test]
    public void CompositeRule_And_AllRulesMatch_ReturnsTrue()
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);
        var rule3 = new Rule<Order>("R3", "Large Quantity", o => o.Quantity >= 10);

        var composite = new CompositeRule<Order>(
            "ALL_CONDITIONS",
            "All Conditions",
            CompositeOperator.And,
            new[] { rule1, rule2, rule3 }
        );

        var order = new Order { Amount = 200, IsPremium = true, Quantity = 15 };

        Assert.True(composite.Evaluate(order));
    }

    [Test]
    public void CompositeRule_And_OneRuleFails_ReturnsFalse()
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);
        var rule3 = new Rule<Order>("R3", "Large Quantity", o => o.Quantity >= 10);

        var composite = new CompositeRule<Order>(
            "ALL_CONDITIONS",
            "All Conditions",
            CompositeOperator.And,
            new[] { rule1, rule2, rule3 }
        );

        // IsPremium is false
        var order = new Order { Amount = 200, IsPremium = false, Quantity = 15 };

        Assert.False(composite.Evaluate(order));
    }

    [Test]
    public void CompositeRule_And_FirstRuleFails_ReturnsFalse()
    {
        // Test that AND composite returns false when first rule fails
        var rule1 = new Rule<Order>("R1", "Always False", o => false);
        var rule2 = new Rule<Order>("R2", "Always True", o => true);

        var composite = new CompositeRule<Order>(
            "SHORT_CIRCUIT",
            "Short Circuit Test",
            CompositeOperator.And,
            new[] { rule1, rule2 }
        );

        var order = new Order();
        var result = composite.Evaluate(order);

        Assert.False(result);
    }

    [Theory]
    [InlineData(150, true, 12, true)]   // All conditions met
    [InlineData(50, true, 12, false)]   // Amount too low
    [InlineData(150, false, 12, false)] // Not premium
    [InlineData(150, true, 5, false)]   // Quantity too low
    [InlineData(50, false, 5, false)]   // All conditions fail
    public void CompositeRule_And_VariousInputs_EvaluatesCorrectly(
        decimal amount, bool isPremium, int quantity, bool expected)
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);
        var rule3 = new Rule<Order>("R3", "Large Quantity", o => o.Quantity >= 10);

        var composite = new CompositeRule<Order>(
            "MULTI_CONDITION",
            "Multi Condition",
            CompositeOperator.And,
            new[] { rule1, rule2, rule3 }
        );

        var order = new Order { Amount = amount, IsPremium = isPremium, Quantity = quantity };

        Assert.Equal(expected, composite.Evaluate(order));
    }

    #endregion

    #region CompositeRule OR Operator Tests

    [Test]
    public void CompositeRule_Or_AllRulesFail_ReturnsFalse()
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 1000);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);

        var composite = new CompositeRule<Order>(
            "ANY_CONDITION",
            "Any Condition",
            CompositeOperator.Or,
            new[] { rule1, rule2 }
        );

        var order = new Order { Amount = 50, IsPremium = false };

        Assert.False(composite.Evaluate(order));
    }

    [Test]
    public void CompositeRule_Or_OneRuleMatches_ReturnsTrue()
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 1000);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);
        var rule3 = new Rule<Order>("R3", "Electronics", o => o.Category == "Electronics");

        var composite = new CompositeRule<Order>(
            "ANY_CONDITION",
            "Any Condition",
            CompositeOperator.Or,
            new[] { rule1, rule2, rule3 }
        );

        // Only category matches
        var order = new Order { Amount = 50, IsPremium = false, Category = "Electronics" };

        Assert.True(composite.Evaluate(order));
    }

    [Test]
    public void CompositeRule_Or_AllRulesMatch_ReturnsTrue()
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);

        var composite = new CompositeRule<Order>(
            "ANY_CONDITION",
            "Any Condition",
            CompositeOperator.Or,
            new[] { rule1, rule2 }
        );

        var order = new Order { Amount = 500, IsPremium = true };

        Assert.True(composite.Evaluate(order));
    }

    [Theory]
    [InlineData(1500, false, true)]  // High amount matches
    [InlineData(50, true, true)]     // Premium matches
    [InlineData(1500, true, true)]   // Both match
    [InlineData(50, false, false)]   // Neither matches
    public void CompositeRule_Or_VariousInputs_EvaluatesCorrectly(
        decimal amount, bool isPremium, bool expected)
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 1000);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);

        var composite = new CompositeRule<Order>(
            "OR_TEST",
            "Or Test",
            CompositeOperator.Or,
            new[] { rule1, rule2 }
        );

        var order = new Order { Amount = amount, IsPremium = isPremium };

        Assert.Equal(expected, composite.Evaluate(order));
    }

    #endregion

    #region CompositeRule NOT Operator Tests

    [Test]
    public void CompositeRule_Not_WhenRuleMatches_ReturnsFalse()
    {
        var rule = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);

        var composite = new CompositeRule<Order>(
            "NOT_HIGH",
            "Not High Amount",
            CompositeOperator.Not,
            new[] { rule }
        );

        var order = new Order { Amount = 500 };

        Assert.False(composite.Evaluate(order));
    }

    [Test]
    public void CompositeRule_Not_WhenRuleDoesNotMatch_ReturnsTrue()
    {
        var rule = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);

        var composite = new CompositeRule<Order>(
            "NOT_HIGH",
            "Not High Amount",
            CompositeOperator.Not,
            new[] { rule }
        );

        var order = new Order { Amount = 50 };

        Assert.True(composite.Evaluate(order));
    }

    [Test]
    public void CompositeRule_Not_WithMultipleRules_UsesOnlyFirstRule()
    {
        var rule1 = new Rule<Order>("R1", "Amount Check", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Premium Check", o => o.IsPremium);

        var composite = new CompositeRule<Order>(
            "NOT_TEST",
            "Not Test",
            CompositeOperator.Not,
            new[] { rule1, rule2 }
        );

        // First rule matches (Amount > 100), so NOT returns false
        // Second rule (IsPremium) is ignored
        var order = new Order { Amount = 200, IsPremium = true };

        Assert.False(composite.Evaluate(order));

        // First rule doesn't match, so NOT returns true
        var order2 = new Order { Amount = 50, IsPremium = true };

        Assert.True(composite.Evaluate(order2));
    }

    [Theory]
    [InlineData(50, true)]   // Amount <= 100, so NOT(false) = true
    [InlineData(100, true)]  // Amount == 100, so NOT(false) = true
    [InlineData(101, false)] // Amount > 100, so NOT(true) = false
    [InlineData(500, false)] // Amount > 100, so NOT(true) = false
    public void CompositeRule_Not_VariousInputs_EvaluatesCorrectly(decimal amount, bool expected)
    {
        var rule = new Rule<Order>("R1", "Amount > 100", o => o.Amount > 100);

        var composite = new CompositeRule<Order>(
            "NOT_AMOUNT",
            "Not Amount",
            CompositeOperator.Not,
            new[] { rule }
        );

        var order = new Order { Amount = amount };

        Assert.Equal(expected, composite.Evaluate(order));
    }

    #endregion

    #region CompositeRule Edge Cases

    [Test]
    public void CompositeRule_EmptyRules_And_ReturnsFalse()
    {
        var composite = new CompositeRule<Order>(
            "EMPTY",
            "Empty Composite",
            CompositeOperator.And,
            Array.Empty<IRule<Order>>()
        );

        var order = new Order { Amount = 100 };

        Assert.False(composite.Evaluate(order));
    }

    [Test]
    public void CompositeRule_EmptyRules_Or_ReturnsFalse()
    {
        var composite = new CompositeRule<Order>(
            "EMPTY",
            "Empty Composite",
            CompositeOperator.Or,
            Array.Empty<IRule<Order>>()
        );

        var order = new Order { Amount = 100 };

        Assert.False(composite.Evaluate(order));
    }

    [Test]
    public void CompositeRule_EmptyRules_Not_ReturnsFalse()
    {
        var composite = new CompositeRule<Order>(
            "EMPTY",
            "Empty Composite",
            CompositeOperator.Not,
            Array.Empty<IRule<Order>>()
        );

        var order = new Order { Amount = 100 };

        Assert.False(composite.Evaluate(order));
    }

    [Test]
    public void CompositeRule_SingleRule_And_BehavesLikeSingleRule()
    {
        var rule = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);

        var composite = new CompositeRule<Order>(
            "SINGLE",
            "Single Rule Composite",
            CompositeOperator.And,
            new[] { rule }
        );

        Assert.True(composite.Evaluate(new Order { Amount = 200 }));
        Assert.False(composite.Evaluate(new Order { Amount = 50 }));
    }

    [Test]
    public void CompositeRule_SingleRule_Or_BehavesLikeSingleRule()
    {
        var rule = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);

        var composite = new CompositeRule<Order>(
            "SINGLE",
            "Single Rule Composite",
            CompositeOperator.Or,
            new[] { rule }
        );

        Assert.True(composite.Evaluate(new Order { Amount = 200 }));
        Assert.False(composite.Evaluate(new Order { Amount = 50 }));
    }

    [Test]
    public void CompositeRule_InvalidOperator_ReturnsFalse()
    {
        var rule = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);

        // Cast an invalid int to CompositeOperator
        var invalidOperator = (CompositeOperator)999;

        var composite = new CompositeRule<Order>(
            "INVALID",
            "Invalid Operator",
            invalidOperator,
            new[] { rule }
        );

        var order = new Order { Amount = 200 };

        // Default case returns false
        Assert.False(composite.Evaluate(order));
    }

    #endregion

    #region CompositeRule Execute Tests

    [Test]
    public void CompositeRule_Execute_WhenMatched_ReturnsSuccess()
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);

        var composite = new CompositeRule<Order>(
            "COMP_1",
            "Test Composite",
            CompositeOperator.And,
            new[] { rule1, rule2 }
        );

        var order = new Order { Amount = 200, IsPremium = true };
        var result = composite.Execute(order);

        Assert.True(result.Matched);
        Assert.True(result.ActionExecuted);
        Assert.Equal("COMP_1", result.RuleId);
        Assert.Equal("Test Composite", result.RuleName);
    }

    [Test]
    public void CompositeRule_Execute_WhenNotMatched_ReturnsNotMatched()
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);

        var composite = new CompositeRule<Order>(
            "COMP_1",
            "Test Composite",
            CompositeOperator.And,
            new[] { rule1, rule2 }
        );

        var order = new Order { Amount = 50, IsPremium = true };
        var result = composite.Execute(order);

        Assert.False(result.Matched);
        Assert.False(result.ActionExecuted);
    }

    [Test]
    public void CompositeRule_Execute_ExecutesMatchingChildRules()
    {
        var actionsExecuted = new List<string>();

        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 100)
            .WithAction(o => actionsExecuted.Add("R1"));
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium)
            .WithAction(o => actionsExecuted.Add("R2"));

        var composite = new CompositeRule<Order>(
            "COMP",
            "Composite",
            CompositeOperator.And,
            new[] { rule1, rule2 }
        );

        var order = new Order { Amount = 200, IsPremium = true };
        composite.Execute(order);

        Assert.Equal(2, actionsExecuted.Count);
        Assert.Contains("R1", actionsExecuted);
        Assert.Contains("R2", actionsExecuted);
    }

    [Test]
    public void CompositeRule_Execute_Or_OnlyExecutesMatchingRules()
    {
        var actionsExecuted = new List<string>();

        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 1000)
            .WithAction(o => actionsExecuted.Add("R1"));
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium)
            .WithAction(o => actionsExecuted.Add("R2"));
        var rule3 = new Rule<Order>("R3", "Electronics", o => o.Category == "Electronics")
            .WithAction(o => actionsExecuted.Add("R3"));

        var composite = new CompositeRule<Order>(
            "COMP",
            "Composite",
            CompositeOperator.Or,
            new[] { rule1, rule2, rule3 }
        );

        // Only premium matches
        var order = new Order { Amount = 200, IsPremium = true, Category = "Books" };
        composite.Execute(order);

        Assert.Equal(1, actionsExecuted.Count);
        Assert.Contains("R2", actionsExecuted);
    }

    [Test]
    public void CompositeRule_Execute_ReturnsChildResultsInOutputs()
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);

        var composite = new CompositeRule<Order>(
            "COMP",
            "Composite",
            CompositeOperator.And,
            new[] { rule1, rule2 }
        );

        var order = new Order { Amount = 200, IsPremium = true };
        var result = composite.Execute(order);

        Assert.True(result.Outputs.ContainsKey("ChildResults"));
        var childResults = result.Outputs["ChildResults"] as List<RuleResult>;
        Assert.NotNull(childResults);
        Assert.Equal(2, childResults!.Count);
    }

    [Test]
    public void CompositeRule_Execute_ChildRuleThrows_ReturnsError()
    {
        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 100)
            .WithAction(o => throw new InvalidOperationException("Test error"));

        var composite = new CompositeRule<Order>(
            "COMP",
            "Composite",
            CompositeOperator.And,
            new[] { rule1 }
        );

        var order = new Order { Amount = 200 };
        var result = composite.Execute(order);

        // The composite catches exceptions from child rules
        Assert.True(result.Matched);
        var childResults = result.Outputs["ChildResults"] as List<RuleResult>;
        Assert.NotNull(childResults);
        Assert.True(childResults!.Any(r => r.ErrorMessage != null));
    }

    #endregion

    #region Nested Composite Rules Tests

    [Test]
    public void CompositeRule_NestedAnd_EvaluatesCorrectly()
    {
        // (Amount > 100 AND Premium) AND (Quantity > 5)
        var amountRule = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);
        var premiumRule = new Rule<Order>("R2", "Premium", o => o.IsPremium);
        var quantityRule = new Rule<Order>("R3", "Large Quantity", o => o.Quantity > 5);

        var innerComposite = new CompositeRule<Order>(
            "INNER",
            "Inner",
            CompositeOperator.And,
            new[] { amountRule, premiumRule }
        );

        var outerComposite = new CompositeRule<Order>(
            "OUTER",
            "Outer",
            CompositeOperator.And,
            new IRule<Order>[] { innerComposite, quantityRule }
        );

        // All conditions met
        Assert.True(outerComposite.Evaluate(new Order { Amount = 200, IsPremium = true, Quantity = 10 }));

        // Inner fails (not premium)
        Assert.False(outerComposite.Evaluate(new Order { Amount = 200, IsPremium = false, Quantity = 10 }));

        // Outer fails (low quantity)
        Assert.False(outerComposite.Evaluate(new Order { Amount = 200, IsPremium = true, Quantity = 3 }));
    }

    [Test]
    public void CompositeRule_NestedOr_EvaluatesCorrectly()
    {
        // (Amount > 1000) OR (Premium AND Electronics)
        var highAmountRule = new Rule<Order>("R1", "High Amount", o => o.Amount > 1000);
        var premiumRule = new Rule<Order>("R2", "Premium", o => o.IsPremium);
        var electronicsRule = new Rule<Order>("R3", "Electronics", o => o.Category == "Electronics");

        var premiumElectronics = new CompositeRule<Order>(
            "PREM_ELEC",
            "Premium Electronics",
            CompositeOperator.And,
            new[] { premiumRule, electronicsRule }
        );

        var composite = new CompositeRule<Order>(
            "MAIN",
            "Main",
            CompositeOperator.Or,
            new IRule<Order>[] { highAmountRule, premiumElectronics }
        );

        // High amount matches
        Assert.True(composite.Evaluate(new Order { Amount = 2000, IsPremium = false, Category = "Books" }));

        // Premium Electronics matches
        Assert.True(composite.Evaluate(new Order { Amount = 50, IsPremium = true, Category = "Electronics" }));

        // Neither matches
        Assert.False(composite.Evaluate(new Order { Amount = 50, IsPremium = true, Category = "Books" }));
    }

    [Test]
    public void CompositeRule_NestedNot_EvaluatesCorrectly()
    {
        // NOT(Amount > 100 AND Premium)
        var amountRule = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);
        var premiumRule = new Rule<Order>("R2", "Premium", o => o.IsPremium);

        var innerComposite = new CompositeRule<Order>(
            "INNER",
            "High Amount Premium",
            CompositeOperator.And,
            new[] { amountRule, premiumRule }
        );

        var notComposite = new CompositeRule<Order>(
            "NOT_INNER",
            "Not High Amount Premium",
            CompositeOperator.Not,
            new IRule<Order>[] { innerComposite }
        );

        // Inner matches (high amount and premium), so NOT returns false
        Assert.False(notComposite.Evaluate(new Order { Amount = 200, IsPremium = true }));

        // Inner doesn't match (low amount), so NOT returns true
        Assert.True(notComposite.Evaluate(new Order { Amount = 50, IsPremium = true }));

        // Inner doesn't match (not premium), so NOT returns true
        Assert.True(notComposite.Evaluate(new Order { Amount = 200, IsPremium = false }));
    }

    [Test]
    public void CompositeRule_DeeplyNested_EvaluatesCorrectly()
    {
        // ((A OR B) AND (C OR D)) OR E
        var ruleA = new Rule<Customer>("A", "Age > 30", c => c.Age > 30);
        var ruleB = new Rule<Customer>("B", "Balance > 10000", c => c.Balance > 10000);
        var ruleC = new Rule<Customer>("C", "Verified", c => c.IsVerified);
        var ruleD = new Rule<Customer>("D", "Premium Tier", c => c.Tier == "Premium");
        var ruleE = new Rule<Customer>("E", "Name is VIP", c => c.Name.StartsWith("VIP"));

        var aOrB = new CompositeRule<Customer>("AB", "A or B", CompositeOperator.Or, new[] { ruleA, ruleB });
        var cOrD = new CompositeRule<Customer>("CD", "C or D", CompositeOperator.Or, new[] { ruleC, ruleD });
        var abAndCd = new CompositeRule<Customer>("ABCD", "(AB) and (CD)", CompositeOperator.And, new IRule<Customer>[] { aOrB, cOrD });
        var final = new CompositeRule<Customer>("FINAL", "Final", CompositeOperator.Or, new IRule<Customer>[] { abAndCd, ruleE });

        // VIP name matches (E is true)
        Assert.True(final.Evaluate(new Customer { Name = "VIP_Customer", Age = 20, Balance = 100, IsVerified = false, Tier = "Standard" }));

        // (A OR B) AND (C OR D) matches
        Assert.True(final.Evaluate(new Customer { Name = "Regular", Age = 35, Balance = 100, IsVerified = true, Tier = "Standard" }));

        // Neither matches
        Assert.False(final.Evaluate(new Customer { Name = "Regular", Age = 25, Balance = 100, IsVerified = false, Tier = "Standard" }));
    }

    #endregion

    #region CompositeRuleBuilder Tests

    [Test]
    public void CompositeRuleBuilder_Build_WithAllProperties_CreatesCorrectRule()
    {
        var rule1 = new Rule<Order>("R1", "Rule 1", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Rule 2", o => o.IsPremium);

        var composite = new CompositeRuleBuilder<Order>()
            .WithId("BUILDER_COMP")
            .WithName("Builder Composite")
            .WithDescription("Built with builder")
            .WithPriority(75)
            .WithOperator(CompositeOperator.Or)
            .AddRule(rule1)
            .AddRule(rule2)
            .Build();

        Assert.Equal("BUILDER_COMP", composite.Id);
        Assert.Equal("Builder Composite", composite.Name);
        Assert.Equal("Built with builder", composite.Description);
        Assert.Equal(75, composite.Priority);
        Assert.Equal(2, composite.Rules.Count);
    }

    [Test]
    public void CompositeRuleBuilder_Build_WithDefaults_UsesDefaultValues()
    {
        var rule = new Rule<Order>("R1", "Rule 1", o => o.Amount > 100);

        var composite = new CompositeRuleBuilder<Order>()
            .AddRule(rule)
            .Build();

        // Default name
        Assert.Equal("Unnamed Composite Rule", composite.Name);
        // Default description
        Assert.Equal("", composite.Description);
        // Default priority
        Assert.Equal(0, composite.Priority);
        // Default operator is And
        Assert.True(composite.Evaluate(new Order { Amount = 200 }));
    }

    [Test]
    public void CompositeRuleBuilder_AddRules_AddMultipleAtOnce()
    {
        var rule1 = new Rule<Order>("R1", "Rule 1", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Rule 2", o => o.IsPremium);
        var rule3 = new Rule<Order>("R3", "Rule 3", o => o.Quantity > 5);

        var composite = new CompositeRuleBuilder<Order>()
            .WithName("Multi Add")
            .AddRules(rule1, rule2, rule3)
            .Build();

        Assert.Equal(3, composite.Rules.Count);
    }

    [Test]
    public void CompositeRuleBuilder_MixedAddMethods_WorksTogether()
    {
        var rule1 = new Rule<Order>("R1", "Rule 1", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Rule 2", o => o.IsPremium);
        var rule3 = new Rule<Order>("R3", "Rule 3", o => o.Quantity > 5);

        var composite = new CompositeRuleBuilder<Order>()
            .WithName("Mixed Add")
            .AddRule(rule1)
            .AddRules(rule2, rule3)
            .Build();

        Assert.Equal(3, composite.Rules.Count);
    }

    [Test]
    public void CompositeRuleBuilder_Build_WithNoRules_ThrowsException()
    {
        var builder = new CompositeRuleBuilder<Order>()
            .WithName("Empty Composite");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void CompositeRuleBuilder_FluentChaining_ReturnsBuilder()
    {
        var rule = new Rule<Order>("R1", "Rule 1", o => o.Amount > 100);

        // Verify that each method returns the builder for chaining
        var builder = new CompositeRuleBuilder<Order>();
        var result1 = builder.WithId("ID");
        var result2 = builder.WithName("Name");
        var result3 = builder.WithDescription("Desc");
        var result4 = builder.WithPriority(10);
        var result5 = builder.WithOperator(CompositeOperator.Or);
        var result6 = builder.AddRule(rule);

        Assert.Same(builder, result1);
        Assert.Same(builder, result2);
        Assert.Same(builder, result3);
        Assert.Same(builder, result4);
        Assert.Same(builder, result5);
        Assert.Same(builder, result6);
    }

    [Test]
    public void CompositeRuleBuilder_WithOperator_SetsOperatorCorrectly()
    {
        var rule1 = new Rule<Order>("R1", "Rule 1", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Rule 2", o => o.Amount < 50);

        // Test OR operator
        var orComposite = new CompositeRuleBuilder<Order>()
            .WithOperator(CompositeOperator.Or)
            .AddRules(rule1, rule2)
            .Build();

        // Amount = 200 matches rule1 (> 100)
        Assert.True(orComposite.Evaluate(new Order { Amount = 200 }));

        // Test AND operator (default)
        var andComposite = new CompositeRuleBuilder<Order>()
            .WithOperator(CompositeOperator.And)
            .AddRules(rule1, rule2)
            .Build();

        // Amount = 200 matches rule1 but not rule2
        Assert.False(andComposite.Evaluate(new Order { Amount = 200 }));

        // Test NOT operator
        var notComposite = new CompositeRuleBuilder<Order>()
            .WithOperator(CompositeOperator.Not)
            .AddRule(rule1)
            .Build();

        // Amount = 200 matches rule1, so NOT returns false
        Assert.False(notComposite.Evaluate(new Order { Amount = 200 }));
        // Amount = 50 does not match rule1, so NOT returns true
        Assert.True(notComposite.Evaluate(new Order { Amount = 50 }));
    }

    [Test]
    public void CompositeRuleBuilder_WithId_GeneratesGuidIfNotSet()
    {
        var rule = new Rule<Order>("R1", "Rule 1", o => o.Amount > 100);

        var composite1 = new CompositeRuleBuilder<Order>()
            .AddRule(rule)
            .Build();

        var composite2 = new CompositeRuleBuilder<Order>()
            .AddRule(rule)
            .Build();

        // Each composite should have a unique ID (GUID)
        Assert.NotEqual(composite1.Id, composite2.Id);
        Assert.True(Guid.TryParse(composite1.Id, out _));
        Assert.True(Guid.TryParse(composite2.Id, out _));
    }

    #endregion

    #region Integration with RulesEngine Tests

    [Test]
    public void CompositeRule_RegisteredWithEngine_ExecutesCorrectly()
    {
        var engine = new RulesEngineCore<Order>();

        var rule1 = new Rule<Order>("R1", "High Amount", o => o.Amount > 100);
        var rule2 = new Rule<Order>("R2", "Premium", o => o.IsPremium);

        var composite = new CompositeRuleBuilder<Order>()
            .WithId("COMP_1")
            .WithName("Composite Rule")
            .WithPriority(100)
            .WithOperator(CompositeOperator.And)
            .AddRules(rule1, rule2)
            .Build();

        engine.RegisterRule(composite);

        var matchingOrder = new Order { Amount = 200, IsPremium = true };
        var result = engine.Execute(matchingOrder);

        Assert.Equal(1, result.MatchedRules);

        var nonMatchingOrder = new Order { Amount = 50, IsPremium = true };
        var result2 = engine.Execute(nonMatchingOrder);

        Assert.Equal(0, result2.MatchedRules);
    }

    [Test]
    public void CompositeRule_MixedWithSimpleRules_ExecutesInPriorityOrder()
    {
        var engine = new RulesEngineCore<Order>();
        var executionOrder = new List<string>();

        var simpleRule = new Rule<Order>("SIMPLE", "Simple Rule", o => true)
            .WithAction(o => executionOrder.Add("SIMPLE"));

        var innerRule1 = new Rule<Order>("R1", "Rule 1", o => true)
            .WithAction(o => executionOrder.Add("R1"));
        var innerRule2 = new Rule<Order>("R2", "Rule 2", o => true)
            .WithAction(o => executionOrder.Add("R2"));

        var composite = new CompositeRuleBuilder<Order>()
            .WithId("COMP")
            .WithName("Composite")
            .WithPriority(50) // Lower priority than simple rule
            .AddRules(innerRule1, innerRule2)
            .Build();

        // Register in wrong order, but priority should determine execution order
        engine.RegisterRule(composite);

        var simpleRuleWithPriority = new Rule<Order>("SIMPLE", "Simple Rule", o => true, priority: 100)
            .WithAction(o => executionOrder.Add("SIMPLE"));
        engine.RegisterRule(simpleRuleWithPriority);

        var order = new Order();
        engine.Execute(order);

        // Simple rule should execute first due to higher priority
        Assert.Equal("SIMPLE", executionOrder[0]);
    }

    #endregion
}
