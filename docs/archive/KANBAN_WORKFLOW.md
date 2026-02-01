# Kanban Workflow for Sub-Agent Orchestration

> **Purpose**: Define a structured workflow for orchestrating parallel sub-agents with verification gates
> **Constraint**: Minimize scope creep while maintaining SOLID principles
> **Goal**: Build a playable mafia text-based game incrementally

---

## Philosophy: When Is Enough Information "Enough"?

### The Minimum Viable Knowledge Threshold

To build a SOLID mafia game, we need **verified answers** to these questions:

| Question | Status | Source |
|----------|--------|--------|
| What interfaces exist? | ✅ Known | `IRule<T>`, `IAgent`, `IAgentMiddleware` |
| Does the code compile? | ❌ No | Build fails, .NET not installed |
| What's actually implemented vs documented? | ⚠️ Gap exists | 30-40% implemented |
| What's the minimum playable game? | ❓ Need to define | See MVP below |

### The "Build → Verify → Extend" Loop

```
┌──────────────────────────────────────────────────────────────┐
│  "Enough" = Code compiles + Tests pass + One feature works  │
└──────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │  Only then add next feature   │
              └───────────────────────────────┘
```

**We have enough information when:**
1. We can describe a single vertical slice (one complete feature)
2. We can write a test that would prove it works
3. We can verify the test passes

---

## Kanban Board Design

### Swimlanes (Columns)

```
┌─────────────┬─────────────┬─────────────┬─────────────┬─────────────┐
│   BACKLOG   │   READY     │ IN PROGRESS │   VERIFY    │    DONE     │
│             │             │   (WIP: 3)  │   (WIP: 2)  │             │
├─────────────┼─────────────┼─────────────┼─────────────┼─────────────┤
│ All tasks   │ Dependencies│ Sub-agents  │ Orchestrator│ Verified &  │
│ from        │ met, ready  │ executing   │ reviewing   │ merged      │
│ TASK_LIST   │ for agents  │ in parallel │ outputs     │             │
└─────────────┴─────────────┴─────────────┴─────────────┴─────────────┘
```

### WIP Limits (Critical for Quality)

| Column | WIP Limit | Rationale |
|--------|-----------|-----------|
| In Progress | 3 agents | Max I can track simultaneously |
| Verify | 2 batches | Ensures verification doesn't backlog |
| Ready | 5 tasks | Prevents over-planning |

### Task Categories (Rows)

```
┌─────────────────────────────────────────────────────────────────────┐
│ INFRASTRUCTURE  │ .NET install, build, test runner                  │
├─────────────────────────────────────────────────────────────────────┤
│ FIXES           │ Compilation errors, null refs, type mismatches    │
├─────────────────────────────────────────────────────────────────────┤
│ FEATURES        │ Agents, routing, game loop, AI                    │
├─────────────────────────────────────────────────────────────────────┤
│ QUALITY         │ Tests, documentation, code cleanup                │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Batch Organization Strategy

### Batch Types

| Batch Type | Agent Type | Parallelism | Verification |
|------------|------------|-------------|--------------|
| **Explore** | Explore agent | Up to 3 | Review findings |
| **Execute** | Bash agent | Up to 2 | Check exit codes + output |
| **Implement** | General-purpose | 1-2 | Build + test |
| **Plan** | Plan agent | 1 | Review design |

### Batch Dependencies

```
BATCH 0: Environment Setup
    └── Install .NET SDK
    └── Verify build tools

BATCH 1: Establish Baseline (GATE: dotnet build succeeds)
    └── Run build, capture all errors
    └── Categorize errors by severity

BATCH 2: Critical Fixes (GATE: dotnet build succeeds with 0 errors)
    └── Fix GameEngine.cs type mismatches
    └── Fix AutonomousAgent instantiation
    └── Fix null references

BATCH 3: Test Baseline (GATE: test runner executes)
    └── Run existing tests
    └── Document pass/fail baseline

BATCH 4: Minimal Viable Game (GATE: game loop runs)
    └── Implement simplest game loop
    └── One agent type working
    └── One command working

BATCH 5+: Incremental Features (GATE: each feature has test)
    └── Add agent types one at a time
    └── Add commands one at a time
    └── Add rules one at a time
