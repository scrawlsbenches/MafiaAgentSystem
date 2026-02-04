# MafiaAgentSystem Task List

> **Generated**: 2026-01-31
> **Last Updated**: 2026-02-04 (F-3 runtime bugs fixed, Story System bugs fixed)
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
│ Layer J: CRITICAL BUG FIXES             ⚠️ NEEDS FIX   │
│   Thread-safety regressions, memory leaks, logic errors │
├─────────────────────────────────────────────────────────┤
│ Layer I: STORY SYSTEM INTEGRATION        ✅ COMPLETE    │
│   GameState↔WorldState sync, NPC relationships, plots   │
├─────────────────────────────────────────────────────────┤
│ Layer H: CODE REVIEW BUG FIXES          ✅ COMPLETE    │
│   Heat balance, event timing, null safety, defeat logic │
├─────────────────────────────────────────────────────────┤
│ Layer G: CRITICAL INTEGRATION            ✅ COMPLETE    │
│   AgentRouter integration, 47 personality rules         │
├─────────────────────────────────────────────────────────┤
│ Layer E: ENHANCEMENT                     ✅ COMPLETE    │
│   DI extensions, interface extraction, new tests        │
├─────────────────────────────────────────────────────────┤
│ Layer D: APPLICATION FIXES               ✅ COMPLETE    │
│   MafiaDemo gameplay bugs (foundation now solid)        │
├─────────────────────────────────────────────────────────┤
│ Layer B: RESOURCE STABILITY              ✅ COMPLETE    │
│   Memory leaks, unbounded growth, TOCTOU                │
├─────────────────────────────────────────────────────────┤
│ Layer A: FOUNDATION                      ✅ COMPLETE    │
│   Thread safety in core libraries                       │
├─────────────────────────────────────────────────────────┤
│ Layer C: TEST INFRASTRUCTURE             ✅ COMPLETE    │
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
| **D** | App Fixes | :white_check_mark: **COMPLETE** | 5 tasks | 10-14 |
| **E** | Enhancement | :white_check_mark: **COMPLETE** | 15 tasks | 35-47 |
| **G** | Critical Integration | :white_check_mark: **COMPLETE** | 5 tasks | 11-16 |
| **H** | Code Review Bug Fixes | :white_check_mark: **COMPLETE** | 14 tasks | 20-30 |
| **I** | **Story System Integration** | :white_check_mark: **COMPLETE** | 16 tasks | 36-52 |
| **J** | **Critical Bug Fixes** | :warning: **NEEDS FIXING** | 12 tasks | 17-26 |
| **F** | Polish | :hourglass: In Progress | 11 tasks remaining | 18-26 |
| | | **TOTAL** | **82 tasks** | **159-227** |

### Completed (Reference)
- [x] **Batch G: Critical Integration** (2026-02-03) **COMPLETE**
  - G-1: AgentRouter Full Integration (RouteAgentActionAsync, routing rules)
  - G-2: 47 Personality-Driven Rules (23 new rules added)
- [x] **Batch E: Enhancement** (2026-02-03) **COMPLETE**
  - E-1a: Service Registration Extensions (AddAgentRouting, AddMiddleware, AddAgent, AddRulesEngine)
  - E-1b: Updated MiddlewareDemo and AdvancedMiddlewareDemo to use AddAgentRouting()
  - E-2a-f: All interface extractions complete (IRulesEngineResult, IRuleExecutionResult, ITraceSpan, IMiddlewareContext, IMetricsSnapshot, IAnalyticsReport, IWorkflowDefinition, IWorkflowStage)
  - Fixed: MiddlewareContext thread safety with ConcurrentDictionary
  - E-3a-g: All additional testing complete (63 new tests, 1905 total)
- [x] **Batch D: Application Fixes** (2026-02-03)
  - D-1: Complete Agent Rule Actions (RecommendedAction property, all actions implemented)
  - D-2: Fix Crew Recruitment (recruit/bribe/laylow actions in ExecuteAgentAction)
  - D-3: Balance Game Economy (heat decay 5/week, promotion thresholds 35/60/80/90)
  - D-4: Fix Trivial Test Assertions (11 Assert.True(true) replaced)
  - D-5: Fix Documented Bug (Rule<T> now preserves Matched=true on action throw)
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

## Batch D: Application Fixes (MafiaDemo) (COMPLETE)

> **Prerequisite**: Batch A complete (thread-safe core)
> **Unlocks**: Batch F (documentation)
> **Why after A**: Fixing gameplay on thread-unsafe core means potential re-fixes.
> **Completed**: 2026-02-03

### Task D-1: Complete Agent Rule Actions :white_check_mark:
**Previously**: P2-FIX-3
**Estimated Time**: 3-4 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Rules/GameRulesEngine.Setup.cs`

**Problem**: Agent rule actions have comments describing intent but no implementation.

**Solution**: Added `RecommendedAction` property to `AgentDecisionContext`. Each rule action now sets this property. Updated `GetAgentAction()` to execute the top matching rule and return its recommended action.

**Subtasks**:
- [x] Implement COLLECT action logic (sets RecommendedAction = "collection")
- [x] Implement EXPAND action logic (sets RecommendedAction = "expand")
- [x] Implement RECRUIT action logic (new rule AMBITIOUS_RECRUIT)
- [x] Implement BRIBE action logic (new rule CALCULATING_BRIBE)
- [x] Implement LAYLOW action logic (rule LOYAL_PROTECT)

---

### Task D-2: Fix Crew Recruitment Having No Effect :white_check_mark:
**Previously**: P2-FIX-4
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Game/GameEngine.cs:ExecuteAgentAction`

**Problem**: `RecruitSoldier` decision is made but crew members are never actually added.

**Solution**: Added handlers for "recruit", "bribe", and "laylow" actions in `ExecuteAgentAction()`:
- Recruit: Costs $5,000, adds 1 to SoldierCount, +2 reputation
- Bribe: Costs $10,000, -15 heat
- Laylow: -5 heat (free)

**Subtasks**:
- [x] Implement actual crew member addition (SoldierCount++)
- [x] Track crew in GameState (existing SoldierCount property)
- [x] Log recruitment events

---

### Task D-3: Balance Game Economy :white_check_mark:
**Previously**: P2-FIX-5
**Estimated Time**: 2-3 hours
**Files**: `GameEngine.cs`, `PlayerAgent.cs`, `MissionSystem.cs`

**Problems**:
- Hit missions pay 25x more than collections ($5000 vs $200)
- Heat decay too slow (10 weeks to recover from single hit)
- Promotion thresholds leave only 5-point buffer at max

**Solution**:
- Heat decay: 2/week → 5/week (recovery in ~5 weeks instead of 12)
- Collection: reward 25% → 40% cut, respect 3 → 4
- Hit: reward $5000 → $2500, respect 25 → 20, heat 30 → 25
- Promotion thresholds: 40/70/85/95 → 35/60/80/90 (more buffer)

**Subtasks**:
- [x] Balance mission rewards
- [x] Adjust heat decay rate
- [x] Review and adjust promotion thresholds

