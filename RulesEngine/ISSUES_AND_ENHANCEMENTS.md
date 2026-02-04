# Rules Engine - Issues & Enhancements

This document identifies issues found during code review and the solutions implemented.

> **Status Legend:** ✅ Resolved | 🔄 Partial | ⏳ Pending

---

## 🔴 Critical Issues

### Issue 1: Thread Safety ✅ RESOLVED (2026-01-31)

**Resolution:** Implemented `ReaderWriterLockSlim` in `RulesEngineCore<T>`. Multiple threads can evaluate rules concurrently (read lock), while rule registration acquires exclusive write lock. See `RulesEngine/Core/RulesEngineCore.cs`.

**Problem:**
```csharp
// Current implementation uses List<IRule<T>>
private readonly List<IRule<T>> _rules;

// This is NOT thread-safe for concurrent add/remove operations
engine.RegisterRule(rule1);  // Thread 1
engine.Execute(fact);        // Thread 2 - could throw!
```

**Impact:** In multi-threaded environments (web servers, background processors), concurrent modifications can cause:
- `InvalidOperationException: Collection was modified`
- Race conditions
- Inconsistent rule evaluation

**Solution:**
```csharp
// Use ConcurrentBag or ReaderWriterLockSlim
private readonly ConcurrentDictionary<string, IRule<T>> _rules;

// Or use immutable pattern
public RulesEngineCore<T> WithRule(IRule<T> rule)
{
    var newRules = new List<IRule<T>>(_rules) { rule };
    return new RulesEngineCore<T>(newRules, _options);
}
```

### Issue 2: Parameter Replacement Can Fail ⏳ PENDING

> **Status (2026-02-04):** The current implementation in `RuleBuilder.cs:159-174` uses a simple `ParameterReplacer` that only handles direct parameter references. The `Expression.Invoke` solution shown below has NOT been implemented. For most use cases (non-closure expressions), the current implementation works correctly.

**Problem:**
```csharp
// When combining expressions, parameter replacement might miss closures
Expression<Func<int, bool>> outer = x => x > capturedValue; // Closure!
Expression<Func<int, bool>> inner = x => x < 10;
// Combining these can produce incorrect results
```

**Impact:** Complex expressions with closures may not combine correctly.

**Suggested Solution (NOT YET IMPLEMENTED):**
```csharp
// Use Expression.Invoke for safer composition
public static Expression<Func<T, bool>> SafeCombine<T>(
    Expression<Func<T, bool>> left,
    Expression<Func<T, bool>> right)
{
    var parameter = Expression.Parameter(typeof(T), "x");
    
    // Use Invoke instead of parameter replacement
    var invokeLeft = Expression.Invoke(left, parameter);
    var invokeRight = Expression.Invoke(right, parameter);
    var combined = Expression.AndAlso(invokeLeft, invokeRight);
    
    return Expression.Lambda<Func<T, bool>>(combined, parameter);
}
```

### Issue 3: No Circular Dependency Detection ⏳ PENDING (Clarified 2026-02-04)

> **Clarification (2026-02-04):** The current engine uses **single-pass execution** - each rule is evaluated once per `Execute()` call. The "circular dependency" scenario below only manifests if:
> 1. You call `EvaluateAll()` or `Execute()` in a loop yourself
> 2. Rules modify state that affects other rules within the same pass
>
> The engine does NOT automatically re-evaluate rules after state changes. This is a design choice, not a bug. The suggested solution below would be needed for a **forward-chaining** engine that re-fires rules until no more match.

**Problem:**
```csharp
// Rule A modifies property that Rule B checks
var ruleA = new RuleBuilder<Data>()
    .When(d => d.Status == "Pending")
    .Then(d => d.Status = "Processing")
    .Build();

var ruleB = new RuleBuilder<Data>()
    .When(d => d.Status == "Processing")
    .Then(d => d.Status = "Pending")  // Circular!
    .Build();

// Only a problem if you call Execute() in a loop until no rules match
```

