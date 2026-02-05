// NOTE: These domain objects are for internal use by RulesEngine.Linq tests.
// Feel free to add properties as needed for testing scenarios.

namespace Mafia.Domain;

/// <summary>
/// A message between agents - the primary fact type for rules.
/// </summary>
public class AgentMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MessageType Type { get; set; }
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Content { get; set; }
    public Dictionary<string, object> Payload { get; set; } = new();
    public HashSet<string> RequiredCapabilities { get; set; } = new();
    public string? Scope { get; set; } // For broadcasts
    public int Priority { get; set; } // For rule-based prioritization

    // Mutable state (modified by rules)
    public bool Blocked { get; set; }
    public string? BlockReason { get; set; }
    public string? ReroutedToId { get; set; }
    public HashSet<string> Flags { get; set; } = new();
    public string? EscalatedToId { get; set; }

    // Navigation (resolved by context)
    public Agent? From { get; set; }
    public Agent? To { get; set; }
    public Agent? ReroutedTo { get; set; }
    public Agent? EscalatedTo { get; set; }

    // Fluent mutation methods for rules
    public void Block(string reason)
    {
        Blocked = true;
        BlockReason = reason;
    }

    public void Reroute(Agent target)
    {
        ReroutedToId = target.Id;
        ReroutedTo = target;
    }

    public void RouteTo(Agent target)
    {
        ToId = target.Id;
        To = target;
    }

    public void Flag(string flag) => Flags.Add(flag);

    public void EscalateTo(Agent target)
    {
        EscalatedToId = target.Id;
        EscalatedTo = target;
    }
}
