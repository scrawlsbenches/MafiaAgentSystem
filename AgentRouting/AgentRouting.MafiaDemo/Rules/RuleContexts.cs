using AgentRouting.MafiaDemo.Game;

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

    /// <summary>A rival is weak (strength below 40). Returns false if no rivals.</summary>
    public bool RivalIsWeak => GameState.HasWeakRival(40);

    /// <summary>A rival is threatening (high hostility and strong). Returns false if no rivals.</summary>
    public bool RivalIsThreatening => GameState.HasRivals &&
                                      GameState.MaxRivalHostility > 70 &&
                                      GameState.MostHostileRival!.Strength > 60;

    /// <summary>A rival is about to attack (very high hostility). Returns false if no rivals.</summary>
    public bool RivalAttackImminent => GameState.MaxRivalHostility > 85;

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