**Impact:** Rules can create unexpected state changes within a single pass if evaluation order matters.

**Solution:**
```csharp
public class RulesEngineCore<T>
{
    private readonly int _maxIterations = 100;
    
    public RulesEngineResult Execute(T fact)
    {
        var result = new RulesEngineResult();
        var iterations = 0;
        var previousState = SerializeState(fact);
        
        while (iterations < _maxIterations)
        {
            var matchedAny = ExecuteOnePass(fact, result);
            
            if (!matchedAny)
                break;
                
            var currentState = SerializeState(fact);
            if (currentState == previousState)
                break; // No state change, we're done
                
            previousState = currentState;
            iterations++;
        }
        
        if (iterations >= _maxIterations)
        {
            result.AddError("Maximum iterations reached - possible circular dependency");
        }
        
        return result;
    }
}
```

### Issue 4: Memory Leaks in Expression Caching ✅ RESOLVED (2026-01-31)

**Resolution:** Implemented LRU eviction in `CachingMiddleware` with configurable max entries (default: 1000). See `AgentRouting/Middleware/CommonMiddleware.cs`.

> **Note (2026-02-04):** This fix applies to AgentRouting's `CachingMiddleware`, not the RulesEngine core. The RulesEngine itself compiles expressions once per rule registration and holds them for the rule's lifetime - no unbounded caching occurs. If dynamic rule creation at scale is needed, consider using `ImmutableRulesEngine<T>` which allows discarding old engine instances.

**Original Problem:**
```csharp
// Current caching never removes old entries
private static readonly ConcurrentDictionary<string, Func<T, object?>> _cache = new();

// Over time, this grows unbounded
for (int i = 0; i < 1000000; i++)
{
    var getter = PropertyAccessor<Person>.GetGetter($"Property{i}"); // Memory leak!
}
```

**Impact:** Long-running applications can consume excessive memory.

**Solution:**
```csharp
public class LRUCache<TKey, TValue> where TKey : notnull
{
    private readonly int _maxSize;
    private readonly ConcurrentDictionary<TKey, CacheEntry<TValue>> _cache = new();
    private readonly LinkedList<TKey> _lruList = new();
    private readonly object _lock = new();
    
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                // Move to front
                _lruList.Remove(entry.Node);
                entry.Node = _lruList.AddFirst(key);
                return entry.Value;
            }
            
            var value = factory(key);
            
            if (_cache.Count >= _maxSize)
            {
                // Remove least recently used
                var lruKey = _lruList.Last!.Value;
                _lruList.RemoveLast();
                _cache.TryRemove(lruKey, out _);
            }
            
            var node = _lruList.AddFirst(key);
            _cache[key] = new CacheEntry<TValue>(value, node);
            return value;
        }
    }
    
    private class CacheEntry<T>
    {
        public T Value { get; }
        public LinkedListNode<TKey> Node { get; set; }
        
        public CacheEntry(T value, LinkedListNode<TKey> node)
        {
            Value = value;
            Node = node;
        }
    }
}
```

## 🟡 Significant Issues

### Issue 5: No Rule Validation ✅ RESOLVED (2026-01-31)

**Resolution:** Added `RuleValidationException` thrown on registration for null/empty ID, null/empty Name, or duplicate IDs (configurable). See `RulesEngine/Core/RulesEngineCore.cs`.

**Problem:**
```csharp
// This compiles but will crash at runtime
var rule = DynamicRuleFactory.CreatePropertyRule<Person>(
    "INVALID",
    "Invalid Rule",
    "NonExistentProperty",  // Property doesn't exist!
    "==",
    "value"
);
```

