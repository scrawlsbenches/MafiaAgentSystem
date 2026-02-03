using RulesEngine.Core;
using AgentRouting.MafiaDemo.Game;
using System.Linq.Expressions;

namespace AgentRouting.MafiaDemo.Rules;

// =============================================================================
// CONTEXT CLASSES - Game State Contexts for Rule Evaluation
// =============================================================================

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

    /// <summary>
    /// The recommended action set by rule evaluation.
    /// Valid values: "collection", "intimidate", "expand", "laylow", "recruit", "bribe", "wait"
    /// </summary>
    public string RecommendedAction { get; set; } = "wait";

    // =========================================================================
    // PERSONALITY TRAITS
    // =========================================================================
    public bool IsAggressive => Aggression > 70;
    public bool IsGreedy => Greed > 70;
    public bool IsAmbitious => Ambition > 70;
    public bool IsLoyal => Loyalty > 80;
    public bool IsHotHeaded => Aggression > 80 && Loyalty < 60;
    public bool IsCalculating => Aggression < 40 && Ambition > 60;
    public bool IsFamilyFirst => Loyalty > 90 && Greed < 50;
    public bool IsCautious => Aggression < 30 && Loyalty > 70;

    // =========================================================================
    // BASIC CONTEXTUAL PROPERTIES
    // =========================================================================
    public bool FamilyNeedsMoney => GameState.FamilyWealth < 100000;
    public bool FamilyUnderThreat => GameState.HeatLevel > 60 ||
                                     GameState.RivalFamilies.Values.Any(r => r.Hostility > 80);
    public bool CanTakeRisks => GameState.FamilyWealth > 150000 && GameState.HeatLevel < 50;
    public bool NeedsMoreSoldiers => GameState.SoldierCount < 15;
    public bool CanAffordBribe => GameState.FamilyWealth > 50000 && GameState.HeatLevel > 40;

    // =========================================================================
    // STRATEGIC PROPERTIES - Phase-based decision making
    // =========================================================================

    /// <summary>Current economic phase of the game</summary>
    public GamePhase Phase => GameState.CurrentPhase;

    /// <summary>In survival mode - prioritize income and safety</summary>
    public bool InSurvivalMode => Phase == GamePhase.Survival;

    /// <summary>In accumulation mode - build resources carefully</summary>
    public bool InAccumulationMode => Phase == GamePhase.Accumulation;

    /// <summary>In growth mode - can afford to expand</summary>
    public bool InGrowthMode => Phase == GamePhase.Growth;

    /// <summary>In dominance mode - can dominate rivals</summary>
    public bool InDominanceMode => Phase == GamePhase.Dominance;

    // =========================================================================
    // HEAT MANAGEMENT STRATEGIES
    // =========================================================================

    /// <summary>Heat is at dangerous levels (above 70)</summary>
    public bool HeatIsDangerous => GameState.HeatLevel > 70;

    /// <summary>Heat is critical (above 85) - need emergency measures</summary>
    public bool HeatIsCritical => GameState.HeatLevel > 85;

    /// <summary>Heat has been rising recently</summary>
    public bool HeatIsRising => GameState.HeatIsRising;

    /// <summary>Heat has been falling recently</summary>
    public bool HeatIsFalling => GameState.HeatIsFalling;

    /// <summary>Safe to take heat-generating actions</summary>
    public bool HasHeatBudget => GameState.HeatLevel < 40;

    /// <summary>Must take action to reduce heat</summary>
    public bool NeedsHeatReduction => GameState.HeatLevel > 50 && GameState.HeatIsRising;

    // =========================================================================
    // RIVAL ASSESSMENT
    // =========================================================================

    /// <summary>There's a weak rival that can be attacked</summary>
    public bool RivalIsWeak => GameState.WeakestRival?.Strength < 40;

    /// <summary>A rival is threatening (high hostility and strong)</summary>
    public bool RivalIsThreatening => GameState.MostHostileRival != null &&
                                      GameState.MostHostileRival.Hostility > 70 &&
                                      GameState.MostHostileRival.Strength > 60;

    /// <summary>A rival is about to attack (very high hostility)</summary>
    public bool RivalAttackImminent => GameState.MostHostileRival?.Hostility > 85;

    /// <summary>All rivals are relatively peaceful</summary>
    public bool RivalsArePeaceful => !GameState.RivalFamilies.Values.Any(r => r.Hostility > 50);

    // =========================================================================
    // ECONOMIC STRATEGIES
    // =========================================================================

    /// <summary>Should conserve resources (shrinking wealth or low funds)</summary>
    public bool ShouldConserve => GameState.WealthIsShrinking || InSurvivalMode;

    /// <summary>Can afford expensive operations</summary>
    public bool CanAffordExpensive => GameState.FamilyWealth > 200000 && !ShouldConserve;

    /// <summary>Good conditions for expansion (growing wealth, low heat)</summary>
    public bool GoodTimeToExpand => GameState.WealthIsGrowing && HasHeatBudget && !RivalIsThreatening;

    /// <summary>Expected value: Is it worth taking an action that generates heat?</summary>
    public bool HeatRiskWorthIt => CanTakeRisks && (InGrowthMode || InDominanceMode);

    // =========================================================================
    // STRATEGIC COMBINATIONS
    // =========================================================================

    /// <summary>Perfect storm: strong position, weak rival, capacity to strike</summary>
    public bool OpportunisticStrike => InDominanceMode && RivalIsWeak && HasHeatBudget;

    /// <summary>Emergency: must reduce heat immediately or face game over</summary>
    public bool EmergencyLayLow => HeatIsCritical || (HeatIsDangerous && HeatIsRising);

    /// <summary>Defensive posture: under threat, need to protect family</summary>
    public bool DefensivePosture => (RivalIsThreatening || HeatIsDangerous) && !InDominanceMode;

    /// <summary>Aggressive expansion opportunity</summary>
    public bool AggressiveOpportunity => GoodTimeToExpand && IsAmbitious && InGrowthMode;
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
/// Territory valuation context - complex economic rules
/// </summary>
public class TerritoryValueContext
{
    public Territory Territory { get; set; } = null!;
    public GameState GameState { get; set; } = null!;

