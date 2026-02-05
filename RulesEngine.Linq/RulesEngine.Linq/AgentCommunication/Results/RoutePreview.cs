using System;
using System.Collections.Generic;
using Mafia.Domain;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Preview of routing decisions.
/// </summary>
public class RoutePreview<T> where T : class
{
    public T Fact { get; init; } = default!;
    public IReadOnlyList<IAgentRule<T>> MatchedRules { get; init; } = Array.Empty<IAgentRule<T>>();
    public Agent? TargetAgent { get; init; }
    public bool WouldBeBlocked { get; init; }
    public string? BlockReason { get; init; }
    public IReadOnlyList<string> AppliedFlags { get; init; } = Array.Empty<string>();
}