**Solution:**
```csharp
public static class RuleValidator
{
    public static ValidationResult Validate<T>(IRule<T> rule)
    {
        var result = new ValidationResult();
        
        if (rule is Rule<T> concreteRule)
        {
            try
            {
                // Try to compile the expression
                var compiled = concreteRule.Condition.Compile();
                
                // Check for common issues
                var visitor = new ValidationVisitor();
                visitor.Visit(concreteRule.Condition);
                
                if (visitor.Errors.Any())
                {
                    result.AddErrors(visitor.Errors);
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Expression compilation failed: {ex.Message}");
            }
        }
        
        return result;
    }
    
    private class ValidationVisitor : ExpressionVisitor
    {
        public List<string> Errors { get; } = new();
        
        protected override Expression VisitMember(MemberExpression node)
        {
            // Validate property exists
            if (node.Member is PropertyInfo prop)
            {
                if (!prop.CanRead)
                {
                    Errors.Add($"Property {prop.Name} is not readable");
                }
            }
            return base.VisitMember(node);
        }
    }
}

public class ValidationResult
{
    public bool IsValid => !Errors.Any();
    public List<string> Errors { get; } = new();
    
    public void AddError(string error) => Errors.Add(error);
    public void AddErrors(IEnumerable<string> errors) => Errors.AddRange(errors);
}
```

### Issue 6: No Rule Conflict Detection

**Problem:**
```csharp
// These rules conflict - which discount applies?
var rule1 = new RuleBuilder<Order>()
    .When(o => o.Total > 100)
    .Then(o => o.Discount = 10)
    .Build();

var rule2 = new RuleBuilder<Order>()
    .When(o => o.Total > 100)
    .Then(o => o.Discount = 20)  // Overwrites rule1!
    .Build();
```

**Solution:**
```csharp
public class ConflictDetector<T>
{
    public List<RuleConflict> DetectConflicts(IEnumerable<IRule<T>> rules)
    {
        var conflicts = new List<RuleConflict>();
        var ruleList = rules.ToList();
        
        for (int i = 0; i < ruleList.Count; i++)
        {
            for (int j = i + 1; j < ruleList.Count; j++)
            {
                var conflict = CheckConflict(ruleList[i], ruleList[j]);
                if (conflict != null)
                {
                    conflicts.Add(conflict);
                }
            }
        }
        
        return conflicts;
    }
    
    private RuleConflict? CheckConflict(IRule<T> rule1, IRule<T> rule2)
    {
        // Sample multiple facts to see if rules produce conflicting results
        var testCases = GenerateTestCases();
        
        foreach (var testCase in testCases)
        {
            var clone1 = DeepClone(testCase);
            var clone2 = DeepClone(testCase);
            
            if (rule1.Evaluate(clone1) && rule2.Evaluate(clone2))
            {
                rule1.Execute(clone1);
                rule2.Execute(clone2);
                
                if (!AreEqual(clone1, clone2))
                {
                    return new RuleConflict
                    {
                        Rule1 = rule1.Name,
                        Rule2 = rule2.Name,
                        Description = "Rules produce different outcomes for same input"
                    };
                }
            }
        }
        
        return null;
    }
}
```

### Issue 7: Poor Debugging Support ✅ RESOLVED (2026-02-04)

**Resolution:** Implemented `DebuggableRule<T>` in `RulesEngine/Enhanced/RuleValidation.cs`. This class tracks evaluation traces using ThreadLocal storage and decomposes expressions to show which parts matched or failed.

**Original Problem:**
```csharp
// Why didn't this rule match?
var result = engine.Execute(order);
// No way to know which part of the condition failed!
```

