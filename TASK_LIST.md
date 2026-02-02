# MafiaAgentSystem Task List

> **Generated**: 2026-01-31
> **Last Updated**: 2026-02-02 (Reorganized by dependency layers)
> **Approach**: Layered batches to minimize churn
> **Constraint**: All tasks are 2-4 hours, none exceeding 1 day

---

## Execution Philosophy: Layers, Not Categories

Previous organization grouped by *category* (thread safety, MafiaDemo, tests), which causes **churn**:
- Fixing app bugs on thread-unsafe code → revisit after core fixes
- Writing tests before test framework ready → retrofit later
- Documenting unstable features → docs become stale

**New approach**: Tasks organized by **what blocks what**. Complete each layer before moving up.

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
│ Layer C: TEST INFRASTRUCTURE                            │
│   Setup/Teardown, state isolation                       │
├─────────────────────────────────────────────────────────┤
│ Layer B: RESOURCE STABILITY                             │
│   Memory leaks, unbounded growth, TOCTOU                │
├─────────────────────────────────────────────────────────┤
│ Layer A: FOUNDATION (first)                             │
│   Thread safety in core libraries                       │
└─────────────────────────────────────────────────────────┘
```

---

## Current Status

| Batch | Layer | Status | Tasks | Hours |
|-------|-------|--------|-------|-------|
| **A** | Foundation | :clock3: READY | 4 tasks | 8-11 |
| **B** | Resources | :hourglass: BLOCKED by A | 3 tasks | 5-8 |
| **C** | Test Infra | :hourglass: BLOCKED by B | 2 tasks | 5-7 |
| **D** | App Fixes | :hourglass: BLOCKED by A | 5 tasks | 10-14 |
| **E** | Enhancement | :hourglass: BLOCKED by C | 15 tasks | 35-47 |
| **F** | Polish | :hourglass: BLOCKED by D,E | 10 tasks | 20-28 |
| | | **TOTAL** | **39 tasks** | **83-115** |

### Completed (Reference)
- [x] P0-NEW-1 through P0-NEW-6 (Critical bugs from code review)
- [x] P0-TS-1: RateLimitMiddleware (verified fixed with lock pattern)
- [x] P0, P1, P2 original tasks (see EXECUTION_PLAN.md)

---

## Batch A: Foundation (Thread Safety)

> **Prerequisite**: None
> **Unlocks**: Batches B, D
> **Why first**: All other code runs on these primitives. Fixing bugs on thread-unsafe code wastes effort.

### Task A-1: Fix CircuitBreakerMiddleware State Machine Race
**Previously**: P0-TS-2
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs:411-435`

**Problem**: State transitions (Closed→Open→HalfOpen→Closed) have race conditions.

**Subtasks**:
- [ ] Use lock or state machine pattern for transitions
- [ ] Ensure failure counting is atomic
- [ ] Add tests for concurrent failures triggering state change

---

### Task A-2: Fix CachingMiddleware TOCTOU
**Previously**: P0-TS-3
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs:186-222`

**Problem**: Time-of-check to time-of-use race between checking cache and adding entry.

**Subtasks**:
- [ ] Use `ConcurrentDictionary.GetOrAdd` with factory lambda
- [ ] Ensure cache expiration checks are atomic
- [ ] Add concurrent cache access tests

---

### Task A-3: Fix AgentBase Capacity Check Race
**Previously**: P0-TS-4
**Estimated Time**: 2 hours
**File**: `AgentRouting/AgentRouting/Core/Agent.cs:159-194`

**Problem**: Capacity check and increment are not atomic.

**Subtasks**:
- [ ] Use `Interlocked.CompareExchange` pattern
- [ ] Add test for concurrent message handling at capacity

---

### Task A-4: Fix AgentRouter Cached Pipeline Double-Check
**Previously**: P0-TS-5
**Estimated Time**: 2 hours
**File**: `AgentRouting/AgentRouting/Core/AgentRouter.cs:43-44, 120-121`

**Problem**: Double-check locking pattern implemented incorrectly (missing volatile or lock).

**Subtasks**:
- [ ] Apply proper double-check locking pattern
- [ ] Or use `Lazy<T>` for pipeline initialization
- [ ] Add concurrent routing test

---

## Batch B: Resource Stability

> **Prerequisite**: Batch A complete
> **Unlocks**: Batch C
> **Why after A**: Resource issues compound with threading issues. Fix threading first.

### Task B-1: Fix CancellationTokenSource Leaks
**Previously**: P2-FIX-1
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Game/GameEngine.cs:228, 256`

