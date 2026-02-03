// Story System - Faction Entity
// Represents a rival family or organization

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Represents a rival family or faction.
/// Extends the existing RivalFamily concept with territory tracking.
/// </summary>
public class Faction
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";          // "Tattaglia Family"

    // Relationship to player
    public int Hostility { get; set; } = 30;        // 0-100
    public int Respect { get; set; } = 50;          // How much they fear/respect us

    // Territory control
    public HashSet<string> ControlledLocationIds { get; set; } = new();
    public int Resources { get; set; } = 100;       // Economic/military strength

    // Personnel
    public HashSet<string> MemberNPCIds { get; set; } = new();

    // State tracking
    public bool AtWar { get; set; }
    public bool HasTruce { get; set; }
    public int? TruceExpiresWeek { get; set; }

    // Computed
    public bool IsAggressive => Hostility > 70 && !HasTruce;
    public bool IsWeak => Resources < 30;
    public bool IsStrong => Resources > 70;
}
