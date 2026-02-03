# MafiaGame Bug Report

**Date:** 2026-02-03
**Tester:** Claude Code
**Modes Tested:** All three (AI Career, Autonomous Game, Scripted Demo)

## Summary

Found 5 bugs during gameplay testing. Bug #2 is a critical logic error affecting game mechanics.

---

## Bug 1: Console.ReadKey() Crashes in Non-Interactive Mode

**Severity:** Medium
**Files:**
- `Program.cs:71`
- `Program.cs:97`
- `AutonomousPlaythrough.cs:190`

**Description:**
All game modes call `Console.ReadKey()` at the end which throws `InvalidOperationException` when console input is redirected (e.g., piped input, CI/CD environments).

**Error:**
```
System.InvalidOperationException: Cannot read keys when either application does not have a console or when console input has been redirected. Try Console.Read.
```

**Suggested Fix:**
Use `Console.IsInputRedirected` check before calling `ReadKey()`:
```csharp
if (!Console.IsInputRedirected)
    Console.ReadKey();
```

---

## Bug 2: Decision Logic Case-Sensitivity Bug (CRITICAL)

**Severity:** Critical
**File:** `PlayerAgent.cs:211`

**Description:**
The decision logic uses a case-sensitive string check to determine if a rule should reject:
```csharp
var accept = !topRule.Name.Contains("REJECT");
```

However, the rule names use mixed case like "Reject - Underqualified", so `Contains("REJECT")` always returns `false`, causing ALL missions to be accepted regardless of the rule's intent.

**Example Output:**
```
║ ✓ DECISION: ACCEPT
║    Reason: Reject - Underqualified   <-- WRONG! Should be REJECT
║    Rule Matched: Reject - Underqualified
```

**Impact:**
- Players accept missions they should reject (underqualified, too risky, etc.)
- Rule engine conditions are evaluated correctly but decision is inverted
- Makes the game much easier than intended

**Suggested Fix:**
Use case-insensitive comparison:
```csharp
var accept = !topRule.Name.Contains("Reject", StringComparison.OrdinalIgnoreCase);
```
Or use the rule ID which is uppercase:
```csharp
var accept = !topRule.Id.Contains("REJECT");
```

---

## Bug 3: Repeated Promotion Display

**Severity:** Low
**Files:**
- `AutonomousPlaythrough.cs:149-157`
- `AutonomousPlaythrough.cs:378-394` (`GetPreviousRank()`)

**Description:**
The promotion notification shows "Associate → Soldier" on every single week after the first promotion, not just when a new promotion occurs.

**Root Cause:**
`GetPreviousRank()` checks if ANY achievement contains "Promoted to" and returns the previous rank. It doesn't check if the promotion is NEW this week.

**Example:**
```
Week 7: Promoted! Associate → Soldier (correct)
Week 8: Promoted! Associate → Soldier (WRONG - no promotion happened)
Week 9: Promoted! Associate → Soldier (WRONG - no promotion happened)
```

**Suggested Fix:**
Track whether promotion occurred in `MissionExecutionResult` or compare ranks before/after processing:
```csharp
var oldRank = player.Character.Rank;
var weekResult = await player.ProcessWeekAsync(gameState);
if (player.Character.Rank != oldRank)
{
    PrintPromotion(oldRank, player.Character.Rank);
}
```

---

## Bug 4: Agent Routing Shows "FAILED" Incorrectly

**Severity:** Low (cosmetic)
**File:** Middleware/autonomous game integration

**Description:**
In Autonomous Game mode, all agent actions display `[route failed]` even when they succeed:
```
underboss-001 bribes officials - Cost $10,000, Heat -15 [route failed]
capo-001 makes an extra collection: $4,552 [route failed]
```

**Impact:**
Confusing output - makes it look like the routing system is broken when it's working fine.

---

## Bug 5: Week Counter Off-By-One

**Severity:** Low
**File:** `AutonomousPlaythrough.cs` / `ProcessWeekAsync()`

**Description:**
When setting max weeks to 10, the final statistics show "Weeks Played: 11".

**Root Cause:**
`ProcessWeekAsync()` increments `_character.Week` at the start (line 517), so the count is off by one in the final summary.

---

## Recommendations

1. **Fix Bug #2 immediately** - it fundamentally breaks the decision-making AI
2. Fix Bug #1 to support CI/CD testing
3. Fix Bug #3 for better user experience
4. Bugs #4 and #5 are lower priority cosmetic issues

## Test Commands Used

```bash
# Scripted Demo
echo "3" | dotnet run --project AgentRouting/AgentRouting.MafiaDemo/ --no-build

# AI Career Mode (10 weeks, instant, random personality)
printf "1\n\n4\n10\n\n" | dotnet run --project AgentRouting/AgentRouting.MafiaDemo/ --no-build

# Autonomous Game (30 second timeout)
printf "2\n" | timeout 30 dotnet run --project AgentRouting/AgentRouting.MafiaDemo/ --no-build
```
