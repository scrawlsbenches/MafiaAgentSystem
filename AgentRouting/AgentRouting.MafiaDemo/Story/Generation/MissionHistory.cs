// Story System - Mission History
// Tracks mission history to prevent repetition

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Tracks mission history to prevent repetition.
/// Uses sliding windows and decay for natural variety.
/// </summary>
public class MissionHistory
{
    private readonly Queue<string> _recentMissionTypes = new();
    private readonly Dictionary<string, int> _locationVisitCounts = new();
    private readonly Dictionary<string, int> _npcInteractionCounts = new();
    private readonly Dictionary<string, int> _lastVisitWeek = new();

    private const int TYPE_MEMORY = 5;      // Remember last 5 mission types
    private const int LOCATION_DECAY = 4;   // Location cooldown in weeks

    public void RecordMission(string missionType, string? locationId, string? npcId, int week)
    {
        // Track mission types
        _recentMissionTypes.Enqueue(missionType);
        if (_recentMissionTypes.Count > TYPE_MEMORY)
            _recentMissionTypes.Dequeue();

        // Track locations
        if (locationId != null)
        {
            _locationVisitCounts[locationId] = _locationVisitCounts.GetValueOrDefault(locationId) + 1;
            _lastVisitWeek[locationId] = week;
        }

        // Track NPCs
        if (npcId != null)
        {
            _npcInteractionCounts[npcId] = _npcInteractionCounts.GetValueOrDefault(npcId) + 1;
        }
    }

    /// <summary>
    /// Calculate a penalty score for a mission based on how repetitive it would be.
    /// Returns 0.0 (fresh) to 1.0 (very repetitive).
    /// </summary>
    public float GetRepetitionScore(string missionType, string? locationId, string? npcId, int currentWeek)
    {
        float score = 0f;

        // Penalize recently used mission types
        int typeCount = _recentMissionTypes.Count(t => t == missionType);
        score += typeCount * 0.15f;

        // Penalize recently visited locations
        if (locationId != null && _lastVisitWeek.TryGetValue(locationId, out int lastWeek))
        {
            int weeksSince = currentWeek - lastWeek;
            if (weeksSince < LOCATION_DECAY)
                score += (LOCATION_DECAY - weeksSince) * 0.1f;
        }

        // Penalize over-used NPCs
        if (npcId != null && _npcInteractionCounts.TryGetValue(npcId, out int npcCount))
        {
            score += Math.Min(npcCount * 0.05f, 0.3f);
        }

        return Math.Min(score, 1.0f);
    }

    /// <summary>
    /// Decay counts over time (call each turn).
    /// </summary>
    public void DecayCounters()
    {
        // Slowly decay location visit counts
        var locations = _locationVisitCounts.Keys.ToList();
        foreach (var loc in locations)
        {
            _locationVisitCounts[loc] = Math.Max(0, _locationVisitCounts[loc] - 1);
            if (_locationVisitCounts[loc] == 0)
                _locationVisitCounts.Remove(loc);
        }
    }
}
