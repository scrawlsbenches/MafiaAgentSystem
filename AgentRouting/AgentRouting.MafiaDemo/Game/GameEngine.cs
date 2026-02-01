using AgentRouting.Core;
using AgentRouting.MafiaDemo;
using AgentRouting.MafiaDemo.Rules;
using System.Collections.Concurrent;
using System.Text;

namespace AgentRouting.MafiaDemo.Game;

/// <summary>
/// Game state tracking resources, territories, and family status
/// </summary>
public class GameState
{
    public decimal FamilyWealth { get; set; } = 100000m;
    public int Reputation { get; set; } = 50; // 0-100
    public int HeatLevel { get; set; } = 0; // Police attention 0-100
    public int Week { get; set; } = 1;
    public DateTime GameStartTime { get; set; } = DateTime.UtcNow;

    public Dictionary<string, Territory> Territories { get; set; } = new();
    public Dictionary<string, decimal> AgentLoyalty { get; set; } = new(); // 0-100
    public List<GameEvent> EventLog { get; set; } = new();
    public Dictionary<string, RivalFamily> RivalFamilies { get; set; } = new();

    public bool GameOver { get; set; } = false;
    public string? GameOverReason { get; set; }

    // Computed properties for rules engine compatibility
    public int Day => Week; // Alias for Week
    public int SoldierCount { get; set; } = 5;
    public decimal TotalRevenue => Territories.Values.Sum(t => t.WeeklyRevenue);
    public int TerritoryCount => Territories.Count;
}

/// <summary>
/// Simple data class for game engine NPC tracking (not a full Agent)
/// </summary>
public class GameAgentData
{
    public string AgentId { get; set; } = "";
    public AgentPersonality Personality { get; set; } = new();
    public int ActionCooldown { get; set; } = 0;
}

public class Territory
{
    public string Name { get; set; } = "";
    public string ControlledBy { get; set; } = ""; // Agent ID
    public decimal WeeklyRevenue { get; set; }
    public int HeatGeneration { get; set; } // How much police attention
    public string Type { get; set; } = ""; // Protection, Gambling, LoanSharking
    public bool UnderDispute { get; set; } = false;
}

public class RivalFamily
{
    public string Name { get; set; } = "";
    public int Strength { get; set; } = 50;
    public int Hostility { get; set; } = 0; // 0-100
    public bool AtWar { get; set; } = false;
}

public class GameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = ""; // Collection, Hit, War, etc.
    public string Description { get; set; } = "";
    public string InvolvedAgent { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Base class for autonomous agents that can make their own decisions
/// </summary>
public abstract class AutonomousAgent : AgentBase
{
    protected Random Random = new();

    public TimeSpan DecisionDelay { get; protected set; } = TimeSpan.FromSeconds(5);
    public int Ambition { get; protected set; } = 5; // 1-10
    public int Loyalty { get; protected set; } = 8; // 1-10
    public int Aggression { get; protected set; } = 5; // 1-10

    protected AutonomousAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
    }

    /// <summary>
    /// Agent makes a decision about what to do next
    /// </summary>
    public abstract AgentDecision? MakeDecision(GameState gameState, Random random);

    /// <summary>
    /// Handles incoming messages - provides default implementation that can be overridden.
    /// </summary>
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        // Default implementation - can be overridden by derived classes
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SoldierThinkingMs, ct);
        return MessageResult.Ok($"Message received by {Name}");
    }
}

public class AgentGoal
{
    public string GoalType { get; set; } = ""; // EarnMoney, GainPower, EliminateRival
    public int Priority { get; set; } = 0;
    public bool Completed { get; set; } = false;
}

public class AgentPersonality
{
    public int Aggression { get; set; } = 50; // 0-100, how likely to use violence
    public int Greed { get; set; } = 50; // How much they care about money
    public int Loyalty { get; set; } = 80; // How loyal to the family
    public int Ambition { get; set; } = 50; // Desire to rise in ranks
}

/// <summary>
/// Agent decisions
/// </summary>
public enum DecisionType
{
    Wait,
    SendMessage,
    CollectMoney,
    RecruitSoldier,
    ExpandTerritory,
    OrderHit,
    MakePeace,
    Negotiate
}

