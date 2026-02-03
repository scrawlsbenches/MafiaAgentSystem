using AgentRouting.Core;
using AgentRouting.MafiaDemo;
using AgentRouting.MafiaDemo.Rules;
using System.Collections.Concurrent;
using System.Text;

namespace AgentRouting.MafiaDemo.Game;

/// <summary>
/// Economic game phases - determines strategic priorities
/// </summary>
public enum GamePhase
{
    /// <summary>Wealth below 50k - focus on survival and income</summary>
    Survival,
    /// <summary>Wealth 50k-150k - build up resources safely</summary>
    Accumulation,
    /// <summary>Wealth 150k-400k - expand operations</summary>
    Growth,
    /// <summary>Wealth over 400k - dominate rivals</summary>
    Dominance
}

/// <summary>
/// Game state tracking resources, territories, and family status
/// </summary>
public class GameState
{
    // ==========================================================================
    // Game Condition Thresholds (Single Source of Truth)
    // ==========================================================================

    /// <summary>Reputation at or below this value triggers defeat by betrayal</summary>
    public const int DefeatReputationThreshold = 10;

    /// <summary>Heat at or above this value triggers defeat by federal crackdown</summary>
    public const int DefeatHeatThreshold = 100;

    /// <summary>Minimum weeks to achieve victory</summary>
    public const int VictoryWeekThreshold = 52;

    /// <summary>Minimum wealth required for victory</summary>
    public const decimal VictoryWealthThreshold = 1000000m;

    /// <summary>Minimum reputation required for victory</summary>
    public const int VictoryReputationThreshold = 80;

    // ==========================================================================
    // Game State Properties
    // ==========================================================================

    public decimal FamilyWealth { get; set; } = 100000m;
    public int Reputation { get; set; } = 50; // 0-100
    public int HeatLevel { get; set; } = 0; // Police attention 0-100
    public int Week { get; set; } = 1;
    public DateTime GameStartTime { get; set; } = DateTime.UtcNow;

    public Dictionary<string, Territory> Territories { get; set; } = new();
    public Dictionary<string, decimal> AgentLoyalty { get; set; } = new(); // 0-100
    /// <summary>
    /// Event log using Queue for O(1) dequeue performance during eviction.
    /// Old events are dequeued when the log reaches capacity.
    /// </summary>
    public Queue<GameEvent> EventLog { get; set; } = new();
    public Dictionary<string, RivalFamily> RivalFamilies { get; set; } = new();

    public bool GameOver { get; set; } = false;
    public string? GameOverReason { get; set; }

    // Computed properties for rules engine compatibility
    public int Day => Week; // Alias for Week
    public int SoldierCount { get; set; } = 5;
    public decimal TotalRevenue => Territories.Values.Sum(t => t.WeeklyRevenue);
    public int TerritoryCount => Territories.Count;

    // Trend tracking for strategic decisions
    public int PreviousHeatLevel { get; set; } = 0;
    public decimal PreviousWealth { get; set; } = 100000m;

    // Computed strategic properties
    public GamePhase CurrentPhase => FamilyWealth switch
    {
        < 50000m => GamePhase.Survival,
        < 150000m => GamePhase.Accumulation,
        < 400000m => GamePhase.Growth,
        _ => GamePhase.Dominance
    };

    public bool HeatIsRising => HeatLevel > PreviousHeatLevel + 5;
    public bool HeatIsFalling => HeatLevel < PreviousHeatLevel - 5;
    public bool WealthIsGrowing => FamilyWealth > PreviousWealth * 1.05m;
    public bool WealthIsShrinking => FamilyWealth < PreviousWealth * 0.95m;

    // ==========================================================================
    // Rival Lookup Properties (with null safety)
    // ==========================================================================

    /// <summary>Returns true if there are any rival families</summary>
    public bool HasRivals => RivalFamilies.Count > 0;

    /// <summary>
    /// Find the most hostile rival family, or null if no rivals exist.
    /// Always check HasRivals or use null-conditional operator when accessing.
    /// </summary>
    public RivalFamily? MostHostileRival => RivalFamilies.Values
        .OrderByDescending(r => r.Hostility)
        .FirstOrDefault();

    /// <summary>
    /// Find the weakest rival family, or null if no rivals exist.
    /// Always check HasRivals or use null-conditional operator when accessing.
    /// </summary>
    public RivalFamily? WeakestRival => RivalFamilies.Values
        .OrderBy(r => r.Strength)
        .FirstOrDefault();

    /// <summary>Returns the hostility of the most hostile rival, or 0 if no rivals</summary>
    public int MaxRivalHostility => MostHostileRival?.Hostility ?? 0;