---

### Task D-4: Fix Trivial Test Assertions :white_check_mark:
**Previously**: P3-TF-2
**Estimated Time**: 2-3 hours
**File**: `Tests/MafiaDemo.Tests/AutonomousGameTests.cs`

**Problem**: Lines 168, 182, 734, 777, 820 have `Assert.True(true)` that test nothing.

**Solution**: Replaced all 11 `Assert.True(true)` statements with meaningful assertions that verify:
- Agent properties are correctly set
- State remains valid after operations
- No invalid values after rule evaluation

**Subtasks**:
- [x] Replace with meaningful assertions
- [x] Verify actual behavior, not just "doesn't crash"
- [x] Add proper state verification after operations

---

### Task D-5: Fix Documented Bug in RuleEdgeCaseTests :white_check_mark:
**Previously**: P3-TF-3
**Estimated Time**: 1-2 hours
**File**: `RulesEngine/RulesEngine/Core/Rule.cs:Execute()`, `Tests/RulesEngine.Tests/RuleEdgeCaseTests.cs`

**Problem**: Test expects wrong result (0 instead of 1 for MatchedRules when action throws).

**Solution**: Fixed `Rule<T>.Execute()` to preserve `Matched=true` when condition matched but action threw an exception. This makes Rule<T> behavior consistent with ActionRule. Updated tests to reflect the corrected behavior.

**Subtasks**:
- [x] Investigate correct behavior (ActionRule preserves Matched=true, Rule<T> did not)
- [x] Fix engine behavior (Rule.cs)
- [x] Update test expectations
- [x] Rename tests to reflect consistent behavior

---

## Batch E: Enhancement

> **Prerequisite**: Batch C complete (test infrastructure ready)
> **Unlocks**: Batch F
> **Why after C**: New tests should use proper Setup/Teardown from the start.

### E-1: DI & IoC (2 tasks, 4-6 hours) :white_check_mark: **COMPLETE**

#### Task E-1a: Create Service Registration Extensions :white_check_mark:
**Previously**: P1-DI-6
**Estimated Time**: 2-3 hours
**Completed**: 2026-02-03

**Subtasks**:
- [x] Create `AddAgentRouting()` extension for core services
- [x] Create `AddMiddleware<T>()` generic registration
- [x] Create `AddAgent<T>()` generic registration
- [x] Create `AddRulesEngine<T>()` generic registration

---

#### Task E-1b: Update Demos to Use Container :white_check_mark:
**Previously**: P1-DI-7
**Estimated Time**: 2-3 hours
**Completed**: 2026-02-03

**Subtasks**:
- [x] Update MiddlewareDemo (simplified with AddAgentRouting())
- [x] Update AdvancedMiddlewareDemo (simplified with AddAgentRouting())
- [x] MafiaDemo - left as-is (appropriate simpler pattern for games)

---

### E-2: Interface Extraction (6 tasks, 12-16 hours) :white_check_mark: **COMPLETE**

All independent, completed 2026-02-03:

- [x] **E-2a**: Extract IRulesEngineResult Interface (2 hours)
- [x] **E-2b**: Extract IRuleExecutionResult<T> Interface (2 hours)
- [x] **E-2c**: Extract ITraceSpan Interface (2 hours)
- [x] **E-2d**: Extract IMiddlewareContext Interface (2 hours) + thread safety fix with ConcurrentDictionary
- [x] **E-2e**: Extract IMetricsSnapshot/IAnalyticsReport Interfaces (2-3 hours)
- [x] **E-2f**: Extract IWorkflowDefinition/IWorkflowStage Interfaces (2-3 hours)

---

### E-3: Additional Testing (7 tasks, 19-25 hours) :white_check_mark: **COMPLETE**

Completed 2026-02-03. Added 63 new tests (1842 → 1905 total).

- [x] **E-3a**: Add Edge Case Tests for Rules (17 new tests)
- [x] **E-3b**: Add Middleware Pipeline Tests (9 new tests)
- [x] **E-3c**: Add Rate Limiter Tests (8 new tests)
- [x] **E-3d**: Add Circuit Breaker Tests (10 new tests)
- [x] **E-3e**: Add Performance Benchmarks (7 new benchmarks)
- [x] **E-3f**: Add Integration Tests for Agent Routing (11 new tests)
- [x] **E-3g**: Add Test Coverage Analysis (CoverageValidationTests.cs - 6 tests)

---

## Batch G: Critical Integration (COMPLETE)

> **Prerequisite**: None (independent of F)
> **Priority**: CRITICAL - Required before documentation can be accurate
> **Source**: Forensic audit 2026-02-03
> **Completed**: 2026-02-03

### G-1: AgentRouter Full Integration :white_check_mark:
**Priority**: CRITICAL
**Estimated Time**: 3-4 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Game/GameEngine.cs`
**Completed**: 2026-02-03

**Problem**: AgentRouter exists in GameEngine but `RouteMessageAsync()` is never called during `ExecuteTurnAsync()`. Messages are processed directly without going through the middleware pipeline.

**Solution**: Added `RouteAgentActionAsync()` method that creates `AgentMessage` instances for each agent action and routes them through the middleware pipeline. Added new routing rules for AgentAction, Collection, and HeatManagement categories.

**Subtasks**:
- [x] Create `AgentMessage` instances for agent decisions
- [x] Route messages through `_router.RouteMessageAsync()` in `ProcessAutonomousActions()`
- [x] Added `RouteAgentActionAsync()` method with category-based routing
- [x] Added routing rules: AGENT_ACTIONS, COLLECTIONS, HEAT_MANAGEMENT
- [x] Update documentation to confirm integration

---

### G-2: Implement 21 Additional Personality-Driven Rules :white_check_mark:
**Priority**: CRITICAL
**Estimated Time**: 4-6 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/Rules/GameRulesEngine.Setup.cs`
**Completed**: 2026-02-03

**Problem**: Documentation claimed ~45 personality-driven rules but only 24 existed.

**Solution**: Added 23 new personality-driven rules (exceeding target of 21), bringing total to **47 agent rules**.

**New Rules Added**:
- Personality combinations (7): CAUTIOUS_AVOID_RISK, CAUTIOUS_SAFE_COLLECT, CAUTIOUS_RECRUIT, FAMILY_FIRST_STABILITY, FAMILY_FIRST_STRENGTHEN, HOTHEADED_RECKLESS, HOTHEADED_DEFIANT
- Phase-personality hybrids (4): SURVIVAL_AGGRESSIVE, SURVIVAL_CAUTIOUS, GROWTH_CAUTIOUS, DOMINANCE_GREEDY
- Rival-response rules (4): RIVAL_WEAK_AMBITIOUS, RIVAL_WEAK_AGGRESSIVE, RIVAL_THREATENING_LOYAL, RIVAL_THREATENING_CALCULATING
- Heat-management rules (4): HEAT_RISING_WEALTHY, HEAT_RISING_AGGRESSIVE, HEAT_FALLING_PRODUCTIVE, HEAT_CRITICAL_EMERGENCY
- Economic-strategy rules (4): WEALTH_GROWING_AMBITIOUS, WEALTH_GROWING_GREEDY, WEALTH_SHRINKING_CAUTIOUS, WEALTH_SHRINKING_GREEDY

