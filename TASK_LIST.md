# MafiaAgentSystem Task List

> **Generated**: 2026-01-31
> **Last Updated**: 2026-02-02
> **Based on**: Comprehensive code review (2026-02-02)
> **Constraint**: All tasks are 2-4 hours, none exceeding 1 day

---

## Current Status (Updated 2026-02-02)

| Priority | Category | Status | Tasks |
|----------|----------|--------|-------|
| **P0-NEW** | **Critical Bugs (Code Review)** | :hourglass: **IN PROGRESS** | **2 remaining** (3 done, 1 false positive) |
| **P0-TS** | **Thread Safety Fixes** | :rotating_light: **NEW** | **6 tasks** |
| P0 | Critical Fixes (Original) | :white_check_mark: **COMPLETE** | 0 remaining |
| P1 | Core Library Improvements | :white_check_mark: **COMPLETE** | 0 remaining |
| P1-DI | Dependency Injection/IoC | :hourglass: **PARTIAL** | 2 remaining |
| P1-IF | Interface Extraction | :clock3: **PENDING** | 6 tasks |
| P2 | MafiaDemo Completion | :white_check_mark: **COMPLETE** | 0 remaining |
| **P2-FIX** | **MafiaDemo Bug Fixes** | :rotating_light: **NEW** | **5 tasks** |
| P3 | Testing & Quality | :hourglass: **PARTIAL** | 7 remaining |
| **P3-TF** | **Test Framework Fixes** | :rotating_light: **NEW** | **4 tasks** |
| P4 | Documentation & Polish | :clock3: **PENDING** | 6 tasks |

**Code Review Summary (2026-02-02)**:
- 6 critical bugs found requiring immediate attention
- 6 thread safety race conditions identified
- 5 MafiaDemo-specific bugs
- Test framework missing critical features
- Total new work: ~50-65 hours

---

## P0-NEW: Critical Bugs (Code Review Findings) :rotating_light:

These bugs cause incorrect behavior, crashes, or resource exhaustion.

