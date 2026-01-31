# 🎮 The Corleone Family - Autonomous Mafia Simulation

**"I'm gonna make him an offer he can't refuse."**

A self-playing text-based mafia game where AI agents run the family business autonomously!

## 🎯 Two Modes

### Mode 1: 🎮 AUTONOMOUS GAME (The Fun One!)
**A self-playing simulation where agents make their own decisions!**

Agents pursue goals, react to events, and create emergent narratives. You just watch the drama unfold!

**Features:**
- ✨ Agents act independently based on personality
- 🎲 Random events (police raids, betrayals, opportunities)
- 📊 Resource management (money, territory, soldiers)
- 🎭 Emergent storytelling
- ⏱️ Real-time asynchronous gameplay
- 💰 Economic simulation
- 🔥 Unpredictable outcomes

### Mode 2: 🎬 SCRIPTED DEMO
Classic eight-scenario demonstration of hierarchical routing

---

## 🎮 How the Autonomous Game Works

### Agents Have Personalities

Each agent has traits that drive decisions:

| Agent | Ambition | Loyalty | Aggression | Decision Frequency |
|-------|----------|---------|------------|-------------------|
| **Godfather** | 8 | 10 | 3 | Every 15 seconds |
| **Underboss** | 7 | 9 | 7 | Every 8 seconds |
| **Consigliere** | 4 | 10 | 2 | Every 12 seconds |
| **Capo** | 8 | 7 | 8 | Every 6 seconds |
| **Soldier** | 5 | 9 | 9 | Every 4 seconds |

### What Agents Do Autonomously

**Godfather:**
- Sends strategic directives
- Responds to major threats
- Makes final decisions
- Grants favors
- Declares war or peace

**Underboss:**
- Collects protection money
- Sends orders to Capos
- Reports to the Don
- Manages daily operations
- Coordinates enforcement

**Consigliere:**
- Provides strategic advice
- Handles legal matters
- Advises on long-term planning
- Suggests political connections

**Capos:**
- Collect money from territory
- Recruit new soldiers
- Report to Underboss
- Manage their crew
- Expand operations

**Soldiers:**
- Make street collections
- Report to Capo
- Carry out enforcement
- Watch for threats

### Random Events

The game generates events that agents must react to:

**🚨 Police Raids**
```
[Day 5] 🚨 POLICE RAID at the social club!
[Day 5] Don Vito → Tom Hagen: "This is why we have lawyers. Pay whoever needs to be paid."
```

**⚔️ Rival Families**
```
[Day 7] ⚔️ The Tattaglia family is moving into our territory
[Day 7] Don Vito: "Send Tom to negotiate. If they want peace, we give it. For now."
```

**🔪 Betrayals**
```
[Day 12] 🔪 BETRAYAL: Paulie might be talking to the Feds!
[Day 12] Don Vito: "Betrayal cannot be tolerated. Watch him. If it's true... handle it quietly."
```

**💼 Business Opportunities**
```
[Day 3] 💼 Business opportunity: casino operation ($50,000)
[Day 3] Don Vito: "A good businessman knows when to invest. Do it."
```

**🙏 Favor Requests**
```
[Day 9] 🙏 local shopkeeper needs protection from thugs
[Day 9] Don Vito: "Of course. We take care of our friends. Someday they will owe us."
```

---

## 🎯 Game Mechanics

### Resource Management

**💰 Treasury**
- Starts at $50,000
- Increases from collections
- Decreases from investments/bribes
- Game over if it hits $0

**🗺️ Territory**
- Start with 3 territories
- Can expand (costs $10,000)
- More territory = more revenue

**👊 Soldiers**
- Start with 10 soldiers
- Capos recruit more
- More soldiers = better collections

### Daily Cycle

Every 30 seconds is one "day" in the game:

```
Day 1: Collections, decisions, events
       End of Day Report
Day 2: More collections, reactions to events
       End of Day Report
...
Day 30 or Bankruptcy: GAME OVER
```

### End of Day Report

