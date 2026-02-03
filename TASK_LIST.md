# MafiaAgentSystem Task List

> **Generated**: 2026-01-31
> **Last Updated**: 2026-02-03 (Batch H: NEW - Code Review Bug Fixes)
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
│ Layer H: CODE REVIEW BUG FIXES (NEW)     ◄── NEXT       │
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
| **H** | **Code Review Bug Fixes** | :construction: **NEW** | 14 tasks | 20-30 |
| **F** | Polish | :hourglass: Pending | 9 tasks remaining | 18-26 |
| | | **TOTAL** | **58 tasks** | **114-161** |

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

## Batch H: Code Review Bug Fixes (NEW)

> **Prerequisite**: Batch G complete
> **Priority**: HIGH - Bugs found during comprehensive code review
> **Source**: Code review 2026-02-03 (see MAFIA_DEMO_CODE_REVIEW.md)
> **Full Report**: `/MAFIA_DEMO_CODE_REVIEW.md`

### H-1: Fix Heat Balance (Critical - Game Unwinnable)
**Priority**: CRITICAL
**Estimated Time**: 2-3 hours
**Files**: `Game/GameEngine.cs:509-541`, `Game/GameEngine.cs:901-911`

**Problem**: Heat generation vs decay is fundamentally imbalanced:
- Territory heat generation: 5 + 10 + 8 = **23 heat/week** from collections
- Natural heat decay: **5/week** in `UpdateGameState()`
- Net: +18 heat/week even without any actions

In testing, game ended at week 21 with uncontrollable heat despite constant bribery and laying low.

**Subtasks**:
- [ ] Increase natural heat decay from 5 to 10-15/week
- [ ] OR reduce territory base heat generation
- [ ] Add tests for heat balance over extended gameplay
- [ ] Verify game is winnable with balanced play

---

### H-2: Fix Event Time Using Real Time Instead of Game Time
**Priority**: CRITICAL
**Estimated Time**: 2-3 hours
**File**: `Rules/GameRulesEngine.cs:133-139`

**Problem**: Events check `DateTime.UtcNow.AddMinutes(-5)` but game progresses in weeks:
```csharp
var recentPoliceRaid = _state.EventLog
    .Where(e => e.Type == "PoliceRaid")
    .Any(e => e.Timestamp > DateTime.UtcNow.AddMinutes(-5));
```

In instant mode, all weeks occur in same second, breaking event exclusivity logic.

**Subtasks**:
- [ ] Add `GameWeek` property to `GameEvent` class
- [ ] Change event checks to use game weeks: `_state.Week - e.GameWeek < 3`
- [ ] Update all event timestamp checks throughout codebase
- [ ] Add tests for event timing in instant mode

---

### H-3: Fix CHAIN_HIT_TO_WAR Rule Exception
**Priority**: HIGH
**Estimated Time**: 1-2 hours
**File**: `Rules/GameRulesEngine.Setup.cs:1140-1147`

**Problem**: Uses `First()` without null check - can throw `InvalidOperationException`:
```csharp
var rival = ctx.State.RivalFamilies.Values.First(r => r.Hostility > 80);
```

**Subtasks**:
- [ ] Replace `First()` with `FirstOrDefault()` and add null check
- [ ] Add test for scenario where no rival has hostility > 80

---

### H-4: Fix Rival Hostility Going Negative
**Priority**: HIGH
**Estimated Time**: 1 hour
**File**: `Game/GameEngine.cs:892-896`

**Problem**: Hostility can go negative due to improper clamping:
```csharp
if (rival.Hostility > 0)
{
    rival.Hostility -= Random.Shared.Next(1, 3); // Can go -1 if hostility was 1
}
```

**Subtasks**:
- [ ] Change to: `rival.Hostility = Math.Max(0, rival.Hostility - Random.Shared.Next(1, 3));`
- [ ] Add test verifying hostility stays >= 0

---

### H-5: Fix MissionEvaluator Applying Rules Twice
**Priority**: HIGH
**Estimated Time**: 1-2 hours
**File**: `MissionSystem.cs:524-535`

**Problem**: `EvaluateAll()` called twice on same context, doubling modifiers:
```csharp
_rules.EvaluateAll(context);  // First application
// ... roll for success ...
_rules.EvaluateAll(context);  // Second application - doubles heat penalties!
```

**Subtasks**:
- [ ] Remove second `EvaluateAll()` call
- [ ] OR split into pre-roll and post-roll rule sets
- [ ] Add test verifying modifiers applied exactly once

---

### H-6: Fix PlayerAgent Decision Trace Field Inconsistency
**Priority**: MEDIUM
**Estimated Time**: 1 hour
**File**: `PlayerAgent.cs:212, 345`

**Problem**: Two decision methods use different fields for rejection check:
```csharp
// DecideMission (line 212):
var accept = !topRule.Id.Contains("REJECT");  // Correct - ID is uppercase

// DecideMissionWithTrace (line 345):
var accept = !topRule.RuleName.Contains("REJECT");  // Wrong - RuleName is mixed case
```

