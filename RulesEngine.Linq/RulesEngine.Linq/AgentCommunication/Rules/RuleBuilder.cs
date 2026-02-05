using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Static factory for creating rules with fluent syntax.
/// </summary>
public static class Rule
{
    private static int _autoId;

    /// <summary>
    /// Start building a rule with a simple condition.
    /// </summary>
    public static RuleBuilder<T> When<T>(Expression<Func<T, bool>> condition) where T : class
        => new RuleBuilder<T>().When(condition);

    /// <summary>
    /// Start building a rule with a context-aware condition.
    /// Context is captured from the enclosing scope for clean syntax.
    /// </summary>
    public static RuleBuilder<T> When<T>(
        Expression<Func<T, IAgentRulesContext, bool>> condition) where T : class
        => new RuleBuilder<T>().When(condition);

    /// <summary>
    /// Create a validation rule for pipelines.
    /// </summary>
    public static PipelineStage<T> Validate<T>(
        Expression<Func<T, bool>> condition,
        string errorMessage) where T : class
        => new ValidationStage<T>(condition, errorMessage);

    /// <summary>
    /// Create a transformation rule for pipelines.
    /// </summary>
    public static PipelineStage<T> Transform<T>(
        Action<T> transformation) where T : class
        => new TransformStage<T>(transformation);

    /// <summary>
    /// Create a routing rule for pipelines.
    /// </summary>
    public static PipelineStage<T> Route<T>(
        Func<T, IAgentRulesContext, Mafia.Domain.Agent?> resolver) where T : class
        => new RouteStage<T>(resolver);

    /// <summary>
    /// Create a logging rule for pipelines.
    /// </summary>
    public static PipelineStage<T> Log<T>(
        Func<T, string> formatter) where T : class
        => new LogStage<T>(formatter);

    internal static string NextId() => $"rule-{Interlocked.Increment(ref _autoId)}";
}

/// <summary>
/// Fluent builder for constructing rules.
/// </summary>
public class RuleBuilder<T> where T : class
{
    private string _id = Rule.NextId();
    private string _name = string.Empty;
    private int _priority;
    private readonly HashSet<string> _tags = new();
    private string? _reason;

    private readonly List<Expression> _conditions = new();
    private readonly List<Expression> _contextConditions = new();
    private Action<T>? _simpleAction;
    private Action<T, IAgentRulesContext>? _contextAction;
    private readonly HashSet<Type> _explicitDependencies = new();

    public RuleBuilder<T> WithId(string id)
    {
        _id = id;
        return this;
    }

    public RuleBuilder<T> WithName(string name)
    {
        _name = name;
        return this;
    }

    public RuleBuilder<T> WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    public RuleBuilder<T> WithTags(params string[] tags)
    {
        foreach (var tag in tags) _tags.Add(tag);
        return this;
    }

    public RuleBuilder<T> WithReason(string reason)
    {
        _reason = reason;
        return this;
    }

    public RuleBuilder<T> When(Expression<Func<T, bool>> condition)
    {
        _conditions.Add(condition);
        return this;
    }

    public RuleBuilder<T> When(Expression<Func<T, IAgentRulesContext, bool>> condition)
    {
        _contextConditions.Add(condition);
        return this;
    }

    public RuleBuilder<T> And(Expression<Func<T, bool>> condition)
        => When(condition);

    public RuleBuilder<T> And(Expression<Func<T, IAgentRulesContext, bool>> condition)
        => When(condition);

    public RuleBuilder<T> DependsOn<TFact>() where TFact : class
    {
        _explicitDependencies.Add(typeof(TFact));
        return this;
    }

    public RuleBuilder<T> Then(Action<T> action)
    {
        _simpleAction = action;
        return this;
    }

    public RuleBuilder<T> Then(Action<T, IAgentRulesContext> action)
    {
        _contextAction = action;
        return this;
    }

    public IAgentRule<T> Build()
    {
        if (string.IsNullOrEmpty(_name))
            _name = _id;

        return new AgentRule<T>(
            _id,
            _name,
            _priority,
            _tags,
            _reason,
            _conditions,
            _contextConditions,
            _simpleAction,
            _contextAction,
            _explicitDependencies);
    }
}