```

---

## Verification Gates

### Gate Criteria

Every batch must pass before proceeding:

| Gate | Criteria | How to Verify |
|------|----------|---------------|
| **G0: Environment** | .NET 8 installed | `dotnet --version` returns 8.x |
| **G1: Compiles** | Zero build errors | `dotnet build` exit code 0 |
| **G2: Tests Run** | Test runner executes | `dotnet run --project Tests/TestRunner` completes |
| **G3: Tests Pass** | No regressions | Pass count >= baseline |
| **G4: Feature Works** | Manual verification | Can execute the feature |

### Verification Commands

```bash
# G0: Environment
dotnet --version

# G1: Compiles
dotnet build 2>&1 | grep -E "(error|warning)" | head -20

# G2: Tests Run
dotnet run --project Tests/TestRunner/ 2>&1 | tail -20

# G3: Tests Pass
# Compare output to baseline

# G4: Feature Works
dotnet run --project AgentRouting/AgentRouting.MafiaDemo/
```

---

## Sub-Agent Dispatch Strategy

### Agent Selection Matrix

| Task Type | Agent | Why |
|-----------|-------|-----|
| Find files/patterns | Explore | Fast, thorough |
| Run commands | Bash | Direct execution |
| Research questions | Explore | Multi-file analysis |
| Write code | General-purpose | Full tool access |
| Design architecture | Plan | Focused planning |

### Parallel Dispatch Rules

**CAN parallelize:**
- Multiple Explore agents searching different areas
- Independent Bash commands
- Code changes to unrelated files

**CANNOT parallelize:**
- Tasks with file dependencies (edit A before edit B)
- Tasks with build dependencies (fix before test)
- Tasks requiring sequential verification

### Dispatch Template

```markdown
## Batch N: [Name]
**Gate Required**: [Previous gate]
**Gate Produces**: [This gate]

### Parallel Agents:
1. Agent Type: [Explore/Bash/General]
   Task: [Specific task]
   Success: [How to verify]

2. Agent Type: [...]
   Task: [...]
   Success: [...]

### Sequential Follow-up:
- After agents complete: [verification step]
- If pass: proceed to Batch N+1
- If fail: [remediation]
```

---

## What We're Forgetting / Risk Mitigation

### Known Unknowns

| Risk | Mitigation |
|------|------------|
| Sub-agent produces wrong output | Always verify with build/test |
| Sub-agent scope creeps | Give precise, bounded instructions |
| Parallel edits conflict | Assign non-overlapping files |
| Verification backlog | Strict WIP limits |
| Context loss between batches | Document state at each gate |

### Things We Might Be Forgetting

1. **Error Recovery**: What if a batch partially succeeds?
   - Strategy: Treat partial success as failure, fix all issues before proceeding

2. **Rollback**: What if we need to undo a batch?
   - Strategy: Git commit after each successful gate

3. **Documentation Drift**: Docs may not match reality
   - Strategy: Update docs as part of each batch

4. **Hidden Dependencies**: Code may have undocumented coupling
   - Strategy: Build after every change, not just at gates

5. **Performance**: Agents take time to spawn and execute
   - Strategy: Batch intelligently, maximize parallelism where safe

6. **Context Limits**: Long sessions may lose context
   - Strategy: Document decisions in files, not just conversation

---

## Minimum Viable Game (MVP) Definition

### What Makes a "Playable" Mafia Game?

**MVP Scope (Intentionally Minimal)**:

```
┌─────────────────────────────────────────────────────────────────┐
│ MINIMUM VIABLE MAFIA GAME                                       │
├─────────────────────────────────────────────────────────────────┤
│ 1. Game starts and displays status                              │
│ 2. Player can issue ONE command type (e.g., "collect")          │
│ 3. ONE agent type responds (e.g., Soldier)                      │
│ 4. Game state changes (e.g., wealth increases)                  │
│ 5. Turn advances                                                │
│ 6. Game can end (win or lose condition)                         │
└─────────────────────────────────────────────────────────────────┘
```

**NOT in MVP**:
- Multiple agent types
- Complex command parsing
- AI opponents
- Missions
- Territory management
- Save/load

### MVP Success Criteria

```bash
# Can start
$ dotnet run --project AgentRouting/AgentRouting.MafiaDemo/
> Game started. You are an Associate in the Corleone family.
> Wealth: $1000 | Reputation: 10 | Heat: 0

# Can act
> collect
> Soldier reports: Collection successful. +$500

# State changes
> status
> Wealth: $1500 | Reputation: 12 | Heat: 5

