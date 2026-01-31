# 🎮 AI Career Mode - Autonomous Agent Playthrough

## 🎯 What Is This?

An **AI agent that plays an entire mafia career game autonomously**, from Associate to Don, using the **Rules Engine** to make ALL decisions!

Watch as the AI:
- ✅ Accepts or rejects missions based on risk/reward
- ✅ Builds skills through experience
- ✅ Advances through the ranks
- ✅ Balances money, respect, and heat
- ✅ Makes personality-driven decisions
- ✅ Creates emergent narratives

**Every playthrough is different!**

---

## 🚀 How to Run

```bash
cd AgentRouting.MafiaDemo
dotnet run
```

Choose option **1** for AI Career Mode!

---

## 🎮 Game Flow

### **Career Path**
```
👥 Associate (Week 1-10)
  ↓ (40 Respect required)
👊 Soldier (Week 10-20)
  ↓ (70 Respect required)
💼 Capo (Week 20-35)
  ↓ (85 Respect required)
🤵 Underboss (Week 35-50)
  ↓ (95 Respect required)
👑 Don (Victory!)
```

### **Sample Playthrough**
```
╔═══ WEEK 1 ═══════════════════════════════════════════════════╗
║
║ 📋 NEW MISSION: Collect from Tony's Restaurant
║    Go collect the weekly payment from Tony's. They owe $650.
║
║    Type: Collection
║    Risk Level: ▓▓░░░░░░░░ (2/10)
║    Reward: +3 Respect, $162
║    Heat: +2
║
║ ✓ DECISION: ACCEPT
║    Reason: Accept Safe Mission - Building Rep
║    Rule Matched: ACCEPT_SAFE_BUILDING
║    Confidence: 75%
║
║ ★ MISSION SUCCESS!
║    You collected the money. They weren't happy, but they paid.
║
║    Respect: +3
║    Money: +$162
║    Heat: +2
║    Skills Improved: StreetSmarts +2
║
║ 📊 CURRENT STATUS:
║    Rank: Associate
║    Respect: [■■□□□□□□□□□□□□□□□□□□] 13/100
║    Money: $1,162
║    Heat: [□□□□□□□□□□□□□□□□□□□□] 2/100
╚════════════════════════════════════════════════════════════════╝

╔═══ WEEK 5 ═══════════════════════════════════════════════════╗
║
║ 📋 NEW MISSION: Send a message to the bar owner
║    The bar owner has refused protection. Make sure they 
║    understand this can't continue.
║
║    Type: Intimidation
║    Risk Level: ▓▓▓▓░░░░░░ (4/10)
║    Reward: +5 Respect, $100
║    Heat: +5
║    Skills needed: Intimidation:15
║
║ ✓ DECISION: ACCEPT
║    Reason: Accept - Default
║    Rule Matched: ACCEPT_DEFAULT
║    Confidence: 65%
║
║ ★ MISSION SUCCESS!
║    Message delivered. They won't forget it.
║
║    Respect: +5
║    Money: +$100
║    Heat: +5
║    Skills Improved: Intimidation +2
║
║ 📊 CURRENT STATUS:
║    Rank: Associate
║    Respect: [■■■■■■■□□□□□□□□□□□□□] 35/100
║    Money: $2,450
║    Heat: [■■□□□□□□□□□□□□□□□□□□] 12/100
╚════════════════════════════════════════════════════════════════╝

╔════════════════════════════════════════════════════════════════╗
║                                                                ║
║                    🎉 PROMOTED! 🎉                             ║
║                                                                ║
║              Associate → Soldier                               ║
║                                                                ║
╚════════════════════════════════════════════════════════════════╝
```

---

## 🧠 How the AI Makes Decisions

### **Rules Engine Decision-Making**

The PlayerAgent uses a **RulesEngineCore<PlayerDecisionContext>** with 8 decision rules:

```csharp
Priority 1000: ACCEPT_DESPERATE
  IF: IsLowOnMoney AND MissionIsSafe
  THEN: Accept mission

Priority 950: REJECT_UNDERQUALIFIED
  IF: NOT MeetsSkillRequirements
  THEN: Reject mission

Priority 900: REJECT_TOO_HOT
  IF: UnderHeat AND MissionIsRisky
  THEN: Reject mission

Priority 850: ACCEPT_HIGH_REWARD
  IF: MissionIsHighReward AND CanAffordRisk
  THEN: Accept mission

Priority 800: ACCEPT_AMBITIOUS
  IF: Player.Personality.IsAmbitious AND CanAffordRisk
  THEN: Accept mission

Priority 800: REJECT_CAUTIOUS
  IF: Player.Personality.IsCautious AND MissionIsRisky
  THEN: Reject mission

Priority 700: ACCEPT_SAFE_BUILDING
  IF: HasLowRespect AND MissionIsSafe
  THEN: Accept mission

Priority 500: ACCEPT_DEFAULT
  IF: MeetsSkillRequirements AND NOT UnderHeat
  THEN: Accept mission
```

**The highest priority matching rule wins!**

---

## 🎭 Personality Types

### **1. Ambitious & Reckless**
```
Ambition: 85
Caution: 25
```
- Takes high-risk missions
- Fast career progression
- Higher failure rate
- Often under heat

### **2. Loyal & Cautious**
```
Loyalty: 90
Caution: 80
```
- Only safe missions
- Slower but steady progress
- Low heat
- High success rate

### **3. Ruthless & Calculating**
```
Ruthlessness: 90
Caution: 60
```
- Strategic risk-taking
- Balanced approach
- Maximizes rewards
- Adapts to situation

### **4. Random**
Randomly generated personality - every playthrough is unique!

---

## 📊 Mission Types by Rank

| Mission Type | Min Rank | Risk | Reward | Description |
|-------------|----------|------|--------|-------------|
| **Collection** | Associate | 1-3 | Low | Collect protection money |
| **Intimidation** | Associate | 3-6 | Medium | Send a message |
| **Information** | Associate | 2-5 | Medium | Gather intelligence |
| **Negotiation** | Soldier | 4-7 | High | Diplomatic missions |
| **Recruitment** | Capo | 3 | Low | Recruit new soldiers |
| **Territory** | Capo | 6-9 | High | Expand/defend territory |
| **Hit** | Underboss | 10 | Very High | Assassination |

---

## 🎯 Game Mechanics

### **Success Calculation**

Mission success uses **MissionEvaluator** with rules engine:

```csharp
Base success chance: 50%

Rules applied:
+ Skill advantage > 30? → 100% success (auto-win)
+ Skill advantage > 10? → +(advantage)% to success
+ Skill advantage < -10? → +(advantage)% to success (negative)
+ High risk (8+)? → If success, bonus rewards
+ Player heat < 30? → +10% success
+ Player heat > 70? → -20% success, +10 heat penalty

Final success chance: 10% to 95% (clamped)
```

**Then rolls 1-100 to determine actual outcome!**

### **Skill Progression**

Skills improve through missions:
- **Success:** +2 to relevant skills
- **Failure:** +1 to relevant skills (learn from mistakes!)

```
Skills (0-100):
- Intimidation (for Collection, Intimidation, Hit)
- Negotiation (for Negotiation, Territory)
- StreetSmarts (for Information, Collection)
- Leadership (for Territory, Recruitment)
- Business (for Territory, Management)
```

### **Heat Management**

```
Heat increases from:
+ Missions (2-30 based on risk)
+ Failed missions (+penalty)

Heat decreases from:
- Natural decay (-3 per week)
- Staying inactive

Heat consequences:
60-80: High risk missions likely fail
80-100: GAME OVER - Arrested!
```

---

## 🏆 Victory Conditions

### **Win: Become the Don**
```
Respect >= 95
Rank = Don
```

### **Lose: Betrayal**
```
Respect <= 0
Game Over: "Lost all respect - betrayed by the family!"
```

### **Lose: Arrested**
```
Heat >= 100
Game Over: "Too much heat - arrested by the Feds!"
```

---

## 📈 Statistics Tracked

```
FINAL STATISTICS:
  Time to become Don: 47 weeks
  Missions Completed: 38/42
  Success Rate: 90.5%
  Total Money Earned: $28,450
  Final Respect: 96/100
  Achievements: 4

ACHIEVEMENTS:
  ⭐ Promoted to Soldier in week 9
  ⭐ Promoted to Capo in week 23
  ⭐ Promoted to Underboss in week 38
  ⭐ Promoted to Don in week 47
```

