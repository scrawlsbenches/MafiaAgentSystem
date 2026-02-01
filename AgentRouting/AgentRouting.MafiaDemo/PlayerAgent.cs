using AgentRouting.MafiaDemo.Missions;
using AgentRouting.MafiaDemo.Game;
using AgentRouting.Core;
using AgentRouting.Middleware;
using RulesEngine.Core;

namespace AgentRouting.MafiaDemo.AI;

/// <summary>
/// Decision context for player AI
/// </summary>
public class PlayerDecisionContext
{
    public PlayerCharacter Player { get; set; } = null!;
    public Mission? Mission { get; set; }
    public GameState GameState { get; set; } = null!;
    
    // Calculated properties
    public bool IsLowOnMoney => Player.Money < 500;
    public bool IsRich => Player.Money > 10000;
    public bool HasLowRespect => Player.Respect < 30;
    public bool HasHighRespect => Player.Respect > 70;
    public bool UnderHeat => Player.Heat > 60;
    public bool SafeOperations => Player.Heat < 30;
    
    // Mission analysis
    public bool MissionIsSafe => Mission?.RiskLevel < 4;
    public bool MissionIsRisky => Mission?.RiskLevel >= 7;
    public bool MissionIsHighReward => Mission?.RespectReward > 10 || Mission?.MoneyReward > 1000;
    public bool CanAffordRisk => Player.Respect > 40 && Player.Money > 2000;
    
    // Skill check
    public bool MeetsSkillRequirements => Mission?.SkillRequirements.All(req => 
        Player.Skills.GetSkill(req.Key) >= req.Value) ?? true;
    public bool OverqualifiedForMission => Mission?.SkillRequirements.All(req =>
        Player.Skills.GetSkill(req.Key) >= req.Value + 20) ?? false;
    
    // Career progression
    public bool ReadyForPromotion => Player.Respect > GetPromotionThreshold();
    public bool IsEarlyCareer => Player.Week < 10;
    public bool IsMidCareer => Player.Week >= 10 && Player.Week < 30;
    public bool IsLateCareer => Player.Week >= 30;
    
    // Personality driven
    public bool ShouldTakeRisk => Player.Personality.IsReckless || 
                                  (Player.Personality.IsAmbitious && CanAffordRisk);
    public bool ShouldBeCautious => Player.Personality.IsCautious || UnderHeat;
    
    private int GetPromotionThreshold()
    {
        return Player.Rank switch
        {
            PlayerRank.Associate => 40,
            PlayerRank.Soldier => 70,
            PlayerRank.Capo => 85,
            PlayerRank.Underboss => 95,
            _ => 100
        };
    }
}

/// <summary>
/// Autonomous agent that plays the mafia game
/// Uses rules engine to make all decisions
/// </summary>
public class PlayerAgent
{
    private readonly PlayerCharacter _character;
    private readonly MissionGenerator _missionGenerator;
    private readonly MissionEvaluator _missionEvaluator;
    private readonly RulesEngineCore<PlayerDecisionContext> _decisionRules;
    private readonly Random _random = new();
    private readonly AgentRouter? _router;

    public PlayerCharacter Character => _character;
    public AgentRouter? Router => _router;

    public PlayerAgent(string name, PlayerPersonality? personality = null, AgentRouter? router = null)
    {
        _character = new PlayerCharacter
        {
            Name = name,
            Personality = personality ?? GenerateRandomPersonality()
        };
        
        _router = router;
        _missionGenerator = new MissionGenerator();
        _missionEvaluator = new MissionEvaluator();
        _decisionRules = new RulesEngineCore<PlayerDecisionContext>();
        
        SetupDecisionRules();
    }
    
    private PlayerPersonality GenerateRandomPersonality()
    {
        return new PlayerPersonality
        {
            Ambition = _random.Next(40, 90),
            Loyalty = _random.Next(60, 95),
            Ruthlessness = _random.Next(20, 70),
            Caution = _random.Next(30, 80)
        };
    }
    