    // Territory properties
    public decimal BaseRevenue => Territory.WeeklyRevenue;
    public int Heat => Territory.HeatGeneration;
    public string Type => Territory.Type;
    public bool Disputed => Territory.UnderDispute;

    // Market conditions
    public int FamilyReputation => GameState.Reputation;
    public int PoliceHeat => GameState.HeatLevel;
    public decimal FamilyWealth => GameState.FamilyWealth;
    public int TotalTerritories => GameState.Territories.Count;

    // Calculated properties for rules
    public bool IsHighValue => BaseRevenue > 15000;
    public bool IsLowRisk => Heat < 5;
    public bool IsHighRisk => Heat > 10;
    public bool IsProtectionRacket => Type == "Protection";
    public bool IsGambling => Type == "Gambling";
    public bool IsSmuggling => Type == "Smuggling";
    public bool MarketIsSaturated => TotalTerritories > 8;
    public bool HighDemand => FamilyReputation > 70 && !MarketIsSaturated;
    public bool PoliceWatching => PoliceHeat > 60;

    // Combination conditions
    public bool PrimeTerritory => IsHighValue && IsLowRisk && !Disputed;
    public bool RiskyButProfitable => IsHighValue && IsHighRisk;
    public bool NeedsToBeCleaned => IsHighRisk && PoliceWatching;
}

/// <summary>
/// Dynamic difficulty context - game adjusts based on player performance
/// </summary>
public class DifficultyContext
{
    public GameState State { get; set; } = null!;
    public int Week { get; set; }
    public decimal AverageWeeklyIncome { get; set; }
    public int WinStreak { get; set; } // Consecutive successful weeks
    public int LossStreak { get; set; } // Consecutive bad weeks

    // Performance metrics
    public bool PlayerDominating => State.FamilyWealth > 500000 && State.Reputation > 80;
    public bool PlayerStruggling => State.FamilyWealth < 50000 || State.Reputation < 30;
    public bool SteadyGrowth => AverageWeeklyIncome > 40000;
    public bool Declining => AverageWeeklyIncome < 20000;

    // Time-based
    public bool EarlyGame => Week < 10;
    public bool MidGame => Week >= 10 && Week < 30;
    public bool LateGame => Week >= 30;

    // Streak tracking
    public bool OnWinStreak => WinStreak >= 3;
    public bool OnLossStreak => LossStreak >= 3;
}

/// <summary>
/// Strategic AI context - rivals adapt their strategy
/// </summary>
public class RivalStrategyContext
{
    public RivalFamily Rival { get; set; } = null!;
    public GameState GameState { get; set; } = null!;

    // Rival state
    public int RivalStrength => Rival.Strength;
    public int RivalHostility => Rival.Hostility;
    public bool AtWar => Rival.AtWar;

    // Player state
    public decimal PlayerWealth => GameState.FamilyWealth;
    public int PlayerReputation => GameState.Reputation;
    public int PlayerTerritories => GameState.Territories.Count;
    public int PlayerHeat => GameState.HeatLevel;

    // Strategic assessment
    public bool RivalIsStronger => RivalStrength > 70;
    public bool RivalIsWeaker => RivalStrength < 40;
    public bool RivalIsAngry => RivalHostility > 70;
    public bool RivalIsNeutral => RivalHostility < 30;

    public bool PlayerIsWeak => PlayerWealth < 100000 || PlayerReputation < 40;
    public bool PlayerIsStrong => PlayerWealth > 300000 && PlayerReputation > 70;
    public bool PlayerIsDistracted => PlayerHeat > 70;

    // Strategic opportunities
    public bool ShouldAttack => RivalIsStronger && PlayerIsWeak && !PlayerIsDistracted;
    public bool ShouldMakePeace => RivalIsWeaker && PlayerIsStrong;
    public bool ShouldWait => RivalIsNeutral || (PlayerIsDistracted && !RivalIsAngry);
    public bool ShouldFormAlliance => PlayerIsStrong && RivalIsStronger;
}

/// <summary>
/// Chain reaction context - events trigger other events
/// </summary>
public class ChainReactionContext
{
    public string TriggeringEvent { get; set; } = "";
    public GameState State { get; set; } = null!;
    public Dictionary<string, object> EventData { get; set; } = new();

