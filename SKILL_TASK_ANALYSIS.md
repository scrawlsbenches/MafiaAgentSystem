# Skill: Task Analysis and Breakdown

> How to analyze complex requests, break them into actionable tasks, prioritize effectively, and size work appropriately.

---

## The Analysis Process

### Phase 1: Understand the Landscape

Before creating tasks, understand what exists:

1. **Read the codebase structure**
   - What are the main projects/modules?
   - What patterns are already established?
   - What constraints exist (dependencies, standards)?

2. **Read existing documentation**
   - CLAUDE.md, README, architecture docs
   - Existing task lists or plans
   - Historical context (transcripts, commit history)

3. **Verify the current state**
   - Does the code build?
   - Do tests pass?
   - What's working vs broken?

### Phase 2: Identify Work Categories

Categorize tasks by impact and urgency:

| Priority | Category | Criteria |
|----------|----------|----------|
| P0 | Critical Fixes | Blocks other work, build failures, crashes |
| P1 | Core Improvements | Important functionality, enables future work |
| P2 | Feature Completion | Fills gaps, completes partial implementations |
| P3 | Quality & Testing | Test coverage, edge cases, performance |
| P4 | Polish & Docs | Documentation, cleanup, nice-to-haves |

### Phase 3: Break Down Tasks

Each task should be:
- **Specific**: Clear deliverable, not vague
- **Sized**: 2-4 hours, never more than 1 day
- **Testable**: Has verification criteria
- **Independent**: Minimal dependencies on other tasks

---

## Task Sizing Guidelines

### Too Big (Break It Down)
```
❌ "Implement the MafiaDemo game"
```
This is a project, not a task. Break into: agent hierarchy, game loop, rules integration, etc.

### Too Small (Combine)
```
❌ "Add a comment to line 47"
```
This is a line item, not a task. Combine with related cleanup.

### Just Right
```
✅ "Add thread safety to RulesEngineCore using ReaderWriterLockSlim"
- Specific: which class, which mechanism
- Sized: 2-3 hours
- Testable: concurrent access tests pass
- Independent: doesn't require other tasks first
```

---

## Dependency Analysis

### Identify Dependencies
For each task, ask:
- What must exist before this can start?
- What files does this modify?
- What does this enable for other tasks?

### Build the Graph
```
P0-1 (fix build) ─┬─→ P1-1 (thread safety) ─→ P1-2 (async)
                  │                           ↓
                  └─→ P1-5 (config) ──────→ P1-4 (caching)
```

### Find Parallelism
Tasks with no dependency arrows between them can run in parallel (if they don't touch the same files).

---

## Task Template

```markdown
### Task [ID]: [Name]
**Estimated Time**: [X-Y hours]
**Dependencies**: [List or None]
**Files**: [List of files to modify]

**Problem**: [What's wrong or missing]

**Subtasks**:
- [ ] Subtask 1
- [ ] Subtask 2
- [ ] Subtask 3

**Acceptance Criteria**:
- [Criterion 1]
- [Criterion 2]
```

---

## Prioritization Matrix

Use this to decide what to do first:

```
                    URGENT
                      │
         P0          │         P1
    Critical Fixes   │   Core Improvements
    (build broken)   │   (enables other work)
                     │
    ─────────────────┼─────────────────
                     │
         P3          │         P2
    Quality/Testing  │   Feature Completion
    (can wait)       │   (adds value)
                     │
                 NOT URGENT

    LOW IMPACT ──────┼────── HIGH IMPACT
```

---

## The "Think It Through" Checklist

Before executing, verify your task breakdown:

### Start to Finish
- [ ] Can I complete task 1 without anything else?
- [ ] Does task 2 have everything it needs from task 1?
- [ ] Are there hidden dependencies I missed?

### Inside the Box
- [ ] Do I have all the technical information needed?
- [ ] Are the file paths correct?
- [ ] Do I know the existing patterns to follow?

### Outside the Box
- [ ] What could go wrong?
- [ ] What if a task fails - can I recover?
- [ ] Are there simpler alternatives?

### Constraints
- [ ] No third-party dependencies?
- [ ] Follows existing patterns?
- [ ] Stays within scope (no scope creep)?

---

## Common Pitfalls

### Pitfall: Starting Without Reading
```
❌ "I'll just start coding the solution"
```
Always read existing code first. Understand patterns, find related implementations.

### Pitfall: Underestimating Dependencies
```
❌ "These tasks are independent"
   [Later] "Oh, they both modify the same file"
```
Map file changes explicitly before claiming independence.

### Pitfall: Scope Creep
```
❌ "While I'm here, I'll also refactor this..."
```
Stay focused. Create new tasks for discovered work.

### Pitfall: Vague Acceptance Criteria
```
❌ "It should work better"
```
Define specific, measurable criteria: tests pass, build succeeds, specific behavior observed.

---

## Example: Analyzing This Project

### What We Found (Phase 1)
- Two main systems: RulesEngine and AgentRouting
- Zero third-party dependency constraint
- Custom TestRunner (no xUnit)
- GameEngine.cs had 44 compilation errors

### How We Categorized (Phase 2)
| Priority | Example Tasks |
|----------|---------------|
| P0 | Fix 44 compilation errors (blocks everything) |
| P1 | Add thread safety, async support (core improvements) |
| P2 | Complete MafiaDemo agents (feature completion) |
| P3 | Add concurrency tests (quality) |
| P4 | Update documentation (polish) |

### How We Broke Down (Phase 3)
Original: "Add thread safety to RulesEngine"

Broken down:
1. Add ReaderWriterLockSlim field
2. Wrap write operations (RegisterRule, RemoveRule, etc.)
3. Wrap read operations (Execute, GetRules, etc.)
4. Implement IDisposable
5. Add concurrency tests

Each subtask is specific, testable, and sized appropriately.

---

## Summary

1. **Understand first**: Read code, docs, and current state before planning
2. **Categorize by priority**: P0 (critical) through P4 (polish)
3. **Size appropriately**: 2-4 hours per task, never more than 1 day
4. **Map dependencies**: Know what blocks what
5. **Define acceptance criteria**: Specific, measurable, testable
6. **Think it through**: Start to finish, inside and outside the box
7. **Avoid scope creep**: Stay focused, create new tasks for discoveries
