# MafiaAgentSystem Task List

> **Generated**: 2026-01-31
> **Last Updated**: 2026-02-02 (Batch A: Foundation complete)
> **Approach**: Layered batches to minimize churn
> **Constraint**: All tasks are 2-4 hours, none exceeding 1 day

---

## Execution Philosophy: Layers, Not Categories

Previous organization grouped by *category* (thread safety, MafiaDemo, tests), which causes **churn**:
- Fixing app bugs on thread-unsafe code → revisit after core fixes
- Writing tests before test framework ready → retrofit later
- Documenting unstable features → docs become stale

**New approach**: Tasks organized by **what blocks what**. Complete each layer before moving up.

**Key insight**: Test Infrastructure has NO production code dependencies and benefits ALL other batches. Do it first.

```
┌─────────────────────────────────────────────────────────┐
│ Layer F: POLISH (last)                                  │
│   Documentation, code cleanup                           │
├─────────────────────────────────────────────────────────┤
│ Layer E: ENHANCEMENT                                    │
│   DI extensions, interface extraction, new tests        │
├─────────────────────────────────────────────────────────┤
│ Layer D: APPLICATION FIXES                              │
│   MafiaDemo gameplay bugs (foundation now solid)        │
├─────────────────────────────────────────────────────────┤
│ Layer B: RESOURCE STABILITY                             │
│   Memory leaks, unbounded growth, TOCTOU                │
├─────────────────────────────────────────────────────────┤
│ Layer A: FOUNDATION                                     │
│   Thread safety in core libraries                       │
├─────────────────────────────────────────────────────────┤
│ Layer C: TEST INFRASTRUCTURE (first)                    │
│   Setup/Teardown, state isolation - enables all testing │
└─────────────────────────────────────────────────────────┘
```

---

## Current Status

| Batch | Layer | Status | Tasks | Hours |
|-------|-------|--------|-------|-------|
| **C** | Test Infra | :white_check_mark: **COMPLETE** | 2 tasks | 5-7 |
| **A** | Foundation | :white_check_mark: **COMPLETE** | 4 tasks | 8-11 |
| **B** | Resources | :white_check_mark: **COMPLETE** | 3 tasks | 5-8 |
| **D** | App Fixes | :rocket: **START HERE** | 5 tasks | 10-14 |
| **E** | Enhancement | :hourglass: After D | 15 tasks | 35-47 |
| **F** | Polish | :hourglass: After D,E | 10 tasks | 20-28 |
| | | **TOTAL** | **39 tasks** | **83-115** |

### Completed (Reference)
- [x] **Batch B: Resource Stability** (2026-02-03)
  - B-1: CancellationTokenSource disposal in MafiaGameEngine
  - B-2: EventLog bounded to 1000 events with oldest-first eviction
  - B-3: Parallel execution priority order (verified already implemented)
- [x] **Batch A: Foundation (Thread Safety)** (2026-02-02)
  - A-1: CircuitBreakerMiddleware HalfOpen test gating
  - A-2: CachingMiddleware request coalescing for cache misses
  - A-3: AgentBase atomic capacity check with CompareExchange
  - A-4: AgentRouter double-checked locking for pipeline cache
- [x] **Batch C: Test Infrastructure** (2026-02-02)
  - C-1: Setup/Teardown support (SetUp, TearDown, OneTimeSetUp, OneTimeTearDown attributes)
  - C-2: Test state isolation (TestBase, AgentRoutingTestBase, MafiaTestBase)
- [x] P0-NEW-1 through P0-NEW-6 (Critical bugs from code review)
- [x] P0-TS-1: RateLimitMiddleware (verified fixed with lock pattern)
- [x] P0, P1, P2 original tasks (see EXECUTION_PLAN.md)

---

## Batch A: Foundation (Thread Safety) (COMPLETE)

> **Prerequisite**: Batch C complete (test infrastructure ready)
> **Unlocks**: Batches B, D
> **Why second**: All other code runs on these primitives. Now with proper test support.
> **Completed**: 2026-02-02

### Task A-1: Fix CircuitBreakerMiddleware State Machine Race :white_check_mark:
**Previously**: P0-TS-2
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs:411-493`

**Problem**: State transitions (Closed→Open→HalfOpen→Closed) have race conditions.

**Solution**: Added `HalfOpenTestInProgress` flag to ensure only ONE request tests recovery in HalfOpen state. Other requests fail fast while test is in progress.

**Subtasks**:
- [x] Use lock or state machine pattern for transitions
- [x] Ensure failure counting is atomic
- [x] Gate HalfOpen to single test request

---

### Task A-2: Fix CachingMiddleware TOCTOU :white_check_mark:
**Previously**: P0-TS-3
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs:179-247`

