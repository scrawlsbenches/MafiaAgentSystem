using System.Linq.Expressions;

namespace RulesEngine.Core;

/// <summary>
/// Represents a single rule that can be evaluated against a fact
/// </summary>
/// <typeparam name="T">The type of fact the rule evaluates</typeparam>
public interface IRule<T>
{
    /// <summary>
    /// Unique identifier for the rule
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Human-readable name of the rule
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what the rule does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Priority for rule execution (higher = earlier)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Evaluates whether the rule matches the given fact
    /// </summary>
    bool Evaluate(T fact);
    
    /// <summary>
    /// Executes the rule's actions if it matches
    /// </summary>
    RuleResult Execute(T fact);
}

/// <summary>
/// Result of a rule execution
/// </summary>
public class RuleResult : IRuleResult
{
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public bool Matched { get; set; }
    public bool ActionExecuted { get; set; }
    public DateTime ExecutedAt { get; set; }
    public Dictionary<string, object> Outputs { get; set; } = new();
    public string? ErrorMessage { get; set; }

    // Explicit interface implementation for IReadOnlyDictionary
    IReadOnlyDictionary<string, object> IRuleResult.Outputs => Outputs;
    
    public static RuleResult Success(string ruleId, string ruleName, Dictionary<string, object>? outputs = null)
    {
        return new RuleResult
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Matched = true,
            ActionExecuted = true,
            ExecutedAt = DateTime.UtcNow,
            Outputs = outputs ?? new()
        };
    }
    
    public static RuleResult NotMatched(string ruleId, string ruleName)
    {
        return new RuleResult
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Matched = false,
            ActionExecuted = false,
            ExecutedAt = DateTime.UtcNow
        };
    }
    
    public static RuleResult Error(string ruleId, string ruleName, string error)
    {
        return new RuleResult
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Matched = false,
            ActionExecuted = false,
            ExecutedAt = DateTime.UtcNow,
            ErrorMessage = error
        };
    }

    /// <summary>
    /// Creates a failure result with just an error message (rule ID provided separately)
    /// </summary>
    public static RuleResult Failure(string ruleId, string errorMessage)
    {
        return new RuleResult
        {
            RuleId = ruleId,
            RuleName = string.Empty,
            Matched = false,
            ActionExecuted = false,
            ExecutedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// A rule that uses expression trees for evaluation
/// </summary>
public class Rule<T> : IRule<T>
{
    private readonly Func<T, bool> _compiledCondition;
    private readonly List<Action<T>> _actions;
    
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int Priority { get; }
    
    /// <summary>
    /// The condition expression (for inspection/debugging)
    /// </summary>
    public Expression<Func<T, bool>> Condition { get; }
    
    public Rule(
        string id,
        string name,
        Expression<Func<T, bool>> condition,
        string description = "",
        int priority = 0)
    {
        Id = id;
        Name = name;
        Description = description;
        Priority = priority;
        Condition = condition;
        _compiledCondition = condition.Compile();
        _actions = new List<Action<T>>();
    }
    
    /// <summary>
    /// Adds an action to execute when the rule matches
    /// </summary>
    public Rule<T> WithAction(Action<T> action)
    {
        _actions.Add(action);
        return this;
    }
    
    public bool Evaluate(T fact)
    {
        try
        {
            return _compiledCondition(fact);
        }
        catch
        {
            return false;
        }
    }
    
    public RuleResult Execute(T fact)
    {
        // Track whether condition matched before attempting actions
        bool conditionMatched;
        try
        {
            conditionMatched = Evaluate(fact);
            if (!conditionMatched)
            {
                return RuleResult.NotMatched(Id, Name);
            }
        }
        catch (Exception ex)
        {
            // Condition evaluation threw - rule did not match
            return RuleResult.Error(Id, Name, ex.Message);
        }

        // Condition matched - now try to execute actions
        try
        {
            foreach (var action in _actions)
            {
                action(fact);
            }

            return RuleResult.Success(Id, Name);
        }
        catch (Exception ex)
        {
            // Action threw but condition DID match - preserve Matched=true
            // This is consistent with ActionRule behavior
            return new RuleResult
            {
                RuleId = Id,
                RuleName = Name,
                Matched = true,
                ActionExecuted = false,
                ExecutedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public override string ToString()
    {
        return $"Rule '{Name}': {Condition}";
    }
}

/// <summary>
/// A composite rule that combines multiple rules with logical operators
/// </summary>
public class CompositeRule<T> : IRule<T>
{
    private readonly List<IRule<T>> _rules;
    private readonly CompositeOperator _operator;
    
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int Priority { get; }
    
    public IReadOnlyList<IRule<T>> Rules => _rules.AsReadOnly();
    
    public CompositeRule(
        string id,
        string name,
        CompositeOperator op,
        IEnumerable<IRule<T>> rules,
        string description = "",
        int priority = 0)
    {
        Id = id;
        Name = name;
        Description = description;
        Priority = priority;
        _operator = op;
        _rules = rules.ToList();
    }
    
    public bool Evaluate(T fact)
    {
        if (!_rules.Any()) return false;
        
        return _operator switch
        {
            CompositeOperator.And => _rules.All(r => r.Evaluate(fact)),
            CompositeOperator.Or => _rules.Any(r => r.Evaluate(fact)),
            CompositeOperator.Not => !_rules.First().Evaluate(fact),
            _ => false
        };
    }
    
    public RuleResult Execute(T fact)
    {
        try
        {
            if (!Evaluate(fact))
            {
                return RuleResult.NotMatched(Id, Name);
            }
            
            // Execute all matching child rules
            var results = new List<RuleResult>();
            foreach (var rule in _rules)
            {
                if (rule.Evaluate(fact))
                {
                    results.Add(rule.Execute(fact));
                }
            }
            
            var outputs = new Dictionary<string, object>
            {
                ["ChildResults"] = results
            };
            
            return RuleResult.Success(Id, Name, outputs);
        }
        catch (Exception ex)
        {
            return RuleResult.Error(Id, Name, ex.Message);
        }
    }
}

/// <summary>
/// Logical operators for composite rules
/// </summary>
public enum CompositeOperator
{
    And,
    Or,
    Not
}
