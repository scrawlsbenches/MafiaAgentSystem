# Execution Plan: MafiaAgentSystem Build & MVP

> **Created**: 2026-01-31
> **Last Updated**: 2026-02-08 (Batch J COMPLETE - all thread-safety, memory, and logic bugs resolved)
> **Constraint**: Zero 3rd party libraries (only .NET SDK)
> **Goal**: Compiling codebase → Test baseline → MVP game → Production Quality

---

## Current Execution State (Updated 2026-02-08)

### Batch Structure (Layered Approach)

After Phase 5, tasks were reorganized into a **layered batch structure** to minimize churn.
See `TASK_LIST.md` for full details.

```
┌─────────────────────────────────────────────────────────────┐
│ Layer F: POLISH                       🔵 NEXT               │
│   Documentation, code cleanup                                │
├─────────────────────────────────────────────────────────────┤
│ Layer J: CRITICAL BUG FIXES          ✅ COMPLETE             │
│   Thread-safety, memory leaks, logic errors (2026-02-08)     │
├─────────────────────────────────────────────────────────────┤
│ Layer I: STORY SYSTEM INTEGRATION    ✅ COMPLETE             │
│   GameState↔WorldState sync, NPCs, plots, missions           │
├─────────────────────────────────────────────────────────────┤
│ Layer H: CODE REVIEW BUG FIXES        ✅ COMPLETE            │
│   Heat balance, event timing, null safety, defeat logic      │
├─────────────────────────────────────────────────────────────┤
│ Layer G: CRITICAL INTEGRATION         ✅ COMPLETE            │
│   AgentRouter integration, 47 personality rules              │
├─────────────────────────────────────────────────────────────┤
│ Layer E: ENHANCEMENT                  ✅ COMPLETE            │
│   DI extensions, interface extraction, new tests             │
├─────────────────────────────────────────────────────────────┤
│ Layer D: APPLICATION FIXES            ✅ COMPLETE            │
│   MafiaDemo gameplay bugs                                    │
├─────────────────────────────────────────────────────────────┤
│ Layer B: RESOURCE STABILITY           ✅ COMPLETE            │
│   Memory leaks, unbounded growth                             │
├─────────────────────────────────────────────────────────────┤
│ Layer A: FOUNDATION                   ✅ COMPLETE            │
│   Thread safety in core libraries                            │
├─────────────────────────────────────────────────────────────┤
│ Layer C: TEST INFRASTRUCTURE          ✅ COMPLETE            │
│   Setup/Teardown, state isolation                            │
└─────────────────────────────────────────────────────────────┘
```

| Batch | Layer | Status | Tasks | Completed |
|-------|-------|--------|-------|-----------|
| **C** | Test Infra | ✅ Complete | 2 | 2026-02-02 |
| **A** | Foundation | ✅ Complete | 4 | 2026-02-02 |
| **B** | Resources | ✅ Complete | 3 | 2026-02-03 |
| **D** | App Fixes | ✅ Complete | 5 | 2026-02-03 |
| **E** | Enhancement | ✅ Complete | 15 | 2026-02-03 |
| **G** | Critical Integration | ✅ Complete | 5 | 2026-02-03 |
| **H** | Code Review Fixes | ✅ Complete | 14 | 2026-02-03 |
| **I** | Story System Integration | ✅ Complete | 16 | 2026-02-03 |
| **J** | Critical Bug Fixes | ✅ Complete | 12 | 2026-02-08 |
| **F** | Polish | 🔵 Next | 10 | F-3 complete |

**Test count: 2,268 tests (346 RulesEngine + 823 AgentRouting + 808 MafiaDemo + 291 other)**

---

## Recent Activity Log

### Batch J: Critical Bug Fixes (2026-02-04 → 2026-02-08) ✅ COMPLETE

**Source**: Deep code review (2026-02-04) identified 18 issues in `CODE_REVIEW_BUGS.txt`.
**Commits**: `3cb8d68` (8 fixes), `747da6a` (7 fixes + 14 regression tests), session 2026-02-08 (3 fixes)

**Thread-Safety Fixes (P0/P1)**:
- [x] J-1a: TrackPerformance race — `AddOrUpdate` now creates new metrics objects
- [x] J-1b: ServiceContainer singleton — double-checked locking with `_singletonLock`
- [x] J-1c: AgentRouter.RegisterAgent — `_agentLock` around all collection access
- [x] J-1d: ABTestingMiddleware — `Random.Shared` replaces non-thread-safe instance
- [x] J-NEW-1: AgentBase.Status — computed from `_activeMessages` instead of cached

**Logic Fixes**:
- [x] J-2a: CompositeRule triple evaluation reduced to 1x
- [x] J-2b: ImmutableRulesEngine validation (null, empty Id/Name, duplicates)
- [x] J-2c: MessageQueueMiddleware async void — try-catch + TCS orphan protection

**Memory/Resource Fixes**:
- [x] J-3a: MetricsMiddleware — bounded circular buffer (max 10,000 samples)
- [x] J-3c: DistributedTracingMiddleware — bounded ConcurrentQueue (max 10,000 spans)

**Additional Fixes (from commit `747da6a`)**:
- [x] CR-01: ImmutableRulesEngine metrics isolation between instances
- [x] CR-04: AgentRouter.OnUnroutableMessage dead-letter event
- [x] CR-05: ExecuteAsync enforces MaxRulesToExecute across async rules
- [x] CR-06: ExecuteAsync tracks performance metrics
- [x] CR-07: AsyncRuleBuilder.Build() throws RuleValidationException
- [x] CR-15: ServiceContainer/ServiceScope dispose aggregates exceptions
- [x] 43 async void test methods → async Task

**Deferred to Batch F**: J-3b (StoryGraph unbounded - low priority), J-4a (SystemClock static - mitigated), J-4b (sanitization - educational code)

**Test count after Batch J: 2,268 tests, 0 failures, 2 skipped.**

---

### Batch I: Story System Integration (2026-02-03) ✅ COMPLETE

**Source**: Story System fully implemented in `AgentRouting.MafiaDemo/Story/` (28 files)
**Design Review**: `AgentRouting.MafiaDemo/Story/GAME_REVIEW.md`