```
═══════════════════════════════════════════════════════
📅 END OF DAY 5 REPORT
═══════════════════════════════════════════════════════
💰 Treasury: $78,500
🗺️  Territories: 4
👊 Soldiers: 12
📊 Total Revenue: $103,250
═══════════════════════════════════════════════════════
```

---

## 🎬 Sample Gameplay Session

```
╔══════════════════════════════════════════════════════╗
║    THE CORLEONE FAMILY - AUTONOMOUS SIMULATION      ║
╚══════════════════════════════════════════════════════╝

⏸️  Press Ctrl+C to stop the simulation

[Day 1] 💰 Luca Brasi collected $8,500
[Day 1] 💼 Sonny Corleone collected $12,300
[Day 1] Peter Clemenza → Sonny Corleone: "Make sure collections are on schedule."
[Day 1] Don Vito → Tom Hagen: "What's your read on the other families?"

[Day 2] 🚨 POLICE RAID at the gambling den!
[Day 2] Tom Hagen: "I'll talk to our lawyers and judges. This will go away, but it'll cost us."
[Day 2] 💰 Luca Brasi collected $6,800
[Day 2] 👊 Sonny Corleone recruited a new soldier (Total: 11)

[Day 3] 💼 Business opportunity: union contract ($30,000)
[Day 3] Don Vito: "A good businessman knows when to invest. Do it."
[Day 3] Treasury: $47,600 (after investment)

[Day 4] ⚔️ The Barzini family is undercutting our prices
[Day 4] Don Vito: "They think we're weak. Show them otherwise. But quietly."
[Day 4] Peter Clemenza → Luca Brasi: "Handle this. Send a message."

[Day 5] 🙏 neighborhood baker needs help with the bank
[Day 5] Don Vito: "Of course. We take care of our friends. Someday they will owe us."
[Day 5] 💰 Sonny Corleone collected $11,500

═══════════════════════════════════════════════════════
📅 END OF DAY 5 REPORT
═══════════════════════════════════════════════════════
💰 Treasury: $82,400
🗺️  Territories: 3
👊 Soldiers: 11
📊 Total Revenue: $95,780
═══════════════════════════════════════════════════════

[Day 6] Tom Hagen → Don Vito: "Our legal exposure is growing. I recommend we clean up some operations."
[Day 6] 🔪 BETRAYAL: Tessio might be talking to the Feds!
[Day 6] Don Vito: "Betrayal cannot be tolerated. Watch him. If it's true... handle it."

...continues for 30 days or until bankruptcy...
```

---

## 🎮 Running the Game

```bash
cd AgentRouting.MafiaDemo
dotnet run
```

**Choose:**
1. **Autonomous Game** - Watch it run itself!
2. **Scripted Demo** - Classic eight scenarios

---

## 🎲 What Makes It Fun

### 1. **Emergent Storytelling**
No two playthroughs are the same! Random events combine with agent personalities to create unique narratives.

### 2. **Agent Interactions**
Watch agents communicate:
- Don gives orders
- Underboss coordinates
- Capos manage crews
- Soldiers execute
- Consigliere advises

### 3. **Personality-Driven Decisions**
- **Aggressive Capo** recruits more, expands faster
- **Cautious Don** avoids risky opportunities
- **Loyal Consigliere** always gives conservative advice

### 4. **Tension and Drama**
- Will you survive police raids?
- Can you handle rival families?
- Will someone betray you?
- Can you stay profitable?

### 5. **Asynchronous Action**
Multiple agents act simultaneously:
```
[Day 8] 💰 Luca (Soldier) collecting...
[Day 8] 💼 Sonny (Capo) recruiting...
[Day 8] 🤵 Peter (Underboss) reporting...
[Day 8] 🚨 POLICE RAID!
[Day 8] 👑 Don Vito responding...
```

---

## 🎯 Technical Innovation

### What This Demonstrates

✅ **Autonomous Agent Behavior**
- Agents make decisions without user input
- Personality traits drive choices
- Goal-oriented behavior