### Task P0-NEW-1: Fix Parallel Execution Ignoring StopOnFirstMatch :white_check_mark: COMPLETE
**Severity**: CRITICAL
**Estimated Time**: 3-4 hours
**Actual Time**: ~2 hours
**File**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs:537-602`

**Problem**: When `EnableParallelExecution=true`, `StopOnFirstMatch` was completely ignored.

**Solution Implemented**:
- Used `CancellationTokenSource` with `ParallelLoopState.Stop()` for early termination
- Added `Execute(T fact, CancellationToken cancellationToken)` overload to expose cancellation
- Fixed both `RulesEngineCore<T>` and `ImmutableRulesEngine<T>`
- Results now maintain priority order even with parallel execution
- Updated `IRulesEngine<T>` interface with new overload

**Subtasks**:
- [x] Analyze current parallel execution implementation
- [x] Implement early termination using CancellationToken + ParallelLoopState.Stop()
- [x] Add 9 new tests for parallel + StopOnFirstMatch + cancellation
- [x] Fix same issue in ImmutableRulesEngine<T>

**Tests Added**:
- `Execute_ParallelExecution_WithStopOnFirstMatch_ReturnsOnlyFirstMatch`
- `Execute_ParallelExecution_WithCancellation_ThrowsOperationCanceled`
- `Execute_SequentialExecution_WithCancellation_ThrowsOperationCanceled`
- `Execute_ParallelExecution_PreservesPriorityOrder`
- `Execute_ParallelWithStopOnFirstMatch_ManyRules_OnlyFirstReturned` (and more)

**Acceptance Criteria**: :white_check_mark: All met
- StopOnFirstMatch works correctly with parallel execution
- CancellationToken exposed for external cancellation
- Results maintain priority order
- 1781 tests pass (9 new)

---

### Task P0-NEW-2: Fix Division by Zero in RuleAnalyzer :white_check_mark: COMPLETE
**Severity**: CRITICAL
**Estimated Time**: 1-2 hours
**Actual Time**: ~30 minutes
**File**: `RulesEngine/RulesEngine/Enhanced/RuleValidation.cs:324`

**Problem**:
```csharp
analysis.MatchRate = (double)matchedCases.Count / _testCases.Count;
```
If `_testCases` is empty, throws `DivideByZeroException`.

**Solution Implemented**:
- Added guard for empty `_testCases` in `AnalyzeRule()` - returns 0.0 match rate
- Added guard in `DetectOverlaps()` - skips overlap detection
- Added guard in `DetectDeadRules()` - skips dead rule detection
- All three methods now handle empty test cases gracefully

**Subtasks**:
- [x] Add guard for empty `_testCases` in AnalyzeRule
- [x] Add guard in DetectOverlaps (also had division)
- [x] Add guard in DetectDeadRules (incorrect behavior with empty list)
- [x] Add 3 unit tests for empty test cases scenarios

**Tests Added**:
- `Analyzer_EmptyTestCases_ReturnsZeroMatchRate_NoDivisionByZero`
- `Analyzer_EmptyTestCases_SkipsOverlapDetection`
- `Analyzer_EmptyTestCases_EmptyDeadRules`

**Acceptance Criteria**: :white_check_mark: All met
- No exception when `_testCases` is empty
- Returns 0.0 match rate (not NaN)
- 1790 tests pass (3 new)

---

### Task P0-NEW-3: Fix Timer Resource Leaks in Middleware :white_check_mark: COMPLETE
**Severity**: CRITICAL
**Estimated Time**: 2-3 hours
**Actual Time**: ~30 minutes
**Files**:
- `AgentRouting/AgentRouting/Middleware/AdvancedMiddleware.cs:291-323` (MessageQueueMiddleware)
- `AgentRouting/AgentRouting/Middleware/AdvancedMiddleware.cs:470-497` (AgentHealthCheckMiddleware)

**Problem**: `Timer` objects created but never disposed. Repeated instantiation causes thread pool exhaustion.

**Solution Implemented**:
- Implemented `IDisposable` on `MessageQueueMiddleware` with proper dispose pattern
- Implemented `IDisposable` on `AgentHealthCheckMiddleware` with proper dispose pattern
- Both use standard dispose pattern with `_disposed` flag for idempotency
- Timers properly cleaned up in `Dispose(bool disposing)` method

**Subtasks**:
- [x] Implement `IDisposable` on `MessageQueueMiddleware`
- [x] Implement `IDisposable` on `AgentHealthCheckMiddleware`
- [x] Add timer disposal in Dispose methods
- [x] Add 6 tests verifying disposal works correctly
- [ ] ~~Update `MiddlewarePipeline` to dispose middleware if disposable~~ (deferred - would require pipeline changes)

**Tests Added**:
- `MessageQueueMiddleware_Dispose_StopsTimer`
- `MessageQueueMiddleware_ImplementsIDisposable`
- `MessageQueueMiddleware_UsingStatement_DisposesCorrectly`
- `AgentHealthCheckMiddleware_Dispose_StopsTimer`
- `AgentHealthCheckMiddleware_ImplementsIDisposable`
- `AgentHealthCheckMiddleware_UsingStatement_DisposesCorrectly`

**Acceptance Criteria**: :white_check_mark: All met
- Both middleware implement `IDisposable`
- Timers are properly cleaned up
- 1787 tests pass (6 new)

---

### Task P0-NEW-4: Fix Mission ID Collision in MafiaDemo :white_check_mark: NOT A BUG
**Severity**: ~~CRITICAL~~ FALSE POSITIVE
**Estimated Time**: N/A (verification only)
**File**: `AgentRouting/AgentRouting.MafiaDemo/MissionSystem.cs:25`

**Original Concern**:
```csharp
public string Id { get; set; } = Guid.NewGuid().ToString();
```
Believed to generate GUID at class definition time.

**Verification Result**: **FALSE POSITIVE**
- C# property initializers ARE evaluated at instance creation time, NOT class definition time
- Each `new Mission()` creates a new instance with a unique GUID
- Existing test `Mission_GeneratesUniqueIds` passes, confirming correct behavior
- No fix needed

**Evidence**:
- Test at `Tests/MafiaDemo.Tests/MissionSystemTests.cs:161-170` passes
- Three Mission instances verified to have different IDs

---

### Task P0-NEW-5: Fix Race Conditions in GameEngine State
**Severity**: CRITICAL
**Estimated Time**: 3-4 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Game/GameEngine.cs:323-366`