**Goal**: Integrate existing Story System with MafiaDemo game mechanics.

**Completed (14 tasks)**:
- [x] I-1: Story System Implementation (reference - already complete)
- [x] I-2a: GameState ↔ WorldState Bridge (`Game/GameWorldBridge.cs`)
- [x] I-2b: WorldState Initialization in GameEngine
- [x] I-2c: Week Counter Consolidation (LinkedWorldState delegates to WorldState.CurrentWeek)
- [x] I-3a: Mission Target NPC References (NPCId, LocationId properties)
- [x] I-3b: Relationship Updates on Mission Completion
- [x] I-3c: NPC Status Effects on Missions
- [x] I-4a: Plot Thread State Machine (Dormant→Available→Active→Completed)
- [x] I-4b: Plot Mission Priority (+20 active, +10 available weighting)
- [x] I-4c: Plot Completion Rewards
- [x] I-5a: HybridMissionGenerator Integration
- [x] I-5b: Apply ConsequenceRules After Missions (MissionConsequenceHandler.ApplyConsequenceRules)
- [x] I-5c: Intel Recording for Information Missions (MissionConsequenceHandler.RecordIntelFromMission)
- [x] I-7a-d: Integration Tests (44 tests in StorySystemIntegrationTests.cs)
  - GameWorldBridge (5), GameState/Week (3), HybridMissionGenerator (5), MissionAdapter (3)
  - PlotThread (4), MissionConsequenceHandler (11), GameEngine (6), MissionHistory (3)
  - **PlayerAgent Story System E2E** (4 tests: consequence rules, intel recording, backward compat)

**Remaining (2 tasks - Moved to Batch F)**:
- [ ] F-2a: Basic NPC Conversation Command (was I-6a)
- [ ] F-2b: Conversation Results Integration (was I-6b)

> **Note**: Conversation tasks moved to Batch F as they add new features rather than integrate existing Story System components.

**Integration Components Verified**:
| Component | File | Status |
|-----------|------|--------|
| GameWorldBridge | `Game/GameWorldBridge.cs` | ✅ Bidirectional sync |
| HybridMissionGenerator | `Story/Integration/HybridMissionGenerator.cs` | ✅ Story + Legacy combined |
| MissionAdapter | `Story/Integration/MissionAdapter.cs` | ✅ NPC/Location modifiers |
| MissionConsequenceHandler | `MissionSystem.cs` | ✅ Relationship + ConsequenceRules + Intel |
| GameEngine Integration | `Game/GameEngine.cs:346-525` | ✅ InitializeStorySystem() |
| Week Counter | `GameEngine.cs:67` | ✅ LinkedWorldState delegation |
| PlayerAgent Story Props | `PlayerAgent.cs:75-110` | ✅ WorldState, StoryGraph, IntelRegistry |

---

### Batch H: Code Review Bug Fixes (2026-02-03) ✅ COMPLETE

**Source**: Comprehensive code review of MafiaDemo (see `/MAFIA_DEMO_CODE_REVIEW.md`)

**Critical Fix - Game Now Winnable**:
- Heat balance was fundamentally broken (23 heat/week generation vs 5 decay = unwinnable)
- Fixed: Territory heat reduced (23→11/week), decay increased (5→8/week)
- Verified: Game reaches victory at week 52 with $2.4M wealth and 91% reputation

**All 14 tasks complete**:
- [x] H-1: Heat balance (CRITICAL) - game now winnable
- [x] H-2: Event timing uses game weeks instead of real time
- [x] H-3: CHAIN_HIT_TO_WAR null safety (FirstOrDefault with null check)
- [x] H-4: Rival hostility clamping (Math.Max(0,...) prevents negative values)
- [x] H-5: MissionEvaluator duplicate rule application removed
- [x] H-6: PlayerAgent decision trace field consistency (RuleId not RuleName)
- [x] H-7: CONSEQUENCE_VULNERABLE null safety
- [x] H-8: RivalStrategyContext.ShouldAttack - logic is intentionally correct, added XML docs
- [x] H-9: Currency symbol display ($ instead of ¤)
- [x] H-10: BribedThisWeek flag for agent coordination (3 tests added)
- [x] H-11: Defeat/Victory constants in GameState (single source of truth)
- [x] H-12: EventLog changed to Queue<GameEvent> for O(1) eviction
- [x] H-13: Victory achievability verified (3 integration tests)
- [x] H-14: Null-safe rival helpers (HasRivals, MaxRivalHostility, etc.)

### Batch G: Critical Integration (2026-02-03) ✅

- [x] G-1: AgentRouter full integration with RouteAgentActionAsync
- [x] G-2: Added 23 personality-driven rules (47 total agent rules)
- [x] G-3-5: Moved to Batch F (documentation polish)

---

## Completed Phases (Historical)

### Phase 1: MVP Foundation ✅
- [x] .NET SDK 8.0.122 installed ✅
- [x] Build succeeds (0 errors in core + MafiaDemo) ✅
- [x] Test baseline: 39 tests, all passing ✅
- [x] MVP game verified: All 8 scenarios run ✅
- [x] Centralized timing (GameTimingOptions.cs) ✅

### Phase 2: Core Library Hardening ✅
- [x] P0-4: Fix null reference in AgentRouter ✅
- [x] P1-1: Thread safety for RulesEngineCore ✅
- [x] P1-2: ExecuteAsync with Cancellation ✅
- [x] P1-3: Cache Sorted Rules ✅
- [x] P1-4: Cache Eviction for CachingMiddleware ✅
- [x] P1-5: Extract Configuration Constants ✅
- [x] P1-6: Rule Validation on Registration ✅
- [x] P1-7: Async Rule Support ✅
- [x] P1-8: Standardize DateTime Usage ✅

### Phase 3: Testing for New Features ✅
- [x] P3-1: Concurrency tests for thread safety (4 tests) ✅
- [x] Async execution and cancellation tests (14 tests) ✅
- [x] Validation and cache tests (10 tests) ✅

**Test count: 39 → 67 (all passing)**

