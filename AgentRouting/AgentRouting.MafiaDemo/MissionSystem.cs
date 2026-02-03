using AgentRouting.MafiaDemo.Game;
using AgentRouting.MafiaDemo.Story;
using RulesEngine.Core;

namespace AgentRouting.MafiaDemo.Missions;

/// <summary>
/// Mission types in the game
/// </summary>
public enum MissionType
{
    Collection,      // Collect protection money
    Intimidation,    // Send a message to someone
    Information,     // Gather intelligence
    Negotiation,     // Diplomatic mission
    Hit,            // Assassination (high level only)
    Territory,      // Expand or defend territory
    Recruitment     // Recruit new members
}

/// <summary>
/// Represents a mission that can be assigned to the player
/// </summary>
public class Mission
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public MissionType Type { get; set; }
    public string AssignedBy { get; set; } = ""; // Agent who assigned it
    
    // Requirements
    public int MinimumRank { get; set; } = 0; // 0=Associate, 1=Soldier, etc.
    public Dictionary<string, int> SkillRequirements { get; set; } = new();
    
    // Risk/Reward
    public int RiskLevel { get; set; } = 1; // 1-10
    public int RespectReward { get; set; }
    public decimal MoneyReward { get; set; }
    public int HeatGenerated { get; set; } // How much police attention
    
    // State
    public MissionStatus Status { get; set; } = MissionStatus.Available;
    public DateTime AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? Outcome { get; set; }
    
    // Mission-specific data
    public Dictionary<string, object> Data { get; set; } = new();

    // Story System Integration - optional NPC and Location references
    /// <summary>
    /// Optional ID of the NPC involved in this mission (from WorldState).
    /// When set, mission outcomes affect NPC relationships.
    /// </summary>
    public string? NPCId { get; set; }

    /// <summary>
    /// Optional ID of the location where this mission takes place (from WorldState).
    /// When set, mission outcomes may affect location state.
    /// </summary>
    public string? LocationId { get; set; }
}

public enum MissionStatus
{
    Available,
    Assigned,
    InProgress,
    Completed,
    Failed,
    Expired
}

/// <summary>
/// Player character with career progression
/// </summary>
public class PlayerCharacter
{
    public string Name { get; set; } = "";
    public PlayerRank Rank { get; set; } = PlayerRank.Associate;
    
    // Stats
    public int Respect { get; set; } = 10;
    public decimal Money { get; set; } = 1000m;
    public int Heat { get; set; } = 0;
    
    // Skills (0-100)
    public PlayerSkills Skills { get; set; } = new();
    
    // Relationships with agents (-100 to 100)
    public Dictionary<string, int> Relationships { get; set; } = new();
    
    // Career tracking
    public int Week { get; set; } = 1;
    public List<Mission> CompletedMissions { get; set; } = new();
    public List<Mission> ActiveMissions { get; set; } = new();
    public List<string> Achievements { get; set; } = new();
    
    // Personality traits (affects decision making)
    public PlayerPersonality Personality { get; set; } = new();
}

public enum PlayerRank
{
    Associate = 0,
    Soldier = 1,
    Capo = 2,
    Underboss = 3,
    Don = 4
}

public class PlayerSkills
{
    public int Intimidation { get; set; } = 10;
    public int Negotiation { get; set; } = 10;
    public int StreetSmarts { get; set; } = 10;
    public int Leadership { get; set; } = 5;
    public int Business { get; set; } = 5;
    
    // Helper to get skill by name
    public int GetSkill(string skillName)
    {
        return skillName.ToLower() switch
        {
            "intimidation" => Intimidation,
            "negotiation" => Negotiation,
            "streetsmarts" => StreetSmarts,
            "leadership" => Leadership,
            "business" => Business,
            _ => 0
        };
    }
}

public class PlayerPersonality
{
    public int Ambition { get; set; } = 50; // 0-100
    public int Loyalty { get; set; } = 70;
    public int Ruthlessness { get; set; } = 30;
    public int Caution { get; set; } = 50;
    
    // Derived traits
    public bool IsAmbitious => Ambition > 70;
    public bool IsLoyal => Loyalty > 70;
    public bool IsRuthless => Ruthlessness > 70;
    public bool IsCautious => Caution > 70;
    public bool IsReckless => Caution < 30;
}

/// <summary>
/// Generates missions based on player rank and game state
/// </summary>
public class MissionGenerator
{
    /// <summary>
    /// Generate a mission without WorldState integration (backwards compatible).
    /// </summary>
    public Mission GenerateMission(PlayerCharacter player, GameState gameState)
    {
        return GenerateMission(player, gameState, null);
    }

