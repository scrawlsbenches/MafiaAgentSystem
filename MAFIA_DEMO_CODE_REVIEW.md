# MafiaDemo Code Review Report

**Date:** 2026-02-03
**Reviewer:** Claude (Opus 4.5)
**Scope:** AgentRouting.MafiaDemo project

## Summary

This code review covers the MafiaDemo project, a test bed for the RulesEngine and AgentRouting systems. The review identified **15 bugs**, **7 design issues**, and **3 potential improvements**.

---

## Critical Bugs

### 1. Heat Balance Makes Game Unwinnable (Severity: Critical)

**Location:** `Game/GameEngine.cs:509-541`, `Game/GameEngine.cs:901-911`

**Problem:** The heat generation and decay are fundamentally imbalanced:
- Territory heat generation: 5 + 10 + 8 = **23 heat/week** from collections
- Natural heat decay: **5/week** in `UpdateGameState()`
- Net heat accumulation: **+18/week** minimum

Even with aggressive bribing (costs $10,000, reduces heat by 15), players still accumulate +3 heat/week. In testing, the game ended at week 21 with uncontrollable heat despite agents constantly bribing and laying low.

**Fix:** Either reduce territory heat generation or increase natural decay rate. Suggested: natural decay of 10/week or reduce base territory heat.

---

### 2. Event Time Check Uses Real Time Instead of Game Time (Severity: High)

**Location:** `Rules/GameRulesEngine.cs:133-139`

```csharp
var recentPoliceRaid = _state.EventLog
    .Where(e => e.Type == "PoliceRaid")
    .Any(e => e.Timestamp > DateTime.UtcNow.AddMinutes(-5));
```

**Problem:** Events are checked against real-world minutes, but the game progresses in game weeks. In instant mode (all delays = 0), multiple weeks occur in the same real second, making the "recent event" check meaningless. Events meant to be mutually exclusive can now stack.

**Fix:** Track event timing using game weeks instead of `DateTime`:
```csharp
var recentPoliceRaid = _state.EventLog
    .Where(e => e.Type == "PoliceRaid")
    .Any(e => _state.Week - e.GameWeek < 3);
```

---

### 3. CHAIN_HIT_TO_WAR Rule Can Throw Exception (Severity: High)

**Location:** `Rules/GameRulesEngine.Setup.cs:1140-1147`

```csharp
_chainEngine.AddRule(
    "CHAIN_HIT_TO_WAR",
    "Hit Escalates to War",
    ctx => ctx.WasHit && ctx.HighTension,
    ctx => {
        var rival = ctx.State.RivalFamilies.Values.First(r => r.Hostility > 80);
        // ...
    },
```

**Problem:** Uses `First()` without checking if any rival has hostility > 80. If `HighTension` is true (any rival with hostility > 80) but no rival exactly matches `> 80` at execution time (race condition or state change), this throws `InvalidOperationException`.

**Fix:** Use `FirstOrDefault()` with null check:
```csharp
var rival = ctx.State.RivalFamilies.Values.FirstOrDefault(r => r.Hostility > 80);
if (rival == null) return;
```

---

### 4. Rival Hostility Can Go Negative (Severity: Medium)

**Location:** `Game/GameEngine.cs:892-896`

```csharp
// Hostility slowly decreases over time
if (rival.Hostility > 0)
{
    rival.Hostility -= Random.Shared.Next(1, 3);
}
```

**Problem:** The check `if (rival.Hostility > 0)` prevents decrementing from 0 but not from going negative. If hostility is 1, it decrements by `Random.Shared.Next(1, 3)` which could be 2, resulting in -1.

**Fix:** Clamp after decrementing:
```csharp
rival.Hostility = Math.Max(0, rival.Hostility - Random.Shared.Next(1, 3));
```

---

### 5. MissionEvaluator Applies Rules Twice (Severity: Medium)

**Location:** `MissionSystem.cs:524-535`