### Phase 4: MafiaDemo Completion ✅
- [x] P2-1: Architecture documentation (ARCHITECTURE.md) ✅
- [x] P2-2 through P2-5: Agent hierarchy - **ALREADY IMPLEMENTED** ✅
- [x] P2-8: Interactive game loop - **ALREADY IMPLEMENTED** ✅
- [x] Wire RulesBasedGameEngine to MafiaGameEngine ✅
- [x] Replace hardcoded agent decisions with rules ✅
- [x] P2-10: Add integration tests (22 tests) ✅

**Test count: 67 → 89 (all passing)**

---

## Execution Batches

### BATCH 0: Environment Setup
**Status**: PENDING
**Agents**: Bash (sequential)
**Duration**: ~5 minutes

**Steps**:
1. Install .NET SDK 8.0
2. Verify installation

**Gate G0**: `dotnet --version` returns 8.x

**Commands**:
```bash
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
chmod 1777 /tmp
apt-get update -o Dir::Etc::sourcelist="sources.list.d/microsoft-prod.list" \
  -o Dir::Etc::sourceparts="-" -o APT::Get::List-Cleanup="0"
apt-get install -y dotnet-sdk-8.0
dotnet --version
```

---

### BATCH 1: Build Assessment
**Status**: PENDING
**Agents**: Bash (parallel where possible)
**Duration**: ~10 minutes

**Steps**:
1. Run `dotnet build` and capture output
2. Count and categorize errors
3. Identify which projects build vs fail

**Gate G1**: Error list documented

**Note**: Skip NuGet restore for xUnit projects - use custom TestRunner only

---

### BATCH 2: Critical Fixes
**Status**: PENDING
**Agents**: Orchestrator + verification
**Duration**: 2-3 hours

**Known Issues to Fix**:
1. GameEngine.cs - AutonomousAgent instantiation (abstract class)
2. Type mismatches between documented and actual APIs
3. Null reference suppressions

**Gate G2**: `dotnet build` succeeds with 0 errors

---

### BATCH 3: Test Baseline
**Status**: PENDING
**Agents**: Bash
**Duration**: ~15 minutes

**Steps**:
1. Run custom TestRunner (zero dependencies)
2. Document pass/fail counts
3. Identify failing tests

**Gate G3**: Baseline documented

**Command**:
```bash
dotnet run --project Tests/TestRunner/
```

---

### BATCH 4: MVP Game Loop
**Status**: PENDING
**Agents**: General-purpose for implementation
**Duration**: 2-4 hours

**MVP Definition**:
- Game starts and shows status
- Player can issue ONE command ("status" or "collect")
- ONE response from system
- Turn counter advances
- Can exit cleanly

**Gate G4**: MVP runs interactively

---

### Phase 5: Architectural Improvements ✅

**Session: 2026-02-01**
**Status: COMPLETE**

Completed:
- [x] Router Consolidation - Merged `MiddlewareAgentRouter` and `AgentRouterWithMiddleware` into `AgentRouter`
  - Added native middleware support to AgentRouter
  - Added `HasMiddleware` property to MiddlewarePipeline
  - Added `CallbackMiddleware`, `ConditionalMiddleware`, and pipeline extensions
  - Renamed `AgentRouterWithMiddleware.cs` to `AgentRouterBuilder.cs`
  - Updated all usages across codebase
  - Updated documentation (MIDDLEWARE_INTEGRATION.md, TASK_LIST.md)
- [x] CircuitBreaker time-windowed failure counting
  - Replaced counter with `Queue<DateTime>` for sliding window
  - Added `failureWindow` parameter (default: 60s)
  - Added `CurrentFailureCount` property for monitoring
  - Added thread-safe locking
  - Added 3 new tests
- [x] SystemClock constructor injection
  - Added `ISystemClock? clock` parameter to CircuitBreaker, RateLimit, Caching
  - Replaced `DateTime.UtcNow` with `_clock.UtcNow`
  - Defaults to `SystemClock.Instance` for backwards compatibility
- [x] IStateStore interface for middleware state management
  - Created `IStateStore` interface and `InMemoryStateStore` implementation
  - Updated RateLimitMiddleware, CachingMiddleware, CircuitBreakerMiddleware to require IStateStore
  - Updated all test files to provide InMemoryStateStore instances
  - Enables future distributed state storage (Redis, etc.)
- [x] RuleResult.Error edge case tests
  - Added 9 comprehensive tests documenting RuleResult behavior
  - Discovered inconsistency: ActionRule vs Rule<T> handle exceptions differently
  - Documented in ARCHITECTURE_DECISIONS.md section 5

**Test count: 184 (all passing)**

---

### Phase 6: Dependency Injection & Inversion of Control ✅ (Core Complete)

**Investigation**: 2026-02-01
**Status**: Core DI complete, remaining tasks moved to Batch E
**Branch**: `claude/investigate-dependency-injection-B6xCF`
**Documentation**: `docs/DI_IOC_INVESTIGATION.md`

**Problem Summary**:
- AgentRouter creates `MiddlewarePipeline` and `RulesEngineCore` internally (not injectable)
- Middleware constructors have complex overloads hiding required dependencies
- No central dependency resolution mechanism
- All demos manually wire dependencies with repeated boilerplate

**Proposed Solution**:
- Create lightweight custom IoC container (zero 3rd party deps)
- Extract `IMiddlewarePipeline` and `IRulesEngine<T>` interfaces
- Refactor AgentRouter to accept injected dependencies
- Standardize middleware constructor patterns
- Add service registration extensions

**P1-DI Tasks** (8 tasks, 19-25h estimated):

| Task ID | Description | Status |
|---------|-------------|--------|
| P1-DI-1 | Create lightweight IoC container | ✅ Complete (37 tests) |
| P1-DI-2 | Add IMiddlewarePipeline interface | ✅ Complete |
| P1-DI-3 | Add IRulesEngine interface | ✅ Complete |
| P1-DI-4 | Refactor AgentRouter for DI | ✅ Complete |
| P1-DI-5 | Standardize middleware constructors | ✅ Complete |
| P1-DI-6 | Create service registration extensions | ✅ Complete (E-1a) |
| P1-DI-7 | Update demos to use container | ✅ Complete (E-1b) |
| P1-DI-8 | Add DI tests | ✅ Complete (included in P1-DI-1) |

