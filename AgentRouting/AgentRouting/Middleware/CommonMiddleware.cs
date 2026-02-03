using System.Collections.Concurrent;
using System.Diagnostics;
using AgentRouting.Configuration;
using AgentRouting.Core;
using AgentRouting.Infrastructure;

namespace AgentRouting.Middleware;

/// <summary>
/// Logs message processing
/// </summary>
public class LoggingMiddleware : MiddlewareBase
{
    private readonly IAgentLogger _logger;
    
    public LoggingMiddleware(IAgentLogger logger)
    {
        _logger = logger;
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        Console.WriteLine($"[Middleware] Processing: {message.Subject}");
        
        var result = await next(message, ct);
        
        Console.WriteLine($"[Middleware] Completed: {message.Subject} - {(result.Success ? "SUCCESS" : "FAILED")}");
        
        return result;
    }
}

/// <summary>
/// Measures and tracks processing time
/// </summary>
public class TimingMiddleware : MiddlewareBase
{
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        
        var result = await next(message, ct);
        
        sw.Stop();
        result.Data["ProcessingTimeMs"] = sw.ElapsedMilliseconds;
        
        return result;
    }
}

/// <summary>
/// Validates messages before processing
/// </summary>
public class ValidationMiddleware : MiddlewareBase
{
    public override Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        // Validate required fields
        if (string.IsNullOrEmpty(message.SenderId))
        {
            return Task.FromResult(ShortCircuit("SenderId is required"));
        }
        
        if (string.IsNullOrEmpty(message.Subject))
        {
            return Task.FromResult(ShortCircuit("Subject is required"));
        }
        
        if (string.IsNullOrEmpty(message.Content))
        {
            return Task.FromResult(ShortCircuit("Content is required"));
        }
        
        return next(message, ct);
    }
}

/// <summary>
/// Rate limiting middleware
/// </summary>
public class RateLimitMiddleware : MiddlewareBase
{
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly ISystemClock _clock;
    private readonly IStateStore _store;

    /// <summary>
    /// Creates a rate limiter with the specified settings.
    /// </summary>
    /// <param name="store">State store for persisting rate limit data.</param>
    /// <param name="maxRequests">Maximum requests allowed within the time window.</param>
    /// <param name="window">Time window for rate limiting.</param>
    /// <param name="clock">Clock for time operations.</param>
    public RateLimitMiddleware(IStateStore store, int maxRequests, TimeSpan window, ISystemClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _maxRequests = maxRequests;
        _window = window;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public override Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        var storeKey = $"ratelimit:{message.SenderId}";
        var now = _clock.UtcNow;

        var state = _store.GetOrAdd(storeKey, _ => new RateLimitState());

        lock (state)
        {
            // Remove old timestamps
            state.Timestamps.RemoveAll(t => now - t > _window);

            // Check limit
            if (state.Timestamps.Count >= _maxRequests)
            {
                return Task.FromResult(ShortCircuit($"Rate limit exceeded. Max {_maxRequests} requests per {_window.TotalSeconds}s"));
            }

            // Add current timestamp
            state.Timestamps.Add(now);
        }

        return next(message, ct);
    }

    private class RateLimitState
    {
        public List<DateTime> Timestamps { get; } = new();
    }
}

/// <summary>
/// Caches message results with LRU eviction policy
/// </summary>
public class CachingMiddleware : MiddlewareBase
{
    private const string CacheStateKey = "caching:state";
    private readonly TimeSpan _ttl;
    private readonly int _maxEntries;
    private readonly ISystemClock _clock;
    private readonly IStateStore _store;

