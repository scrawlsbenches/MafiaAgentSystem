// Story System - Dynamic Mission Generator
// Generates missions dynamically based on world state, story graph, and history

using AgentRouting.MafiaDemo.Missions;

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// A candidate mission being considered for generation.
/// </summary>
public class MissionCandidate
{
    public MissionSource Source { get; set; }
    public string? NodeId { get; set; }
    public string MissionType { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? LocationId { get; set; }
    public string? NPCId { get; set; }
    public string? PlotThreadId { get; set; }
    public int Priority { get; set; }
    public float Score { get; set; }
}

/// <summary>
/// Generates missions dynamically based on world state, story graph, and history.
///
/// ALGORITHM:
/// 1. Gather candidate missions from multiple sources:
///    - Story graph available missions
///    - Active plot thread missions
///    - World state opportunities (hostile NPCs, hot locations, etc.)
///    - Fallback template missions
/// 2. Score each candidate by:
///    - Relevance to current world state
///    - Repetition penalty
///    - Plot priority
///    - Player capability match
/// 3. Select highest scoring mission with some randomness for variety
/// </summary>
public class DynamicMissionGenerator
{
    private readonly WorldState _world;
    private readonly StoryGraph _graph;
    private readonly IntelRegistry _intel;
    private readonly MissionHistory _history;
    private readonly Random _random = new();

    public DynamicMissionGenerator(WorldState world, StoryGraph graph, IntelRegistry intel, MissionHistory history)
    {
        _world = world;
        _graph = graph;
        _intel = intel;
        _history = history;
    }

    public MissionCandidate GenerateMission(PlayerCharacter player)
    {
        var candidates = new List<MissionCandidate>();

        // 1. Get missions from story graph
        candidates.AddRange(GetGraphMissions());

        // 2. Get missions from active plots
        candidates.AddRange(GetPlotMissions());

        // 3. Get missions from world state
        candidates.AddRange(GetWorldStateMissions(player));

        // 4. Generate fallback missions if needed
        if (candidates.Count < 3)
        {
            candidates.AddRange(GenerateFallbackMissions(player, 3 - candidates.Count));
        }

        // Score all candidates
        foreach (var candidate in candidates)
        {
            candidate.Score = CalculateScore(candidate, player);
        }

        // Select with weighted randomness
        return SelectWeighted(candidates);
    }

    private IEnumerable<MissionCandidate> GetGraphMissions()
    {
        return _graph.GetAvailableMissions(_world).Select(node => new MissionCandidate
        {
            Source = MissionSource.StoryGraph,
            NodeId = node.Id,
            MissionType = node.Metadata.GetValueOrDefault("MissionType")?.ToString() ?? "Information",
            Title = node.Title,
            Description = node.Description,
            LocationId = node.LocationId,
            NPCId = node.NPCId,
            PlotThreadId = node.PlotThreadId,
            Priority = node.PlotThreadId != null ? 80 : 50
        });
    }

    private IEnumerable<MissionCandidate> GetPlotMissions()
    {
        foreach (var plot in _graph.GetActivePlots())
        {
            if (plot.CurrentMissionNodeId != null)
            {
                var node = _graph.GetNode(plot.CurrentMissionNodeId);
                if (node != null && node.IsUnlocked && !node.IsCompleted)
                {
                    yield return new MissionCandidate
                    {
                        Source = MissionSource.PlotThread,
                        NodeId = node.Id,
                        MissionType = node.Metadata.GetValueOrDefault("MissionType")?.ToString() ?? "Information",
                        Title = $"[{plot.Title}] {node.Title}",
                        Description = node.Description,
                        LocationId = node.LocationId,
                        NPCId = node.NPCId,
                        PlotThreadId = plot.Id,
                        Priority = plot.Priority + 20  // Plot missions get bonus
                    };
                }
            }
        }
    }

    private IEnumerable<MissionCandidate> GetWorldStateMissions(PlayerCharacter player)
    {
        // Hostile NPCs create revenge/conflict missions
        foreach (var npc in _world.GetNPCsByStatus(NPCStatus.Hostile).Take(2))
        {
            yield return new MissionCandidate
            {
                Source = MissionSource.WorldState,
                MissionType = "Intimidation",
                Title = $"Deal with {npc.Name}",
                Description = $"{npc.Name} has become a problem. Handle it.",
                LocationId = npc.LocationId,
                NPCId = npc.Id,
                Priority = 70
            };
        }

        // High-heat locations create bribe/lay-low missions
        foreach (var loc in _world.GetLocationsByState(LocationState.Compromised).Take(1))
        {
            yield return new MissionCandidate
            {
                Source = MissionSource.WorldState,
                MissionType = "Negotiation",
                Title = $"Cool down {loc.Name}",
                Description = $"{loc.Name} is too hot. Bribe officials or lay low.",
                LocationId = loc.Id,
                Priority = 60
            };
        }

        // Allied NPCs we haven't visited create relationship missions
        foreach (var npc in _world.NPCs.Values
            .Where(n => n.IsAlly && n.LastInteractionWeek < _world.CurrentWeek - 4)
            .Take(1))
        {
            yield return new MissionCandidate
            {
                Source = MissionSource.WorldState,
                MissionType = "Collection",
                Title = $"Check in with {npc.Name}",
                Description = $"It's been a while since we visited {npc.Name}. Maintain the relationship.",
                LocationId = npc.LocationId,
                NPCId = npc.Id,
                Priority = 40
            };
        }

        // Intel-driven opportunities
        var opportunities = _intel.GetByType(IntelType.LocationOpportunity, _world.CurrentWeek)
            .Where(i => i.IsReliable && !i.IsActedUpon)
            .Take(1);

        foreach (var intel in opportunities)
        {
            var loc = _world.GetLocation(intel.SubjectId);
            if (loc != null)
            {
                yield return new MissionCandidate
                {
                    Source = MissionSource.Intel,
                    MissionType = "Territory",
                    Title = $"Opportunity at {loc.Name}",
                    Description = intel.Summary,
                    LocationId = loc.Id,
                    Priority = 65
                };
            }
        }
    }