**Problem**: Multiple async operations modify `_state` concurrently without synchronization:
- `ProcessWeeklyCollections()` modifies `FamilyWealth`
- `ProcessAutonomousActions()` modifies `FamilyWealth`
- `ProcessRivalFamilyActions()` modifies `FamilyWealth`

**Subtasks**:
- [ ] Add `SemaphoreSlim` or lock for state modifications
- [ ] Audit all state modification points
- [ ] Consider making GameState immutable with updates returning new state
- [ ] Add concurrent access tests

**Acceptance Criteria**:
- No race conditions in state modifications
- Tests verify thread safety

---

### Task P0-NEW-6: Fix Random Seeding Predictability
**Severity**: HIGH
**Estimated Time**: 2-3 hours
**Files**:
- `AgentRouting/AgentRouting.MafiaDemo/Autonomous/MafiaAgents.cs:77`
- `AgentRouting/AgentRouting.MafiaDemo/AI/PlayerAgent.cs:72`
- `AgentRouting/AgentRouting.MafiaDemo/Missions/MissionSystem.cs:143`

**Problem**: Multiple `Random` instances created in quick succession have identical or similar seeds, making agent "decisions" predictable and correlated.

**Solution**: Use `Random.Shared` (.NET 6+) or inject a shared `Random` instance.

**Subtasks**:
- [ ] Replace `new Random()` with `Random.Shared`
- [ ] Or create `IRandomProvider` for testability
- [ ] Verify decisions are properly randomized
- [ ] Add test with seeded random for deterministic testing

**Acceptance Criteria**:
- Decisions are properly randomized
- Testable with seeded random

---

## P0-TS: Thread Safety Fixes :rotating_light:

Race conditions that cause incorrect behavior under concurrent access.

### Task P0-TS-1: Fix RateLimitMiddleware Race Condition
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs:104-138`

**Problem**: Check-then-act on request count is not atomic.

**Subtasks**:
- [ ] Use `Interlocked` operations or lock for counter updates
- [ ] Ensure window expiration is thread-safe
- [ ] Add concurrent access stress test

---

### Task P0-TS-2: Fix CircuitBreakerMiddleware State Machine Race
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs:411-435`

**Problem**: State transitions (Closed→Open→HalfOpen→Closed) have race conditions.

**Subtasks**:
- [ ] Use lock or state machine pattern for transitions
- [ ] Ensure failure counting is atomic
- [ ] Add tests for concurrent failures triggering state change

---

### Task P0-TS-3: Fix CachingMiddleware TOCTOU
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs:186-222`

**Problem**: Time-of-check to time-of-use race between checking cache and adding entry.

**Subtasks**:
- [ ] Use `ConcurrentDictionary.GetOrAdd` pattern
- [ ] Ensure cache expiration checks are atomic
- [ ] Add concurrent cache access tests

---

### Task P0-TS-4: Fix AgentBase Capacity Check Race
**Estimated Time**: 2 hours
**File**: `AgentRouting/AgentRouting/Core/Agent.cs:159-194`

**Problem**: Capacity check and increment are not atomic.

**Subtasks**:
- [ ] Use `Interlocked.CompareExchange` pattern
- [ ] Add test for concurrent message handling at capacity

---

### Task P0-TS-5: Fix AgentRouter Cached Pipeline Double-Check
**Estimated Time**: 2 hours
**File**: `AgentRouting/AgentRouting/Core/AgentRouter.cs:43-44, 120-121`

**Problem**: Double-check locking pattern implemented incorrectly (missing volatile or lock).

**Subtasks**:
- [ ] Apply proper double-check locking pattern
- [ ] Or use `Lazy<T>` for pipeline initialization
- [ ] Add concurrent routing test

---

### Task P0-TS-6: Fix Parallel Execution Losing Priority Order
**Estimated Time**: 2 hours
**File**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs:543`

