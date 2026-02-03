// Story System - Agent Response
// Response to a question, crafted based on persona and knowledge

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Response to a question, crafted based on persona and knowledge.
/// </summary>
public class AgentResponse
{
    public string QuestionId { get; set; } = "";
    public string ResponderId { get; set; } = "";
    public ResponseType Type { get; set; }

    public string Content { get; set; } = "";           // The actual response
    public bool IsHonest { get; set; } = true;          // Is this truthful?
    public int ConfidenceLevel { get; set; } = 100;     // How confident in the info

    // Shared information
    public List<Memory> SharedMemories { get; set; } = new();
    public Intel? SharedIntel { get; set; }

    // Relationship effects
    public int RelationshipChange { get; set; }         // How this affects relationship
    public bool RefusedToAnswer { get; set; }
    public string? RefusalReason { get; set; }

    // Story effects
    public List<string> UnlockedNodeIds { get; set; } = new();   // Story nodes unlocked
    public List<string> TriggeredEvents { get; set; } = new();    // Events triggered
}