    private IEnumerable<MissionCandidate> GenerateFallbackMissions(PlayerCharacter player, int count)
    {
        var accessibleLocations = _world.GetAccessibleLocations().ToList();
        var activeNPCs = _world.GetNPCsByStatus(NPCStatus.Active).ToList();

        for (int i = 0; i < count && accessibleLocations.Count > 0; i++)
        {
            var location = accessibleLocations[_random.Next(accessibleLocations.Count)];
            var npcsHere = _world.GetNPCsAtLocation(location.Id).ToList();
            var npc = npcsHere.Count > 0 ? npcsHere[_random.Next(npcsHere.Count)] : null;

            var missionType = PickFallbackMissionType(player);

            yield return new MissionCandidate
            {
                Source = MissionSource.Generated,
                MissionType = missionType,
                Title = GenerateFallbackTitle(missionType, location, npc),
                Description = GenerateFallbackDescription(missionType, location, npc),
                LocationId = location.Id,
                NPCId = npc?.Id,
                Priority = 30
            };
        }
    }

    private string PickFallbackMissionType(PlayerCharacter player)
    {
        // Weight by what player hasn't done recently
        var types = new[] { "Collection", "Intimidation", "Information", "Negotiation" };
        var weights = types.Select(t =>
            1.0f - _history.GetRepetitionScore(t, null, null, _world.CurrentWeek)
        ).ToArray();

        float total = weights.Sum();
        float roll = (float)_random.NextDouble() * total;
        float cumulative = 0;

        for (int i = 0; i < types.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return types[i];
        }

        return types[0];
    }

    private string GenerateFallbackTitle(string missionType, Location location, NPC? npc)
    {
        return missionType switch
        {
            "Collection" => $"Collect from {location.Name}",
            "Intimidation" => npc != null
                ? $"Send a message to {npc.DisplayName}"
                : $"Show presence at {location.Name}",
            "Information" => $"Scout {location.Name}",
            "Negotiation" => npc != null
                ? $"Talk to {npc.DisplayName}"
                : $"Meet contact at {location.Name}",
            _ => $"Visit {location.Name}"
        };
    }

    private string GenerateFallbackDescription(string missionType, Location location, NPC? npc)
    {
        return missionType switch
        {
            "Collection" => $"Collect the weekly payment from {location.Name}.",
            "Intimidation" => npc != null
                ? $"{npc.DisplayName} needs to understand how things work around here."
                : $"Make our presence known at {location.Name}.",
            "Information" => $"Keep your ears open at {location.Name}. Report anything interesting.",
            "Negotiation" => npc != null
                ? $"Have a conversation with {npc.DisplayName}. Find common ground."
                : $"Meet with a contact at {location.Name}.",
            _ => $"Handle business at {location.Name}."
        };
    }

    private float CalculateScore(MissionCandidate candidate, PlayerCharacter player)
    {
        float score = candidate.Priority;

        // Apply repetition penalty
        float repetition = _history.GetRepetitionScore(
            candidate.MissionType,
            candidate.LocationId,
            candidate.NPCId,
            _world.CurrentWeek);
        score *= (1.0f - repetition * 0.5f);  // Up to 50% penalty for repetition

        // Bonus for plot missions
        if (candidate.PlotThreadId != null)
            score *= 1.2f;

        // Bonus for intel-driven missions
        if (candidate.Source == MissionSource.Intel)
            score *= 1.1f;

        // Slight random variance for variety
        score *= 0.9f + (float)_random.NextDouble() * 0.2f;

        return score;
    }

    private MissionCandidate SelectWeighted(List<MissionCandidate> candidates)
    {
        if (candidates.Count == 0)
            throw new InvalidOperationException("No mission candidates available");

        // Sort by score descending
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Pick from top 3 with weighted probability
        var top = candidates.Take(3).ToList();
        float totalScore = top.Sum(c => c.Score);
        float roll = (float)_random.NextDouble() * totalScore;
        float cumulative = 0;

        foreach (var candidate in top)
        {
            cumulative += candidate.Score;
            if (roll <= cumulative)
                return candidate;
        }

        return top[0];
    }
}
