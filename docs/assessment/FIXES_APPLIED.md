# Fixes Applied

**Date:** 2026-02-08
**Branch:** claude/repo-assessment-review-ueLwR
**Tests before:** 2,254 passed | **Tests after:** 2,268 passed (14 new regression tests)

---

## Critical Bugs Fixed

### CR-01: ImmutableRulesEngine Shared Mutable Metrics (P0)
**File:** `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`
**Fix:** `WithRule()`, `WithRules()`, and `WithoutRule()` now create new `ConcurrentDictionary` copies instead of sharing the same reference. Each engine instance has isolated performance metrics.
**Tests:** `CR01_ImmutableEngine_WithRule_MetricsAreIsolated`, `CR01_ImmutableEngine_WithoutRule_MetricsAreIsolated`

### CR-04: Silent Message Loss on Unroutable Messages (P0)
**File:** `AgentRouting/AgentRouting/Core/AgentRouter.cs`
**Fix:** Added `OnUnroutableMessage` event that fires with the message and a descriptive reason when no agent can handle a message. Callers can subscribe to implement dead-letter queues or alerting.
**Tests:** `CR04_RouteMessage_NoAgentAvailable_FiresUnroutableEvent`, `CR04_RouteMessage_AgentAvailable_DoesNotFireUnroutableEvent`

### CR-05: ExecuteAsync Ignores MaxRulesToExecute for Async Rules (P1)
**File:** `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`
**Fix:** `ExecuteAsync()` now enforces `MaxRulesToExecute` as a combined limit across both sync and async rules. Previously only sync rules were limited.
**Tests:** `CR05_ExecuteAsync_MaxRulesToExecute_LimitsAsyncRules`

### CR-05b: ImmutableRulesEngine Missing MaxRulesToExecute (P1)
**File:** `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`
**Fix:** `ImmutableRulesEngine<T>.Execute()` now respects `RulesEngineOptions.MaxRulesToExecute`. Previously the immutable variant processed all rules regardless of this setting.
**Tests:** `CR05b_ImmutableEngine_MaxRulesToExecute_LimitsExecution`

### CR-06: ExecuteAsync Missing Performance Tracking (P1)
**File:** `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`
**Fix:** `ExecuteAsync()` now tracks per-rule execution time using `Stopwatch` when `TrackPerformance` is enabled. Both sync and async rules get metrics. Previously only synchronous `Execute()` tracked performance.
**Tests:** `CR06_ExecuteAsync_TrackPerformance_RecordsMetrics`

### CR-07: AsyncRuleBuilder Wrong Exception Type (P1)
**File:** `RulesEngine/RulesEngine/Core/AsyncRule.cs`
**Fix:** `AsyncRuleBuilder<T>.Build()` now throws `RuleValidationException` (consistent with `RuleBuilder<T>.Build()`) instead of `InvalidOperationException`.
**Tests:** `CR07_AsyncRuleBuilder_MissingId_ThrowsRuleValidationException` (and 3 more variants)

### CR-15: Disposal Exception Swallowing (P1)
**File:** `AgentRouting/AgentRouting/DependencyInjection/ServiceContainer.cs`
**Fix:** `ServiceContainer.Dispose()` and `ServiceScope.Dispose()` now collect exceptions from failing service disposals and throw `AggregateException` after all services have been disposed. Previously all disposal exceptions were silently swallowed.
**Tests:** `CR15_ServiceContainer_ThrowingDisposable_AggregatesExceptions`, `CR15_ServiceContainer_MixedDisposables_AllGetDisposed`, `CR15_ServiceContainer_NoThrowingDisposables_DisposesCleanly`

---

## Additional Fixes

### Async Void Test Methods (43 tests)
**Files:** `Tests/MafiaDemo.Tests/StorySystemIntegrationTests.cs`, `PlayerAgentCoverageTests.cs`, `PlayerAgentTests.cs`
**Fix:** Changed 43 `async void` test methods to `async Task`. The `async void` pattern causes unhandled exceptions that crash the test runner process if a test fails, preventing proper test reporting.

### Existing Test Updates
**Files:** `Tests/RulesEngine.Tests/AsyncRuleBuilderTests.cs`, `AsyncExecutionTests.cs`, `FoundationTests.cs`
**Fix:** Updated 9 existing tests that expected `InvalidOperationException` from `AsyncRuleBuilder.Build()` to expect `RuleValidationException` (reflecting the CR-07 fix).

---

## Documentation Fixes

### CLAUDE.md
- Updated test count from 1,862 to 2,268
- Fixed `RulesEngine.Linq/RulesEngine.Linq.sln` references to `RulesEngine.Linq/RulesEngine.Linq/` (the .sln file doesn't exist)

---

## Issues Already Fixed in Prior Sessions

The following issues from the code review were found to be already addressed:

| CR | Issue | Status |
|----|-------|--------|
| CR-02 | Async void timer callbacks | Already has try-catch wrappers |
| CR-03 | Agent capacity race condition | Already uses `TryAcquireSlot` with `Interlocked.CompareExchange` |
| CR-08 | SessionState thread-safety | Uses `ConcurrentDictionary` for fact sets |
| CR-09 | Unbounded compiled cache | `ClearSessionCache` called on session dispose |
| CR-11 | ServiceContainer singleton race | Uses proper double-checked locking |
| CR-12 | Agent registration thread-safety | Already has `lock (_agentLock)` |
| CR-13 | ABTestingMiddleware Random | Already uses `Random.Shared` |

---

## Remaining Issues (Not Fixed in This Session)

### P2 Medium
- CR-10: Code duplication between `RulesEngineCore<T>` and `ImmutableRulesEngine<T>` (200+ lines, refactoring risk)
- CR-22: ImmutableRulesEngine missing async support
- CR-23: RulesEngineCore lock contention on high-throughput reads
- CR-24: No per-agent timeout configuration
- String hardcoding in MafiaDemo (constants extraction)

### P3 Low
- Missing XML documentation on public APIs
- Inconsistent null checking patterns
- Magic numbers in middleware defaults
- Console.WriteLine in production code

### Pre-existing Test Issues
- ~25 tests use non-deterministic random seed hunting pattern
- `PlayerAgent_FailedMission_DoesNotRecordIntel` has a game logic assertion issue (expects 0 intel on failed mission, gets 1)

---

## Delta Review Fixes (2026-02-09)

**Branch:** claude/review-history-files-Qt637
**Tests before:** 2,268 passed | **Tests after:** 2,270 passed (2 new regression tests)

### CR-04b: OnUnroutableMessage Event Handler Exception Breaks Routing
**File:** `AgentRouting/AgentRouting/Core/AgentRouter.cs`
**Fix:** Wrapped `OnUnroutableMessage?.Invoke(message, reason)` in try-catch so subscriber exceptions cannot propagate through routing. Callers always receive `MessageResult.Fail` regardless of handler behavior.
**Tests:** `CR04_RouteMessage_ThrowingEventHandler_StillReturnsFailResult`

### CR-36: ImmutableRulesEngine Shares Mutable Options Reference
**File:** `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`
**Fix:** Private constructor now clones `RulesEngineOptions` instead of storing the reference. Each derived engine (`WithRule`/`WithRules`/`WithoutRule`) gets its own isolated options copy, matching the existing metrics isolation pattern.
**Tests:** `CR36_ImmutableEngine_WithRule_OptionsAreIsolated`