**Problem**: `CancellationTokenSource` created but never disposed.

**Subtasks**:
- [ ] Add `Dispose()` call in `StopGame()` method
- [ ] Or wrap in `using` statements
- [ ] Verify no leaks in repeated start/stop cycles

---

### Task B-2: Fix EventLog Unbounded Growth
**Previously**: P2-FIX-2
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Game/GameEngine.cs:22`

**Problem**: `EventLog` grows indefinitely during long games.

**Subtasks**:
- [ ] Add max capacity constant (e.g., 1000 events)
- [ ] Implement oldest-first eviction when full
- [ ] Or use circular buffer pattern

---

### Task B-3: Fix Parallel Execution Priority Order
**Previously**: P0-TS-6
**Estimated Time**: 2 hours
**File**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs:543`

**Problem**: Parallel execution doesn't preserve rule priority order in results.

**Subtasks**:
- [ ] Store results with original indices
- [ ] Sort results by priority after parallel execution
- [ ] Add test verifying result order matches priority

---

## Batch C: Test Infrastructure

> **Prerequisite**: Batch B complete
> **Unlocks**: Batch E (new tests)
> **Why before more tests**: Writing tests without Setup/Teardown means retrofitting later.

### Task C-1: Add Setup/Teardown Support
**Previously**: P3-TF-1
**Estimated Time**: 3-4 hours
**Files**: `Tests/TestRunner.Framework/`, `Tests/TestRunner/`

**Problem**: No way to run initialization before tests or cleanup after.

**Subtasks**:
- [ ] Add `[SetUp]` attribute for per-test initialization
- [ ] Add `[TearDown]` attribute for per-test cleanup
- [ ] Update TestRunner to discover and invoke setup/teardown
- [ ] Add `[OneTimeSetUp]` and `[OneTimeTearDown]` for class-level
- [ ] Add tests for the new attributes

---

### Task C-2: Add Test State Isolation
**Previously**: P3-TF-4
**Estimated Time**: 2-3 hours
**Files**: Multiple test files

**Problem**: `SystemClock.Instance`, `GameTimingOptions.Current` are global mutable state.

**Subtasks**:
- [ ] Create test base class with state reset
- [ ] Reset `SystemClock.Instance` to default after each test
- [ ] Reset `GameTimingOptions.Current` after each test
- [ ] Audit other global state
- [ ] Use Setup/Teardown from C-1

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
A (Foundation) ──┬──► B (Resources) ──► C (Test Infra) ──► E (Enhancement) ──┐
                 │                                                            │
                 └──► D (App Fixes) ─────────────────────────────────────────┴──► F (Polish)
```

### Batch Summary

| Batch | Focus | Tasks | Hours | Key Deliverable |
|-------|-------|-------|-------|-----------------|
| A | Thread Safety | 4 | 8-11 | Concurrent-safe core libraries |
| B | Resources | 3 | 5-8 | No leaks, bounded collections |
| C | Test Infra | 2 | 5-7 | Setup/Teardown, isolation |
| D | App Fixes | 5 | 10-14 | Working MafiaDemo gameplay |
| E | Enhancement | 15 | 35-47 | DI, interfaces, more tests |
| F | Polish | 10 | 20-28 | Clean docs, stable release |

### Critical Files by Batch

| Batch | Files |
|-------|-------|
| A | `CommonMiddleware.cs`, `Agent.cs`, `AgentRouter.cs` |
| B | `GameEngine.cs`, `RulesEngineCore.cs` |
| C | `TestRunner.Framework/`, `TestRunner/` |
| D | `GameRulesEngine.cs`, `MafiaAgents.cs`, test files |
| E | Various core library files |
| F | All markdown documentation |

---

**Last Updated**: 2026-02-02 (Reorganized by dependency layers)