**Subtasks**:
- [x] Add 7 CAUTIOUS/FAMILY_FIRST/HOT_HEADED personality rules
- [x] Add 4 phase-personality hybrid rules
- [x] Add 4 rival assessment rules
- [x] Add 4 heat management rules
- [x] Add 4 economic strategy rules
- [x] Update rule count in documentation (82 → 105 total)

---

### G-3: Replace Console.WriteLine in Middleware with IAgentLogger :white_check_mark:
**Priority**: HIGH
**Estimated Time**: 2-3 hours
**Files**: `CommonMiddleware.cs`, `AdvancedMiddleware.cs`
**Status**: Moved to F-1g (documentation/polish) - low impact

---

### G-4: Fix Stale File References in Documentation :white_check_mark:
**Priority**: HIGH
**Estimated Time**: 1-2 hours
**Status**: Moved to Batch F (documentation)

---

### G-5: Correct Rule Count Claims Across Documentation :white_check_mark:
**Priority**: HIGH
**Estimated Time**: 1 hour
**Status**: Moved to Batch F (documentation)

---

## Batch H: Code Review Bug Fixes (IN PROGRESS)

> **Prerequisite**: Batch G complete
> **Priority**: HIGH - Bugs found during comprehensive code review
> **Source**: Code review 2026-02-03 (see MAFIA_DEMO_CODE_REVIEW.md)
> **Full Report**: `/MAFIA_DEMO_CODE_REVIEW.md`
> **Progress**: **ALL 14 TASKS COMPLETE** (2026-02-03)

### H-1: Fix Heat Balance (Critical - Game Unwinnable) :white_check_mark:
**Priority**: CRITICAL
**Estimated Time**: 2-3 hours
**Files**: `Game/GameEngine.cs:509-541`, `Game/GameEngine.cs:901-911`
**Completed**: 2026-02-03

**Problem**: Heat generation vs decay was fundamentally imbalanced.

**Solution**:
- Reduced territory heat generation: 5+10+8 → 2+5+4 = 11/week
- Increased natural decay: 5 → 8/week
- Net: +3 heat/week (manageable with occasional bribing)
- Game now reaches victory at week 52

**Subtasks**:
- [x] Reduce territory base heat generation (23 → 11)
- [x] Increase natural heat decay (5 → 8)
- [x] Verify game is winnable with balanced play (confirmed: victory at week 52)

---

### H-2: Fix Event Time Using Real Time Instead of Game Time :white_check_mark:
**Priority**: CRITICAL
**Estimated Time**: 2-3 hours
**File**: `Rules/GameRulesEngine.cs:133-139`
**Completed**: 2026-02-03

**Solution**:
- Added `GameWeek` property to `GameEvent` class
- Changed event checks to use `_state.Week - e.GameWeek < 3`
- Updated LogEvent to capture GameWeek

**Subtasks**:
- [x] Add `GameWeek` property to `GameEvent` class
- [x] Change event checks to use game weeks
- [x] Update LogEvent to capture GameWeek

---

### H-3: Fix CHAIN_HIT_TO_WAR Rule Exception :white_check_mark:
**Priority**: HIGH
**Estimated Time**: 1-2 hours
**File**: `Rules/GameRulesEngine.Setup.cs:1140-1147`
**Completed**: 2026-02-03

**Solution**: Changed `First()` to `FirstOrDefault()` with null check.

**Subtasks**:
- [x] Replace `First()` with `FirstOrDefault()` and add null check

---

### H-4: Fix Rival Hostility Going Negative :white_check_mark:
**Priority**: HIGH
**Estimated Time**: 1 hour
**File**: `Game/GameEngine.cs:892-896`
**Completed**: 2026-02-03

**Solution**: Added `Math.Max(0, ...)` clamping.

**Subtasks**:
- [x] Change to: `rival.Hostility = Math.Max(0, rival.Hostility - Random.Shared.Next(1, 3));`
- [x] Add test verifying hostility stays >= 0 (2 tests added)

---

### H-5: Fix MissionEvaluator Applying Rules Twice :white_check_mark:
**Priority**: HIGH
**Estimated Time**: 1-2 hours
**File**: `MissionSystem.cs:524-535`
**Completed**: 2026-02-03

**Solution**: Removed duplicate `EvaluateAll()` call. High-risk bonus logic now applied explicitly after the success roll.

**Subtasks**:
- [x] Remove second `EvaluateAll()` call
- [x] Handle post-roll bonuses explicitly (HIGH_RISK_HIGH_REWARD)

---

### H-6: Fix PlayerAgent Decision Trace Field Inconsistency :white_check_mark:
**Priority**: MEDIUM
**Estimated Time**: 1 hour
**File**: `PlayerAgent.cs:212, 345`
**Completed**: 2026-02-03

**Solution**: Changed `RuleName.Contains("REJECT")` to `RuleId.Contains("REJECT", StringComparison.OrdinalIgnoreCase)` for consistency with DecideMission method.

**Subtasks**:
- [x] Change line 345 to use `RuleId` instead of `RuleName`
- [x] Add `StringComparison.OrdinalIgnoreCase` for safety

---

### H-7: Fix CONSEQUENCE_VULNERABLE Empty Territory Check :white_check_mark:
**Priority**: MEDIUM
**Estimated Time**: 1 hour
**File**: `Rules/GameRulesEngine.Setup.cs:101-111`
**Completed**: 2026-02-03

**Solution**: Added `Territories.Any()` check in condition and `FirstOrDefault()` with null check in action.

**Subtasks**:
- [x] Add empty check before `First()`
- [x] Use FirstOrDefault with null check

---

### H-8: Clarify RivalStrategyContext.ShouldAttack Logic :white_check_mark:
**Priority**: MEDIUM
**Estimated Time**: 1 hour
**File**: `Rules/RuleContexts.cs:292`
**Completed**: 2026-02-03

**Analysis**: The logic is actually **intentionally correct**:
```csharp
public bool ShouldAttack => RivalIsStronger && PlayerIsWeak && !PlayerIsDistracted;
```

**Design rationale**: Rivals wait until law enforcement attention is LOW before attacking.
- When player has high heat (distracted), attacking would draw unwanted police attention to the rival
- When player heat is low, rivals can strike without law enforcement interference
- This is consistent with `ShouldWait` which says: wait when `PlayerIsDistracted && !RivalIsAngry`

**Solution**: Added comprehensive XML documentation explaining the strategic reasoning.

**Subtasks**:
- [x] Analyze intended behavior (current logic is correct)
- [x] Add XML documentation explaining design rationale

---

### H-9: Fix Currency Symbol Display in Career Mode :white_check_mark:
**Priority**: LOW
**Estimated Time**: 30 minutes
**File**: `AutonomousPlaythrough.cs:265-267`
**Completed**: 2026-02-03

