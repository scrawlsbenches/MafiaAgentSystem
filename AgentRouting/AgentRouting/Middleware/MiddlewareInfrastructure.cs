using AgentRouting.Core;

namespace AgentRouting.Middleware;

/// <summary>
/// Delegate for the next middleware in the pipeline
/// </summary>
public delegate Task<MessageResult> MessageDelegate(AgentMessage message, CancellationToken ct);

/// <summary>
/// Base interface for all middleware components
/// </summary>
public interface IAgentMiddleware
{
    /// <summary>
    /// Process the message and optionally call the next middleware
    /// </summary>
    Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct = default);
}

/// <summary>
/// Builder for composing middleware pipeline
/// </summary>
public class MiddlewarePipeline
{
    private readonly List<IAgentMiddleware> _middleware = new();
    
    /// <summary>
    /// Add middleware to the pipeline
    /// </summary>
    public MiddlewarePipeline Use(IAgentMiddleware middleware)
    {
        _middleware.Add(middleware);
        return this;
    }
    
    /// <summary>
    /// Add middleware using a factory function
    /// </summary>
    public MiddlewarePipeline Use(Func<MessageDelegate, MessageDelegate> middleware)
    {
        _middleware.Add(new DelegateMiddleware(middleware));
        return this;
    }
    
    /// <summary>
    /// Build the complete pipeline
    /// </summary>
    public MessageDelegate Build(MessageDelegate finalHandler)
    {
        MessageDelegate pipeline = finalHandler;
        
        // Build pipeline in reverse order (last middleware wraps the handler)
        for (int i = _middleware.Count - 1; i >= 0; i--)
        {
            var currentMiddleware = _middleware[i];
            var next = pipeline;
            
            pipeline = async (message, ct) =>
            {
                return await currentMiddleware.InvokeAsync(message, next, ct);
            };
        }
        
        return pipeline;
    }
    
    /// <summary>
    /// Helper class for delegate-based middleware
    /// </summary>
    private class DelegateMiddleware : IAgentMiddleware
    {
        private readonly Func<MessageDelegate, MessageDelegate> _middleware;
        
        public DelegateMiddleware(Func<MessageDelegate, MessageDelegate> middleware)
        {
            _middleware = middleware;
        }
        
        public Task<MessageResult> InvokeAsync(
            AgentMessage message,
            MessageDelegate next,
            CancellationToken ct = default)
        {
            var wrappedNext = _middleware(next);
            return wrappedNext(message, ct);
        }
    }
}

/// <summary>
/// Router with middleware support
/// </summary>
public class MiddlewareAgentRouter : AgentRouter
{
    private readonly MiddlewarePipeline _pipeline;
    private MessageDelegate? _builtPipeline;
    
    public MiddlewareAgentRouter(IAgentLogger logger) : base(logger)
    {
        _pipeline = new MiddlewarePipeline();
    }
    
    /// <summary>
    /// Add middleware to the routing pipeline
    /// </summary>
    public MiddlewareAgentRouter UseMiddleware(IAgentMiddleware middleware)
    {
        _pipeline.Use(middleware);
        _builtPipeline = null; // Invalidate cached pipeline
        return this;
    }
    
    /// <summary>
    /// Add middleware using a delegate
    /// </summary>
    public MiddlewareAgentRouter UseMiddleware(Func<MessageDelegate, MessageDelegate> middleware)
    {
        _pipeline.Use(middleware);
        _builtPipeline = null;
        return this;
    }
    
    /// <summary>
    /// Route message through middleware pipeline
    /// </summary>
    public new async Task<MessageResult> RouteMessageAsync(
        AgentMessage message,
        CancellationToken ct = default)
    {
        // Build pipeline on first use
        if (_builtPipeline == null)
        {
            _builtPipeline = _pipeline.Build(
                async (msg, token) => await base.RouteMessageAsync(msg, token)
            );
        }
        
        return await _builtPipeline(message, ct);
    }
}

/// <summary>
/// Base class for middleware with common functionality
/// </summary>
public abstract class MiddlewareBase : IAgentMiddleware
{
    public abstract Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct = default);
    
    /// <summary>
    /// Create a successful result and continue to next middleware
    /// </summary>
    protected async Task<MessageResult> ContinueAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        return await next(message, ct);
    }
    
    /// <summary>
    /// Short-circuit the pipeline with a result
    /// </summary>
    protected MessageResult ShortCircuit(string reason)
    {
        return MessageResult.Fail(reason);
    }
}

/// <summary>
/// Context object for middleware to share data
/// </summary>
public class MiddlewareContext
{
    private readonly Dictionary<string, object> _items = new();
    
    public T? Get<T>(string key)
    {
        return _items.TryGetValue(key, out var value) ? (T)value : default;
    }
    
    public void Set<T>(string key, T value)
    {
        if (value != null)
        {
            _items[key] = value;
        }
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
/// Extensions for attaching context to messages
/// </summary>
public static class MessageContextExtensions
{
    private const string ContextKey = "__MiddlewareContext__";
    
    public static MiddlewareContext GetContext(this AgentMessage message)
    {
        if (!message.Metadata.TryGetValue(ContextKey, out var context))
        {
            context = new MiddlewareContext();
            message.Metadata[ContextKey] = context;
        }
        return (MiddlewareContext)context;
    }
}
