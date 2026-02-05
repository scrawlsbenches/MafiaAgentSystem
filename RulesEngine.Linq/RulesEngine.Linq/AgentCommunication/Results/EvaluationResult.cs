using System;
using System.Collections.Generic;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Aggregate evaluation results across all fact types.
/// </summary>
public interface IEvaluationResult
{
    Guid SessionId { get; }
    TimeSpan Duration { get; }
    int TotalFactsEvaluated { get; }
    int TotalRulesEvaluated { get; }
    int TotalMatches { get; }
    bool HasErrors { get; }

    IEvaluationResult<T> ForType<T>() where T : class;
}

/// <summary>
/// Evaluation results for a specific fact type.
/// </summary>
public interface IEvaluationResult<T> where T : class
{
    IReadOnlyList<FactRuleMatch<T>> Matches { get; }
    IReadOnlyList<T> FactsWithMatches { get; }
    IReadOnlyList<T> FactsWithoutMatches { get; }
}

/// <summary>
/// A fact and the rules that matched it.
/// </summary>
public class FactRuleMatch<T> where T : class
{
    public T Fact { get; init; } = default!;
    public IReadOnlyList<IAgentRule<T>> Rules { get; init; } = Array.Empty<IAgentRule<T>>();
}