**Solution**: Changed `:C0` format to explicit `+$` / `-$` format with `:N0`.

**Subtasks**:
- [x] Use explicit format: `+${value:N0}` instead of `:C0`

---

### H-10: Add Agent Action Coordination :white_check_mark:
**Priority**: MEDIUM
**Estimated Time**: 2-3 hours
**File**: `Game/GameEngine.cs`
**Completed**: 2026-02-03

**Problem**: Multiple agents can bribe simultaneously, each costing $10,000 but only marginally more effective. No coordination.

**Solution**: Added `BribedThisWeek` flag to `GameState` that:
- Resets at the start of each turn in `ExecuteTurnAsync()`
- Checked/set in agent bribe action (returns skip message if already bribed)
- Checked/set in player bribe action (`ExecuteBribe()`)

**Subtasks**:
- [x] Add "already bribed this week" flag to prevent duplicate bribes
- [x] Update agent bribe action to check/set flag
- [x] Update player bribe action to check/set flag
- [x] Add tests for coordinated agent behavior (3 tests)

---

### H-11: Consolidate Defeat Condition Logic :white_check_mark:
**Priority**: MEDIUM
**Estimated Time**: 1-2 hours
**Files**: `Game/GameEngine.cs:913-939`, `Rules/GameRulesEngine.Setup.cs:64-73`
**Completed**: 2026-02-03

**Solution**: Added game condition constants to `GameState` class:
- `DefeatReputationThreshold = 10`
- `DefeatHeatThreshold = 100`
- `VictoryWeekThreshold = 52`
- `VictoryWealthThreshold = 1000000m`
- `VictoryReputationThreshold = 80`

Both GameEngine.CheckGameOver() and GameRulesEngine.Setup rules now use these constants.

**Subtasks**:
- [x] Add constants to GameState class
- [x] Update GameEngine.CheckGameOver() to use constants
- [x] Update GameRulesEngine defeat/victory rules to use constants

---

### H-12: Improve Event Log Eviction Performance :white_check_mark:
**Priority**: LOW
**Estimated Time**: 1 hour
**File**: `Game/GameEngine.cs:944-948`
**Completed**: 2026-02-03

**Solution**: Changed `List<GameEvent>` to `Queue<GameEvent>` for O(1) dequeue:
- EventLog property type: `List<GameEvent>` → `Queue<GameEvent>`
- `RemoveAt(0)` → `Dequeue()` (O(1) instead of O(n))
- `Add()` → `Enqueue()`
- Updated test to use `.First()` instead of `[0]` indexing

**Subtasks**:
- [x] Change EventLog type to Queue<GameEvent>
- [x] Update LogEvent to use Dequeue/Enqueue
- [x] Update tests to use LINQ First() instead of indexing

---

### H-13: Review Victory Condition Achievability :white_check_mark:
**Priority**: MEDIUM
**Estimated Time**: 2-3 hours
**File**: `Game/GameEngine.cs:934-939`
**Completed**: 2026-02-03

**Solution**: Added three integration tests to verify victory achievability:
1. `MafiaGameEngine_VictoryAchievable_SimulatedOptimalPlay` - Full game simulation
2. `MafiaGameEngine_HeatBalance_AllowsProgressTo52Weeks` - Verifies 25+ weeks without intervention
3. `MafiaGameEngine_VictoryConditions_AllThreeRequirementsMet` - Near-victory state test

After H-1 heat balance fix, games now reliably reach victory.

**Subtasks**:
- [x] Verify victory is achievable after H-1 fix (confirmed)
- [x] Add integration test for simulated optimal play
- [x] Add test for heat balance allowing 25+ week progress
- [x] Add test for victory conditions when close to goal

---

### H-14: Add Null Safety to Rival Lookups :white_check_mark:
**Priority**: LOW
**Estimated Time**: 1 hour
**File**: `Game/GameEngine.cs`, `Rules/RuleContexts.cs`
**Completed**: 2026-02-03

**Solution**: Added null-safe helper properties to `GameState` class:
- `HasRivals` - Returns true if any rival families exist
- `MaxRivalHostility` - Returns max hostility or 0 if no rivals
- `MinRivalStrength` - Returns min strength or 0 if no rivals
- `HasHostileRival(threshold)` - Safe check for hostile rivals
- `HasWeakRival(threshold)` - Safe check for weak rivals

Updated `AgentDecisionContext` in RuleContexts.cs to use these safe helpers.

**Subtasks**:
- [x] Add HasRivals, MaxRivalHostility, MinRivalStrength properties
- [x] Add HasHostileRival() and HasWeakRival() helper methods
- [x] Update AgentDecisionContext to use safe helpers
- [x] Document null-safe access patterns

---

## Batch I: Story System Integration

> **Prerequisite**: Batch H complete (stable game mechanics)
> **Status**: Story System IMPLEMENTED, now needs INTEGRATION with MafiaDemo game
> **Design Review**: See `AgentRouting.MafiaDemo/Story/GAME_REVIEW.md`

### Overview

The Story System has been fully implemented in `AgentRouting.MafiaDemo/Story/` with:

- **World State Layer**: Location, NPC, Faction, WorldState
- **Narrative Layer**: StoryNode, StoryGraph, StoryEvent, PlotThread
- **Intelligence Layer**: Intel, IntelRegistry
- **Agent Layer**: Persona, Memory, EntityMind
- **Communication Layer**: AgentQuestion, AgentResponse, ConversationContext
- **Rules Layer**: 5 rule setup files (Conversation, Evolution, Triggers, Memory, Consequences)
- **Engine Layer**: RulesBasedConversationEngine, DynamicMissionGenerator
- **Seeding Layer**: WorldStateSeeder

**This batch focuses on INTEGRATING these systems with the existing MafiaDemo game.**

### Architecture (Integration Points)

```
┌─────────────────────────────────────────────────────────────────┐
│                    EXISTING MAFIA DEMO                          │
├─────────────────────────────────────────────────────────────────┤
│  GameState (Territory, RivalFamily, Heat, Reputation)           │
│  MissionSystem (MissionGenerator, MissionEvaluator)             │
│  PlayerAgent (PlayerDecisionContext, DecisionRules)             │
│  GameEngine (Turn processing, victory/defeat conditions)        │
└────────────────────────────┬────────────────────────────────────┘
                             │ INTEGRATION
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                      STORY SYSTEM                               │
├─────────────────────────────────────────────────────────────────┤
│  WorldState (Location, NPC, Faction)                            │
│  StoryGraph (PlotThread, StoryNode, StoryEdge)                  │
│  IntelRegistry (Intel gathering & tracking)                     │
│  EntityMind (Persona, Memory, Conversation)                     │
│  DynamicMissionGenerator (Context-aware missions)               │
│  ConsequenceRules (Mission outcome effects)                     │
└─────────────────────────────────────────────────────────────────┘
```

---

### I-1: Implementation Complete (Reference) :white_check_mark:

