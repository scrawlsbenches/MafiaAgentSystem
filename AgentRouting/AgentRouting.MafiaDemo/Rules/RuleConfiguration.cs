using RulesEngine.Core;

namespace AgentRouting.MafiaDemo.Rules;

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
