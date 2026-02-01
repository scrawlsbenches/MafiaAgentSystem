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

**Status:** ✅ Completed (2026-02-01)

**Problem:** `RateLimitMiddleware`, `CachingMiddleware`, and `CircuitBreakerMiddleware` stored state in private fields. This works for single-instance but not for distributed deployments, and makes state management inconsistent across middleware.

**Decision:** Create `IStateStore` interface and require it via constructor injection.

**Resolution:**
- Created `IStateStore` interface in `AgentRouting/Infrastructure/StateStore.cs`
- Created `InMemoryStateStore` implementation using `ConcurrentDictionary`
- Updated 3 middleware to require `IStateStore` as first constructor parameter:
  - `RateLimitMiddleware` - uses `GetOrAdd` for per-sender rate limit state
  - `CachingMiddleware` - uses `GetOrAdd` for global cache state
  - `CircuitBreakerMiddleware` - uses `GetOrAdd` for circuit breaker state
- All middleware state classes moved to private nested classes
- Updated all test files to provide `InMemoryStateStore` instances

**Interface:**
```csharp
public interface IStateStore
{
    T? Get<T>(string key);
    void Set<T>(string key, T value);
    bool TryGet<T>(string key, out T? value);
    bool Remove(string key);
    T GetOrAdd<T>(string key, Func<string, T> factory);
    void Clear();
}
```

**Usage:**
```csharp
// Create shared state store
var stateStore = new InMemoryStateStore();

// Pass to middleware
var rateLimiter = new RateLimitMiddleware(stateStore, maxRequests: 100, window: TimeSpan.FromMinutes(1), SystemClock.Instance);
var circuitBreaker = new CircuitBreakerMiddleware(stateStore, failureThreshold: 5);
var cache = new CachingMiddleware(stateStore, TimeSpan.FromMinutes(5));
```

**Files changed:**
- `AgentRouting/Infrastructure/StateStore.cs` - New interface and implementation
- `AgentRouting/Middleware/CommonMiddleware.cs` - Updated 3 middleware classes
- `Tests/TestRunner/Tests/*.cs` - Updated all test instantiations

**Future:** `RedisStateStore`, `DistributedStateStore`, etc. can be created without changing middleware code.

---

### 5. RuleResult.Error Semantics

**Status:** ✅ Documented (2026-02-01)

**Problem:** When a rule's action throws, different rule implementations behave differently:
- `ActionRule<T>` (from `AddRule()`): Correctly sets `Matched = true`
- `Rule<T>` (from `RuleBuilder`): Uses `RuleResult.Error()` which sets `Matched = false`

This inconsistency means you lose information about whether the condition matched when using `Rule<T>`.

**Discovery:** Comprehensive tests were added that reveal the inconsistency:
- `RuleResult_Error_ActionThrows_AfterMatchingCondition_SetsMatchedFalse` - Documents `Rule<T>` behavior
- `RuleResult_Error_ActionRuleVsRuleT_InconsistentBehavior` - Shows the difference between the two

**Current behavior comparison:**
```csharp
// ActionRule (AddRule) - CORRECT behavior:
catch (Exception ex)
{
    return new RuleResult { Matched = true, ActionExecuted = false, ErrorMessage = ex.Message };
}

// Rule<T> (RuleBuilder) - LOSES match information:
catch (Exception ex)
{
    return RuleResult.Error(Id, Name, ex.Message);  // Matched = false
}
```

**Resolution options:**
1. **Fix Rule<T>:** Change `Rule<T>.Execute()` to not use `RuleResult.Error()`, instead inline similar logic to `ActionRule`
2. **Keep as-is:** Document the inconsistency, prefer `AddRule()` for rules that might throw
3. **Add RuleResult.MatchedWithError:** New factory method that sets `Matched = true` with error

**Decision:** For now, document the behavior with tests (9 new tests added). Future work may fix the inconsistency.

**Files added/changed:**
- `Tests/TestRunner/Tests/RuleEdgeCaseTests.cs` - 9 new tests in `RuleResult.Error Edge Cases` region

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
