// Story System - Mission Adapter
// Converts between Story System's MissionCandidate and existing Mission system

using AgentRouting.MafiaDemo.Missions;

namespace AgentRouting.MafiaDemo.Story.Integration;

/// <summary>
/// Adapts MissionCandidate from the Story System to Mission objects
/// compatible with the existing mission execution flow.
///
/// DESIGN DECISION: This adapter bridges the two systems without
/// requiring changes to either. The MissionCandidate carries narrative
/// context (plot threads, story nodes) while Mission is the runtime
/// execution format.
/// </summary>
public static class MissionAdapter
{
    /// <summary>
    /// Convert a MissionCandidate to a Mission object.
    /// Maps narrative metadata to mission properties.
    /// </summary>
    public static Mission ToMission(MissionCandidate candidate, WorldState world)
    {
        var missionType = ParseMissionType(candidate.MissionType);

        var mission = new Mission
        {
            Id = candidate.NodeId ?? Guid.NewGuid().ToString(),
            Title = candidate.Title,
            Description = candidate.Description,
            Type = missionType,
            AssignedBy = DetermineAssigner(candidate),
            NPCId = candidate.NPCId,
            LocationId = candidate.LocationId,
            RiskLevel = CalculateRiskLevel(candidate, world),
            RespectReward = CalculateRespectReward(candidate),
            MoneyReward = CalculateMoneyReward(candidate, world),
            HeatGenerated = CalculateHeatGenerated(candidate, missionType),
            SkillRequirements = GetSkillRequirements(missionType),
            Data = BuildMissionData(candidate)
        };

        // Apply location-based modifiers
        if (!string.IsNullOrEmpty(candidate.LocationId))
        {
            var location = world.GetLocation(candidate.LocationId);
            if (location != null)
            {
                ApplyLocationModifiers(mission, location);
            }
        }

        // Apply NPC-based modifiers
        if (!string.IsNullOrEmpty(candidate.NPCId))
        {
            var npc = world.GetNPC(candidate.NPCId);
            if (npc != null)
            {
                ApplyNPCModifiers(mission, npc);
            }
        }

        return mission;
    }

    private static MissionType ParseMissionType(string typeString)
    {
        return typeString.ToLowerInvariant() switch
        {
            "collection" => MissionType.Collection,
            "intimidation" => MissionType.Intimidation,
            "information" => MissionType.Information,
            "negotiation" => MissionType.Negotiation,
            "hit" => MissionType.Hit,
            "territory" => MissionType.Territory,
            "recruitment" => MissionType.Recruitment,
            _ => MissionType.Information // Default for story missions
        };
    }

    private static string DetermineAssigner(MissionCandidate candidate)
    {
        // Plot missions come from higher-ups
        if (candidate.Source == MissionSource.PlotThread)
            return "underboss-001";

        // Story graph missions come from the appropriate hierarchy level
        if (candidate.Source == MissionSource.StoryGraph)
            return candidate.Priority > 70 ? "godfather-001" : "capo-001";

        // Intel-driven missions suggest careful planning
        if (candidate.Source == MissionSource.Intel)
            return "consigliere-001";

        // Default to capo for generated missions
        return "capo-001";
    }

    private static int CalculateRiskLevel(MissionCandidate candidate, WorldState world)
    {
        int baseRisk = 3;

        // Plot missions tend to be more important but riskier
        if (candidate.PlotThreadId != null)
            baseRisk += 2;

        // High priority missions are riskier
        if (candidate.Priority > 70)
            baseRisk += 2;

        // Location heat affects risk
        if (!string.IsNullOrEmpty(candidate.LocationId))
        {
            var location = world.GetLocation(candidate.LocationId);
            if (location?.IsHot == true)
                baseRisk += 2;
        }

        return Math.Clamp(baseRisk, 1, 10);
    }

    private static int CalculateRespectReward(MissionCandidate candidate)
    {
        int baseRespect = 5;

        // Plot missions reward more respect
        if (candidate.PlotThreadId != null)
            baseRespect += 5;

        // Priority-based bonus
        baseRespect += candidate.Priority / 20;

        return baseRespect;
    }