The following components are ALREADY IMPLEMENTED in `Story/`:

| Component | Files | Status |
|-----------|-------|--------|
| World State | `World/Location.cs`, `World/NPC.cs`, `World/Faction.cs`, `World/WorldState.cs` | ✅ Complete |
| Story Graph | `Narrative/StoryNode.cs`, `Narrative/StoryGraph.cs`, `Narrative/StoryEvent.cs` | ✅ Complete |
| Intel System | `Intelligence/Intel.cs`, `Intelligence/IntelRegistry.cs` | ✅ Complete |
| Agent Mind | `Agents/Persona.cs`, `Agents/Memory.cs`, `Agents/EntityMind.cs` | ✅ Complete |
| Communication | `Communication/AgentQuestion.cs`, `Communication/AgentResponse.cs`, `Communication/ConversationContext.cs` | ✅ Complete |
| Rules | `Rules/ConversationRulesSetup.cs`, `Rules/EvolutionRules.cs`, `Rules/StoryTriggerRules.cs`, `Rules/MemoryRelevanceRules.cs`, `Rules/ConsequenceRules.cs` | ✅ Complete |
| Engine | `Engine/RulesBasedConversationEngine.cs` | ✅ Complete |
| Generation | `Generation/DynamicMissionGenerator.cs`, `Generation/MissionHistory.cs` | ✅ Complete |
| Seeding | `Seeding/WorldStateSeeder.cs` | ✅ Complete |
| Core | `Core/Enums.cs`, `Core/Thresholds.cs` | ✅ Complete |

---

### I-2: Foundation Integration (P0) (3 tasks, 6-9 hours)

#### Task I-2a: GameState ↔ WorldState Bridge :white_check_mark: COMPLETE
**Priority**: P0 - Foundation for all integration
**Estimated Time**: 2-3 hours
**Files**: `Game/GameEngine.cs`, new `Game/GameWorldBridge.cs`

**Problem**: GameState and WorldState track overlapping concepts (Territory/Location, RivalFamily/Faction) with no synchronization.

**Implementation**:
```csharp
public class GameWorldBridge
{
    public void SyncToWorldState(GameState game, WorldState world);
    public void SyncFromWorldState(WorldState world, GameState game);
}
```

**Subtasks**:
- [x] Create GameWorldBridge class
- [x] Implement Territory → Location sync (heat, state)
- [x] Implement RivalFamily → Faction sync (hostility, resources)
- [x] Add bridge initialization in GameEngine constructor
- [x] Add sync call in ExecuteTurnAsync() after game state changes

---

#### Task I-2b: WorldState Initialization :white_check_mark: COMPLETE
**Priority**: P0 - Required before other integrations
**Estimated Time**: 2-3 hours
**Files**: `Game/GameEngine.cs`

**Problem**: WorldState, StoryGraph, IntelRegistry are not initialized in GameEngine.

**Subtasks**:
- [x] Add WorldState, StoryGraph, IntelRegistry fields to GameEngine
- [x] Call WorldStateSeeder.CreateInitialWorld() in constructor
- [x] Add StoryGraph initialization with seed plot threads
- [x] Add CurrentWeek synchronization between GameState and WorldState

---

#### Task I-2c: Week Counter Consolidation :white_check_mark: COMPLETE
**Priority**: P0 - Prevents dual-tracking bugs
**Estimated Time**: 2-3 hours
**Files**: `Game/GameEngine.cs`, `Story/World/WorldState.cs`
**Completed**: 2026-02-03

**Problem**: Both GameState.Week and WorldState.CurrentWeek track weeks independently.

**Solution**: Added `LinkedWorldState` property to `GameState`. When set, `GameState.Week` delegates to `WorldState.CurrentWeek`. This is set automatically in `GameEngine.InitializeStorySystem()`.

**Subtasks**:
- [x] Make WorldState.CurrentWeek the single source of truth
- [x] Update GameState.Week to delegate to WorldState
- [x] Add test verifying week consistency across turns (3 new tests)

---

### I-3: NPC & Relationship Integration (P1) :white_check_mark: **COMPLETE**

#### Task I-3a: Mission Target NPC References :white_check_mark: COMPLETE
**Priority**: P1 - Enables relationship tracking
**Estimated Time**: 2-3 hours
**Files**: `MissionSystem.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] Add NPCId and LocationId properties to Mission class
- [x] Update MissionGenerator to assign NPC/Location from WorldState
- [x] Add NPC name formatting in mission descriptions
- [x] Maintain backward compatibility with missions without NPCs

---

#### Task I-3b: Relationship Updates on Mission Completion :white_check_mark: COMPLETE
**Priority**: P1 - Makes actions have consequences
**Estimated Time**: 2-3 hours
**Files**: `MissionSystem.cs`, `PlayerAgent.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] Add MissionConsequenceHandler.ApplyMissionConsequences() method
- [x] Define relationship change rules per mission type
- [x] Update NPC status based on repeated interactions
- [x] Call after mission execution in PlayerAgent

---

#### Task I-3c: NPC Status Effects on Missions :white_check_mark: COMPLETE
**Priority**: P1 - Makes relationships meaningful
**Estimated Time**: 2-3 hours
**Files**: `MissionSystem.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] Add NPC status check in MissionGenerator (hostile NPCs → harder missions)
- [x] Add relationship bonus/penalty via ApplyNPCEffects()
- [x] Allied NPCs make missions easier (-2 risk)
- [x] Intimidated NPCs have higher collection yields (+20%)

---

### I-4: Plot Thread Integration (P1) :white_check_mark: **COMPLETE**

#### Task I-4a: Plot Thread State Machine :white_check_mark: COMPLETE
**Priority**: P1 - Enables story arcs
**Estimated Time**: 2-3 hours
**Files**: `Game/GameEngine.cs`, `Story/Narrative/StoryGraph.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] Add UpdatePlotThreads() call in ExecuteTurnAsync()
- [x] Evaluate PlotThread.ActivationCondition against WorldState
- [x] Transition Dormant → Available when condition met
- [x] Log plot thread state changes as events

---

#### Task I-4b: Plot Mission Priority :white_check_mark: COMPLETE
**Priority**: P1 - Ensures plot progression
**Estimated Time**: 2-3 hours
**Files**: `Story/Generation/DynamicMissionGenerator.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] Query StoryGraph for active plot missions (GetPlotMissions)
- [x] Weight plot missions higher (+20 active, +10 available)
- [x] Add plot thread title prefix to mission titles
- [x] Track plot mission completion in PlotThread

---

#### Task I-4c: Plot Completion Rewards :white_check_mark: COMPLETE
**Priority**: P1 - Motivates plot engagement
**Estimated Time**: 2-3 hours
**Files**: `Game/GameEngine.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] Check for plot completion when mission completes (CompletePlotMission)
- [x] Apply PlotThread.RespectReward and MoneyReward (ApplyPlotRewards)
- [x] Call PlotThread.OnCompleted callback
- [x] Add achievement/event log entry for plot completion