**P1-IF Tasks** (6 tasks, 12-16h estimated):

| Task ID | Description | Status |
|---------|-------------|--------|
| P1-IF-1 | Extract IRulesEngineResult interface | ✅ Complete (E-2a) |
| P1-IF-2 | Extract IRuleExecutionResult<T> interface | ✅ Complete (E-2b) |
| P1-IF-3 | Extract ITraceSpan interface | ✅ Complete (E-2c) |
| P1-IF-4 | Extract IMiddlewareContext interface | ✅ Complete (E-2d) |
| P1-IF-5 | Extract IMetricsSnapshot + IAnalyticsReport | ✅ Complete (E-2e) |
| P1-IF-6 | Extract IWorkflowDefinition + IWorkflowStage | ✅ Complete (E-2f) |

**Batch Plan**:

```
Batch DI-A (Parallel - new files): ✅ COMPLETE
├── P1-DI-1: ServiceContainer ✅
├── P1-DI-2: IMiddlewarePipeline ✅
└── P1-DI-3: IRulesEngine ✅

Batch IF-A (Parallel - all independent): ✅ COMPLETE (in Batch E-2)
├── P1-IF-1: IRulesEngineResult ✅
├── P1-IF-2: IRuleExecutionResult<T> ✅
├── P1-IF-3: ITraceSpan ✅
├── P1-IF-4: IMiddlewareContext ✅
├── P1-IF-5: IMetricsSnapshot + IAnalyticsReport ✅
└── P1-IF-6: IWorkflowDefinition + IWorkflowStage ✅

Batch DI-B (Sequential - depends on DI-A): ✅ COMPLETE
├── P1-DI-4: AgentRouter refactoring ✅
└── P1-DI-5: Middleware constructors ✅

Batch DI-C (Parallel - after DI-B): ✅ COMPLETE (in Batch E-1)
├── P1-DI-6: ServiceExtensions ✅
├── P1-DI-7: Demo updates ✅
└── P1-DI-8: DI tests ✅
```

**Gate G6**: Build succeeds, all tests pass (1905), DI and interface extraction complete ✅

**Total Phase 6**: COMPLETE (incorporated into Batch E)

---

### Phase 7: Code Review & Bug Fixes ✅ (Reorganized into Batches A-D)

**Session**: 2026-02-02 - 2026-02-03
**Status**: COMPLETE (reorganized into layered batches)
**Documentation**: `TASK_LIST.md`, `DEEP_CODE_REVIEW.md`

**Note**: Phase 7 was reorganized into Batches C, A, B, D for better dependency ordering.
All critical bugs and thread safety issues have been addressed.

**Comprehensive Code Review Completed**:
A deep code review was performed covering all modules. Key findings:

**Critical Bugs Found (P0-NEW)**:
| ID | Issue | File | Severity |
|----|-------|------|----------|
| P0-NEW-1 | Parallel execution ignores StopOnFirstMatch | RulesEngineCore.cs:402-404 | CRITICAL |
| P0-NEW-2 | Division by zero in RuleAnalyzer | RuleValidation.cs:324 | CRITICAL |
| P0-NEW-3 | Timer resource leaks | AdvancedMiddleware.cs:296,454 | CRITICAL |
| P0-NEW-4 | Mission ID collision (all same ID) | MissionSystem.cs:25 | CRITICAL |
| P0-NEW-5 | Race conditions in GameEngine state | GameEngine.cs:323-366 | CRITICAL |
| P0-NEW-6 | Random seeding predictability | Multiple files | HIGH |

**Thread Safety Issues Found (P0-TS)**:
| ID | Issue | File |
|----|-------|------|
| P0-TS-1 | RateLimitMiddleware race condition | CommonMiddleware.cs:104-138 |
| P0-TS-2 | CircuitBreaker state machine race | CommonMiddleware.cs:411-435 |
| P0-TS-3 | CachingMiddleware TOCTOU | CommonMiddleware.cs:186-222 |
| P0-TS-4 | AgentBase capacity check race | Agent.cs:159-194 |
| P0-TS-5 | AgentRouter double-check locking | AgentRouter.cs:43-44 |
| P0-TS-6 | Parallel execution loses priority order | RulesEngineCore.cs:543 |

**MafiaDemo Bugs (P2-FIX)**:
- CancellationTokenSource leaks
- EventLog unbounded growth
- Empty agent rule action implementations
- Crew recruitment has no effect
- Game economy imbalance

**Test Framework Gaps (P3-TF)**:
- No Setup/Teardown support
- Trivial assertions (`Assert.True(true)`)
- Documented bug in RuleEdgeCaseTests
- No test state isolation

**Estimated New Work**: 92-126 hours (42 tasks)

**Batch Plan for Phase 7**:

```
Batch 7A: Critical Bugs (Parallel - different files)
├── P0-NEW-1: Parallel+StopOnFirstMatch
├── P0-NEW-2: Division by zero
├── P0-NEW-3: Timer leaks
├── P0-NEW-4: Mission ID collision
└── P0-NEW-6: Random seeding

Batch 7B: GameEngine + Thread Safety (Sequential - shared concerns)
├── P0-NEW-5: GameEngine race conditions
├── P0-TS-1: RateLimitMiddleware
├── P0-TS-2: CircuitBreaker
└── P0-TS-3: CachingMiddleware

Batch 7C: Remaining Thread Safety (Parallel)
├── P0-TS-4: AgentBase capacity
├── P0-TS-5: AgentRouter locking
└── P0-TS-6: Parallel priority order

Batch 7D: Test Framework (Sequential)
├── P3-TF-1: Setup/Teardown support
├── P3-TF-2: Fix trivial assertions
├── P3-TF-3: Fix RuleEdgeCaseTests bug
└── P3-TF-4: Test state isolation

Batch 7E: MafiaDemo Fixes (Parallel)
├── P2-FIX-1: CTS leaks
├── P2-FIX-2: EventLog growth
├── P2-FIX-3: Agent rule actions
├── P2-FIX-4: Crew recruitment
└── P2-FIX-5: Economy balance
```

**Gate G7**: All critical bugs fixed, thread safety verified, tests pass with proper isolation

---

## Success Criteria

