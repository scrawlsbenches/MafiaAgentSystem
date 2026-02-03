# Execution Plan: MafiaAgentSystem Build & MVP

> **Created**: 2026-01-31
> **Last Updated**: 2026-02-03
> **Constraint**: Zero 3rd party libraries (only .NET SDK)
> **Goal**: Compiling codebase → Test baseline → MVP game → Production Quality

---

## Current Execution State (Updated 2026-02-03)

### Batch Structure (Layered Approach)

After Phase 5, tasks were reorganized into a **layered batch structure** to minimize churn.
See `TASK_LIST.md` for full details.

```
┌─────────────────────────────────────────────────────────────┐
│ Layer F: POLISH (last)                                       │
│   Documentation, code cleanup                                │
├─────────────────────────────────────────────────────────────┤
│ Layer E: ENHANCEMENT                    ← START HERE         │
│   DI extensions, interface extraction, new tests             │
├─────────────────────────────────────────────────────────────┤
│ Layer D: APPLICATION FIXES              ✅ COMPLETE           │
│   MafiaDemo gameplay bugs                                    │
├─────────────────────────────────────────────────────────────┤
│ Layer B: RESOURCE STABILITY             ✅ COMPLETE           │
│   Memory leaks, unbounded growth                             │
├─────────────────────────────────────────────────────────────┤
│ Layer A: FOUNDATION                     ✅ COMPLETE           │
│   Thread safety in core libraries                            │
├─────────────────────────────────────────────────────────────┤
│ Layer C: TEST INFRASTRUCTURE            ✅ COMPLETE           │
│   Setup/Teardown, state isolation                            │
└─────────────────────────────────────────────────────────────┘
```

| Batch | Layer | Status | Tasks | Completed |
|-------|-------|--------|-------|-----------|
| **C** | Test Infra | ✅ Complete | 2 | 2026-02-02 |
| **A** | Foundation | ✅ Complete | 4 | 2026-02-02 |
| **B** | Resources | ✅ Complete | 3 | 2026-02-03 |
| **D** | App Fixes | ✅ Complete | 5 | 2026-02-03 |
| **E** | Enhancement | 🚀 **NEXT** | 15 | - |
| **F** | Polish | ⏳ Pending | 10 | - |

**Test count: 184+ (all passing)**

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
| P1-DI-6 | Create service registration extensions | ⏳ Pending |
| P1-DI-7 | Update demos to use container | ⏳ Pending |
| P1-DI-8 | Add DI tests | ✅ Complete (included in P1-DI-1) |

**P1-IF Tasks** (6 tasks, 12-16h estimated):

| Task ID | Description | Status |
|---------|-------------|--------|
| P1-IF-1 | Extract IRulesEngineResult interface | ⏳ Pending |
| P1-IF-2 | Extract IRuleExecutionResult<T> interface | ⏳ Pending |
| P1-IF-3 | Extract ITraceSpan interface | ⏳ Pending |
| P1-IF-4 | Extract IMiddlewareContext interface | ⏳ Pending |
| P1-IF-5 | Extract IMetricsSnapshot + IAnalyticsReport | ⏳ Pending |
| P1-IF-6 | Extract IWorkflowDefinition + IWorkflowStage | ⏳ Pending |

**Batch Plan**:

```
Batch DI-A (Parallel - new files): ✅ COMPLETE
├── P1-DI-1: ServiceContainer ✅
├── P1-DI-2: IMiddlewarePipeline ✅
└── P1-DI-3: IRulesEngine ✅

Batch IF-A (Parallel - all independent, can run with DI-A):
├── P1-IF-1: IRulesEngineResult
├── P1-IF-2: IRuleExecutionResult<T>
├── P1-IF-3: ITraceSpan
├── P1-IF-4: IMiddlewareContext
├── P1-IF-5: IMetricsSnapshot + IAnalyticsReport
└── P1-IF-6: IWorkflowDefinition + IWorkflowStage

Batch DI-B (Sequential - depends on DI-A): ✅ COMPLETE
├── P1-DI-4: AgentRouter refactoring ✅
└── P1-DI-5: Middleware constructors ✅

Batch DI-C (Parallel - after DI-B):
├── P1-DI-6: ServiceExtensions
├── P1-DI-7: Demo updates
└── P1-DI-8: DI tests
```

**Gate G6**: Build succeeds, all tests pass (184+), new DI tests pass

**Total Phase 6 Estimate**: 31-41 hours (14 tasks)

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

---

## Next Steps

**Batch E: Enhancement** (15 tasks, 35-47 hours)

See `TASK_LIST.md` for full details. Summary:

| Group | Tasks | Hours | Notes |
|-------|-------|-------|-------|
| E-1: DI Extensions | 2 | 4-6 | Service registration, demo updates |
| E-2: Interface Extraction | 6 | 12-16 | All parallelizable |
| E-3: Additional Testing | 7 | 19-25 | Edge cases, benchmarks |

**Batch F: Polish** (10 tasks, 20-28 hours)

Documentation consolidation, API docs, cleanup. Depends on E completion.
