namespace MafiaRules.Domain;

/// <summary>
/// Represents a message sent between agents in the hierarchy.
/// </summary>
public class AgentMessage
{
    public string Id { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public string FromAgentId { get; set; } = string.Empty;
    public string ToAgentId { get; set; } = string.Empty;
    public string? TerritoryId { get; set; }
    public object? Payload { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Scope { get; set; }
    public bool Blocked { get; set; }
    public string? BlockReason { get; set; }
    public List<string> Flags { get; set; } = new();
    public List<string> RequiredCapabilities { get; set; } = new();

    // Navigation properties (resolved during evaluation)
    public Agent? From { get; set; }
    public Agent? To { get; set; }
    public Territory? Territory { get; set; }

    /// <summary>
    /// Adds a flag to this message for tracking/audit purposes.
    /// </summary>
    public void Flag(string flag) => Flags.Add(flag);

    /// <summary>
    /// Reroutes this message to a different target agent.
    /// </summary>
    public void Reroute(Agent newTarget)
    {
        ToAgentId = newTarget.Id;
        To = newTarget;
    }

    /// <summary>
    /// Routes this message to a specific target agent.
    /// </summary>
    public void RouteTo(Agent target)
    {
        ToAgentId = target.Id;
        To = target;
    }

    /// <summary>
    /// Escalates this message to a higher authority.
    /// </summary>
    public void EscalateTo(Agent target)
    {
        ToAgentId = target.Id;
        To = target;
        Flag("escalated");
    }
}
