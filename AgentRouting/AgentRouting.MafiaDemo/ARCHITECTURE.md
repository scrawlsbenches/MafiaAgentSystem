# MafiaDemo Architecture

> A text-based mafia family simulation that exercises RulesEngine and AgentRouting.

---

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        MafiaGameEngine                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │  GameState   │  │ AgentRouter  │  │ RulesBasedGameEngine │  │
│  │  - Wealth    │  │  - Routing   │  │  - GameRules         │  │
│  │  - Heat      │  │  - Middleware│  │  - AgentRules        │  │
│  │  - Reputation│  │              │  │  - EventRules        │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌───────────────┐    ┌───────────────┐    ┌───────────────┐
│   Godfather   │    │   Underboss   │    │  Consigliere  │
│  (Strategic)  │◄───│ (Operations)  │    │   (Advisor)   │
└───────────────┘    └───────────────┘    └───────────────┘
                              │
                    ┌─────────┴─────────┐
                    ▼                   ▼
            ┌───────────────┐  ┌───────────────┐
            │     Capo      │  │     Capo      │
            │  (Territory)  │  │  (Territory)  │
            └───────────────┘  └───────────────┘
                    │
            ┌───────┴───────┐
            ▼               ▼
    ┌───────────────┐ ┌───────────────┐
    │   Soldier     │ │   Soldier     │
    │ (Enforcement) │ │ (Enforcement) │
    └───────────────┘ └───────────────┘
```

---

## Agent Hierarchy

### Rank Structure

| Rank | Class | Responsibilities | Message Categories |
|------|-------|------------------|-------------------|
| **Godfather** | `AutonomousGodfather` | Final decisions, major disputes, strategy | FinalDecision, MajorDispute, FavorRequest |
| **Underboss** | `AutonomousUnderboss` | Daily operations, crew management | DailyOperations, CrewManagement, Revenue |
| **Consigliere** | `AutonomousConsigliere` | Legal advice, strategy, negotiations | Legal, Strategy, Negotiations |
| **Capo** | `AutonomousCapo` | Territory control, soldier management | ProtectionRacket, CrewLeadership |
| **Soldier** | `AutonomousSoldier` | Enforcement, collections | Enforcement, Collections |

### Personality Traits

Each agent has personality traits (1-10 scale) that affect decisions:

```csharp
public int Ambition { get; }   // Desire to rise in ranks
public int Loyalty { get; }    // Loyalty to family
public int Aggression { get; } // Willingness to use violence
```

---

## Message Flow

### Orders Flow Down
```
Godfather ──► Underboss ──► Capo ──► Soldier
   │              │
   └──► Consigliere (advisory)
```

### Reports Flow Up
```
Soldier ──► Capo ──► Underboss ──► Godfather
                         │
              Consigliere ◄──┘ (strategic input)