    // Event type checks
    public bool WasPoliceRaid => TriggeringEvent == "PoliceRaid";
    public bool WasHit => TriggeringEvent == "Hit";
    public bool WasBetrayal => TriggeringEvent == "Betrayal";
    public bool WasTerritoryLoss => TriggeringEvent == "TerritoryLost";

    // Cascade potential
    public bool HighTension => State.RivalFamilies.Values.Any(r => r.Hostility > 80);
    public bool Unstable => State.Reputation < 40 && State.HeatLevel > 60;
    public bool CrisisMode => State.FamilyWealth < 30000 || State.HeatLevel > 85;
}

// =============================================================================
// UNIFIED GAME RULES ENGINE
// =============================================================================

/// <summary>
/// Unified rules engine for all game logic - combines basic game rules,
/// agent decisions, events, territory valuation, difficulty, rival AI, and chain reactions.
/// </summary>
public class GameRulesEngine
{
    // Core game rule engines
    private readonly RulesEngineCore<GameRuleContext> _gameRules;
    private readonly RulesEngineCore<AgentDecisionContext> _agentRules;
    private readonly RulesEngineCore<EventContext> _eventRules;

    // Advanced rule engines
    private readonly RulesEngineCore<TerritoryValueContext> _valuationEngine;
    private readonly RulesEngineCore<DifficultyContext> _difficultyEngine;
    private readonly RulesEngineCore<RivalStrategyContext> _strategyEngine;
    private readonly RulesEngineCore<ChainReactionContext> _chainEngine;

    private readonly GameState _state;

    public GameState State => _state;

    public GameRulesEngine(GameState state)
    {
        _state = state;

        // Initialize core engines
        _gameRules = new RulesEngineCore<GameRuleContext>();

        // Agent rules use StopOnFirstMatch since only one action matters
        // TrackPerformance enables metrics collection for debugging
        _agentRules = new RulesEngineCore<AgentDecisionContext>(new RulesEngineOptions
        {
            StopOnFirstMatch = true,
            TrackPerformance = true
        });

        _eventRules = new RulesEngineCore<EventContext>();

        // Initialize advanced engines
        _valuationEngine = new RulesEngineCore<TerritoryValueContext>();
        _difficultyEngine = new RulesEngineCore<DifficultyContext>();
        _strategyEngine = new RulesEngineCore<RivalStrategyContext>();
        _chainEngine = new RulesEngineCore<ChainReactionContext>();

        // Setup all rules
        SetupGameRules();
        SetupAgentRules();
        SetupEventRules();
        SetupValuationRules();
        SetupDifficultyRules();
        SetupStrategyRules();
        SetupChainReactionRules();
    }

    // =========================================================================
    // GAME RULES - Victory, defeat, warnings, consequences
    // =========================================================================

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

    // =========================================================================
    // AGENT RULES - AI decision-making with strategic awareness
    // =========================================================================