---

### I-5: Mission System Integration (P2) :white_check_mark: **COMPLETE**

#### Task I-5a: Integrate DynamicMissionGenerator :white_check_mark: COMPLETE
**Priority**: P2 - Adds mission variety
**Estimated Time**: 2-3 hours
**Files**: `Story/Integration/HybridMissionGenerator.cs`, `Story/Integration/MissionAdapter.cs`, `Game/GameEngine.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] Create HybridMissionGenerator combining Story + Legacy systems
- [x] Create MissionAdapter to convert MissionCandidate to Mission
- [x] Add GenerateMission() and GenerateMissionChoices() to GameEngine
- [x] Fall back to legacy generator when Story System disabled

---

#### Task I-5b: Apply ConsequenceRules After Missions :white_check_mark: COMPLETE
**Priority**: P2 - Adds cascading effects
**Estimated Time**: 2-3 hours
**Files**: `MissionSystem.cs`, `PlayerAgent.cs`
**Completed**: 2026-02-03

**Problem**: ConsequenceRulesSetup defines rules but they're never applied.

**Solution**: Added `MissionConsequenceHandler.ApplyConsequenceRules()` method that creates `ConsequenceContext` and executes consequence rules. Called from `PlayerAgent.ExecuteMissionAsync()` when Story System is enabled.

**Subtasks**:
- [x] Create ConsequenceContext from mission result
- [x] Call consequence rules engine after mission execution
- [x] Apply world state changes from consequences
- [x] Log applied consequences for story recap (appended to result.Message)

---

#### Task I-5c: Intel Recording for Information Missions :white_check_mark: COMPLETE
**Priority**: P2 - Leverages intel system
**Estimated Time**: 2-3 hours
**Files**: `MissionSystem.cs`, `PlayerAgent.cs`
**Completed**: 2026-02-03

**Problem**: Information missions don't actually produce intel.

**Solution**: Added `MissionConsequenceHandler.RecordIntelFromMission()` method that creates Intel objects based on mission context (NPC, Location, or general). Added `IntelRegistry` property to `PlayerAgent`. Called from `PlayerAgent.ExecuteMissionAsync()` when Story System is enabled.

**Subtasks**:
- [x] Create Intel object from successful Information mission
- [x] Add Intel to IntelRegistry
- [x] Define intel type based on mission context (NpcActivity, LocationStatus, Rumor)
- [x] 4 new tests for intel recording

---

### I-6: Conversation System (P3) - **MOVED TO BATCH F**

> **Status**: Deferred to Batch F - Focus on completing core Story System integration first.
> These tasks add new gameplay features but aren't required for Story System functionality.
> See Batch F tasks F-2a and F-2b for the conversation system implementation.

---

### I-7: Integration Testing :white_check_mark: **COMPLETE**

#### Task I-7a: GameState ↔ WorldState Sync Tests :white_check_mark: COMPLETE
**Priority**: HIGH
**Estimated Time**: 2-3 hours
**Files**: `Tests/MafiaDemo.Tests/StorySystemIntegrationTests.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] Test Territory changes sync to Location (GameWorldBridge_SyncToWorldState_PropagatesContestState)
- [x] Test RivalFamily changes sync to Faction (via Initialize)
- [x] Test week counter consistency (GameWorldBridge_SyncToWorldState_UpdatesWeek)
- [x] Test heat level synchronization (via bridge sync)

---

#### Task I-7b: NPC Relationship Tests :white_check_mark: COMPLETE
**Priority**: HIGH
**Estimated Time**: 2-3 hours
**Files**: `Tests/MafiaDemo.Tests/StorySystemIntegrationTests.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] Test relationship changes from mission outcomes (MissionConsequenceHandler_AppliesRelationshipChanges)
- [x] Test NPC status transitions (via UpdateNPCStatus)
- [x] Test negotiation improves relationship (MissionConsequenceHandler_NegotiationImprovesRelationship)
- [x] Test interaction history recording (MissionConsequenceHandler_RecordsInteractionHistory)

---

#### Task I-7c: Plot Thread Progression Tests :white_check_mark: COMPLETE
**Priority**: HIGH
**Estimated Time**: 2-3 hours
**Files**: `Tests/MafiaDemo.Tests/StorySystemIntegrationTests.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] Test plot activation from world state (PlotThread_Lifecycle_DormantToAvailable)
- [x] Test plot mission surfacing (PlotThread_StartPlotThread_TransitionsToActive)
- [x] Test plot completion check (PlotThread_IsPlotCompleted_ChecksMissionIndex)
- [x] Test plot start conditions (PlotThread_StartPlotThread_FailsIfNotAvailable)

---

#### Task I-7d: Full Integration Tests :white_check_mark: COMPLETE
**Priority**: HIGH
**Estimated Time**: 2-3 hours
**Files**: `Tests/MafiaDemo.Tests/StorySystemIntegrationTests.cs`
**Completed**: 2026-02-03

**Subtasks**:
- [x] GameEngine integration (GameEngine_StorySystemEnabled_WhenInitialized)
- [x] Mission generation (GameEngine_GenerateMission_ReturnsValidMission)
- [x] Mission choices (GameEngine_GenerateMissionChoices_ReturnsMultiple)
- [x] Mission history (GameEngine_RecordMissionCompletion_UpdatesHistory, MissionHistory_* tests)

---

## Batch J: Critical Bug Fixes (Code Review 2026-02-04)

> **Prerequisite**: None - Critical bugs found during deep code review
> **Priority**: P0/P1 - These bugs were supposedly fixed but still exist or were reintroduced
> **Full Report**: `/CODE_REVIEW_BUGS.txt`
> **Status**: NEEDS FIXING

### J-1: Thread-Safety Bugs (4 tasks, 8-12 hours)

#### Task J-1a: Fix TrackPerformance Race Condition (RulesEngineCore)
**Priority**: P0 - CRITICAL
**Estimated Time**: 2-3 hours
**File**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs:666-693`

**Problem**: `ConcurrentDictionary.AddOrUpdate` mutates existing `RulePerformanceMetrics` object in place:
```csharp
(_, existing) =>
{
    existing.ExecutionCount++;          // NOT THREAD-SAFE
    existing.TotalExecutionTime += duration;  // NOT THREAD-SAFE
    ...
    return existing;
}
```
The update factory can be called multiple times concurrently, causing data corruption.

**Subtasks**:
- [ ] Use Interlocked operations OR create new immutable metrics object in update factory
- [ ] Add concurrency test to verify fix
- [ ] Apply same fix to ImmutableRulesEngine.TrackPerformance (lines 1066-1094)

---

#### Task J-1b: Fix ServiceContainer Singleton Race Condition
**Priority**: P0 - CRITICAL
**Estimated Time**: 2-3 hours
**File**: `AgentRouting/AgentRouting/DependencyInjection/ServiceContainer.cs:72-84`

**Problem**: Multiple threads can invoke the factory simultaneously, creating multiple "singleton" instances:
```csharp
var instance = (TService)descriptor.Factory(this);  // Called by multiple threads
if (_singletons.TryAdd(type, instance))
    return instance;
