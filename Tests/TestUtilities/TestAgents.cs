using AgentRouting.Core;
using AgentRouting.Agents;

namespace TestUtilities;

/// <summary>
/// A simple test agent that implements IAgent directly.
/// Supports configurable categories and a custom handler.
/// </summary>
public class SimpleTestAgent : IAgent
{
    private readonly Func<AgentMessage, MessageResult>? _handler;

    public string Id { get; }
    public string Name { get; }
    public AgentCapabilities Capabilities { get; } = new();
    public AgentStatus Status { get; set; } = AgentStatus.Available;

    public SimpleTestAgent(string id, string name, params string[] categories)
        : this(id, name, null, categories) { }

    public SimpleTestAgent(string id, string name, Func<AgentMessage, MessageResult>? handler, params string[] categories)
    {
        Id = id;
        Name = name;
        _handler = handler;
        Capabilities.SupportedCategories.AddRange(categories);
    }

    public bool CanHandle(AgentMessage message) =>
        Capabilities.SupportedCategories.Count == 0 ||
        Capabilities.SupportedCategories.Contains(message.Category);

    public Task<MessageResult> ProcessMessageAsync(AgentMessage message, CancellationToken ct)
        => Task.FromResult(_handler?.Invoke(message) ?? MessageResult.Ok($"Processed by {Name}"));
}

/// <summary>
/// A test agent that extends AgentBase and tracks received messages.
/// </summary>
public class TrackingTestAgent : AgentBase
{
    private readonly Func<AgentMessage, MessageResult>? _handler;

    public List<AgentMessage> ReceivedMessages { get; } = new();

    public TrackingTestAgent(string id, string name, Func<AgentMessage, MessageResult>? handler = null)
        : base(id, name, SilentAgentLogger.Instance)
    {
        _handler = handler;
    }

    protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
    {
        ReceivedMessages.Add(message);
        return Task.FromResult(_handler?.Invoke(message) ?? MessageResult.Ok($"Handled by {Name}"));
    }
}

/// <summary>
/// A test agent that supports category-based routing.
/// </summary>
public class CategoryTestAgent : AgentBase
{
    public CategoryTestAgent(string id, string name, params string[] categories)
        : base(id, name, SilentAgentLogger.Instance)
    {
        foreach (var cat in categories)
        {
            Capabilities.SupportedCategories.Add(cat);
        }
    }

    protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
    {
        return Task.FromResult(MessageResult.Ok($"Handled {message.Category} by {Name}"));
    }
}

/// <summary>
/// A test agent that always throws an exception.
/// Useful for testing error handling.
/// </summary>
public class ThrowingTestAgent : AgentBase
{
    private readonly Exception _exception;

    public ThrowingTestAgent(string id, string name, Exception? exception = null)
        : base(id, name, SilentAgentLogger.Instance)
    {
        _exception = exception ?? new InvalidOperationException("Agent threw exception");
    }

    protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
    {
        throw _exception;
    }
}

/// <summary>
/// A test agent that captures message metadata for verification.
/// </summary>
public class MetadataCapturingTestAgent : AgentBase
{
    public Dictionary<string, object> CapturedMetadata { get; } = new();

    public MetadataCapturingTestAgent(string id, string name)
        : base(id, name, SilentAgentLogger.Instance) { }

    protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
    {
        foreach (var kvp in message.Metadata)
        {
            CapturedMetadata[kvp.Key] = kvp.Value;
        }
        return Task.FromResult(MessageResult.Ok("Captured"));
    }
}

/// <summary>
/// A lightweight test agent for benchmarking and performance tests.
/// </summary>
public class BenchmarkTestAgent : AgentBase
{
    public BenchmarkTestAgent(string id, string name)
        : base(id, name, SilentAgentLogger.Instance) { }

    protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
        => Task.FromResult(MessageResult.Ok());
}