    /// <summary>
    /// Generate a mission with optional WorldState integration.
    /// When WorldState is provided, missions are linked to NPCs and locations.
    /// </summary>
    public Mission GenerateMission(PlayerCharacter player, GameState gameState, WorldState? worldState)
    {
        var missionType = SelectMissionType(player.Rank, gameState);

        return missionType switch
        {
            MissionType.Collection => GenerateCollectionMission(player, worldState),
            MissionType.Intimidation => GenerateIntimidationMission(player, worldState),
            MissionType.Information => GenerateInformationMission(player, worldState),
            MissionType.Negotiation => GenerateNegotiationMission(player, worldState),
            MissionType.Hit => GenerateHitMission(player),
            MissionType.Territory => GenerateTerritoryMission(player, worldState),
            MissionType.Recruitment => GenerateRecruitmentMission(player),
            _ => GenerateCollectionMission(player, worldState)
        };
    }
    
    private MissionType SelectMissionType(PlayerRank rank, GameState gameState)
    {
        // Different ranks get different mission types
        var availableTypes = new List<MissionType>();
        
        // Everyone can do collections and intimidation
        availableTypes.Add(MissionType.Collection);
        availableTypes.Add(MissionType.Intimidation);
        availableTypes.Add(MissionType.Information);
        
        // Soldiers and up can negotiate
        if (rank >= PlayerRank.Soldier)
        {
            availableTypes.Add(MissionType.Negotiation);
        }
        
        // Capos can recruit and manage territory
        if (rank >= PlayerRank.Capo)
        {
            availableTypes.Add(MissionType.Recruitment);
            availableTypes.Add(MissionType.Territory);
        }
        
        // Underboss and Don can order hits
        if (rank >= PlayerRank.Underboss)
        {
            availableTypes.Add(MissionType.Hit);
        }
        
        return availableTypes[Random.Shared.Next(availableTypes.Count)];
    }
    
    private Mission GenerateCollectionMission(PlayerCharacter player, WorldState? worldState = null)
    {
        // Try to use NPC from WorldState if available
        NPC? targetNpc = null;
        Location? location = null;

        if (worldState != null)
        {
            // Find business owners for collection
            var businessOwners = worldState.NPCs.Values
                .Where(n => n.Role is "restaurant_owner" or "baker" or "bar_owner" or "shopkeeper" or "butcher"
                         && n.Status == NPCStatus.Active)
                .ToList();

            if (businessOwners.Count > 0)
            {
                targetNpc = businessOwners[Random.Shared.Next(businessOwners.Count)];
                location = worldState.GetLocation(targetNpc.LocationId);
            }
        }

        // Fallback to static data if no NPC available
        var businesses = new[]
        {
            "Tony's Restaurant", "Luigi's Bakery", "Marino's Deli",
            "Sal's Bar", "Vinnie's Grocery", "Angelo's Butcher Shop"
        };

        var business = targetNpc != null && location != null
            ? location.Name
            : businesses[Random.Shared.Next(businesses.Length)];

        var amount = location != null
            ? (int)location.WeeklyValue
            : Random.Shared.Next(400, 1000);

        var mission = new Mission
        {
            Title = $"Collect from {business}",
            Description = $"Go collect the weekly payment from {business}. They owe ${amount}.",
            Type = MissionType.Collection,
            AssignedBy = player.Rank >= PlayerRank.Capo ? "underboss-001" : "capo-001",
            MinimumRank = 0,
            RiskLevel = Random.Shared.Next(1, 4),
            RespectReward = 4,
            MoneyReward = amount * 0.40m, // Player gets 40% cut (balanced)
            HeatGenerated = 2,
            Data = new Dictionary<string, object>
            {
                ["BusinessName"] = business,
                ["AmountOwed"] = amount
            },
            NPCId = targetNpc?.Id,
            LocationId = location?.Id
        };

        // Apply NPC relationship modifiers to difficulty
        if (targetNpc != null)
        {
            ApplyNPCEffects(mission, targetNpc);
        }

        return mission;
    }

