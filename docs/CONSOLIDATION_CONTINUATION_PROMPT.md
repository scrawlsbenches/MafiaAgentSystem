# C# File Consolidation - Continuation Prompt

Use this prompt to continue the file consolidation work in a new session.

---

## Prompt

```
Continue the C# file consolidation work for this codebase.

## Project Overview
This is the MafiaAgentSystem - a .NET 8 project with:
- **RulesEngine**: Expression-based business rules library
- **AgentRouting**: Message routing with middleware pipeline
- **MafiaDemo**: Test bed game exercising both libraries

## Work Completed
Two consolidations have been finished:

1. **MafiaDemo Rules** (done):
   - `RulesBasedEngine.cs` + `AdvancedRulesEngine.cs` → `GameRulesEngine.cs`
   - Deleted old files, updated all references

2. **RulesEngine Core** (done):
   - `ThreadSafeRulesEngine.cs` merged into `RulesEngineCore.cs`
   - Added `ImmutableRulesEngine<T>` class
   - Removed redundant `LockedRulesEngine<T>`

## Remaining Work
See `docs/CSHARP_MERGE_OPPORTUNITIES.md` for the full report. Next priority:

**CRITICAL: MafiaAgents.cs + AutonomousAgents.cs**
- Location: `AgentRouting/AgentRouting.MafiaDemo/`
- Issue: Duplicate agent hierarchies (~70% overlap)
- Estimated savings: 400-500 lines

## Approach Guidelines
1. Consolidate **max two files at a time**
2. **Best effort** - perfection is impossible, we cannot fully test
3. **NO backwards compatibility aliases** - they're confusing, just update references
4. Update tests to use new class names
5. Pre-existing bugs may surface - fix them as encountered

## Build & Test Commands
Read `CLAUDE.md.onhold` for full details. Quick reference:

```bash
# Restore (NuGet is blocked - use offline)
dotnet restore AgentRouting/AgentRouting.sln --source /nonexistent
dotnet restore Tests/RulesEngine.Tests/ --source /nonexistent
dotnet restore Tests/AgentRouting.Tests/ --source /nonexistent
dotnet restore Tests/MafiaDemo.Tests/ --source /nonexistent
dotnet restore Tests/TestRunner/ --source /nonexistent

# Build
dotnet build AgentRouting/AgentRouting.sln --no-restore
dotnet build Tests/RulesEngine.Tests/ --no-restore
dotnet build Tests/AgentRouting.Tests/ --no-restore
dotnet build Tests/MafiaDemo.Tests/ --no-restore
dotnet build Tests/TestRunner/ --no-restore

# Run tests (1772 currently passing)
dotnet run --project Tests/TestRunner/ --no-build
```

## Gotchas from Previous Session
- Tests reference old class names - search and replace needed
- API changes (e.g., constructor params) require test updates
- Some test bugs are pre-existing (wrong method names like `ProcessAsync` vs `ProcessMessageAsync`)
- The `RulesEngine.Enhanced` namespace still exists for `RuleValidator`, `DebuggableRule`, `RuleAnalyzer`

## Key Files
- `docs/CSHARP_MERGE_OPPORTUNITIES.md` - Remaining merge opportunities
- `CLAUDE.md.onhold` - Build/test instructions
- `AgentRouting/AgentRouting.MafiaDemo/GameRulesEngine.cs` - Example of consolidated file
- `RulesEngine/RulesEngine/Core/RulesEngineCore.cs` - Example with ImmutableRulesEngine added

Please read `docs/CSHARP_MERGE_OPPORTUNITIES.md` and continue with the next consolidation.
```

---

## Current State (as of 2026-02-02)

| Metric | Value |
|--------|-------|
| Tests passing | 1772 |
| Demos working | All 5 |
| Branch | `claude/organize-files-metadata-10HSC` |

## Files Deleted This Session
- `AgentRouting/AgentRouting.MafiaDemo/RulesBasedEngine.cs`
- `AgentRouting/AgentRouting.MafiaDemo/AdvancedRulesEngine.cs`
- `RulesEngine/RulesEngine/Enhanced/ThreadSafeRulesEngine.cs`