| Gate | Criteria | Verified |
|------|----------|----------|
| G0 | .NET 8.x installed | ✅ |
| G1 | Build errors catalogued | ✅ |
| G2 | Build succeeds (0 errors) | ✅ |
| G3 | Test baseline documented | ✅ |
| G4 | MVP game runs | ✅ |
| G5-G8 | Phase 2 hardening complete | ✅ |
| Batch C | Test infrastructure ready | ✅ |
| Batch A | Thread safety verified | ✅ |
| Batch B | Resource stability verified | ✅ |
| Batch D | Application bugs fixed | ✅ |

---

## Constraints

1. **Zero 3rd party libraries** - Only .NET SDK
2. **Use custom TestRunner** - Skip xUnit projects
3. **Commit after each gate** - Rollback safety
4. **WIP limit: 3** - Max parallel agents
5. **Verify before proceed** - No skipping gates

---

## Rollback Strategy

Each gate produces a git commit. If a batch fails:
1. `git stash` or `git checkout .` to undo uncommitted changes
2. Return to last successful gate
3. Re-analyze and retry

---

## Log

### BATCH 0 Log
```
✅ .NET SDK 8.0.122 installed
✅ dotnet --version returns 8.0.122
Gate G0 PASSED
```

### BATCH 1 Log
```
✅ Build assessment complete
✅ Errors catalogued and prioritized
Gate G1 PASSED
```

### BATCH 2 Log
```
✅ Critical fixes applied (abstract class, type mismatches, null refs)
✅ Build succeeds with 0 errors
Gate G2 PASSED
```

### BATCH 3 Log
```
✅ TestRunner: 39 tests passing
✅ Baseline documented
Gate G3 PASSED
```

### BATCH 4 Log
```
✅ MVP game runs all 8 scenarios
✅ Interactive game loop functional
✅ GameTimingOptions.cs centralizes delays
Gate G4 PASSED
```

---

## Phase 2: Core Library Hardening

### BATCH 5: Parallel Foundation Improvements
**Status**: ✅ COMPLETE
**Agents**: 3 parallel sub-agents (no file conflicts)
**Duration**: ~2-3 hours

**Tasks (parallel - different files)**:
| Task | Files | Agent |
|------|-------|-------|
| P0-4 | AgentRouter.cs, IAgentLogger, ConsoleAgentLogger | Agent-5A |
| P1-5 | New AgentRoutingDefaults.cs, middleware updates | Agent-5B |
| P1-8 | All DateTime.Now usages → DateTime.UtcNow | Agent-5C |

**Gate G5**: All 3 tasks complete, build succeeds, tests pass

---

### BATCH 6: RulesEngine Thread Safety + Validation
**Status**: ✅ COMPLETE
**Agents**: 1 agent (sequential - same file)
**Duration**: ~3-4 hours
**Depends**: BATCH 5

**Tasks (sequential - both touch RulesEngineCore.cs)**:
- P1-1: Add ReaderWriterLockSlim for thread safety
- P1-6: Add rule validation on registration

**Gate G6**: Thread-safe engine, validation works, all tests pass

---

### BATCH 7: Async & Caching Improvements
**Status**: ✅ COMPLETE
**Agents**: 3 parallel sub-agents
**Duration**: ~3-4 hours
**Depends**: BATCH 6

**Tasks (parallel - different files)**:
| Task | Files | Agent |
|------|-------|-------|
| P1-2 | RulesEngineCore.cs (ExecuteAsync) | Agent-7A |
| P1-3 | RulesEngineCore.cs (cache) | Agent-7B |
| P1-4 | CommonMiddleware.cs (CachingMiddleware) | Agent-7C |

**Note**: P1-2 and P1-3 touch same file but different methods - careful coordination needed

**Gate G7**: Async execution works, caching works, all tests pass

---

### BATCH 8: Async Rules
**Status**: ✅ COMPLETE
**Agents**: 1 agent
**Duration**: ~3-4 hours
**Depends**: BATCH 7 (P1-2)

**Tasks**:
- P1-7: Add IAsyncRule<T> and AsyncRule<T>

**Gate G8**: Async rules work, all tests pass

---

### BATCH 5 Log
```
✅ P0-4: Fixed null reference in AgentRouter (null! removed)
✅ P1-5: Created AgentRoutingDefaults.cs and MiddlewareDefaults.cs
✅ P1-8: Created SystemClock.cs, replaced DateTime.Now with DateTime.UtcNow
Commit: 88ba9f8
```

### BATCH 6 Log
```
✅ P1-1: Added ReaderWriterLockSlim to RulesEngineCore
✅ P1-6: Added RuleValidationException, validation on registration
Commit: 4902cad
```

### BATCH 7 Log
```
✅ P1-2: Added ExecuteAsync with cancellation support
✅ P1-3: Added sorted rules caching (GetSortedRules)
✅ P1-4: Added LRU eviction to CachingMiddleware
Commit: ea0d9bd
```

### BATCH 8 Log
```
✅ P1-7: Added IAsyncRule<T>, AsyncRule<T>, AsyncRuleBuilder<T>
Updated RulesEngineCore to handle async rules
Commit: 4eec858
```

---

## Layered Batch Logs (New Structure)

### Batch C Log (Test Infrastructure) - 2026-02-02
```
✅ C-1: Added Setup/Teardown support
   - [SetUp], [TearDown], [OneTimeSetUp], [OneTimeTearDown] attributes
   - TestRunner discovers and invokes lifecycle methods
   - LifecycleAttributeTests.cs added

✅ C-2: Added Test State Isolation
   - TestBase class in TestRunner.Framework
   - AgentRoutingTestBase resets SystemClock.Instance
   - MafiaTestBase resets GameTimingOptions.Current

Gate: Test infrastructure ready for all subsequent batches
```

