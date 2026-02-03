// Story System - NPC Entity
// Represents a non-player character with persistent relationships

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Represents a non-player character with persistent relationships.
/// NPCs remember how the player treated them and react accordingly.
///
/// DESIGN DECISION: Relationship is a single int (-100 to 100) rather than
/// a complex multi-dimensional model. This keeps rules simple while still
/// allowing nuanced behavior through status + relationship combinations.
/// </summary>
public class NPC
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";          // "Tony Marinelli"
    public string Role { get; set; } = "";          // "restaurant_owner", "dock_worker"
    public string? Title { get; set; }              // "the shopkeeper", "the informant"

    // Location binding
    public string LocationId { get; set; } = "";
    public string? FactionId { get; set; }          // Which faction they belong to

    // Relationship state
    public int Relationship { get; set; } = 0;      // -100 (enemy) to +100 (ally)
    public NPCStatus Status { get; set; } = NPCStatus.Active;

    // History tracking
    public List<string> InteractionHistory { get; set; } = new();
    public string? LastMissionId { get; set; }
    public int? LastInteractionWeek { get; set; }
    public int TotalInteractions { get; set; }

    // Knowledge - which agents know about this NPC
    public HashSet<string> KnownByAgents { get; set; } = new();

    // Computed properties (using centralized thresholds)
    public bool IsAlly => Relationship > Thresholds.Friend;
    public bool IsEnemy => Relationship < Thresholds.Hostile;
    public bool IsNeutral => Relationship >= Thresholds.Hostile && Relationship <= Thresholds.Friend;
    public bool CanBeIntimidated => Status == NPCStatus.Active && Relationship > Thresholds.DeepEnemy;
    public bool WillResist => Status == NPCStatus.Hostile || Relationship < Thresholds.Enemy;

    // For display
    public string DisplayName => Title ?? Name;
}
