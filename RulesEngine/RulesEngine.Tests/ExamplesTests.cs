using Xunit;
using RulesEngine.Core;
using RulesEngine.Examples;

namespace RulesEngine.Tests;

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
    
    [Theory]
    [InlineData("Price", ">", 50.0, 100.0, true)]
    [InlineData("Price", ">", 50.0, 30.0, false)]
    [InlineData("Price", "==", 50.0, 50.0, true)]
    [InlineData("Price", "!=", 50.0, 30.0, true)]
    [InlineData("Price", "<=", 50.0, 40.0, true)]
    [InlineData("Price", "<=", 50.0, 60.0, false)]
    public void DynamicRuleFactory_NumericComparisons_WorkCorrectly(
        string property, string op, double compareValue, double actualValue, bool expectedMatch)
    {
        // Arrange
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "TEST",
            "Test Rule",
            property,
            op,
            (decimal)compareValue
        );
        
        var product = new Product { Price = (decimal)actualValue };
        
        // Act
        var matches = rule.Evaluate(product);
        
        // Assert
        Assert.Equal(expectedMatch, matches);
    }
    
    [Fact]
    public void DynamicRuleFactory_StringContains_WorksCorrectly()
    {
        // Arrange
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "NAME_CHECK",
            "Name Contains",
            "Name",
            "contains",
            "phone"
        );
        
        var matching = new Product { Name = "Smartphone Pro" };
        var notMatching = new Product { Name = "Laptop" };
        
        // Act & Assert
        Assert.True(rule.Evaluate(matching));
        Assert.False(rule.Evaluate(notMatching));
    }
    
    [Fact]
    public void DynamicRuleFactory_StringStartsWith_WorksCorrectly()
    {
        // Arrange
        var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
            "PREFIX_CHECK",
            "Name Prefix",
            "Category",
            "startswith",
            "Elec"
        );
        
        var matching = new Product { Category = "Electronics" };
        var notMatching = new Product { Category = "Furniture" };
        
        // Act & Assert
        Assert.True(rule.Evaluate(matching));
        Assert.False(rule.Evaluate(notMatching));
    }
    
    [Fact]
    public void DynamicRuleFactory_MultiCondition_CombinesCorrectly()
    {
        // Arrange
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
        
        // Act & Assert
        Assert.True(rule.Evaluate(matching));
        Assert.False(rule.Evaluate(notMatching));
    }
    
    [Fact]
    public void DynamicRuleFactory_FromDefinitions_CreatesRules()
    {
        // Arrange
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
        
        // Act
        var rules = DynamicRuleFactory.CreateRulesFromDefinitions<Product>(definitions);
        
        // Assert
        Assert.Single(rules);
        Assert.Equal("RULE1", rules[0].Id);
        Assert.Equal("Expensive Items", rules[0].Name);
        Assert.True(rules[0].Evaluate(new Product { Price = 150 }));
        Assert.False(rules[0].Evaluate(new Product { Price = 50 }));
    }
    
    [Fact]
    public void DynamicRuleFactory_InvalidOperator_ThrowsException()
    {
        // Act & Assert
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
}

/// <summary>
/// Tests for e-commerce discount rules
/// </summary>
public class DiscountRulesTests
{
    [Fact]
    public void DiscountEngine_VIPCustomer_Receives20PercentDiscount()
    {
        // Arrange
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD001",
            TotalAmount = 1000m,
            CustomerType = "VIP",
            ItemCount = 1
        };
        
        // Act
        var result = engine.Execute(order);
        