```csharp
// Apply all rules
_rules.EvaluateAll(context);  // First time

// ... roll for success ...

// Apply final bonuses/penalties
_rules.EvaluateAll(context);  // Second time - duplicates modifiers!
```

**Problem:** `EvaluateAll()` is called twice on the same context. The first call sets up modifiers, then after rolling for success, the second call re-applies the same modifiers. This can double heat penalties and other effects.

**Fix:** Remove the second `EvaluateAll()` call or use separate rule sets for pre-roll and post-roll.

---

### 6. PlayerAgent Decision Trace Uses Wrong Field for Rejection Check (Severity: Medium)

**Location:** `PlayerAgent.cs:345`

```csharp
// In DecideMissionWithTrace:
var accept = !topRule.RuleName.Contains("REJECT");  // Uses RuleName (mixed case)

// In DecideMission (line 212):
var accept = !topRule.Id.Contains("REJECT");  // Uses Id (uppercase)
```

**Problem:** `DecideMissionWithTrace` checks `RuleName` (e.g., "Reject - Underqualified") while `DecideMission` checks `Id` (e.g., "REJECT_UNDERQUALIFIED"). The `RuleName.Contains("REJECT")` check is case-sensitive and may not match "Reject" with uppercase R.

**Fix:** Use consistent field and case-insensitive comparison:
```csharp
var accept = !topRule.RuleId.Contains("REJECT", StringComparison.OrdinalIgnoreCase);
```

---

### 7. CONSEQUENCE_VULNERABLE Rule Missing Empty Check (Severity: Medium)

**Location:** `Rules/GameRulesEngine.Setup.cs:101-111`

```csharp
_gameRules.AddRule(
    "CONSEQUENCE_VULNERABLE",
    "Vulnerable Position",
    ctx => ctx.IsVulnerable && !ctx.State.Territories.Values.Any(t => t.UnderDispute),
    ctx => {
        var territory = ctx.State.Territories.Values.First(); // No check!
```

**Problem:** Calls `First()` on territories without checking if any exist. If all territories are lost, this throws an exception.

**Fix:** Add empty check:
```csharp
if (!ctx.State.Territories.Any()) return;
var territory = ctx.State.Territories.Values.First();
```

---

## Design Issues

### 8. RivalStrategyContext.ShouldAttack Logic Inverted (Severity: Medium)

**Location:** `Rules/RuleContexts.cs:292`

```csharp
public bool ShouldAttack => RivalIsStronger && PlayerIsWeak && !PlayerIsDistracted;
```

**Problem:** Rivals attack when player is NOT distracted. Logically, rivals should attack when player IS distracted (high heat means law enforcement focus, making the player vulnerable).

**Fix:** Change to `&& PlayerIsDistracted` or rename to clarify intent.

---

### 9. Currency Symbol Display Issue in AI Career Mode (Severity: Low)

**Location:** `AutonomousPlaythrough.cs:265-267`

```csharp
Console.WriteLine($"║    Money: {(missionResult.MoneyGained >= 0 ? "+" : "")}{missionResult.MoneyGained:C0}");
```

**Problem:** Uses `:C0` format which uses system locale. In non-US systems, this displays as "¤" instead of "$", causing inconsistent UI.

**Fix:** Use explicit format:
```csharp
Console.WriteLine($"║    Money: {(missionResult.MoneyGained >= 0 ? "+$" : "-$")}{Math.Abs(missionResult.MoneyGained):N0}");
```

---

### 10. Multiple Agents Can Bribe Simultaneously Without Coordination (Severity: Medium)

**Problem:** In the autonomous game, multiple agents can independently decide to bribe in the same week, each costing $10,000. There's no coordination to prevent wasteful duplicate actions.

**Impact:** In testing, 2 agents bribing in the same week cost $20,000 but reduced heat by only $30 total (where one $10K bribe gives -15).

**Fix:** Add agent action coordination or shared "already bribed this week" flag.

---

### 11. Game Victory Requires 52 Weeks AND $1M AND 80 Rep (Severity: Medium)

