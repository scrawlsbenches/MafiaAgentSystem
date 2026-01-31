using System.Linq.Expressions;

namespace RulesEngine.Core;

/// <summary>
/// Fluent API for building rules with expression trees
/// </summary>
public class RuleBuilder<T>
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Unnamed Rule";
    private string _description = "";
    private int _priority = 0;
    private Expression<Func<T, bool>>? _condition;
    private readonly List<Action<T>> _actions = new();
    
    /// <summary>
    /// Sets the rule ID
    /// </summary>
    public RuleBuilder<T> WithId(string id)
    {
        _id = id;
        return this;
    }
    
    /// <summary>
    /// Sets the rule name
    /// </summary>
    public RuleBuilder<T> WithName(string name)
    {
        _name = name;
        return this;
    }
    
    /// <summary>
    /// Sets the rule description
    /// </summary>
    public RuleBuilder<T> WithDescription(string description)
    {
        _description = description;
        return this;
    }
    
    /// <summary>
    /// Sets the rule priority (higher = executes earlier)
    /// </summary>
    public RuleBuilder<T> WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }
    
    /// <summary>
    /// Sets the condition using an expression
    /// </summary>
    public RuleBuilder<T> When(Expression<Func<T, bool>> condition)
    {
        _condition = condition;
        return this;
    }
    
    /// <summary>
    /// Adds an AND condition to the existing condition
    /// </summary>
    public RuleBuilder<T> And(Expression<Func<T, bool>> condition)
    {
        if (_condition == null)
        {
            _condition = condition;
        }
        else
        {
            _condition = CombineWithAnd(_condition, condition);
        }
        return this;
    }
    
    /// <summary>
    /// Adds an OR condition to the existing condition
    /// </summary>
    public RuleBuilder<T> Or(Expression<Func<T, bool>> condition)
    {
        if (_condition == null)
        {
            _condition = condition;
        }
        else
        {
            _condition = CombineWithOr(_condition, condition);
        }
        return this;
    }
    
    /// <summary>
    /// Adds an action to execute when the rule matches
    /// </summary>
    public RuleBuilder<T> Then(Action<T> action)
    {
        _actions.Add(action);
        return this;
    }
    
    /// <summary>
    /// Builds the rule
    /// </summary>
    public Rule<T> Build()
    {
        if (_condition == null)
        {
            throw new InvalidOperationException("Rule must have a condition");
        }
        
        var rule = new Rule<T>(_id, _name, _condition, _description, _priority);
        
        foreach (var action in _actions)
        {
            rule.WithAction(action);
        }
        
        return rule;
    }
    
    private static Expression<Func<T, bool>> CombineWithAnd(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        
        var andExpression = Expression.AndAlso(leftBody, rightBody);
        
        return Expression.Lambda<Func<T, bool>>(andExpression, parameter);
    }
    
    private static Expression<Func<T, bool>> CombineWithOr(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        
        var orExpression = Expression.OrElse(leftBody, rightBody);
        
        return Expression.Lambda<Func<T, bool>>(orExpression, parameter);
    }
    
    private static Expression ReplaceParameter(
        Expression expression,
        ParameterExpression oldParameter,
        ParameterExpression newParameter)
    {
        return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
    }
    
    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;
        
        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }
        
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }
    }
}

/// <summary>
/// Builder for composite rules
/// </summary>
public class CompositeRuleBuilder<T>
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Unnamed Composite Rule";
    private string _description = "";
    private int _priority = 0;
    private CompositeOperator _operator = CompositeOperator.And;
    private readonly List<IRule<T>> _rules = new();
    
    public CompositeRuleBuilder<T> WithId(string id)
    {
        _id = id;
        return this;
    }
    
    public CompositeRuleBuilder<T> WithName(string name)
    {
        _name = name;
        return this;
    }
    
    public CompositeRuleBuilder<T> WithDescription(string description)
    {
        _description = description;
        return this;
    }
    
    public CompositeRuleBuilder<T> WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }
    
    public CompositeRuleBuilder<T> WithOperator(CompositeOperator op)
    {
        _operator = op;
        return this;
    }
    
    public CompositeRuleBuilder<T> AddRule(IRule<T> rule)
    {
        _rules.Add(rule);
        return this;
    }
    
    public CompositeRuleBuilder<T> AddRules(params IRule<T>[] rules)
    {
        _rules.AddRange(rules);
        return this;
    }
    
    public CompositeRule<T> Build()
    {
        if (!_rules.Any())
        {
            throw new InvalidOperationException("Composite rule must have at least one child rule");
        }
        
        return new CompositeRule<T>(_id, _name, _operator, _rules, _description, _priority);
    }
}