**Implemented Solution (see RuleValidation.cs:185-273):**
```csharp
public class DebuggableRule<T> : Rule<T>
{
    private readonly List<string> _evaluationTrace = new();
    
    public IReadOnlyList<string> EvaluationTrace => _evaluationTrace;
    
    public override bool Evaluate(T fact)
    {
        _evaluationTrace.Clear();
        
        try
        {
            var result = base.Evaluate(fact);
            
            if (!result)
            {
                // Decompose the expression and evaluate each part
                var decomposed = DecomposeExpression(Condition.Body);
                foreach (var part in decomposed)
                {
                    var partLambda = Expression.Lambda<Func<T, bool>>(part, Condition.Parameters);
                    var partResult = partLambda.Compile()(fact);
                    _evaluationTrace.Add($"{part} = {partResult}");
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _evaluationTrace.Add($"ERROR: {ex.Message}");
            throw;
        }
    }
    
    private IEnumerable<Expression> DecomposeExpression(Expression expr)
    {
        if (expr is BinaryExpression binary)
        {
            yield return binary.Left;
            yield return binary.Right;
            
            foreach (var sub in DecomposeExpression(binary.Left))
                yield return sub;
            foreach (var sub in DecomposeExpression(binary.Right))
                yield return sub;
        }
    }
}

// Usage:
var debugRule = new DebugRule<Order>(/* ... */);
debugRule.Evaluate(order);

Console.WriteLine("Evaluation trace:");
foreach (var trace in debugRule.EvaluationTrace)
{
    Console.WriteLine($"  {trace}");
}
// Output:
//   order.Total > 100 = False
//   order.CustomerType == "VIP" = True
```

## 🟢 Enhancements

### Enhancement 1: Async Rule Support ✅ IMPLEMENTED (2026-01-31)

**Implementation:** Added `IAsyncRule<T>`, `AsyncRule<T>`, and `AsyncRuleBuilder<T>`. `RulesEngineCore<T>.ExecuteAsync()` handles both sync and async rules with cancellation support. See `RulesEngine/Core/AsyncRule.cs`.

**Original Limitation:** Rules couldn't perform async operations.

**Enhancement:**
```csharp
public interface IAsyncRule<T>
{
    Task<bool> EvaluateAsync(T fact, CancellationToken cancellationToken = default);
    Task<RuleResult> ExecuteAsync(T fact, CancellationToken cancellationToken = default);
}

public class AsyncRule<T> : IAsyncRule<T>
{
    private readonly Func<T, CancellationToken, Task<bool>> _condition;
    private readonly List<Func<T, CancellationToken, Task>> _actions;
    
    public async Task<bool> EvaluateAsync(T fact, CancellationToken ct = default)
    {
        return await _condition(fact, ct);
    }
    
    public async Task<RuleResult> ExecuteAsync(T fact, CancellationToken ct = default)
    {
        if (!await EvaluateAsync(fact, ct))
        {
            return RuleResult.NotMatched(Id, Name);
        }
        
        foreach (var action in _actions)
        {
            await action(fact, ct);
        }
        
        return RuleResult.Success(Id, Name);
    }
}

// Usage:
var asyncRule = new AsyncRuleBuilder<Order>()
    .WithName("Credit Check")
    .WhenAsync(async (order, ct) =>
    {
        var creditScore = await _creditService.GetScoreAsync(order.CustomerId, ct);
        return creditScore > 650;
    })
    .ThenAsync(async (order, ct) =>
    {
        await _approvalService.AutoApproveAsync(order.Id, ct);
    })
    .Build();
```

### Enhancement 2: Rule Serialization

**Need:** Store rules in database/config and reload them.

**Enhancement:**
```csharp
public class RuleSerializer
{
    public string Serialize<T>(Rule<T> rule)
    {
        var definition = new SerializableRule
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            Priority = rule.Priority,
            Condition = ExpressionToString(rule.Condition)
        };
        
        return JsonSerializer.Serialize(definition);
    }
    
    public Rule<T> Deserialize<T>(string json)
    {
        var definition = JsonSerializer.Deserialize<SerializableRule>(json)!;
        
        // Parse the condition string back to expression
        var condition = StringToExpression<T>(definition.Condition);
        
        return new Rule<T>(
            definition.Id,
            definition.Name,
            condition,
            definition.Description,
            definition.Priority
        );
    }
    
    private string ExpressionToString<T>(Expression<Func<T, bool>> expr)
    {
        // Simple serialization - in production use ExpressionSerializer library
        return expr.ToString();
    }
    
    private Expression<Func<T, bool>> StringToExpression<T>(string exprString)
    {
        // Parse expression - in production use DynamicExpresso or similar
        // This is complex - typically use a library
        throw new NotImplementedException("Use DynamicExpresso or similar library");
    }
}

// Better approach: Use existing library
// Install-Package DynamicExpresso.Core
using DynamicExpresso;

public class ImprovedRuleSerializer
{
    private readonly Interpreter _interpreter = new Interpreter();
    
    public string SerializeCondition<T>(Expression<Func<T, bool>> condition)
    {
        return condition.ToString();
    }
    
    public Expression<Func<T, bool>> DeserializeCondition<T>(string conditionString)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        _interpreter.SetVariable("x", parameter);
        
        var lambda = _interpreter.ParseAsExpression<Func<T, bool>>(
            conditionString, 
            typeof(T)
        );
        
        return (Expression<Func<T, bool>>)lambda;
    }
}
```