---

## 🎨 What Makes This Special

### **1. Rules Engine Drives Everything**
```csharp
// Not this:
if (player.money < 500 && mission.risk < 4)
    return "accept";

// But this:
Rule: "ACCEPT_DESPERATE"
  Condition: ctx => ctx.IsLowOnMoney && ctx.MissionIsSafe
  Priority: 1000
```

**Benefits:**
- ✅ Declarative logic
- ✅ Easy to add/modify rules
- ✅ Testable in isolation
- ✅ Clear priority system

### **2. Emergent Narratives**
Every playthrough tells a different story:
- **Ambitious player:** Rise fast, fall hard?
- **Cautious player:** Slow and steady wins?
- **Ruthless player:** Strategic domination?

### **3. Demonstrates All Systems**
- ✅ Mission generation
- ✅ AI decision-making
- ✅ Rules engine
- ✅ Skill progression
- ✅ Career advancement
- ✅ Resource management

### **4. Educational Value**
Shows:
- How rules engines work
- Priority-based evaluation
- Context objects
- Autonomous agents
- Emergent behavior

---

## 🔧 Technical Implementation

### **Architecture**
```
PlayerAgent (AI)
    ├── RulesEngineCore<PlayerDecisionContext>
    │   └── 8 decision rules (priority-based)
    │
    ├── MissionGenerator
    │   └── Creates missions by rank
    │
    ├── MissionEvaluator
    │   ├── RulesEngineCore<MissionContext>
    │   └── 6 evaluation rules
    │
    └── PlayerCharacter
        ├── Stats (Respect, Money, Heat)
        ├── Skills (5 skills, 0-100)
        ├── Personality (4 traits)
        └── Career tracking
```

### **Key Classes**

**PlayerAgent:**
- Makes all decisions
- Processes weeks
- Tracks progression

**Mission:**
- Type, risk, reward
- Skill requirements
- State tracking

**MissionEvaluator:**
- Rules-based success calculation
- Skill checks
- Bonus/penalty application

**PlayerCharacter:**
- Stats and skills
- Personality traits
- Career history

---

## 🎮 Future Enhancements

### **Easy Adds:**
- [ ] Save/load games
- [ ] More mission types
- [ ] Rival family interactions
- [ ] Random events (betrayals, opportunities)
- [ ] Relationship system
- [ ] Multiple ending conditions

### **Advanced:**
- [ ] Human player mode (player makes decisions)
- [ ] Multiplayer (compete with AI)
- [ ] Territory management mini-game
- [ ] Dialogue system
- [ ] Visual dashboard
- [ ] Achievement system

---

## 🎯 Why This is Awesome

### **For Learning:**
- ✅ Shows rules engine in action
- ✅ Demonstrates autonomous agents
- ✅ Priority-based decision systems
- ✅ Emergent gameplay
- ✅ Clean architecture

### **For Fun:**
- ✅ Different every time
- ✅ Watch AI strategize
- ✅ See career progression
- ✅ Multiple personalities
- ✅ Unpredictable outcomes

### **For Portfolio:**
- ✅ "I built an AI that plays a game"
- ✅ Uses expression trees
- ✅ Rules-driven architecture
- ✅ Complete game loop
- ✅ Production patterns

---

## 🚀 Try It Now!

```bash
cd AgentRouting.MafiaDemo
dotnet run
```

**Choose option 1!**

Watch an AI agent make its way from the streets to the top of the Corleone family!

**"I'm gonna make him an offer he can't refuse."** 🎩

---

## 📝 Implementation Notes

**Files Created:**
- `MissionSystem.cs` - Complete mission framework
- `PlayerAgent.cs` - Autonomous AI player
- `AutonomousPlaythrough.cs` - Playback/visualization

**Total Addition:** ~1,500 lines of production-ready code

**Rules Engine Usage:**
- 8 decision rules (player AI)
- 6 evaluation rules (mission success)
- All using expression trees
- Priority-based evaluation

**This shows the full power of the rules engine framework!**
