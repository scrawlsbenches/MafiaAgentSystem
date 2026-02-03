// Story System - Location Entity
// Represents a physical location in the game world

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Represents a physical location in the game world.
/// Locations persist across the game and remember what happened there.
///
/// DESIGN DECISION: Using a flat dictionary for history rather than a linked list
/// because we need O(1) lookup by event type and O(n) is fine for iteration
/// (history won't exceed ~50 events per location in a typical game).
/// </summary>
public class Location
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    // Current state
    public LocationState State { get; set; } = LocationState.Neutral;
    public string? OwnerId { get; set; }  // Faction ID or "player" or null
    public int LocalHeat { get; set; }     // 0-100, police attention at this spot

    // Tracking
    public int TimesVisited { get; set; }
    public int? LastVisitedWeek { get; set; }
    public List<string> EventHistory { get; set; } = new();  // Event IDs

    // NPCs present at this location (NPC ID -> relationship modifier)
    public Dictionary<string, int> ResidentNPCs { get; set; } = new();

    // Economic value (affects mission rewards)
    public decimal WeeklyValue { get; set; } = 500m;
    public decimal ProtectionCut { get; set; } = 0.4m;  // Our percentage

    // Computed properties for rules engine (using centralized thresholds)
    public bool IsHot => LocalHeat > Thresholds.HighHeat;
    public bool IsOurs => OwnerId == "player";
    public bool IsContested => State == LocationState.Contested;
    public bool WasRecentlyVisited(int currentWeek) =>
        LastVisitedWeek.HasValue && (currentWeek - LastVisitedWeek.Value) < Thresholds.RecentWeeks;
}