    /// <summary>
    /// Apply NPC relationship effects to mission parameters.
    /// Hostile NPCs make missions harder, allied NPCs make them easier.
    /// </summary>
    private void ApplyNPCEffects(Mission mission, NPC npc)
    {
        // Hostile NPCs (Relationship < -50) make missions harder (+10 difficulty via RiskLevel)
        if (npc.Relationship < Thresholds.Hostile)
        {
            mission.RiskLevel = Math.Min(10, mission.RiskLevel + 2); // +2 risk ~ +10 difficulty
            mission.Data["NPCHostile"] = true;
        }
        // Allied NPCs (Relationship > 50) make missions easier (-10 difficulty via RiskLevel)
        else if (npc.Relationship > Thresholds.Friend)
        {
            mission.RiskLevel = Math.Max(1, mission.RiskLevel - 2); // -2 risk ~ -10 difficulty
            mission.Data["NPCAllied"] = true;
        }

        // Intimidated NPCs (previously intimidated with low relationship) have +20% collection yields
        if (mission.Type == MissionType.Collection &&
            npc.Relationship < Thresholds.Enemy &&
            npc.InteractionHistory.Any(h => h.Contains("intimidated")))
        {
            mission.MoneyReward *= 1.20m;
            mission.Data["IntimidatedBonus"] = true;
        }
    }
    
    private Mission GenerateIntimidationMission(PlayerCharacter player, WorldState? worldState = null)
    {
        // Try to use NPC from WorldState if available
        NPC? targetNpc = null;
        Location? location = null;

        if (worldState != null)
        {
            // Find NPCs suitable for intimidation (various roles)
            var targets = worldState.NPCs.Values
                .Where(n => n.Status == NPCStatus.Active && n.CanBeIntimidated)
                .ToList();

            if (targets.Count > 0)
            {
                targetNpc = targets[Random.Shared.Next(targets.Count)];
                location = worldState.GetLocation(targetNpc.LocationId);
            }
        }

        // Fallback to static data
        var staticTargets = new[]
        {
            ("shopkeeper", "been late with payments"),
            ("dock worker", "been talking to the cops"),
            ("rival associate", "disrespected the family"),
            ("bar owner", "refused protection")
        };

        string target, reason;
        if (targetNpc != null)
        {
            target = targetNpc.DisplayName;
            reason = targetNpc.Relationship < 0 ? "been causing problems" : "needs a reminder of who's in charge";
        }
        else
        {
            (target, reason) = staticTargets[Random.Shared.Next(staticTargets.Length)];
        }

        var mission = new Mission
        {
            Title = $"Send a message to {target}",
            Description = $"{target} has {reason}. Make sure they understand this can't continue.",
            Type = MissionType.Intimidation,
            AssignedBy = "capo-001",
            MinimumRank = 0,
            RiskLevel = Random.Shared.Next(3, 6),
            RespectReward = 5,
            MoneyReward = 100m,
            HeatGenerated = Random.Shared.Next(3, 7),
            SkillRequirements = new Dictionary<string, int>
            {
                ["Intimidation"] = 8  // Lowered from 15 so new players can attempt
            },
            Data = new Dictionary<string, object>
            {
                ["Target"] = target,
                ["Reason"] = reason
            },
            NPCId = targetNpc?.Id,
            LocationId = location?.Id
        };

        // Apply NPC relationship modifiers
        if (targetNpc != null)
        {
            ApplyNPCEffects(mission, targetNpc);
        }

        return mission;
    }
    
    private Mission GenerateInformationMission(PlayerCharacter player, WorldState? worldState = null)
    {
        // Try to use NPC from WorldState as information source
        NPC? sourceNpc = null;
        Location? location = null;

        if (worldState != null)
        {
            // Informants or allied NPCs can be information sources
            var sources = worldState.NPCs.Values
                .Where(n => n.Status == NPCStatus.Active &&
                           (n.Role == "informant" || n.Relationship > 0))
                .ToList();

            if (sources.Count > 0)
            {
                sourceNpc = sources[Random.Shared.Next(sources.Count)];
                location = worldState.GetLocation(sourceNpc.LocationId);
            }
        }

        var scenarios = new[]
        {
            ("who's stealing from the docks", "find the thief and report back"),
            ("which cop is on Barzini's payroll", "we need to know who we can trust"),
            ("where the Tattaglias are hiding their money", "we might need this information later"),
            ("who's been talking to the Feds", "there's an informant somewhere")
        };

        var (subject, goal) = scenarios[Random.Shared.Next(scenarios.Length)];

        var mission = new Mission
        {
            Title = $"Find out {subject}",
            Description = $"Keep your ears open and {goal}.",
            Type = MissionType.Information,
            AssignedBy = player.Rank >= PlayerRank.Soldier ? "underboss-001" : "capo-001",
            MinimumRank = 0,
            RiskLevel = Random.Shared.Next(2, 5),
            RespectReward = 7,
            MoneyReward = 200m,
            HeatGenerated = 1,
            SkillRequirements = new Dictionary<string, int>
            {
                ["StreetSmarts"] = 8  // Lowered from 20 so new players can attempt
            },
            Data = new Dictionary<string, object>
            {
                ["Subject"] = subject
            },
            NPCId = sourceNpc?.Id,
            LocationId = location?.Id
        };

        // Allied NPCs make gathering information easier
        if (sourceNpc != null)
        {
            ApplyNPCEffects(mission, sourceNpc);
        }

        return mission;
    }
    
