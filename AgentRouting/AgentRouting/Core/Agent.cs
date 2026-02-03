using System.Collections.Concurrent;

namespace AgentRouting.Core;

/// <summary>
/// Represents a message that can be sent between agents
/// </summary>
public class AgentMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SenderId { get; set; } = "";
    public string ReceiverId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Content { get; set; } = "";
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;
    public string Category { get; set; } = "";
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ConversationId { get; set; }
    public string? ReplyToMessageId { get; set; }
    
    public override string ToString()
    {
        return $"[{Priority}] {Subject} from {SenderId} to {ReceiverId}";
    }
}

public enum MessagePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>
/// Represents the result of processing a message
/// </summary>
public class MessageResult
{
    public bool Success { get; set; }
    public string? Response { get; set; }
    public List<AgentMessage> ForwardedMessages { get; set; } = new();
    public string? Error { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    
    public static MessageResult Ok(string? response = null)
    {
        return new MessageResult { Success = true, Response = response };
    }
    
    public static MessageResult Fail(string error)
    {
        return new MessageResult { Success = false, Error = error };
    }
    
    public static MessageResult Forward(AgentMessage message, string? response = null)
    {
        return new MessageResult 
        { 
            Success = true, 
            Response = response,
            ForwardedMessages = new List<AgentMessage> { message }
        };
    }
}

/// <summary>
/// Core agent interface
/// </summary>
public interface IAgent
{
    string Id { get; }
    string Name { get; }
    AgentCapabilities Capabilities { get; }
    AgentStatus Status { get; }
    
    Task<MessageResult> ProcessMessageAsync(AgentMessage message, CancellationToken ct = default);
    bool CanHandle(AgentMessage message);
}

/// <summary>
/// Capabilities that an agent can advertise
/// </summary>
public class AgentCapabilities
{
    public List<string> SupportedCategories { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public int MaxConcurrentMessages { get; set; } = 10;
    public List<string> RequiredMetadata { get; set; } = new();
    
    public bool SupportsCategory(string category)
    {
        return SupportedCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
    }
    
    public bool HasSkill(string skill)
    {
        return Skills.Contains(skill, StringComparer.OrdinalIgnoreCase);
    }
}

public enum AgentStatus
{
    Available,
    Busy,
    Offline,
    Error
}

/// <summary>
/// Base agent implementation with common functionality
/// </summary>
public abstract class AgentBase : IAgent
{
    private readonly ConcurrentQueue<AgentMessage> _messageQueue = new();
    private int _activeMessages = 0;
    
    public string Id { get; }
    public string Name { get; }
    public AgentCapabilities Capabilities { get; protected set; }
    public AgentStatus Status { get; protected set; } = AgentStatus.Available;
    
    protected IAgentLogger Logger { get; }
    
    protected AgentBase(string id, string name, IAgentLogger logger)
    {
        Id = id;
        Name = name;
        Logger = logger;
        Capabilities = new AgentCapabilities();
    }
    
    public virtual bool CanHandle(AgentMessage message)
    {
        // Check if we support this category
        if (!string.IsNullOrEmpty(message.Category) &&
            Capabilities.SupportedCategories.Any() &&
            !Capabilities.SupportsCategory(message.Category))
        {
            return false;
        }

        // Check if we're at capacity (advisory - actual enforcement is in ProcessMessageAsync)
        if (_activeMessages >= Capabilities.MaxConcurrentMessages)
        {
            return false;
        }

        // Check if offline
        if (Status == AgentStatus.Offline || Status == AgentStatus.Error)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Atomically tries to acquire a processing slot.
    /// Returns true if slot acquired, false if at capacity.
    /// </summary>
    private bool TryAcquireSlot()
    {
        while (true)
        {
            int current = _activeMessages;
            if (current >= Capabilities.MaxConcurrentMessages)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _activeMessages, current + 1, current) == current)
            {
                return true;
            }
            // CAS failed, another thread modified _activeMessages, retry
        }
    }