### Batch A Log (Foundation/Thread Safety) - 2026-02-02
```
✅ A-1: Fixed CircuitBreakerMiddleware State Machine Race
   - Added HalfOpenTestInProgress flag
   - Proper state machine transitions (Closed→Open→HalfOpen→Closed)
   - Only ONE request tests recovery in HalfOpen state

✅ A-2: Fixed CachingMiddleware TOCTOU
   - Added PendingRequests dictionary with TaskCompletionSource
   - Request coalescing prevents duplicate computation
   - Concurrent requests for same key wait for first result

✅ A-3: Fixed AgentBase Capacity Check Race
   - Added TryAcquireSlot() with Interlocked.CompareExchange
   - Atomic check-and-increment pattern
   - Semantic checks before slot acquisition

✅ A-4: Fixed AgentRouter Cached Pipeline Double-Check
   - Added _pipelineLock object
   - Added volatile keyword on _builtPipeline
   - Proper double-checked locking pattern

Gate: Thread-safe core libraries verified
```

### Batch B Log (Resource Stability) - 2026-02-03
```
✅ B-1: Fixed CancellationTokenSource Leaks
   - Added try/finally in StartGameAsync() to dispose CTS
   - Updated StopGame() to dispose after cancellation
   - Set _cts = null after disposal

✅ B-2: Fixed EventLog Unbounded Growth
   - Added MaxEventLogSize constant (1000 events)
   - Oldest-first eviction in LogEvent()

✅ B-3: Verified Parallel Execution Priority Order
   - Already implemented correctly
   - Results stored with indices, sorted after execution
   - Existing tests verify behavior

Gate: No memory leaks, bounded collections
```

### Batch D Log (Application Fixes) - 2026-02-03
```
✅ D-1: Completed Agent Rule Actions
   - Added RecommendedAction property to AgentDecisionContext
   - All rules now set RecommendedAction (collect, expand, recruit, bribe, laylow)
   - GetAgentAction() executes top matching rule

✅ D-2: Fixed Crew Recruitment
   - Added handlers for recruit/bribe/laylow in ExecuteAgentAction()
   - Recruit: $5,000 cost, +1 soldier, +2 reputation
   - Bribe: $10,000 cost, -15 heat
   - Laylow: -5 heat (free)

✅ D-3: Balanced Game Economy
   - Heat decay: 2/week → 5/week
   - Collection: 25% → 40% cut, respect 3 → 4
   - Hit: $5000 → $2500, respect 25 → 20, heat 30 → 25
   - Promotion thresholds: 40/70/85/95 → 35/60/80/90

✅ D-4: Fixed Trivial Test Assertions
   - Replaced 11 Assert.True(true) with meaningful assertions
   - Tests now verify agent properties and state validity

✅ D-5: Fixed Rule<T> Exception Handling
   - Rule<T>.Execute() now preserves Matched=true when action throws
   - Consistent with ActionRule behavior
   - Updated test expectations

Gate: MafiaDemo gameplay working correctly
```

### Batch E Log (Enhancement - COMPLETE) - 2026-02-03
```
✅ E-1a: Created Service Registration Extensions
   - ServiceExtensions.cs with AddAgentRouting(), AddMiddleware<T>(),
     AddAgent<T>(), AddRulesEngine<T>()
   - AgentRoutingOptions for configuring router setup
   - All extensions support singleton/transient patterns

✅ E-1b: Updated Demos to Use Container
   - MiddlewareDemo: Simplified setup using AddAgentRouting()
   - AdvancedMiddlewareDemo: Simplified setup using AddAgentRouting()
   - MafiaDemo: Left as-is (appropriate simpler pattern for games)

✅ E-2a: Extract IRulesEngineResult Interface
   - Created IResults.cs in RulesEngine/Core/
   - RulesEngineResult now implements IRulesEngineResult

✅ E-2b: Extract IRuleExecutionResult<T> Interface
   - Added to IResults.cs
   - RuleExecutionResult<T> now implements IRuleExecutionResult<T>

✅ E-2c: Extract ITraceSpan Interface
   - Created IMiddlewareTypes.cs in AgentRouting/Middleware/
   - TraceSpan now implements ITraceSpan

✅ E-2d: Extract IMiddlewareContext Interface
   - Added to IMiddlewareTypes.cs
   - MiddlewareContext now implements IMiddlewareContext
   - BONUS: Fixed thread safety with ConcurrentDictionary

✅ E-2e: Extract IMetricsSnapshot + IAnalyticsReport Interfaces
   - Added to IMiddlewareTypes.cs
   - MetricsSnapshot and AnalyticsReport implement interfaces

✅ E-2f: Extract IWorkflowDefinition + IWorkflowStage Interfaces
   - Added to IMiddlewareTypes.cs
   - WorkflowDefinition and WorkflowStage implement interfaces

✅ E-3a: Add Edge Case Tests for Rules (17 new tests)
   - Negative priority handling, MaxRulesToExecute option
   - ImmutableRulesEngine WithRule/WithoutRule tests
   - Parallel execution tests, AllowDuplicateRuleIds option
   - EvaluateAll, GetMatchingRules, concurrent registration

✅ E-3b: Add Middleware Pipeline Tests (9 new tests)
   - Cancellation token respect, concurrent pipeline execution
   - Middleware context sharing, pipeline reusability
   - Deep pipeline (50 middleware), metadata modification
   - Conditional middleware execution

✅ E-3c: Add Rate Limiter Tests (8 new tests)
   - Very short window reset, empty sender handling
   - One request limit, multiple windows independence
   - Handler failure counting, high concurrency limits

✅ E-3d: Add Circuit Breaker Tests (10 new tests)
   - Half-open state success/failure transitions
   - Large failure threshold, partial failures
   - Concurrent half-open transitions, slow handler handling
   - Separate state stores, recovery after outage

✅ E-3e: Add Performance Benchmarks (7 new benchmarks)
   - Circuit breaker, ImmutableRulesEngine
   - Rule builder creation, AgentRouter routing
   - 10-middleware pipeline, GetMatchingRules (500 rules)
   - Concurrent message routing

✅ E-3f: Add Integration Tests for Agent Routing (11 new tests)
   - Agent registration verification, rule priority
   - Error propagation, exception handling
   - Concurrent registration/routing, category routing
   - Message metadata preservation, routing context

✅ E-3g: Add Test Coverage Analysis
   - CoverageValidationTests.cs with 6 validation tests
   - Validates threshold compliance per module
   - Identifies zero-coverage classes and quick wins
   - Generates summary reports

Gate: 1905 tests passing, all enhancements complete
```