    private Mission GenerateNegotiationMission(PlayerCharacter player, WorldState? worldState = null)
    {
        // Try to use NPC from WorldState for negotiation
        NPC? targetNpc = null;
        Location? location = null;

        if (worldState != null)
        {
            // Business owners or neutral/friendly NPCs for negotiation
            var targets = worldState.NPCs.Values
                .Where(n => n.Status == NPCStatus.Active &&
                           n.Relationship > Thresholds.Enemy) // Not deeply hostile
                .ToList();

            if (targets.Count > 0)
            {
                targetNpc = targets[Random.Shared.Next(targets.Count)];
                location = worldState.GetLocation(targetNpc.LocationId);
            }
        }

        var scenarios = new[]
        {
            ("Tattaglia family", "negotiate a truce"),
            ("union boss", "secure favorable terms"),
            ("city councilman", "arrange a meeting with the Don"),
            ("business owner", "convince them to accept protection")
        };

        string party, goal;
        if (targetNpc != null)
        {
            party = targetNpc.DisplayName;
            goal = "reach a mutually beneficial arrangement";
        }
        else
        {
            (party, goal) = scenarios[Random.Shared.Next(scenarios.Length)];
        }

        var mission = new Mission
        {
            Title = $"Negotiate with {party}",
            Description = $"We need someone diplomatic to {goal}.",
            Type = MissionType.Negotiation,
            AssignedBy = "underboss-001",
            MinimumRank = 1, // Soldier or higher
            RiskLevel = Random.Shared.Next(4, 7),
            RespectReward = 10,
            MoneyReward = 500m,
            HeatGenerated = 0,
            SkillRequirements = new Dictionary<string, int>
            {
                ["Negotiation"] = 30
            },
            Data = new Dictionary<string, object>
            {
                ["Party"] = party,
                ["Goal"] = goal
            },
            NPCId = targetNpc?.Id,
            LocationId = location?.Id
        };

        // Apply NPC relationship modifiers
        if (targetNpc != null)
        {
            ApplyNPCEffects(mission, targetNpc);
        }

        return mission;
    }
    
    private Mission GenerateHitMission(PlayerCharacter player)
    {
        var targets = new[]
        {
            "rival family Capo",
            "informant",
            "corrupt detective",
            "business owner who killed a made man"
        };

        var target = targets[Random.Shared.Next(targets.Length)];

        return new Mission
        {
            Title = $"Eliminate the {target}",
            Description = $"The Don has authorized this. Make it clean, make it quiet.",
            Type = MissionType.Hit,
            AssignedBy = "godfather-001",
            MinimumRank = 3, // Underboss only
            RiskLevel = 10,
            RespectReward = 20,
            MoneyReward = 2500m, // Balanced: ~6x collection reward (down from ~25x)
            HeatGenerated = 25,  // Balanced: ~5 weeks to recover (down from 30)
            SkillRequirements = new Dictionary<string, int>
            {
                ["Intimidation"] = 50,
                ["StreetSmarts"] = 40
            },
            Data = new Dictionary<string, object>
            {
                ["Target"] = target
            }
        };
    }
    
