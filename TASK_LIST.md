# MafiaAgentSystem Task List

> **Generated**: 2026-01-31
> **Last Updated**: 2026-02-03 (Batch I: NEW - Dynamic Story System)
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
│ Layer I: DYNAMIC STORY SYSTEM            🆕 NEW        │
│   Story graph, world state, agent intel, consequences   │
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
| **I** | **Dynamic Story System** | :hourglass: **NEW** | 12 tasks | 28-40 |
| **F** | Polish | :hourglass: Pending | 9 tasks remaining | 18-26 |
| | | **TOTAL** | **70 tasks** | **142-201** |

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

## Batch I: Dynamic Story System

> **Prerequisite**: Batch H complete (stable game mechanics)
> **Why now**: Transforms static missions into emergent narrative through world state, agent intelligence, and consequence chains.

### Overview

The current mission system picks randomly from static templates, causing repetitive gameplay. This batch introduces a **Story Graph** that tracks world state, enabling:

- **Dynamic missions** constrained by location/NPC state
- **Compounding narratives** where actions have lasting consequences
- **Agent intelligence sharing** through the existing AgentRouter
- **Backtracking** to locations with changed context

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         STORY GRAPH                             │
├─────────────────────────────────────────────────────────────────┤
│  WORLD STATE                                                    │
│  ├─ Locations: state, owner, heat, history                     │
│  ├─ NPCs: relationship, status, lastInteraction                │
│  └─ Factions: hostility, territory, resources                  │
│                                                                 │
│  CONSEQUENCE ENGINE (RulesEngine)                               │
│  ├─ Mission outcomes → world state changes                     │
│  ├─ Unlock/lock future mission availability                    │
│  └─ Trigger agent communications                               │
│                                                                 │
│  AGENT INTELLIGENCE (AgentRouter)                               │
│  ├─ Agents share intel about locations/NPCs                    │
│  ├─ Information flows up/down hierarchy                        │
│  └─ Constrains mission generation                              │
│                                                                 │
│  DYNAMIC MISSION GENERATOR                                      │
│  ├─ Queries world state for available missions                 │
│  ├─ Weights by player history and relationships                │
│  └─ Creates contextual, non-repetitive content                 │
└─────────────────────────────────────────────────────────────────┘
```

---

### I-1: World State Model (3 tasks, 6-8 hours)

#### Task I-1a: Location State Tracking
**Estimated Time**: 2-3 hours
**Files**: `AgentRouting.MafiaDemo/Story/WorldState.cs` (new)

**Problem**: Locations are just strings in mission templates with no memory.

**Implementation**:
```csharp
public class Location
{
    public string Id { get; set; }
    public string Name { get; set; }
    public LocationState State { get; set; } = LocationState.Neutral;
    public string? Owner { get; set; }  // "player", "rival-x", null
    public int LocalHeat { get; set; }  // Police attention at this location
    public List<string> History { get; set; } = new();  // Event IDs
    public Dictionary<string, int> NPCsPresent { get; set; } = new();
}

public enum LocationState { Friendly, Neutral, Hostile, Contested, Destroyed }
```

**Subtasks**:
- [ ] Create Location model with state tracking
- [ ] Create LocationRegistry with CRUD operations
- [ ] Seed initial locations from existing mission templates
- [ ] Add location state change events

---

#### Task I-1b: NPC Relationship System
**Estimated Time**: 2-3 hours
**Files**: `AgentRouting.MafiaDemo/Story/NPCSystem.cs` (new)

**Problem**: Mission targets are anonymous ("the shopkeeper") with no persistence.

**Implementation**:
```csharp
public class NPC
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Role { get; set; }  // "shopkeeper", "informant", "rival"
    public string LocationId { get; set; }
    public int RelationshipToPlayer { get; set; }  // -100 to +100
    public NPCStatus Status { get; set; } = NPCStatus.Active;
    public string? LastMissionId { get; set; }
    public List<string> KnownByAgents { get; set; } = new();
}