    /// <summary>Returns the strength of the weakest rival, or 0 if no rivals</summary>
    public int MinRivalStrength => WeakestRival?.Strength ?? 0;

    /// <summary>Returns true if any rival has hostility above the threshold</summary>
    public bool HasHostileRival(int threshold = 70) =>
        HasRivals && RivalFamilies.Values.Any(r => r.Hostility > threshold);

    /// <summary>Returns true if any rival is weak (below strength threshold)</summary>
    public bool HasWeakRival(int threshold = 40) =>
        HasRivals && RivalFamilies.Values.Any(r => r.Strength < threshold);
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
    public int GameWeek { get; set; } = 0;  // Track which game week event occurred (for proper timing checks)
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
/// Agent that receives and acknowledges game engine messages.
/// This agent handles messages routed to "game-engine" from autonomous agent actions.
/// </summary>
public class GameEngineAgent : AgentBase
{
    public GameEngineAgent(IAgentLogger logger)
        : base("game-engine", "Game Engine", logger)
    {
        Capabilities = new AgentCapabilities
        {
            SupportedCategories = new List<string>
            {
                "Collection",
                "AgentAction",
                "HeatManagement",
                "DailyOperations",
                "FinalDecision",
                "MajorDispute",
                "Legal"
            },
            MaxConcurrentMessages = 100
        };
    }

    protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
    {
        // Game engine acknowledges all routed messages
        // The actual game logic is handled separately in MafiaGameEngine.ExecuteAgentAction
        return Task.FromResult(MessageResult.Ok($"Game engine received: {message.Subject}"));
    }
}

/// <summary>
/// Main game engine - runs the autonomous simulation.
///
/// THREADING MODEL: This class is designed for single-threaded operation.
/// The game loop in StartGameAsync() and player actions via ExecutePlayerAction()
/// should not be called concurrently from multiple threads. If multi-threaded
/// access is needed in the future, synchronization (e.g., SemaphoreSlim) should
/// be added around state modifications.
///
/// The GameState is publicly exposed via the State property and is mutable.
/// External code should not modify GameState while the game loop is running.
/// </summary>
public class MafiaGameEngine
{
    /// <summary>
    /// Maximum number of events to keep in the event log.
    /// Oldest events are evicted when this limit is exceeded.
    /// </summary>
    private const int MaxEventLogSize = 1000;

    private readonly GameState _state;
    private readonly AgentRouter? _router;
    private readonly IAgentLogger _logger;
    private readonly Dictionary<string, GameAgentData> _gameAgents;
    private readonly Dictionary<string, AutonomousAgent> _autonomousAgents;
    private readonly ConcurrentQueue<string> _messageLog = new();
    private readonly GameRulesEngine? _rulesEngine;
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
        _rulesEngine = new GameRulesEngine(_state);
        _rulesEngine.SetupAsyncEventRules(); // Initialize async event processing

        // Register the game engine agent to receive routed messages
        _router.RegisterAgent(new GameEngineAgent(_logger));
    }

    public MafiaGameEngine(IAgentLogger logger)
    {
        _logger = logger;
        _router = new AgentRouterBuilder().WithLogger(logger).Build();
        _state = new GameState();
        _gameAgents = new Dictionary<string, GameAgentData>();
        _autonomousAgents = new Dictionary<string, AutonomousAgent>();
        InitializeGame();
        _rulesEngine = new GameRulesEngine(_state);
        _rulesEngine.SetupAsyncEventRules(); // Initialize async event processing

        // Register the game engine agent to receive routed messages
        _router.RegisterAgent(new GameEngineAgent(_logger));
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

        // Agent actions route to capo for execution
        _router.AddRoutingRule("AGENT_ACTIONS", "Agent action execution",
            ctx => ctx.Category == "AgentAction",
            "capo-001", priority: 500);

        // Collection reports route to underboss
        _router.AddRoutingRule("COLLECTIONS", "Collection reports",
            ctx => ctx.Category == "Collection",
            "underboss-001", priority: 600);

        // Heat-related activities route to consigliere for risk assessment
        _router.AddRoutingRule("HEAT_MANAGEMENT", "Heat management",
            ctx => ctx.Category == "HeatManagement",
            "consigliere-001", priority: 700);
    }