    private Mission GenerateTerritoryMission(PlayerCharacter player, WorldState? worldState = null)
    {
        Location? targetLocation = null;

        if (worldState != null)
        {
            // Find contested or neutral locations for territory missions
            var locations = worldState.Locations.Values
                .Where(l => l.State == LocationState.Contested || l.State == LocationState.Neutral)
                .ToList();

            if (locations.Count > 0)
            {
                targetLocation = locations[Random.Shared.Next(locations.Count)];
            }
        }

        var scenarios = new[]
        {
            ("expand into the Bronx gambling scene", "set up new operations"),
            ("defend Little Italy from Tattaglia incursion", "show them it's our territory"),
            ("take over the Brooklyn docks", "increase our smuggling operations")
        };

        string area, goal;
        if (targetLocation != null)
        {
            area = $"the {targetLocation.Name} area";
            goal = targetLocation.State == LocationState.Contested
                ? "establish control"
                : "expand our influence";
        }
        else
        {
            (area, goal) = scenarios[Random.Shared.Next(scenarios.Length)];
        }

        return new Mission
        {
            Title = $"Territory: {area}",
            Description = $"The family wants to {goal}.",
            Type = MissionType.Territory,
            AssignedBy = "underboss-001",
            MinimumRank = 2, // Capo or higher
            RiskLevel = Random.Shared.Next(6, 9),
            RespectReward = 15,
            MoneyReward = 2000m,
            HeatGenerated = 10,
            SkillRequirements = new Dictionary<string, int>
            {
                ["Leadership"] = 40,
                ["Business"] = 30
            },
            Data = new Dictionary<string, object>
            {
                ["Area"] = area,
                ["Goal"] = goal
            },
            LocationId = targetLocation?.Id
        };
    }
    
    private Mission GenerateRecruitmentMission(PlayerCharacter player)
    {
        return new Mission
        {
            Title = "Recruit a new soldier",
            Description = "Find someone loyal and capable. They need to prove themselves before being made.",
            Type = MissionType.Recruitment,
            AssignedBy = "underboss-001",
            MinimumRank = 2, // Capo or higher
            RiskLevel = 3,
            RespectReward = 8,
            MoneyReward = 300m,
            HeatGenerated = 0,
            SkillRequirements = new Dictionary<string, int>
            {
                ["Leadership"] = 35,
                ["StreetSmarts"] = 25
            }
        };
    }
}

/// <summary>
/// Evaluates mission success based on player stats and random factors
/// </summary>
public class MissionEvaluator
{
    private readonly RulesEngineCore<MissionContext> _rules;

    public MissionEvaluator()
    {
        _rules = new RulesEngineCore<MissionContext>();
        SetupMissionRules();
    }
    
    private void SetupMissionRules()
    {
        // Auto-success for overqualified players
        _rules.AddRule(
            "AUTO_SUCCESS_OVERQUALIFIED",
            "Automatic Success - Overqualified",
            ctx => ctx.SkillAdvantage > 30,
            ctx => {
                ctx.SuccessChance = 100;
                ctx.BonusRespect = 5;
            },
            priority: 1000
        );
        
        // Bonus for high skills
        _rules.AddRule(
            "SKILL_BONUS",
            "Skill Bonus",
            ctx => ctx.SkillAdvantage > 10,
            ctx => {
                ctx.SuccessChance += ctx.SkillAdvantage;
                ctx.BonusRespect = ctx.SkillAdvantage / 5;
            },
            priority: 800
        );
        
        // Penalty for low skills
        _rules.AddRule(
            "SKILL_PENALTY",
            "Insufficient Skills",
            ctx => ctx.SkillAdvantage < -10,
            ctx => {
                ctx.SuccessChance += ctx.SkillAdvantage; // Negative
                ctx.HeatPenalty = Math.Abs(ctx.SkillAdvantage) / 2;
            },
            priority: 800
        );
        
        // High risk = higher potential rewards but also danger
        _rules.AddRule(
            "HIGH_RISK_HIGH_REWARD",
            "High Risk Mission",
            ctx => ctx.Mission.RiskLevel >= 8,
            ctx => {
                if (ctx.Success)
                {
                    ctx.BonusRespect = 10;
                    ctx.BonusMoney = ctx.Mission.MoneyReward * 0.5m;
                }
            },
            priority: 700
        );
        
        // Low heat helps
        _rules.AddRule(
            "LOW_HEAT_BONUS",
            "Operating Under Radar",
            ctx => ctx.Player.Heat < 30,
            ctx => {
                ctx.SuccessChance += 10;
            },
            priority: 600
        );
        
        // High heat is dangerous
        _rules.AddRule(
            "HIGH_HEAT_PENALTY",
            "Police Attention",
            ctx => ctx.Player.Heat > 70,
            ctx => {
                ctx.SuccessChance -= 20;
                ctx.HeatPenalty = 10;
            },
            priority: 600
        );
    }
    
