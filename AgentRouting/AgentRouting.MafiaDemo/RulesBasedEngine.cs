using RulesEngine.Core;
using AgentRouting.MafiaDemo.Game;
using System.Linq.Expressions;

namespace AgentRouting.MafiaDemo.Rules;

/// <summary>
/// Game state context for rule evaluation
/// </summary>
public class GameRuleContext
{
    public GameState State { get; set; } = null!;
    public int Week { get; set; }
    public decimal Wealth { get; set; }
    public int Reputation { get; set; }
    public int Heat { get; set; }
    public int TerritoryCount { get; set; }
    public string? CurrentEvent { get; set; }
    public string? TriggeredBy { get; set; }
    
    // Helper properties for complex rules
    public bool IsWeakFinancially => Wealth < 50000;
    public bool IsStrongFinancially => Wealth > 200000;
    public bool IsRichFinancially => Wealth > 500000;
    public bool HasLowReputation => Reputation < 30;
    public bool HasHighReputation => Reputation > 70;
    public bool IsUnderHeat => Heat > 50;
    public bool IsSevereHeat => Heat > 80;
    public bool IsEarlyGame => Week < 10;
    public bool IsMidGame => Week >= 10 && Week < 30;
    public bool IsLateGame => Week >= 30;
    public bool HasFewTerritories => TerritoryCount < 3;
    public bool HasManyTerritories => TerritoryCount > 5;
    
    // Combination checks
    public bool IsVulnerable => IsWeakFinancially && (HasLowReputation || IsUnderHeat);
    public bool IsDominant => IsStrongFinancially && HasHighReputation && TerritoryCount > 4;
    public bool NeedsToLayLow => IsSevereHeat || (IsUnderHeat && HasLowReputation);
    public bool CanExpand => IsStrongFinancially && Heat < 60;
    public bool ShouldBeAggressive => HasHighReputation && IsStrongFinancially && Heat < 40;
}

/// <summary>
/// Agent decision context for rule-based AI
/// </summary>
public class AgentDecisionContext
{
    public string AgentId { get; set; } = "";
    public int Aggression { get; set; }
    public int Greed { get; set; }
    public int Loyalty { get; set; }
    public int Ambition { get; set; }
    public GameState GameState { get; set; } = null!;
    
    // Helper properties
    public bool IsAggressive => Aggression > 70;
    public bool IsGreedy => Greed > 70;
    public bool IsAmbitious => Ambition > 70;
    public bool IsLoyal => Loyalty > 80;
    public bool IsHotHeaded => Aggression > 80 && Loyalty < 60;
    public bool IsCalculating => Aggression < 40 && Ambition > 60;
    public bool IsFamilyFirst => Loyalty > 90 && Greed < 50;
    
    // Contextual
    public bool FamilyNeedsMoney => GameState.FamilyWealth < 100000;
    public bool FamilyUnderThreat => GameState.HeatLevel > 60 || 
                                     GameState.RivalFamilies.Values.Any(r => r.Hostility > 80);
    public bool CanTakeRisks => GameState.FamilyWealth > 150000 && GameState.HeatLevel < 50;
}

/// <summary>
/// Event generation context
/// </summary>
public class EventContext
{
    public int Week { get; set; }
    public int Heat { get; set; }
    public int Reputation { get; set; }
    public decimal Wealth { get; set; }
    public int TerritoryCount { get; set; }
    public bool HasRecentPoliceRaid { get; set; }
    public bool HasRecentHit { get; set; }
    public int RivalHostilityMax { get; set; }
    
    // Event likelihood helpers
    public bool PoliceAttentionHigh => Heat > 60;
    public bool WealthyTarget => Wealth > 300000;
    public bool WeakPosition => Reputation < 40;
    public bool TenseSituation => RivalHostilityMax > 70;
}

/// <summary>
/// Rules-based game engine - uses RulesEngine for all game logic
/// </summary>
public class RulesBasedGameEngine
{
    private readonly RulesEngineCore<GameRuleContext> _gameRules;
    private readonly RulesEngineCore<AgentDecisionContext> _agentRules;
    private readonly RulesEngineCore<EventContext> _eventRules;
    private readonly GameState _state;
    