# Can end
> [after N turns or conditions]
> Game Over: You've risen to Godfather! (or: You've been arrested!)
```

---

## Documentation That Will Help

### Existing Documentation Value

| Document | Usefulness | How It Helps |
|----------|------------|--------------|
| **CLAUDE.md** | ⭐⭐⭐⭐⭐ | Build commands, SOLID patterns, extension points |
| **TASK_LIST.md** | ⭐⭐⭐⭐⭐ | Prioritized work breakdown |
| **mafia-code-review.md** | ⭐⭐⭐⭐ | Known issues and fixes |
| **MafiaDemo/CODE_REVIEW.md** | ⭐⭐⭐⭐ | Specific code issues |
| **RulesEngine/README.md** | ⭐⭐⭐ | API patterns |
| **AgentRouting/README.md** | ⭐⭐⭐ | Agent patterns |

### Documentation We Should Create

| Document | Purpose | When to Create |
|----------|---------|----------------|
| **MVP_SPEC.md** | Defines minimum game | Before Batch 4 |
| **BATCH_LOG.md** | Records each batch outcome | During execution |
| **BASELINE.md** | Test baseline counts | After Batch 3 |

---

## Execution Plan: First 3 Batches

### BATCH 0: Environment Setup
**Duration**: 30 minutes
**Agent**: Bash (sequential)

```bash
# Step 1: Install .NET
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
chmod 1777 /tmp
apt-get update -o Dir::Etc::sourcelist="sources.list.d/microsoft-prod.list" \
  -o Dir::Etc::sourceparts="-" -o APT::Get::List-Cleanup="0"
apt-get install -y dotnet-sdk-8.0

# Step 2: Verify
dotnet --version
```

**Gate G0**: `dotnet --version` shows 8.x

---

### BATCH 1: Establish Baseline
**Duration**: 1 hour
**Agents**: 2 parallel Bash

**Agent 1**: Build and capture errors
```bash
dotnet build 2>&1 | tee build-output.txt
grep -c "error CS" build-output.txt  # Count errors
```

**Agent 2**: List all source files
```bash
find . -name "*.cs" -type f | wc -l
find . -name "*.csproj" -type f
```

**Gate G1**: Build output captured, errors counted

---

### BATCH 2: Critical Fixes
**Duration**: 2-3 hours
**Agents**: 1 General-purpose (sequential due to dependencies)

**Task**: Fix all compilation errors in priority order:
1. GameEngine.cs type mismatches
2. AutonomousAgent instantiation
3. Missing references

**Gate G2**: `dotnet build` succeeds with 0 errors

---

### BATCH 3: Test Baseline
**Duration**: 1 hour
**Agent**: Bash

```bash
dotnet run --project Tests/TestRunner/ 2>&1 | tee test-baseline.txt
```

**Gate G3**: Test runner completes, baseline documented

---

## Workload Management: Orchestrator Capacity

### My Sustainable Workload

| Activity | Time Allocation | Notes |
|----------|-----------------|-------|
| Dispatch agents | 10% | Quick, parallel |
| Wait for agents | 40% | Can do other work |
| Verify outputs | 30% | Must be thorough |
| Fix issues | 15% | Remediation |
| Document | 5% | Update logs |

### When to Pause

- Verification queue > 2 batches
- Consecutive failures > 2
- Unclear requirements (ask user)
- Scope creep detected

### Scope Creep Signals

- "While we're at it, let's also..."
- Adding features not in MVP
- Refactoring working code
- Gold-plating (perfect vs good enough)

**Response**: Stop, return to MVP definition, defer to backlog

---

## Summary: The Right Amount of Information

### To Start Building:

1. ✅ We have interfaces defined (`IRule<T>`, `IAgent`, `IAgentMiddleware`)
2. ✅ We have SOLID patterns documented
3. ✅ We have a prioritized task list
4. ❌ We don't have a compiling codebase (fix first)
5. ❌ We don't have a test baseline (establish second)
6. ❌ We don't have an MVP spec (define third)

### The Formula:

```
Enough Information =
    Compiling Code +
    Passing Tests +
    Clear MVP Definition +
    One Verified Feature
```

**We're missing all four.** The workflow above addresses this in order.

---

## Next Action

Execute **BATCH 0: Environment Setup** to install .NET, then proceed through the gates.

Would you like me to begin execution?
