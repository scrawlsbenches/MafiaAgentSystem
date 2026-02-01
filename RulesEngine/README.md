# Rules Engine

A powerful, production-ready rules engine built with C# expression trees. Create, manage, and execute business rules dynamically at runtime.

## Overview

This rules engine enables you to:
- **Define rules using expression trees** - Type-safe, compilable conditions
- **Execute rules with priorities** - Control evaluation order
- **Build complex logic** - Combine rules with AND, OR, NOT operators
- **Track performance** - Monitor rule execution metrics
- **Create rules dynamically** - Build rules from configuration at runtime

## Quick Start

### Basic Rule

```csharp
// Create a simple rule
var rule = new RuleBuilder<Order>()
    .WithName("Large Order Discount")
    .When(order => order.TotalAmount > 500)
    .Then(order => order.DiscountAmount = 50)
    .Build();

// Execute the rule
var order = new Order { TotalAmount = 600 };
var result = rule.Execute(order);

Console.WriteLine(order.DiscountAmount); // Output: 50
```

### Rules Engine

```csharp
// Create an engine
var engine = new RulesEngineCore<Order>();

// Register multiple rules
engine.RegisterRules(
    discountRule,
    freeShippingRule,
    loyaltyPointsRule
);

// Execute all applicable rules
var result = engine.Execute(order);

Console.WriteLine($"Matched {result.MatchedRules} rules");
Console.WriteLine($"Executed in {result.TotalExecutionTime.TotalMilliseconds}ms");
```

## Key Features

### 1. Fluent Rule Builder

Build complex rules with a readable, fluent API:

```csharp
var rule = new RuleBuilder<Customer>()
    .WithId("VIP_BENEFITS")
    .WithName("VIP Customer Benefits")
    .WithDescription("Special benefits for VIP customers")
    .WithPriority(100)
    .When(customer => customer.TotalPurchases > 10000)
    .And(customer => customer.YearsAsCustomer > 2)
    .Or(customer => customer.ReferralCount > 5)
    .Then(customer => customer.DiscountRate = 0.15m)
    .Then(customer => customer.FreeShipping = true)
    .Build();
```

### 2. Composite Rules

Combine multiple rules with logical operators:

```csharp
var rule1 = new Rule<Account>("R1", "High Balance", acc => acc.Balance > 10000);
var rule2 = new Rule<Account>("R2", "Active", acc => acc.IsActive);

var compositeRule = new CompositeRuleBuilder<Account>()
    .WithName("Premium Account")
    .WithOperator(CompositeOperator.And)
    .AddRule(rule1)
    .AddRule(rule2)
    .Build();
```

### 3. Dynamic Rule Creation

Create rules from configuration at runtime:

```csharp
// From property comparisons
var rule = DynamicRuleFactory.CreatePropertyRule<Product>(
    "EXPENSIVE_ITEMS",
    "Expensive Products",
    "Price",
    ">",
    1000m
);

// From multiple conditions
var rule = DynamicRuleFactory.CreateMultiConditionRule<Product>(
    "VALID_PRODUCT",
    "Valid Product",
    ("Price", ">", 0m),
    ("InStock", "==", true),
    ("Category", "!=", "Discontinued")
);

// From JSON/config definitions
var definitions = LoadFromConfig(); // Your configuration source
var rules = DynamicRuleFactory.CreateRulesFromDefinitions<Product>(definitions);
```

### 4. Engine Configuration

Fine-tune engine behavior:

```csharp
var engine = new RulesEngineCore<Order>(new RulesEngineOptions
{
    StopOnFirstMatch = true,       // Stop after first matching rule
    EnableParallelExecution = true, // Execute rules in parallel
    TrackPerformance = true,        // Track execution metrics
    MaxRulesToExecute = 10          // Limit number of rules
});
```

### 5. Performance Tracking

Monitor rule performance:

