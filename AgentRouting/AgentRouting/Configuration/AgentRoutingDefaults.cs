namespace AgentRouting.Configuration;

/// <summary>
/// Centralized default configuration values for agent routing operations.
/// These values can be overridden when constructing routing components.
/// </summary>
public static class AgentRoutingDefaults
{
    /// <summary>
    /// Default maximum number of concurrent messages that can be processed.
    /// </summary>
    public const int MaxConcurrentMessages = 100;

    /// <summary>
    /// Default timeout for message processing operations.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default maximum number of retry attempts for failed operations.
    /// </summary>
    public const int MaxRetries = 3;

    /// <summary>
    /// Default delay between retry attempts.
    /// </summary>
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Whether routing should stop on first matching rule by default.
    /// </summary>
    public const bool StopOnFirstMatch = true;

    /// <summary>
    /// Whether to track performance metrics by default.
    /// </summary>
    public const bool TrackPerformance = true;

    /// <summary>
    /// Default message priority value.
    /// </summary>
    public const int DefaultPriority = 0;

    /// <summary>
    /// Maximum queue size for pending messages.
    /// </summary>
    public const int MaxQueueSize = 10000;
}
