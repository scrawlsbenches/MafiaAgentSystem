---
paths:
  - "AgentRouting/AgentRouting.MafiaDemo/**/*.cs"
  - "Tests/MafiaDemo.Tests/**/*.cs"
---

# MafiaDemo — SOPs and Reference

## Purpose

MafiaDemo is a **test bed**, not a product. Its purpose is to exercise RulesEngine and AgentRouting through real-world usage patterns and find API gaps. Treat it as a stress test for the core libraries.

## Before Modifying Game Code

1. Understand the 8 rules engine instances and their contexts:
   - `_gameRules` (GameRuleContext) — victory/defeat, warnings
   - `_agentRules` (AgentDecisionContext) — AI agent decisions (~45 rules)
   - `_eventRules` (EventContext) — random event generation
   - `_valuationEngine` (TerritoryValueContext) — economic pricing
   - `_difficultyEngine` (DifficultyContext) — adaptive difficulty
   - `_strategyEngine` (RivalStrategyContext) — rival AI
   - `_chainEngine` (ChainReactionContext) — event cascades
   - `_asyncRules` (AsyncEventContext) — time-delayed operations
2. Determine which engine(s) your change affects
3. Read `AgentRouting/AgentRouting.MafiaDemo/ARCHITECTURE.md` for the full game architecture
4. Run baseline: `dotnet run --project Tests/TestRunner/ --no-build -- Tests/MafiaDemo.Tests/bin/Debug/net8.0/MafiaDemo.Tests.dll`

## Key Namespaces

| Namespace | Purpose |
|-----------|---------|
| `MafiaDemo.Game` | GameState, Territory, RivalFamily, AutonomousAgent base |
| `MafiaDemo.Rules` | Rules engine integration for game logic |
| `MafiaDemo.Missions` | Mission system with player progression |
| `MafiaDemo.AI` | PlayerAgent with rules-driven decisions |
| `MafiaDemo.Autonomous` | NPC agents (Godfather, Underboss, etc.) |

## Game Timing in Tests

Tests must use `GameTimingOptions.Instant` to avoid real delays:
```csharp
GameTimingOptions.Current = GameTimingOptions.Instant; // No delays
```
If a test is slow, check that timing is set to Instant.

## When Finding an API Gap

This is the primary value of MafiaDemo. When game code requires something the core libraries don't support:
1. Document the gap — what does the game need that doesn't exist?
2. Determine which core library should provide it (RulesEngine or AgentRouting)
3. Add the gap to `TASK_LIST.md` with context
4. Implement a workaround in MafiaDemo if needed for now

## After Changes

1. Run MafiaDemo tests
2. If you modified how the game uses rules or agents, consider whether the usage pattern reveals a core library issue worth documenting
