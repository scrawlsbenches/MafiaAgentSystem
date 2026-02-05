using System;
using System.Collections.Generic;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Rule that can participate in cross-fact queries.
/// </summary>
public interface IAgentRule<T> where T : class
{
    string Id { get; }
    string Name { get; }
    int Priority { get; }
    IReadOnlySet<string> Tags { get; }
    string? Reason { get; }

    /// <summary>
    /// Fact types this rule depends on (detected + explicit).
    /// </summary>
    IReadOnlySet<Type> Dependencies { get; }

    /// <summary>
    /// Evaluate condition with context access.
    /// </summary>
    bool Evaluate(T fact, IAgentRulesContext context);

    /// <summary>
    /// Execute action with context access.
    /// </summary>
    RuleActionResult Execute(T fact, IAgentRulesContext context);
}
