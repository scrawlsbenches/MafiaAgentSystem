using TestRunner.Framework;
using RulesEngine.Core;
using RulesEngine.Examples;

namespace TestRunner.Tests;

/// <summary>
/// Tests for dynamic rule factory
/// </summary>
public class DynamicRuleFactoryTests
{
    private class Product
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public string Category { get; set; } = "";
        public bool InStock { get; set; }
    }

    #region Individual Operator Tests (Redundant - kept for API documentation)
    // NOTE: These individual tests are now redundant with the Theory-based tests below
    // (DynamicRuleFactory_NumericComparisons_Theory, DynamicRuleFactory_StringComparisons_Theory).
    // They are kept as explicit examples of the API usage and for documentation purposes.

    [Test]
    public void DynamicRuleFactory_NumericGreaterThan_WorksCorrectly()
    {
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "TEST",
            "Test Rule",
            "Price",
            ">",
            50m
        );

        var expensive = new Product { Price = 100m };
        var cheap = new Product { Price = 30m };

        Assert.True(rule.Evaluate(expensive));
        Assert.False(rule.Evaluate(cheap));
    }

    [Test]
    public void DynamicRuleFactory_NumericEquals_WorksCorrectly()
    {
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "TEST",
            "Test Rule",
            "Price",
            "==",
            50m
        );

        var exact = new Product { Price = 50m };
        var different = new Product { Price = 30m };

        Assert.True(rule.Evaluate(exact));
        Assert.False(rule.Evaluate(different));
    }

    [Test]
    public void DynamicRuleFactory_NumericNotEquals_WorksCorrectly()
    {
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "TEST",
            "Test Rule",
            "Price",
            "!=",
            50m
        );

        var different = new Product { Price = 30m };
        var same = new Product { Price = 50m };

        Assert.True(rule.Evaluate(different));
        Assert.False(rule.Evaluate(same));
    }

    [Test]
    public void DynamicRuleFactory_NumericLessThanOrEqual_WorksCorrectly()
    {
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "TEST",
            "Test Rule",
            "Price",
            "<=",
            50m
        );

        var under = new Product { Price = 40m };
        var exact = new Product { Price = 50m };
        var over = new Product { Price = 60m };

        Assert.True(rule.Evaluate(under));
        Assert.True(rule.Evaluate(exact));
        Assert.False(rule.Evaluate(over));
    }

    [Test]
    public void DynamicRuleFactory_StringContains_WorksCorrectly()
    {
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "NAME_CHECK",
            "Name Contains",
            "Name",
            "contains",
            "phone"
        );

        var matching = new Product { Name = "Smartphone Pro" };
        var notMatching = new Product { Name = "Laptop" };

        Assert.True(rule.Evaluate(matching));
        Assert.False(rule.Evaluate(notMatching));
    }

    [Test]
    public void DynamicRuleFactory_StringStartsWith_WorksCorrectly()
    {
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "PREFIX_CHECK",
            "Name Prefix",
            "Category",
            "startswith",
            "Elec"
        );

        var matching = new Product { Category = "Electronics" };
        var notMatching = new Product { Category = "Furniture" };

        Assert.True(rule.Evaluate(matching));
        Assert.False(rule.Evaluate(notMatching));
    }

    #endregion

    [Test]
    public void DynamicRuleFactory_MultiCondition_CombinesCorrectly()
    {
        var rule = DynamicRuleFactory.CreateMultiConditionRule<Product>(
            "MULTI",
            "Multi Condition",
            ("Price", ">", 50m),
            ("InStock", "==", true),
            ("Category", "==", "Electronics")
        );

        var matching = new Product
        {
            Price = 100,
            InStock = true,
            Category = "Electronics"
        };

        var notMatching = new Product
        {
            Price = 100,
            InStock = false,  // Fails this condition
            Category = "Electronics"
        };

        Assert.True(rule.Evaluate(matching));
        Assert.False(rule.Evaluate(notMatching));
    }

    [Test]
    public void DynamicRuleFactory_FromDefinitions_CreatesRules()
    {
        var definitions = new List<RuleDefinition>
        {
            new RuleDefinition
            {
                Id = "RULE1",
                Name = "Expensive Items",
                Priority = 10,
                Conditions = new List<ConditionDefinition>
                {
                    new ConditionDefinition
                    {
                        PropertyName = "Price",
                        Operator = ">",
                        Value = 100m,
                        LogicalOperator = "AND"
                    }
                }
            }
        };

        var rules = DynamicRuleFactory.CreateRulesFromDefinitions<Product>(definitions);

        Assert.Single(rules);
        Assert.Equal("RULE1", rules[0].Id);
        Assert.Equal("Expensive Items", rules[0].Name);
        Assert.True(rules[0].Evaluate(new Product { Price = 150 }));
        Assert.False(rules[0].Evaluate(new Product { Price = 50 }));
    }

    [Test]
    public void DynamicRuleFactory_InvalidOperator_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() =>
            DynamicRuleFactory.CreatePropertyRule<Product>(
                "BAD",
                "Bad Rule",
                "Price",
                "invalid_operator",
                100m
            )
        );
    }

    // Theory-based comprehensive test for all numeric operators
    [Theory]
    [InlineData("Price", ">", 50.0, 100.0, true)]
    [InlineData("Price", ">", 50.0, 30.0, false)]
    [InlineData("Price", ">", 50.0, 50.0, false)]
    [InlineData("Price", ">=", 50.0, 50.0, true)]
    [InlineData("Price", ">=", 50.0, 49.0, false)]
    [InlineData("Price", "<", 50.0, 30.0, true)]
    [InlineData("Price", "<", 50.0, 60.0, false)]
    [InlineData("Price", "<", 50.0, 50.0, false)]
    [InlineData("Price", "<=", 50.0, 50.0, true)]
    [InlineData("Price", "<=", 50.0, 51.0, false)]
    [InlineData("Price", "==", 50.0, 50.0, true)]
    [InlineData("Price", "==", 50.0, 30.0, false)]
    [InlineData("Price", "!=", 50.0, 30.0, true)]
    [InlineData("Price", "!=", 50.0, 50.0, false)]
    public void DynamicRuleFactory_NumericComparisons_Theory(
        string property, string op, double compareValue, double actualValue, bool expectedMatch)
    {
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "TEST",
            "Test Rule",
            property,
            op,
            (decimal)compareValue
        );

        var product = new Product { Price = (decimal)actualValue };

        var matches = rule.Evaluate(product);

        Assert.Equal(expectedMatch, matches);
    }

    // Theory-based test for string operators
    [Theory]
    [InlineData("Name", "contains", "phone", "Smartphone Pro", true)]
    [InlineData("Name", "contains", "phone", "Laptop", false)]
    [InlineData("Name", "contains", "Phone", "smartphone", false)]  // Case sensitive
    [InlineData("Category", "startswith", "Elec", "Electronics", true)]
    [InlineData("Category", "startswith", "Elec", "Furniture", false)]
    [InlineData("Category", "endswith", "ics", "Electronics", true)]
    [InlineData("Category", "endswith", "ics", "Furniture", false)]
    [InlineData("Name", "==", "Laptop", "Laptop", true)]
    [InlineData("Name", "==", "Laptop", "Desktop", false)]
    public void DynamicRuleFactory_StringComparisons_Theory(
        string property, string op, string compareValue, string actualValue, bool expectedMatch)
    {
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "TEST",
            "Test Rule",
            property,
            op,
            compareValue
        );

        var product = new Product();
        if (property == "Name")
            product.Name = actualValue;
        else if (property == "Category")
            product.Category = actualValue;

        var matches = rule.Evaluate(product);

        Assert.Equal(expectedMatch, matches);
    }

    // Theory-based test for boolean operators
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void DynamicRuleFactory_BooleanEquals_Theory(bool inStockValue, bool expectedMatch)
    {
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "TEST",
            "Test Rule",
            "InStock",
            "==",
            true
        );

        var product = new Product { InStock = inStockValue };

        Assert.Equal(expectedMatch, rule.Evaluate(product));
    }
}