**Problem**: Time-of-check to time-of-use race between checking cache and adding entry.

**Solution**: Added `PendingRequests` dictionary to coalesce concurrent requests for same key. First request computes, others wait for result.

**Subtasks**:
- [x] Use request coalescing pattern with TaskCompletionSource
- [x] Ensure cache expiration checks are atomic
- [x] Duplicate requests now wait instead of redundant computation

---

### Task A-3: Fix AgentBase Capacity Check Race :white_check_mark:
**Previously**: P0-TS-4
**Estimated Time**: 2 hours
**File**: `AgentRouting/AgentRouting/Core/Agent.cs:159-202`

**Problem**: Capacity check and increment are not atomic.

**Solution**: Added `TryAcquireSlot()` method using `Interlocked.CompareExchange` loop to atomically check and increment.

**Subtasks**:
- [x] Use `Interlocked.CompareExchange` pattern
- [x] Semantic checks (category, status) before atomic slot acquisition

---

### Task A-4: Fix AgentRouter Cached Pipeline Double-Check :white_check_mark:
**Previously**: P0-TS-5
**Estimated Time**: 2 hours
**File**: `AgentRouting/AgentRouting/Core/AgentRouter.cs:18-19, 121-134`

**Problem**: Double-check locking pattern implemented incorrectly (missing volatile or lock).

**Solution**: Added `_pipelineLock` object and `volatile` keyword on `_builtPipeline`. Proper double-checked locking in `RouteMessageAsync`.

**Subtasks**:
- [x] Apply proper double-check locking pattern
- [x] Add volatile keyword for memory barrier
- [x] Use local variable to avoid multiple volatile reads

---

## Batch B: Resource Stability (COMPLETE)

> **Prerequisite**: Batch A complete
> **Unlocks**: Batch E
> **Why after A**: Resource issues compound with threading issues. Fix threading first.
> **Completed**: 2026-02-03

### Task B-1: Fix CancellationTokenSource Leaks :white_check_mark:
**Previously**: P2-FIX-1
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Game/GameEngine.cs:228, 256`

**Problem**: `CancellationTokenSource` created but never disposed.

**Solution**: Added try/finally in `StartGameAsync()` to dispose CTS after game loop. Updated `StopGame()` to dispose after cancellation.

**Subtasks**:
- [x] Add `Dispose()` call in `StopGame()` method
- [x] Wrap in try/finally in `StartGameAsync()`
- [x] Set `_cts = null` after disposal to prevent double-dispose

---

### Task B-2: Fix EventLog Unbounded Growth :white_check_mark:
**Previously**: P2-FIX-2
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Game/GameEngine.cs:22`

**Problem**: `EventLog` grows indefinitely during long games.

**Solution**: Added `MaxEventLogSize` constant (1000 events) and oldest-first eviction in `LogEvent()`.

**Subtasks**:
- [x] Add max capacity constant (1000 events)
- [x] Implement oldest-first eviction when full

---

### Task B-3: Fix Parallel Execution Priority Order :white_check_mark:
**Previously**: P0-TS-6
**Estimated Time**: 2 hours
**File**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs:543`

**Problem**: Parallel execution doesn't preserve rule priority order in results.

**Status**: Already implemented. Code stores results with original indices and sorts after execution. Tests exist and pass.

**Subtasks**:
- [x] Store results with original indices (already in code at line 539, 566)
- [x] Sort results by priority after parallel execution (already in code at line 592)
- [x] Test verifying result order matches priority (exists in `ThreadSafeEngineTests.cs:428, 1214`)

---

## Batch C: Test Infrastructure (COMPLETE)

> **Prerequisite**: None - has no production code dependencies
> **Unlocks**: ALL batches (better testing for everything)
> **Why first**: Setup/Teardown and state isolation benefit every test we write for A, B, D, E.
> **Completed**: 2026-02-02

### Task C-1: Add Setup/Teardown Support :white_check_mark:
**Previously**: P3-TF-1
**Estimated Time**: 3-4 hours
**Files**: `Tests/TestRunner.Framework/`, `Tests/TestRunner/`

**Problem**: No way to run initialization before tests or cleanup after.

**Subtasks**:
- [x] Add `[SetUp]` attribute for per-test initialization
- [x] Add `[TearDown]` attribute for per-test cleanup
- [x] Update TestRunner to discover and invoke setup/teardown
- [x] Add `[OneTimeSetUp]` and `[OneTimeTearDown]` for class-level
- [x] Add tests for the new attributes (`LifecycleAttributeTests.cs`)

---

### Task C-2: Add Test State Isolation :white_check_mark:
**Previously**: P3-TF-4
**Estimated Time**: 2-3 hours
**Files**: Multiple test files

**Problem**: `SystemClock.Instance`, `GameTimingOptions.Current` are global mutable state.

**Subtasks**:
- [x] Create test base class with state reset (`TestBase` in TestRunner.Framework)
- [x] Reset `SystemClock.Instance` to default after each test (`AgentRoutingTestBase`)
- [x] Reset `GameTimingOptions.Current` after each test (`MafiaTestBase`)
- [x] Audit other global state (only 2 global singletons found)
- [x] Use Setup/Teardown from C-1

---

## Batch D: Application Fixes (MafiaDemo)

> **Prerequisite**: Batch A complete (thread-safe core)
> **Unlocks**: Batch F (documentation)
> **Why after A**: Fixing gameplay on thread-unsafe core means potential re-fixes.

### Task D-1: Complete Agent Rule Actions
**Previously**: P2-FIX-3
**Estimated Time**: 3-4 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/GameRulesEngine.cs:422-481`