    /// <summary>
    /// Start the autonomous game loop
    /// </summary>
    public async Task StartGameAsync()
    {
        _running = true;
        _cts = new CancellationTokenSource();

        Console.WriteLine("\n🎮 Game started! Press Ctrl+C to stop.\n");

        try
        {
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

                    // Display performance metrics at game end
                    if (_rulesEngine != null)
                    {
                        Console.WriteLine();
                        Console.WriteLine(_rulesEngine.GetAgentRulePerformanceSummary());
                    }
                    break;
                }

                await GameTimingOptions.DelayAsync(GameTimingOptions.Current.TurnDelayMs, _cts.Token);
            }
        }
        finally
        {
            // Dispose the CancellationTokenSource to prevent resource leaks
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Stop the game
    /// </summary>
    public void StopGame()
    {
        _running = false;
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private void InitializeGame()
    {
        // Initialize territories
        // Heat balance: Total heat from territories = 11/week
        // With natural decay of 8/week, net = +3/week (manageable)
        // Bribe (-15) or laylow (-5) can control heat effectively
        _state.Territories["little-italy"] = new Territory
        {
            Name = "Little Italy",
            ControlledBy = "capo-001",
            WeeklyRevenue = 15000,
            HeatGeneration = 2,  // Reduced from 5 (protection is low-profile)
            Type = "Protection"
        };

        _state.Territories["docks"] = new Territory
        {
            Name = "Brooklyn Docks",
            ControlledBy = "capo-001",
            WeeklyRevenue = 20000,
            HeatGeneration = 5,  // Reduced from 10 (still highest - smuggling is risky)
            Type = "Smuggling"
        };

        _state.Territories["bronx"] = new Territory
        {
            Name = "Bronx Gambling",
            ControlledBy = "capo-001",
            WeeklyRevenue = 12000,
            HeatGeneration = 4,  // Reduced from 8 (gambling is semi-legal)
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

        // Track previous state for trend detection
        _state.PreviousHeatLevel = _state.HeatLevel;
        _state.PreviousWealth = _state.FamilyWealth;

        turnEvents.Add($"\n╔══════════════════════════════════════════════════════════════╗");
        turnEvents.Add($"║  📅 WEEK {_state.Week} - Corleone Family Operations          ║");
        turnEvents.Add($"║  📊 Phase: {_state.CurrentPhase,-15}                         ║");
        turnEvents.Add($"╚══════════════════════════════════════════════════════════════╝\n");

        // 1. Weekly collections
        var collectionResults = await ProcessWeeklyCollections();
        turnEvents.AddRange(collectionResults);

        // 2. Random events
        var randomEvents = ProcessRandomEvents();
        turnEvents.AddRange(randomEvents);

        // 2.5. Async events (triggered by game conditions)
        var asyncEvents = await ProcessAsyncEventsAsync();
        turnEvents.AddRange(asyncEvents);

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
            var variation = Random.Shared.Next(-20, 20);
            revenue += revenue * (variation / 100m);

            totalRevenue += revenue;

            events.Add($"  {territory.Name}: ${revenue:N0}");

            // Increase heat
            _state.HeatLevel += territory.HeatGeneration;
        }

        _state.FamilyWealth += totalRevenue;

        events.Add($"  ✓ Total: ${totalRevenue:N0}\n");

        LogEvent("Collection", $"Weekly collection: ${totalRevenue:N0}", "capo-001");

        await Task.CompletedTask; // Async-ready for future timing delays
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
        if (Random.Shared.Next(100) < 10)
        {
            var eventType = Random.Shared.Next(6);

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
                    var bonus = Random.Shared.Next(5000, 15000);
                    events.Add($"💎 OPPORTUNITY: Lucrative score!");
                    _state.FamilyWealth += bonus;
                    events.Add($"   Gained ${bonus:N0}\n");
                    LogEvent("Opportunity", $"Profitable opportunity: ${bonus:N0}", "soldier-001");
                    break;

                case 3: // Rival provocation
                    var rival = _state.RivalFamilies.Values.ElementAt(Random.Shared.Next(_state.RivalFamilies.Count));
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

    /// <summary>
    /// Process async events based on current game conditions.
    /// Uses AsyncRule engine for time-sensitive events like police investigations,
    /// informant intel gathering, and business deal negotiations.
    /// </summary>
    private async Task<List<string>> ProcessAsyncEventsAsync()
    {
        var events = new List<string>();

        if (_rulesEngine == null)
            return events;

        // Determine which async events to trigger based on game state
        var triggeredEvents = new List<string>();

        // High heat triggers police activity
        if (_state.HeatLevel > 50 && Random.Shared.Next(100) < 30)
        {
            triggeredEvents.Add("PoliceActivity");
        }

        // Wealthy family can gather intel
        if (_state.FamilyWealth > 50000 && Random.Shared.Next(100) < 20)
        {
            triggeredEvents.Add("GatherIntel");
        }

        // Good reputation enables business opportunities
        if (_state.Reputation > 40 && _state.Week % 4 == 0)
        {
            triggeredEvents.Add("BusinessOpportunity");
        }

        // Process triggered events
        foreach (var trigger in triggeredEvents)
        {
            var result = await _rulesEngine.ProcessAsyncEventAsync(trigger, delayMs: 50);

            if (result != "No matching async event handler")
            {
                events.Add($"⏳ {result}");
            }
        }

        if (events.Any())
        {
            events.Insert(0, "───────────────────────────");
            events.Insert(0, "⏰ ONGOING OPERATIONS");
            events.Add("");
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
                // Route action through AgentRouter middleware pipeline
                var routeResult = await RouteAgentActionAsync(agent, action);

                // Execute the action effects on game state
                var result = await ExecuteAgentAction(agent, action);

                if (!string.IsNullOrEmpty(result))
                {
                    // Include routing info if available
                    var routeInfo = routeResult.Success ? "" : " [route failed]";
                    events.Add($"  {result}{routeInfo}");
                }

                agent.ActionCooldown = Random.Shared.Next(1, 3); // Cooldown 1-2 turns
            }
        }

        if (events.Count == 2) // No actions taken
        {
            events.Add("  (No actions this week)");
        }

        events.Add("");
        return events;
    }

    /// <summary>
    /// Route an agent's action through the AgentRouter middleware pipeline.
    /// This enables logging, validation, rate limiting, and other cross-cutting concerns.
    /// </summary>
    private async Task<MessageResult> RouteAgentActionAsync(GameAgentData agent, string action)
    {
        if (_router == null)
        {
            return MessageResult.Ok("No router configured");
        }

        // Determine message category based on action type
        var category = action switch
        {
            "collection" => "Collection",
            "bribe" or "laylow" => "HeatManagement",
            "intimidate" or "expand" or "recruit" => "AgentAction",
            _ => "DailyOperations"
        };

        // Create message representing this agent action
        var message = new AgentMessage
        {
            SenderId = agent.AgentId,
            ReceiverId = "game-engine", // Game engine processes actions
            Subject = $"Agent Action: {action}",
            Content = $"{agent.AgentId} requests to execute action: {action}",
            Category = category,
            Priority = action == "intimidate" ? MessagePriority.High : MessagePriority.Normal,
            Metadata = new Dictionary<string, object>
            {
                ["action"] = action,
                ["agentPersonality"] = new Dictionary<string, int>
                {
                    ["aggression"] = agent.Personality.Aggression,
                    ["greed"] = agent.Personality.Greed,
                    ["loyalty"] = agent.Personality.Loyalty,
                    ["ambition"] = agent.Personality.Ambition
                },
                ["gameWeek"] = _state.Week,
                ["familyWealth"] = _state.FamilyWealth,
                ["heatLevel"] = _state.HeatLevel
            }
        };

        try
        {
            // Route through middleware pipeline (logging, validation, etc.)
            var result = await _router.RouteMessageAsync(message, CancellationToken.None);

            // Log the routing for debugging
            if (result.Success)
            {
                LogEvent("AgentRoute", $"{agent.AgentId} action '{action}' routed successfully", agent.AgentId);
            }

            return result;
        }
        catch (Exception ex)
        {
            LogEvent("RouteError", $"Route error for {agent.AgentId}: {ex.Message}", agent.AgentId);
            return MessageResult.Fail($"Routing failed: {ex.Message}");
        }
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
        var roll = Random.Shared.Next(100);

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
                var amount = Random.Shared.Next(1000, 5000);
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

            case "recruit":
                // Recruitment costs money but adds soldiers
                if (_state.FamilyWealth >= 5000)
                {
                    _state.FamilyWealth -= 5000;
                    _state.SoldierCount += 1;
                    _state.Reputation += 2;
                    LogEvent("Recruit", $"{agent.AgentId} recruited new soldier", agent.AgentId);
                    return $"{agent.AgentId} recruits a new soldier - Cost $5,000, Soldiers: {_state.SoldierCount}";
                }
                break;

            case "bribe":
                // Bribe officials to reduce heat
                if (_state.FamilyWealth >= 10000 && _state.HeatLevel > 20)
                {
                    _state.FamilyWealth -= 10000;
                    _state.HeatLevel -= 15;
                    LogEvent("Bribe", $"{agent.AgentId} bribed officials", agent.AgentId);
                    return $"{agent.AgentId} bribes officials - Cost $10,000, Heat -15";
                }
                break;

            case "laylow":
                // Laying low reduces heat slightly but no other action
                _state.HeatLevel = Math.Max(0, _state.HeatLevel - 5);
                return $"{agent.AgentId} lays low - Heat -5";
        }

        return null;
    }

    private List<string> ProcessRivalFamilyActions()
    {
        var events = new List<string>();

        foreach (var rival in _state.RivalFamilies.Values)
        {
            // High hostility = chance of attack
            if (rival.Hostility > 70 && Random.Shared.Next(100) < 30)
            {
                events.Add($"⚔️  RIVAL ACTION: {rival.Name} attacks!");

                var damage = Random.Shared.Next(5000, 15000);
                _state.FamilyWealth -= damage;
                _state.Reputation -= 5;
                rival.Hostility -= 10; // Vented some hostility

                events.Add($"   Lost ${damage:N0} and reputation");
                events.Add($"   (You may want to retaliate)\n");

                LogEvent("RivalAttack", $"{rival.Name} attacked", "system");
            }

            // Hostility slowly decreases over time (clamped to prevent negative values)
            if (rival.Hostility > 0)
            {
                rival.Hostility = Math.Max(0, rival.Hostility - Random.Shared.Next(1, 3));
            }
        }

        return events;
    }

    private void UpdateGameState()
    {
        // Heat naturally decreases (balanced: 8/week)
        // With territory heat generation of 11/week, net = +3/week
        // This makes the game winnable with occasional heat management
        if (_state.HeatLevel > 0)
        {
            _state.HeatLevel -= 8;
        }

        _state.HeatLevel = Math.Max(0, Math.Min(100, _state.HeatLevel));
        _state.Reputation = Math.Max(0, Math.Min(100, _state.Reputation));
    }

    private void CheckGameOver()
    {
        // Defeat conditions (using GameState constants for single source of truth)
        if (_state.FamilyWealth <= 0)
        {
            _state.GameOver = true;
            _state.GameOverReason = "The family went bankrupt. You've been absorbed by the Barzinis.";
        }

        if (_state.HeatLevel >= GameState.DefeatHeatThreshold)
        {
            _state.GameOver = true;
            _state.GameOverReason = "The Feds shut down the family. Everyone's going to prison.";
        }

        if (_state.Reputation <= GameState.DefeatReputationThreshold)
        {
            _state.GameOver = true;
            _state.GameOverReason = "The family lost all respect. You've been betrayed from within.";
        }

        // Victory condition (using GameState constants for single source of truth)
        if (_state.Week >= GameState.VictoryWeekThreshold &&
            _state.FamilyWealth >= GameState.VictoryWealthThreshold &&
            _state.Reputation >= GameState.VictoryReputationThreshold)
        {
            _state.GameOver = true;
            _state.GameOverReason = "Victory! You've built an empire. The Corleone family controls New York.";
        }
    }

    private void LogEvent(string type, string description, string agent)
    {
        // Evict oldest events if at capacity (O(1) dequeue operation)
        while (_state.EventLog.Count >= MaxEventLogSize)
        {
            _state.EventLog.Dequeue();
        }

        _state.EventLog.Enqueue(new GameEvent
        {
            Type = type,
            Description = description,
            InvolvedAgent = agent,
            GameWeek = _state.Week  // Track when event occurred for proper timing checks
        });
    }

    /// <summary>
    /// Player actions - player can issue commands
    /// </summary>
    public Task<string> ExecutePlayerAction(string command)
    {
        var parts = command.ToLowerInvariant().Split(' ');
        var action = parts[0];

        string result = action switch
        {
            "status" => GetStatusReport(),
            "territories" => GetTerritoryReport(),
            "rivals" => GetRivalReport(),
            "events" => GetRecentEvents(),
            "help" => GetHelpText(),
            "bribe" => ExecuteBribe(),
            "expand" => ExecuteExpand(),
            "hit" => ExecuteHit(parts),
            "peace" => ExecutePeace(parts),
            _ => $"❌ Unknown command: {action}. Type 'help' for commands."
        };

        return Task.FromResult(result);
    }

    private string ExecuteBribe()
    {
        if (_state.FamilyWealth >= 10000)
        {
            _state.FamilyWealth -= 10000;
            _state.HeatLevel -= 20;
            LogEvent("Bribe", "Player bribed officials", "player");
            return "💰 Paid $10,000 in bribes. Heat reduced by 20.";
        }
        return "❌ Not enough money (need $10,000)";
    }

    private string ExecuteExpand()
    {
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
    }

    private string ExecuteHit(string[] parts)
    {
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
    }

    private string ExecutePeace(string[] parts)
    {
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