    private void SetupAgentRules()
    {
        // =====================================================================
        // EMERGENCY RULES - Highest priority, override everything
        // =====================================================================

        // CRITICAL: Emergency lay low when heat is about to cause game over
        _agentRules.AddRule(
            "EMERGENCY_LAYLOW",
            "Emergency Heat Reduction",
            ctx => ctx.EmergencyLayLow,
            ctx => { ctx.RecommendedAction = "laylow"; },
            priority: 1000
        );

        // CRITICAL: Emergency bribe when we can afford it and heat is critical
        _agentRules.AddRule(
            "EMERGENCY_BRIBE",
            "Emergency Bribery",
            ctx => ctx.HeatIsCritical && ctx.GameState.FamilyWealth > 15000,
            ctx => { ctx.RecommendedAction = "bribe"; },
            priority: 995
        );

        // =====================================================================
        // PHASE-BASED STRATEGIC RULES - Adapt to economic situation
        // =====================================================================

        // Survival mode: Focus on safe income generation
        _agentRules.AddRule(
            "SURVIVAL_COLLECTION",
            "Survival Mode Collection",
            ctx => ctx.InSurvivalMode && !ctx.HeatIsDangerous,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 980
        );

        // Survival mode: Lay low if heat is building
        _agentRules.AddRule(
            "SURVIVAL_LAYLOW",
            "Survival Mode Safety",
            ctx => ctx.InSurvivalMode && ctx.NeedsHeatReduction,
            ctx => { ctx.RecommendedAction = "laylow"; },
            priority: 985
        );

        // Dominance mode: Opportunistic strikes against weak rivals
        _agentRules.AddRule(
            "DOMINANCE_STRIKE",
            "Dominance Opportunistic Strike",
            ctx => ctx.OpportunisticStrike && ctx.IsAggressive,
            ctx => { ctx.RecommendedAction = "intimidate"; },
            priority: 920
        );

        // Growth mode: Aggressive expansion when conditions are right
        _agentRules.AddRule(
            "GROWTH_EXPAND",
            "Growth Phase Expansion",
            ctx => ctx.AggressiveOpportunity,
            ctx => { ctx.RecommendedAction = "expand"; },
            priority: 910
        );

        // =====================================================================
        // DEFENSIVE RULES - React to threats
        // =====================================================================

        // Defensive: Lay low when under threat and not dominant
        _agentRules.AddRule(
            "DEFENSIVE_LAYLOW",
            "Defensive Posture",
            ctx => ctx.DefensivePosture && ctx.IsCautious,
            ctx => { ctx.RecommendedAction = "laylow"; },
            priority: 900
        );

        // Defensive: Bribe to reduce heat when affordable
        _agentRules.AddRule(
            "DEFENSIVE_BRIBE",
            "Defensive Bribery",
            ctx => ctx.NeedsHeatReduction && ctx.CanAffordBribe,
            ctx => { ctx.RecommendedAction = "bribe"; },
            priority: 895
        );

        // Defensive: Recruit soldiers when rival attack is imminent
        // Using RuleBuilder for complex multi-condition rules (clearer than inline)
        var defensiveRecruit = new RuleBuilder<AgentDecisionContext>()
            .WithId("DEFENSIVE_RECRUIT")
            .WithName("Defensive Recruitment")
            .WithPriority(890)
            .When(ctx => ctx.RivalAttackImminent)
            .And(ctx => ctx.NeedsMoreSoldiers)
            .And(ctx => ctx.GameState.FamilyWealth > 10000)
            .Then(ctx => ctx.RecommendedAction = "recruit")
            .Build();
        _agentRules.RegisterRule(defensiveRecruit);

        // =====================================================================
        // PERSONALITY-DRIVEN RULES - Agent character affects decisions
        // =====================================================================

        // Loyal agents protect family when heat is high
        _agentRules.AddRule(
            "LOYAL_PROTECT",
            "Family Protection",
            ctx => ctx.IsFamilyFirst && ctx.HeatIsDangerous,
            ctx => { ctx.RecommendedAction = "laylow"; },
            priority: 850
        );

        // Greedy agents prioritize money when family needs it
        _agentRules.AddRule(
            "GREEDY_COLLECTION",
            "Greedy Agent Collection",
            ctx => ctx.IsGreedy && ctx.FamilyNeedsMoney && !ctx.HeatIsDangerous,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 800
        );

        // Aggressive agents retaliate when family is threatened (but not at critical heat)
        _agentRules.AddRule(
            "AGGRESSIVE_RETALIATE",
            "Aggressive Retaliation",
            ctx => ctx.IsAggressive && ctx.FamilyUnderThreat && !ctx.HeatIsCritical,
            ctx => { ctx.RecommendedAction = "intimidate"; },
            priority: 750
        );

        // Hot-headed agents act impulsively when they can take risks
        _agentRules.AddRule(
            "HOTHEADED_VIOLENCE",
            "Hot-headed Violence",
            ctx => ctx.IsHotHeaded && ctx.HeatRiskWorthIt,
            ctx => { ctx.RecommendedAction = "intimidate"; },
            priority: 700
        );

        // Ambitious agents expand when in growth mode
        _agentRules.AddRule(
            "AMBITIOUS_EXPAND",
            "Ambitious Expansion",
            ctx => ctx.IsAmbitious && ctx.GoodTimeToExpand && ctx.GameState.FamilyWealth > 100000,
            ctx => { ctx.RecommendedAction = "expand"; },
            priority: 650
        );

        // Ambitious agents recruit when needing more soldiers
        // Using RuleBuilder - 4 conditions are clearer with fluent API
        var ambitiousRecruit = new RuleBuilder<AgentDecisionContext>()
            .WithId("AMBITIOUS_RECRUIT")
            .WithName("Ambitious Recruitment")
            .WithPriority(625)
            .When(ctx => ctx.IsAmbitious)
            .And(ctx => ctx.NeedsMoreSoldiers)
            .And(ctx => !ctx.ShouldConserve)
            .And(ctx => ctx.GameState.FamilyWealth > 50000)
            .Then(ctx => ctx.RecommendedAction = "recruit")
            .Build();
        _agentRules.RegisterRule(ambitiousRecruit);

        // Calculating agents bribe strategically
        _agentRules.AddRule(
            "CALCULATING_BRIBE",
            "Strategic Bribe",
            ctx => ctx.IsCalculating && ctx.CanAffordBribe && ctx.HeatIsRising,
            ctx => { ctx.RecommendedAction = "bribe"; },
            priority: 600
        );

        // Calculating agents make strategic collections when rivals are peaceful
        _agentRules.AddRule(
            "CALCULATING_STRATEGY",
            "Strategic Planning",
            ctx => ctx.IsCalculating && ctx.RivalsArePeaceful && ctx.HasHeatBudget,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 550
        );

        // =====================================================================
        // OPPORTUNISTIC RULES - Take advantage of good situations
        // =====================================================================

        // Peaceful times: safe to collect
        // Using RuleBuilder - demonstrates fluent syntax for opportunistic rules
        var opportunisticCollection = new RuleBuilder<AgentDecisionContext>()
            .WithId("OPPORTUNISTIC_COLLECTION")
            .WithName("Opportunistic Collection")
            .WithPriority(400)
            .When(ctx => ctx.RivalsArePeaceful)
            .And(ctx => ctx.HasHeatBudget)
            .And(ctx => ctx.HeatIsFalling)
            .Then(ctx => ctx.RecommendedAction = "collection")
            .Build();
        _agentRules.RegisterRule(opportunisticCollection);

        // Wealthy and stable: expand operations
        _agentRules.AddRule(
            "OPPORTUNISTIC_EXPAND",
            "Opportunistic Expansion",
            ctx => ctx.CanAffordExpensive && ctx.HasHeatBudget && !ctx.RivalIsThreatening,
            ctx => { ctx.RecommendedAction = "expand"; },
            priority: 350
        );

        // =====================================================================
        // DEFAULT RULES - Fallback behavior
        // =====================================================================

        // Accumulation phase default: focus on collection
        _agentRules.AddRule(
            "DEFAULT_ACCUMULATION",
            "Default Accumulation",
            ctx => ctx.InAccumulationMode && !ctx.HeatIsDangerous,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 100
        );

        // Default: cautious wait
        _agentRules.AddRule(
            "DEFAULT_WAIT",
            "Cautious Waiting",
            ctx => true,
            ctx => { ctx.RecommendedAction = "wait"; },
            priority: 1
        );
    }

