# Code Deduplication Plan

## Overview

This document outlines a plan to consolidate duplicate code in the MafiaAgentSystem codebase. Each section describes the duplication, proposed solution, files affected, and risk level.

---

## 1. Rule Validation Logic

### Current State
The same validation logic is repeated 3 times in `RulesEngineCore.cs`:

```csharp
// Lines 67-70 (RegisterRule)
if (string.IsNullOrEmpty(rule.Id))
    throw new RuleValidationException("Rule ID cannot be null or empty");
if (string.IsNullOrEmpty(rule.Name))
    throw new RuleValidationException("Rule name cannot be null or empty", rule.Id);

// Lines 100-103 (RegisterRules) - identical
// Lines 135-138 (AddRule) - same pattern with string parameters
```

### Proposed Solution
Extract to a private helper method:

```csharp
private static void ValidateRuleIdentifiers(string id, string name)
{
    if (string.IsNullOrEmpty(id))
        throw new RuleValidationException("Rule ID cannot be null or empty");
    if (string.IsNullOrEmpty(name))
        throw new RuleValidationException("Rule name cannot be null or empty", id);
}
```

### Files Changed
- `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`

### Risk: LOW
- Internal refactoring only
- No API changes
- Easy to test

---

## 2. Test Helper Methods

### Current State
Identical helper methods exist in multiple test files:

| Method | Files |
|--------|-------|
| `CreateTestMessage()` | CircuitBreakerTests.cs, RateLimitTests.cs, MiddlewareTests.cs, MiddlewarePipelineTests.cs |
| `CreateSuccessHandler()` | CircuitBreakerTests.cs, RateLimitTests.cs, MiddlewarePipelineTests.cs |
| `CreateFailureHandler()` | CircuitBreakerTests.cs, RateLimitTests.cs |

### Proposed Solution
Create a shared test utilities class:

```csharp
// Tests/TestRunner.Framework/TestUtilities.cs
namespace TestRunner.Framework;

public static class TestUtilities
{
    public static AgentMessage CreateTestMessage(
        string senderId = "test-sender",
        string subject = "Test Subject",
        string content = "Test content") { ... }

    public static MessageDelegate CreateSuccessHandler(string message = "Success") { ... }

    public static MessageDelegate CreateFailureHandler(string message = "Failure") { ... }
}
```

### Files Changed
- `Tests/TestRunner.Framework/TestUtilities.cs` (new)
- `Tests/AgentRouting.Tests/CircuitBreakerTests.cs`
- `Tests/AgentRouting.Tests/RateLimitTests.cs`
- `Tests/AgentRouting.Tests/MiddlewareTests.cs`
- `Tests/AgentRouting.Tests/MiddlewarePipelineTests.cs`

### Risk: LOW
- Test code only
- No production impact
- Improves test maintainability

---

## 3. Builder Pattern Methods

### Current State
`RuleBuilder<T>` and `CompositeRuleBuilder<T>` have identical methods:

```csharp
// RuleBuilder<T> lines 20-51
public RuleBuilder<T> WithId(string id) { _id = id; return this; }
public RuleBuilder<T> WithName(string name) { _name = name; return this; }
public RuleBuilder<T> WithDescription(string desc) { _description = desc; return this; }
public RuleBuilder<T> WithPriority(int priority) { _priority = priority; return this; }

// CompositeRuleBuilder<T> lines 189-211 - IDENTICAL pattern
```

### Proposed Solution
Create a base builder class:

```csharp
public abstract class RuleBuilderBase<TBuilder, TRule>
    where TBuilder : RuleBuilderBase<TBuilder, TRule>
{
    protected string? _id;
    protected string? _name;
    protected string? _description;
    protected int _priority;

    public TBuilder WithId(string id) { _id = id; return (TBuilder)this; }
    public TBuilder WithName(string name) { _name = name; return (TBuilder)this; }
    public TBuilder WithDescription(string desc) { _description = desc; return (TBuilder)this; }
    public TBuilder WithPriority(int priority) { _priority = priority; return (TBuilder)this; }

    public abstract TRule Build();
}
```

### Files Changed
- `RulesEngine/RulesEngine/Core/RuleBuilderBase.cs` (new)
- `RulesEngine/RulesEngine/Core/RuleBuilder.cs`
- `RulesEngine/RulesEngine/Core/CompositeRule.cs`

### Risk: MEDIUM
- Public API preserved (builders return same types)
- Inheritance adds complexity
- Requires careful testing

### Alternative: NO CHANGE
The duplication is only ~30 lines across 2 files. The cost of abstraction may outweigh the benefit. Consider skipping this consolidation.

---

## 4. TrackPerformance Method

### Current State
Identical implementation in two files:

- `RulesEngineCore.cs` lines 609-637
- `ThreadSafeRulesEngine.cs` lines 136-164

```csharp
private void TrackPerformance(string ruleId, TimeSpan duration)
{
    _metrics.AddOrUpdate(
        ruleId,
        _ => new RulePerformanceMetrics { ... },
        (_, existing) => { ... }
    );
}
```

