using RulesEngine.Core;
using RulesEngine.Enhanced;
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
        // COMPOSITE RULES - Complex multi-strategy decisions using CompositeRule
        // =====================================================================

        // Composite rule: Multiple ways to trigger "intimidate" action
        // Uses OR logic - if ANY sub-rule matches, the composite matches
        var intimidateStrategies = new CompositeRuleBuilder<AgentDecisionContext>()
            .WithId("COMPOSITE_INTIMIDATE")
            .WithName("Multi-Strategy Intimidation")
            .WithDescription("Combines multiple intimidation triggers with OR logic")
            .WithPriority(500)
            .WithOperator(CompositeOperator.Or)
            .AddRule(new RuleBuilder<AgentDecisionContext>()
                .WithId("INTIMIDATE_DOMINANT")
                .WithName("Dominant Position Strike")
                .When(ctx => ctx.InDominanceMode)
                .And(ctx => ctx.RivalIsWeak)
                .Build())
            .AddRule(new RuleBuilder<AgentDecisionContext>()
                .WithId("INTIMIDATE_DEFENSIVE")
                .WithName("Defensive Retaliation")
                .When(ctx => ctx.FamilyUnderThreat)
                .And(ctx => ctx.IsAggressive)
                .And(ctx => !ctx.HeatIsCritical)
                .Build())
            .AddRule(new RuleBuilder<AgentDecisionContext>()
                .WithId("INTIMIDATE_OPPORTUNISTIC")
                .WithName("Opportunistic Strike")
                .When(ctx => ctx.OpportunisticStrike)
                .Build())
            .Build();

        // Wrap composite with action
        _agentRules.AddRule(
            "COMPOSITE_INTIMIDATE_ACTION",
            "Composite Intimidation Strategy",
            ctx => intimidateStrategies.Evaluate(ctx),
            ctx => { ctx.RecommendedAction = "intimidate"; },
            priority: 500
        );

        // Composite rule: Multiple safe collection strategies (AND logic)
        // Uses AND logic - ALL sub-rules must match for composite to match
        var safeCollectionRequirements = new CompositeRuleBuilder<AgentDecisionContext>()
            .WithId("COMPOSITE_SAFE_COLLECT")
            .WithName("Safe Collection Prerequisites")
            .WithDescription("All conditions must be met for safe collection")
            .WithPriority(450)
            .WithOperator(CompositeOperator.And)
            .AddRule(new RuleBuilder<AgentDecisionContext>()
                .WithId("SAFE_HEAT_CHECK")
                .WithName("Heat Level Safe")
                .When(ctx => ctx.HasHeatBudget)
                .Build())
            .AddRule(new RuleBuilder<AgentDecisionContext>()
                .WithId("SAFE_RIVAL_CHECK")
                .WithName("No Imminent Rival Threat")
                .When(ctx => !ctx.RivalAttackImminent)
                .Build())
            .AddRule(new RuleBuilder<AgentDecisionContext>()
                .WithId("SAFE_ECONOMIC_CHECK")
                .WithName("Not in Crisis")
                .When(ctx => !ctx.InSurvivalMode || ctx.GameState.FamilyWealth > 20000)
                .Build())
            .Build();

        _agentRules.AddRule(
            "COMPOSITE_SAFE_COLLECT_ACTION",
            "Safe Collection Strategy",
            ctx => safeCollectionRequirements.Evaluate(ctx) && ctx.IsGreedy,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 450
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

        // =====================================================================
        // COMPOSITE EVENT RULES - Multiple triggers for same event type
        // =====================================================================

        // Composite: Crisis event triggers (OR logic - any of these causes crisis)
        var crisisConditions = new CompositeRuleBuilder<EventContext>()
            .WithId("COMPOSITE_CRISIS_CONDITIONS")
            .WithName("Crisis Event Triggers")
            .WithDescription("Multiple conditions that trigger a crisis event")
            .WithPriority(950)
            .WithOperator(CompositeOperator.Or)
            .AddRule(new RuleBuilder<EventContext>()
                .WithId("CRISIS_FINANCIAL")
                .WithName("Financial Crisis")
                .When(ctx => ctx.Wealth < 10000 && ctx.Week > 5)
                .Build())
            .AddRule(new RuleBuilder<EventContext>()
                .WithId("CRISIS_HEAT")
                .WithName("Heat Crisis")
                .When(ctx => ctx.Heat >= 95)
                .Build())
            .AddRule(new RuleBuilder<EventContext>()
                .WithId("CRISIS_ATTACK")
                .WithName("Under Attack Crisis")
                .When(ctx => ctx.TenseSituation && ctx.WeakPosition)
                .Build())
            .Build();

        _eventRules.AddRule(
            "EVENT_CRISIS",
            "Family Crisis",
            ctx => crisisConditions.Evaluate(ctx),
            ctx => { /* Generate crisis event requiring immediate action */ },
            priority: 950
        );

        // Composite: Good fortune event (AND logic - all conditions for windfall)
        var fortuneConditions = new CompositeRuleBuilder<EventContext>()
            .WithId("COMPOSITE_FORTUNE_CONDITIONS")
            .WithName("Good Fortune Conditions")
            .WithPriority(600)
            .WithOperator(CompositeOperator.And)
            .AddRule(new RuleBuilder<EventContext>()
                .WithId("FORTUNE_REPUTATION")
                .WithName("Good Standing")
                .When(ctx => ctx.Reputation > 70)
                .Build())
            .AddRule(new RuleBuilder<EventContext>()
                .WithId("FORTUNE_LOW_HEAT")
                .WithName("Low Police Interest")
                .When(ctx => ctx.Heat < 30)
                .Build())
            .AddRule(new RuleBuilder<EventContext>()
                .WithId("FORTUNE_TIMING")
                .WithName("Quarterly Timing")
                .When(ctx => ctx.Week % 8 == 0 && ctx.Week > 0)
                .Build())
            .Build();

        _eventRules.AddRule(
            "EVENT_WINDFALL",
            "Unexpected Windfall",
            ctx => fortuneConditions.Evaluate(ctx),
            ctx => { /* Generate windfall event - bonus income */ },
            priority: 600
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

        // =====================================================================
        // COMPOSITE VALUATION RULES - Complex territory pricing
        // =====================================================================

        // Composite: Golden territory (AND logic - all premium conditions)
        var goldenConditions = new CompositeRuleBuilder<TerritoryValueContext>()
            .WithId("COMPOSITE_GOLDEN_TERRITORY")
            .WithName("Golden Territory Conditions")
            .WithPriority(1100)
            .WithOperator(CompositeOperator.And)
            .AddRule(new RuleBuilder<TerritoryValueContext>()
                .WithId("GOLDEN_PRIME")
                .WithName("Prime Location")
                .When(ctx => ctx.PrimeTerritory)
                .Build())
            .AddRule(new RuleBuilder<TerritoryValueContext>()
                .WithId("GOLDEN_LOW_HEAT")
                .WithName("Under the Radar")
                .When(ctx => ctx.PoliceHeat < 20)
                .Build())
            .AddRule(new RuleBuilder<TerritoryValueContext>()
                .WithId("GOLDEN_HIGH_REP")
                .WithName("Well Respected")
                .When(ctx => ctx.FamilyReputation > 80)
                .Build())
            .Build();

        _valuationEngine.AddRule(
            "VALUATION_GOLDEN",
            "Golden Territory Bonus",
            ctx => goldenConditions.Evaluate(ctx),
            ctx => {
                ctx.Territory.WeeklyRevenue = ctx.Territory.WeeklyRevenue * 2.0m;
                Console.WriteLine($"  🌟 {ctx.Territory.Name} is GOLDEN! Revenue x2!");
            },
            priority: 1100
        );

        // Composite: Troubled territory (OR logic - any problem reduces value)
        var troubledConditions = new CompositeRuleBuilder<TerritoryValueContext>()
            .WithId("COMPOSITE_TROUBLED_TERRITORY")
            .WithName("Troubled Territory Conditions")
            .WithPriority(800)
            .WithOperator(CompositeOperator.Or)
            .AddRule(new RuleBuilder<TerritoryValueContext>()
                .WithId("TROUBLED_DISPUTED")
                .WithName("Under Dispute")
                .When(ctx => ctx.Disputed)
                .Build())
            .AddRule(new RuleBuilder<TerritoryValueContext>()
                .WithId("TROUBLED_POLICE")
                .WithName("Heavy Police Presence")
                .When(ctx => ctx.PoliceHeat > 80)
                .Build())
            .AddRule(new RuleBuilder<TerritoryValueContext>()
                .WithId("TROUBLED_LOW_REP")
                .WithName("Poor Reputation")
                .When(ctx => ctx.FamilyReputation < 30)
                .Build())
            .Build();

        _valuationEngine.AddRule(
            "VALUATION_TROUBLED",
            "Troubled Territory Penalty",
            ctx => troubledConditions.Evaluate(ctx) && !ctx.PrimeTerritory,
            ctx => {
                ctx.Territory.WeeklyRevenue = ctx.Territory.WeeklyRevenue * 0.6m;
                Console.WriteLine($"  💔 {ctx.Territory.Name} has problems - Revenue -40%");
            },
            priority: 800
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

    // =========================================================================
    // RULE ANALYZER - Detect rule conflicts, overlaps, and dead rules
    // =========================================================================

    /// <summary>
    /// Analyze agent rules using a set of test scenarios.
    /// Detects overlapping rules, dead rules, and provides match statistics.
    /// </summary>
    /// <param name="testScenarios">Test cases to analyze against</param>
    /// <returns>Analysis report with findings</returns>
    public AnalysisReport AnalyzeAgentRules(IEnumerable<AgentDecisionContext> testScenarios)
    {
        var analyzer = new RuleAnalyzer<AgentDecisionContext>(_agentRules, testScenarios);
        return analyzer.Analyze();
    }

    /// <summary>
    /// Generate standard test scenarios for agent rule analysis.
    /// Creates a variety of game states to test rule coverage.
    /// </summary>
    public List<AgentDecisionContext> GenerateAnalysisTestCases()
    {
        var testCases = new List<AgentDecisionContext>();

        // Scenario 1: Survival mode, critical heat
        var survivalCritical = new GameState
        {
            FamilyWealth = 20000m,
            HeatLevel = 90,
            PreviousHeatLevel = 85,
            SoldierCount = 10
        };
        survivalCritical.RivalFamilies["test"] = new RivalFamily { Hostility = 50 };
        testCases.Add(CreateTestContext(survivalCritical, aggressive: false, greedy: false));

        // Scenario 2: Dominance mode, weak rival
        var dominanceWeak = new GameState
        {
            FamilyWealth = 500000m,
            HeatLevel = 20,
            Reputation = 90,
            SoldierCount = 50
        };
        dominanceWeak.RivalFamilies["test"] = new RivalFamily { Hostility = 30, Strength = 30 };
        testCases.Add(CreateTestContext(dominanceWeak, aggressive: true, greedy: false));

        // Scenario 3: Growth mode, peaceful
        var growthPeaceful = new GameState
        {
            FamilyWealth = 150000m,
            HeatLevel = 30,
            PreviousHeatLevel = 35,
            Reputation = 60,
            SoldierCount = 20
        };
        growthPeaceful.RivalFamilies["test"] = new RivalFamily { Hostility = 20 };
        testCases.Add(CreateTestContext(growthPeaceful, aggressive: false, greedy: true));

        // Scenario 4: Under attack, need defense
        var underAttack = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 50,
            SoldierCount = 8
        };
        underAttack.RivalFamilies["test"] = new RivalFamily { Hostility = 90, Strength = 70 };
        testCases.Add(CreateTestContext(underAttack, aggressive: true, greedy: false));

        // Scenario 5: Accumulation mode, stable
        var accumulationStable = new GameState
        {
            FamilyWealth = 80000m,
            HeatLevel = 25,
            PreviousHeatLevel = 25,
            Reputation = 50,
            SoldierCount = 15
        };
        accumulationStable.RivalFamilies["test"] = new RivalFamily { Hostility = 40 };
        testCases.Add(CreateTestContext(accumulationStable, aggressive: false, greedy: true));

        // Scenario 6: High heat, need bribe
        var highHeatBribe = new GameState
        {
            FamilyWealth = 200000m,
            HeatLevel = 65,
            PreviousHeatLevel = 60,
            SoldierCount = 20
        };
        highHeatBribe.RivalFamilies["test"] = new RivalFamily { Hostility = 40 };
        testCases.Add(CreateTestContext(highHeatBribe, aggressive: false, greedy: false, calculating: true));

        return testCases;
    }

    private AgentDecisionContext CreateTestContext(GameState state, bool aggressive, bool greedy, bool calculating = false)
    {
        // IsCalculating requires: Aggression < 40 && Ambition > 60
        // So calculating agents need low aggression AND high ambition
        return new AgentDecisionContext
        {
            GameState = state,
            AgentId = "test-agent",
            Aggression = aggressive ? 80 : (calculating ? 30 : 50),
            Greed = greedy ? 80 : 30,
            Loyalty = 50,
            Ambition = calculating ? 70 : 60  // High ambition for calculating agents
        };
    }

    /// <summary>
    /// Run analysis and return a formatted report string.
    /// </summary>
    public string GetAgentRuleAnalysisReport()
    {
        var testCases = GenerateAnalysisTestCases();
        var report = AnalyzeAgentRules(testCases);
        return report.ToString();
    }

    // =========================================================================
    // DEBUGGABLE RULES - Trace WHY decisions were made
    // =========================================================================

    /// <summary>
    /// Get agent action with detailed debug tracing.
    /// Returns the recommended action and provides a trace showing
    /// which rules were evaluated and which one matched.
    /// </summary>
    public string GetAgentActionWithTrace(GameAgentData agent, out List<string> trace)
    {
        trace = new List<string>();
        var context = CreateAgentContext(agent);

        trace.Add($"=== Decision Trace for {agent.AgentId} ===");
        trace.Add($"Game Phase: {context.Phase}");
        trace.Add($"Heat: {context.GameState.HeatLevel} (Critical: {context.HeatIsCritical}, Dangerous: {context.HeatIsDangerous})");
        trace.Add($"Wealth: ${context.GameState.FamilyWealth:N0}");
        trace.Add($"Personality: Aggr={agent.Personality.Aggression}, Greed={agent.Personality.Greed}");
        trace.Add("");

        // Evaluate each rule and trace
        var rules = _agentRules.GetRules().OrderByDescending(r => r.Priority).ToList();
        trace.Add("Rule Evaluation (by priority):");

        foreach (var rule in rules)
        {
            var matches = rule.Evaluate(context);
            var symbol = matches ? "✓" : "✗";
            trace.Add($"  [{symbol}] {rule.Name} (P:{rule.Priority}) - {(matches ? "MATCHED" : "no match")}");

            if (matches)
            {
                // This rule would be selected (with StopOnFirstMatch)
                trace.Add($"");
                trace.Add($">>> Selected: {rule.Name}");
                break;
            }
        }

        // Execute to get the action
        _agentRules.Execute(context);
        trace.Add($">>> Action: {context.RecommendedAction}");

        return context.RecommendedAction ?? "wait";
    }

    /// <summary>
    /// Creates the agent context (extracted for reuse)
    /// </summary>
    private AgentDecisionContext CreateAgentContext(GameAgentData agent)
    {
        return new AgentDecisionContext
        {
            GameState = _state,
            AgentId = agent.AgentId,
            Aggression = agent.Personality.Aggression,
            Greed = agent.Personality.Greed,
            Loyalty = agent.Personality.Loyalty,
            Ambition = agent.Personality.Ambition
        };
    }

    // =========================================================================
    // ASYNC RULES - Delayed/async event processing
    // =========================================================================

    private readonly List<IAsyncRule<AsyncEventContext>> _asyncRules = new();

    /// <summary>
    /// Initialize async event rules for time-delayed game events.
    /// </summary>
    public void SetupAsyncEventRules()
    {
        // Async rule 1: Police Investigation (takes time to complete)
        var policeInvestigation = new AsyncRuleBuilder<AsyncEventContext>()
            .WithId("ASYNC_POLICE_INVESTIGATION")
            .WithName("Police Investigation")
            .WithPriority(100)
            .WithCondition(async ctx =>
            {
                // Simulate checking if investigation should start (uses centralized timing)
                await GameTimingOptions.DelayAsync(GameTimingOptions.Current.CapoThinkingMs);
                return ctx.TriggerType == "PoliceActivity" && ctx.GameState.HeatLevel > 50;
            })
            .WithAction(async ctx =>
            {
                // Simulate investigation taking time (uses centralized timing)
                await GameTimingOptions.DelayAsync(ctx.DelayMs);
                ctx.ResultMessage = "Police investigation completed - heat reduced by 10";
                ctx.GameState.HeatLevel = Math.Max(0, ctx.GameState.HeatLevel - 10);
                return RuleResult.Success("ASYNC_POLICE_INVESTIGATION", "Police Investigation",
                    new Dictionary<string, object> { ["HeatReduced"] = 10 });
            })
            .Build();
        _asyncRules.Add(policeInvestigation);

        // Async rule 2: Informant Network (gathering intel)
        var informantNetwork = new AsyncRuleBuilder<AsyncEventContext>()
            .WithId("ASYNC_INFORMANT_INTEL")
            .WithName("Informant Network Intel")
            .WithPriority(90)
            .WithCondition(async ctx =>
            {
                await GameTimingOptions.DelayAsync(GameTimingOptions.Current.AssociateThinkingMs);
                return ctx.TriggerType == "GatherIntel" && ctx.GameState.FamilyWealth > 50000;
            })
            .WithAction(async ctx =>
            {
                await GameTimingOptions.DelayAsync(ctx.DelayMs);
                // Reveal information about rivals
                var intel = ctx.GameState.RivalFamilies.Values.FirstOrDefault();
                ctx.ResultMessage = intel != null
                    ? $"Intel gathered: Rival strength is {intel.Strength}, hostility is {intel.Hostility}"
                    : "No rival intel available";
                return RuleResult.Success("ASYNC_INFORMANT_INTEL", "Informant Network Intel");
            })
            .Build();
        _asyncRules.Add(informantNetwork);

        // Async rule 3: Business Deal Negotiation
        var businessDeal = new AsyncRuleBuilder<AsyncEventContext>()
            .WithId("ASYNC_BUSINESS_DEAL")
            .WithName("Business Deal Negotiation")
            .WithPriority(80)
            .WithCondition(async ctx =>
            {
                await GameTimingOptions.DelayAsync(GameTimingOptions.Current.ConsigliereThinkingMs);
                return ctx.TriggerType == "BusinessOpportunity" && ctx.GameState.Reputation > 40;
            })
            .WithAction(async ctx =>
            {
                await GameTimingOptions.DelayAsync(ctx.DelayMs);
                var bonus = ctx.GameState.Reputation * 100;
                ctx.GameState.FamilyWealth += bonus;
                ctx.ResultMessage = $"Business deal completed! Earned ${bonus:N0}";
                return RuleResult.Success("ASYNC_BUSINESS_DEAL", "Business Deal Negotiation",
                    new Dictionary<string, object> { ["Bonus"] = bonus });
            })
            .Build();
        _asyncRules.Add(businessDeal);
    }

    /// <summary>
    /// Process an async event with optional delay simulation.
    /// </summary>
    /// <param name="triggerType">Type of event trigger</param>
    /// <param name="delayMs">Simulated delay in milliseconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result message from the processed event</returns>
    public async Task<string> ProcessAsyncEventAsync(
        string triggerType,
        int delayMs = 100,
        CancellationToken cancellationToken = default)
    {
        var context = new AsyncEventContext
        {
            GameState = _state,
            TriggerType = triggerType,
            DelayMs = delayMs,
            Timestamp = DateTime.UtcNow
        };

        foreach (var rule in _asyncRules.OrderByDescending(r => r.Priority))
        {
            if (await rule.EvaluateAsync(context, cancellationToken))
            {
                var result = await rule.ExecuteAsync(context, cancellationToken);
                if (result.Matched)
                {
                    return context.ResultMessage ?? "Event processed";
                }
            }
        }

        return "No matching async event handler";
    }

    /// <summary>
    /// Get registered async rule IDs
    /// </summary>
    public IEnumerable<string> GetAsyncRuleIds()
    {
        return _asyncRules.Select(r => r.Id);
    }

    // =========================================================================
    // RULE VALIDATOR - Validate rules at startup
    // =========================================================================

    /// <summary>
    /// Validate all registered agent rules and return any warnings/errors.
    /// Call this at startup to catch configuration issues early.
    /// </summary>
    /// <returns>List of validation messages (empty if all rules are valid)</returns>
    public List<string> ValidateAllAgentRules()
    {
        var messages = new List<string>();
        var rules = _agentRules.GetRules().ToList();

        // Check for duplicate priorities (can cause unpredictable behavior)
        var priorityGroups = rules.GroupBy(r => r.Priority).Where(g => g.Count() > 1);
        foreach (var group in priorityGroups)
        {
            var ruleNames = string.Join(", ", group.Select(r => r.Name));
            messages.Add($"[WARNING] Multiple rules with priority {group.Key}: {ruleNames}");
        }

        // Check for rules with empty/null IDs or names
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
                messages.Add($"[ERROR] Rule '{rule.Name}' has empty ID");
            if (string.IsNullOrWhiteSpace(rule.Name))
                messages.Add($"[ERROR] Rule with ID '{rule.Id}' has empty name");
        }

        // Check for very low priority rules that might never execute (with StopOnFirstMatch)
        var veryLowPriorityRules = rules.Where(r => r.Priority < 10 && r.Priority > 1).ToList();
        if (veryLowPriorityRules.Any())
        {
            messages.Add($"[INFO] {veryLowPriorityRules.Count} rules have very low priority (2-9) and may rarely execute");
        }

        return messages;
    }

    /// <summary>
    /// Run rule analysis and log warnings about conflicts/dead rules.
    /// Call this at startup in debug mode for comprehensive validation.
    /// </summary>
    /// <returns>Analysis summary with any issues found</returns>
    public string RunStartupRuleAnalysis()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Agent Rule Startup Analysis ===");
        sb.AppendLine();

        // Basic validation
        var validationMessages = ValidateAllAgentRules();
        if (validationMessages.Any())
        {
            sb.AppendLine("Validation Issues:");
            foreach (var msg in validationMessages)
                sb.AppendLine($"  {msg}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("✓ Basic validation passed");
        }

        // Rule analysis with test scenarios
        var testCases = GenerateAnalysisTestCases();
        var report = AnalyzeAgentRules(testCases);

        // Check for dead rules (never matched any test case)
        var deadRules = report.RuleAnalyses.Where(a => a.MatchedCount == 0).ToList();
        if (deadRules.Any())
        {
            sb.AppendLine();
            sb.AppendLine($"⚠ Potential Dead Rules ({deadRules.Count} rules never matched):");
            foreach (var rule in deadRules.Take(10))
                sb.AppendLine($"  - {rule.RuleId}");
            if (deadRules.Count > 10)
                sb.AppendLine($"  ... and {deadRules.Count - 10} more");
        }

        // Check for overlapping rules (multiple rules match same scenarios)
        var overlaps = report.Overlaps.Take(5).ToList();
        if (overlaps.Any())
        {
            sb.AppendLine();
            sb.AppendLine("⚠ Rules with overlaps (may cause priority conflicts):");
            foreach (var overlap in overlaps)
            {
                sb.AppendLine($"  - {overlap.Rule1} overlaps with {overlap.Rule2} ({overlap.OverlapRate:P0})");
            }
        }

        // Summary stats
        sb.AppendLine();
        sb.AppendLine($"Total rules: {report.RuleAnalyses.Count}");
        sb.AppendLine($"Test scenarios: {testCases.Count}");
        sb.AppendLine($"Rules with matches: {report.RuleAnalyses.Count(a => a.MatchedCount > 0)}");

        return sb.ToString();
    }

    // =========================================================================
    // EXTENDED TEST SCENARIOS - More coverage for rule analysis
    // =========================================================================

    /// <summary>
    /// Generate extended test scenarios covering edge cases and all personality types.
    /// Use this for comprehensive rule analysis.
    /// </summary>
    public List<AgentDecisionContext> GenerateExtendedTestCases()
    {
        var testCases = GenerateAnalysisTestCases(); // Start with base cases

        // Edge case: Zero wealth (bankruptcy)
        var bankrupt = new GameState
        {
            FamilyWealth = 0m,
            HeatLevel = 50,
            SoldierCount = 5
        };
        bankrupt.RivalFamilies["test"] = new RivalFamily { Hostility = 60 };
        testCases.Add(CreateTestContext(bankrupt, aggressive: false, greedy: true));

        // Edge case: Maximum heat
        var maxHeat = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 100,
            PreviousHeatLevel = 95,
            SoldierCount = 20
        };
        maxHeat.RivalFamilies["test"] = new RivalFamily { Hostility = 50 };
        testCases.Add(CreateTestContext(maxHeat, aggressive: false, greedy: false));

        // Hot-headed agent (high aggression, low loyalty)
        var hotHeadedState = new GameState
        {
            FamilyWealth = 150000m,
            HeatLevel = 40,
            SoldierCount = 25
        };
        hotHeadedState.RivalFamilies["test"] = new RivalFamily { Hostility = 70, Strength = 50 };
        testCases.Add(new AgentDecisionContext
        {
            GameState = hotHeadedState,
            AgentId = "hotheaded-agent",
            Aggression = 85,  // IsHotHeaded requires > 80
            Greed = 50,
            Loyalty = 40,     // IsHotHeaded requires < 60
            Ambition = 60
        });

        // Family-first loyal agent
        var loyalState = new GameState
        {
            FamilyWealth = 80000m,
            HeatLevel = 70,
            SoldierCount = 15
        };
        loyalState.RivalFamilies["test"] = new RivalFamily { Hostility = 60 };
        testCases.Add(new AgentDecisionContext
        {
            GameState = loyalState,
            AgentId = "loyal-agent",
            Aggression = 30,
            Greed = 40,       // IsFamilyFirst requires < 50
            Loyalty = 95,     // IsFamilyFirst requires > 90
            Ambition = 50
        });

        // Cautious agent (low aggression, high loyalty)
        var cautiousState = new GameState
        {
            FamilyWealth = 120000m,
            HeatLevel = 55,
            SoldierCount = 18
        };
        cautiousState.RivalFamilies["test"] = new RivalFamily { Hostility = 45 };
        testCases.Add(new AgentDecisionContext
        {
            GameState = cautiousState,
            AgentId = "cautious-agent",
            Aggression = 25,  // IsCautious requires < 30
            Greed = 50,
            Loyalty = 75,     // IsCautious requires > 70
            Ambition = 50
        });

        // Wealthy with no soldiers (needs recruitment)
        var needsSoldiers = new GameState
        {
            FamilyWealth = 300000m,
            HeatLevel = 25,
            SoldierCount = 5  // Very low
        };
        needsSoldiers.RivalFamilies["test"] = new RivalFamily { Hostility = 80, Strength = 60 };
        testCases.Add(CreateTestContext(needsSoldiers, aggressive: true, greedy: false));

        // Rival attack imminent scenario
        var imminentAttack = new GameState
        {
            FamilyWealth = 200000m,
            HeatLevel = 30,
            SoldierCount = 12
        };
        imminentAttack.RivalFamilies["test"] = new RivalFamily { Hostility = 95, Strength = 80 };
        testCases.Add(CreateTestContext(imminentAttack, aggressive: true, greedy: false));

        // Perfect conditions (low heat, high wealth, weak rivals)
        var perfectConditions = new GameState
        {
            FamilyWealth = 400000m,
            HeatLevel = 10,
            PreviousHeatLevel = 15,  // Heat falling
            Reputation = 80,
            SoldierCount = 40
        };
        perfectConditions.RivalFamilies["test"] = new RivalFamily { Hostility = 20, Strength = 25 };
        testCases.Add(CreateTestContext(perfectConditions, aggressive: false, greedy: true));

        return testCases;
    }

    // =========================================================================
    // METRICS HISTORY - Track rule performance across game sessions (in-memory)
    // =========================================================================

    private static readonly List<MetricsSnapshot> _metricsHistory = new();

    /// <summary>
    /// Save current metrics snapshot to in-memory history.
    /// Call this at end of game or periodically during long sessions.
    /// </summary>
    public void SaveMetricsSnapshot(string sessionLabel = "")
    {
        var metrics = _agentRules.GetAllMetrics();
        var snapshot = new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            SessionLabel = string.IsNullOrEmpty(sessionLabel) ? $"Session_{_metricsHistory.Count + 1}" : sessionLabel,
            RuleMetrics = metrics.ToDictionary(
                kvp => kvp.Key,
                kvp => new RuleMetricsSummary
                {
                    ExecutionCount = kvp.Value.ExecutionCount,
                    TotalExecutionTimeMs = kvp.Value.TotalExecutionTime.TotalMilliseconds,
                    AverageExecutionTimeMs = kvp.Value.AverageExecutionTime.TotalMilliseconds
                })
        };
        _metricsHistory.Add(snapshot);
    }

    /// <summary>
    /// Get historical metrics summary across all saved snapshots.
    /// </summary>
    public static string GetMetricsHistory()
    {
        if (!_metricsHistory.Any())
            return "No metrics history recorded yet.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Rule Metrics History ===");
        sb.AppendLine();

        foreach (var snapshot in _metricsHistory.TakeLast(5)) // Last 5 sessions
        {
            sb.AppendLine($"📊 {snapshot.SessionLabel} ({snapshot.Timestamp:HH:mm:ss})");
            var topRules = snapshot.RuleMetrics
                .OrderByDescending(kvp => kvp.Value.ExecutionCount)
                .Take(5);
            foreach (var rule in topRules)
            {
                sb.AppendLine($"   {rule.Key}: {rule.Value.ExecutionCount}x (avg {rule.Value.AverageExecutionTimeMs:F2}ms)");
            }
            sb.AppendLine();
        }

        var totalExecutions = _metricsHistory.Sum(s => s.RuleMetrics.Values.Sum(m => m.ExecutionCount));
        sb.AppendLine($"Total sessions: {_metricsHistory.Count}");
        sb.AppendLine($"Total rule executions: {totalExecutions}");

        return sb.ToString();
    }

    /// <summary>
    /// Clear metrics history (useful for testing)
    /// </summary>
    public static void ClearMetricsHistory() => _metricsHistory.Clear();

    // =========================================================================
    // CONFIG LOADER - Load rules from simple config format (string-based)
    // =========================================================================

    /// <summary>
    /// Load rules from config string.
    /// Format:
    /// [RULE]
    /// Id=CONFIG_MY_RULE
    /// Name=My Custom Rule
    /// Priority=500
    /// Condition=PropertyName==value
    /// Action=laylow
    /// </summary>
    public int LoadRulesFromConfigString(string configContent)
    {
        var rules = RuleConfigLoader.ParseConfigString(configContent);
        return RegisterDynamicAgentRules(rules);
    }
}