    // =========================================================================
    // EVENT RULES - Event generation
    // =========================================================================

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

    // =========================================================================
    // VALUATION RULES - Territory economics
    // =========================================================================

    private void SetupValuationRules()
    {
        // Premium territories
        _valuationEngine.AddRule(
            "VALUATION_PRIME",
            "Prime Territory Premium",
            ctx => ctx.PrimeTerritory && ctx.HighDemand,
            ctx => {
                ctx.Territory.WeeklyRevenue = ctx.Territory.WeeklyRevenue * 1.5m;
                Console.WriteLine($"  💎 {ctx.Territory.Name} is prime real estate! Revenue +50%");
            },
            priority: 1000
        );

        // High risk discount
        _valuationEngine.AddRule(
            "VALUATION_RISKY",
            "High Risk Discount",
            ctx => ctx.IsHighRisk && ctx.PoliceWatching,
            ctx => {
                ctx.Territory.WeeklyRevenue = ctx.Territory.WeeklyRevenue * 0.7m;
                Console.WriteLine($"  ⚠️  {ctx.Territory.Name} too hot - Revenue -30%");
            },
            priority: 900
        );

        // Gambling boom during good times
        _valuationEngine.AddRule(
            "VALUATION_GAMBLING_BOOM",
            "Gambling Boom",
            ctx => ctx.IsGambling && ctx.FamilyReputation > 70 && !ctx.PoliceWatching,
            ctx => {
                ctx.Territory.WeeklyRevenue += 5000;
                Console.WriteLine($"  🎰 Gambling booming at {ctx.Territory.Name}! +$5,000");
            },
            priority: 850
        );

        // Smuggling premium when heat is low
        _valuationEngine.AddRule(
            "VALUATION_SMUGGLING_SAFE",
            "Safe Smuggling Routes",
            ctx => ctx.IsSmuggling && ctx.PoliceHeat < 40,
            ctx => {
                ctx.Territory.WeeklyRevenue += 8000;
                Console.WriteLine($"  🚢 Smuggling routes open at {ctx.Territory.Name}! +$8,000");
            },
            priority: 850
        );

        // Disputed territory penalty
        _valuationEngine.AddRule(
            "VALUATION_DISPUTED",
            "Territory Under Dispute",
            ctx => ctx.Disputed,
            ctx => {
                ctx.Territory.WeeklyRevenue = ctx.Territory.WeeklyRevenue * 0.5m;
                Console.WriteLine($"  ⚔️  {ctx.Territory.Name} contested - Revenue -50%");
            },
            priority: 950
        );

        // Market saturation
        _valuationEngine.AddRule(
            "VALUATION_SATURATED",
            "Market Saturation",
            ctx => ctx.MarketIsSaturated && !ctx.IsHighValue,
            ctx => {
                ctx.Territory.WeeklyRevenue -= 2000;
                Console.WriteLine($"  📉 Market saturated - {ctx.Territory.Name} -$2,000");
            },
            priority: 700
        );
    }

    // =========================================================================
    // DIFFICULTY RULES - Dynamic difficulty adjustment
    // =========================================================================

    private void SetupDifficultyRules()
    {
        // Player dominating - increase difficulty
        _difficultyEngine.AddRule(
            "DIFFICULTY_RAMP_UP",
            "Increase Challenge",
            ctx => ctx.PlayerDominating && ctx.OnWinStreak,
            ctx => {
                // Make rivals stronger
                foreach (var rival in ctx.State.RivalFamilies.Values)
                {
                    rival.Strength += 10;
                    rival.Hostility += 15;
                }
                Console.WriteLine("🔥 The other families are getting nervous about your success!");
                Console.WriteLine("   Rivals are stronger and more aggressive");
            },
            priority: 1000
        );

        // Player struggling - give help
        _difficultyEngine.AddRule(
            "DIFFICULTY_ASSIST",
            "Provide Assistance",
            ctx => ctx.PlayerStruggling && ctx.OnLossStreak && !ctx.LateGame,
            ctx => {
                // Give bonus
                ctx.State.FamilyWealth += 20000;
                ctx.State.HeatLevel = Math.Max(0, ctx.State.HeatLevel - 20);
                Console.WriteLine("🍀 Lucky break! An old debt has been repaid");
                Console.WriteLine("   +$20,000 and heat reduced");
            },
            priority: 1000
        );

        // Steady growth - maintain balance
        _difficultyEngine.AddRule(
            "DIFFICULTY_BALANCED",
            "Maintain Balance",
            ctx => ctx.SteadyGrowth && !ctx.PlayerDominating && !ctx.PlayerStruggling,
            ctx => {
                Console.WriteLine("⚖️  The balance of power remains stable");
            },
            priority: 500
        );

        // Late game ramp
        _difficultyEngine.AddRule(
            "DIFFICULTY_ENDGAME",
            "Endgame Challenge",
            ctx => ctx.LateGame && ctx.State.Week % 5 == 0,
            ctx => {
                // Increase pressure
                ctx.State.HeatLevel += 5;
                foreach (var rival in ctx.State.RivalFamilies.Values)
                {
                    rival.Hostility += 5;
                }
                Console.WriteLine("⏰ The endgame approaches - all families are on edge");
            },
            priority: 900
        );
    }

