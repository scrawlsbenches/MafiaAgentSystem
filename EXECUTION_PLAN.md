# Execution Plan: MafiaAgentSystem Build & MVP

> **Created**: 2026-01-31
> **Constraint**: Zero 3rd party libraries (only .NET SDK)
> **Goal**: Compiling codebase → Test baseline → MVP game

---

## Execution State (Updated 2026-02-01)

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

**PHASE 2 COMPLETE**

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

**Discovery**: Code review revealed P2-2 through P2-8 were already implemented.
Original estimate: 30-38h → Revised estimate: 7-9h (integration only)

**PHASE 4 COMPLETE**

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

### Phase 5: Architectural Improvements (In Progress)

**Session: 2026-02-01**

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

### Phase 6: Dependency Injection & Inversion of Control (Planned)

**Investigation**: 2026-02-01
**Status**: PLANNED
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
| P1-DI-1 | Create lightweight IoC container | ⏳ Pending |
| P1-DI-2 | Add IMiddlewarePipeline interface | ⏳ Pending |
| P1-DI-3 | Add IRulesEngine interface | ⏳ Pending |
| P1-DI-4 | Refactor AgentRouter for DI | ⏳ Pending |
| P1-DI-5 | Standardize middleware constructors | ⏳ Pending |
| P1-DI-6 | Create service registration extensions | ⏳ Pending |
| P1-DI-7 | Update demos to use container | ⏳ Pending |
| P1-DI-8 | Add DI tests | ⏳ Pending |

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
Batch DI-A (Parallel - new files):
├── P1-DI-1: ServiceContainer
├── P1-DI-2: IMiddlewarePipeline
└── P1-DI-3: IRulesEngine

Batch IF-A (Parallel - all independent, can run with DI-A):
├── P1-IF-1: IRulesEngineResult
├── P1-IF-2: IRuleExecutionResult<T>
├── P1-IF-3: ITraceSpan
├── P1-IF-4: IMiddlewareContext
├── P1-IF-5: IMetricsSnapshot + IAnalyticsReport
└── P1-IF-6: IWorkflowDefinition + IWorkflowStage

Batch DI-B (Sequential - depends on DI-A):
├── P1-DI-4: AgentRouter refactoring
└── P1-DI-5: Middleware constructors

Batch DI-C (Parallel - after DI-B):
├── P1-DI-6: ServiceExtensions
├── P1-DI-7: Demo updates
└── P1-DI-8: DI tests
```

**Gate G6**: Build succeeds, all tests pass (184+), new DI tests pass

**Total Phase 6 Estimate**: 31-41 hours (14 tasks)

---

## Success Criteria

| Gate | Criteria | Verified |
|------|----------|----------|
| G0 | .NET 8.x installed | [ ] |
| G1 | Build errors catalogued | [ ] |
| G2 | Build succeeds (0 errors) | [ ] |
| G3 | Test baseline documented | [ ] |
| G4 | MVP game runs | [ ] |

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