    public GameState State => _state;
    
    public RulesBasedGameEngine(GameState state)
    {
        _state = state;
        _gameRules = new RulesEngineCore<GameRuleContext>();
        _agentRules = new RulesEngineCore<AgentDecisionContext>();
        _eventRules = new RulesEngineCore<EventContext>();
        
        SetupGameRules();
        SetupAgentRules();
        SetupEventRules();
    }
    
    /// <summary>
    /// Setup game-level rules (consequences, state changes)
    /// </summary>
    private void SetupGameRules()
    {
        // Victory conditions
        _gameRules.AddRule(
            "VICTORY_EMPIRE",
            "Empire Victory",
            ctx => ctx.Week >= 52 && ctx.IsRichFinancially && ctx.HasHighReputation,
            ctx => {
                ctx.State.GameOver = true;
                ctx.State.GameOverReason = "🏆 VICTORY! You've built an empire that will last generations!";
            },
            priority: 1000
        );
        
        _gameRules.AddRule(
            "VICTORY_SURVIVAL",
            "Survival Victory",
            ctx => ctx.Week >= 52 && ctx.Wealth > 150000,
            ctx => {
                ctx.State.GameOver = true;
                ctx.State.GameOverReason = "✅ Victory! The family survived and prospered!";
            },
            priority: 900
        );
        
        // Defeat conditions
        _gameRules.AddRule(
            "DEFEAT_BANKRUPTCY",
            "Bankruptcy",
            ctx => ctx.Wealth <= 0,
            ctx => {
                ctx.State.GameOver = true;
                ctx.State.GameOverReason = "💸 DEFEAT: The family went bankrupt. Absorbed by rivals.";
            },
            priority: 1000
        );
        
        _gameRules.AddRule(
            "DEFEAT_FEDERAL",
            "Federal Crackdown",
            ctx => ctx.Heat >= 100,
            ctx => {
                ctx.State.GameOver = true;
                ctx.State.GameOverReason = "🚨 DEFEAT: Federal crackdown! Everyone's going to prison!";
            },
            priority: 1000
        );
        
        _gameRules.AddRule(
            "DEFEAT_BETRAYAL",
            "Internal Betrayal",
            ctx => ctx.Reputation <= 5,
            ctx => {
                ctx.State.GameOver = true;
                ctx.State.GameOverReason = "🔪 DEFEAT: Betrayed from within. The family is destroyed.";
            },
            priority: 1000
        );
        
        // Warning conditions
        _gameRules.AddRule(
            "WARNING_HEAT",
            "High Heat Warning",
            ctx => ctx.Heat > 80 && ctx.Heat < 100,
            ctx => {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("⚠️  WARNING: Federal attention is CRITICAL! Lay low immediately!");
                Console.ResetColor();
            },
            priority: 800
        );
        
        _gameRules.AddRule(
            "WARNING_MONEY",
            "Low Funds Warning",
            ctx => ctx.Wealth < 30000 && ctx.Wealth > 0,
            ctx => {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  WARNING: Family funds are critically low!");
                Console.ResetColor();
            },
            priority: 800
        );
        
        // Automatic consequences
        _gameRules.AddRule(
            "CONSEQUENCE_VULNERABLE",
            "Vulnerable Position",
            ctx => ctx.IsVulnerable && !ctx.State.Territories.Values.Any(t => t.UnderDispute),
            ctx => {
                var territory = ctx.State.Territories.Values.First();
                territory.UnderDispute = true;
                Console.WriteLine($"⚔️  Rivals sense weakness! {territory.Name} is under dispute!");
            },
            priority: 700
        );
        
        _gameRules.AddRule(
            "CONSEQUENCE_DOMINANT",
            "Dominant Position",
            ctx => ctx.IsDominant && ctx.Reputation < 100,
            ctx => {
                ctx.State.Reputation += 5;
                Console.WriteLine("⭐ The family's dominance is recognized. Reputation +5");
            },
            priority: 700
        );
        
        // Opportunity rules
        _gameRules.AddRule(
            "OPPORTUNITY_EXPANSION",
            "Expansion Opportunity",
            ctx => ctx.CanExpand && ctx.Week % 5 == 0,
            ctx => {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("💼 OPPORTUNITY: New territory available for expansion!");
                Console.WriteLine("   Use 'expand' command to acquire (costs $50,000)");
                Console.ResetColor();
            },
            priority: 500
        );
        
        // Heat management
        _gameRules.AddRule(
            "HEAT_DECAY_PEACEFUL",
            "Heat Decay When Peaceful",
            ctx => ctx.Heat > 0 && !ctx.ShouldBeAggressive,
            ctx => {
                ctx.State.HeatLevel = Math.Max(0, ctx.State.HeatLevel - 3);
            },
            priority: 300
        );
        
        _gameRules.AddRule(
            "HEAT_INCREASE_AGGRESSIVE",
            "Heat from Aggressive Stance",
            ctx => ctx.ShouldBeAggressive && ctx.Week % 2 == 0,
            ctx => {
                ctx.State.HeatLevel = Math.Min(100, ctx.State.HeatLevel + 2);
            },
            priority: 300
        );
    }
    