// =============================================================================
// ASYNC EVENT CONTEXT - For async rule processing
// =============================================================================

/// <summary>
/// Context for async event processing rules.
/// Used for events that simulate time-delayed operations.
/// </summary>
public class AsyncEventContext
{
    /// <summary>Current game state</summary>
    public GameState GameState { get; set; } = null!;

    /// <summary>Type of event trigger (e.g., "PoliceActivity", "GatherIntel")</summary>
    public string TriggerType { get; set; } = "";

    /// <summary>
    /// Base delay in milliseconds (processed through GameTimingOptions.DelayAsync
    /// which applies the global DelayMultiplier - 0 = instant, 1 = normal speed)
    /// </summary>
    public int DelayMs { get; set; } = 100;

    /// <summary>When the event was triggered</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Result message after processing</summary>
    public string? ResultMessage { get; set; }

    /// <summary>Additional event data</summary>
    public Dictionary<string, object> EventData { get; set; } = new();
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

// =============================================================================
// METRICS SNAPSHOT - In-memory metrics history
// =============================================================================

/// <summary>
/// A snapshot of rule metrics at a point in time
/// </summary>
public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public string SessionLabel { get; set; } = "";
    public Dictionary<string, RuleMetricsSummary> RuleMetrics { get; set; } = new();
}

