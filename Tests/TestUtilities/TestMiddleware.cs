using AgentRouting.Core;
using AgentRouting.Middleware;

namespace TestUtilities;

/// <summary>
/// Middleware that captures messages for verification.
/// </summary>
public class MessageCapturingMiddleware : MiddlewareBase
{
    private readonly Action<AgentMessage> _capture;

    public MessageCapturingMiddleware(Action<AgentMessage> capture)
    {
        _capture = capture;
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        _capture(message);
        return await next(message, ct);
    }
}

/// <summary>
/// Middleware that executes callbacks before and/or after the pipeline.
/// </summary>
public class CallbackMiddleware : MiddlewareBase
{
    private readonly Action? _before;
    private readonly Action? _after;

    public CallbackMiddleware(Action? before = null, Action? after = null)
    {
        _before = before;
        _after = after;
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        _before?.Invoke();
        var result = await next(message, ct);
        _after?.Invoke();
        return result;
    }
}

/// <summary>
/// Middleware that tracks execution order using numeric IDs.
/// Records positive ID before pipeline, negative ID after.
/// </summary>
public class OrderTrackingMiddleware : MiddlewareBase
{
    private readonly int _id;
    private readonly List<int> _log;

    public OrderTrackingMiddleware(int id, List<int> log)
    {
        _id = id;
        _log = log;
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        _log.Add(_id); // Before (positive)
        var result = await next(message, ct);
        _log.Add(-_id); // After (negative)
        return result;
    }
}

/// <summary>
/// Middleware that tracks execution order using string names.
/// </summary>
public class NamedTrackingMiddleware : MiddlewareBase
{
    private readonly string _name;
    private readonly List<string> _order;

    public NamedTrackingMiddleware(string name, List<string> order)
    {
        _name = name;
        _order = order;
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        _order.Add($"{_name}-Before");
        var result = await next(message, ct);
        _order.Add($"{_name}-After");
        return result;
    }
}

/// <summary>
/// Middleware that short-circuits the pipeline with an error.
/// </summary>
public class ShortCircuitMiddleware : MiddlewareBase
{
    private readonly string _error;

    public ShortCircuitMiddleware(string error)
    {
        _error = error;
    }

    public override Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        return Task.FromResult(ShortCircuit(_error));
    }
}

/// <summary>
/// Middleware that throws an exception.
/// Useful for testing error handling.
/// </summary>
public class ThrowingMiddleware : MiddlewareBase
{
    private readonly Exception _exception;

    public ThrowingMiddleware() : this(new InvalidOperationException("Middleware exception")) { }

    public ThrowingMiddleware(Exception exception)
    {
        _exception = exception;
    }

    public override Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        throw _exception;
    }
}

/// <summary>
/// Middleware that throws an exception in the "after" phase.
/// </summary>
public class AfterThrowingMiddleware : MiddlewareBase
{
    private readonly Exception _exception;

    public AfterThrowingMiddleware() : this(new InvalidOperationException("After phase exception")) { }

    public AfterThrowingMiddleware(Exception exception)
    {
        _exception = exception;
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        var result = await next(message, ct);
        throw _exception;
    }
}

/// <summary>
/// Middleware that modifies the response.
/// </summary>
public class ResultModifyingMiddleware : MiddlewareBase
{
    private readonly string _newResponse;

    public ResultModifyingMiddleware(string newResponse)
    {
        _newResponse = newResponse;
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        var result = await next(message, ct);
        result.Response = _newResponse;
        return result;
    }
}

/// <summary>
/// Middleware that counts invocations.
/// </summary>
public class CountingMiddleware : MiddlewareBase
{
    public int InvokeCount { get; private set; }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        InvokeCount++;
        return await next(message, ct);
    }

    public void Reset() => InvokeCount = 0;
}

/// <summary>
/// Middleware that checks cancellation before proceeding.
/// </summary>
public class CancellationCheckingMiddleware : MiddlewareBase
{
    public override Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return next(message, ct);
    }
}

/// <summary>
/// Middleware that sets a value in the message metadata.
/// </summary>
public class ContextSettingMiddleware : MiddlewareBase
{
    private readonly string _key;
    private readonly object _value;

    public ContextSettingMiddleware(string key, object value)
    {
        _key = key;
        _value = value;
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        message.Metadata[_key] = _value;
        return await next(message, ct);
    }
}

/// <summary>
/// Middleware that reads a value from the message metadata and stores its existence in the result.
/// </summary>
public class ContextReadingMiddleware : MiddlewareBase
{
    private readonly string _key;

    public ContextReadingMiddleware(string key)
    {
        _key = key;
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        var exists = message.Metadata.ContainsKey(_key);
        var result = await next(message, ct);
        result.Data["context_key_exists"] = exists;
        return result;
    }
}

/// <summary>
/// Middleware that adds metadata to the message.
/// Alias for ContextSettingMiddleware for clarity.
/// </summary>
public class MetadataAddingMiddleware : ContextSettingMiddleware
{
    public MetadataAddingMiddleware(string key, object value) : base(key, value) { }
}

/// <summary>
/// Middleware that executes an action only when a condition is met.
/// </summary>
public class ConditionalMiddleware : MiddlewareBase
{
    private readonly Func<AgentMessage, bool> _condition;
    private readonly Action _action;

    public ConditionalMiddleware(Func<AgentMessage, bool> condition, Action action)
    {
        _condition = condition;
        _action = action;
    }

    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct)
    {
        if (_condition(message))
        {
            _action();
        }
        return await next(message, ct);
    }
}