public class AgentDecision
{
    public DecisionType Type { get; set; }
    public AgentMessage? Message { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Main game engine - runs the autonomous simulation
/// </summary>
public class MafiaGameEngine
{
    private readonly GameState _state;
    private readonly AgentRouter? _router;
    private readonly IAgentLogger _logger;
    private readonly Dictionary<string, GameAgentData> _gameAgents;
    private readonly Dictionary<string, AutonomousAgent> _autonomousAgents;
    private readonly Random _random = new();
    private readonly ConcurrentQueue<string> _messageLog = new();
    private readonly RulesBasedGameEngine? _rulesEngine;
    private CancellationTokenSource? _cts;
    private bool _running = false;

    public GameState State => _state;

    public MafiaGameEngine(AgentRouter router)
    {
        _router = router;
        _logger = new ConsoleAgentLogger();
        _state = new GameState();
        _gameAgents = new Dictionary<string, GameAgentData>();
        _autonomousAgents = new Dictionary<string, AutonomousAgent>();
        InitializeGame();
        _rulesEngine = new RulesBasedGameEngine(_state);
    }

    public MafiaGameEngine(IAgentLogger logger)
    {
        _logger = logger;
        _router = new AgentRouter(logger);
        _state = new GameState();
        _gameAgents = new Dictionary<string, GameAgentData>();
        _autonomousAgents = new Dictionary<string, AutonomousAgent>();
        InitializeGame();
        _rulesEngine = new RulesBasedGameEngine(_state);
    }

    /// <summary>
    /// Register an autonomous agent for the simulation
    /// </summary>
    public void RegisterAutonomousAgent(AutonomousAgent agent)
    {
        _autonomousAgents[agent.Id] = agent;
        _router?.RegisterAgent(agent);
    }

    /// <summary>
    /// Setup routing rules for the family hierarchy
    /// </summary>
    public void SetupRoutingRules()
    {
        if (_router == null) return;

        // The Don handles final decisions
        _router.AddRoutingRule("GODFATHER", "Godfather decisions",
            ctx => ctx.Category == "FinalDecision" || ctx.Category == "MajorDispute",
            "godfather-001", priority: 1000);

        // Underboss handles daily operations
        _router.AddRoutingRule("UNDERBOSS", "Underboss operations",
            ctx => ctx.Category == "DailyOperations",
            "underboss-001", priority: 800);

        // Consigliere handles legal
        _router.AddRoutingRule("CONSIGLIERE", "Legal matters",
            ctx => ctx.Category == "Legal",
            "consigliere-001", priority: 900);
    }

    /// <summary>
    /// Start the autonomous game loop
    /// </summary>
    public async Task StartGameAsync()
    {
        _running = true;
        _cts = new CancellationTokenSource();

        Console.WriteLine("\n🎮 Game started! Press Ctrl+C to stop.\n");

        while (_running && !_state.GameOver && !_cts.Token.IsCancellationRequested)
        {
            var events = await ExecuteTurnAsync();
            foreach (var evt in events)
            {
                Console.WriteLine(evt);
            }

            if (_state.GameOver)
            {
                Console.WriteLine($"\n🎬 GAME OVER: {_state.GameOverReason}");
                break;
            }

            await GameTimingOptions.DelayAsync(GameTimingOptions.Current.TurnDelayMs, _cts.Token);
        }
    }

    /// <summary>
    /// Stop the game
    /// </summary>
    public void StopGame()
    {
        _running = false;
        _cts?.Cancel();
    }

    private void InitializeGame()
    {
        // Initialize territories
        _state.Territories["little-italy"] = new Territory
        {
            Name = "Little Italy",
            ControlledBy = "capo-001",
            WeeklyRevenue = 15000,
            HeatGeneration = 5,
            Type = "Protection"
        };

        _state.Territories["docks"] = new Territory
        {
            Name = "Brooklyn Docks",
            ControlledBy = "capo-001",
            WeeklyRevenue = 20000,
            HeatGeneration = 10,
            Type = "Smuggling"
        };

        _state.Territories["bronx"] = new Territory
        {
            Name = "Bronx Gambling",
            ControlledBy = "capo-001",
            WeeklyRevenue = 12000,
            HeatGeneration = 8,
            Type = "Gambling"
        };

        // Initialize rival families
        _state.RivalFamilies["tattaglia"] = new RivalFamily
        {
            Name = "Tattaglia Family",
            Strength = 60,
            Hostility = 20
        };

        _state.RivalFamilies["barzini"] = new RivalFamily
        {
            Name = "Barzini Family",
            Strength = 70,
            Hostility = 30
        };

        // Initialize game agent data for simulation
        _gameAgents["underboss-001"] = new GameAgentData
        {
            AgentId = "underboss-001",
            Personality = new AgentPersonality { Aggression = 60, Greed = 70, Loyalty = 90, Ambition = 40 }
        };

        _gameAgents["capo-001"] = new GameAgentData
        {
            AgentId = "capo-001",
            Personality = new AgentPersonality { Aggression = 80, Greed = 60, Loyalty = 70, Ambition = 70 }
        };

        _gameAgents["soldier-001"] = new GameAgentData
        {
            AgentId = "soldier-001",
            Personality = new AgentPersonality { Aggression = 90, Greed = 40, Loyalty = 95, Ambition = 30 }
        };

        LogEvent("GameStart", "The Corleone Family begins operations in New York", "godfather-001");
    }

    /// <summary>
    /// Run one game turn - all agents take autonomous actions
    /// </summary>
    public async Task<List<string>> ExecuteTurnAsync()
    {
        var turnEvents = new List<string>();

        turnEvents.Add($"\n╔══════════════════════════════════════════════════════════════╗");
        turnEvents.Add($"║  📅 WEEK {_state.Week} - Corleone Family Operations          ║");
        turnEvents.Add($"╚══════════════════════════════════════════════════════════════╝\n");

        // 1. Weekly collections
        var collectionResults = await ProcessWeeklyCollections();
        turnEvents.AddRange(collectionResults);

        // 2. Random events
        var randomEvents = ProcessRandomEvents();
        turnEvents.AddRange(randomEvents);

        // 3. Autonomous agent actions
        var agentActions = await ProcessAutonomousActions();
        turnEvents.AddRange(agentActions);

        // 4. Rival family actions
        var rivalActions = ProcessRivalFamilyActions();
        turnEvents.AddRange(rivalActions);

        // 5. Evaluate game rules (win/loss, consequences)
        if (_rulesEngine != null)
        {
            var ruleEvents = _rulesEngine.EvaluateGameRules();
            turnEvents.AddRange(ruleEvents);
        }

        // 6. Update game state
        UpdateGameState();

        // 7. Check win/loss conditions
        CheckGameOver();

        turnEvents.Add($"\n💰 Family Wealth: ${_state.FamilyWealth:N0}");
        turnEvents.Add($"⭐ Reputation: {_state.Reputation}/100");
        turnEvents.Add($"🚨 Heat Level: {_state.HeatLevel}/100");

        _state.Week++;

        return turnEvents;
    }

    private async Task<List<string>> ProcessWeeklyCollections()
    {
        var events = new List<string>();
        events.Add("💼 WEEKLY COLLECTIONS");
        events.Add("─────────────────────");

        decimal totalRevenue = 0;

        foreach (var territory in _state.Territories.Values)
        {
            var revenue = territory.WeeklyRevenue;

            // Random variation
            var variation = _random.Next(-20, 20);
            revenue += revenue * (variation / 100m);

            totalRevenue += revenue;

            events.Add($"  {territory.Name}: ${revenue:N0}");

            // Increase heat
            _state.HeatLevel += territory.HeatGeneration;
        }

        _state.FamilyWealth += totalRevenue;

        events.Add($"  ✓ Total: ${totalRevenue:N0}\n");

        LogEvent("Collection", $"Weekly collection: ${totalRevenue:N0}", "capo-001");

        return events;
    }

    private List<string> ProcessRandomEvents()
    {
        var events = new List<string>();

        // Use rules engine for events if available
        if (_rulesEngine != null)
        {
            var ruleEvents = _rulesEngine.GenerateEvents();
            events.AddRange(ruleEvents);
        }

        // Keep some random chance for variety (reduced from 20% to 10%)
        if (_random.Next(100) < 10)
        {
            var eventType = _random.Next(6);

            switch (eventType)
            {
                case 0: // Police raid
                    events.Add("🚨 POLICE RAID on the social club!");
                    _state.HeatLevel += 15;
                    _state.FamilyWealth -= 5000;
                    events.Add("   Lost $5,000 in bribes and legal fees\n");
                    LogEvent("PoliceRaid", "Police raided the social club", "system");
                    break;

                case 1: // Informant threat
                    events.Add("🐀 INFORMANT WARNING: Someone might be talking!");
                    _state.Reputation -= 5;
                    events.Add("   Family reputation damaged\n");
                    LogEvent("Informant", "Potential informant identified", "consigliere-001");
                    break;

                case 2: // Opportunity
                    var bonus = _random.Next(5000, 15000);
                    events.Add($"💎 OPPORTUNITY: Lucrative score!");
                    _state.FamilyWealth += bonus;
                    events.Add($"   Gained ${bonus:N0}\n");
                    LogEvent("Opportunity", $"Profitable opportunity: ${bonus:N0}", "soldier-001");
                    break;

                case 3: // Rival provocation
                    var rival = _state.RivalFamilies.Values.ElementAt(_random.Next(_state.RivalFamilies.Count));
                    rival.Hostility += 10;
                    events.Add($"⚔️  {rival.Name} PROVOCATION!");
                    events.Add($"   Tensions rising (Hostility: {rival.Hostility}/100)\n");
                    LogEvent("Provocation", $"{rival.Name} made a move", "underboss-001");
                    break;

                case 4: // New recruit
                    events.Add("👤 NEW RECRUIT joins the family!");
                    _state.Reputation += 3;
                    events.Add("   Family growing stronger\n");
                    LogEvent("Recruit", "New soldier inducted", "capo-001");
                    break;

                case 5: // Heat dies down
                    if (_state.HeatLevel > 10)
                    {
                        _state.HeatLevel -= 10;
                        events.Add("😌 Heat dies down - cops looking elsewhere\n");
                        LogEvent("HeatDrop", "Police attention decreased", "system");
                    }
                    break;
            }
        }

        return events;
    }

    private async Task<List<string>> ProcessAutonomousActions()
    {
        var events = new List<string>();
        events.Add("🤖 AUTONOMOUS AGENT ACTIONS");
        events.Add("───────────────────────────");

        foreach (var agent in _gameAgents.Values)
        {
            if (agent.ActionCooldown > 0)
            {
                agent.ActionCooldown--;
                continue;
            }

            // Decide what action to take based on personality
            var action = DecideAction(agent);

            if (action != null)
            {
                var result = await ExecuteAgentAction(agent, action);
                if (!string.IsNullOrEmpty(result))
                {
                    events.Add($"  {result}");
                }

                agent.ActionCooldown = _random.Next(1, 3); // Cooldown 1-2 turns
            }
        }

        if (events.Count == 2) // No actions taken
        {
            events.Add("  (No actions this week)");
        }

        events.Add("");
        return events;
    }

    private string? DecideAction(GameAgentData agent)
    {
        // Use rules engine if available
        if (_rulesEngine != null)
        {
            var action = _rulesEngine.GetAgentAction(agent);
            if (action != "wait")
            {
                return action;
            }
        }

        // Fallback to existing probability-based logic
        var roll = _random.Next(100);

        // High aggression = more likely to initiate violence
        if (roll < agent.Personality.Aggression / 2)
        {
            return "intimidate";
        }

        // High greed = focus on money
        if (roll < agent.Personality.Greed / 2 + 30)
        {
            return "collection";
        }

        // High ambition = seek to impress the Don
        if (roll < agent.Personality.Ambition / 2 + 20)
        {
            return "expand";
        }

        return null;
    }

    private async Task<string?> ExecuteAgentAction(GameAgentData agent, string action)
    {
        await Task.CompletedTask; // Make async warning go away
        switch (action)
        {
            case "intimidate":
                var target = "local businesses";
                _state.HeatLevel += 3;
                return $"{agent.AgentId} intimidates {target} - Heat +3";

            case "collection":
                var amount = _random.Next(1000, 5000);
                _state.FamilyWealth += amount;
                return $"{agent.AgentId} makes an extra collection: ${amount:N0}";

            case "expand":
                if (_state.FamilyWealth > 50000)
                {
                    _state.FamilyWealth -= 10000;
                    _state.Reputation += 5;
                    return $"{agent.AgentId} expands operations - Reputation +5";
                }
                break;
        }

        return null;
    }

    private List<string> ProcessRivalFamilyActions()
    {
        var events = new List<string>();

        foreach (var rival in _state.RivalFamilies.Values)
        {
            // High hostility = chance of attack
            if (rival.Hostility > 70 && _random.Next(100) < 30)
            {
                events.Add($"⚔️  RIVAL ACTION: {rival.Name} attacks!");

                var damage = _random.Next(5000, 15000);
                _state.FamilyWealth -= damage;
                _state.Reputation -= 5;
                rival.Hostility -= 10; // Vented some hostility

                events.Add($"   Lost ${damage:N0} and reputation");
                events.Add($"   (You may want to retaliate)\n");

                LogEvent("RivalAttack", $"{rival.Name} attacked", "system");
            }

            // Hostility slowly decreases over time
            if (rival.Hostility > 0)
            {
                rival.Hostility -= _random.Next(1, 3);
            }
        }

        return events;
    }

    private void UpdateGameState()
    {
        // Heat naturally decreases
        if (_state.HeatLevel > 0)
        {
            _state.HeatLevel -= 2;
        }

        _state.HeatLevel = Math.Max(0, Math.Min(100, _state.HeatLevel));
        _state.Reputation = Math.Max(0, Math.Min(100, _state.Reputation));
    }

    private void CheckGameOver()
    {
        if (_state.FamilyWealth <= 0)
        {
            _state.GameOver = true;
            _state.GameOverReason = "The family went bankrupt. You've been absorbed by the Barzinis.";
        }

        if (_state.HeatLevel >= 100)
        {
            _state.GameOver = true;
            _state.GameOverReason = "The Feds shut down the family. Everyone's going to prison.";
        }

        if (_state.Reputation <= 10)
        {
            _state.GameOver = true;
            _state.GameOverReason = "The family lost all respect. You've been betrayed from within.";
        }

        // Victory condition
        if (_state.Week >= 52 && _state.FamilyWealth >= 1000000 && _state.Reputation >= 80)
        {
            _state.GameOver = true;
            _state.GameOverReason = "Victory! You've built an empire. The Corleone family controls New York.";
        }
    }

    private void LogEvent(string type, string description, string agent)
    {
        _state.EventLog.Add(new GameEvent
        {
            Type = type,
            Description = description,
            InvolvedAgent = agent
        });
    }

    /// <summary>
    /// Player actions - player can issue commands
    /// </summary>
    public async Task<string> ExecutePlayerAction(string command)
    {
        var parts = command.ToLowerInvariant().Split(' ');
        var action = parts[0];

        switch (action)
        {
            case "status":
                return GetStatusReport();

            case "territories":
                return GetTerritoryReport();

            case "rivals":
                return GetRivalReport();

            case "events":
                return GetRecentEvents();

            case "bribe":
                if (_state.FamilyWealth >= 10000)
                {
                    _state.FamilyWealth -= 10000;
                    _state.HeatLevel -= 20;
                    LogEvent("Bribe", "Player bribed officials", "player");
                    return "💰 Paid $10,000 in bribes. Heat reduced by 20.";
                }
                return "❌ Not enough money (need $10,000)";

            case "expand":
                if (_state.FamilyWealth >= 50000)
                {
                    _state.FamilyWealth -= 50000;
                    _state.Territories[$"new-territory-{_state.Week}"] = new Territory
                    {
                        Name = $"New Territory (Week {_state.Week})",
                        ControlledBy = "capo-001",
                        WeeklyRevenue = 10000,
                        HeatGeneration = 5,
                        Type = "Protection"
                    };
                    LogEvent("Expand", "Player expanded into new territory", "player");
                    return "🗺️  Expanded into new territory! (+$10,000/week)";
                }
                return "❌ Not enough money (need $50,000)";

            case "hit":
                if (parts.Length < 2) return "Usage: hit <rival-family-name>";
                var rivalName = string.Join(" ", parts.Skip(1));
                var target = _state.RivalFamilies.Values.FirstOrDefault(r =>
                    r.Name.ToLowerInvariant().Contains(rivalName));

                if (target != null)
                {
                    if (_state.FamilyWealth >= 25000)
                    {
                        _state.FamilyWealth -= 25000;
                        target.Strength -= 20;
                        target.Hostility += 30;
                        _state.HeatLevel += 25;
                        _state.Reputation += 10;
                        LogEvent("Hit", $"Ordered hit on {target.Name}", "player");
                        return $"💀 Hit executed on {target.Name}. They're weakened but very angry!";
                    }
                    return "❌ Not enough money (need $25,000)";
                }
                return "❌ Rival family not found";

            case "peace":
                if (parts.Length < 2) return "Usage: peace <rival-family-name>";
                var peaceRival = string.Join(" ", parts.Skip(1));
                var peaceTarget = _state.RivalFamilies.Values.FirstOrDefault(r =>
                    r.Name.ToLowerInvariant().Contains(peaceRival));

                if (peaceTarget != null)
                {
                    if (_state.FamilyWealth >= 30000)
                    {
                        _state.FamilyWealth -= 30000;
                        peaceTarget.Hostility -= 40;
                        peaceTarget.AtWar = false;
                        LogEvent("Peace", $"Made peace with {peaceTarget.Name}", "player");
                        return $"🕊️  Peace treaty signed with {peaceTarget.Name}. Cost $30,000.";
                    }
                    return "❌ Not enough money (need $30,000)";
                }
                return "❌ Rival family not found";

            case "help":
                return GetHelpText();

            default:
                return $"❌ Unknown command: {action}. Type 'help' for commands.";
        }
    }

    private string GetStatusReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n📊 FAMILY STATUS REPORT");
        sb.AppendLine("═══════════════════════");
        sb.AppendLine($"Week: {_state.Week}");
        sb.AppendLine($"Wealth: ${_state.FamilyWealth:N0}");
        sb.AppendLine($"Reputation: {_state.Reputation}/100");
        sb.AppendLine($"Heat Level: {_state.HeatLevel}/100");
        sb.AppendLine($"Territories: {_state.Territories.Count}");
        sb.AppendLine($"Weekly Income: ${_state.Territories.Values.Sum(t => t.WeeklyRevenue):N0}");
        return sb.ToString();
    }

    private string GetTerritoryReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n🗺️  TERRITORY CONTROL");
        sb.AppendLine("═══════════════════");
        foreach (var territory in _state.Territories.Values)
        {
            sb.AppendLine($"\n{territory.Name}");
            sb.AppendLine($"  Type: {territory.Type}");
            sb.AppendLine($"  Revenue: ${territory.WeeklyRevenue:N0}/week");
            sb.AppendLine($"  Heat: {territory.HeatGeneration}");
            if (territory.UnderDispute)
                sb.AppendLine($"  ⚠️  UNDER DISPUTE!");
        }
        return sb.ToString();
    }