### Proposed Solution
Extract to a static helper class:

```csharp
// RulesEngine/RulesEngine/Core/PerformanceTracker.cs
internal static class PerformanceTracker
{
    public static void Track(
        ConcurrentDictionary<string, RulePerformanceMetrics> metrics,
        string ruleId,
        TimeSpan duration) { ... }
}
```

### Files Changed
- `RulesEngine/RulesEngine/Core/PerformanceTracker.cs` (new)
- `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`
- `RulesEngine/RulesEngine/Enhanced/ThreadSafeRulesEngine.cs`

### Risk: LOW
- Internal helper only
- No API changes
- Simple extraction

---

## 5. ExecuteSequential/ExecuteParallel Methods

### Current State
Nearly identical implementations:

- `RulesEngineCore.cs` lines 502-546
- `ThreadSafeRulesEngine.cs` lines 90-134

### Proposed Solution

**Option A: Extract to shared helper**
```csharp
internal static class RuleExecutor
{
    public static void ExecuteSequential<T>(
        T fact,
        IEnumerable<IRule<T>> rules,
        RulesEngineResult result,
        RulesEngineOptions options,
        Action<string, TimeSpan>? trackPerformance) { ... }
}
```

**Option B: Base class with template method**
Both engines inherit from a common base that provides execution logic.

**Option C: NO CHANGE (RECOMMENDED)**
`ThreadSafeRulesEngine` uses an **immutable pattern** (`WithRule()` returns a new instance) while `RulesEngineCore` uses a mutable pattern with locking. These are fundamentally different designs:

- `RulesEngineCore`: Mutable, uses `ReaderWriterLockSlim`, modifies in-place
- `ThreadSafeRulesEngine`: Immutable, each modification returns new instance

The duplication is intentional - they serve different concurrency models. Consolidating would couple designs that should remain independent.

### Files Changed (if Option A - NOT RECOMMENDED)
- `RulesEngine/RulesEngine/Core/RuleExecutor.cs` (new)
- `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`
- `RulesEngine/RulesEngine/Enhanced/ThreadSafeRulesEngine.cs`

### Risk: MEDIUM
- Execution is core functionality
- Must preserve exact semantics
- Requires thorough testing

### Recommendation
Review `ThreadSafeRulesEngine` usage before deciding. If it's deprecated or rarely used, consider Option C (no change).

---

## 6. Middleware State Classes

### Current State
Three similar state classes in `CommonMiddleware.cs`:

```csharp
private class RateLimitState { public List<DateTime> Timestamps { get; } = new(); }
private class CacheState { public ConcurrentDictionary<string, CacheEntry> Cache { get; } = new(); }
private class CircuitBreakerState { ... more complex ... }
```

### Proposed Solution: NO CHANGE
These classes have different purposes and structures. The apparent similarity is superficial - they hold different data types for different middleware behaviors. Consolidating them would create unnecessary coupling.

### Risk: N/A

---

## Implementation Order

Recommended sequence (lowest risk first):

| Order | Item | Risk | Effort |
|-------|------|------|--------|
| 1 | Rule Validation Logic | LOW | 30 min |
| 2 | Test Helper Methods | LOW | 45 min |
| 3 | TrackPerformance Method | LOW | 30 min |
| 4 | Builder Pattern (evaluate) | MEDIUM | 1 hour |
| 5 | Execute Methods (evaluate) | MEDIUM | 1 hour |

---

## Verification Plan

After each change:

1. Build the solution: `dotnet build AgentRouting/AgentRouting.sln --no-restore`
2. Run all tests: `dotnet run --project Tests/TestRunner/ --no-build`
3. Run tests 3x for concurrency: verify no flaky tests
4. Run coverage to ensure no regression

---

## Decisions Needed

Before proceeding, please confirm:

1. **Builder consolidation**: Proceed with base class, or skip? (adds complexity for ~30 lines saved)
2. **Execute methods**: Consolidate, or keep separate? (need to verify ThreadSafeRulesEngine usage)
3. **Middleware state classes**: Confirmed skip? (different purposes)

---

## Summary

| Change | Lines Saved | Risk | Recommendation |
|--------|-------------|------|----------------|
| Rule Validation | ~12 | LOW | DO IT |
| Test Helpers | ~40 | LOW | DO IT |
| TrackPerformance | ~30 | LOW | DO IT |
| Builder Pattern | ~30 | MEDIUM | SKIP (complexity not worth it) |
| Execute Methods | ~50 | MEDIUM | SKIP (different concurrency models) |
| Middleware State | 0 | N/A | SKIP (not real duplication) |

**Total lines saved (recommended changes): ~82 lines**

---

## Approval Checklist

Please confirm before proceeding:

- [ ] Proceed with Rule Validation consolidation
- [ ] Proceed with Test Helpers consolidation
- [ ] Proceed with TrackPerformance consolidation
- [ ] Skip Builder Pattern (confirmed)
- [ ] Skip Execute Methods (confirmed - different designs)
- [ ] Skip Middleware State (confirmed)
