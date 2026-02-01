# Architecture Decisions

This document tracks architectural discussions, decisions, and open questions for the MafiaAgentSystem.

> **Purpose:** Maintain continuity across sessions. Record the "why" behind decisions.

---

## Active Discussions

### 1. Circuit Breaker Time Window

**Status:** 🔄 In Progress

**Problem:** Current circuit breaker accumulates ALL failures indefinitely. A success in Closed state does NOT reset the count. This causes circuits to open even with healthy services that have occasional transient errors.

**Decision:** Implement time-windowed failure counting (industry standard).

**Implementation Plan:**
- Add `TimeSpan failureWindow` parameter
- Track failures with timestamps
- Only count failures within the window
- Clean up old failure timestamps periodically

**Example:**
```csharp
// "Open circuit after 5 failures within 30 seconds"
var circuitBreaker = new CircuitBreakerMiddleware(
    failureThreshold: 5,
    failureWindow: TimeSpan.FromSeconds(30),
    resetTimeout: TimeSpan.FromSeconds(60));
```

---

### 2. SystemClock Injection

**Status:** 🔄 In Progress

**Problem:** `SystemClock.Instance` is a static mutable singleton. This causes:
- Race conditions in parallel tests
- Global state pollution
- Difficulty testing time-dependent code

**Decision:** Refactor to constructor injection.

**Implementation Plan:**
- Keep `ISystemClock` interface
- Inject via constructor where needed (CircuitBreaker, Cache, RateLimit)
- Provide default `SystemClock.Default` for convenience
- Remove static `Instance` property

**Before:**
```csharp
var now = SystemClock.Instance.UtcNow;
```

**After:**
```csharp
public CircuitBreakerMiddleware(ISystemClock? clock = null)
{
    _clock = clock ?? SystemClock.Default;
}
```

---

### 3. Router Consolidation

**Status:** 🔍 Investigating

**Question:** Why do we have both `AgentRouter` and `MiddlewareAgentRouter`?

**Investigation needed:**
- [ ] Compare the two classes
- [ ] Identify unique functionality in each
- [ ] Determine if consolidation is possible
- [ ] Check for breaking changes

**Outcome:** TBD after investigation

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
