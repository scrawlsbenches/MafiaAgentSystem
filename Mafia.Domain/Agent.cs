// NOTE: These domain objects are for internal use by RulesEngine.Linq tests.
// Feel free to add properties as needed for testing scenarios.

namespace Mafia.Domain;

/// <summary>
/// An agent in the family hierarchy.
/// </summary>
public class Agent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AgentRole Role { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Available;
    public string FamilyId { get; set; } = string.Empty;
    public string? SuperiorId { get; set; }
    public string? CapoId { get; set; } // For soldiers: their capo
    public int CurrentTaskCount { get; set; }
    public double ReputationScore { get; set; } = 1.0;
    public HashSet<string> Capabilities { get; set; } = new();
    public string? TerritoryId { get; set; } // Which territory this agent is assigned to

    // Navigation (resolved by context)
    public Agent? Superior { get; set; }
    public Family? Family { get; set; }
}