### Bug Fixes & Refactoring Log - 2026-02-03
```
Session: GameRulesEngine refactoring and bug report resolution

✅ Refactored GameRulesEngine.cs (~2750 lines → 5 files)
   - Created Rules/ subfolder
   - Rules/RuleContexts.cs - 8 context classes
   - Rules/RuleConfiguration.cs - Support classes
   - Rules/GameRulesEngine.cs - Core partial class
   - Rules/GameRulesEngine.Setup.cs - 7 rule setup methods
   - Rules/GameRulesEngine.Analysis.cs - Analysis/debugging

✅ Fixed Bug #2 (CRITICAL): Case-sensitivity in PlayerAgent.cs
   - Changed topRule.Name.Contains("REJECT") to topRule.Id.Contains("REJECT")
   - Updated 4 tests that documented buggy behavior

✅ Fixed Bug #4: Agent routing "[route failed]" display
   - Created GameEngineAgent class in GameEngine.cs
   - Registered agent in both MafiaGameEngine constructors

✅ Fixed Bug #1: Console.ReadKey() crashes in non-interactive mode
   - Added Console.IsInputRedirected checks in Program.cs and AutonomousPlaythrough.cs

✅ Fixed Bug #3: Repeated promotion display every week
   - Capture rank before ProcessWeekAsync, compare after
   - Removed flawed GetPreviousRank() method

✅ Fixed Bug #5: Week counter off-by-one
   - Display character.Week - 1 in PrintFinalSummary

✅ Fixed flaky test: MafiaGameEngine_GameOver_WhenHeatMaxed
   - Set initial heat to 120 instead of 100 (accounts for heat reduction)

✅ Deleted BUG_REPORT.md (all bugs resolved)

✅ Updated documentation
   - ARCHITECTURE.md: Updated file map with Rules/ folder
   - TASK_LIST.md: Fixed stale file references
   - EXECUTION_PLAN.md: This log entry

Gate: 1916 tests passing (11 new tests from bug fix verification)
```

### Batch H Log (Code Review Bug Fixes - COMPLETE) - 2026-02-03
```
✅ H-1: Fixed Heat Balance (CRITICAL)
   - Territory heat reduced: 23 → 11/week
   - Natural decay increased: 5 → 8/week
   - Game now reaches victory at week 52

✅ H-2: Event Timing Uses Game Weeks
   - Added GameWeek property to GameEvent
   - Changed event checks to use game weeks

✅ H-3: CHAIN_HIT_TO_WAR Null Safety
   - Changed First() to FirstOrDefault() with null check

✅ H-4: Rival Hostility Clamping
   - Added Math.Max(0, ...) to prevent negative values
   - 2 tests added

✅ H-5: MissionEvaluator Duplicate Fix
   - Removed duplicate EvaluateAll() call

✅ H-6: PlayerAgent Field Consistency
   - Changed RuleName to RuleId for decision trace

✅ H-7: CONSEQUENCE_VULNERABLE Null Safety
   - Added Territories.Any() check

✅ H-8: RivalStrategyContext.ShouldAttack
   - Logic is intentionally correct
   - Added comprehensive XML documentation

✅ H-9: Currency Symbol Display
   - Changed :C0 to explicit $/:N0 format

✅ H-10: Bribe Coordination
   - Added BribedThisWeek flag to GameState
   - Resets at turn start, prevents duplicate bribes
   - 3 tests added

✅ H-11: Defeat/Victory Constants
   - Added constants to GameState (DefeatReputationThreshold, etc.)
   - Single source of truth for game conditions

✅ H-12: EventLog Performance
   - Changed List<GameEvent> to Queue<GameEvent>
   - O(1) eviction instead of O(n)

✅ H-13: Victory Achievability Tests
   - 3 integration tests verifying game is winnable

✅ H-14: Null-Safe Rival Helpers
   - Added HasRivals, MaxRivalHostility, MinRivalStrength
   - Added HasHostileRival(), HasWeakRival() methods

Gate: All 14 code review bugs fixed, game is winnable
```

### Batch I Log (Story System Integration - IN PROGRESS) - 2026-02-03
```
Story System Components (28 files in Story/):
├── World/ - Location, NPC, Faction, WorldState
├── Narrative/ - StoryNode, StoryGraph, StoryEvent, PlotThread
├── Intelligence/ - Intel, IntelRegistry
├── Agents/ - Persona, Memory, EntityMind
├── Communication/ - AgentQuestion, AgentResponse, ConversationContext
├── Rules/ - 5 rule setup files
├── Engine/ - RulesBasedConversationEngine
├── Generation/ - DynamicMissionGenerator, MissionHistory
├── Integration/ - GameWorldBridge, HybridMissionGenerator, MissionAdapter
├── Seeding/ - WorldStateSeeder
└── Core/ - Enums, Thresholds

✅ I-2a: GameWorldBridge
   - Bidirectional sync between GameState and WorldState
   - Territory ↔ Location, RivalFamily ↔ Faction mapping

✅ I-2b: WorldState Initialization
   - GameEngine.InitializeStorySystem() calls WorldStateSeeder
   - StoryGraph initialized with seed plot threads
   - IntelRegistry initialized

✅ I-3a-c: NPC & Relationship Integration
   - Mission class has NPCId, LocationId properties
   - MissionConsequenceHandler.ApplyMissionConsequences()
   - NPC status affects mission difficulty

✅ I-4a-c: Plot Thread Integration
   - Plot state machine: Dormant → Available → Active → Completed
   - Plot missions weighted higher in generation
   - Plot completion rewards applied

✅ I-5a: HybridMissionGenerator
   - Combines Story + Legacy mission generation
   - Falls back to legacy when Story System disabled

✅ I-2c: Week Counter Consolidation (2026-02-03)
   - Added LinkedWorldState property to GameState
   - GameState.Week now delegates to WorldState.CurrentWeek when linked
   - Linked automatically in GameEngine.InitializeStorySystem()
   - 3 new tests for week counter consistency

✅ I-5b: ConsequenceRules Integration (2026-02-03)
   - Added MissionConsequenceHandler.ApplyConsequenceRules()
   - Creates ConsequenceContext from mission result
   - Executes consequence rules (intimidation, hit, negotiation, etc.)
   - 3 new tests for consequence rules

✅ I-5c: Intel Recording (2026-02-03)
   - Added MissionConsequenceHandler.RecordIntelFromMission()
   - Creates Intel for NPC, Location, or general info
   - Added IntelRegistry property to PlayerAgent
   - 4 new tests for intel recording

✅ I-7a-d: Integration Tests
   - 37+ tests in StorySystemIntegrationTests.cs
   - Covers GameWorldBridge, HybridMissionGenerator, PlotThread, MissionConsequences
   - Now includes ConsequenceRules and Intel recording tests

Remaining (Moved to Batch F):
- [ ] F-2a: Basic NPC Conversation Command (was I-6a)
- [ ] F-2b: Conversation Results Integration (was I-6b)

✅ PlayerAgent E2E Integration Tests (2026-02-03)
   - 4 additional tests verifying Story System through PlayerAgent.ExecuteMissionAsync
   - Tests consequence rules application, intel recording, backward compatibility

Gate: Story System integrated with MafiaDemo (14/16 tasks complete, conversation deferred to F)
Total integration tests: 44 (40 + 4 PlayerAgent E2E)
```