    /// <summary>
    /// Creates a caching middleware with the specified settings.
    /// </summary>
    /// <param name="store">State store for persisting cache data.</param>
    /// <param name="ttl">Time-to-live for cached entries.</param>
    /// <param name="maxEntries">Maximum number of cache entries.</param>
    /// <param name="clock">Clock for time operations.</param>
    public CachingMiddleware(IStateStore store, TimeSpan ttl, int maxEntries, ISystemClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _ttl = ttl;
        _maxEntries = maxEntries;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    private CacheState GetCacheState() => _store.GetOrAdd(CacheStateKey, _ => new CacheState());

    /// <summary>
    /// Gets the current number of entries in the cache.
    /// </summary>
    public int Count => GetCacheState().Cache.Count;

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        var key = GenerateCacheKey(message);
        var cacheState = GetCacheState();

        // Check cache first
        if (cacheState.Cache.TryGetValue(key, out var entry))
        {
            // Check expiration
            if (_clock.UtcNow - entry.CachedAt < _ttl)
            {
                entry.LastAccessedAt = _clock.UtcNow;
                Console.WriteLine($"[Cache] HIT: {message.Subject}");
                return entry.Result;
            }

            // Expired, remove it
            cacheState.Cache.TryRemove(key, out _);
        }

        // Cache miss - check if there's already a pending request for this key
        var tcs = new TaskCompletionSource<MessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingTask = cacheState.PendingRequests.GetOrAdd(key, tcs.Task);

        if (pendingTask != tcs.Task)
        {
            // Another request is already computing this - wait for it
            Console.WriteLine($"[Cache] COALESCE: {message.Subject}");
            return await pendingTask;
        }

        // We're responsible for computing the result
        Console.WriteLine($"[Cache] MISS: {message.Subject}");

        try
        {
            var result = await next(message, ct);

            if (result.Success)
            {
                var now = _clock.UtcNow;
                cacheState.Cache[key] = new CacheEntry
                {
                    Result = result,
                    CachedAt = now,
                    LastAccessedAt = now
                };

                // Evict if cache exceeds max size
                EvictIfNeeded(cacheState);
            }

            tcs.SetResult(result);
            return result;
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
            throw;
        }
        finally
        {
            // Remove from pending requests
            cacheState.PendingRequests.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Evicts least recently used entries when cache exceeds max size.
    /// Removes 10% extra entries as buffer to avoid frequent evictions.
    /// </summary>
    private void EvictIfNeeded(CacheState cacheState)
    {
        if (cacheState.Cache.Count <= _maxEntries)
            return;

        // Remove oldest by last access time (LRU)
        var entriesToRemove = cacheState.Cache
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .Take(cacheState.Cache.Count - _maxEntries + (_maxEntries / 10)) // Remove 10% extra for buffer
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in entriesToRemove)
            cacheState.Cache.TryRemove(key, out _);
    }

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    public void Clear()
    {
        GetCacheState().Cache.Clear();
    }

    /// <summary>
    /// Removes all expired entries from the cache.
    /// Call this periodically to free memory from stale entries.
    /// </summary>
    public void CleanupExpired()
    {
        var cacheState = GetCacheState();
        var now = _clock.UtcNow;
        var expiredKeys = cacheState.Cache
            .Where(kvp => now - kvp.Value.CachedAt >= _ttl)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
            cacheState.Cache.TryRemove(key, out _);
    }

    private string GenerateCacheKey(AgentMessage message)
    {
        return $"{message.SenderId}:{message.Category}:{message.Subject}:{message.Content}";
    }

    private class CacheState
    {
        public ConcurrentDictionary<string, CacheEntry> Cache { get; } = new();
        public ConcurrentDictionary<string, Task<MessageResult>> PendingRequests { get; } = new();
    }

    private class CacheEntry
    {
        public MessageResult Result { get; set; } = null!;
        public DateTime CachedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
    }
}

/// <summary>
/// Retries failed operations with exponential backoff
/// </summary>
public class RetryMiddleware : MiddlewareBase
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _delay;

    /// <summary>
    /// Creates a retry middleware with default settings from MiddlewareDefaults.
    /// </summary>
    public RetryMiddleware()
        : this(MiddlewareDefaults.RetryDefaultMaxAttempts, MiddlewareDefaults.RetryDefaultBaseDelay)
    {
    }