    private void SetupDecisionRules()
    {
        // === MISSION ACCEPTANCE RULES ===
        
        // Always accept if desperate for money
        _decisionRules.AddRule(
            "ACCEPT_DESPERATE",
            "Accept Mission - Desperate",
            ctx => ctx.IsLowOnMoney && ctx.MissionIsSafe,
            ctx => { /* Accept */ },
            priority: 1000
        );
        
        // Reject if skills are too low
        _decisionRules.AddRule(
            "REJECT_UNDERQUALIFIED",
            "Reject - Underqualified",
            ctx => ctx.Mission != null && !ctx.MeetsSkillRequirements,
            ctx => { /* Reject */ },
            priority: 950
        );
        
        // Reject if under too much heat
        _decisionRules.AddRule(
            "REJECT_TOO_HOT",
            "Reject - Too Much Heat",
            ctx => ctx.UnderHeat && ctx.MissionIsRisky,
            ctx => { /* Reject */ },
            priority: 900
        );
        
        // Accept high reward missions if can afford risk
        _decisionRules.AddRule(
            "ACCEPT_HIGH_REWARD",
            "Accept - High Reward",
            ctx => ctx.MissionIsHighReward && ctx.CanAffordRisk,
            ctx => { /* Accept */ },
            priority: 850
        );
        
        // Ambitious players take more risks
        _decisionRules.AddRule(
            "ACCEPT_AMBITIOUS",
            "Accept - Ambitious Personality",
            ctx => ctx.Player.Personality.IsAmbitious && ctx.CanAffordRisk,
            ctx => { /* Accept */ },
            priority: 800
        );
        
        // Cautious players avoid risks
        _decisionRules.AddRule(
            "REJECT_CAUTIOUS",
            "Reject - Cautious Personality",
            ctx => ctx.Player.Personality.IsCautious && ctx.MissionIsRisky,
            ctx => { /* Reject */ },
            priority: 800
        );
        
        // Accept safe missions when building reputation
        _decisionRules.AddRule(
            "ACCEPT_SAFE_BUILDING",
            "Accept Safe Mission - Building Rep",
            ctx => ctx.HasLowRespect && ctx.MissionIsSafe,
            ctx => { /* Accept */ },
            priority: 700
        );
        
        // Default: Accept if meets basic criteria
        _decisionRules.AddRule(
            "ACCEPT_DEFAULT",
            "Accept - Default",
            ctx => ctx.MeetsSkillRequirements && !ctx.UnderHeat,
            ctx => { /* Accept */ },
            priority: 500
        );
    }
    
    /// <summary>
    /// Decide whether to accept a mission
    /// </summary>
    public MissionDecision DecideMission(Mission mission, GameState gameState)
    {
        var context = new PlayerDecisionContext
        {
            Player = _character,
            Mission = mission,
            GameState = gameState
        };
        
        var matchedRules = _decisionRules.GetMatchingRules(context);
        
        if (!matchedRules.Any())
        {
            // No rules matched - reject by default
            return new MissionDecision
            {
                Accept = false,
                Reason = "No compelling reason to take this mission",
                RuleMatched = "NONE"
            };
        }
        
        // Check highest priority matched rule
        var topRule = matchedRules.First();
        
        // Rules with REJECT in name = reject, otherwise accept
        var accept = !topRule.Name.Contains("REJECT");
        
        return new MissionDecision
        {
            Accept = accept,
            Reason = topRule.Description,
            RuleMatched = topRule.Name,
            Confidence = CalculateConfidence(context, accept)
        };
    }
    
    private int CalculateConfidence(PlayerDecisionContext context, bool accepting)
    {
        var confidence = 50;
        
        if (accepting)
        {
            if (context.OverqualifiedForMission) confidence += 30;
            if (context.MissionIsSafe) confidence += 20;
            if (context.SafeOperations) confidence += 15;
            if (context.MissionIsHighReward) confidence += 10;
        }
        else
        {
            if (!context.MeetsSkillRequirements) confidence += 40;
            if (context.UnderHeat) confidence += 30;
            if (context.MissionIsRisky) confidence += 20;
        }
        
        return Math.Min(100, confidence);
    }
    
