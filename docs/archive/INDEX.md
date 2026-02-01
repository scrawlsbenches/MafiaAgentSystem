# Archived Documentation

This folder contains historical documentation that was useful during development but is now superseded by current documentation.

## Why Keep These?

These files contain **rationale and context** for design decisions. While the issues they describe are resolved, understanding *why* certain patterns were adopted helps future development.

## Contents

| File | Original Purpose | Status |
|------|------------------|--------|
| `mafia-code-review.md` | Initial code review identifying P0-P2 issues | All issues resolved |
| `KANBAN_WORKFLOW.md` | Multi-agent orchestration process | Process complete, methodology in SKILL files |
| `MafiaDemo-CODE_REVIEW.md` | Game compilation issues | All issues resolved |
| `RulesEngine-CODE_REVIEW.md` | Thread safety, caching analysis | All issues resolved |
| `IMPLEMENTATION_SUMMARY.md` | Early implementation tracking | Superseded by README |

## Current Documentation

For current project state, see:
- `CLAUDE.md` - Development guide
- `ORIGINS.md` - Project history and design rationale
- `TASK_LIST.md` - Remaining work
- `EXECUTION_PLAN.md` - Completed work with batch logs

## When to Reference These

- **Understanding design decisions**: Why was `ReaderWriterLockSlim` chosen?
- **Seeing the evolution**: How did the architecture emerge?
- **Learning from reviews**: What patterns were identified and applied?