    // =========================================================================
    // STRATEGY RULES - Rival AI behavior
    // =========================================================================

    private void SetupStrategyRules()
    {
        // Opportunistic attack
        _strategyEngine.AddRule(
            "STRATEGY_ATTACK_WEAK",
            "Attack Weak Player",
            ctx => ctx.ShouldAttack,
            ctx => {
                var damage = Random.Shared.Next(10000, 25000);
                ctx.GameState.FamilyWealth -= damage;
                ctx.Rival.Hostility -= 15;
                Console.WriteLine($"⚔️  {ctx.Rival.Name} sees weakness and attacks!");
                Console.WriteLine($"   Lost ${damage:N0} to the attack");
            },
            priority: 1000
        );

        // Make peace when losing
        _strategyEngine.AddRule(
            "STRATEGY_SUE_FOR_PEACE",
            "Rival Seeks Peace",
            ctx => ctx.ShouldMakePeace && ctx.AtWar,
            ctx => {
                ctx.Rival.Hostility -= 40;
                ctx.Rival.AtWar = false;
                Console.WriteLine($"🕊️  {ctx.Rival.Name} seeks peace - they're weakened");
            },
            priority: 950
        );

        // Form temporary alliance
        _strategyEngine.AddRule(
            "STRATEGY_ALLIANCE",
            "Rival Proposes Alliance",
            ctx => ctx.ShouldFormAlliance && !ctx.AtWar && ctx.RivalHostility < 40,
            ctx => {
                ctx.Rival.Hostility -= 20;
                ctx.GameState.Reputation += 5;
                Console.WriteLine($"🤝 {ctx.Rival.Name} proposes a temporary alliance");
                Console.WriteLine("   Reputation +5");
            },
            priority: 900
        );

        // Wait and watch
        _strategyEngine.AddRule(
            "STRATEGY_OBSERVE",
            "Rival Watches and Waits",
            ctx => ctx.ShouldWait,
            ctx => {
                // Slow hostility decrease
                ctx.Rival.Hostility = Math.Max(0, ctx.Rival.Hostility - 5);
            },
            priority: 500
        );

        // Provoke when player has high heat
        _strategyEngine.AddRule(
            "STRATEGY_PROVOKE",
            "Provoke Distracted Player",
            ctx => ctx.PlayerIsDistracted && ctx.RivalIsAngry,
            ctx => {
                ctx.GameState.HeatLevel += 10;
                Console.WriteLine($"🎯 {ctx.Rival.Name} provokes you while you're distracted!");
                Console.WriteLine("   Heat +10");
            },
            priority: 850
        );
    }

    // =========================================================================
    // CHAIN REACTION RULES - Cascading events
    // =========================================================================

    private void SetupChainReactionRules()
    {
        // Police raid triggers informant fears
        _chainEngine.AddRule(
            "CHAIN_RAID_TO_INFORMANT",
            "Raid Triggers Informant Paranoia",
            ctx => ctx.WasPoliceRaid && ctx.State.HeatLevel > 50,
            ctx => {
                ctx.State.Reputation -= 10;
                Console.WriteLine("  ↳ The raid has everyone paranoid about informants");
                Console.WriteLine("     Reputation -10");
            },
            priority: 1000
        );

        // Hit triggers war
        _chainEngine.AddRule(
            "CHAIN_HIT_TO_WAR",
            "Hit Escalates to War",
            ctx => ctx.WasHit && ctx.HighTension,
            ctx => {
                var rival = ctx.State.RivalFamilies.Values.First(r => r.Hostility > 80);
                rival.AtWar = true;
                rival.Hostility = 100;
                Console.WriteLine($"  ↳ The hit has started a war with {rival.Name}!");
            },
            priority: 1000
        );

        // Betrayal triggers leadership crisis
        _chainEngine.AddRule(
            "CHAIN_BETRAYAL_TO_CRISIS",
            "Betrayal Triggers Crisis",
            ctx => ctx.WasBetrayal && ctx.Unstable,
            ctx => {
                ctx.State.Reputation -= 15;
                ctx.State.FamilyWealth -= 10000;
                Console.WriteLine("  ↳ The betrayal triggers a leadership crisis!");
                Console.WriteLine("     Multiple soldiers question their loyalty");
                Console.WriteLine("     Reputation -15, Wealth -$10,000");
            },
            priority: 1000
        );

        // Territory loss triggers revenge
        _chainEngine.AddRule(
            "CHAIN_LOSS_TO_REVENGE",
            "Loss Triggers Revenge",
            ctx => ctx.WasTerritoryLoss && !ctx.CrisisMode,
            ctx => {
                Console.WriteLine("  ↳ The loss demands revenge!");
                Console.WriteLine("     Your soldiers are calling for blood");
                // Could trigger automatic retaliation
            },
            priority: 900
        );

        // Multiple crises compound
        _chainEngine.AddRule(
            "CHAIN_COMPOUND_CRISIS",
            "Compounding Crises",
            ctx => ctx.CrisisMode && (ctx.WasPoliceRaid || ctx.WasBetrayal),
            ctx => {
                Console.WriteLine("  ↳ ⚠️  CRISIS IS COMPOUNDING!");
                Console.WriteLine("     The family is in serious danger");
                ctx.State.HeatLevel += 15;
                ctx.State.Reputation -= 10;
            },
            priority: 1100
        );
    }