```

### Message Structure

```csharp
public class AgentMessage
{
    public string SenderId { get; set; }
    public string ReceiverId { get; set; }
    public string Subject { get; set; }
    public string Content { get; set; }
    public string Category { get; set; }      // Routes to correct handler
    public MessagePriority Priority { get; set; }
}
```

---

## Decision System

### Current Implementation (Hardcoded Logic)

Each agent's `MakeDecision()` uses probability-based logic:

```csharp
public override AgentDecision? MakeDecision(GameState gameState, Random random)
{
    var roll = random.Next(0, 10);

    if (roll < 4) return CollectMoney();
    if (roll < 7) return SendOrders();
    // etc.
}
```

### Target Implementation (Rules-Driven)

Replace hardcoded logic with RulesEngine:

```csharp
public override AgentDecision? MakeDecision(GameState gameState, Random random)
{
    var context = new AgentDecisionContext
    {
        AgentId = Id,
        Aggression = Aggression,
        Greed = Greed,
        GameState = gameState
    };

    var matchingRules = _decisionRules.GetMatchingRules(context);
    return ExecuteHighestPriorityRule(matchingRules);
}
```

---

## Game State

### Core Metrics

| Metric | Range | Effect |
|--------|-------|--------|
| **FamilyWealth** | $0+ | Funds for operations. $0 = Game Over |
| **Reputation** | 0-100 | Respect in underworld. <10 = Betrayal |
| **HeatLevel** | 0-100 | Police attention. 100 = RICO |
| **Week** | 1+ | Turn counter. 52+ with goals = Victory |

### Territories

```csharp
public class Territory
{
    public string Name { get; set; }
    public string ControlledBy { get; set; }  // Capo ID
    public decimal WeeklyRevenue { get; set; }
    public int HeatGeneration { get; set; }
    public string Type { get; set; }  // Protection, Gambling, Smuggling
}
```

### Rival Families

```csharp
public class RivalFamily
{
    public string Name { get; set; }
    public int Strength { get; set; }    // 0-100
    public int Hostility { get; set; }   // 0-100, >70 = may attack
    public bool AtWar { get; set; }
}
```

---

## Rules Integration Points

The game uses **8 specialized `RulesEngineCore<T>` instances** with ~98 total rules:

### 1. Game Rules (`_gameRules` - `RulesEngineCore<GameRuleContext>`)

Evaluate game state, trigger victory/defeat conditions and warnings:

```csharp
// Example: High heat triggers police raid
engine.AddRule("POLICE_RAID", "Police raid when heat is critical",
    ctx => ctx.Heat > 80 && ctx.Week % 3 == 0,
    ctx => TriggerPoliceRaid(),
    priority: 100);
```

### 2. Agent Rules (`_agentRules` - `RulesEngineCore<AgentDecisionContext>`)

Drive agent decisions with ~45 personality-driven rules:

```csharp
// Example: Aggressive agent attacks when family is threatened
engine.AddRule("AGGRESSIVE_RESPONSE", "Attack when threatened",
    ctx => ctx.IsAggressive && ctx.FamilyUnderThreat,
    ctx => ctx.RecommendedAction = "intimidate",
    priority: 80);
```

### 3. Event Rules (`_eventRules` - `RulesEngineCore<EventContext>`)

Generate random events based on state:

```csharp
// Example: Informant appears when wealthy and high heat
engine.AddRule("INFORMANT_APPEARS", "Rat in the organization",
    ctx => ctx.WealthyTarget && ctx.PoliceAttentionHigh,
    ctx => SpawnInformantEvent(),
    priority: 50);
```

### 4. Valuation Engine (`_valuationEngine` - `RulesEngineCore<TerritoryValueContext>`)

Calculate territory values for strategic decisions:

```csharp
engine.AddRule("HIGH_REVENUE_BONUS", "Boost value for high revenue",
    ctx => ctx.WeeklyRevenue > 5000,
    ctx => ctx.ValueMultiplier *= 1.5,
    priority: 100);
```

### 5. Difficulty Engine (`_difficultyEngine` - `RulesEngineCore<DifficultyContext>`)

Adaptive difficulty scaling based on player performance:

```csharp
engine.AddRule("EASY_MODE", "Reduce difficulty when struggling",
    ctx => ctx.ConsecutiveLosses > 3,
    ctx => ctx.DifficultyModifier = 0.7,
    priority: 100);
```

### 6. Strategy Engine (`_strategyEngine` - `RulesEngineCore<RivalStrategyContext>`)

Control rival family AI behavior:

```csharp
engine.AddRule("AGGRESSIVE_RIVAL", "Rival attacks when player is weak",
    ctx => ctx.PlayerReputation < 30 && ctx.RivalStrength > 70,
    ctx => ctx.RivalAction = "attack",
    priority: 90);
```

### 7. Chain Engine (`_chainEngine` - `RulesEngineCore<ChainReactionContext>`)

Handle event cascades and chain reactions:

```csharp
engine.AddRule("RETALIATION_CHAIN", "Rival retaliates after attack",
    ctx => ctx.TriggerEvent == "hit_on_rival",
    ctx => ctx.ChainEvents.Add("rival_counter_attack"),
    priority: 100);