### Enhancement 3: Rule Versioning

**Need:** Track rule changes over time.

**Enhancement:**
```csharp
public class VersionedRule<T>
{
    public string Id { get; set; } = "";
    public int Version { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public IRule<T> Rule { get; set; } = null!;
    public string ChangedBy { get; set; } = "";
    public string ChangeReason { get; set; } = "";
}

public class VersionedRulesEngine<T>
{
    private readonly List<VersionedRule<T>> _versionedRules = new();
    
    public void RegisterRule(IRule<T> rule, string changedBy, string reason)
    {
        var existing = _versionedRules
            .Where(vr => vr.Id == rule.Id)
            .OrderByDescending(vr => vr.Version)
            .FirstOrDefault();
        
        var newVersion = new VersionedRule<T>
        {
            Id = rule.Id,
            Version = (existing?.Version ?? 0) + 1,
            EffectiveDate = DateTime.UtcNow,
            Rule = rule,
            ChangedBy = changedBy,
            ChangeReason = reason
        };
        
        // Expire the old version
        if (existing != null)
        {
            existing.ExpiryDate = DateTime.UtcNow;
        }
        
        _versionedRules.Add(newVersion);
    }
    
    public RulesEngineResult Execute(T fact, DateTime? asOf = null)
    {
        var effectiveDate = asOf ?? DateTime.UtcNow;
        
        var activeRules = _versionedRules
            .Where(vr => vr.EffectiveDate <= effectiveDate)
            .Where(vr => !vr.ExpiryDate.HasValue || vr.ExpiryDate > effectiveDate)
            .GroupBy(vr => vr.Id)
            .Select(g => g.OrderByDescending(vr => vr.Version).First())
            .Select(vr => vr.Rule);
        
        var engine = new RulesEngineCore<T>();
        foreach (var rule in activeRules)
        {
            engine.RegisterRule(rule);
        }
        
        return engine.Execute(fact);
    }
    
    public List<VersionedRule<T>> GetRuleHistory(string ruleId)
    {
        return _versionedRules
            .Where(vr => vr.Id == ruleId)
            .OrderByDescending(vr => vr.Version)
            .ToList();
    }
}
```

### Enhancement 4: Rule Templates

**Need:** Reuse common rule patterns.

**Enhancement:**
```csharp
public static class RuleTemplates
{
    // Template: Threshold rule
    public static Rule<T> CreateThresholdRule<T>(
        string id,
        string name,
        Expression<Func<T, decimal>> valueSelector,
        decimal threshold,
        Action<T> action)
    {
        var parameter = valueSelector.Parameters[0];
        var comparison = Expression.GreaterThan(
            valueSelector.Body,
            Expression.Constant(threshold)
        );
        var condition = Expression.Lambda<Func<T, bool>>(comparison, parameter);
        
        return new Rule<T>(id, name, condition).WithAction(action);
    }
    
    // Template: Time-based rule
    public static Rule<T> CreateTimeWindowRule<T>(
        string id,
        string name,
        TimeSpan startTime,
        TimeSpan endTime,
        Expression<Func<T, bool>> condition,
        Action<T> action)
    {
        return new RuleBuilder<T>()
            .WithId(id)
            .WithName(name)
            .When(fact =>
            {
                var now = DateTime.Now.TimeOfDay;
                return now >= startTime && now <= endTime;
            })
            .And(condition)
            .Then(action)
            .Build();
    }
    
    // Template: Percentage-based discount
    public static Rule<Order> CreatePercentageDiscountRule(
        string id,
        string name,
        Expression<Func<Order, bool>> eligibility,
        decimal percentage)
    {
        return new RuleBuilder<Order>()
            .WithId(id)
            .WithName(name)
            .When(eligibility)
            .Then(order => order.DiscountAmount += order.TotalAmount * percentage)
            .Build();
    }
}

// Usage:
var vipRule = RuleTemplates.CreatePercentageDiscountRule(
    "VIP_DISCOUNT",
    "VIP Customer Discount",
    order => order.CustomerType == "VIP",
    0.20m
);
```

