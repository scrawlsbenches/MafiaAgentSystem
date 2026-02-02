namespace RulesEngine.Core;

/// <summary>
/// Interface for the rules engine that evaluates rules against facts.
/// Supports both synchronous and asynchronous rule execution.
/// </summary>
/// <typeparam name="T">The type of fact the engine evaluates</typeparam>
public interface IRulesEngine<T> : IDisposable
{
    /// <summary>
    /// Registers a rule with the engine.
    /// </summary>
    /// <param name="rule">The rule to register</param>
    /// <exception cref="ArgumentNullException">Thrown when rule is null</exception>
    /// <exception cref="RuleValidationException">Thrown when rule fails validation</exception>
    void RegisterRule(IRule<T> rule);

    /// <summary>
    /// Registers multiple rules with the engine.
    /// </summary>
    /// <param name="rules">The rules to register</param>
    /// <exception cref="ArgumentNullException">Thrown when rules array is null</exception>
    /// <exception cref="RuleValidationException">Thrown when any rule fails validation</exception>
    void RegisterRules(params IRule<T>[] rules);

    /// <summary>
    /// Convenience method for inline rule creation with condition and action.
    /// </summary>
    /// <param name="id">Unique rule identifier</param>
    /// <param name="name">Human-readable rule name</param>
    /// <param name="condition">Predicate that determines if rule applies</param>
    /// <param name="action">Action to execute when rule matches (modifies fact in-place)</param>
    /// <param name="priority">Rule priority (higher = evaluated first)</param>
    /// <exception cref="RuleValidationException">Thrown when rule fails validation</exception>
    void AddRule(string id, string name, Func<T, bool> condition, Action<T> action, int priority = 0);

    /// <summary>
    /// Registers an async rule with the engine.
    /// </summary>
    /// <param name="rule">The async rule to register</param>
    /// <exception cref="ArgumentNullException">Thrown when rule is null</exception>
    /// <exception cref="RuleValidationException">Thrown when rule fails validation</exception>
    void RegisterAsyncRule(IAsyncRule<T> rule);

    /// <summary>
    /// Registers multiple async rules with the engine.
    /// </summary>
    /// <param name="rules">The async rules to register</param>
    /// <exception cref="ArgumentNullException">Thrown when rules array is null</exception>
    /// <exception cref="RuleValidationException">Thrown when any rule fails validation</exception>
    void RegisterAsyncRules(params IAsyncRule<T>[] rules);

    /// <summary>
    /// Gets all registered synchronous rules.
    /// </summary>
    IReadOnlyList<IRule<T>> GetRules();

    /// <summary>
    /// Gets all registered async rules.
    /// </summary>
    IReadOnlyList<IAsyncRule<T>> GetAsyncRules();

    /// <summary>
    /// Removes a rule by ID (checks both sync and async rules).
    /// </summary>
    /// <param name="ruleId">The ID of the rule to remove</param>
    /// <returns>True if a rule was removed, false if no rule with that ID exists</returns>
    bool RemoveRule(string ruleId);

    /// <summary>
    /// Clears all rules (both sync and async).
    /// </summary>
    void ClearRules();

    /// <summary>
    /// Evaluates all matching rules and applies their actions to the fact.
    /// Unlike Execute(), this modifies the fact in-place and doesn't return results.
    /// Rules are evaluated in priority order (highest first).
    /// </summary>
    /// <param name="fact">The fact to evaluate rules against</param>
    void EvaluateAll(T fact);

    /// <summary>
    /// Evaluates all applicable rules against the fact and returns results.
    /// </summary>
    /// <param name="fact">The fact to evaluate rules against</param>
    /// <returns>Results of rule execution</returns>
    RulesEngineResult Execute(T fact);

    /// <summary>
    /// Evaluates all applicable rules against the fact with cancellation support.
    /// </summary>
    /// <param name="fact">The fact to evaluate rules against</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Results of rule execution</returns>
    RulesEngineResult Execute(T fact, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously executes all applicable rules against the fact with cancellation support.
    /// Processes sync rules first, then async rules, both in priority order.
    /// </summary>
    /// <param name="fact">The fact to evaluate rules against</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A collection of rule execution results</returns>
    Task<IEnumerable<RuleExecutionResult<T>>> ExecuteAsync(T fact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks which rules would match without executing actions.
    /// </summary>
    /// <param name="fact">The fact to check against</param>
    /// <returns>List of rules that would match</returns>
    List<IRule<T>> GetMatchingRules(T fact);

    /// <summary>
    /// Gets performance metrics for a specific rule.
    /// </summary>
    /// <param name="ruleId">The rule ID to get metrics for</param>
    /// <returns>Performance metrics, or null if not found</returns>
    RulePerformanceMetrics? GetMetrics(string ruleId);

    /// <summary>
    /// Gets all performance metrics.
    /// </summary>
    /// <returns>Dictionary of rule ID to performance metrics</returns>
    Dictionary<string, RulePerformanceMetrics> GetAllMetrics();
}
