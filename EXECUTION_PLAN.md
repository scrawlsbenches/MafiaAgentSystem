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

### Phase 4: MafiaDemo Completion (In Progress)
- [x] P2-1: Architecture documentation (ARCHITECTURE.md) ✅
- [x] P2-2 through P2-5: Agent hierarchy - **ALREADY IMPLEMENTED** ✅
- [x] P2-8: Interactive game loop - **ALREADY IMPLEMENTED** ✅
- [ ] Wire RulesBasedGameEngine to MafiaGameEngine
- [ ] Replace hardcoded agent decisions with rules
- [ ] P2-10: Add integration tests

**Discovery**: Code review revealed P2-2 through P2-8 were already implemented.
Original estimate: 30-38h → Revised estimate: 7-9h (integration only)

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
[Pending execution]
```

### BATCH 1 Log
```
[Pending execution]
```

### BATCH 2 Log
```
[Pending execution]
```

### BATCH 3 Log
```
[Pending execution]
```

### BATCH 4 Log
```
[Completed - MVP verified]
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