### Batch F-3 Log (Runtime Bug Fixes - COMPLETE) - 2026-02-04
```
✅ F-3a: AI Career Mode Story System Integration (HIGH)
   - Changed AutonomousPlaythrough.cs to use MafiaGameEngine instead of raw GameState
   - Wired PlayerAgent's WorldState, StoryGraph, IntelRegistry from engine
   - Story System now active in AI Career Mode

✅ F-3b: Mission Success Rate Too Low (MEDIUM)
   - Lowered skill bonus threshold from >10 to >5
   - Added EARLY_CAREER_BOOST rule (+10% for Associates)
   - New players now have ~75-80% success rate on early missions

✅ F-3c: Plot Count Display Misleading (LOW)
   - Display now shows both active AND available plots
   - Example: "📖 Story System: Active (0 active, 1 available plots)"

Additional Story System Bug Fixes (discovered during code review):
   - Fixed null reference on LastInteractionWeek in DynamicMissionGenerator
   - Fixed KeyNotFoundException in WorldState.GetNPCsAtLocation
   - Added NPC interaction decay to MissionHistory
   - Implemented delayed triggers in StoryGraph
   - Added edge validation to StoryGraph.AddEdge()
   - Fixed off-by-one expiration in StoryNode.HasExpired()

Tests: 10 new integration tests added (StorySystemIntegrationTests.cs)
Total: 1,977 tests (was 1,862)

Gate: All F-3 tasks complete, Story System bugs fixed
```

### Runtime Testing Log - 2026-02-03
```
Ran all three MafiaDemo modes to verify runtime behavior:

Option 3 (Scripted Demo): ✅ Runs clean
Option 2 (Autonomous Game): Shows "0 active plots" (minor display issue)
Option 1 (AI Career Mode): ❌ Major bug - doesn't use Story System

Bugs Discovered:
- F-3a (HIGH): AutonomousPlaythrough.cs:98 creates raw GameState instead of MafiaGameEngine
  - Story System never initialized in AI Career Mode
  - All Story integration work bypassed

- F-3b (MEDIUM): Mission success rate too harsh for new players
  - Base rate 50% + modifiers
  - New player with 0 loyalty/skill fails ~40% of missions
  - Suggested fix: Bump base rate to 60%

- F-3c (LOW): "0 active plots" display misleading
  - Plots are dormant by design until triggered
  - Display should explain plot system or show available plots

Updated TASK_LIST.md with F-3 section (3 new tasks)
```

### Documentation Review Log - 2026-02-03
```
Deep review of all process markdown files:

✅ Updated Tests/COVERAGE_REPORT.md
   - Test count: 172 → ~1905
   - Added 50 test files inventory
   - Added TestUtilities documentation

✅ Updated README.md
   - Test count: 184+ → ~1905
   - Batch E: Current → Complete
   - Remaining work updated for Batch F

✅ Updated CLAUDE.md
   - Added TestUtilities to test project structure
   - Added IResults.cs interfaces documentation
   - Added IMiddlewareTypes.cs interfaces documentation
   - Expanded file locations with new files

✅ Updated DEEP_CODE_REVIEW.md
   - Test count: 184+ → ~1905
   - Marked documentation update tasks as done

✅ Updated EXECUTION_PLAN.md
   - Phase 6 P1-DI/P1-IF tasks marked complete
   - Batch plan updated with completion status
   - Next Steps updated for Batch F
```

---

## Next Steps

**Batch F: Polish** (11 tasks remaining) - CURRENT

See `TASK_LIST.md` for full details. Work organized into three groups:

### F-1: Documentation (8 tasks)
| Task | Description | Hours | Notes |
|------|-------------|-------|-------|
| F-1a | Consolidate MafiaDemo docs | 2-3 | Merge 7 overlapping files |
| F-1b | Update ARCHITECTURE.md status | 1-2 | ✅ Already complete |
| F-1c | Update CLAUDE.md patterns | 2-3 | New DI/interface patterns |
| F-1d | XML documentation | 3-4 | Public API docs |
| F-1e | API reference docs | 3-4 | Comprehensive reference |
| F-1f | MafiaDemo player guide | 2-3 | Gameplay documentation |
| F-1g | Code style cleanup | 2-3 | Warnings, formatting |
| F-1h | Release checklist | 1-2 | Deployment preparation |

### F-2: Conversation System (2 tasks, deferred from Batch I)
| Task | Description | Hours | Priority |
|------|-------------|-------|----------|
| F-2a | Basic NPC Conversation Command | 2-3 | P3 |
| F-2b | Conversation Results Integration | 2-3 | P3 |

### F-3: Runtime Bugs ✅ COMPLETE (2026-02-04)
| Task | Description | Hours | Status |
|------|-------------|-------|--------|
| F-3a | AI Career Mode Story System Integration | 3-4 | ✅ Complete |
| F-3b | Mission success rate fix | 1-2 | ✅ Complete |
| F-3c | Plot count display fix | 0.5-1 | ✅ Complete |

**Additional fixes**: 6 Story System bugs fixed, 10 new integration tests added.