        // Assert
        Assert.True(result.MatchedRules > 0);
        Assert.Equal(200m, order.DiscountAmount); // 20% of 1000
        Assert.Contains("VIP", order.DiscountReason);
    }
    
    [Fact]
    public void DiscountEngine_LargeOrder_ReceivesFixedDiscount()
    {
        // Arrange
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD002",
            TotalAmount = 600m,
            CustomerType = "Regular",
            ItemCount = 1
        };
        
        // Act
        var result = engine.Execute(order);
        
        // Assert
        Assert.Equal(50m, order.DiscountAmount);
        Assert.Contains("large order", order.DiscountReason);
    }
    
    [Fact]
    public void DiscountEngine_FirstOrder_Receives15PercentDiscount()
    {
        // Arrange
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD003",
            TotalAmount = 100m,
            CustomerType = "Regular",
            IsFirstOrder = true,
            ItemCount = 1
        };
        
        // Act
        var result = engine.Execute(order);
        
        // Assert
        Assert.Equal(15m, order.DiscountAmount); // 15% of 100
        Assert.Contains("First order", order.DiscountReason);
    }
    
    [Fact]
    public void DiscountEngine_ElectronicsBundle_ReceivesDiscount()
    {
        // Arrange
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD004",
            TotalAmount = 500m,
            CustomerType = "Regular",
            ProductCategory = "Electronics",
            ItemCount = 3
        };
        
        // Act
        var result = engine.Execute(order);
        
        // Assert
        Assert.Equal(50m, order.DiscountAmount); // 10% of 500
        Assert.Contains("Electronics bundle", order.DiscountReason);
    }
    
    [Fact]
    public void DiscountEngine_PremiumWithLargeOrder_GetsFreeShipping()
    {
        // Arrange
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD005",
            TotalAmount = 150m,
            CustomerType = "Premium",
            ItemCount = 1
        };
        
        // Act
        var result = engine.Execute(order);
        
        // Assert
        Assert.True(order.FreeShipping);
        Assert.Contains("Free shipping", order.DiscountReason);
    }
    
    [Fact]
    public void DiscountEngine_MultipleRulesApply_StacksDiscounts()
    {
        // Arrange
        var engine = DiscountRulesExample.CreateDiscountEngine();
        var order = new Order
        {
            OrderId = "ORD006",
            TotalAmount = 600m,
            CustomerType = "VIP",
            ItemCount = 1
        };
        
        // Act
        var result = engine.Execute(order);
        
        // Assert
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
    [Fact]
    public void ApprovalEngine_SmallPurchase_RequiresManagerApproval()
    {
        // Arrange
        var engine = ApprovalWorkflowExample.CreateApprovalEngine();
        var request = new PurchaseRequest
        {
            RequestId = "PR001",
            Amount = 5000m,
            Department = "IT"
        };
        
        // Act
        var result = engine.Execute(request);
        
        // Assert
        Assert.Equal("Manager", request.ApprovalLevel);
        Assert.Contains("Manager", request.RequiredApprovers);
    }
    
    [Fact]
    public void ApprovalEngine_MediumPurchase_RequiresDirectorApproval()
    {
        // Arrange
        var engine = ApprovalWorkflowExample.CreateApprovalEngine();
        var request = new PurchaseRequest
        {
            RequestId = "PR002",
            Amount = 25000m,
            Department = "Marketing"
        };
        
        // Act
        var result = engine.Execute(request);
        
        // Assert
        Assert.Equal("Director", request.ApprovalLevel);
        Assert.Contains("Director", request.RequiredApprovers);
    }
    
    [Fact]
    public void ApprovalEngine_LargePurchase_RequiresCFOApproval()
    {
        // Arrange
        var engine = ApprovalWorkflowExample.CreateApprovalEngine();
        var request = new PurchaseRequest
        {
            RequestId = "PR003",
            Amount = 75000m,
            Department = "Operations"
        };
        
        // Act
        var result = engine.Execute(request);
        
        // Assert
        Assert.Equal("CFO", request.ApprovalLevel);
        Assert.Contains("CFO", request.RequiredApprovers);
    }
    
    [Fact]
    public void ApprovalEngine_VeryLargePurchase_RequiresCEOApproval()
    {
        // Arrange
        var engine = ApprovalWorkflowExample.CreateApprovalEngine();
        var request = new PurchaseRequest
        {
            RequestId = "PR004",
            Amount = 150000m,
            Department = "Operations"
        };
        
        // Act
        var result = engine.Execute(request);
        
        // Assert
        Assert.Equal("CEO", request.ApprovalLevel);
        Assert.Contains("CEO", request.RequiredApprovers);
    }
    
    [Fact]
    public void ApprovalEngine_FinanceDepartment_RequiresCFO()
    {
        // Arrange
        var engine = ApprovalWorkflowExample.CreateApprovalEngine();
        var request = new PurchaseRequest
        {
            RequestId = "PR005",
            Amount = 5000m, // Small amount
            Department = "Finance" // But finance department
        };
        
        // Act
        var result = engine.Execute(request);
        
        // Assert
        Assert.Equal("CFO", request.ApprovalLevel);
        Assert.Contains("CFO", request.RequiredApprovers);
    }
    
    [Fact]
    public void ApprovalEngine_ITEquipmentReview_RequiresAdditionalApproval()
    {
        // Arrange
        var mainEngine = ApprovalWorkflowExample.CreateApprovalEngine();
        var reviewEngine = ApprovalWorkflowExample.CreateAdditionalReviewEngine();
        
        var request = new PurchaseRequest
        {
            RequestId = "PR006",
            Amount = 30000m,
            Category = "IT Equipment"
        };
        
        // Act
        mainEngine.Execute(request);
        reviewEngine.Execute(request);
        
        // Assert
        Assert.True(request.RequiresAdditionalReview);
        Assert.Contains("IT Director", request.RequiredApprovers);
    }
}