return (TService)_singletons[type];  // Discard extra instances
```

**Subtasks**:
- [ ] Use double-checked locking OR Lazy<T> for singleton creation
- [ ] Add concurrency test to verify only one instance created
- [ ] Fix same issue in ServiceScope.Resolve (lines 210-213)

---

#### Task J-1c: Fix AgentRouter.RegisterAgent Thread-Safety
**Priority**: P1 - HIGH
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting/Core/AgentRouter.cs:62-66`

**Problem**: Registration operations are not synchronized:
```csharp
_agents.Add(agent);           // Not thread-safe
_agentById[agent.Id] = agent; // Not thread-safe
```

**Subtasks**:
- [ ] Add lock around registration operations OR use concurrent collections properly
- [ ] Add concurrency test for simultaneous registrations

---

#### Task J-1d: Fix ABTestingMiddleware Non-Thread-Safe Random
**Priority**: P1 - HIGH
**Estimated Time**: 1 hour
**File**: `AgentRouting/AgentRouting/Middleware/AdvancedMiddleware.cs:386, 406`

**Problem**: `System.Random` is not thread-safe. Concurrent calls corrupt internal state.

**Subtasks**:
- [ ] Replace `new Random()` with `Random.Shared` (thread-safe in .NET 6+)

---

### J-2: Logic Errors (3 tasks, 4-6 hours)

#### Task J-2a: Fix CompositeRule.Execute Double Evaluation
**Priority**: P1 - HIGH
**Estimated Time**: 2-3 hours
**File**: `RulesEngine/RulesEngine/Core/Rule.cs:262-292`

**Problem**: Child rules are evaluated 2-3 times:
1. In `Evaluate(fact)` call (line 266)
2. Explicitly in foreach loop (line 274)
3. Inside `Execute(fact)` which calls `Evaluate` again

**Subtasks**:
- [ ] Cache evaluation results or refactor to evaluate once
- [ ] Add performance test to verify improvement

---

#### Task J-2b: Fix ImmutableRulesEngine Missing Validation
**Priority**: P1 - HIGH
**Estimated Time**: 1-2 hours
**File**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs:913-926`

**Problem**: `ImmutableRulesEngine.WithRule()` doesn't validate:
- Rule is not null
- Rule.Id is not null/empty
- Rule.Name is not null/empty
- No duplicate rule IDs

**Subtasks**:
- [ ] Add validation consistent with RulesEngineCore.RegisterRule()
- [ ] Add tests for validation

---

#### Task J-2c: Fix MessageQueueMiddleware async void
**Priority**: P0 - CRITICAL
**Estimated Time**: 1 hour
**File**: `AgentRouting/AgentRouting/Middleware/AdvancedMiddleware.cs:347`

**Problem**: `async void ProcessBatch(object? state)` - exceptions crash application.

**Subtasks**:
- [ ] Change to async Task with proper exception handling
- [ ] Wrap Timer callback appropriately

---

### J-3: Memory/Resource Issues (3 tasks, 3-5 hours)

#### Task J-3a: Fix MetricsMiddleware Unbounded Growth
**Priority**: P1 - HIGH
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs:574-617`

**Problem**: `ConcurrentBag<long> _processingTimes` grows unboundedly.

**Subtasks**:
- [ ] Implement ring buffer OR periodic cleanup OR reservoir sampling
- [ ] Add test for memory bounds

---

#### Task J-3b: Fix StoryGraph Event Log Unbounded Growth
**Priority**: P2 - MEDIUM
**Estimated Time**: 1 hour
**File**: `AgentRouting/AgentRouting.MafiaDemo/Story/Narrative/StoryGraph.cs:23`

**Problem**: `List<StoryEvent> _eventLog` grows unboundedly.

**Subtasks**:
- [ ] Add max size and eviction (similar to GameState.EventLog)

---

#### Task J-3c: Fix DistributedTracingMiddleware Unbounded Spans
**Priority**: P2 - MEDIUM
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting/Middleware/AdvancedMiddleware.cs:17, 75`

**Problem**: `ConcurrentBag<TraceSpan> _spans` grows unboundedly.

**Subtasks**:
- [ ] Implement span rotation OR max size OR external export with cleanup

---

### J-4: Weak Implementations (2 tasks, 2-3 hours)

#### Task J-4a: Fix SystemClock Static Mutable State
**Priority**: P2 - MEDIUM
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting/Infrastructure/SystemClock.cs:23`

**Problem**: Static mutable `Instance` causes test interference in parallel execution.

**Subtasks**:
- [ ] Use AsyncLocal<ISystemClock> OR proper DI pattern
- [ ] Update tests to use new pattern

---

#### Task J-4b: Fix SanitizationMiddleware Incomplete XSS Filter
**Priority**: P2 - MEDIUM
**Estimated Time**: 1 hour
**File**: `AgentRouting/AgentRouting/Middleware/AdvancedMiddleware.cs:248-259`

**Problem**: Current filter is trivially bypassable (case variations, encoding, nesting).

**Subtasks**:
- [ ] Use proper HTML encoding OR allowlist approach
- [ ] Add test cases for bypass attempts

---

## Batch F: Polish (14 tasks total)

> **Prerequisite**: Batches D and E complete
> **Why last**: Document stable code, not moving targets.
> **Updated**: Added F-3 runtime bugs discovered during testing (2026-02-03)

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

#### Task F-1b: Update ARCHITECTURE.md Integration Status :white_check_mark:
**Completed**: 2026-02-03
**Estimated Time**: 1-2 hours
**File**: `AgentRouting/AgentRouting.MafiaDemo/ARCHITECTURE.md`

**Problem**: "Needs Integration" section listed items that were actually complete.

**Resolution**:
- [x] All 5 "Needs Integration" items were verified as COMPLETE
- [x] Marked as done: rules-driven decisions, AgentRouter integration, async rules
- [x] Added AI Career Mode and personality effects to completed list
- [x] Documented all 8 rule engines (was showing only 3)
- [x] Updated Next Steps to reflect remaining enhancement opportunities

---

#### Remaining Documentation Tasks (from original P4)

- [ ] **F-1c**: Update CLAUDE.md with New Patterns (2-3 hours)
- [ ] **F-1d**: Add XML Documentation to Public APIs (3-4 hours)
- [ ] **F-1e**: Create API Reference Documentation (3-4 hours)
- [ ] **F-1f**: Create MafiaDemo Player Guide (2-3 hours)
- [ ] **F-1g**: Clean Up Code Style and Warnings (2-3 hours)
- [ ] **F-1h**: Create Release Checklist (1-2 hours)

---

### F-2: Conversation System (Moved from Batch I) (2 tasks, 4-6 hours)

> **Source**: Moved from Batch I (I-6a, I-6b) - Conversation tasks are feature additions, not core Story System integration.