    private static decimal CalculateMoneyReward(MissionCandidate candidate, WorldState world)
    {
        decimal baseReward = 300m;

        // Location-based value
        if (!string.IsNullOrEmpty(candidate.LocationId))
        {
            var location = world.GetLocation(candidate.LocationId);
            if (location != null)
            {
                baseReward = location.WeeklyValue * location.ProtectionCut;
            }
        }

        // Priority bonus
        baseReward *= 1 + (candidate.Priority / 200m);

        return Math.Round(baseReward, 0);
    }

    private static int CalculateHeatGenerated(MissionCandidate candidate, MissionType type)
    {
        // Base heat by mission type
        int baseHeat = type switch
        {
            MissionType.Collection => 2,
            MissionType.Intimidation => 5,
            MissionType.Information => 1,
            MissionType.Negotiation => 0,
            MissionType.Hit => 25,
            MissionType.Territory => 10,
            MissionType.Recruitment => 0,
            _ => 2
        };

        // Plot missions may generate more attention
        if (candidate.PlotThreadId != null)
            baseHeat += 2;

        return baseHeat;
    }

    private static Dictionary<string, int> GetSkillRequirements(MissionType type)
    {
        return type switch
        {
            MissionType.Collection => new Dictionary<string, int>(),
            MissionType.Intimidation => new Dictionary<string, int> { ["Intimidation"] = 8 },
            MissionType.Information => new Dictionary<string, int> { ["StreetSmarts"] = 8 },
            MissionType.Negotiation => new Dictionary<string, int> { ["Negotiation"] = 30 },
            MissionType.Hit => new Dictionary<string, int> { ["Intimidation"] = 50, ["StreetSmarts"] = 40 },
            MissionType.Territory => new Dictionary<string, int> { ["Leadership"] = 40, ["Business"] = 30 },
            MissionType.Recruitment => new Dictionary<string, int> { ["Leadership"] = 35, ["StreetSmarts"] = 25 },
            _ => new Dictionary<string, int>()
        };
    }

    private static Dictionary<string, object> BuildMissionData(MissionCandidate candidate)
    {
        var data = new Dictionary<string, object>
        {
            ["Source"] = candidate.Source.ToString(),
            ["Priority"] = candidate.Priority,
            ["Score"] = candidate.Score
        };

        if (candidate.NodeId != null)
            data["StoryNodeId"] = candidate.NodeId;

        if (candidate.PlotThreadId != null)
            data["PlotThreadId"] = candidate.PlotThreadId;

        return data;
    }

    private static void ApplyLocationModifiers(Mission mission, Location location)
    {
        // Hot locations are riskier
        if (location.IsHot)
        {
            mission.RiskLevel = Math.Min(10, mission.RiskLevel + 2);
            mission.HeatGenerated += 3;
            mission.Data["HotLocation"] = true;
        }

        // Contested territories have variable outcomes
        if (location.IsContested)
        {
            mission.RespectReward += 5; // Higher stakes = higher respect
            mission.Data["ContestedTerritory"] = true;
        }

        // Our territories are safer
        if (location.IsOurs)
        {
            mission.RiskLevel = Math.Max(1, mission.RiskLevel - 1);
            mission.Data["FriendlyTerritory"] = true;
        }
    }

    private static void ApplyNPCModifiers(Mission mission, NPC npc)
    {
        // Hostile NPCs make missions harder
        if (npc.Relationship < Thresholds.Hostile)
        {
            mission.RiskLevel = Math.Min(10, mission.RiskLevel + 2);
            mission.Data["HostileNPC"] = true;
        }
        // Allied NPCs make missions easier
        else if (npc.Relationship > Thresholds.Friend)
        {
            mission.RiskLevel = Math.Max(1, mission.RiskLevel - 2);
            mission.Data["AlliedNPC"] = true;
        }

        // Previously intimidated NPCs yield more in collections
        if (mission.Type == MissionType.Collection &&
            npc.Relationship < Thresholds.Enemy &&
            npc.InteractionHistory.Any(h => h.Contains("intimidated")))
        {
            mission.MoneyReward *= 1.20m;
            mission.Data["IntimidatedBonus"] = true;
        }
    }
}
