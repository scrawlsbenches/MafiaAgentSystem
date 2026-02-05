namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Queryable collection of rules for a fact type.
/// </summary>
public interface IRuleSet<T> where T : class
{
    void Add(IAgentRule<T> rule);
    void AddRange(IEnumerable<IAgentRule<T>> rules);
    bool Remove(string ruleId);
    IAgentRule<T>? FindById(string ruleId);

    /// <summary>
    /// Get all rules as a queryable collection.
    /// </summary>
    IQueryable<IAgentRule<T>> AsQueryable();

    /// <summary>
    /// Filter rules by predicate.
    /// </summary>
    IEnumerable<IAgentRule<T>> Where(Func<IAgentRule<T>, bool> predicate);

    /// <summary>
    /// Preview which rules would match a fact without executing actions.
    /// </summary>
    IEnumerable<IAgentRule<T>> WouldMatch(T fact);

    /// <summary>
    /// For routing rules: preview where a message would be routed.
    /// </summary>
    RoutePreview<T> WouldRoute(T fact);

    /// <summary>
    /// Create a processing pipeline for this fact type.
    /// </summary>
    IPipelineBuilder<T> Pipeline();
}
