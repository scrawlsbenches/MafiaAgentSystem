namespace AgentRouting.Configuration;

/// <summary>
/// Centralized default configuration values for middleware components.
/// These values can be overridden when constructing individual middleware instances.
/// </summary>
public static class MiddlewareDefaults
{
    #region Cache Middleware Defaults

    /// <summary>
    /// Default time-to-live for cached entries.
    /// </summary>
    public static readonly TimeSpan CacheDefaultTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default maximum number of entries in the cache.
    /// </summary>
    public const int CacheMaxEntries = 1000;

    #endregion

    #region Rate Limit Middleware Defaults

    /// <summary>
    /// Default time window for rate limiting.
    /// </summary>
    public static readonly TimeSpan RateLimitDefaultWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Default maximum number of requests allowed within the rate limit window.
    /// </summary>
    public const int RateLimitDefaultMaxRequests = 100;

    #endregion

    #region Circuit Breaker Middleware Defaults

    /// <summary>
    /// Default number of failures within the window before the circuit opens.
    /// </summary>
    public const int CircuitBreakerDefaultThreshold = 5;

    /// <summary>
    /// Default timeout before attempting to close an open circuit (half-open state).
    /// </summary>
    public static readonly TimeSpan CircuitBreakerDefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default sliding time window for counting failures.
    /// Only failures within this window are counted toward the threshold.
    /// </summary>
    public static readonly TimeSpan CircuitBreakerDefaultFailureWindow = TimeSpan.FromSeconds(60);

    #endregion

    #region Retry Middleware Defaults

    /// <summary>
    /// Default maximum number of retry attempts.
    /// </summary>
    public const int RetryDefaultMaxAttempts = 3;

    /// <summary>
    /// Default base delay between retry attempts.
    /// Used as multiplier with attempt number for exponential backoff.
    /// </summary>
    public static readonly TimeSpan RetryDefaultBaseDelay = TimeSpan.FromMilliseconds(100);

    #endregion

    #region Validation Middleware Defaults

    /// <summary>
    /// Default maximum content length for messages.
    /// </summary>
    public const int ValidationMaxContentLength = 1048576; // 1 MB

    /// <summary>
    /// Default maximum subject length for messages.
    /// </summary>
    public const int ValidationMaxSubjectLength = 500;

    #endregion

    #region Metrics Middleware Defaults

    /// <summary>
    /// Default interval for metrics snapshot generation.
    /// </summary>
    public static readonly TimeSpan MetricsSnapshotInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Default number of processing time samples to retain.
    /// </summary>
    public const int MetricsMaxSamples = 10000;

    #endregion
}
