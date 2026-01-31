using System.Collections.Concurrent;
using System.Diagnostics;
using AgentRouting.Core;

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
    private readonly ConcurrentDictionary<string, RateLimitState> _state = new();
    
    public RateLimitMiddleware(int maxRequests, TimeSpan window)
    {
        _maxRequests = maxRequests;
        _window = window;
    }
    
    public override Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        var key = message.SenderId;
        var now = DateTime.UtcNow;
        
        var state = _state.GetOrAdd(key, _ => new RateLimitState());
        
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
/// Caches message results
/// </summary>
public class CachingMiddleware : MiddlewareBase
{
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    
    public CachingMiddleware(TimeSpan ttl)
    {
        _ttl = ttl;
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        var key = GenerateCacheKey(message);
        
        // Check cache
        if (_cache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow - entry.Timestamp < _ttl)
            {
                Console.WriteLine($"[Cache] HIT: {message.Subject}");
                return entry.Result;
            }
            
            // Expired
            _cache.TryRemove(key, out _);
        }
        
        Console.WriteLine($"[Cache] MISS: {message.Subject}");
        
        // Process and cache
        var result = await next(message, ct);
        
        if (result.Success)
        {
            _cache[key] = new CacheEntry
            {
                Result = result,
                Timestamp = DateTime.UtcNow
            };
        }
        
        return result;
    }
    
    private string GenerateCacheKey(AgentMessage message)
    {
        return $"{message.SenderId}:{message.Category}:{message.Subject}:{message.Content}";
    }
    
    private class CacheEntry
    {
        public MessageResult Result { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}

/// <summary>
/// Retries failed operations
/// </summary>
public class RetryMiddleware : MiddlewareBase
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _delay;
    
    public RetryMiddleware(int maxAttempts = 3, TimeSpan? delay = null)
    {
        _maxAttempts = maxAttempts;
        _delay = delay ?? TimeSpan.FromMilliseconds(100);
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
/// Circuit breaker pattern
/// </summary>
public class CircuitBreakerMiddleware : MiddlewareBase
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount = 0;
    private DateTime? _openedAt;
    
    public CircuitBreakerMiddleware(int failureThreshold = 5, TimeSpan? resetTimeout = null)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30);
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        // Check if we should reset
        if (_state == CircuitState.Open && _openedAt.HasValue)
        {
            if (DateTime.UtcNow - _openedAt.Value > _resetTimeout)
            {
                Console.WriteLine("[Circuit] Attempting to close circuit (half-open state)");
                _state = CircuitState.HalfOpen;
            }
        }
        
        // If circuit is open, fail fast
        if (_state == CircuitState.Open)
        {
            return ShortCircuit("Circuit breaker is OPEN - service unavailable");
        }
        
        try
        {
            var result = await next(message, ct);
            
            if (result.Success)
            {
                if (_state == CircuitState.HalfOpen)
                {
                    Console.WriteLine("[Circuit] Closing circuit - service recovered");
                    _state = CircuitState.Closed;
                    _failureCount = 0;
                }
                return result;
            }
            else
            {
                RecordFailure();
                return result;
            }
        }
        catch (Exception ex)
        {
            RecordFailure();
            return MessageResult.Fail($"Circuit breaker caught exception: {ex.Message}");
        }
    }
    
    private void RecordFailure()
    {
        _failureCount++;
        
        if (_failureCount >= _failureThreshold && _state != CircuitState.Open)
        {
            Console.WriteLine($"[Circuit] OPENING circuit - {_failureCount} failures");
            _state = CircuitState.Open;
            _openedAt = DateTime.UtcNow;
        }
    }
    
    private enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
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

public class MetricsSnapshot
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