/// <summary>
/// Tests for e-commerce discount rules
/// </summary>
public class DiscountRulesTests
{
    [Test]
    public void DiscountEngine_VIPCustomer_Receives20PercentDiscount()
    {
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD001",
            TotalAmount = 1000m,
            CustomerType = "VIP",
            ItemCount = 1
        };

        engine.Execute(order);

        // VIP gets 20% ($200) + large order discount ($50 for orders > $500) + free shipping
        Assert.Equal(250m, order.DiscountAmount);
        Assert.Contains("VIP", order.DiscountReason);
        Assert.Contains("large order", order.DiscountReason);
        Assert.True(order.FreeShipping);
    }

    [Test]
    public void DiscountEngine_LargeOrder_ReceivesFixedDiscount()
    {
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD002",
            TotalAmount = 600m,
            CustomerType = "Regular",
            ItemCount = 1
        };

        engine.Execute(order);

        Assert.Equal(50m, order.DiscountAmount);
        Assert.Contains("large order", order.DiscountReason);
    }

    [Test]
    public void DiscountEngine_FirstOrder_Receives15PercentDiscount()
    {
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD003",
            TotalAmount = 100m,
            CustomerType = "Regular",
            IsFirstOrder = true,
            ItemCount = 1
        };

        engine.Execute(order);

        Assert.Equal(15m, order.DiscountAmount); // 15% of 100
        Assert.Contains("First order", order.DiscountReason);
    }

    [Test]
    public void DiscountEngine_ElectronicsBundle_ReceivesDiscount()
    {
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD004",
            TotalAmount = 500m,
            CustomerType = "Regular",
            ProductCategory = "Electronics",
            ItemCount = 3
        };

        engine.Execute(order);

        Assert.Equal(50m, order.DiscountAmount); // 10% of 500
        Assert.Contains("Electronics bundle", order.DiscountReason);
    }

    [Test]
    public void DiscountEngine_PremiumWithLargeOrder_GetsFreeShipping()
    {
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD005",
            TotalAmount = 150m,
            CustomerType = "Premium",
            ItemCount = 1
        };

        engine.Execute(order);

        Assert.True(order.FreeShipping);
        Assert.Contains("Free shipping", order.DiscountReason);
    }

    [Test]
    public void DiscountEngine_MultipleRulesApply_StacksDiscounts()
    {
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD006",
            TotalAmount = 600m,
            CustomerType = "VIP",
            ItemCount = 1
        };

        engine.Execute(order);

        // Should get both VIP (20% = 120) and large order ($50) discounts
        Assert.Equal(170m, order.DiscountAmount);
        Assert.Contains("VIP", order.DiscountReason);
        Assert.Contains("large order", order.DiscountReason);
    }
}