```

### 8. Async Rules (`_asyncRules` - `List<IAsyncRule<AsyncEventContext>>`)

Time-delayed operations for I/O-bound decisions:

```csharp
// Example: Police investigation with simulated delay
var investigation = new AsyncRuleBuilder<AsyncEventContext>()
    .WithId("POLICE_INVESTIGATION")
    .WithCondition(async ctx => ctx.Heat > 60)
    .WithAction(async ctx => {
        await Task.Delay(100); // Simulate investigation time
        ctx.Result = "Investigation launched";
    })
    .Build();
```

---

## Turn Execution Flow

```
┌─────────────────────────────────────────────────────────────┐
│                      ExecuteTurnAsync()                      │
├─────────────────────────────────────────────────────────────┤
│ 1. ProcessWeeklyCollections()                               │
│    └─► Collect revenue from all territories                 │
│                                                              │
│ 2. ProcessRandomEvents()                                     │
│    └─► Roll for police raids, opportunities, etc.           │
│                                                              │
│ 3. ProcessAutonomousActions()                               │
│    └─► Each agent makes decision based on personality       │
│                                                              │
│ 4. ProcessRivalFamilyActions()                              │
│    └─► Hostile rivals may attack                            │
│                                                              │
│ 5. UpdateGameState()                                         │
│    └─► Decay heat, clamp values                             │
│                                                              │
│ 6. CheckGameOver()                                           │
│    └─► Check win/loss conditions                            │
└─────────────────────────────────────────────────────────────┘
```

---

## Integration Status

### Completed
- [x] Agent hierarchy classes
- [x] AutonomousAgent base class
- [x] MafiaGameEngine turn loop
- [x] GameState tracking
- [x] Territory and rival systems
- [x] Player command interface
- [x] Message routing setup
- [x] RulesBasedGameEngine scaffolding
- [x] Context classes (GameRuleContext, AgentDecisionContext, EventContext)
- [x] Wire RulesBasedGameEngine to MafiaGameEngine (`GetAgentAction()` called in turn loop)
- [x] Replace hardcoded agent decisions with rules (~45 personality-driven rules)
- [x] Sophisticated event generation rules (7+ event rules with probability)
- [x] Connect agent message handling to routing pipeline (AgentRouter in GameEngine)
- [x] Async rule support for I/O-bound decisions (3 async rules: police investigation, informant network, business deals)
- [x] AI Autopilot mode using rules (AI Career Mode - Option 1 in Program.cs)
- [x] Personality effects on decisions (rules check Aggression, Loyalty, Ambition traits)

### Enhancement Opportunities
- [ ] Save/load game state persistence
- [ ] Inter-agent relationships and loyalty dynamics (beyond basic traits)
- [ ] Territory disputes with other families (more sophisticated AI)

---

## File Map

```
AgentRouting.MafiaDemo/
├── Game/
│   └── GameEngine.cs          # MafiaGameEngine, GameState, AutonomousAgent base
├── AutonomousAgents.cs        # Godfather, Underboss, Consigliere, Capo, Soldier
├── MafiaAgents.cs             # Basic agent implementations
├── PlayerAgent.cs             # Player-controlled agent
├── RulesBasedEngine.cs        # RulesEngine integration (needs wiring)
├── AdvancedRulesEngine.cs     # Additional rules and contexts
├── MissionSystem.cs           # Mission/quest system
├── GameTimingOptions.cs       # Centralized delay configuration
├── AutonomousPlaythrough.cs   # Demo/test scenarios
├── Program.cs                 # Entry point and command loop
└── ARCHITECTURE.md            # This document
```

---

## Next Steps

Remaining enhancement opportunities:

1. **Save/Load Game State**: Persist game progress to disk
2. **Deeper Agent Relationships**: Track loyalty shifts and betrayals based on actions
3. **Sophisticated Rival AI**: Territory disputes, alliances, and multi-front wars
4. **Additional Documentation**: Consolidate overlapping markdown files (see TASK_LIST.md F-1a)
