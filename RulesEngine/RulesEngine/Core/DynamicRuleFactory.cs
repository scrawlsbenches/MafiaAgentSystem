using System.Linq.Expressions;
using System.Reflection;

namespace RulesEngine.Core;

/// <summary>
/// Factory for creating rules dynamically at runtime
/// </summary>
public static class DynamicRuleFactory
{
    /// <summary>
    /// Creates a rule from a property comparison
    /// Example: CreatePropertyRule("Age", ">", 18)
    /// </summary>
    public static Rule<T> CreatePropertyRule<T>(
        string ruleId,
        string ruleName,
        string propertyName,
        string @operator,
        object value,
        int priority = 0)
    {
        var condition = BuildPropertyCondition<T>(propertyName, @operator, value);
        return new Rule<T>(ruleId, ruleName, condition, priority: priority);
    }
    
    /// <summary>
    /// Builds an expression for a property comparison
    /// </summary>
    public static Expression<Func<T, bool>> BuildPropertyCondition<T>(
        string propertyName,
        string @operator,
        object value)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyName);
        var constant = Expression.Constant(value);
        
        Expression comparison = @operator switch
        {
            "==" or "equals" => Expression.Equal(property, constant),
            "!=" or "notequals" => Expression.NotEqual(property, constant),
            ">" or "greaterthan" => Expression.GreaterThan(property, constant),
            "<" or "lessthan" => Expression.LessThan(property, constant),
            ">=" or "greaterthanorequal" => Expression.GreaterThanOrEqual(property, constant),
            "<=" or "lessthanorequal" => Expression.LessThanOrEqual(property, constant),
            "contains" => BuildContainsExpression(property, constant),
            "startswith" => BuildStartsWithExpression(property, constant),
            "endswith" => BuildEndsWithExpression(property, constant),
            _ => throw new ArgumentException($"Unsupported operator: {@operator}")
        };
        
        return Expression.Lambda<Func<T, bool>>(comparison, parameter);
    }
    
    /// <summary>
    /// Creates a rule from multiple conditions combined with AND
    /// </summary>
    public static Rule<T> CreateMultiConditionRule<T>(
        string ruleId,
        string ruleName,
        params (string propertyName, string @operator, object value)[] conditions)
    {
        if (conditions.Length == 0)
        {
            throw new ArgumentException("At least one condition is required");
        }
        
        var builder = new RuleBuilder<T>()
            .WithId(ruleId)
            .WithName(ruleName);
        
        foreach (var (propertyName, @operator, value) in conditions)
        {
            var condition = BuildPropertyCondition<T>(propertyName, @operator, value);
            builder.And(condition);
        }
        
        return builder.Build();
    }
    
    /// <summary>
    /// Creates rules from a configuration-like structure
    /// </summary>
    public static List<Rule<T>> CreateRulesFromDefinitions<T>(
        IEnumerable<RuleDefinition> definitions)
    {
        var rules = new List<Rule<T>>();
        
        foreach (var def in definitions)
        {
            var builder = new RuleBuilder<T>()
                .WithId(def.Id)
                .WithName(def.Name)
                .WithDescription(def.Description)
                .WithPriority(def.Priority);
            
            foreach (var condition in def.Conditions)
            {
                var expr = BuildPropertyCondition<T>(
                    condition.PropertyName,
                    condition.Operator,
                    condition.Value);
                
                if (condition.LogicalOperator == "OR")
                {
                    builder.Or(expr);
                }
                else
                {
                    builder.And(expr);
                }
            }
            
            rules.Add(builder.Build());
        }
        
        return rules;
    }
    
    private static Expression BuildContainsExpression(Expression property, Expression constant)
    {
        var containsMethod = typeof(string).GetMethod(
            nameof(string.Contains), 
            new[] { typeof(string) })!;
        
        return Expression.Call(property, containsMethod, constant);
    }
    
    private static Expression BuildStartsWithExpression(Expression property, Expression constant)
    {
        var startsWithMethod = typeof(string).GetMethod(
            nameof(string.StartsWith), 
            new[] { typeof(string) })!;
        
        return Expression.Call(property, startsWithMethod, constant);
    }
    
    private static Expression BuildEndsWithExpression(Expression property, Expression constant)
    {
        var endsWithMethod = typeof(string).GetMethod(
            nameof(string.EndsWith), 
            new[] { typeof(string) })!;
        
        return Expression.Call(property, endsWithMethod, constant);
    }
}

/// <summary>
/// Definition for creating a rule from configuration
/// </summary>
public class RuleDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Priority { get; set; } = 0;
    public List<ConditionDefinition> Conditions { get; set; } = new();
}

/// <summary>
/// Definition for a single condition
/// </summary>
public class ConditionDefinition
{
    public string PropertyName { get; set; } = "";
    public string Operator { get; set; } = "==";
    public object Value { get; set; } = new object();
    public string LogicalOperator { get; set; } = "AND"; // AND or OR
}
