using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Concrete rule implementation with dependency analysis.
/// </summary>
internal class AgentRule<T> : IAgentRule<T> where T : class
{
    private readonly List<Expression> _simpleConditions;
    private readonly List<Expression> _contextConditions;
    private readonly Action<T>? _simpleAction;
    private readonly Action<T, IAgentRulesContext>? _contextAction;
    private readonly HashSet<Type> _explicitDependencies;

    // Compiled delegates (lazy)
    private List<Func<T, bool>>? _compiledSimple;
    private List<Func<T, IAgentRulesContext, bool>>? _compiledContext;
    private HashSet<Type>? _detectedDependencies;

    public AgentRule(
        string id,
        string name,
        int priority,
        HashSet<string> tags,
        string? reason,
        List<Expression> simpleConditions,
        List<Expression> contextConditions,
        Action<T>? simpleAction,
        Action<T, IAgentRulesContext>? contextAction,
        HashSet<Type> explicitDependencies)
    {
        Id = id;
        Name = name;
        Priority = priority;
        Tags = tags;
        Reason = reason;
        _simpleConditions = simpleConditions;
        _contextConditions = contextConditions;
        _simpleAction = simpleAction;
        _contextAction = contextAction;
        _explicitDependencies = explicitDependencies;
    }

    public string Id { get; }
    public string Name { get; }
    public int Priority { get; }
    public IReadOnlySet<string> Tags { get; }
    public string? Reason { get; }

    public IReadOnlySet<Type> Dependencies
    {
        get
        {
            // Combine explicit + detected
            var all = new HashSet<Type>(_explicitDependencies);
            if (_detectedDependencies != null)
            {
                foreach (var dep in _detectedDependencies)
                    all.Add(dep);
            }
            return all;
        }
    }

    public bool Evaluate(T fact, IAgentRulesContext context)
    {
        EnsureCompiled();

        // Check all simple conditions
        foreach (var condition in _compiledSimple!)
        {
            if (!condition(fact))
                return false;
        }

        // Check all context conditions
        foreach (var condition in _compiledContext!)
        {
            if (!condition(fact, context))
                return false;
        }

        return true;
    }

    public RuleActionResult Execute(T fact, IAgentRulesContext context)
    {
        if (_contextAction != null)
        {
            _contextAction(fact, context);
        }
        else if (_simpleAction != null)
        {
            _simpleAction(fact);
        }

        return RuleActionResult.Success();
    }

    private void EnsureCompiled()
    {
        if (_compiledSimple != null) return;

        _compiledSimple = _simpleConditions
            .Cast<Expression<Func<T, bool>>>()
            .Select(e => e.Compile())
            .ToList();

        _compiledContext = _contextConditions
            .Cast<Expression<Func<T, IAgentRulesContext, bool>>>()
            .Select(e => e.Compile())
            .ToList();

        // TODO: Use DependencyExtractor to analyze expressions
        // and populate _detectedDependencies
    }
}