    // =========================================================================
    // PUBLIC API - Evaluation methods
    // =========================================================================

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

        var matchedRules = _gameRules.GetMatchingRules(context);

        foreach (var rule in matchedRules)
        {
            events.Add($"[Rule: {rule.Name}]");
        }

        return events;
    }

    /// <summary>
    /// Get agent action using rules. Rules are evaluated in priority order
    /// and the first matching rule sets the RecommendedAction on the context.
    /// With StopOnFirstMatch enabled, only the highest priority matching rule executes.
    /// </summary>
    public string GetAgentAction(GameAgentData agent)
    {
        var context = new AgentDecisionContext
        {
            AgentId = agent.AgentId,
            Aggression = agent.Personality.Aggression,
            Greed = agent.Personality.Greed,
            Loyalty = agent.Personality.Loyalty,
            Ambition = agent.Personality.Ambition,
            GameState = _state,
            RecommendedAction = "wait" // Default
        };

        // Use the engine's Execute method which:
        // 1. Respects StopOnFirstMatch option (only first matching rule runs)
        // 2. Tracks performance metrics for debugging
        _agentRules.Execute(context);

        return context.RecommendedAction;
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

        var matchedRules = _eventRules.GetMatchingRules(context);

        foreach (var rule in matchedRules.Take(2)) // Max 2 events per turn
        {
            events.Add($"Event triggered by rule: {rule.Name}");
        }

        return events;
    }

    /// <summary>
    /// Apply territory valuation rules
    /// </summary>
    public void ApplyTerritoryValuation(Territory territory, GameState state)
    {
        var context = new TerritoryValueContext
        {
            Territory = territory,
            GameState = state
        };

        _valuationEngine.EvaluateAll(context);
    }

    /// <summary>
    /// Apply difficulty adjustment
    /// </summary>
    public void ApplyDifficultyAdjustment(GameState state, decimal averageIncome, int winStreak, int lossStreak)
    {
        var context = new DifficultyContext
        {
            State = state,
            Week = state.Week,
            AverageWeeklyIncome = averageIncome,
            WinStreak = winStreak,
            LossStreak = lossStreak
        };

        _difficultyEngine.EvaluateAll(context);
    }

    /// <summary>
    /// Apply rival strategy
    /// </summary>
    public void ApplyRivalStrategy(RivalFamily rival, GameState state)
    {
        var context = new RivalStrategyContext
        {
            Rival = rival,
            GameState = state
        };

        _strategyEngine.EvaluateAll(context);
    }

    /// <summary>
    /// Apply chain reactions from event
    /// </summary>
    public void ApplyChainReactions(string triggeringEvent, GameState state, Dictionary<string, object>? data = null)
    {
        var context = new ChainReactionContext
        {
            TriggeringEvent = triggeringEvent,
            State = state,
            EventData = data ?? new Dictionary<string, object>()
        };

        _chainEngine.EvaluateAll(context);
    }

    // =========================================================================
    // PERFORMANCE METRICS - For debugging and optimization
    // =========================================================================

    /// <summary>
    /// Get performance metrics for agent decision rules
    /// </summary>
    public Dictionary<string, RulePerformanceMetrics> GetAgentRuleMetrics()
    {
        return _agentRules.GetAllMetrics();
    }

    /// <summary>
    /// Get a summary of agent rule performance for display
    /// </summary>
    public string GetAgentRulePerformanceSummary()
    {
        var metrics = _agentRules.GetAllMetrics();
        if (metrics.Count == 0)
            return "No performance data collected yet.";

        var totalExecutions = metrics.Values.Sum(m => m.ExecutionCount);
        var avgTime = metrics.Values.Where(m => m.ExecutionCount > 0)
            .Average(m => m.AverageExecutionTime.TotalMicroseconds);

        var topRules = metrics.Values
            .Where(m => m.ExecutionCount > 0)
            .OrderByDescending(m => m.ExecutionCount)
            .Take(5)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("📊 Agent Rule Performance Metrics:");
        sb.AppendLine($"   Total rule evaluations: {totalExecutions}");
        sb.AppendLine($"   Average execution time: {avgTime:F2}μs");
        sb.AppendLine("   Top 5 triggered rules:");

        foreach (var rule in topRules)
        {
            sb.AppendLine($"     • {rule.RuleId}: {rule.ExecutionCount}x (avg {rule.AverageExecutionTime.TotalMicroseconds:F1}μs)");
        }

        return sb.ToString();
    }

    // =========================================================================
    // DYNAMIC RULE FACTORY - Configurable/Moddable Agent Rules
    // =========================================================================

    /// <summary>
    /// Register agent rules dynamically from configuration definitions.
    /// This enables modding and external configuration of game behavior.
    /// </summary>
    /// <param name="definitions">Rule definitions with their associated actions</param>
    /// <returns>Number of rules registered</returns>
    public int RegisterDynamicAgentRules(IEnumerable<AgentRuleDefinition> definitions)
    {
        int count = 0;
        foreach (var def in definitions)
        {
            var rule = CreateDynamicAgentRule(def);
            _agentRules.RegisterRule(rule);
            count++;
        }
        return count;
    }

    /// <summary>
    /// Creates a single agent rule from a definition using DynamicRuleFactory
    /// </summary>
    private Rule<AgentDecisionContext> CreateDynamicAgentRule(AgentRuleDefinition definition)
    {
        // Use DynamicRuleFactory to build the rule from property conditions
        var rules = DynamicRuleFactory.CreateRulesFromDefinitions<AgentDecisionContext>(
            new[] { definition.ToRuleDefinition() }
        );

        var baseRule = rules.First();

        // Add the action that sets the recommended action
        return baseRule.WithAction(ctx => ctx.RecommendedAction = definition.RecommendedAction);
    }

    /// <summary>
    /// Load example configurable rules that demonstrate DynamicRuleFactory usage.
    /// In a real game, these could be loaded from JSON/XML config files.
    /// </summary>
    public void LoadExampleConfigurableRules()
    {
        var configurableRules = new List<AgentRuleDefinition>
        {
            // Example 1: Simple boolean condition rule
            // "When in survival mode with dangerous heat, always lay low"
            new AgentRuleDefinition
            {
                Id = "CONFIG_EMERGENCY_LAYLOW",
                Name = "Configurable Emergency LayLow",
                Description = "Loaded from configuration: Emergency heat management",
                Priority = 999, // Highest priority - overrides other rules
                Conditions = new List<ConditionDefinition>
                {
                    new ConditionDefinition { PropertyName = "InSurvivalMode", Operator = "==", Value = true },
                    new ConditionDefinition { PropertyName = "HeatIsCritical", Operator = "==", Value = true }
                },
                RecommendedAction = "laylow"
            },

            // Example 2: Multi-condition expansion rule
            // "When wealth is growing and heat is manageable, expand"
            new AgentRuleDefinition
            {
                Id = "CONFIG_SMART_EXPAND",
                Name = "Configurable Smart Expansion",
                Description = "Loaded from configuration: Opportunistic expansion",
                Priority = 400,
                Conditions = new List<ConditionDefinition>
                {
                    new ConditionDefinition { PropertyName = "GoodTimeToExpand", Operator = "==", Value = true },
                    new ConditionDefinition { PropertyName = "InGrowthMode", Operator = "==", Value = true }
                },
                RecommendedAction = "expand"
            },

            // Example 3: Defensive recruitment
            // "When rivals are threatening and we need soldiers, recruit"
            new AgentRuleDefinition
            {
                Id = "CONFIG_DEFENSIVE_RECRUIT",
                Name = "Configurable Defensive Recruitment",
                Description = "Loaded from configuration: Build forces when threatened",
                Priority = 600,
                Conditions = new List<ConditionDefinition>
                {
                    new ConditionDefinition { PropertyName = "RivalIsThreatening", Operator = "==", Value = true },
                    new ConditionDefinition { PropertyName = "NeedsMoreSoldiers", Operator = "==", Value = true }
                },
                RecommendedAction = "recruit"
            }
        };

        RegisterDynamicAgentRules(configurableRules);
    }

    /// <summary>
    /// Get all registered dynamic rule IDs (useful for debugging/mod management)
    /// </summary>
    public IEnumerable<string> GetDynamicRuleIds()
    {
        return _agentRules.GetAllMetrics()
            .Keys
            .Where(id => id.StartsWith("CONFIG_"));
    }
}