public enum NPCStatus { Active, Intimidated, Allied, Hostile, Dead, Fled, Imprisoned }
```

**Subtasks**:
- [ ] Create NPC model with relationship tracking
- [ ] Create NPCRegistry with lookup by location/role
- [ ] Generate NPCs dynamically with persistent names
- [ ] Track NPC status changes from mission outcomes

---

#### Task I-1c: Faction Territory Tracking
**Estimated Time**: 2 hours
**Files**: `AgentRouting.MafiaDemo/Story/FactionSystem.cs` (new)

**Problem**: Rival families exist but don't have persistent territory claims.

**Implementation**:
```csharp
public class Faction
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int HostilityToPlayer { get; set; }
    public List<string> ControlledLocations { get; set; } = new();
    public int Resources { get; set; }
    public List<string> KnownNPCs { get; set; } = new();  // NPCs loyal to this faction
}
```

**Subtasks**:
- [ ] Create Faction model extending existing RivalFamily
- [ ] Track territory claims per faction
- [ ] Link NPCs to factions
- [ ] Add faction-based mission constraints

---

### I-2: Story Graph & Consequences (3 tasks, 7-10 hours)

#### Task I-2a: Story Graph Data Structure
**Estimated Time**: 2-3 hours
**Files**: `AgentRouting.MafiaDemo/Story/StoryGraph.cs` (new)

**Problem**: No way to track causal relationships between events.

**Implementation**:
```csharp
public class StoryGraph
{
    public Dictionary<string, StoryNode> Nodes { get; } = new();
    public List<StoryEdge> Edges { get; } = new();

    public void RecordEvent(StoryEvent evt);
    public IEnumerable<StoryNode> GetUnlockedNodes(WorldState state);
    public IEnumerable<string> GetAvailablePlots();
}

public class StoryNode
{
    public string Id { get; set; }
    public StoryNodeType Type { get; set; }  // Event, Mission, Consequence
    public Func<WorldState, bool> UnlockCondition { get; set; }
    public List<string> Prerequisites { get; set; } = new();
}

public class StoryEdge
{
    public string FromNodeId { get; set; }
    public string ToNodeId { get; set; }
    public EdgeType Type { get; set; }  // Unlocks, Blocks, Triggers
}
```

**Subtasks**:
- [ ] Implement StoryGraph with node/edge management
- [ ] Add unlock condition evaluation
- [ ] Create graph traversal for available missions
- [ ] Add plot thread tracking

---

#### Task I-2b: Consequence Engine Rules
**Estimated Time**: 3-4 hours
**Files**: `AgentRouting.MafiaDemo/Story/ConsequenceRules.cs` (new)

**Problem**: Mission outcomes don't affect future gameplay.

**Implementation**: Use existing RulesEngine to define consequence rules:
```csharp
// Example consequences
engine.AddRule("INTIMIDATION_SUCCESS_RELATIONSHIP",
    ctx => ctx.MissionType == MissionType.Intimidation && ctx.Success,
    ctx => {
        ctx.TargetNPC.Status = NPCStatus.Intimidated;
        ctx.TargetNPC.RelationshipToPlayer -= 20;
        ctx.Location.State = LocationState.Friendly;
    });

engine.AddRule("INTIMIDATION_FAIL_RETALIATION",
    ctx => ctx.MissionType == MissionType.Intimidation && !ctx.Success,
    ctx => {
        ctx.TargetNPC.Status = NPCStatus.Hostile;
        ctx.StoryGraph.UnlockNode("retaliation-" + ctx.TargetNPC.Id);
    });
```

**Subtasks**:
- [ ] Create ConsequenceContext for rule evaluation
- [ ] Define 15-20 consequence rules for each mission type
- [ ] Wire consequences to fire after mission completion
- [ ] Add consequence logging for story recap

---

#### Task I-2c: Plot Thread System
**Estimated Time**: 2-3 hours
**Files**: `AgentRouting.MafiaDemo/Story/PlotThreads.cs` (new)

**Problem**: No multi-mission story arcs.

**Implementation**:
```csharp
public class PlotThread
{
    public string Id { get; set; }
    public string Title { get; set; }  // "The Tattaglia Expansion"
    public PlotState State { get; set; } = PlotState.Available;
    public List<string> RequiredMissionIds { get; set; } = new();
    public List<string> CompletedMissionIds { get; set; } = new();
    public int Priority { get; set; }
    public Func<WorldState, bool> ActivationCondition { get; set; }
}