/// <summary>
/// Summary of metrics for a single rule
/// </summary>
public class RuleMetricsSummary
{
    public int ExecutionCount { get; set; }
    public double TotalExecutionTimeMs { get; set; }
    public double AverageExecutionTimeMs { get; set; }
}

// =============================================================================
// RULE CONFIG LOADER - Parse rules from simple text format
// =============================================================================

/// <summary>
/// Parses agent rules from a simple text-based configuration format.
/// Enables modding without JSON dependencies.
/// </summary>
public static class RuleConfigLoader
{
    /// <summary>
    /// Parse rules from a configuration string.
    /// Format:
    /// [RULE]
    /// Id=CONFIG_MY_RULE
    /// Name=My Custom Rule
    /// Priority=500
    /// Condition=PropertyName==value
    /// Action=laylow
    /// </summary>
    public static List<AgentRuleDefinition> ParseConfigString(string configContent)
    {
        var rules = new List<AgentRuleDefinition>();
        if (string.IsNullOrWhiteSpace(configContent))
            return rules;

        var lines = configContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        AgentRuleDefinition? currentRule = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Skip comments and empty lines
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                continue;

            // New rule marker
            if (line.Equals("[RULE]", StringComparison.OrdinalIgnoreCase))
            {
                if (currentRule != null && !string.IsNullOrEmpty(currentRule.Id))
                    rules.Add(currentRule);
                currentRule = new AgentRuleDefinition();
                continue;
            }

            // Skip if no current rule
            if (currentRule == null)
                continue;

            // Parse key=value pairs
            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = line.Substring(0, eqIndex).Trim().ToLowerInvariant();
            var value = line.Substring(eqIndex + 1).Trim();

            switch (key)
            {
                case "id":
                    currentRule.Id = value;
                    break;
                case "name":
                    currentRule.Name = value;
                    break;
                case "description":
                    currentRule.Description = value;
                    break;
                case "priority":
                    if (int.TryParse(value, out var priority))
                        currentRule.Priority = priority;
                    break;
                case "action":
                    currentRule.RecommendedAction = value;
                    break;
                case "condition":
                    var condition = ParseCondition(value);
                    if (condition != null)
                        currentRule.Conditions.Add(condition);
                    break;
            }
        }