    public MissionResult EvaluateMission(Mission mission, PlayerCharacter player)
    {
        var context = new MissionContext
        {
            Mission = mission,
            Player = player,
            SuccessChance = 50 // Base 50%
        };
        
        // Calculate skill advantage
        foreach (var (skillName, required) in mission.SkillRequirements)
        {
            var playerSkill = player.Skills.GetSkill(skillName);
            context.SkillAdvantage += playerSkill - required;
        }
        
        // Apply all rules
        _rules.EvaluateAll(context);
        
        // Clamp success chance
        context.SuccessChance = Math.Max(10, Math.Min(95, context.SuccessChance));

        // Roll for success
        var roll = Random.Shared.Next(1, 101);
        context.Success = roll <= context.SuccessChance;

        // Apply post-roll bonuses (HIGH_RISK_HIGH_REWARD rule logic)
        // This was previously done via a second EvaluateAll call, but that
        // also double-applied pre-roll modifiers. Now we handle it explicitly.
        if (context.Success && mission.RiskLevel >= 8)
        {
            context.BonusRespect = 10;
            context.BonusMoney = mission.MoneyReward * 0.5m;
        }

        return new MissionResult
        {
            Success = context.Success,
            RespectGained = context.Success ? mission.RespectReward + context.BonusRespect : -5,
            MoneyGained = context.Success ? mission.MoneyReward + context.BonusMoney : 0,
            HeatGained = context.Success ? mission.HeatGenerated : mission.HeatGenerated + context.HeatPenalty,
            Message = GenerateOutcomeMessage(mission, context),
            SkillGains = GenerateSkillGains(mission, context.Success)
        };
    }
    
    private string GenerateOutcomeMessage(Mission mission, MissionContext context)
    {
        if (context.Success)
        {
            var messages = mission.Type switch
            {
                MissionType.Collection => new[]
                {
                    "You collected the money. They weren't happy, but they paid.",
                    "Smooth collection. They know better than to be late next time.",
                    "You got the money. Had to remind them who they're dealing with."
                },
                MissionType.Intimidation => new[]
                {
                    "Message delivered. They won't forget it.",
                    "You made your point clear. They understand now.",
                    "They got the message loud and clear."
                },
                MissionType.Information => new[]
                {
                    "You found what you were looking for. The boss will be pleased.",
                    "Good work. This information is valuable.",
                    "You did your homework. The family appreciates it."
                },
                _ => new[] { "Mission accomplished successfully." }
            };
            
            return messages[Random.Shared.Next(messages.Length)];
        }
        else
        {
            var messages = new[]
            {
                "Things didn't go as planned. Better luck next time.",
                "The mission failed. The boss isn't happy.",
                "You messed up. This will affect your reputation."
            };
            
            return messages[Random.Shared.Next(messages.Length)];
        }
    }
    
    private Dictionary<string, int> GenerateSkillGains(Mission mission, bool success)
    {
        var gains = new Dictionary<string, int>();
        
        // Gain skills even on failure (you learn from mistakes)
        var gainAmount = success ? 2 : 1;
        
        foreach (var skillName in mission.SkillRequirements.Keys)
        {
            gains[skillName] = gainAmount;
        }
        
        return gains;
    }
}

public class MissionContext
{
    public Mission Mission { get; set; } = null!;
    public PlayerCharacter Player { get; set; } = null!;
    
    public int SuccessChance { get; set; } = 50;
    public int SkillAdvantage { get; set; } = 0;
    public bool Success { get; set; }
    
    // Bonuses/Penalties
    public int BonusRespect { get; set; } = 0;
    public decimal BonusMoney { get; set; } = 0;
    public int HeatPenalty { get; set; } = 0;
}

public class MissionResult
{
    public bool Success { get; set; }
    public int RespectGained { get; set; }
    public decimal MoneyGained { get; set; }
    public int HeatGained { get; set; }
    public string Message { get; set; } = "";
    public Dictionary<string, int> SkillGains { get; set; } = new();
}

/// <summary>
/// Handles applying mission consequences to the world state.
/// Updates NPC relationships based on mission outcomes.
/// </summary>
public static class MissionConsequenceHandler
{
    /// <summary>
    /// Relationship change values per mission type.
    /// Positive values improve relationship, negative values worsen it.
    /// </summary>
    private static readonly Dictionary<MissionType, (int Success, int Failure)> RelationshipChanges = new()
    {
        // Collection: -5 (resentment for taking money)
        [MissionType.Collection] = (-5, -3),

        // Intimidation success: -20 (fear), failure: -10 (contempt)
        [MissionType.Intimidation] = (-20, -10),

        // Negotiation: +15 (respect through diplomacy)
        [MissionType.Negotiation] = (15, 5),

        // Information: +5 (trust if paid for info)
        [MissionType.Information] = (5, 0),

        // Other mission types with default changes
        [MissionType.Hit] = (-30, -15),
        [MissionType.Territory] = (0, -5),
        [MissionType.Recruitment] = (10, 0)
    };

