// Story System - Hybrid Mission Generator
// Combines DynamicMissionGenerator (Story) and MissionGenerator (Legacy) systems

using AgentRouting.MafiaDemo.Game;
using AgentRouting.MafiaDemo.Missions;

namespace AgentRouting.MafiaDemo.Story.Integration;

/// <summary>
/// A unified mission entry combining candidates from both systems.
/// Used for scoring and selection across all sources.
/// </summary>
public class UnifiedMissionEntry
{
    public Mission Mission { get; set; } = null!;
    public MissionSource Source { get; set; }
    public float Score { get; set; }
    public string? PlotThreadId { get; set; }
    public string? StoryNodeId { get; set; }
}

/// <summary>
/// Combines Story System's DynamicMissionGenerator with the legacy MissionGenerator.
///
/// ALGORITHM:
/// 1. Generate candidates from Story System (if enabled)
/// 2. Generate candidates from Legacy system
/// 3. Convert all to unified format with scores
/// 4. Apply repetition penalties across both systems
/// 5. Select best mission with weighted randomness
///
/// DESIGN DECISION: The hybrid approach ensures backward compatibility -
/// games without Story System enabled continue to work unchanged.
/// When Story System is enabled, narrative missions are prioritized
/// but fallback missions are still available for variety.
/// </summary>
public class HybridMissionGenerator
{
    private readonly DynamicMissionGenerator? _storyGenerator;
    private readonly MissionGenerator _legacyGenerator;
    private readonly WorldState? _world;
    private readonly MissionHistory? _history;
    private readonly Random _random = new();

    /// <summary>
    /// Create a hybrid generator with Story System integration.
    /// </summary>
    public HybridMissionGenerator(
        WorldState world,
        StoryGraph graph,
        IntelRegistry intel,
        MissionHistory history)
    {
        _world = world;
        _history = history;
        _storyGenerator = new DynamicMissionGenerator(world, graph, intel, history);
        _legacyGenerator = new MissionGenerator();
    }

    /// <summary>
    /// Create a hybrid generator without Story System (legacy mode).
    /// </summary>
    public HybridMissionGenerator()
    {
        _storyGenerator = null;
        _world = null;
        _history = null;
        _legacyGenerator = new MissionGenerator();
    }

    /// <summary>
    /// Check if Story System is available.
    /// </summary>
    public bool StorySystemEnabled => _storyGenerator != null && _world != null;

    /// <summary>
    /// Generate a mission, blending Story System and Legacy generators.
    /// Returns the best mission after scoring all candidates.
    /// </summary>
    public Mission GenerateMission(PlayerCharacter player, GameState gameState)
    {
        var candidates = new List<UnifiedMissionEntry>();

        // 1. Get Story System missions (if enabled)
        if (_storyGenerator != null && _world != null)
        {
            var storyCandidate = _storyGenerator.GenerateMission(player);
            var storyMission = MissionAdapter.ToMission(storyCandidate, _world);

            candidates.Add(new UnifiedMissionEntry
            {
                Mission = storyMission,
                Source = storyCandidate.Source,
                Score = storyCandidate.Score,
                PlotThreadId = storyCandidate.PlotThreadId,
                StoryNodeId = storyCandidate.NodeId
            });

            // Get additional story candidates for variety
            // (regenerate a few times to get different options)
            for (int i = 0; i < 2; i++)
            {
                var additional = _storyGenerator.GenerateMission(player);
                // Skip duplicates
                if (candidates.All(c => c.StoryNodeId != additional.NodeId || additional.NodeId == null))
                {
                    var mission = MissionAdapter.ToMission(additional, _world);
                    candidates.Add(new UnifiedMissionEntry
                    {
                        Mission = mission,
                        Source = additional.Source,
                        Score = additional.Score * 0.8f, // Slightly lower score for additional options
                        PlotThreadId = additional.PlotThreadId,
                        StoryNodeId = additional.NodeId
                    });
                }
            }
        }

        // 2. Get Legacy missions
        var legacyMission = _legacyGenerator.GenerateMission(player, gameState, _world);
        candidates.Add(new UnifiedMissionEntry
        {
            Mission = legacyMission,
            Source = MissionSource.Generated,
            Score = CalculateLegacyScore(legacyMission, player)
        });

        // Add one more legacy mission for variety
        var legacyMission2 = _legacyGenerator.GenerateMission(player, gameState, _world);
        if (legacyMission2.Type != legacyMission.Type) // Different type for variety
        {
            candidates.Add(new UnifiedMissionEntry
            {
                Mission = legacyMission2,
                Source = MissionSource.Generated,
                Score = CalculateLegacyScore(legacyMission2, player) * 0.8f
            });
        }

        // 3. Apply cross-system repetition penalties
        if (_history != null && _world != null)
        {
            foreach (var entry in candidates)
            {
                var repetitionPenalty = _history.GetRepetitionScore(
                    entry.Mission.Type.ToString(),
                    entry.Mission.LocationId,
                    entry.Mission.NPCId,
                    _world.CurrentWeek);

                entry.Score *= (1.0f - repetitionPenalty * 0.4f);
            }
        }

        // 4. Boost plot thread missions (narrative priority)
        foreach (var entry in candidates.Where(c => c.PlotThreadId != null))
        {
            entry.Score *= 1.3f;
        }

        // 5. Select best mission with weighted randomness
        return SelectWeighted(candidates).Mission;
    }

