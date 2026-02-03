using AgentRouting.Core;

namespace AgentRouting.Middleware;

/// <summary>
/// Interface for distributed tracing spans.
/// Extracted from TraceSpan to enable dependency inversion and custom implementations.
/// </summary>
public interface ITraceSpan
{
    /// <summary>
    /// Unique identifier for the entire trace (shared across all spans in a trace)
    /// </summary>
    string TraceId { get; }

    /// <summary>
    /// Unique identifier for this specific span
    /// </summary>
    string SpanId { get; }

    /// <summary>
    /// The span ID of the parent span, if any
    /// </summary>
    string? ParentSpanId { get; }

    /// <summary>
    /// Name of the service that created this span
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Name of the operation being traced
    /// </summary>
    string OperationName { get; }

    /// <summary>
    /// When this span started
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// How long the operation took
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Whether the operation completed successfully
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// Additional metadata tags for the span
    /// </summary>
    IReadOnlyDictionary<string, string> Tags { get; }
}

/// <summary>
/// Interface for middleware context that allows sharing data between middleware.
/// Extracted from MiddlewareContext to enable dependency inversion.
/// </summary>
public interface IMiddlewareContext
{
    /// <summary>
    /// Gets a value from the context by key
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve</typeparam>
    /// <param name="key">The key to look up</param>
    /// <returns>The value if found, default(T) otherwise</returns>
    T? Get<T>(string key);

    /// <summary>
    /// Sets a value in the context
    /// </summary>
    /// <typeparam name="T">The type of value to store</typeparam>
    /// <param name="key">The key to store under</param>
    /// <param name="value">The value to store</param>
    void Set<T>(string key, T value);

    /// <summary>
    /// Tries to get a value from the context
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve</typeparam>
    /// <param name="key">The key to look up</param>
    /// <param name="value">The retrieved value if found</param>
    /// <returns>True if the value was found, false otherwise</returns>
    bool TryGet<T>(string key, out T? value);
}

/// <summary>
/// Interface for metrics snapshots from middleware.
/// Extracted from MetricsSnapshot to enable dependency inversion.
/// </summary>
public interface IMetricsSnapshot
{
    /// <summary>
    /// Total number of messages processed
    /// </summary>
    int TotalMessages { get; }

    /// <summary>
    /// Number of successful message processings
    /// </summary>
    int SuccessCount { get; }

    /// <summary>
    /// Number of failed message processings
    /// </summary>
    int FailureCount { get; }

    /// <summary>
    /// Success rate (0.0 to 1.0)
    /// </summary>
    double SuccessRate { get; }

    /// <summary>
    /// Average processing time in milliseconds
    /// </summary>
    double AverageProcessingTimeMs { get; }

    /// <summary>
    /// Minimum processing time in milliseconds
    /// </summary>
    long MinProcessingTimeMs { get; }

    /// <summary>
    /// Maximum processing time in milliseconds
    /// </summary>
    long MaxProcessingTimeMs { get; }
}

/// <summary>
/// Interface for analytics reports from middleware.
/// Extracted from AnalyticsReport to enable dependency inversion.
/// </summary>
public interface IAnalyticsReport
{
    /// <summary>
    /// Total number of messages analyzed
    /// </summary>
    int TotalMessages { get; }

    /// <summary>
    /// Message counts grouped by category
    /// </summary>
    IReadOnlyDictionary<string, int> CategoryCounts { get; }

    /// <summary>
    /// Message counts grouped by agent
    /// </summary>
    IReadOnlyDictionary<string, int> AgentWorkload { get; }
}

/// <summary>
/// Interface for workflow definitions.
/// Extracted from WorkflowDefinition to enable dependency inversion.
/// </summary>
public interface IWorkflowDefinition
{
    /// <summary>
    /// Unique identifier for this workflow
    /// </summary>
    string WorkflowId { get; }

    /// <summary>
    /// The stages that make up this workflow
    /// </summary>
    IReadOnlyList<IWorkflowStage> Stages { get; }
}

/// <summary>
/// Interface for workflow stages.
/// Extracted from WorkflowStage to enable dependency inversion.
/// </summary>
public interface IWorkflowStage
{
    /// <summary>
    /// Name of this stage
    /// </summary>
    string Name { get; }

    /// <summary>
    /// ID of the agent that handles this stage
    /// </summary>
    string AgentId { get; }

    /// <summary>
    /// Optional condition that determines if this stage should execute
    /// </summary>
    Func<AgentMessage, bool>? Condition { get; }
}
