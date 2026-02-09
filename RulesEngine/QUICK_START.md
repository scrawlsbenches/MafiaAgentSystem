# Rules Engine - Quick Start Guide

Get started with the Rules Engine in 5 minutes!

## Setup

```bash
cd RulesEngine
dotnet restore
dotnet build
```

## Run the Demo

```bash
dotnet run --project RulesEngine.Demo
```

This runs 6 interactive demos showing:
1. Basic rule creation
2. Multiple rules with priorities
3. Dynamic rule generation
4. E-commerce discount engine
5. Approval workflow
6. Performance tracking

## Run the Tests

```bash
dotnet test
```

You should see 2,270+ tests pass across the full test suite.

## Your First Rule

### 1. Create a Simple Rule

```csharp
using RulesEngine.Core;

// Define what you're evaluating
public class Order
{
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
}

// Create the rule
var rule = new RuleBuilder<Order>()
    .WithName("Large Order Discount")
    .When(order => order.TotalAmount > 500)
    .Then(order => order.DiscountAmount = 50)
    .Build();

// Execute it
var order = new Order { TotalAmount = 600 };
rule.Execute(order);

Console.WriteLine($"Discount: ${order.DiscountAmount}"); // Output: $50
```

### 2. Create a Rules Engine

```csharp
var engine = new RulesEngineCore<Order>();

// Add multiple rules
engine.RegisterRule(new RuleBuilder<Order>()
    .WithName("Large Order")
    .When(order => order.TotalAmount > 500)
    .Then(order => order.DiscountAmount += 50)
    .Build());

engine.RegisterRule(new RuleBuilder<Order>()
    .WithName("VIP Customer")
    .When(order => order.CustomerType == "VIP")
    .Then(order => order.DiscountAmount += order.TotalAmount * 0.20m)
    .Build());

// Execute all applicable rules
var result = engine.Execute(order);
Console.WriteLine($"Matched {result.MatchedRules} rules");
```

## Common Patterns

### Pattern 1: Complex Conditions

```csharp
var rule = new RuleBuilder<Customer>()
    .WithName("Eligible for Promotion")
    .When(c => c.Age >= 18)
    .And(c => c.TotalPurchases > 1000)
    .Or(c => c.IsVIP)
    .Then(c => c.PromotionCode = "SPECIAL20")
    .Build();
```

### Pattern 2: Priority-Based Execution

```csharp
// Higher priority = executes first
var urgentRule = new RuleBuilder<Task>()
    .WithPriority(100)
    .When(task => task.Priority == "Urgent")
    .Then(task => task.AssignTo = "OnCallTeam")
    .Build();

var normalRule = new RuleBuilder<Task>()
    .WithPriority(50)
    .When(task => task.Priority == "Normal")
    .Then(task => task.AssignTo = "StandardQueue")
    .Build();
```

### Pattern 3: Dynamic Rules from Config

```csharp
// Create rules from JSON, database, etc.
var definitions = new List<RuleDefinition>
{
    new RuleDefinition
    {
        Name = "Premium Shipping",
        Conditions = new List<ConditionDefinition>
        {
            new ConditionDefinition
            {
                PropertyName = "Total",
                Operator = ">",
                Value = 100m
            }
        }
    }
};

var rules = DynamicRuleFactory.CreateRulesFromDefinitions<Order>(definitions);
```

### Pattern 4: Stop on First Match

```csharp
// For approval workflows - stop after determining level
var engine = new RulesEngineCore<PurchaseRequest>(new RulesEngineOptions
{
    StopOnFirstMatch = true
});

engine.RegisterRules(
    ceoApprovalRule,    // Priority 100
    cfoApprovalRule,    // Priority 90
    directorApprovalRule // Priority 80
);
```

### Pattern 5: Performance Tracking

```csharp
var engine = new RulesEngineCore<Order>(new RulesEngineOptions
{
    TrackPerformance = true
});

// After execution
var metrics = engine.GetMetrics("MY_RULE_ID");
Console.WriteLine($"Average execution time: {metrics.AverageExecutionTime.TotalMilliseconds}ms");
```

## Real-World Examples

### E-Commerce Discounts

See `Examples/DiscountRulesExample.cs` for a complete implementation with:
- VIP customer discounts
- Large order discounts
- First-time customer discounts
- Bundle deals
- Free shipping rules

### Approval Workflows

See `Examples/ApprovalWorkflowExample.cs` for:
- Multi-level approval routing
- Department-specific rules
- Amount-based escalation
- Urgent request handling

## Next Steps

1. **Explore the Tests** - Hundreds of examples in `RulesEngine.Tests/`
2. **Run the Demo** - See everything in action
3. **Read the README** - Comprehensive guide with advanced features
4. **Build Your Rules** - Apply to your own domain

## Tips

✅ **DO:**
- Use descriptive rule names
- Set appropriate priorities
- Cache rules (don't recreate unnecessarily)
- Use performance tracking in production
- Write tests for your rules

❌ **DON'T:**
- Put business logic in actions that should be in conditions
- Create rules inside loops (cache them!)
- Ignore rule execution errors
- Mix unrelated rules in one engine

## Common Issues

**Q: My rule isn't executing**
- Check the condition is returning true
- Verify the rule is registered with the engine
- Make sure priority is set correctly

**Q: Rules executing in wrong order**
- Set priorities (higher = first)
- Check engine options (StopOnFirstMatch?)

**Q: Performance is slow**
- Enable performance tracking to find bottlenecks
- Cache compiled rules
- Consider parallel execution

## Help & Resources

- Full README: Comprehensive documentation
- Tests: Hundreds of working examples
- Demo: Interactive demonstrations
- Examples: Real-world scenarios

Happy rule building! 🎯