    private string GetRivalReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n⚔️  RIVAL FAMILIES");
        sb.AppendLine("═══════════════");
        foreach (var rival in _state.RivalFamilies.Values)
        {
            sb.AppendLine($"\n{rival.Name}");
            sb.AppendLine($"  Strength: {rival.Strength}/100");
            sb.AppendLine($"  Hostility: {rival.Hostility}/100");
            sb.AppendLine($"  At War: {(rival.AtWar ? "YES" : "No")}");
        }
        return sb.ToString();
    }

    private string GetRecentEvents()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n📜 RECENT EVENTS");
        sb.AppendLine("═══════════════");
        var recent = _state.EventLog.TakeLast(10).Reverse();
        foreach (var evt in recent)
        {
            sb.AppendLine($"  • {evt.Description}");
        }
        return sb.ToString();
    }

    private string GetHelpText()
    {
        return @"
🎮 AVAILABLE COMMANDS
═══════════════════

Game Info:
  status      - View family status
  territories - View controlled territories
  rivals      - View rival families
  events      - View recent events log

Actions:
  bribe       - Pay $10,000 to reduce heat by 20
  expand      - Pay $50,000 to acquire new territory
  hit <rival> - Pay $25,000 to hit a rival family
  peace <rival> - Pay $30,000 to make peace

  next        - Advance to next week
  help        - Show this help
  quit        - End game
";
    }
}
