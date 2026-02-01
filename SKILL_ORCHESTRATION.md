# Skill: Multi-Agent Orchestration

> How to organize complex work into parallel batches with sub-agents while maintaining quality and avoiding conflicts.

---

## Core Principles

### 1. File Ownership (No Conflicts)
Each file should be owned by exactly one agent per batch. When multiple tasks touch the same file, either:
- Assign them to a single agent (sequential within agent)
- Run them in separate batches (sequential across batches)

**Good**: Agent-A owns `Router.cs`, Agent-B owns `Middleware.cs`
**Bad**: Agent-A and Agent-B both modify `Router.cs`

### 2. Dependency-First Analysis
Before creating batches, analyze task dependencies:
1. What files does each task modify?
2. Which tasks depend on others completing first?
3. Which tasks can run in parallel (no file overlap, no dependencies)?

### 3. Verification Gates
Every batch must pass a gate before proceeding:
- **Build Gate**: `dotnet build` succeeds with 0 errors
- **Test Gate**: All tests pass
- **Commit Gate**: Changes committed for rollback safety

Never skip gates. A failed gate means stop, fix, then retry.

### 4. Progressive Commitment
Commit after each successful batch. This provides:
- Rollback points if later batches fail
- Clear history of what changed when
- Ability to bisect if issues emerge later

---

## Batch Organization Process

### Step 1: List All Tasks
Start with a complete task list. Each task should have:
- Clear deliverable
- Files it will modify
- Dependencies on other tasks
- Estimated complexity (small/medium/large)

### Step 2: Build Dependency Graph
```
Task A (file1.cs) ─┐
                   ├─→ Task D (file3.cs) ─→ Task E (file1.cs)
Task B (file2.cs) ─┘
Task C (file2.cs) ───→ Task F (file4.cs)
```

### Step 3: Group into Batches
- Tasks with no dependencies and no file conflicts → same batch (parallel)
- Tasks with dependencies → later batch
- Tasks touching same file → same agent OR separate batches

### Step 4: Assign Agents
Within each batch:
- List which agent owns which files
- Specify what each agent should NOT touch
- Define verification steps for each agent

---

## Batch Template

```markdown
### BATCH N: [Description]
**Status**: PENDING | IN PROGRESS | ✅ COMPLETE
**Agents**: [count] parallel sub-agents
**Depends**: BATCH [N-1] (if applicable)

**Tasks (parallel - different files)**:
| Task | Files | Agent |
|------|-------|-------|
| Task-1 | file1.cs, file2.cs | Agent-NA |
| Task-2 | file3.cs | Agent-NB |
| Task-3 | file4.cs, file5.cs | Agent-NC |

**Gate GN**: [Verification criteria]
```

---

## Sub-Agent Instructions Template

When launching a sub-agent, include:

```markdown
**Task [ID]: [Name]**

**Files YOU OWN**:
- path/to/file1.cs
- path/to/file2.cs (create new)

**DO NOT modify**:
- path/to/other.cs (Agent-X is working on it)
- path/to/another.cs (Agent-Y is working on it)

**Steps**:
1. [Specific action]
2. [Specific action]
3. [Verification step]

**Verification**:
- `dotnet build [project]` - succeeds
- `dotnet run --project Tests/TestRunner/` - all tests pass

When done, summarize exactly what you changed.
```

---

## WIP Limits

Limit parallel agents to prevent:
- Context overload when reviewing results
- Resource contention
- Complexity in resolving conflicts

**Recommended**: 2-3 parallel agents per batch

---

## Verification Checklist

After each batch completes:

- [ ] All agents reported success
- [ ] Build passes: `dotnet build`
- [ ] Tests pass: `dotnet run --project Tests/TestRunner/`
- [ ] Changes reviewed (spot-check key files)
- [ ] Committed with descriptive message
- [ ] Pushed to remote (if appropriate)

---

## Anti-Patterns

### Don't: Assign Same File to Multiple Agents
```
❌ Agent-A: Modify RulesEngine.cs lines 1-100
   Agent-B: Modify RulesEngine.cs lines 101-200
```
Merge conflicts are likely. Have one agent do both, or run sequentially.

### Don't: Skip Verification Gates
```
❌ BATCH 5 done, BATCH 6 done, BATCH 7 done... now let's test
```
A failure in BATCH 7 might have been caused by BATCH 5. Test after each.

### Don't: Overload Batches
```
❌ BATCH 1: 10 parallel agents doing everything
```
Hard to review, hard to debug. Keep batches focused.

### Don't: Vague Instructions
```
❌ "Fix the thread safety issues"
```
Be specific about which files, which patterns, what verification.

---

## Example: Phase 2 Batches (This Project)

### BATCH 5: Foundation (3 parallel agents)
| Task | Files | Why Parallel |
|------|-------|--------------|
| P0-4 | AgentRouter.cs | Different file |
| P1-5 | AgentRoutingDefaults.cs (new) | Different file |
| P1-8 | Multiple files (DateTime only) | Non-overlapping changes |

### BATCH 6: Same File (1 agent, sequential)
| Task | Files | Why Sequential |
|------|-------|----------------|
| P1-1 | RulesEngineCore.cs | Same file |
| P1-6 | RulesEngineCore.cs | Same file |

Both tasks modified `RulesEngineCore.cs`, so one agent handled both.

### BATCH 7: Mixed (2 agents)
| Task | Files | Strategy |
|------|-------|----------|
| P1-2 + P1-3 | RulesEngineCore.cs | One agent (same file) |
| P1-4 | CommonMiddleware.cs | Separate agent (different file) |

---

## Recovery Strategies

### If a batch fails:
1. Don't panic - you have commits from previous batches
2. Identify which agent's changes caused the failure
3. Options:
   - Fix forward: Correct the issue and continue
   - Rollback: `git revert` or `git reset` to last good commit
   - Retry: Give agent clearer instructions and retry

### If agents conflict:
1. This shouldn't happen if file ownership was clear
2. If it does: keep one agent's changes, discard the other
3. Re-run the discarded agent's task in a new batch

---

## Summary

1. **Analyze first**: Map tasks to files before batching
2. **One owner per file**: Prevent conflicts by design
3. **Verify always**: Build + test after every batch
4. **Commit often**: Enable rollback
5. **Clear instructions**: Tell agents what they own and what to avoid
6. **Limit WIP**: 2-3 parallel agents maximum