// =============================================================================
// AGENT RULE DEFINITION - Configuration model for moddable rules
// =============================================================================

/// <summary>
/// Definition for creating agent decision rules from configuration.
/// This extends the base RuleDefinition with an action to execute.
/// Can be serialized to/from JSON/XML for modding support.
/// </summary>
public class AgentRuleDefinition
{
    /// <summary>Rule ID - should start with "CONFIG_" for configurable rules</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Human-readable rule name</summary>
    public string Name { get; set; } = "";

    /// <summary>Description of what this rule does</summary>
    public string Description { get; set; } = "";

    /// <summary>Priority (higher = evaluated first)</summary>
    public int Priority { get; set; } = 0;

    /// <summary>Conditions that must all be true for the rule to match</summary>
    public List<ConditionDefinition> Conditions { get; set; } = new();

    /// <summary>
    /// The action to recommend when this rule matches.
    /// Valid values: "collect", "expand", "recruit", "attack", "laylow", "bribe", "negotiate"
    /// </summary>
    public string RecommendedAction { get; set; } = "";

    /// <summary>
    /// Converts this to a base RuleDefinition for DynamicRuleFactory
    /// </summary>
    public RuleDefinition ToRuleDefinition()
    {
        return new RuleDefinition
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Priority = Priority,
            Conditions = Conditions
        };
    }
}