**Problem**: Parallel execution doesn't preserve rule priority order in results.

**Subtasks**:
- [ ] Store results with original indices
- [ ] Sort results by priority after parallel execution
- [ ] Add test verifying result order matches priority

---

## P2-FIX: MafiaDemo Bug Fixes :rotating_light:

Issues specific to the MafiaDemo game implementation.

### Task P2-FIX-1: Fix CancellationTokenSource Leaks
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Game/GameEngine.cs:222, 250`

**Problem**: `CancellationTokenSource` created but never disposed.

**Subtasks**:
- [ ] Wrap in `using` statements
- [ ] Or track and dispose in game cleanup

---

### Task P2-FIX-2: Fix EventLog Unbounded Growth
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Game/GameEngine.cs:22`

**Problem**: `EventLog` grows indefinitely during long games.

**Subtasks**:
- [ ] Add max capacity with oldest-first eviction
- [ ] Or add periodic cleanup
- [ ] Consider circular buffer pattern

---

### Task P2-FIX-3: Complete Agent Rule Actions
**Estimated Time**: 3-4 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Rules/GameRulesEngine.cs:422-481`

**Problem**: Agent rule actions have comments describing intent but no implementation.

**Subtasks**:
- [ ] Implement COLLECT action logic
- [ ] Implement EXPAND action logic
- [ ] Implement RECRUIT action logic
- [ ] Implement BRIBE action logic
- [ ] Add tests for each action type

---

### Task P2-FIX-4: Fix Crew Recruitment Having No Effect
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Autonomous/MafiaAgents.cs:510, 516`

**Problem**: `RecruitSoldier` decision is made but crew members are never actually added.

**Subtasks**:
- [ ] Implement actual crew member addition
- [ ] Track crew in GameState
- [ ] Add test verifying recruitment works

---

### Task P2-FIX-5: Balance Game Economy
**Estimated Time**: 2-3 hours
**Files**: Various MafiaDemo files

**Problems**:
- Hit missions pay 25x more than collections ($5000 vs $200)
- Heat decay too slow (10 weeks to recover from single hit)
- Promotion thresholds leave only 5-point buffer at max

**Subtasks**:
- [ ] Balance mission rewards
- [ ] Adjust heat decay rate
- [ ] Review and adjust promotion thresholds
- [ ] Document expected progression curve

---

## P3-TF: Test Framework Fixes :rotating_light:

Critical gaps in the test framework.

### Task P3-TF-1: Add Setup/Teardown Support
**Estimated Time**: 3-4 hours
**File**: `Tests/TestRunner.Framework/TestAttribute.cs`, `Tests/TestRunner/TestRunner.cs`

**Problem**: No way to run initialization before tests or cleanup after, causing test isolation issues.

**Subtasks**:
- [ ] Add `[SetUp]` attribute for per-test initialization
- [ ] Add `[TearDown]` attribute for per-test cleanup
- [ ] Update TestRunner to discover and invoke setup/teardown
- [ ] Add `[OneTimeSetUp]` and `[OneTimeTearDown]` for class-level
- [ ] Add tests for the new attributes

**Acceptance Criteria**:
- Setup runs before each test
- Teardown runs after each test (even on failure)
- Class-level variants work correctly

---

### Task P3-TF-2: Fix Trivial Assertions in MafiaDemo Tests
**Estimated Time**: 2-3 hours
**File**: `Tests/MafiaDemo.Tests/AutonomousGameTests.cs`