    /// <summary>
    /// Apply mission consequences to the world state.
    /// Updates NPC relationships and records interaction history.
    /// </summary>
    /// <param name="mission">The completed mission</param>
    /// <param name="result">The mission result</param>
    /// <param name="worldState">The world state to update</param>
    /// <returns>A description of the consequences applied, or null if no NPC was affected</returns>
    public static string? ApplyMissionConsequences(Mission mission, MissionResult result, WorldState? worldState)
    {
        if (worldState == null || string.IsNullOrEmpty(mission.NPCId))
        {
            return null;
        }

        var npc = worldState.GetNPC(mission.NPCId);
        if (npc == null)
        {
            return null;
        }

        // Get relationship change for this mission type
        var (successChange, failureChange) = RelationshipChanges.GetValueOrDefault(
            mission.Type,
            (0, 0) // Default: no change
        );

        var relationshipChange = result.Success ? successChange : failureChange;

        // Apply the relationship change
        var oldRelationship = npc.Relationship;
        npc.Relationship = Math.Clamp(npc.Relationship + relationshipChange, -100, 100);

        // Update NPC interaction history
        npc.LastInteractionWeek = worldState.CurrentWeek;
        npc.TotalInteractions++;
        npc.LastMissionId = mission.Id;

        // Record what happened in the interaction history
        var historyEntry = result.Success
            ? $"Week {worldState.CurrentWeek}: {mission.Type} mission completed successfully"
            : $"Week {worldState.CurrentWeek}: {mission.Type} mission failed";

        // Special history entries for intimidation
        if (mission.Type == MissionType.Intimidation && result.Success)
        {
            historyEntry = $"Week {worldState.CurrentWeek}: intimidated by player";
        }

        npc.InteractionHistory.Add(historyEntry);

        // Update NPC status based on new relationship
        UpdateNPCStatus(npc);

        // Build consequence description
        var direction = relationshipChange > 0 ? "improved" : (relationshipChange < 0 ? "worsened" : "unchanged");
        return $"{npc.Name}'s relationship {direction} ({oldRelationship} -> {npc.Relationship})";
    }

    /// <summary>
    /// Update NPC status based on their current relationship level.
    /// </summary>
    private static void UpdateNPCStatus(NPC npc)
    {
        // Very hostile NPCs become hostile in status
        if (npc.Relationship < Thresholds.DeepEnemy && npc.Status == NPCStatus.Active)
        {
            npc.Status = NPCStatus.Hostile;
        }
        // Hostile NPCs can calm down if relationship improves
        else if (npc.Relationship > Thresholds.Hostile && npc.Status == NPCStatus.Hostile)
        {
            npc.Status = NPCStatus.Active;
        }
    }

    // Lazy-initialized rules engine for consequence rules
    private static RulesEngineCore<ConsequenceContext>? _consequenceEngine;

    /// <summary>
    /// Get or create the consequence rules engine (lazy initialization).
    /// </summary>
    private static RulesEngineCore<ConsequenceContext> GetConsequenceEngine()
    {
        if (_consequenceEngine == null)
        {
            _consequenceEngine = new RulesEngineCore<ConsequenceContext>();
            ConsequenceRulesSetup.RegisterConsequenceRules(_consequenceEngine);
        }
        return _consequenceEngine;
    }

    /// <summary>
    /// Apply rule-based consequences to the world state after mission completion.
    /// This extends the basic relationship changes with richer world state modifications.
    ///
    /// Consequences include:
    /// - NPC status changes (Intimidated, Hostile, Dead)
    /// - Location state changes
    /// - Faction hostility changes
    /// - Revenge mission unlocking
    /// - Territory transfers
    /// </summary>
    /// <param name="mission">The completed mission</param>
    /// <param name="result">The mission result</param>
    /// <param name="worldState">The world state to update</param>
    /// <param name="storyGraph">The story graph for plot unlocking</param>
    /// <param name="gameState">The game state for player context</param>
    /// <returns>List of applied consequence descriptions</returns>
    public static List<string> ApplyConsequenceRules(
        Mission mission,
        MissionResult result,
        WorldState worldState,
        StoryGraph storyGraph,
        GameState gameState)
    {
        // Create consequence context
        var context = new ConsequenceContext
        {
            MissionId = mission.Id,
            MissionType = mission.Type.ToString(),
            Success = result.Success,
            World = worldState,
            Graph = storyGraph,
            PlayerRespect = gameState.Reputation,
            PlayerHeat = gameState.HeatLevel
        };

        // Resolve involved entities
        if (!string.IsNullOrEmpty(mission.LocationId))
        {
            context.Location = worldState.GetLocation(mission.LocationId);
        }

        if (!string.IsNullOrEmpty(mission.NPCId))
        {
            context.TargetNPC = worldState.GetNPC(mission.NPCId);

            // Try to find the faction this NPC belongs to
            if (context.TargetNPC?.FactionId != null)
            {
                context.TargetFaction = worldState.GetFaction(context.TargetNPC.FactionId);
            }
        }

        // Execute consequence rules
        var engine = GetConsequenceEngine();
        engine.EvaluateAll(context);

        return context.AppliedConsequences;
    }

