using RulesEngine.Core;
using AgentRouting.MafiaDemo.Game;

namespace AgentRouting.MafiaDemo.Rules;

/// <summary>
/// Partial class containing all rule setup methods for GameRulesEngine.
/// Separated for maintainability - these methods register rules with the engines.
/// </summary>
public partial class GameRulesEngine
{
    // =========================================================================
    // GAME RULES - Victory, defeat, warnings, consequences
    // =========================================================================

    private void SetupGameRules()
    {
        // Victory conditions (using GameState constants for single source of truth)
        _gameRules.AddRule(
            "VICTORY_EMPIRE",
            "Empire Victory",
            ctx => ctx.Week >= GameState.VictoryWeekThreshold && ctx.IsRichFinancially && ctx.HasHighReputation,
            ctx => {
                ctx.State.GameOver = true;
                ctx.State.GameOverReason = "VICTORY! You've built an empire that will last generations!";
            },
            priority: 1000
        );

        _gameRules.AddRule(
            "VICTORY_SURVIVAL",
            "Survival Victory",
            ctx => ctx.Week >= GameState.VictoryWeekThreshold && ctx.Wealth > 150000,
            ctx => {
                ctx.State.GameOver = true;
                ctx.State.GameOverReason = "Victory! The family survived and prospered!";
            },
            priority: 900
        );

        // Defeat conditions (using GameState constants for single source of truth)
        _gameRules.AddRule(
            "DEFEAT_BANKRUPTCY",
            "Bankruptcy",
            ctx => ctx.Wealth <= 0,
            ctx => {
                ctx.State.GameOver = true;
                ctx.State.GameOverReason = "DEFEAT: The family went bankrupt. Absorbed by rivals.";
            },
            priority: 1000
        );

        _gameRules.AddRule(
            "DEFEAT_FEDERAL",
            "Federal Crackdown",
            ctx => ctx.Heat >= GameState.DefeatHeatThreshold,
            ctx => {
                ctx.State.GameOver = true;
                ctx.State.GameOverReason = "DEFEAT: Federal crackdown! Everyone's going to prison!";
            },
            priority: 1000
        );

        _gameRules.AddRule(
            "DEFEAT_BETRAYAL",
            "Internal Betrayal",
            ctx => ctx.Reputation <= GameState.DefeatReputationThreshold,
            ctx => {
                ctx.State.GameOver = true;
                ctx.State.GameOverReason = "DEFEAT: Betrayed from within. The family is destroyed.";
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
                Console.WriteLine("WARNING: Federal attention is CRITICAL! Lay low immediately!");
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
                Console.WriteLine("WARNING: Family funds are critically low!");
                Console.ResetColor();
            },
            priority: 800
        );

        // Automatic consequences
        _gameRules.AddRule(
            "CONSEQUENCE_VULNERABLE",
            "Vulnerable Position",
            ctx => ctx.IsVulnerable && ctx.State.Territories.Any() && !ctx.State.Territories.Values.Any(t => t.UnderDispute),
            ctx => {
                var territory = ctx.State.Territories.Values.FirstOrDefault();
                if (territory == null) return;  // Safety check
                territory.UnderDispute = true;
                Console.WriteLine($"Rivals sense weakness! {territory.Name} is under dispute!");
            },
            priority: 700
        );

        _gameRules.AddRule(
            "CONSEQUENCE_DOMINANT",
            "Dominant Position",
            ctx => ctx.IsDominant && ctx.Reputation < 100,
            ctx => {
                ctx.State.Reputation += 5;
                Console.WriteLine("The family's dominance is recognized. Reputation +5");
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
                Console.WriteLine("OPPORTUNITY: New territory available for expansion!");
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
        // ADDITIONAL PERSONALITY RULES - Expanded personality coverage
        // =====================================================================

        // Cautious agents avoid risks when heat is building
        _agentRules.AddRule(
            "CAUTIOUS_AVOID_RISK",
            "Cautious Risk Avoidance",
            ctx => ctx.IsCautious && ctx.HeatIsRising && !ctx.InSurvivalMode,
            ctx => { ctx.RecommendedAction = "laylow"; },
            priority: 780
        );

        // Cautious agents prefer safe collections only when situation is stable
        _agentRules.AddRule(
            "CAUTIOUS_SAFE_COLLECT",
            "Cautious Collection",
            ctx => ctx.IsCautious && ctx.HeatIsFalling && ctx.RivalsArePeaceful,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 720
        );

        // Cautious agents recruit only when absolutely necessary
        var cautiousRecruit = new RuleBuilder<AgentDecisionContext>()
            .WithId("CAUTIOUS_RECRUIT")
            .WithName("Cautious Recruitment")
            .WithPriority(710)
            .When(ctx => ctx.IsCautious)
            .And(ctx => ctx.GameState.SoldierCount < 5)
            .And(ctx => ctx.RivalIsThreatening)
            .And(ctx => ctx.GameState.FamilyWealth > 100000)
            .Then(ctx => ctx.RecommendedAction = "recruit")
            .Build();
        _agentRules.RegisterRule(cautiousRecruit);

        // Family-first agents always prioritize stability
        _agentRules.AddRule(
            "FAMILY_FIRST_STABILITY",
            "Family First Stability",
            ctx => ctx.IsFamilyFirst && ctx.HeatIsRising,
            ctx => { ctx.RecommendedAction = "bribe"; },
            priority: 840
        );

        // Family-first agents recruit when family is weak
        _agentRules.AddRule(
            "FAMILY_FIRST_STRENGTHEN",
            "Family First Strengthening",
            ctx => ctx.IsFamilyFirst && ctx.NeedsMoreSoldiers && ctx.GameState.FamilyWealth > 30000,
            ctx => { ctx.RecommendedAction = "recruit"; },
            priority: 820
        );

        // Hot-headed agents take unnecessary risks
        _agentRules.AddRule(
            "HOTHEADED_RECKLESS",
            "Hot-headed Recklessness",
            ctx => ctx.IsHotHeaded && !ctx.HeatIsCritical && ctx.GameState.FamilyWealth > 50000,
            ctx => { ctx.RecommendedAction = "intimidate"; },
            priority: 680
        );

        // Hot-headed agents refuse to back down even when they should
        _agentRules.AddRule(
            "HOTHEADED_DEFIANT",
            "Hot-headed Defiance",
            ctx => ctx.IsHotHeaded && ctx.RivalIsThreatening && !ctx.HeatIsCritical,
            ctx => { ctx.RecommendedAction = "intimidate"; },
            priority: 760
        );

        // =====================================================================
        // PHASE-PERSONALITY HYBRID RULES - Personality modifies phase behavior
        // =====================================================================

        // Survival mode + aggressive personality = fight to survive
        _agentRules.AddRule(
            "SURVIVAL_AGGRESSIVE",
            "Aggressive Survival",
            ctx => ctx.InSurvivalMode && ctx.IsAggressive && ctx.RivalIsWeak,
            ctx => { ctx.RecommendedAction = "intimidate"; },
            priority: 970
        );

        // Survival mode + cautious personality = extra careful
        _agentRules.AddRule(
            "SURVIVAL_CAUTIOUS",
            "Cautious Survival",
            ctx => ctx.InSurvivalMode && ctx.IsCautious,
            ctx => { ctx.RecommendedAction = "laylow"; },
            priority: 975
        );

        // Growth mode + cautious personality = measured expansion
        var growthCautious = new RuleBuilder<AgentDecisionContext>()
            .WithId("GROWTH_CAUTIOUS")
            .WithName("Cautious Growth")
            .WithPriority(905)
            .When(ctx => ctx.InGrowthMode)
            .And(ctx => ctx.IsCautious)
            .And(ctx => ctx.HasHeatBudget)
            .And(ctx => ctx.GameState.FamilyWealth > 200000)
            .Then(ctx => ctx.RecommendedAction = "expand")
            .Build();
        _agentRules.RegisterRule(growthCautious);

        // Dominance mode + greedy personality = maximize extraction
        _agentRules.AddRule(
            "DOMINANCE_GREEDY",
            "Greedy Dominance",
            ctx => ctx.InDominanceMode && ctx.IsGreedy && ctx.HasHeatBudget,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 915
        );

        // =====================================================================
        // RIVAL-RESPONSE RULES - React to rival family situations
        // =====================================================================

        // Weak rival + ambitious agent = seize opportunity
        _agentRules.AddRule(
            "RIVAL_WEAK_AMBITIOUS",
            "Seize Weak Rival Opportunity",
            ctx => ctx.RivalIsWeak && ctx.IsAmbitious && ctx.CanTakeRisks,
            ctx => { ctx.RecommendedAction = "expand"; },
            priority: 640
        );

        // Weak rival + aggressive agent = attack
        _agentRules.AddRule(
            "RIVAL_WEAK_AGGRESSIVE",
            "Attack Weak Rival",
            ctx => ctx.RivalIsWeak && ctx.IsAggressive && ctx.HasHeatBudget,
            ctx => { ctx.RecommendedAction = "intimidate"; },
            priority: 660
        );

        // Threatening rival + loyal agent = protect family
        _agentRules.AddRule(
            "RIVAL_THREATENING_LOYAL",
            "Loyal Defense Against Threat",
            ctx => ctx.RivalIsThreatening && ctx.IsLoyal && ctx.NeedsMoreSoldiers,
            ctx => { ctx.RecommendedAction = "recruit"; },
            priority: 830
        );

        // Threatening rival + calculating agent = strategic response
        var rivalThreateningCalculating = new RuleBuilder<AgentDecisionContext>()
            .WithId("RIVAL_THREATENING_CALCULATING")
            .WithName("Strategic Threat Response")
            .WithPriority(810)
            .When(ctx => ctx.RivalIsThreatening)
            .And(ctx => ctx.IsCalculating)
            .And(ctx => ctx.GameState.HeatLevel < 60)
            .Then(ctx => ctx.RecommendedAction = "bribe")
            .Build();
        _agentRules.RegisterRule(rivalThreateningCalculating);

        // =====================================================================
        // HEAT-MANAGEMENT RULES - Respond to heat levels
        // =====================================================================

        // Heat rising + wealthy = bribe proactively
        _agentRules.AddRule(
            "HEAT_RISING_WEALTHY",
            "Proactive Wealthy Bribe",
            ctx => ctx.HeatIsRising && ctx.GameState.FamilyWealth > 100000 && !ctx.IsAggressive,
            ctx => { ctx.RecommendedAction = "bribe"; },
            priority: 870
        );

        // Heat rising + aggressive = ignore and push forward
        _agentRules.AddRule(
            "HEAT_RISING_AGGRESSIVE",
            "Aggressive Heat Ignore",
            ctx => ctx.HeatIsRising && ctx.IsAggressive && !ctx.HeatIsDangerous,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 740
        );

        // Heat falling = safe to be productive
        _agentRules.AddRule(
            "HEAT_FALLING_PRODUCTIVE",
            "Productive Low Heat Period",
            ctx => ctx.HeatIsFalling && ctx.HasHeatBudget && !ctx.InSurvivalMode,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 580
        );

        // Heat critical + any personality = emergency measures
        _agentRules.AddRule(
            "HEAT_CRITICAL_EMERGENCY",
            "Critical Heat Emergency",
            ctx => ctx.HeatIsCritical && ctx.GameState.FamilyWealth < 20000,
            ctx => { ctx.RecommendedAction = "laylow"; },
            priority: 990
        );

        // =====================================================================
        // ECONOMIC-STRATEGY RULES - Respond to wealth trends
        // =====================================================================

        // Wealth growing + ambitious = expand aggressively
        _agentRules.AddRule(
            "WEALTH_GROWING_AMBITIOUS",
            "Ambitious Wealth Growth",
            ctx => ctx.GameState.WealthIsGrowing && ctx.IsAmbitious && ctx.CanTakeRisks,
            ctx => { ctx.RecommendedAction = "expand"; },
            priority: 630
        );

        // Wealth growing + greedy = maximize collections
        _agentRules.AddRule(
            "WEALTH_GROWING_GREEDY",
            "Greedy Wealth Maximization",
            ctx => ctx.GameState.WealthIsGrowing && ctx.IsGreedy && ctx.HasHeatBudget,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 620
        );

        // Wealth shrinking + cautious = conserve
        _agentRules.AddRule(
            "WEALTH_SHRINKING_CAUTIOUS",
            "Cautious Wealth Conservation",
            ctx => ctx.GameState.WealthIsShrinking && ctx.IsCautious,
            ctx => { ctx.RecommendedAction = "laylow"; },
            priority: 860
        );

        // Wealth shrinking + greedy = desperate collection
        _agentRules.AddRule(
            "WEALTH_SHRINKING_GREEDY",
            "Desperate Greedy Collection",
            ctx => ctx.GameState.WealthIsShrinking && ctx.IsGreedy && !ctx.HeatIsDangerous,
            ctx => { ctx.RecommendedAction = "collection"; },
            priority: 855
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
                Console.WriteLine($"  {ctx.Territory.Name} is prime real estate! Revenue +50%");
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
                Console.WriteLine($"  {ctx.Territory.Name} too hot - Revenue -30%");
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
                Console.WriteLine($"  Gambling booming at {ctx.Territory.Name}! +$5,000");
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
                Console.WriteLine($"  Smuggling routes open at {ctx.Territory.Name}! +$8,000");
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
                Console.WriteLine($"  {ctx.Territory.Name} contested - Revenue -50%");
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
                Console.WriteLine($"  Market saturated - {ctx.Territory.Name} -$2,000");
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
                Console.WriteLine($"  {ctx.Territory.Name} is GOLDEN! Revenue x2!");
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
                Console.WriteLine($"  {ctx.Territory.Name} has problems - Revenue -40%");
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
                Console.WriteLine("The other families are getting nervous about your success!");
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
                Console.WriteLine("Lucky break! An old debt has been repaid");
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
                Console.WriteLine("The balance of power remains stable");
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
                Console.WriteLine("The endgame approaches - all families are on edge");
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
                Console.WriteLine($"{ctx.Rival.Name} sees weakness and attacks!");
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
                Console.WriteLine($"{ctx.Rival.Name} seeks peace - they're weakened");
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
                Console.WriteLine($"{ctx.Rival.Name} proposes a temporary alliance");
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
                Console.WriteLine($"{ctx.Rival.Name} provokes you while you're distracted!");
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
                Console.WriteLine("  The raid has everyone paranoid about informants");
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
                var rival = ctx.State.RivalFamilies.Values.FirstOrDefault(r => r.Hostility > 80);
                if (rival == null) return;  // Safety check - no rival with hostility > 80
                rival.AtWar = true;
                rival.Hostility = 100;
                Console.WriteLine($"  The hit has started a war with {rival.Name}!");
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
                Console.WriteLine("  The betrayal triggers a leadership crisis!");
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
                Console.WriteLine("  The loss demands revenge!");
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
                Console.WriteLine("  CRISIS IS COMPOUNDING!");
                Console.WriteLine("     The family is in serious danger");
                ctx.State.HeatLevel += 15;
                ctx.State.Reputation -= 10;
            },
            priority: 1100
        );
    }
}