    /// <summary>
    /// Execute mission - NOW USES MIDDLEWARE if available!
    /// </summary>
    public async Task<MissionExecutionResult> ExecuteMissionAsync(Mission mission, GameState gameState)
    {
        MissionResult result;
        
        // If router with middleware is available, use it!
        if (_router != null)
        {
            // Create message to agent
            var message = new AgentMessage
            {
                SenderId = $"player-{_character.Name.Replace(" ", "-").ToLower()}",
                ReceiverId = mission.AssignedBy,
                Subject = $"Mission: {mission.Title}",
                Content = $"I'm ready to handle this mission. {mission.Description}",
                Category = mission.Type.ToString(),
                Priority = mission.RiskLevel >= 7 ? MessagePriority.High : MessagePriority.Normal,
                Metadata = new Dictionary<string, object>
                {
                    ["MissionId"] = mission.Id,
                    ["MissionType"] = mission.Type.ToString(),
                    ["PlayerRank"] = _character.Rank.ToString(),
                    ["PlayerRespect"] = _character.Respect,
                    ["PlayerMoney"] = _character.Money,
                    ["PlayerHeat"] = _character.Heat
                }
            };
            
            // Route through middleware pipeline!
            var agentResponse = await _router.RouteMessageAsync(message);
            
            // Evaluate mission normally
            result = _missionEvaluator.EvaluateMission(mission, _character);
            
            // Check if middleware modified the outcome
            if (agentResponse.Data.ContainsKey("BonusRespect"))
            {
                result.RespectGained += (int)agentResponse.Data["BonusRespect"];
            }
            if (agentResponse.Data.ContainsKey("BonusMoney"))
            {
                result.MoneyGained += (decimal)agentResponse.Data["BonusMoney"];
            }
        }
        else
        {
            // Fallback to direct evaluation
            result = _missionEvaluator.EvaluateMission(mission, _character);
        }
        
        // Apply results to character
        _character.Respect += result.RespectGained;
        _character.Money += result.MoneyGained;
        _character.Heat += result.HeatGained;
        
        // Apply skill gains
        foreach (var (skill, gain) in result.SkillGains)
        {
            ApplySkillGain(skill, gain);
        }
        
        // Track mission
        if (result.Success)
        {
            mission.Status = MissionStatus.Completed;
            mission.Success = true;
            _character.CompletedMissions.Add(mission);
        }
        else
        {
            mission.Status = MissionStatus.Failed;
            mission.Success = false;
        }
        
        mission.CompletedAt = DateTime.UtcNow;
        mission.Outcome = result.Message;
        
        // Check for promotion
        CheckPromotion();
        
        // Clean up stats
        _character.Respect = Math.Max(0, Math.Min(100, _character.Respect));
        _character.Heat = Math.Max(0, Math.Min(100, _character.Heat));
        
        return new MissionExecutionResult
        {
            MissionResult = result,
            PromotionEarned = null,
            NewSkills = result.SkillGains
        };
    }
    
    private void ApplySkillGain(string skillName, int gain)
    {
        switch (skillName.ToLower())
        {
            case "intimidation":
                _character.Skills.Intimidation = Math.Min(100, _character.Skills.Intimidation + gain);
                break;
            case "negotiation":
                _character.Skills.Negotiation = Math.Min(100, _character.Skills.Negotiation + gain);
                break;
            case "streetsmarts":
                _character.Skills.StreetSmarts = Math.Min(100, _character.Skills.StreetSmarts + gain);
                break;
            case "leadership":
                _character.Skills.Leadership = Math.Min(100, _character.Skills.Leadership + gain);
                break;
            case "business":
                _character.Skills.Business = Math.Min(100, _character.Skills.Business + gain);
                break;
        }
    }
    
