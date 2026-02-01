# Architecture Decisions

This document tracks architectural discussions, decisions, and open questions for the MafiaAgentSystem.

> **Purpose:** Maintain continuity across sessions. Record the "why" behind decisions.

---

## Active Discussions

### 1. Circuit Breaker Time Window

**Status:** ✅ Completed (2026-02-01)

**Problem:** Circuit breaker accumulated ALL failures indefinitely. A success in Closed state did NOT reset the count. This caused circuits to open even with healthy services that had occasional transient errors.

**Decision:** Implement time-windowed failure counting (industry standard).

**Resolution:**
- Added `TimeSpan failureWindow` parameter (default: 60 seconds)
- Replaced `int _failureCount` with `Queue<DateTime> _failureTimestamps`
- Added `PruneOldFailures()` to remove expired timestamps
- Added `CurrentFailureCount` property for monitoring
- Added thread safety with `lock` for concurrent access
- Added `CircuitBreakerDefaultFailureWindow` to MiddlewareDefaults
- Added 3 new tests for time-window behavior

**Usage:**
```csharp
// "Open circuit after 5 failures within 30 seconds"
var circuitBreaker = new CircuitBreakerMiddleware(
    failureThreshold: 5,
    resetTimeout: TimeSpan.FromSeconds(60),
    failureWindow: TimeSpan.FromSeconds(30));
```

**Files changed:**
- `AgentRouting/Middleware/CommonMiddleware.cs` - CircuitBreakerMiddleware
- `AgentRouting/Configuration/MiddlewareDefaults.cs` - Added default
- `Tests/TestRunner/Tests/CircuitBreakerTests.cs` - 3 new tests

---

### 2. SystemClock Injection

**Status:** ✅ Completed (2026-02-01)

**Problem:** `SystemClock.Instance` is a static mutable singleton. This causes:
- Race conditions in parallel tests
- Global state pollution
- Difficulty testing time-dependent code

**Decision:** Refactor to constructor injection in key middleware.

**Resolution:**
- Added optional `ISystemClock? clock` parameter to 3 middleware:
  - `CircuitBreakerMiddleware`
  - `RateLimitMiddleware`
  - `CachingMiddleware`
- Default to `SystemClock.Instance` for backwards compatibility
- Replaced `DateTime.UtcNow` with `_clock.UtcNow` in those classes
- Kept `SystemClock.Instance` singleton for convenience (not removed)

**Usage (testable):**
```csharp
// Production - uses default clock
var circuitBreaker = new CircuitBreakerMiddleware(threshold: 5);

// Testing - inject fake clock
var fakeClock = new FakeClock(fixedTime);
var circuitBreaker = new CircuitBreakerMiddleware(
    failureThreshold: 5,
    clock: fakeClock);
```

**Files changed:**
- `AgentRouting/Middleware/CommonMiddleware.cs` - 3 middleware classes

---

### 3. Router Consolidation

**Status:** ✅ Completed (2026-02-01)

**Question:** Why do we have both `AgentRouter` and `MiddlewareAgentRouter`?

**Investigation:**
- MiddlewareAgentRouter extended AgentRouter using `new` keyword (code smell)
- AgentRouterWithMiddleware was also redundant
- Both provided middleware support that could be integrated into base class

**Resolution:**
- Added native middleware support to `AgentRouter` (UseMiddleware methods)
- Added `HasMiddleware` property to `MiddlewarePipeline`
- Updated all usages across codebase to use `AgentRouter` directly
- Deleted `MiddlewareAgentRouter` class
- Converted `AgentRouterWithMiddleware.cs` to only contain `AgentRouterBuilder`
- Added missing `CallbackMiddleware`, `ConditionalMiddleware`, and extension methods

---

### 4. Middleware State Interface

**Status:** 📋 Planned

**Problem:** `RateLimitMiddleware` and `CircuitBreakerMiddleware` store state in memory. This works for single-instance but not for distributed deployments.

**Decision:** Create `IStateStore` interface for future extensibility.

**Design:**
```csharp
public interface IStateStore
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<long> IncrementAsync(string key);
    Task RemoveAsync(string key);
}

// Default implementation
public class InMemoryStateStore : IStateStore { ... }

// Future: RedisStateStore, etc.
```

**Scope:** Local multi-threaded testing first. Distributed support as future enhancement.

---

### 5. RuleResult.Error Semantics

**Status:** 📋 Planned

**Problem:** When a rule's action throws, `RuleResult.Error()` sets `Matched = false`. This loses information about whether the rule actually matched.

**Current behavior:**
```csharp
// Rule matched, action threw
return RuleResult.Error(Id, Name, ex.Message);
// Matched = false, ActionExecuted = false
```

**Options:**
1. **Keep as-is:** Error = not matched (simpler, current behavior)
2. **Change semantics:** Matched = true, ActionExecuted = false, ErrorMessage set

**Decision:** Add comprehensive tests first to understand edge cases, then decide.

---

### 6. Rule<T> Typed Results

**Status:** 📋 Planned

**Problem:** Rules currently return `RuleResult` with a generic `Dictionary<string, object> Outputs`. For the MafiaGame, strongly typed results would be beneficial.

**Possible design:**
```csharp
public interface IRule<TFact, TResult>
{
    TResult Execute(TFact fact);
}

// Example usage
var rule = new Rule<GameState, AgentDecision>(...);
AgentDecision decision = rule.Execute(gameState);
```

**Considerations:**
- Backward compatibility with existing `IRule<T>`
- Memory overhead of generic type parameters
- Complexity vs. value trade-off

---

## Completed Decisions

### Documentation Consolidation (2026-02-01)

**Decision:** Consolidate stale documentation, preserve rationale in archives.

**Outcome:**
- Created `ORIGINS.md` for project history
- Archived stale reviews to `docs/archive/`
- Updated `CLAUDE.md` with documentation structure

---

### Test Framework (Historical)

**Decision:** Use custom zero-dependency test framework instead of xUnit/NUnit.

**Rationale:**
- Zero external dependencies constraint
- Full control over test execution
- Educational value in building from scratch

**Outcome:** 172 tests passing with custom framework.

---

## Open Questions

1. **Thread safety in CircuitBreaker:** Should we use `Interlocked` operations or a lock?

2. **Rate limiter window type:** Sliding window vs. fixed window?

3. **Rule priority ties:** What happens when two rules have the same priority?

4. **Agent offline behavior:** How should routing handle offline agents?

---

## References

- `ORIGINS.md` - Project history and design rationale
- `Tests/COVERAGE_REPORT.md` - Test coverage analysis
- `RulesEngine/ISSUES_AND_ENHANCEMENTS.md` - Historical issues with resolution status