    /// <summary>
    /// Setup agent decision-making rules
    /// </summary>
    private void SetupAgentRules()
    {
        // Greedy agents prioritize money
        _agentRules.AddRule(
            "GREEDY_COLLECTION",
            "Greedy Agent Collection",
            ctx => ctx.IsGreedy && ctx.FamilyNeedsMoney,
            ctx => { /* Return "collection" action */ },
            priority: 900
        );
        
        // Aggressive agents attack when family is threatened
        _agentRules.AddRule(
            "AGGRESSIVE_RETALIATE",
            "Aggressive Retaliation",
            ctx => ctx.IsAggressive && ctx.FamilyUnderThreat,
            ctx => { /* Return "intimidate" action */ },
            priority: 850
        );
        
        // Hot-headed agents act impulsively
        _agentRules.AddRule(
            "HOTHEADED_VIOLENCE",
            "Hot-headed Violence",
            ctx => ctx.IsHotHeaded && ctx.CanTakeRisks,
            ctx => { /* Return "hit" action */ },
            priority: 800
        );
        
        // Ambitious agents seek to impress
        _agentRules.AddRule(
            "AMBITIOUS_EXPAND",
            "Ambitious Expansion",
            ctx => ctx.IsAmbitious && ctx.GameState.FamilyWealth > 100000,
            ctx => { /* Return "expand" action */ },
            priority: 750
        );
        
        // Calculating agents make strategic moves
        _agentRules.AddRule(
            "CALCULATING_STRATEGY",
            "Strategic Planning",
            ctx => ctx.IsCalculating && ctx.GameState.Week % 3 == 0,
            ctx => { /* Return "report" action */ },
            priority: 700
        );
        
        // Loyal agents protect family
        _agentRules.AddRule(
            "LOYAL_PROTECT",
            "Family Protection",
            ctx => ctx.IsFamilyFirst && ctx.GameState.HeatLevel > 60,
            ctx => { /* Return "layfow" action */ },
            priority: 950
        );
        
        // Default: cautious wait
        _agentRules.AddRule(
            "DEFAULT_WAIT",
            "Cautious Waiting",
            ctx => true,
            ctx => { /* Return "wait" action */ },
            priority: 1
        );
    }
    
