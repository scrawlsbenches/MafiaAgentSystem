using AgentRouting.MafiaDemo.Game;
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
    public Mission GenerateMission(PlayerCharacter player, GameState gameState)
    {
        var missionType = SelectMissionType(player.Rank, gameState);
        
        return missionType switch
        {
            MissionType.Collection => GenerateCollectionMission(player),
            MissionType.Intimidation => GenerateIntimidationMission(player),
            MissionType.Information => GenerateInformationMission(player),
            MissionType.Negotiation => GenerateNegotiationMission(player),
            MissionType.Hit => GenerateHitMission(player),
            MissionType.Territory => GenerateTerritoryMission(player),
            MissionType.Recruitment => GenerateRecruitmentMission(player),
            _ => GenerateCollectionMission(player)
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
    
    private Mission GenerateCollectionMission(PlayerCharacter player)
    {
        var businesses = new[] 
        { 
            "Tony's Restaurant", "Luigi's Bakery", "Marino's Deli", 
            "Sal's Bar", "Vinnie's Grocery", "Angelo's Butcher Shop" 
        };
        
        var business = businesses[Random.Shared.Next(businesses.Length)];
        var amount = Random.Shared.Next(300, 800);
        
        return new Mission
        {
            Title = $"Collect from {business}",
            Description = $"Go collect the weekly payment from {business}. They owe ${amount}.",
            Type = MissionType.Collection,
            AssignedBy = player.Rank >= PlayerRank.Capo ? "underboss-001" : "capo-001",
            MinimumRank = 0,
            RiskLevel = Random.Shared.Next(1, 4),
            RespectReward = 3,
            MoneyReward = amount * 0.25m, // Player gets 25% cut
            HeatGenerated = 2,
            Data = new Dictionary<string, object>
            {
                ["BusinessName"] = business,
                ["AmountOwed"] = amount
            }
        };
    }
    
    private Mission GenerateIntimidationMission(PlayerCharacter player)
    {
        var targets = new[]
        {
            ("shopkeeper", "been late with payments"),
            ("dock worker", "been talking to the cops"),
            ("rival associate", "disrespected the family"),
            ("bar owner", "refused protection")
        };
        
        var (target, reason) = targets[Random.Shared.Next(targets.Length)];
        
        return new Mission
        {
            Title = $"Send a message to the {target}",
            Description = $"The {target} has {reason}. Make sure they understand this can't continue.",
            Type = MissionType.Intimidation,
            AssignedBy = "capo-001",
            MinimumRank = 0,
            RiskLevel = Random.Shared.Next(3, 6),
            RespectReward = 5,
            MoneyReward = 100m,
            HeatGenerated = Random.Shared.Next(3, 7),
            SkillRequirements = new Dictionary<string, int>
            {
                ["Intimidation"] = 15
            },
            Data = new Dictionary<string, object>
            {
                ["Target"] = target,
                ["Reason"] = reason
            }
        };
    }
    
    private Mission GenerateInformationMission(PlayerCharacter player)
    {
        var scenarios = new[]
        {
            ("who's stealing from the docks", "find the thief and report back"),
            ("which cop is on Barzini's payroll", "we need to know who we can trust"),
            ("where the Tattaglias are hiding their money", "we might need this information later"),
            ("who's been talking to the Feds", "there's an informant somewhere")
        };
        
        var (subject, goal) = scenarios[Random.Shared.Next(scenarios.Length)];
        
        return new Mission
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
                ["StreetSmarts"] = 20
            },
            Data = new Dictionary<string, object>
            {
                ["Subject"] = subject
            }
        };
    }
    
    private Mission GenerateNegotiationMission(PlayerCharacter player)
    {
        var scenarios = new[]
        {
            ("Tattaglia family", "negotiate a truce"),
            ("union boss", "secure favorable terms"),
            ("city councilman", "arrange a meeting with the Don"),
            ("business owner", "convince them to accept protection")
        };
        
        var (party, goal) = scenarios[Random.Shared.Next(scenarios.Length)];
        
        return new Mission
        {
            Title = $"Negotiate with the {party}",
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
            }
        };
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
            RespectReward = 25,
            MoneyReward = 5000m,
            HeatGenerated = 30,
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
    
    private Mission GenerateTerritoryMission(PlayerCharacter player)
    {
        var scenarios = new[]
        {
            ("expand into the Bronx gambling scene", "set up new operations"),
            ("defend Little Italy from Tattaglia incursion", "show them it's our territory"),
            ("take over the Brooklyn docks", "increase our smuggling operations")
        };
        
        var (area, goal) = scenarios[Random.Shared.Next(scenarios.Length)];
        
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
            }
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
        
        // Apply final bonuses/penalties
        _rules.EvaluateAll(context);
        
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
