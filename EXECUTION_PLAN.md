# Execution Plan: MafiaAgentSystem Build & MVP

> **Created**: 2026-01-31
> **Constraint**: Zero 3rd party libraries (only .NET SDK)
> **Goal**: Compiling codebase → Test baseline → MVP game

---

## Execution State (Updated 2026-01-31)

- [x] .NET SDK 8.0.122 installed ✅
- [x] Build succeeds (0 errors in core + MafiaDemo) ✅
- [x] Test baseline: 39 tests, all passing ✅
- [ ] MVP game loop verification pending

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
[Pending execution]
```