    public async Task<MessageResult> ProcessMessageAsync(
        AgentMessage message,
        CancellationToken ct = default)
    {
        // Check semantic constraints first (category, status)
        if (!string.IsNullOrEmpty(message.Category) &&
            Capabilities.SupportedCategories.Any() &&
            !Capabilities.SupportsCategory(message.Category))
        {
            return MessageResult.Fail($"Agent {Name} does not support category: {message.Category}");
        }

        if (Status == AgentStatus.Offline || Status == AgentStatus.Error)
        {
            return MessageResult.Fail($"Agent {Name} is {Status}");
        }

        // Atomically acquire a slot (enforces capacity)
        if (!TryAcquireSlot())
        {
            return MessageResult.Fail($"Agent {Name} is at capacity");
        }

        try
        {
            Status = _activeMessages >= Capabilities.MaxConcurrentMessages
                ? AgentStatus.Busy
                : AgentStatus.Available;
            
            Logger.LogMessageReceived(this, message);
            
            var result = await HandleMessageAsync(message, ct);
            
            Logger.LogMessageProcessed(this, message, result);
            
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(this, message, ex);
            return MessageResult.Fail($"Error processing message: {ex.Message}");
        }
        finally
        {
            Interlocked.Decrement(ref _activeMessages);
            Status = AgentStatus.Available;
        }
    }
    
    /// <summary>
    /// Derived classes implement their specific message handling logic
    /// </summary>
    protected abstract Task<MessageResult> HandleMessageAsync(
        AgentMessage message, 
        CancellationToken ct);
    
    /// <summary>
    /// Helper to create a reply message
    /// </summary>
    protected AgentMessage CreateReply(AgentMessage originalMessage, string content)
    {
        return new AgentMessage
        {
            SenderId = Id,
            ReceiverId = originalMessage.SenderId,
            Subject = $"Re: {originalMessage.Subject}",
            Content = content,
            Category = originalMessage.Category,
            ConversationId = originalMessage.ConversationId ?? originalMessage.Id,
            ReplyToMessageId = originalMessage.Id,
            Priority = originalMessage.Priority
        };
    }
    
    /// <summary>
    /// Helper to forward a message to another agent
    /// </summary>
    protected AgentMessage ForwardMessage(AgentMessage message, string toAgentId, string? note = null)
    {
        return new AgentMessage
        {
            SenderId = Id,
            ReceiverId = toAgentId,
            Subject = $"Fwd: {message.Subject}",
            Content = note != null ? $"{note}\n\n--- Forwarded ---\n{message.Content}" : message.Content,
            Category = message.Category,
            ConversationId = message.ConversationId ?? message.Id,
            Priority = message.Priority,
            Metadata = new Dictionary<string, object>(message.Metadata)
            {
                ["OriginalSender"] = message.SenderId,
                ["ForwardedBy"] = Id
            }
        };
    }
}

/// <summary>
/// Logger interface for agent activities
/// </summary>
public interface IAgentLogger
{
    void LogMessageReceived(IAgent agent, AgentMessage message);
    void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result);
    void LogMessageRouted(AgentMessage message, IAgent? fromAgent, IAgent toAgent);
    void LogError(IAgent agent, AgentMessage message, Exception ex);
}

/// <summary>
/// Console logger implementation
/// </summary>
public class ConsoleAgentLogger : IAgentLogger
{
    private readonly object _lock = new();
    
    public void LogMessageReceived(IAgent agent, AgentMessage message)
    {
        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {agent.Name} received: {message.Subject}");
            Console.ResetColor();
        }
    }
    
    public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result)
    {
        lock (_lock)
        {
            Console.ForegroundColor = result.Success ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {agent.Name} processed: {message.Subject} - {(result.Success ? "SUCCESS" : "FAILED")}");
            if (!string.IsNullOrEmpty(result.Response))
            {
                Console.WriteLine($"  Response: {result.Response}");
            }
            Console.ResetColor();
        }
    }
    
    public void LogMessageRouted(AgentMessage message, IAgent? fromAgent, IAgent toAgent)
    {
        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Routed: {message.Subject}");
            Console.WriteLine($"  From: {fromAgent?.Name ?? "Router"} → To: {toAgent.Name}");
            Console.ResetColor();
        }
    }
    
    public void LogError(IAgent agent, AgentMessage message, Exception ex)
    {
        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] ERROR in {agent.Name}: {ex.Message}");
            Console.ResetColor();
        }
    }
}
