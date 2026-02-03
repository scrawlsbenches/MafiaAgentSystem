using RulesEngine.Core;
using AgentRouting.MafiaDemo.Game;

namespace AgentRouting.MafiaDemo.Rules;

// =============================================================================
// UNIFIED GAME RULES ENGINE
// =============================================================================

/// <summary>
/// Unified rules engine for all game logic - combines basic game rules,
/// agent decisions, events, territory valuation, difficulty, rival AI, and chain reactions.
///
/// This is a partial class split across multiple files for maintainability:
/// - GameRulesEngine.cs (this file) - Core fields, constructor, and public API
/// - GameRulesEngine.Setup.cs - Rule setup methods
/// - GameRulesEngine.Analysis.cs - Analysis, debugging, and validation methods
/// </summary>
public partial class GameRulesEngine
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

        // Setup all rules (implemented in GameRulesEngine.Setup.cs)
        SetupGameRules();
        SetupAgentRules();
        SetupEventRules();
        SetupValuationRules();
        SetupDifficultyRules();
        SetupStrategyRules();
        SetupChainReactionRules();
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
        sb.AppendLine("Agent Rule Performance Metrics:");
        sb.AppendLine($"   Total rule evaluations: {totalExecutions}");
        sb.AppendLine($"   Average execution time: {avgTime:F2}us");
        sb.AppendLine("   Top 5 triggered rules:");

        foreach (var rule in topRules)
        {
            sb.AppendLine($"     - {rule.RuleId}: {rule.ExecutionCount}x (avg {rule.AverageExecutionTime.TotalMicroseconds:F1}us)");
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