    /// <summary>
    /// Creates a retry middleware with custom settings.
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts.</param>
    /// <param name="delay">Base delay between retries (multiplied by attempt number for exponential backoff).</param>
    public RetryMiddleware(int maxAttempts, TimeSpan? delay = null)
    {
        _maxAttempts = maxAttempts;
        _delay = delay ?? MiddlewareDefaults.RetryDefaultBaseDelay;
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        MessageResult? result = null;
        
        for (int attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                result = await next(message, ct);
                
                if (result.Success)
                {
                    if (attempt > 1)
                    {
                        Console.WriteLine($"[Retry] Succeeded on attempt {attempt}");
                    }
                    return result;
                }
                
                if (attempt < _maxAttempts)
                {
                    Console.WriteLine($"[Retry] Attempt {attempt} failed, retrying...");
                    await Task.Delay(_delay * attempt, ct); // Exponential backoff
                }
            }
            catch (Exception ex)
            {
                if (attempt == _maxAttempts)
                {
                    return MessageResult.Fail($"Failed after {_maxAttempts} attempts: {ex.Message}");
                }
                
                Console.WriteLine($"[Retry] Attempt {attempt} threw exception, retrying...");
                await Task.Delay(_delay * attempt, ct);
            }
        }
        
        return result ?? MessageResult.Fail($"Failed after {_maxAttempts} attempts");
    }
}

/// <summary>
/// Circuit breaker pattern - protects downstream services from cascading failures.
/// Uses a sliding time window for failure counting (industry standard approach).
/// </summary>
public class CircuitBreakerMiddleware : MiddlewareBase
{
    private const string CircuitBreakerStateKey = "circuitbreaker:state";
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private readonly TimeSpan _failureWindow;
    private readonly ISystemClock _clock;
    private readonly IStateStore _store;