**Problem**: Lines 168, 182, 734, 777, 820 have `Assert.True(true)` that test nothing.

**Subtasks**:
- [ ] Replace with meaningful assertions
- [ ] Verify actual behavior, not just "doesn't crash"
- [ ] Add proper state verification after operations

---

### Task P3-TF-3: Fix Documented Bug in RuleEdgeCaseTests
**Estimated Time**: 1-2 hours
**File**: `Tests/RulesEngine.Tests/RuleEdgeCaseTests.cs:467`

**Problem**: Test expects wrong result (0 instead of 1 for MatchedRules when action throws).

**Subtasks**:
- [ ] Investigate correct behavior
- [ ] Fix test expectation OR fix engine behavior
- [ ] Document the correct behavior

---

### Task P3-TF-4: Add Test State Isolation
**Estimated Time**: 2-3 hours
**Files**: Multiple test files

**Problem**: `SystemClock.Instance`, `GameTimingOptions.Current` are global mutable state shared across tests.

**Subtasks**:
- [ ] Create test base class with state reset in setup/teardown
- [ ] Reset `SystemClock.Instance` to default after each test
- [ ] Reset `GameTimingOptions.Current` after each test
- [ ] Audit other global state
- [ ] Use new setup/teardown from P3-TF-1

---

## P1: Core Library Improvements :white_check_mark: COMPLETE

> All P1 tasks completed in previous phases. See EXECUTION_PLAN.md for details.

---

## P1-DI: Dependency Injection & IoC (Partial)

### Completed:
- [x] P1-DI-1: ServiceContainer (37 tests)
- [x] P1-DI-2: IMiddlewarePipeline interface
- [x] P1-DI-3: IRulesEngine interface
- [x] P1-DI-4: AgentRouter DI refactoring
- [x] P1-DI-5: Middleware constructor standardization

### Remaining:

### Task P1-DI-6: Create Service Registration Extensions
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting/DependencyInjection/ServiceExtensions.cs` (new)

**Subtasks**:
- [ ] Create `AddAgentRouting()` extension for core services
- [ ] Create `AddMiddleware<T>()` generic registration
- [ ] Create `AddAgent<T>()` generic registration
- [ ] Register defaults: `ISystemClock`, `IStateStore`, `IAgentLogger`

---

### Task P1-DI-7: Update Demos to Use Container
**Estimated Time**: 2-3 hours
**Files**: Demo Program.cs files

**Subtasks**:
- [ ] Update AgentRouting demo
- [ ] Update MiddlewareDemo
- [ ] Update MafiaDemo
- [ ] Verify all demos run correctly

---

## P1-IF: Interface Extraction :clock3: PENDING

> 6 tasks, 12-16 hours total. All independent, can parallelize.

### Task P1-IF-1: Extract IRulesEngineResult Interface
**Estimated Time**: 2 hours
**Files**: `RulesEngine/RulesEngine/Core/`

---

### Task P1-IF-2: Extract IRuleExecutionResult<T> Interface
**Estimated Time**: 2 hours
**Files**: `RulesEngine/RulesEngine/Core/`

---

### Task P1-IF-3: Extract ITraceSpan Interface
**Estimated Time**: 2 hours
**Files**: `AgentRouting/AgentRouting/Middleware/`

---

### Task P1-IF-4: Extract IMiddlewareContext Interface
**Estimated Time**: 2 hours
**Files**: `AgentRouting/AgentRouting/Middleware/`

---

### Task P1-IF-5: Extract IMetricsSnapshot and IAnalyticsReport Interfaces
**Estimated Time**: 2-3 hours
**Files**: `AgentRouting/AgentRouting/Middleware/`

---

### Task P1-IF-6: Extract IWorkflowDefinition and IWorkflowStage Interfaces
**Estimated Time**: 2-3 hours
**Files**: `AgentRouting/AgentRouting/Middleware/`

---

## P3: Testing & Quality (Remaining)

### Completed:
- [x] P3-1: Concurrency tests
- [x] Async execution tests
- [x] Validation and cache tests
- [x] MafiaDemo integration tests

### Remaining:

### Task P3-2: Add Edge Case Tests for Rules
**Estimated Time**: 3-4 hours

---

### Task P3-3: Add Middleware Pipeline Tests
**Estimated Time**: 3-4 hours

---

### Task P3-4: Add Rate Limiter Tests
**Estimated Time**: 2-3 hours

---

### Task P3-5: Add Circuit Breaker Tests
**Estimated Time**: 2-3 hours

---

### Task P3-6: Add Performance Benchmarks
**Estimated Time**: 3-4 hours

---

### Task P3-7: Add Integration Tests for Agent Routing
**Estimated Time**: 3-4 hours

---

### Task P3-8: Add Test Coverage Analysis
**Estimated Time**: 2-3 hours

---

## P4: Documentation & Polish :clock3: PENDING

> 6 tasks. See original TASK_LIST.md entries (unchanged).

- [ ] P4-1: Update CLAUDE.md with New Patterns
- [ ] P4-2: Add XML Documentation to Public APIs
- [ ] P4-3: Create API Reference Documentation
- [ ] P4-4: Create MafiaDemo Player Guide
- [ ] P4-5: Clean Up Code Style and Warnings
- [ ] P4-6: Create Release Checklist

---

## Priority Execution Order

### Immediate (Block Everything Else)
1. **P0-NEW-1**: Parallel execution + StopOnFirstMatch (API contract violation)
2. **P0-NEW-3**: Timer leaks (resource exhaustion)
3. **P0-NEW-4**: Mission ID collision (data corruption)
4. **P0-NEW-5**: GameEngine race conditions

### High Priority (This Week)
5. **P0-NEW-2**: Division by zero
6. **P0-NEW-6**: Random seeding
7. **P0-TS-1 through P0-TS-6**: Thread safety fixes
8. **P3-TF-1**: Setup/Teardown support

### Medium Priority (Next Sprint)
9. **P2-FIX-1 through P2-FIX-5**: MafiaDemo fixes
10. **P3-TF-2 through P3-TF-4**: Test improvements
11. **P1-DI-6, P1-DI-7**: Complete DI work

### Lower Priority (Backlog)
12. **P1-IF-1 through P1-IF-6**: Interface extraction
13. **P3-2 through P3-8**: Additional testing
14. **P4-1 through P4-6**: Documentation

---

## Estimates Summary

| Category | Tasks | Hours |
|----------|-------|-------|
| P0-NEW Critical Bugs | 6 | 13-18 |
| P0-TS Thread Safety | 6 | 12-16 |
| P2-FIX MafiaDemo | 5 | 10-14 |
| P3-TF Test Framework | 4 | 8-12 |
| P1-DI Remaining | 2 | 4-6 |
| P1-IF Interface Extraction | 6 | 12-16 |
| P3 Testing Remaining | 7 | 19-25 |
| P4 Documentation | 6 | 14-19 |
| **Total New/Remaining** | **42** | **92-126** |

---

## Quick Reference: Critical Files

| File | Issues |
|------|--------|
| `RulesEngineCore.cs` | Parallel+StopOnFirstMatch, priority order |
| `RuleValidation.cs` | Division by zero |
| `AdvancedMiddleware.cs` | Timer leaks (2 locations) |
| `CommonMiddleware.cs` | 3 race conditions |
| `Agent.cs` | Capacity race |
| `AgentRouter.cs` | Double-check locking |
| `GameEngine.cs` | Race conditions, CTS leaks, EventLog growth |
| `MissionSystem.cs` | ID collision |
| `MafiaAgents.cs` | Random seeding, recruitment no-op |
| `GameRulesEngine.cs` | Empty action implementations |
| `AutonomousGameTests.cs` | Trivial assertions |
| `RuleEdgeCaseTests.cs` | Wrong expected value |

---

**Last Updated**: 2026-02-02 (Post Code Review)