    private void CheckPromotion()
    {
        var promoted = false;
        var oldRank = _character.Rank;
        
        switch (_character.Rank)
        {
            case PlayerRank.Associate when _character.Respect >= 40:
                _character.Rank = PlayerRank.Soldier;
                promoted = true;
                break;
            case PlayerRank.Soldier when _character.Respect >= 70:
                _character.Rank = PlayerRank.Capo;
                promoted = true;
                break;
            case PlayerRank.Capo when _character.Respect >= 85:
                _character.Rank = PlayerRank.Underboss;
                promoted = true;
                break;
            case PlayerRank.Underboss when _character.Respect >= 95:
                _character.Rank = PlayerRank.Don;
                promoted = true;
                break;
        }
        
        if (promoted)
        {
            _character.Achievements.Add($"Promoted to {_character.Rank} in week {_character.Week}");
        }
    }
    
    /// <summary>
    /// Advance one week - generate mission and decide
    /// </summary>
    public async Task<WeekResult> ProcessWeekAsync(GameState gameState)
    {
        _character.Week++;
        
        // Heat naturally decreases over time
        _character.Heat = Math.Max(0, _character.Heat - 3);
        
        // Generate a mission
        var mission = _missionGenerator.GenerateMission(_character, gameState);
        
        // Decide whether to accept
        var decision = DecideMission(mission, gameState);
        
        MissionExecutionResult? executionResult = null;
        
        if (decision.Accept)
        {
            executionResult = await ExecuteMissionAsync(mission, gameState);
        }
        
        return new WeekResult
        {
            Week = _character.Week,
            GeneratedMission = mission,
            Decision = decision,
            ExecutionResult = executionResult
        };
    }
    
    /// <summary>
    /// Get character summary
    /// </summary>
    public string GetSummary()
    {
        var middlewareStatus = _router != null 
            ? "✓ Using AgentRouter with Middleware Pipeline" 
            : "⚠ Direct execution mode (no middleware)";
            
        return $@"
═══════════════════════════════════════════════════════════
{_character.Name} - {_character.Rank}
═══════════════════════════════════════════════════════════

Week: {_character.Week}
Respect: {_character.Respect}/100
Money: ${_character.Money:N0}
Heat: {_character.Heat}/100

Skills:
  Intimidation: {_character.Skills.Intimidation}
  Negotiation: {_character.Skills.Negotiation}
  Street Smarts: {_character.Skills.StreetSmarts}
  Leadership: {_character.Skills.Leadership}
  Business: {_character.Skills.Business}

Personality:
  Ambition: {_character.Personality.Ambition}/100 {(_character.Personality.IsAmbitious ? "(Ambitious)" : "")}
  Loyalty: {_character.Personality.Loyalty}/100 {(_character.Personality.IsLoyal ? "(Loyal)" : "")}
  Ruthlessness: {_character.Personality.Ruthlessness}/100 {(_character.Personality.IsRuthless ? "(Ruthless)" : "")}
  Caution: {_character.Personality.Caution}/100 {(_character.Personality.IsCautious ? "(Cautious)" : "")}

Missions Completed: {_character.CompletedMissions.Count}
Achievements: {_character.Achievements.Count}

{middlewareStatus}
";
    }
}

public class MissionDecision
{
    public bool Accept { get; set; }
    public string Reason { get; set; } = "";
    public string RuleMatched { get; set; } = "";
    public int Confidence { get; set; } // 0-100
}

public class MissionExecutionResult
{
    public MissionResult MissionResult { get; set; } = null!;
    public PlayerRank? PromotionEarned { get; set; }
    public Dictionary<string, int> NewSkills { get; set; } = new();
}

public class WeekResult
{
    public int Week { get; set; }
    public Mission GeneratedMission { get; set; } = null!;
    public MissionDecision Decision { get; set; } = null!;
    public MissionExecutionResult? ExecutionResult { get; set; }
}