        // Don't forget the last rule
        if (currentRule != null && !string.IsNullOrEmpty(currentRule.Id))
            rules.Add(currentRule);

        return rules;
    }

    /// <summary>
    /// Parse a condition string like "PropertyName==value" or "Heat>50"
    /// </summary>
    private static ConditionDefinition? ParseCondition(string conditionStr)
    {
        // Try common operators in order of specificity
        string[] operators = { "==", "!=", ">=", "<=", ">", "<", "contains" };

        foreach (var op in operators)
        {
            var opIndex = conditionStr.IndexOf(op, StringComparison.OrdinalIgnoreCase);
            if (opIndex > 0)
            {
                var propertyName = conditionStr.Substring(0, opIndex).Trim();
                var valueStr = conditionStr.Substring(opIndex + op.Length).Trim();

                // Parse the value
                object parsedValue;
                if (bool.TryParse(valueStr, out var boolVal))
                    parsedValue = boolVal;
                else if (int.TryParse(valueStr, out var intVal))
                    parsedValue = intVal;
                else if (double.TryParse(valueStr, out var doubleVal))
                    parsedValue = doubleVal;
                else
                    parsedValue = valueStr; // Keep as string

                return new ConditionDefinition
                {
                    PropertyName = propertyName,
                    Operator = op,
                    Value = parsedValue
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Get example configuration format documentation
    /// </summary>
    public static string GetConfigFormatHelp()
    {
        return @"
Rule Configuration Format
=========================

# Comments start with # or //

[RULE]
Id=CONFIG_MY_RULE
Name=My Custom Rule
Description=Optional description
Priority=500
Condition=HeatIsDangerous==true
Condition=InSurvivalMode==true
Action=laylow

[RULE]
Id=CONFIG_ANOTHER_RULE
Name=Another Rule
Priority=400
Condition=FamilyWealth>100000
Action=expand

Supported Operators: ==, !=, >, <, >=, <=, contains
Supported Value Types: true/false, integers, decimals, strings
Available Actions: collection, expand, recruit, attack, laylow, bribe, negotiate, wait
";
    }
}