    /// <summary>
    /// Generate multiple mission options for player choice.
    /// Returns 3 distinct missions when possible.
    /// </summary>
    public List<Mission> GenerateMissionChoices(PlayerCharacter player, GameState gameState, int count = 3)
    {
        var seen = new HashSet<string>();
        var missions = new List<Mission>();

        // Try to generate distinct missions
        for (int attempts = 0; attempts < count * 3 && missions.Count < count; attempts++)
        {
            var mission = GenerateMission(player, gameState);

            // Use title + type as uniqueness key
            var key = $"{mission.Type}:{mission.Title}";
            if (!seen.Contains(key))
            {
                seen.Add(key);
                missions.Add(mission);
            }
        }

        return missions;
    }

    /// <summary>
    /// Calculate a score for legacy-generated missions.
    /// Matches the scoring scale of DynamicMissionGenerator.
    /// </summary>
    private float CalculateLegacyScore(Mission mission, PlayerCharacter player)
    {
        float score = 50f; // Base score

        // Rank-appropriate missions score higher
        var minRank = mission.MinimumRank;
        var rankDiff = (int)player.Rank - minRank;
        if (rankDiff >= 0 && rankDiff <= 1)
            score += 20f; // Right at player's level
        else if (rankDiff > 1)
            score -= 10f; // Too easy

        // Risk/reward balance
        if (mission.RiskLevel >= 5 && mission.RespectReward >= 10)
            score += 15f;

        // Money missions for low-cash players
        if (player.Money < 500 && mission.MoneyReward > 400)
            score += 10f;

        // Low heat missions for high-heat players
        if (player.Heat > 50 && mission.HeatGenerated < 3)
            score += 10f;

        // Random variance for variety
        score *= 0.9f + (float)_random.NextDouble() * 0.2f;

        return score;
    }

    /// <summary>
    /// Select from candidates using weighted probability.
    /// Higher scores = higher probability of selection.
    /// </summary>
    private UnifiedMissionEntry SelectWeighted(List<UnifiedMissionEntry> candidates)
    {
        if (candidates.Count == 0)
            throw new InvalidOperationException("No mission candidates available");

        // Sort by score descending
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Pick from top entries with weighted probability
        var topCount = Math.Min(3, candidates.Count);
        var top = candidates.Take(topCount).ToList();

        float totalScore = top.Sum(c => Math.Max(0.1f, c.Score)); // Minimum 0.1 to avoid division issues
        float roll = (float)_random.NextDouble() * totalScore;
        float cumulative = 0;

        foreach (var candidate in top)
        {
            cumulative += Math.Max(0.1f, candidate.Score);
            if (roll <= cumulative)
                return candidate;
        }

        return top[0]; // Fallback to highest scored
    }
}