    /// <summary>
    /// Creates a circuit breaker with the specified settings.
    /// </summary>
    /// <param name="store">State store for persisting circuit breaker state.</param>
    /// <param name="failureThreshold">Number of failures within the window before opening.</param>
    /// <param name="resetTimeout">Time to wait before attempting to close an open circuit.</param>
    /// <param name="failureWindow">Sliding time window for counting failures.</param>
    /// <param name="clock">Clock for time operations.</param>
    public CircuitBreakerMiddleware(
        IStateStore store,
        int failureThreshold,
        TimeSpan resetTimeout,
        TimeSpan failureWindow,
        ISystemClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout;
        _failureWindow = failureWindow;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    private CircuitBreakerState GetState() => _store.GetOrAdd(CircuitBreakerStateKey, _ => new CircuitBreakerState());

    /// <summary>
    /// Gets the current number of failures within the sliding window.
    /// </summary>
    public int CurrentFailureCount
    {
        get
        {
            var state = GetState();
            lock (state.Lock)
            {
                PruneOldFailures(state);
                return state.FailureTimestamps.Count;
            }
        }
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        var state = GetState();
        bool isHalfOpenTest = false;

        // Check circuit state and acquire test slot if HalfOpen
        lock (state.Lock)
        {
            // Check if we should transition from Open to HalfOpen
            if (state.State == CircuitState.Open && state.OpenedAt.HasValue)
            {
                if (_clock.UtcNow - state.OpenedAt.Value > _resetTimeout)
                {
                    Console.WriteLine("[Circuit] Attempting to close circuit (half-open state)");
                    state.State = CircuitState.HalfOpen;
                    state.HalfOpenTestInProgress = false; // Reset for new HalfOpen period
                }
            }

            // If circuit is open, fail fast
            if (state.State == CircuitState.Open)
            {
                return ShortCircuit("Circuit breaker is OPEN - service unavailable");
            }

            // If circuit is half-open, only allow ONE request through to test recovery
            if (state.State == CircuitState.HalfOpen)
            {
                if (state.HalfOpenTestInProgress)
                {
                    // Another request is already testing - fail fast
                    return ShortCircuit("Circuit breaker is HALF-OPEN - test in progress");
                }
                // We're the test request
                state.HalfOpenTestInProgress = true;
                isHalfOpenTest = true;
            }
        }

        try
        {
            var result = await next(message, ct);

            lock (state.Lock)
            {
                if (result.Success)
                {
                    if (state.State == CircuitState.HalfOpen)
                    {
                        Console.WriteLine("[Circuit] Closing circuit - service recovered");
                        state.State = CircuitState.Closed;
                        state.FailureTimestamps.Clear();
                        state.HalfOpenTestInProgress = false;
                    }
                    return result;
                }
                else
                {
                    RecordFailure(state);
                    if (isHalfOpenTest)
                    {
                        state.HalfOpenTestInProgress = false;
                    }
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            lock (state.Lock)
            {
                RecordFailure(state);
                if (isHalfOpenTest)
                {
                    state.HalfOpenTestInProgress = false;
                }
            }
            return MessageResult.Fail($"Circuit breaker caught exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Records a failure and checks if circuit should open.
    /// Must be called within a lock on state.Lock.
    /// </summary>
    private void RecordFailure(CircuitBreakerState state)
    {
        var now = _clock.UtcNow;
        state.FailureTimestamps.Enqueue(now);

        // Prune failures outside the window
        PruneOldFailures(state);

        if (state.FailureTimestamps.Count >= _failureThreshold && state.State != CircuitState.Open)
        {
            Console.WriteLine($"[Circuit] OPENING circuit - {state.FailureTimestamps.Count} failures in {_failureWindow.TotalSeconds}s window");
            state.State = CircuitState.Open;
            state.OpenedAt = now;
        }
    }

    /// <summary>
    /// Removes failure timestamps that are outside the sliding window.
    /// Must be called within a lock on state.Lock.
    /// </summary>
    private void PruneOldFailures(CircuitBreakerState state)
    {
        var cutoff = _clock.UtcNow - _failureWindow;
        while (state.FailureTimestamps.Count > 0 && state.FailureTimestamps.Peek() < cutoff)
        {
            state.FailureTimestamps.Dequeue();
        }
    }

    private enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    private class CircuitBreakerState
    {
        public Queue<DateTime> FailureTimestamps { get; } = new();
        public CircuitState State { get; set; } = CircuitState.Closed;
        public DateTime? OpenedAt { get; set; }
        public bool HalfOpenTestInProgress { get; set; }
        public object Lock { get; } = new();
    }
}

/// <summary>
/// Tracks metrics and statistics
/// </summary>
public class MetricsMiddleware : MiddlewareBase
{
    private int _totalMessages = 0;
    private int _successCount = 0;
    private int _failureCount = 0;
    private readonly ConcurrentBag<long> _processingTimes = new();
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        Interlocked.Increment(ref _totalMessages);
        
        var sw = Stopwatch.StartNew();
        var result = await next(message, ct);
        sw.Stop();
        
        _processingTimes.Add(sw.ElapsedMilliseconds);
        
        if (result.Success)
            Interlocked.Increment(ref _successCount);
        else
            Interlocked.Increment(ref _failureCount);
        
        return result;
    }
    
    public MetricsSnapshot GetSnapshot()
    {
        var times = _processingTimes.ToArray();
        
        return new MetricsSnapshot
        {
            TotalMessages = _totalMessages,
            SuccessCount = _successCount,
            FailureCount = _failureCount,
            SuccessRate = _totalMessages > 0 ? (double)_successCount / _totalMessages : 0,
            AverageProcessingTimeMs = times.Length > 0 ? times.Average() : 0,
            MinProcessingTimeMs = times.Length > 0 ? times.Min() : 0,
            MaxProcessingTimeMs = times.Length > 0 ? times.Max() : 0
        };
    }
}

public class MetricsSnapshot : IMetricsSnapshot
{
    public int TotalMessages { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public long MinProcessingTimeMs { get; set; }
    public long MaxProcessingTimeMs { get; set; }

    public override string ToString()
    {
        return $"Total: {TotalMessages}, Success: {SuccessCount}, Failed: {FailureCount}, " +
               $"Success Rate: {SuccessRate:P}, Avg Time: {AverageProcessingTimeMs:F2}ms";
    }
}

/// <summary>
/// Simple authentication middleware
/// </summary>
public class AuthenticationMiddleware : MiddlewareBase
{
    private readonly HashSet<string> _authenticatedSenders;
    
    public AuthenticationMiddleware(params string[] authenticatedSenders)
    {
        _authenticatedSenders = new HashSet<string>(authenticatedSenders, StringComparer.OrdinalIgnoreCase);
    }
    
    public override Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        if (!_authenticatedSenders.Contains(message.SenderId))
        {
            return Task.FromResult(ShortCircuit($"Sender '{message.SenderId}' is not authenticated"));
        }
        
        return next(message, ct);
    }
}

/// <summary>
/// Priority boost for VIP senders
/// </summary>
public class PriorityBoostMiddleware : MiddlewareBase
{
    private readonly HashSet<string> _vipSenders;
    
    public PriorityBoostMiddleware(params string[] vipSenders)
    {
        _vipSenders = new HashSet<string>(vipSenders, StringComparer.OrdinalIgnoreCase);
    }
    
    public override Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        if (_vipSenders.Contains(message.SenderId) && message.Priority < MessagePriority.High)
        {
            Console.WriteLine($"[Priority] Boosting priority for VIP sender: {message.SenderId}");
            message.Priority = MessagePriority.High;
        }
        
        return next(message, ct);
    }
}

/// <summary>
/// Enriches messages with additional context
/// </summary>
public class EnrichmentMiddleware : MiddlewareBase
{
    public override Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        // Add timestamp if not present
        if (!message.Metadata.ContainsKey("ReceivedAt"))
        {
            message.Metadata["ReceivedAt"] = DateTime.UtcNow;
        }
        
        // Add machine name
        message.Metadata["ProcessedBy"] = Environment.MachineName;
        
        // Generate correlation ID if not present
        if (string.IsNullOrEmpty(message.ConversationId))
        {
            message.ConversationId = Guid.NewGuid().ToString();
        }

        return next(message, ct);
    }
}

/// <summary>
/// Tracks business analytics: category distribution and agent workload
/// </summary>
public class AnalyticsMiddleware : MiddlewareBase
{
    private readonly ConcurrentDictionary<string, int> _categoryCount = new();
    private readonly ConcurrentDictionary<string, int> _agentWorkload = new();
    private int _totalMessages = 0;

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        Interlocked.Increment(ref _totalMessages);

        // Track categories
        if (!string.IsNullOrEmpty(message.Category))
        {
            _categoryCount.AddOrUpdate(message.Category, 1, (_, count) => count + 1);
        }

        // Track agent workload
        if (!string.IsNullOrEmpty(message.ReceiverId))
        {
            _agentWorkload.AddOrUpdate(message.ReceiverId, 1, (_, count) => count + 1);
        }

        return await next(message, ct);
    }

    public AnalyticsReport GetReport()
    {
        return new AnalyticsReport
        {
            TotalMessages = _totalMessages,
            CategoryCounts = new Dictionary<string, int>(_categoryCount),
            AgentWorkload = new Dictionary<string, int>(_agentWorkload)
        };
    }

    public string GenerateReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Analytics Report ===");
        report.AppendLine($"Total Messages Processed: {_totalMessages}");
        report.AppendLine();

        report.AppendLine("Messages by Category:");
        foreach (var (category, count) in _categoryCount.OrderByDescending(x => x.Value))
        {
            var percentage = _totalMessages > 0 ? count * 100.0 / _totalMessages : 0;
            report.AppendLine($"  {category}: {count} ({percentage:F1}%)");
        }

        report.AppendLine();
        report.AppendLine("Agent Workload:");
        foreach (var (agentId, count) in _agentWorkload.OrderByDescending(x => x.Value))
        {
            report.AppendLine($"  {agentId}: {count} messages");
        }

        return report.ToString();
    }
}

public class AnalyticsReport : IAnalyticsReport
{
    public int TotalMessages { get; set; }
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
    public Dictionary<string, int> AgentWorkload { get; set; } = new();

    // Explicit interface implementations for IReadOnlyDictionary
    IReadOnlyDictionary<string, int> IAnalyticsReport.CategoryCounts => CategoryCounts;
    IReadOnlyDictionary<string, int> IAnalyticsReport.AgentWorkload => AgentWorkload;
}