    /// <summary>
    /// Setup event generation rules
    /// </summary>
    private void SetupEventRules()
    {
        // Police raids more likely with high heat
        _eventRules.AddRule(
            "EVENT_POLICE_RAID",
            "Police Raid Event",
            ctx => ctx.PoliceAttentionHigh && !ctx.HasRecentPoliceRaid,
            ctx => { /* Generate police raid */ },
            priority: 900
        );
        
        // Informant threats when reputation is low
        _eventRules.AddRule(
            "EVENT_INFORMANT",
            "Informant Threat",
            ctx => ctx.WeakPosition && ctx.Week > 10,
            ctx => { /* Generate informant event */ },
            priority: 850
        );
        
        // Rival attacks when tense
        _eventRules.AddRule(
            "EVENT_RIVAL_ATTACK",
            "Rival Family Attack",
            ctx => ctx.TenseSituation && !ctx.HasRecentHit,
            ctx => { /* Generate rival attack */ },
            priority: 800
        );
        
        // Opportunities for wealthy families
        _eventRules.AddRule(
            "EVENT_OPPORTUNITY",
            "Business Opportunity",
            ctx => ctx.WealthyTarget && ctx.Week % 4 == 0,
            ctx => { /* Generate opportunity */ },
            priority: 700
        );
        
        // Betrayal in weak position
        _eventRules.AddRule(
            "EVENT_BETRAYAL",
            "Internal Betrayal",
            ctx => ctx.WeakPosition && ctx.Heat > 70,
            ctx => { /* Generate betrayal */ },
            priority: 750
        );
    }
    
    /// <summary>
    /// Evaluate all game rules for current state
    /// </summary>
    public List<string> EvaluateGameRules()
    {
        var events = new List<string>();
        
        var context = new GameRuleContext
        {
            State = _state,
            Week = _state.Week,
            Wealth = _state.FamilyWealth,
            Reputation = _state.Reputation,
            Heat = _state.HeatLevel,
            TerritoryCount = _state.Territories.Count
        };
        
        var matchedRules = _gameRules.EvaluateAll(context);
        
        foreach (var rule in matchedRules)
        {
            events.Add($"[Rule: {rule.Name}]");
        }
        
        return events;
    }
    
    /// <summary>
    /// Get agent action using rules
    /// </summary>
    public string GetAgentAction(AutonomousAgent agent)
    {
        var context = new AgentDecisionContext
        {
            AgentId = agent.AgentId,
            Aggression = agent.Personality.Aggression,
            Greed = agent.Personality.Greed,
            Loyalty = agent.Personality.Loyalty,
            Ambition = agent.Personality.Ambition,
            GameState = _state
        };
        
        // Evaluate rules and get highest priority matching rule
        var matchedRules = _agentRules.EvaluateAll(context);
        
        if (matchedRules.Any())
        {
            var topRule = matchedRules.First();
            
            // Map rule names to actions
            if (topRule.Name.Contains("COLLECTION")) return "collection";
            if (topRule.Name.Contains("RETALIATE") || topRule.Name.Contains("VIOLENCE")) return "intimidate";
            if (topRule.Name.Contains("EXPAND")) return "expand";
            if (topRule.Name.Contains("PROTECT")) return "laylow";
        }
        
        return "wait";
    }
    
    /// <summary>
    /// Generate events using rules
    /// </summary>
    public List<string> GenerateEvents()
    {
        var events = new List<string>();
        
        var recentPoliceRaid = _state.EventLog
            .Where(e => e.Type == "PoliceRaid")
            .Any(e => e.Timestamp > DateTime.UtcNow.AddMinutes(-5));
        
        var recentHit = _state.EventLog
            .Where(e => e.Type == "Hit")
            .Any(e => e.Timestamp > DateTime.UtcNow.AddMinutes(-5));
        
        var context = new EventContext
        {
            Week = _state.Week,
            Heat = _state.HeatLevel,
            Reputation = _state.Reputation,
            Wealth = _state.FamilyWealth,
            TerritoryCount = _state.Territories.Count,
            HasRecentPoliceRaid = recentPoliceRaid,
            HasRecentHit = recentHit,
            RivalHostilityMax = _state.RivalFamilies.Values.Any() 
                ? _state.RivalFamilies.Values.Max(r => r.Hostility) 
                : 0
        };
        
        var matchedRules = _eventRules.EvaluateAll(context);
        
        foreach (var rule in matchedRules.Take(2)) // Max 2 events per turn
        {
            events.Add($"Event triggered by rule: {rule.Name}");
        }
        
        return events;
    }
}