### Enhancement 5: Integration with Dependency Injection

**Need:** Use services in rule actions.

**Enhancement:**
```csharp
public class ServiceAwareRule<T> : IRule<T>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Expression<Func<T, bool>> _condition;
    private readonly List<Func<T, IServiceProvider, Task>> _actions;
    
    public async Task<RuleResult> ExecuteAsync(T fact)
    {
        if (!Evaluate(fact))
        {
            return RuleResult.NotMatched(Id, Name);
        }
        
        foreach (var action in _actions)
        {
            await action(fact, _serviceProvider);
        }
        
        return RuleResult.Success(Id, Name);
    }
}

// Usage with ASP.NET Core:
public class OrderRulesConfiguration
{
    public void ConfigureRules(
        RulesEngineCore<Order> engine,
        IServiceProvider services)
    {
        var emailRule = new RuleBuilder<Order>()
            .WithName("VIP Email Notification")
            .When(order => order.CustomerType == "VIP")
            .Then(order =>
            {
                var emailService = services.GetRequiredService<IEmailService>();
                emailService.SendVIPConfirmation(order);
            })
            .Build();
        
        engine.RegisterRule(emailRule);
    }
}
```

## 📊 Performance Optimizations

### Optimization 1: Expression Tree Caching

```csharp
public class OptimizedRulesEngine<T>
{
    private readonly ConcurrentDictionary<string, Func<T, bool>> _compiledConditions = new();
    
    public void RegisterRule(Rule<T> rule)
    {
        // Cache the compiled expression
        _compiledConditions.GetOrAdd(rule.Id, _ => rule.Condition.Compile());
        _rules.Add(rule);
    }
    
    public RulesEngineResult Execute(T fact)
    {
        // Use cached compiled expressions
        foreach (var rule in _rules)
        {
            var compiledCondition = _compiledConditions[rule.Id];
            if (compiledCondition(fact))
            {
                // Execute rule
            }
        }
    }
}
```

### Optimization 2: Parallel Rule Evaluation (Improved)

```csharp
public class ParallelRulesEngine<T>
{
    public async Task<RulesEngineResult> ExecuteParallelAsync(T fact)
    {
        var tasks = _rules.Select(async rule =>
        {
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() => rule.Execute(fact));
            sw.Stop();
            
            return (result, duration: sw.Elapsed);
        });
        
        var results = await Task.WhenAll(tasks);
        
        var engineResult = new RulesEngineResult();
        foreach (var (result, duration) in results)
        {
            engineResult.AddRuleResult(result);
            TrackPerformance(result.RuleId, duration);
        }
        
        return engineResult;
    }
}
```

## 🎯 Best Practices Summary

1. **Always validate rules** before adding to production
2. **Use thread-safe collections** for multi-threaded scenarios
3. **Implement circuit breakers** for rules that call external services
4. **Version your rules** to track changes over time
5. **Monitor performance** in production
6. **Test rule conflicts** before deployment
7. **Use LRU caching** to prevent memory leaks
8. **Implement proper logging** for rule execution
9. **Consider async patterns** for I/O-bound operations
10. **Separate rule definition from execution** for maintainability

These enhancements transform the basic rules engine into a production-ready system suitable for enterprise applications!