    /// <summary>
    /// Record intel gathered from an Information mission.
    /// Creates an Intel object and adds it to the registry based on mission context.
    ///
    /// Intel types created:
    /// - Location intel: Local heat, owner, state
    /// - NPC intel: Status, relationship, faction
    /// - Faction intel: Hostility, resources, at war
    /// </summary>
    /// <param name="mission">The completed Information mission</param>
    /// <param name="result">The mission result (must be Success=true for intel)</param>
    /// <param name="worldState">The world state for context</param>
    /// <param name="intelRegistry">The registry to add intel to</param>
    /// <param name="playerCharacterName">The player's character name (source agent)</param>
    /// <returns>The created Intel object, or null if no intel was recorded</returns>
    public static Intel? RecordIntelFromMission(
        Mission mission,
        MissionResult result,
        WorldState worldState,
        IntelRegistry intelRegistry,
        string playerCharacterName = "Player")
    {
        // Only record intel from successful Information missions
        if (mission.Type != MissionType.Information || !result.Success)
        {
            return null;
        }

        Intel intel;

        // Determine intel type based on mission context
        if (!string.IsNullOrEmpty(mission.NPCId))
        {
            var npc = worldState.GetNPC(mission.NPCId);
            if (npc == null) return null;

            intel = new Intel
            {
                Type = IntelType.NpcActivity,
                SubjectId = npc.Id,
                SubjectType = "npc",
                Summary = $"Information gathered about {npc.Name}",
                SourceAgentId = playerCharacterName,
                Reliability = 70 + (result.RespectGained > 0 ? 10 : 0), // More respect = better info
                GatheredWeek = worldState.CurrentWeek,
                ExpiresWeek = worldState.CurrentWeek + 12, // Intel expires after 12 weeks
                Data = new Dictionary<string, object>
                {
                    ["Status"] = npc.Status.ToString(),
                    ["Relationship"] = npc.Relationship,
                    ["FactionId"] = npc.FactionId ?? "none",
                    ["Location"] = npc.LocationId
                }
            };

            // If NPC has a faction, also note faction info
            if (!string.IsNullOrEmpty(npc.FactionId))
            {
                var faction = worldState.GetFaction(npc.FactionId);
                if (faction != null)
                {
                    intel.Data["FactionHostility"] = faction.Hostility;
                    intel.Data["FactionResources"] = faction.Resources;
                }
            }
        }
        else if (!string.IsNullOrEmpty(mission.LocationId))
        {
            var location = worldState.GetLocation(mission.LocationId);
            if (location == null) return null;

            intel = new Intel
            {
                Type = IntelType.LocationStatus,
                SubjectId = location.Id,
                SubjectType = "location",
                Summary = $"Information gathered about {location.Name}",
                SourceAgentId = playerCharacterName,
                Reliability = 75,
                GatheredWeek = worldState.CurrentWeek,
                ExpiresWeek = worldState.CurrentWeek + 8, // Location intel expires faster
                Data = new Dictionary<string, object>
                {
                    ["State"] = location.State.ToString(),
                    ["LocalHeat"] = location.LocalHeat,
                    ["OwnerId"] = location.OwnerId ?? "none",
                    ["WeeklyValue"] = location.WeeklyValue
                }
            };
        }
        else
        {
            // Generic intel without specific target
            intel = new Intel
            {
                Type = IntelType.Rumor,
                SubjectId = "general",
                SubjectType = "general",
                Summary = $"General intelligence gathered: {mission.Description}",
                SourceAgentId = playerCharacterName,
                Reliability = 50,
                GatheredWeek = worldState.CurrentWeek,
                ExpiresWeek = worldState.CurrentWeek + 4 // Rumors expire quickly
            };
        }

        // Add to registry
        intelRegistry.Add(intel);

        return intel;
    }
}