**Problem**: Agent rule actions have comments describing intent but no implementation.

**Subtasks**:
- [ ] Implement COLLECT action logic
- [ ] Implement EXPAND action logic
- [ ] Implement RECRUIT action logic
- [ ] Implement BRIBE action logic
- [ ] Add tests for each action type

---

### Task D-2: Fix Crew Recruitment Having No Effect
**Previously**: P2-FIX-4
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/MafiaAgents.cs:510, 516`

**Problem**: `RecruitSoldier` decision is made but crew members are never actually added.

**Subtasks**:
- [ ] Implement actual crew member addition
- [ ] Track crew in GameState
- [ ] Add test verifying recruitment works

---

### Task D-3: Balance Game Economy
**Previously**: P2-FIX-5
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

### Task D-4: Fix Trivial Test Assertions
**Previously**: P3-TF-2
**Estimated Time**: 2-3 hours
**File**: `Tests/MafiaDemo.Tests/AutonomousGameTests.cs`

**Problem**: Lines 168, 182, 734, 777, 820 have `Assert.True(true)` that test nothing.

**Subtasks**:
- [ ] Replace with meaningful assertions
- [ ] Verify actual behavior, not just "doesn't crash"
- [ ] Add proper state verification after operations

---

### Task D-5: Fix Documented Bug in RuleEdgeCaseTests
**Previously**: P3-TF-3
**Estimated Time**: 1-2 hours
**File**: `Tests/RulesEngine.Tests/RuleEdgeCaseTests.cs:467`

**Problem**: Test expects wrong result (0 instead of 1 for MatchedRules when action throws).

**Subtasks**:
- [ ] Investigate correct behavior
- [ ] Fix test expectation OR fix engine behavior
- [ ] Document the correct behavior

---

## Batch E: Enhancement

> **Prerequisite**: Batch C complete (test infrastructure ready)
> **Unlocks**: Batch F
> **Why after C**: New tests should use proper Setup/Teardown from the start.

### E-1: DI & IoC (2 tasks, 4-6 hours)

#### Task E-1a: Create Service Registration Extensions
**Previously**: P1-DI-6
**Estimated Time**: 2-3 hours

**Subtasks**:
- [ ] Create `AddAgentRouting()` extension for core services
- [ ] Create `AddMiddleware<T>()` generic registration
- [ ] Create `AddAgent<T>()` generic registration

---

#### Task E-1b: Update Demos to Use Container
**Previously**: P1-DI-7
**Estimated Time**: 2-3 hours

**Subtasks**:
- [ ] Update AgentRouting demo
- [ ] Update MiddlewareDemo
- [ ] Update MafiaDemo

---

### E-2: Interface Extraction (6 tasks, 12-16 hours)

All independent, can parallelize:

- [ ] **E-2a**: Extract IRulesEngineResult Interface (2 hours)
- [ ] **E-2b**: Extract IRuleExecutionResult<T> Interface (2 hours)
- [ ] **E-2c**: Extract ITraceSpan Interface (2 hours)
- [ ] **E-2d**: Extract IMiddlewareContext Interface (2 hours)
- [ ] **E-2e**: Extract IMetricsSnapshot/IAnalyticsReport Interfaces (2-3 hours)
- [ ] **E-2f**: Extract IWorkflowDefinition/IWorkflowStage Interfaces (2-3 hours)

---

### E-3: Additional Testing (7 tasks, 19-25 hours)

- [ ] **E-3a**: Add Edge Case Tests for Rules (3-4 hours)
- [ ] **E-3b**: Add Middleware Pipeline Tests (3-4 hours)
- [ ] **E-3c**: Add Rate Limiter Tests (2-3 hours)
- [ ] **E-3d**: Add Circuit Breaker Tests (2-3 hours)
- [ ] **E-3e**: Add Performance Benchmarks (3-4 hours)
- [ ] **E-3f**: Add Integration Tests for Agent Routing (3-4 hours)
- [ ] **E-3g**: Add Test Coverage Analysis (2-3 hours)

---

## Batch F: Polish

> **Prerequisite**: Batches D and E complete
> **Why last**: Document stable code, not moving targets.

### F-1: Documentation (8 tasks, 17-24 hours)

#### Task F-1a: Consolidate MafiaDemo Documentation
**NEW TASK**
**Estimated Time**: 2-3 hours
**Files**: 7 MafiaDemo markdown files

**Problem**: Significant overlap between:
- README.md, GAME_README.md, AI_CAREER_MODE_README.md
- RULES_ENGINE_DEEP_DIVE.md, RULES_ENGINE_SUMMARY.md (60% overlap)
- MIDDLEWARE_INTEGRATION.md (too brief at 89 lines)

**Subtasks**:
- [ ] Merge overlapping content
- [ ] Create: README.md (overview), PLAY_GUIDE.md (all modes), DESIGN.md (architecture)
- [ ] Archive or delete redundant files

---

#### Task F-1b: Update ARCHITECTURE.md Integration Status
**NEW TASK**
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/ARCHITECTURE.md`