/// <summary>
/// Tests for approval workflow
/// </summary>
public class ApprovalWorkflowTests
{
    [Test]
    public void ApprovalEngine_SmallPurchase_RequiresManagerApproval()
    {
        var engine = ApprovalWorkflowExample.CreateApprovalEngine();
        var request = new PurchaseRequest
        {
            RequestId = "PR001",
            Amount = 5000m,
            Department = "IT"
        };

        engine.Execute(request);

        Assert.Equal("Manager", request.ApprovalLevel);
        Assert.Contains("Manager", request.RequiredApprovers);
    }

    [Test]
    public void ApprovalEngine_MediumPurchase_RequiresDirectorApproval()
    {
        var engine = ApprovalWorkflowExample.CreateApprovalEngine();
        var request = new PurchaseRequest
        {
            RequestId = "PR002",
            Amount = 25000m,
            Department = "Marketing"
        };

        engine.Execute(request);

        Assert.Equal("Director", request.ApprovalLevel);
        Assert.Contains("Director", request.RequiredApprovers);
    }

    [Test]
    public void ApprovalEngine_LargePurchase_RequiresCFOApproval()
    {
        var engine = ApprovalWorkflowExample.CreateApprovalEngine();
        var request = new PurchaseRequest
        {
            RequestId = "PR003",
            Amount = 75000m,
            Department = "Operations"
        };

        engine.Execute(request);

        Assert.Equal("CFO", request.ApprovalLevel);
        Assert.Contains("CFO", request.RequiredApprovers);
    }

    [Test]
    public void ApprovalEngine_VeryLargePurchase_RequiresCEOApproval()
    {
        var engine = ApprovalWorkflowExample.CreateApprovalEngine();
        var request = new PurchaseRequest
        {
            RequestId = "PR004",
            Amount = 150000m,
            Department = "Operations"
        };

        engine.Execute(request);

        Assert.Equal("CEO", request.ApprovalLevel);
        Assert.Contains("CEO", request.RequiredApprovers);
    }

    [Test]
    public void ApprovalEngine_FinanceDepartment_RequiresCFO()
    {
        var engine = ApprovalWorkflowExample.CreateApprovalEngine();
        var request = new PurchaseRequest
        {
            RequestId = "PR005",
            Amount = 5000m, // Small amount
            Department = "Finance" // But finance department
        };

        engine.Execute(request);

        Assert.Equal("CFO", request.ApprovalLevel);
        Assert.Contains("CFO", request.RequiredApprovers);
    }

    [Test]
    public void ApprovalEngine_ITEquipmentReview_RequiresAdditionalApproval()
    {
        var mainEngine = ApprovalWorkflowExample.CreateApprovalEngine();
        var reviewEngine = ApprovalWorkflowExample.CreateAdditionalReviewEngine();

        var request = new PurchaseRequest
        {
            RequestId = "PR006",
            Amount = 30000m,
            Category = "IT Equipment"
        };

        mainEngine.Execute(request);
        reviewEngine.Execute(request);

        Assert.True(request.RequiresAdditionalReview);
        Assert.Contains("IT Director", request.RequiredApprovers);
    }
}