```csharp
// After executing rules
var metrics = engine.GetMetrics("MY_RULE_ID");

Console.WriteLine($"Executed: {metrics.ExecutionCount} times");
Console.WriteLine($"Average: {metrics.AverageExecutionTime.TotalMilliseconds}ms");
Console.WriteLine($"Min: {metrics.MinExecutionTime.TotalMilliseconds}ms");
Console.WriteLine($"Max: {metrics.MaxExecutionTime.TotalMilliseconds}ms");
```

## Real-World Examples

### E-Commerce Discounts

```csharp
var engine = new RulesEngineCore<Order>();

// VIP discount
engine.RegisterRule(new RuleBuilder<Order>()
    .WithName("VIP Discount")
    .WithPriority(100)
    .When(order => order.CustomerType == "VIP")
    .Then(order => order.DiscountAmount += order.TotalAmount * 0.20m)
    .Build());

// Large order discount
engine.RegisterRule(new RuleBuilder<Order>()
    .WithName("Large Order")
    .WithPriority(90)
    .When(order => order.TotalAmount > 500)
    .Then(order => order.DiscountAmount += 50)
    .Build());

// First-time customer
engine.RegisterRule(new RuleBuilder<Order>()
    .WithName("First Order")
    .WithPriority(80)
    .When(order => order.IsFirstOrder)
    .Then(order => order.DiscountAmount += order.TotalAmount * 0.15m)
    .Build());

var order = new Order 
{ 
    TotalAmount = 600, 
    CustomerType = "VIP" 
};

var result = engine.Execute(order);
// Applies both VIP (20%) and large order ($50) discounts
```

### Approval Workflows

```csharp
var engine = new RulesEngineCore<PurchaseRequest>(new RulesEngineOptions
{
    StopOnFirstMatch = true // Determine approval level
});

// CEO approval for very large purchases
engine.RegisterRule(new RuleBuilder<PurchaseRequest>()
    .WithPriority(100)
    .When(req => req.Amount > 100000)
    .Then(req => req.ApprovalLevel = "CEO")
    .Build());

// CFO approval for large purchases or finance department
engine.RegisterRule(new RuleBuilder<PurchaseRequest>()
    .WithPriority(90)
    .When(req => req.Amount > 50000)
    .Or(req => req.Department == "Finance")
    .Then(req => req.ApprovalLevel = "CFO")
    .Build());

// Director approval for medium purchases
engine.RegisterRule(new RuleBuilder<PurchaseRequest>()
    .WithPriority(80)
    .When(req => req.Amount > 10000)
    .Then(req => req.ApprovalLevel = "Director")
    .Build());

// Manager approval (default)
engine.RegisterRule(new RuleBuilder<PurchaseRequest>()
    .WithPriority(70)
    .When(req => req.Amount > 0)
    .Then(req => req.ApprovalLevel = "Manager")
    .Build());

var request = new PurchaseRequest { Amount = 75000 };
engine.Execute(request);
Console.WriteLine(request.ApprovalLevel); // "CFO"
```

### Data Validation

```csharp
var validationEngine = new RulesEngineCore<UserRegistration>();

validationEngine.RegisterRules(
    new RuleBuilder<UserRegistration>()
        .WithName("Email Required")
        .When(user => string.IsNullOrEmpty(user.Email))
        .Then(user => user.ValidationErrors.Add("Email is required"))
        .Build(),
    
    new RuleBuilder<UserRegistration>()
        .WithName("Age Requirement")
        .When(user => user.Age < 18)
        .Then(user => user.ValidationErrors.Add("Must be 18 or older"))
        .Build(),
    
    new RuleBuilder<UserRegistration>()
        .WithName("Password Strength")
        .When(user => user.Password.Length < 8)
        .Then(user => user.ValidationErrors.Add("Password must be at least 8 characters"))
        .Build()
);

var user = new UserRegistration { Email = "", Age = 16, Password = "weak" };
var result = validationEngine.Execute(user);

// user.ValidationErrors contains all validation failures
```

## Advanced Features

### Rule Chaining

Execute multiple engines in sequence:

```csharp
// First, determine approval level
var approvalEngine = CreateApprovalEngine();
approvalEngine.Execute(request);

// Then, check for additional reviews needed
var reviewEngine = CreateReviewEngine();
reviewEngine.Execute(request);

// Finally, notify appropriate approvers
var notificationEngine = CreateNotificationEngine();
notificationEngine.Execute(request);
```

### Rule Management

```csharp
// Get all registered rules
var allRules = engine.GetRules();

// Get rules that would match without executing
var matches = engine.GetMatchingRules(order);

// Remove a specific rule
engine.RemoveRule("RULE_ID");

// Clear all rules
engine.ClearRules();
```

### Error Handling

```csharp
var result = engine.Execute(fact);

// Check for errors
if (result.Errors > 0)
{
    var errors = result.GetErrors();
    foreach (var error in errors)
    {
        Console.WriteLine($"Rule {error.RuleName} failed: {error.ErrorMessage}");
    }
}
```

## Architecture

### Expression Trees

The rules engine uses C# expression trees for:
- **Type safety** - Compile-time checking of rule conditions
- **Performance** - Compiled expressions run at near-native speed
- **Inspection** - View rule structure programmatically
- **Serialization** - Convert rules to/from strings for storage

### Rule Lifecycle

1. **Define** - Create rules using builders or factory methods
2. **Register** - Add rules to the engine
3. **Prioritize** - Engine sorts by priority (high to low)
4. **Evaluate** - Check conditions against facts
5. **Execute** - Run actions for matched rules
6. **Track** - Record performance metrics

## Testing

The project includes comprehensive tests using a **custom zero-dependency test framework**:

```bash
# Run from repository root
dotnet run --project Tests/TestRunner/
```

**Current status: 118 tests passing**

Test coverage includes:
- ✅ Basic rule creation and evaluation
- ✅ Fluent builder API
- ✅ Composite rules (AND, OR, NOT)
- ✅ Engine execution and prioritization
- ✅ Dynamic rule factory
- ✅ Real-world scenarios (discounts, approvals, validation)
- ✅ Performance tracking
- ✅ Error handling

## Performance Considerations

### Best Practices

1. **Cache compiled rules** - Don't recreate rules unnecessarily
2. **Use priorities wisely** - High-priority rules execute first
3. **Enable parallel execution** - For independent rules
4. **Limit rule count** - Use MaxRulesToExecute for safety
5. **Profile with metrics** - Identify slow rules

### Performance Characteristics

- **Rule compilation**: One-time cost when rule is created
- **Rule evaluation**: Near-native speed (expression trees)
- **Rule execution**: Depends on action complexity
- **Typical throughput**: 100,000+ evaluations/second

## Use Cases

### Business Rules Management

- Pricing and discounts
- Promotion eligibility
- Loyalty programs
- Product recommendations

### Workflow Automation

- Approval routing
- Task assignment
- Escalation policies
- SLA monitoring

### Data Validation

- Form validation
- Data quality checks
- Business constraint enforcement
- Compliance checking

### Decision Making

- Credit approval
- Risk assessment
- Fraud detection
- Personalization

## Extensibility

### Custom Rule Types

```csharp
public class TimedRule<T> : Rule<T>
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    public override bool Evaluate(T fact)
    {
        var now = DateTime.UtcNow;
        if (now < StartDate || now > EndDate)
            return false;
            
        return base.Evaluate(fact);
    }
}
```

### Custom Actions

```csharp
public interface IAction<T>
{
    void Execute(T fact);
}

// Use dependency injection for actions
var rule = new RuleBuilder<Order>()
    .When(order => order.Total > 1000)
    .Then(order => serviceProvider.GetService<IEmailService>().SendVIPEmail(order))
    .Build();
```

## License

This is an educational project - use freely for learning and building!

## Next Steps

1. Run the tests to see everything in action
2. Check out the Examples folder for real-world scenarios
3. Build your own rules for your domain
4. Extend the engine with custom features

Happy rule building! 🎯
