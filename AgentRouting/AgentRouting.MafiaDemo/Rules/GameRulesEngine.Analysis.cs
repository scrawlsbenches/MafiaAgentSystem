using RulesEngine.Core;
using RulesEngine.Enhanced;
using AgentRouting.MafiaDemo.Game;

namespace AgentRouting.MafiaDemo.Rules;

/// <summary>
/// Partial class containing analysis, debugging, validation, and test scenario methods.
/// Separated for maintainability - these methods help understand rule behavior.
/// </summary>
public partial class GameRulesEngine
{
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
            var symbol = matches ? "[Y]" : "[N]";
            trace.Add($"  {symbol} {rule.Name} (P:{rule.Priority}) - {(matches ? "MATCHED" : "no match")}");

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
            sb.AppendLine("[OK] Basic validation passed");
        }

        // Rule analysis with test scenarios
        var testCases = GenerateAnalysisTestCases();
        var report = AnalyzeAgentRules(testCases);

        // Check for dead rules (never matched any test case)
        var deadRules = report.RuleAnalyses.Where(a => a.MatchedCount == 0).ToList();
        if (deadRules.Any())
        {
            sb.AppendLine();
            sb.AppendLine($"Potential Dead Rules ({deadRules.Count} rules never matched):");
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
            sb.AppendLine("Rules with overlaps (may cause priority conflicts):");
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
            sb.AppendLine($"{snapshot.SessionLabel} ({snapshot.Timestamp:HH:mm:ss})");
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
}
