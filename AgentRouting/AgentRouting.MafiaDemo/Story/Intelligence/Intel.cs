// Story System - Intel
// Represents information that agents share about the world

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Intel represents information that agents share about the world.
///
/// DESIGN DECISION: Intel has a reliability score and expiration.
/// This lets us model uncertainty and information decay naturally.
/// Agents at different levels have different intel gathering abilities.
/// </summary>
public class Intel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public IntelType Type { get; set; }
    public string SubjectId { get; set; } = "";     // Location, NPC, or Faction ID
    public string SubjectType { get; set; } = "";   // "location", "npc", "faction"

    // Content
    public string Summary { get; set; } = "";       // Human-readable summary
    public Dictionary<string, object> Data { get; set; } = new();

    // Metadata
    public string SourceAgentId { get; set; } = "";
    public int Reliability { get; set; }            // 0-100
    public int GatheredWeek { get; set; }
    public int? ExpiresWeek { get; set; }

    // Processing
    public bool IsProcessed { get; set; }
    public bool IsActedUpon { get; set; }

    public bool IsExpired(int currentWeek) =>
        ExpiresWeek.HasValue && currentWeek > ExpiresWeek.Value;

    public bool IsReliable => Reliability >= 75;
}