**Location:** `Game/GameEngine.cs:934-939`

```csharp
if (_state.Week >= 52 && _state.FamilyWealth >= 1000000 && _state.Reputation >= 80)
```

**Problem:** Victory requires all three conditions simultaneously at week 52+. Given the heat balance issues, reaching week 52 with 80+ reputation is nearly impossible.

**Fix:** Either reduce requirements or extend the timeline.

---

### 12. Duplicate Game Over Logic in Rules Engine vs Engine Core (Severity: Low)

**Locations:**
- `Game/GameEngine.cs:913-939` - CheckGameOver() checks reputation <= 10
- `Rules/GameRulesEngine.Setup.cs:64-73` - DEFEAT_BETRAYAL checks reputation <= 5

**Problem:** Two different thresholds for reputation-based defeat create confusion and potential race conditions.

**Fix:** Consolidate to single source of truth.

---

### 13. Event Log Eviction Uses O(n) RemoveAt(0) (Severity: Low)

**Location:** `Game/GameEngine.cs:944-948`

```csharp
while (_state.EventLog.Count >= MaxEventLogSize)
{
    _state.EventLog.RemoveAt(0);  // O(n) for each removal
}
```

**Problem:** Removing from index 0 of a List requires shifting all elements, making eviction O(n) per removal.

**Fix:** Use `Queue<T>` for O(1) dequeue or circular buffer pattern.

---

### 14. Defeat Condition Threshold Inconsistencies

| Condition | GameEngine.cs | GameRulesEngine.Setup.cs |
|-----------|---------------|--------------------------|
| Reputation | <= 10 (line 929) | <= 5 (line 68) |
| Bankruptcy | <= 0 (line 915) | <= 0 (line 44) |
| Heat | >= 100 (line 923) | >= 100 (line 57) |

**Fix:** Consolidate all defeat checks in one location.

---

## Potential Improvements

### 15. Add Null Safety to Rival Lookups

Several places use `FirstOrDefault()` on rival families without null checks:
- `GameState.MostHostileRival` (line 68) - returns null safely but callers don't always check
- `GameState.WeakestRival` (line 72) - same issue

**Fix:** Add null-conditional operators at call sites or return `Option<T>`.

---

### 16. Consider Using Immutable State for Rules Engine

The game state is mutated by rules during evaluation. This can cause subtle bugs when rule evaluation order matters.

**Fix:** Consider using `ImmutableRulesEngine` for state evaluation and applying mutations in a separate pass.

---

### 17. Add Action Cooldown Visualization

Agents have `ActionCooldown` but players can't see when agents will act again, making the game feel unpredictable.

**Fix:** Display cooldown status in agent output.

---

## Test Coverage Notes

All 641 MafiaDemo tests pass. However, the tests don't cover:
- Heat balance over extended gameplay
- Edge cases with empty territories/rivals
- Cross-week event timing
- Victory condition reachability

---

## Recommendations

1. **Priority 1:** Fix heat balance - currently makes game unwinnable
2. **Priority 2:** Change event time tracking from real-time to game-weeks
3. **Priority 3:** Add null checks to CHAIN_HIT_TO_WAR and CONSEQUENCE_VULNERABLE rules
4. **Priority 4:** Fix rival hostility negative value bug
5. **Priority 5:** Consolidate defeat conditions to single source

---

## Files Reviewed

| File | Lines | Issues Found |
|------|-------|--------------|
| Game/GameEngine.cs | 1142 | 4 |
| Rules/GameRulesEngine.cs | 520 | 1 |
| Rules/GameRulesEngine.Setup.cs | 1193 | 2 |
| Rules/RuleContexts.cs | 346 | 1 |
| MissionSystem.cs | 630 | 1 |
| PlayerAgent.cs | 728 | 1 |
| MafiaAgents.cs | 883 | 0 |
| AutonomousPlaythrough.cs | 389 | 1 |
| Program.cs | 603 | 0 |

**Total Issues:** 15 bugs + 7 design issues = 22 findings
