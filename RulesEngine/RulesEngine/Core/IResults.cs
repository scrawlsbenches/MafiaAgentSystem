namespace RulesEngine.Core;

/// <summary>
/// Interface for individual rule execution results.
/// Extracted from RuleResult to enable dependency inversion.
/// </summary>
public interface IRuleResult
{
    /// <summary>
    /// The unique identifier of the rule that was executed
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// The human-readable name of the rule
    /// </summary>
    string RuleName { get; }

    /// <summary>
    /// Whether the rule's condition matched the fact
    /// </summary>
    bool Matched { get; }

    /// <summary>
    /// Whether the rule's action was executed (only true if Matched and no errors)
    /// </summary>
    bool ActionExecuted { get; }

    /// <summary>
    /// When the rule was executed
    /// </summary>
    DateTime ExecutedAt { get; }

    /// <summary>
    /// Optional outputs from the rule execution
    /// </summary>
    IReadOnlyDictionary<string, object> Outputs { get; }

    /// <summary>
    /// Error message if the rule execution failed, null otherwise
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Whether the rule execution resulted in an error
    /// </summary>
    bool HasError => ErrorMessage != null;
}

/// <summary>
/// Interface for aggregated rules engine execution results.
/// Extracted from RulesEngineResult to enable dependency inversion.
/// </summary>
public interface IRulesEngineResult
{
    /// <summary>
    /// All individual rule results from the execution
    /// </summary>
    IReadOnlyList<RuleResult> RuleResults { get; }

    /// <summary>
    /// Total time taken to execute all rules
    /// </summary>
    TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Total number of rules that were evaluated
    /// </summary>
    int TotalRulesEvaluated { get; }

    /// <summary>
    /// Number of rules whose conditions matched
    /// </summary>
    int MatchedRules { get; }

    /// <summary>
    /// Number of rule actions that were executed
    /// </summary>
    int ExecutedActions { get; }

    /// <summary>
    /// Number of rules that resulted in errors
    /// </summary>
    int Errors { get; }

    /// <summary>
    /// Gets all rules that matched
    /// </summary>
    List<RuleResult> GetMatchedRules();

    /// <summary>
    /// Gets all rules that had errors
    /// </summary>
    List<RuleResult> GetErrors();
}

/// <summary>
/// Interface for detailed rule execution results including exception info.
/// Extracted from RuleExecutionResult to enable dependency inversion.
/// </summary>
/// <typeparam name="T">The type of fact the rule evaluated</typeparam>
public interface IRuleExecutionResult<T>
{
    /// <summary>
    /// The sync rule that was executed (null if async rule was executed)
    /// </summary>
    IRule<T>? Rule { get; }

    /// <summary>
    /// The async rule that was executed (null if sync rule was executed)
    /// </summary>
    IAsyncRule<T>? AsyncRule { get; }

    /// <summary>
    /// The ID of the rule that was executed
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// The name of the rule that was executed
    /// </summary>
    string RuleName { get; }

    /// <summary>
    /// Whether this result is from an async rule
    /// </summary>
    bool IsAsyncRule { get; }

    /// <summary>
    /// The result of the rule execution
    /// </summary>
    RuleResult Result { get; }

    /// <summary>
    /// Whether the rule was successfully executed without exceptions
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// The exception that occurred during execution, if any
    /// </summary>
    Exception? Exception { get; }
}