**Problem**: "Needs Integration" section lists items that are actually complete.

**Subtasks**:
- [ ] Update integration status to match actual implementation
- [ ] Remove completed items or mark as done
- [ ] Add any new integration points discovered

---

#### Remaining Documentation Tasks (from original P4)

- [ ] **F-1c**: Update CLAUDE.md with New Patterns (2-3 hours)
- [ ] **F-1d**: Add XML Documentation to Public APIs (3-4 hours)
- [ ] **F-1e**: Create API Reference Documentation (3-4 hours)
- [ ] **F-1f**: Create MafiaDemo Player Guide (2-3 hours)
- [ ] **F-1g**: Clean Up Code Style and Warnings (2-3 hours)
- [ ] **F-1h**: Create Release Checklist (1-2 hours)

---

## Completed Tasks (Reference)

### P0-NEW: Critical Bugs (All Complete)
- [x] P0-NEW-1: Fix Parallel Execution Ignoring StopOnFirstMatch
- [x] P0-NEW-2: Fix Division by Zero in RuleAnalyzer
- [x] P0-NEW-3: Fix Timer Resource Leaks in Middleware
- [x] P0-NEW-4: Mission ID Collision (FALSE POSITIVE)
- [x] P0-NEW-5: GameEngine Race Conditions (DOCUMENTED - single-threaded design)
- [x] P0-NEW-6: Fix Random Seeding Predictability

### P0-TS-1: RateLimitMiddleware (Complete)
- [x] Verified fixed with proper `lock(state)` pattern
- [x] Check-then-act sequence is protected
- [x] No action needed

### Original P0, P1, P2 (All Complete)
See EXECUTION_PLAN.md for details.

---

## Quick Reference

### Dependency Graph
```
C (Test Infra) ──► A (Foundation) ──┬──► B (Resources) ──► E (Enhancement) ──┐
                                    │                                         │
                                    └──► D (App Fixes) ──────────────────────┴──► F (Polish)
```

### Batch Summary

| Order | Batch | Focus | Tasks | Hours | Key Deliverable |
|-------|-------|-------|-------|-------|-----------------|
| 1 | C | Test Infra | 2 | 5-7 | Setup/Teardown, isolation |
| 2 | A | Thread Safety | 4 | 8-11 | Concurrent-safe core libraries |
| 3 | B | Resources | 3 | 5-8 | No leaks, bounded collections |
| 4 | D | App Fixes | 5 | 10-14 | Working MafiaDemo gameplay |
| 5 | E | Enhancement | 15 | 35-47 | DI, interfaces, more tests |
| 6 | F | Polish | 10 | 20-28 | Clean docs, stable release |

### Critical Files by Batch

| Batch | Files |
|-------|-------|
| C | `TestRunner.Framework/`, `TestRunner/` |
| A | `CommonMiddleware.cs`, `Agent.cs`, `AgentRouter.cs` |
| B | `GameEngine.cs`, `RulesEngineCore.cs` |
| D | `GameRulesEngine.cs`, `MafiaAgents.cs`, test files |
| E | Various core library files |
| F | All markdown documentation |

---

**Last Updated**: 2026-02-02 (Batch A: Foundation complete)
