namespace MafiaRules.Domain;

/// <summary>
/// Represents a member of a mafia family with a role in the hierarchy.
/// </summary>
public class Agent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AgentRole Role { get; set; }
    public AgentStatus Status { get; set; }
    public string FamilyId { get; set; } = string.Empty;
    public string SuperiorId { get; set; } = string.Empty;
    public int CurrentTaskCount { get; set; }
    public double ReputationScore { get; set; }
    public List<string> Capabilities { get; set; } = new();

    // Navigation properties (resolved during evaluation)
    public Agent? Superior { get; set; }
    public List<Agent> Subordinates { get; set; } = new();

    /// <summary>
    /// Gets the hierarchy level based on role.
    /// Higher values indicate more authority.
    /// </summary>
    public int HierarchyLevel => Role switch
    {
        AgentRole.Soldier => 1,
        AgentRole.Capo => 2,
        AgentRole.Underboss => 3,
        AgentRole.Consigliere => 3,
        AgentRole.Godfather => 4,
        _ => 0
    };
}