✅ **Asynchronous Multi-Agent Systems**
- Agents run on independent timers
- Concurrent decision-making
- Message passing between agents

✅ **Emergent Gameplay**
- No scripted story
- Outcomes depend on agent choices + random events
- Unique every time

✅ **Event-Driven Architecture**
- Random event generation
- Agent reaction to events
- Cascading effects

✅ **State Management**
- Game state (treasury, territory, soldiers)
- Agent state (decisions, goals)
- Relationship tracking

✅ **Real-Time Simulation**
- Time-based progression
- Resource accumulation
- Dynamic difficulty

---

## 🎓 Real-World Applications

This pattern applies to:

**1. Game AI**
- NPCs with autonomous behavior
- Emergent narratives
- Procedural story generation

**2. Business Simulation**
- Market simulations
- Economic modeling
- Agent-based economics

**3. Social Simulation**
- Society modeling
- Organizational dynamics
- Network effects

**4. Multi-Agent Systems**
- Distributed AI
- Swarm intelligence
- Autonomous vehicles

**5. Strategy Games**
- Civilization-style games
- Dynasty simulators
- Management sims

---

## 🎮 Gameplay Tips

### Watching the Simulation

**Look for patterns:**
- Is the Don making good decisions?
- Are Capos collecting enough?
- How does the family respond to crises?

**Track resources:**
- Is treasury growing or shrinking?
- Are you expanding territory?
- Growing or losing soldiers?

**Enjoy the drama:**
- Betrayals!
- Police raids!
- Rival families!
- Business deals!

### Let It Run

The longer it runs, the more interesting it gets:
- Day 1-5: Setup phase
- Day 6-15: Growth and challenges
- Day 16-30: Crisis management
- Day 30+: Endgame

---

## 🏆 Win/Loss Conditions

**💸 LOSS: Bankruptcy**
```
🎬 GAME OVER
💸 The family has gone bankrupt.
The other families have taken over your territory.
```

**✅ WIN: Survival**
```
🎬 GAME OVER
✅ Survived 30 days!
💰 Final Treasury: $127,500
🗺️  Final Territories: 6
👊 Final Soldiers: 18
```

---

## 🎨 Agent Personality Traits Explained

### Ambition (1-10)
How much the agent wants to grow/expand
- **Low:** Conservative, slow growth
- **High:** Aggressive expansion, takes risks

### Loyalty (1-10)
How much the agent follows the Don's wishes
- **Low:** May go rogue, betray
- **High:** Always loyal, never betrays

### Aggression (1-10)
How the agent responds to threats
- **Low:** Diplomatic, avoids conflict
- **High:** Violent responses, quick to fight

---

## 🚀 Future Enhancements

Ideas for expansion:
- 🎭 More agent types (Enforcer, Accountant, Politician)
- 🏙️ Multiple families competing
- 💰 More complex economy
- 🎲 More random events
- 📊 Better analytics/visualization
- 🗳️ Player can be an agent
- 🎯 Missions and objectives
- 🏆 Achievements

---

## 🎬 Famous Quotes You'll See

- "I'm gonna make him an offer he can't refuse"
- "It's not personal. It's strictly business"
- "Leave the gun. Take the cannoli"
- "Keep your friends close, but your enemies closer"
- "A man who doesn't spend time with his family..."
- "I believe in America"

---

## 🎯 What You'll Learn

### Game Design
- Autonomous agent systems
- Emergent narratives
- Resource management
- Event-driven gameplay

### Software Architecture
- Multi-agent coordination
- Asynchronous processing
- State management
- Decision-making AI

### AI Patterns
- Goal-oriented behavior
- Personality traits
- Reactive agents
- Proactive agents

---

**"Now you come to me and you say, 'Don Corleone, give me an autonomous mafia simulation.'"**

**Run it and watch The Family run itself! 🎩🎮**

Press Ctrl+C to stop. Or let it run until the family goes broke or survives 30 days!

*"Just when I thought I was out... they pull me back in!"*
