using System.Collections.Concurrent;
using AgentRouting.Core;

namespace AgentRouting.Middleware;

/// <summary>
/// Delegate representing the next middleware in the pipeline
/// </summary>
public delegate Task<MessageResult> MessageDelegate(AgentMessage message, CancellationToken ct);

/// <summary>
/// Core middleware interface
/// </summary>
public interface IMessageMiddleware
{
    /// <summary>
    /// Process a message and optionally call the next middleware
    /// </summary>
    Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct = default);
}

/// <summary>
/// Builds and executes a middleware pipeline
/// </summary>
public class MiddlewarePipeline
{
    private readonly List<IMessageMiddleware> _middleware = new();
    private MessageDelegate? _compiledPipeline;
    
    /// <summary>
    /// Add middleware to the end of the pipeline
    /// </summary>
    public MiddlewarePipeline Use(IMessageMiddleware middleware)
    {
        _middleware.Add(middleware);
        _compiledPipeline = null; // Invalidate compiled pipeline
        return this;
    }
    
    /// <summary>
    /// Add middleware using a factory function
    /// </summary>
    public MiddlewarePipeline Use(Func<MessageDelegate, MessageDelegate> middlewareFactory)
    {
        return Use(new DelegateMiddleware(middlewareFactory));
    }
    
    /// <summary>
    /// Build the final pipeline with the terminal handler
    /// </summary>
    public MessageDelegate Build(MessageDelegate terminalHandler)
    {
        if (_compiledPipeline != null)
            return _compiledPipeline;
        
        MessageDelegate pipeline = terminalHandler;
        
        // Build pipeline in reverse order
        for (int i = _middleware.Count - 1; i >= 0; i--)
        {
            var middleware = _middleware[i];
            var next = pipeline;
            
            // Capture variables for closure
            pipeline = (msg, ct) => middleware.InvokeAsync(msg, next, ct);
        }
        
        _compiledPipeline = pipeline;
        return _compiledPipeline;
    }
    
    /// <summary>
    /// Execute the pipeline with a message
    /// </summary>
    public async Task<MessageResult> ExecuteAsync(
        AgentMessage message,
        MessageDelegate terminalHandler,
        CancellationToken ct = default)
    {
        var pipeline = Build(terminalHandler);
        return await pipeline(message, ct);
    }
    
    /// <summary>
    /// Get all middleware in the pipeline
    /// </summary>
    public IReadOnlyList<IMessageMiddleware> GetMiddleware() => _middleware.AsReadOnly();
}

/// <summary>
/// Middleware that wraps a delegate
/// </summary>
internal class DelegateMiddleware : IMessageMiddleware
{
    private readonly Func<MessageDelegate, MessageDelegate> _factory;
    
    public DelegateMiddleware(Func<MessageDelegate, MessageDelegate> factory)
    {
        _factory = factory;
    }
    
    public Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        var wrappedNext = _factory(next);
        return wrappedNext(message, ct);
    }
}

/// <summary>
/// Base class for middleware with common functionality
/// </summary>
public abstract class MiddlewareBase : IMessageMiddleware
{
    public abstract Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct = default);
    
    /// <summary>
    /// Helper to short-circuit the pipeline
    /// </summary>
    protected MessageResult ShortCircuit(string error)
    {
        return MessageResult.Fail(error);
    }
    
    /// <summary>
    /// Helper to modify result after processing
    /// </summary>
    protected async Task<MessageResult> ModifyResult(
        AgentMessage message,
        MessageDelegate next,
        Func<MessageResult, MessageResult> modifier,
        CancellationToken ct)
    {
        var result = await next(message, ct);
        return modifier(result);
    }
}

/// <summary>
/// Context that can be shared across middleware
/// </summary>
public class MiddlewareContext
{
    private readonly ConcurrentDictionary<string, object> _items = new();
    
    public T? Get<T>(string key)
    {
        return _items.TryGetValue(key, out var value) ? (T)value : default;
    }
    
    public void Set<T>(string key, T value) where T : notnull
    {
        _items[key] = value;
    }
    
    public bool TryGet<T>(string key, out T? value)
    {
        if (_items.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        
        value = default;
        return false;
    }
}

/// <summary>
/// Extension methods for fluent middleware configuration
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// Add middleware when a condition is met
    /// </summary>
    public static MiddlewarePipeline UseWhen(
        this MiddlewarePipeline pipeline,
        Func<AgentMessage, bool> predicate,
        IMessageMiddleware middleware)
    {
        return pipeline.Use(new ConditionalMiddleware(predicate, middleware));
    }
    
    /// <summary>
    /// Add simple before/after actions as middleware
    /// </summary>
    public static MiddlewarePipeline UseCallback(
        this MiddlewarePipeline pipeline,
        Action<AgentMessage>? before = null,
        Action<AgentMessage, MessageResult>? after = null)
    {
        return pipeline.Use(new CallbackMiddleware(before, after));
    }
}

/// <summary>
/// Middleware that only executes if a condition is met
/// </summary>
public class ConditionalMiddleware : MiddlewareBase
{
    private readonly Func<AgentMessage, bool> _predicate;
    private readonly IMessageMiddleware _middleware;
    
    public ConditionalMiddleware(
        Func<AgentMessage, bool> predicate,
        IMessageMiddleware middleware)
    {
        _predicate = predicate;
        _middleware = middleware;
    }
    
    public override Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        if (_predicate(message))
        {
            return _middleware.InvokeAsync(message, next, ct);
        }
        
        return next(message, ct);
    }
}

/// <summary>
/// Simple middleware for callbacks
/// </summary>
public class CallbackMiddleware : MiddlewareBase
{
    private readonly Action<AgentMessage>? _before;
    private readonly Action<AgentMessage, MessageResult>? _after;
    
    public CallbackMiddleware(
        Action<AgentMessage>? before = null,
        Action<AgentMessage, MessageResult>? after = null)
    {
        _before = before;
        _after = after;
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        _before?.Invoke(message);
        
        var result = await next(message, ct);
        
        _after?.Invoke(message, result);
        
        return result;
    }
}