**Subtasks**:
- [ ] Change line 345 to use `RuleId` instead of `RuleName`
- [ ] Add `StringComparison.OrdinalIgnoreCase` for safety
- [ ] Add test comparing both methods return same decision

---

### H-7: Fix CONSEQUENCE_VULNERABLE Empty Territory Check
**Priority**: MEDIUM
**Estimated Time**: 1 hour
**File**: `Rules/GameRulesEngine.Setup.cs:101-111`

**Problem**: Calls `First()` without checking if territories exist:
```csharp
var territory = ctx.State.Territories.Values.First(); // Throws if empty
```

**Subtasks**:
- [ ] Add empty check before `First()`
- [ ] Add test for scenario with no territories

---

### H-8: Fix RivalStrategyContext.ShouldAttack Logic
**Priority**: MEDIUM
**Estimated Time**: 1 hour
**File**: `Rules/RuleContexts.cs:292`

**Problem**: Logic appears inverted - rivals attack when player is NOT distracted:
```csharp
public bool ShouldAttack => RivalIsStronger && PlayerIsWeak && !PlayerIsDistracted;
```

Rivals should attack when player IS distracted (focused on law enforcement).

**Subtasks**:
- [ ] Clarify intended behavior with game design
- [ ] Either fix logic or rename property to reflect actual meaning

---

### H-9: Fix Currency Symbol Display in Career Mode
**Priority**: LOW
**Estimated Time**: 30 minutes
**File**: `AutonomousPlaythrough.cs:265-267`

**Problem**: Uses `:C0` format which produces "¤" in non-US locales:
```csharp
Console.WriteLine($"║    Money: {missionResult.MoneyGained:C0}");
// Displays: +¤100 instead of +$100
```

**Subtasks**:
- [ ] Use explicit format: `+${value:N0}` instead of `:C0`

---

### H-10: Add Agent Action Coordination
**Priority**: MEDIUM
**Estimated Time**: 2-3 hours
**File**: `Game/GameEngine.cs`

**Problem**: Multiple agents can bribe simultaneously, each costing $10,000 but only marginally more effective. No coordination.

**Subtasks**:
- [ ] Add "already bribed this week" flag to prevent duplicate bribes
- [ ] OR pool bribe resources across agents
- [ ] Add test for coordinated agent behavior

---

### H-11: Consolidate Defeat Condition Logic
**Priority**: MEDIUM
**Estimated Time**: 1-2 hours
**Files**: `Game/GameEngine.cs:913-939`, `Rules/GameRulesEngine.Setup.cs:64-73`

**Problem**: Duplicate defeat logic with inconsistent thresholds:
| Condition | GameEngine.cs | GameRulesEngine.Setup.cs |
|-----------|---------------|--------------------------|
| Reputation | <= 10 | <= 5 |

**Subtasks**:
- [ ] Consolidate all defeat checks to single location (GameEngine)
- [ ] Remove duplicate rules from GameRulesEngine
- [ ] Document canonical defeat conditions

---

### H-12: Improve Event Log Eviction Performance
**Priority**: LOW
**Estimated Time**: 1 hour
**File**: `Game/GameEngine.cs:944-948`

**Problem**: `RemoveAt(0)` is O(n) for List:
```csharp
while (_state.EventLog.Count >= MaxEventLogSize)
{
    _state.EventLog.RemoveAt(0);  // Shifts all elements
}
```

**Subtasks**:
- [ ] Change EventLog to `Queue<GameEvent>` for O(1) dequeue
- [ ] OR implement circular buffer pattern

---

### H-13: Review Victory Condition Achievability
**Priority**: MEDIUM
**Estimated Time**: 2-3 hours
**File**: `Game/GameEngine.cs:934-939`

**Problem**: Victory requires all conditions at week 52+:
```csharp
if (_state.Week >= 52 && _state.FamilyWealth >= 1000000 && _state.Reputation >= 80)
```

With heat balance issues (H-1), this is nearly impossible to achieve.

**Subtasks**:
- [ ] After H-1 fix, verify victory is achievable
- [ ] Consider reducing requirements or extending timeline
- [ ] Add integration test that simulates winnable game

---

### H-14: Add Null Safety to Rival Lookups
**Priority**: LOW
**Estimated Time**: 1 hour
**File**: `Game/GameEngine.cs`

**Problem**: Properties like `MostHostileRival` and `WeakestRival` return null when no rivals match, but callers don't always check.

**Subtasks**:
- [ ] Audit all usages of MostHostileRival, WeakestRival
- [ ] Add null-conditional operators where needed
- [ ] Add tests for empty rival scenarios

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
| 7 | **H** | **Code Review** | **14** | **20-30** | **Bug fixes from code review** |
| 8 | F | Polish | 10 | 20-28 | Clean docs, stable release |

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
| F | All markdown documentation |

---

**Last Updated**: 2026-02-03 (Batch H added: 14 code review bug fixes)