public enum PlotState { Available, Active, Completed, Failed, Abandoned }
```

**Subtasks**:
- [ ] Create PlotThread model
- [ ] Define 5-8 initial plot threads (revenge, expansion, betrayal, etc.)
- [ ] Wire plot activation to world state changes
- [ ] Add plot-specific mission generation

---

### I-3: Agent Intelligence System (3 tasks, 8-12 hours)

#### Task I-3a: Intel Message Types
**Estimated Time**: 2-3 hours
**Files**: `AgentRouting.MafiaDemo/Story/IntelSystem.cs` (new)

**Problem**: Agents don't share information about the world.

**Implementation**:
```csharp
public class IntelMessage : AgentMessage
{
    public IntelType Type { get; set; }
    public string SubjectId { get; set; }  // Location or NPC ID
    public Dictionary<string, object> IntelData { get; set; } = new();
    public int Reliability { get; set; }  // 0-100
    public DateTime Timestamp { get; set; }
}

public enum IntelType
{
    LocationStatus,    // "Tony's is being watched"
    NPCMovement,       // "Informant fled to Brooklyn"
    FactionActivity,   // "Tattaglias moving on the docks"
    ThreatWarning,     // "Feds planning raid"
    Opportunity        // "Rival capo is vulnerable"
}
```

**Subtasks**:
- [ ] Create IntelMessage extending AgentMessage
- [ ] Add intel categories matching world state
- [ ] Create IntelRegistry for storing received intel
- [ ] Wire intel to mission constraint system

---

#### Task I-3b: Agent Intel Generation
**Estimated Time**: 3-4 hours
**Files**: `AgentRouting.MafiaDemo/Autonomous/*.cs` (modify)

**Problem**: Autonomous agents don't proactively share information.

**Implementation**: Modify existing agents to generate intel:
```csharp
// In SoldierAgent
public override async Task<AgentMessage?> GenerateIntelAsync(WorldState state)
{
    // Soldiers notice street-level changes
    var hotLocations = state.Locations
        .Where(l => l.LocalHeat > 50)
        .ToList();

    if (hotLocations.Any())
    {
        return new IntelMessage
        {
            Type = IntelType.LocationStatus,
            SubjectId = hotLocations.First().Id,
            IntelData = new { Heat = hotLocations.First().LocalHeat },
            Category = "Intelligence"
        };
    }
    return null;
}
```

**Subtasks**:
- [ ] Add GenerateIntelAsync to AutonomousAgent base
- [ ] Implement intel generation for each agent type (role-appropriate)
- [ ] Add intel generation to turn processing
- [ ] Route intel through AgentRouter hierarchy

---

#### Task I-3c: Intel-Driven Constraints
**Estimated Time**: 3-4 hours
**Files**: `AgentRouting.MafiaDemo/Story/MissionConstraints.cs` (new)

**Problem**: Mission availability doesn't reflect agent knowledge.

**Implementation**:
```csharp
public class MissionConstraintEngine
{
    private readonly IntelRegistry _intel;
    private readonly WorldState _world;

    public bool IsMissionAvailable(Mission mission, PlayerCharacter player)
    {
        // Check if location is accessible
        var location = _world.GetLocation(mission.LocationId);
        if (location.State == LocationState.Hostile && !player.HasCombatSkill)
            return false;

        // Check if we have intel suggesting danger
        var recentIntel = _intel.GetRecent(mission.LocationId, TimeSpan.FromWeeks(2));
        if (recentIntel.Any(i => i.Type == IntelType.ThreatWarning))
            return false;  // Or mark as high-risk

        return true;
    }
}
```

**Subtasks**:
- [ ] Create MissionConstraintEngine
- [ ] Define constraint rules based on intel types
- [ ] Integrate with MissionGenerator
- [ ] Add "why unavailable" explanations for player

---

### I-4: Dynamic Mission Generator (3 tasks, 7-10 hours)

#### Task I-4a: Context-Aware Mission Generation
**Estimated Time**: 3-4 hours
**Files**: `AgentRouting.MafiaDemo/MissionSystem.cs` (modify)

**Problem**: Missions are randomly selected from static templates.

**Implementation**:
```csharp
public class DynamicMissionGenerator
{
    public Mission GenerateMission(PlayerCharacter player, WorldState world, StoryGraph graph)
    {
        // Get available locations (not hostile, not recently visited)
        var availableLocations = world.Locations
            .Where(l => l.State != LocationState.Hostile)
            .Where(l => !player.RecentLocations.Contains(l.Id))
            .ToList();

        // Get NPCs needing attention (relationship changed, status changed)
        var relevantNPCs = world.NPCs
            .Where(n => n.LastMissionId != null)
            .Where(n => ShouldRevisit(n, player))
            .ToList();

        // Check active plot threads for priority missions
        var plotMissions = graph.GetActivePlots()
            .SelectMany(p => p.GetNextMissions(world))
            .ToList();

        // Weight and select
        return SelectBestMission(availableLocations, relevantNPCs, plotMissions, player);
    }
}
```

**Subtasks**:
- [ ] Refactor MissionGenerator to use WorldState
- [ ] Add location-based mission filtering
- [ ] Add NPC-based mission generation
- [ ] Integrate plot thread missions

---

#### Task I-4b: Mission Variety Expansion
**Estimated Time**: 2-3 hours
**Files**: `AgentRouting.MafiaDemo/Story/MissionTemplates.cs` (new)

**Problem**: Only 4-6 templates per mission type causes repetition.

**Implementation**: Expand templates with dynamic elements:
```csharp
public class MissionTemplateEngine
{
    public Mission GenerateFromTemplate(MissionTemplate template, Location loc, NPC npc)
    {
        return new Mission
        {
            Title = template.TitleFormat
                .Replace("{npc}", npc.Name)
                .Replace("{location}", loc.Name),
            Description = template.DescriptionFormat
                .Replace("{reason}", GetContextualReason(npc, loc)),
            // ... dynamic risk/reward based on state
        };
    }
}
```

**Subtasks**:
- [ ] Create parameterized mission templates
- [ ] Add 10+ templates per mission type
- [ ] Generate contextual descriptions from world state
- [ ] Add unique missions for plot threads

---

#### Task I-4c: Anti-Repetition System
**Estimated Time**: 2-3 hours
**Files**: `AgentRouting.MafiaDemo/Story/MissionHistory.cs` (new)

**Problem**: Same mission can appear multiple weeks in a row.

**Implementation**:
```csharp
public class MissionHistoryTracker
{
    private readonly Queue<string> _recentMissionTypes = new(capacity: 5);
    private readonly Dictionary<string, int> _locationVisits = new();
    private readonly Dictionary<string, int> _npcInteractions = new();

    public float GetRepetitionPenalty(Mission candidate)
    {
        float penalty = 0;

        // Penalize recently used mission types
        if (_recentMissionTypes.Contains(candidate.Type.ToString()))
            penalty += 0.3f;

        // Penalize recently visited locations
        if (_locationVisits.TryGetValue(candidate.LocationId, out var visits))
            penalty += 0.1f * visits;

        return penalty;
    }
}
```

**Subtasks**:
- [ ] Track recent mission types, locations, NPCs
- [ ] Calculate repetition penalties
- [ ] Weight mission selection by freshness
- [ ] Ensure minimum variety guarantees

---

### I-5: Integration & Testing (2 tasks, 4-6 hours)

#### Task I-5a: Wire Systems Together
**Estimated Time**: 2-3 hours
**Files**: `AgentRouting.MafiaDemo/Game/GameEngine.cs` (modify)

**Subtasks**:
- [ ] Initialize WorldState at game start
- [ ] Initialize StoryGraph with seed nodes
- [ ] Hook consequence engine to mission completion
- [ ] Add intel processing to turn loop
- [ ] Replace old MissionGenerator with DynamicMissionGenerator

---

#### Task I-5b: Story System Tests
**Estimated Time**: 2-3 hours
**Files**: `Tests/MafiaDemo.Tests/StorySystemTests.cs` (new)

**Subtasks**:
- [ ] Test Location state transitions
- [ ] Test NPC relationship changes from missions
- [ ] Test consequence rule firing
- [ ] Test intel routing through agents
- [ ] Test mission variety over 52-week simulation
- [ ] Test plot thread activation and completion

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
                                                                       F (Polish) ◄────────────────┘
```

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
| 8 | **I** | **Story System** | **12** | **28-40** | **Dynamic narrative, agent intel** |
| 9 | F | Polish | 10 | 20-28 | Clean docs, stable release |

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
| F | All markdown documentation |

---

**Last Updated**: 2026-02-03 (Batch I: NEW - Dynamic Story System with 12 tasks)
