using RulesEngine.Core;
using AgentRouting.MafiaDemo.Game;
using System.Linq.Expressions;

namespace AgentRouting.MafiaDemo.Rules;

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
/// Demonstrates advanced rules engine usage
/// </summary>
public class AdvancedRulesEngine
{
    private readonly RulesEngineCore<TerritoryValueContext> _valuationEngine;
    private readonly RulesEngineCore<DifficultyContext> _difficultyEngine;
    private readonly RulesEngineCore<RivalStrategyContext> _strategyEngine;
    private readonly RulesEngineCore<ChainReactionContext> _chainEngine;
    
    public AdvancedRulesEngine()
    {
        _valuationEngine = new RulesEngineCore<TerritoryValueContext>();
        _difficultyEngine = new RulesEngineCore<DifficultyContext>();
        _strategyEngine = new RulesEngineCore<RivalStrategyContext>();
        _chainEngine = new RulesEngineCore<ChainReactionContext>();
        
        SetupValuationRules();
        SetupDifficultyRules();
        SetupStrategyRules();
        SetupChainReactionRules();
    }
    
    /// <summary>
    /// Territory valuation rules - dynamic pricing based on conditions
    /// </summary>
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
    
    /// <summary>
    /// Dynamic difficulty adjustment rules
    /// </summary>
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
    
    /// <summary>
    /// Strategic AI for rival families
    /// </summary>
    private void SetupStrategyRules()
    {
        // Opportunistic attack
        _strategyEngine.AddRule(
            "STRATEGY_ATTACK_WEAK",
            "Attack Weak Player",
            ctx => ctx.ShouldAttack,
            ctx => {
                var damage = new Random().Next(10000, 25000);
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
    
    /// <summary>
    /// Chain reaction rules - events trigger cascading effects
    /// </summary>
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
}