#### Task F-2a: Basic NPC Conversation Command
**Priority**: P3 - Adds NPC dialogue
**Estimated Time**: 2-3 hours
**Files**: `Game/GameEngine.cs`

**Problem**: No way to talk to NPCs in the game.

**Subtasks**:
- [ ] Add "talk <npc-name>" command to game engine
- [ ] Look up NPC by name in WorldState
- [ ] Create basic AgentQuestion (WhatDoYouKnow, WhereIs)
- [ ] Format AgentResponse for display

---

#### Task F-2b: Conversation Results Integration
**Priority**: P3 - Makes conversations meaningful
**Estimated Time**: 2-3 hours
**Files**: `Game/GameEngine.cs`

**Problem**: Conversation responses should affect gameplay.

**Subtasks**:
- [ ] Update relationship based on ResponseDecision.RelationshipModifier
- [ ] Record intel from information responses
- [ ] Update EntityMind memory of the conversation
- [ ] Add bargaining outcome handling (money for info)

---

### F-3: Runtime Bugs ✅ COMPLETE (2026-02-04)

> **Source**: Found during MafiaDemo runtime testing
> **Completed**: 2026-02-04

#### Task F-3a: AI Career Mode Missing Story System Integration :white_check_mark:
**Priority**: P1 - HIGH - Major feature gap
**Estimated Time**: 2-3 hours
**Files**: `AutonomousPlaythrough.cs`
**Completed**: 2026-02-04

**Problem**: AI Career Mode (Option 1) creates raw `GameState` instead of using `MafiaGameEngine`, bypassing the entire Story System.

**Solution**: Changed `AutonomousPlaythrough.cs` to use `MafiaGameEngine` and wire PlayerAgent's Story System properties.

**Subtasks**:
- [x] Replace `new GameState()` with `new MafiaGameEngine(router, logger)`
- [x] Wire PlayerAgent's WorldState, StoryGraph, IntelRegistry properties from engine
- [x] Use engine's GenerateMission() instead of PlayerAgent's internal generator
- [x] Verified Story System integration in Career Mode

---

#### Task F-3b: Mission Success Rate Too Low for New Players :white_check_mark:
**Priority**: P2 - MEDIUM - Balance issue
**Estimated Time**: 1-2 hours
**Files**: `MissionSystem.cs`
**Completed**: 2026-02-04

**Problem**: New players have only 60% success rate leading to frequent game overs.

**Solution**:
- Lowered skill bonus threshold from >10 to >5
- Added EARLY_CAREER_BOOST rule (+10% for Associates)
- New players now have ~75-80% success rate on early missions

**Subtasks**:
- [x] Lower skill advantage threshold for bonus (>5 instead of >10)
- [x] Add EARLY_CAREER_BOOST rule for Associates
- [x] Verified reasonable success rate for new players

---

#### Task F-3c: Plot Count Display Misleading :white_check_mark:
**Priority**: P3 - LOW - UX improvement
**Estimated Time**: 0.5-1 hour
**Files**: `Game/GameEngine.cs`
**Completed**: 2026-02-04

**Problem**: Message showed only active plots, not available plots.

**Solution**: Display now shows both: `"📖 Story System: Active (X active, Y available plots)"`

**Subtasks**:
- [x] Change display to show both Active and Available plot counts

---

### Additional Story System Bug Fixes (2026-02-04)

Discovered during deep code review of Story System implementation:

| Bug | Severity | File | Fix |
|-----|----------|------|-----|
| Null reference on LastInteractionWeek | Critical | DynamicMissionGenerator.cs | Added null coalescing |
| KeyNotFoundException in GetNPCsAtLocation | Critical | WorldState.cs | Fixed GetNPCsNeedingAttention |
| Missing NPC interaction decay | High | MissionHistory.cs | Added decay logic |
| Delayed triggers not implemented | High | StoryGraph.cs | Implemented pending triggers queue |
| No edge validation | High | StoryGraph.cs | Added validation in AddEdge() |
| Off-by-one expiration | Medium | StoryNode.cs | Changed > to >= |

**Tests**: 10 new integration tests added to StorySystemIntegrationTests.cs

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
C (Test Infra) ──► A (Foundation) ──┬──► B (Resources) ──► E (Enhancement) ──► G (Integration) ──┐
                                    │                                                              │
                                    └──► D (App Fixes) ────────────────────────────────────────────┤
                                                                                                   │
                                                                       H (Code Review) ◄──────────┤
                                                                                                   │
                                                                       I (Story System) ◄─────────┤
                                                                                                   │
                                                           ┌───► J (Critical Bugs) ◄───────────────┤
                                                           │                                       │
                                                           └───► F (Polish) ◄──────────────────────┘
```

**NOTE**: Batch J contains bugs that were supposedly fixed in earlier batches but still exist.
These are REGRESSIONS or INCOMPLETE FIXES that need immediate attention.

### Batch Summary

| Order | Batch | Focus | Tasks | Hours | Key Deliverable |
|-------|-------|-------|-------|-------|-----------------|
| 1 | C | Test Infra | 2 | 5-7 | Setup/Teardown, isolation |
| 2 | A | Thread Safety | 4 | 8-11 | Concurrent-safe core libraries |
| 3 | B | Resources | 3 | 5-8 | No leaks, bounded collections |
| 4 | D | App Fixes | 5 | 10-14 | Working MafiaDemo gameplay |
| 5 | E | Enhancement | 15 | 35-47 | DI, interfaces, more tests |
| 6 | G | Integration | 5 | 11-16 | AgentRouter, 47 personality rules |
| 7 | H | Code Review | 14 | 20-30 | Bug fixes from code review |
| 8 | I | Story Integration | 18 | 36-52 | GameState↔WorldState, NPCs, plots |
| **9** | **J** | **Critical Bugs** | **12** | **17-26** | **Thread-safety, memory, logic fixes** |
| 10 | F | Polish | 10 | 20-28 | Clean docs, stable release |

### Critical Files by Batch

| Batch | Files |
|-------|-------|
| C | `TestRunner.Framework/`, `TestRunner/` |
| A | `CommonMiddleware.cs`, `Agent.cs`, `AgentRouter.cs` |
| B | `GameEngine.cs`, `RulesEngineCore.cs` |
| D | `Rules/GameRulesEngine*.cs`, `MafiaAgents.cs`, test files |
| E | Various core library files |
| G | `GameEngine.cs`, `GameRulesEngine.Setup.cs` |
| H | `GameEngine.cs`, `GameRulesEngine.cs`, `MissionSystem.cs`, `PlayerAgent.cs`, `RuleContexts.cs` |
| I | `Story/WorldState.cs`, `Story/StoryGraph.cs`, `Story/IntelSystem.cs`, `MissionSystem.cs`, `Autonomous/*.cs` |
| J | `RulesEngineCore.cs`, `ServiceContainer.cs`, `AgentRouter.cs`, `AdvancedMiddleware.cs`, `CommonMiddleware.cs` |
| F | All markdown documentation |

---

**Last Updated**: 2026-02-04 (Batch J added - Critical bug fixes from deep code review)
