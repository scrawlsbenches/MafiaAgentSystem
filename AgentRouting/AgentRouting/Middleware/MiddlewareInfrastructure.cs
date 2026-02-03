using System.Collections.Concurrent;
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
/// Interface for middleware pipeline composition.
/// Allows building a chain of middleware that process messages.
/// </summary>
public interface IMiddlewarePipeline
{
    /// <summary>
    /// Returns true if any middleware has been added to the pipeline.
    /// </summary>
    bool HasMiddleware { get; }

    /// <summary>
    /// Add middleware to the pipeline.
    /// </summary>
    IMiddlewarePipeline Use(IAgentMiddleware middleware);

    /// <summary>
    /// Add middleware using a factory function.
    /// </summary>
    IMiddlewarePipeline Use(Func<MessageDelegate, MessageDelegate> middleware);

    /// <summary>
    /// Build the complete pipeline with a final handler.
    /// </summary>
    MessageDelegate Build(MessageDelegate finalHandler);
}

/// <summary>
/// Builder for composing middleware pipeline
/// </summary>
public class MiddlewarePipeline : IMiddlewarePipeline
{
    private readonly List<IAgentMiddleware> _middleware = new();

    /// <summary>
    /// Returns true if any middleware has been added to the pipeline.
    /// </summary>
    public bool HasMiddleware => _middleware.Count > 0;

    /// <summary>
    /// Add middleware to the pipeline
    /// </summary>
    public IMiddlewarePipeline Use(IAgentMiddleware middleware)
    {
        _middleware.Add(middleware);
        return this;
    }

    /// <summary>
    /// Add middleware using a factory function
    /// </summary>
    public IMiddlewarePipeline Use(Func<MessageDelegate, MessageDelegate> middleware)
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
/// Context object for middleware to share data.
/// Thread-safe implementation using ConcurrentDictionary.
/// </summary>
public class MiddlewareContext : IMiddlewareContext
{
    private readonly ConcurrentDictionary<string, object> _items = new();

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

/// <summary>
/// Middleware that allows specifying before/after callbacks.
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
        CancellationToken ct = default)
    {
        _before?.Invoke(message);
        var result = await next(message, ct);
        _after?.Invoke(message, result);
        return result;
    }
}

/// <summary>
/// Middleware that only executes when a condition is met.
/// </summary>
public class ConditionalMiddleware : MiddlewareBase
{
    private readonly Func<AgentMessage, bool> _condition;
    private readonly IAgentMiddleware _innerMiddleware;

    public ConditionalMiddleware(Func<AgentMessage, bool> condition, IAgentMiddleware innerMiddleware)
    {
        _condition = condition;
        _innerMiddleware = innerMiddleware;
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct = default)
    {
        if (_condition(message))
        {
            return await _innerMiddleware.InvokeAsync(message, next, ct);
        }
        return await next(message, ct);
    }
}

/// <summary>
/// Extension methods for MiddlewarePipeline convenience.
/// </summary>
public static class MiddlewarePipelineExtensions
{
    /// <summary>
    /// Add middleware that only executes when the condition is met.
    /// </summary>
    public static IMiddlewarePipeline UseWhen(
        this IMiddlewarePipeline pipeline,
        Func<AgentMessage, bool> condition,
        IAgentMiddleware middleware)
    {
        return pipeline.Use(new ConditionalMiddleware(condition, middleware));
    }

    /// <summary>
    /// Add a callback middleware with before/after actions.
    /// </summary>
    public static IMiddlewarePipeline UseCallback(
        this IMiddlewarePipeline pipeline,
        Action<AgentMessage>? before = null,
        Action<AgentMessage, MessageResult>? after = null)
    {
        return pipeline.Use(new CallbackMiddleware(before, after));
    }

    /// <summary>
    /// Execute the pipeline with the given terminal handler.
    /// </summary>
    public static async Task<MessageResult> ExecuteAsync(
        this IMiddlewarePipeline pipeline,
        AgentMessage message,
        MessageDelegate terminalHandler,
        CancellationToken ct = default)
    {
        var executor = pipeline.Build(terminalHandler);
        return await executor(message, ct);
    }
}
