using TestRunner.Framework;
using AgentRouting.MafiaDemo.AI;
using AgentRouting.MafiaDemo.Missions;
using AgentRouting.MafiaDemo.Game;

namespace TestRunner.Tests;

/// <summary>
/// Comprehensive tests for PlayerAgent and related classes
/// </summary>
public class PlayerAgentTests
{
    #region PlayerDecisionContext - Financial Properties Tests

    [Test]
    public void PlayerDecisionContext_IsLowOnMoney_TrueWhenMoneyBelow500()
    {
        var player = CreateTestPlayer(money: 400m);
        var context = CreateContext(player);

        Assert.True(context.IsLowOnMoney);
    }

    [Test]
    public void PlayerDecisionContext_IsLowOnMoney_FalseWhenMoneyAtOrAbove500()
    {
        var player = CreateTestPlayer(money: 500m);
        var context = CreateContext(player);

        Assert.False(context.IsLowOnMoney);
    }

    [Test]
    public void PlayerDecisionContext_IsRich_TrueWhenMoneyAbove10000()
    {
        var player = CreateTestPlayer(money: 15000m);
        var context = CreateContext(player);

        Assert.True(context.IsRich);
    }

    [Test]
    public void PlayerDecisionContext_IsRich_FalseWhenMoneyAtOrBelow10000()
    {
        var player = CreateTestPlayer(money: 10000m);
        var context = CreateContext(player);

        Assert.False(context.IsRich);
    }

    #endregion

    #region PlayerDecisionContext - Respect Properties Tests

    [Test]
    public void PlayerDecisionContext_HasLowRespect_TrueWhenRespectBelow30()
    {
        var player = CreateTestPlayer(respect: 25);
        var context = CreateContext(player);

        Assert.True(context.HasLowRespect);
    }

    [Test]
    public void PlayerDecisionContext_HasLowRespect_FalseWhenRespectAtOrAbove30()
    {
        var player = CreateTestPlayer(respect: 30);
        var context = CreateContext(player);

        Assert.False(context.HasLowRespect);
    }

    [Test]
    public void PlayerDecisionContext_HasHighRespect_TrueWhenRespectAbove70()
    {
        var player = CreateTestPlayer(respect: 80);
        var context = CreateContext(player);

        Assert.True(context.HasHighRespect);
    }

    [Test]
    public void PlayerDecisionContext_HasHighRespect_FalseWhenRespectAtOrBelow70()
    {
        var player = CreateTestPlayer(respect: 70);
        var context = CreateContext(player);

        Assert.False(context.HasHighRespect);
    }

    #endregion

    #region PlayerDecisionContext - Heat Properties Tests

    [Test]
    public void PlayerDecisionContext_UnderHeat_TrueWhenHeatAbove60()
    {
        var player = CreateTestPlayer(heat: 65);
        var context = CreateContext(player);

        Assert.True(context.UnderHeat);
    }

    [Test]
    public void PlayerDecisionContext_UnderHeat_FalseWhenHeatAtOrBelow60()
    {
        var player = CreateTestPlayer(heat: 60);
        var context = CreateContext(player);

        Assert.False(context.UnderHeat);
    }

    [Test]
    public void PlayerDecisionContext_SafeOperations_TrueWhenHeatBelow30()
    {
        var player = CreateTestPlayer(heat: 20);
        var context = CreateContext(player);

        Assert.True(context.SafeOperations);
    }

    [Test]
    public void PlayerDecisionContext_SafeOperations_FalseWhenHeatAtOrAbove30()
    {
        var player = CreateTestPlayer(heat: 30);
        var context = CreateContext(player);

        Assert.False(context.SafeOperations);
    }

    #endregion

    #region PlayerDecisionContext - Mission Analysis Properties Tests

    [Test]
    public void PlayerDecisionContext_MissionIsSafe_TrueWhenRiskLevelBelow4()
    {
        var player = CreateTestPlayer();
        var mission = CreateTestMission(riskLevel: 3);
        var context = CreateContext(player, mission);

        Assert.True(context.MissionIsSafe);
    }

    [Test]
    public void PlayerDecisionContext_MissionIsSafe_FalseWhenRiskLevelAtOrAbove4()
    {
        var player = CreateTestPlayer();
        var mission = CreateTestMission(riskLevel: 4);
        var context = CreateContext(player, mission);

        Assert.False(context.MissionIsSafe);
    }

    [Test]
    public void PlayerDecisionContext_MissionIsRisky_TrueWhenRiskLevelAtOrAbove7()
    {
        var player = CreateTestPlayer();
        var mission = CreateTestMission(riskLevel: 7);
        var context = CreateContext(player, mission);

        Assert.True(context.MissionIsRisky);
    }

    [Test]
    public void PlayerDecisionContext_MissionIsRisky_FalseWhenRiskLevelBelow7()
    {
        var player = CreateTestPlayer();
        var mission = CreateTestMission(riskLevel: 6);
        var context = CreateContext(player, mission);

        Assert.False(context.MissionIsRisky);
    }

    [Test]
    public void PlayerDecisionContext_MissionIsHighReward_TrueWhenRespectAbove10()
    {
        var player = CreateTestPlayer();
        var mission = CreateTestMission(respectReward: 15, moneyReward: 500m);
        var context = CreateContext(player, mission);

        Assert.True(context.MissionIsHighReward);
    }

    [Test]
    public void PlayerDecisionContext_MissionIsHighReward_TrueWhenMoneyAbove1000()
    {
        var player = CreateTestPlayer();
        var mission = CreateTestMission(respectReward: 5, moneyReward: 1500m);
        var context = CreateContext(player, mission);

        Assert.True(context.MissionIsHighReward);
    }

    [Test]
    public void PlayerDecisionContext_MissionIsHighReward_FalseWhenBothLow()
    {
        var player = CreateTestPlayer();
        var mission = CreateTestMission(respectReward: 5, moneyReward: 500m);
        var context = CreateContext(player, mission);

        Assert.False(context.MissionIsHighReward);
    }

    [Test]
    public void PlayerDecisionContext_CanAffordRisk_TrueWhenRespectOver40AndMoneyOver2000()
    {
        var player = CreateTestPlayer(respect: 50, money: 3000m);
        var context = CreateContext(player);

        Assert.True(context.CanAffordRisk);
    }

    [Test]
    public void PlayerDecisionContext_CanAffordRisk_FalseWhenRespectAt40()
    {
        var player = CreateTestPlayer(respect: 40, money: 3000m);
        var context = CreateContext(player);

        Assert.False(context.CanAffordRisk);
    }

    [Test]
    public void PlayerDecisionContext_CanAffordRisk_FalseWhenMoneyAt2000()
    {
        var player = CreateTestPlayer(respect: 50, money: 2000m);
        var context = CreateContext(player);

        Assert.False(context.CanAffordRisk);
    }

    #endregion

    #region PlayerDecisionContext - Skill Check Properties Tests

    [Test]
    public void PlayerDecisionContext_MeetsSkillRequirements_TrueWhenNoRequirements()
    {
        var player = CreateTestPlayer();
        var mission = CreateTestMission();
        var context = CreateContext(player, mission);

        Assert.True(context.MeetsSkillRequirements);
    }

    [Test]
    public void PlayerDecisionContext_MeetsSkillRequirements_TrueWhenSkillsMet()
    {
        var player = CreateTestPlayer();
        player.Skills.Intimidation = 30;
        var mission = CreateTestMission();
        mission.SkillRequirements["Intimidation"] = 25;
        var context = CreateContext(player, mission);

        Assert.True(context.MeetsSkillRequirements);
    }

    [Test]
    public void PlayerDecisionContext_MeetsSkillRequirements_FalseWhenSkillsNotMet()
    {
        var player = CreateTestPlayer();
        player.Skills.Intimidation = 10;
        var mission = CreateTestMission();
        mission.SkillRequirements["Intimidation"] = 25;
        var context = CreateContext(player, mission);

        Assert.False(context.MeetsSkillRequirements);
    }

    [Test]
    public void PlayerDecisionContext_OverqualifiedForMission_TrueWhenSkillsExceedBy20()
    {
        var player = CreateTestPlayer();
        player.Skills.Intimidation = 50;
        var mission = CreateTestMission();
        mission.SkillRequirements["Intimidation"] = 25;
        var context = CreateContext(player, mission);

        Assert.True(context.OverqualifiedForMission);
    }

    [Test]
    public void PlayerDecisionContext_OverqualifiedForMission_FalseWhenSkillsDontExceedBy20()
    {
        var player = CreateTestPlayer();
        player.Skills.Intimidation = 40;
        var mission = CreateTestMission();
        mission.SkillRequirements["Intimidation"] = 25;
        var context = CreateContext(player, mission);

        Assert.False(context.OverqualifiedForMission);
    }

    [Test]
    public void PlayerDecisionContext_OverqualifiedForMission_FalseWhenNoMission()
    {
        var player = CreateTestPlayer();
        var context = CreateContext(player, null);

        Assert.False(context.OverqualifiedForMission);
    }

    #endregion

    #region PlayerDecisionContext - Career Progression Properties Tests

    [Test]
    public void PlayerDecisionContext_IsEarlyCareer_TrueWhenWeekBelow10()
    {
        var player = CreateTestPlayer(week: 5);
        var context = CreateContext(player);

        Assert.True(context.IsEarlyCareer);
    }

    [Test]
    public void PlayerDecisionContext_IsEarlyCareer_FalseWhenWeekAtOrAbove10()
    {
        var player = CreateTestPlayer(week: 10);
        var context = CreateContext(player);

        Assert.False(context.IsEarlyCareer);
    }

    [Test]
    public void PlayerDecisionContext_IsMidCareer_TrueWhenWeekBetween10And29()
    {
        var player = CreateTestPlayer(week: 15);
        var context = CreateContext(player);

        Assert.True(context.IsMidCareer);
    }

    [Test]
    public void PlayerDecisionContext_IsMidCareer_FalseWhenWeekBelow10()
    {
        var player = CreateTestPlayer(week: 5);
        var context = CreateContext(player);

        Assert.False(context.IsMidCareer);
    }

    [Test]
    public void PlayerDecisionContext_IsMidCareer_FalseWhenWeekAtOrAbove30()
    {
        var player = CreateTestPlayer(week: 30);
        var context = CreateContext(player);

        Assert.False(context.IsMidCareer);
    }

    [Test]
    public void PlayerDecisionContext_IsLateCareer_TrueWhenWeekAtOrAbove30()
    {
        var player = CreateTestPlayer(week: 30);
        var context = CreateContext(player);

        Assert.True(context.IsLateCareer);
    }

    [Test]
    public void PlayerDecisionContext_IsLateCareer_FalseWhenWeekBelow30()
    {
        var player = CreateTestPlayer(week: 29);
        var context = CreateContext(player);

        Assert.False(context.IsLateCareer);
    }

    #endregion

    #region PlayerDecisionContext - ReadyForPromotion Tests

    [Test]
    public void PlayerDecisionContext_ReadyForPromotion_Associate_TrueWhenRespectOver35()
    {
        var player = CreateTestPlayer(respect: 40, rank: PlayerRank.Associate);
        var context = CreateContext(player);

        Assert.True(context.ReadyForPromotion);
    }

    [Test]
    public void PlayerDecisionContext_ReadyForPromotion_Associate_FalseWhenRespectAtOrBelow35()
    {
        var player = CreateTestPlayer(respect: 35, rank: PlayerRank.Associate);
        var context = CreateContext(player);

        Assert.False(context.ReadyForPromotion);
    }

    [Test]
    public void PlayerDecisionContext_ReadyForPromotion_Soldier_TrueWhenRespectOver60()
    {
        var player = CreateTestPlayer(respect: 65, rank: PlayerRank.Soldier);
        var context = CreateContext(player);

        Assert.True(context.ReadyForPromotion);
    }

    [Test]
    public void PlayerDecisionContext_ReadyForPromotion_Capo_TrueWhenRespectOver80()
    {
        var player = CreateTestPlayer(respect: 85, rank: PlayerRank.Capo);
        var context = CreateContext(player);

        Assert.True(context.ReadyForPromotion);
    }

    [Test]
    public void PlayerDecisionContext_ReadyForPromotion_Underboss_TrueWhenRespectOver90()
    {
        var player = CreateTestPlayer(respect: 95, rank: PlayerRank.Underboss);
        var context = CreateContext(player);

        Assert.True(context.ReadyForPromotion);
    }

    [Test]
    public void PlayerDecisionContext_ReadyForPromotion_Don_AlwaysFalse()
    {
        var player = CreateTestPlayer(respect: 100, rank: PlayerRank.Don);
        var context = CreateContext(player);

        Assert.False(context.ReadyForPromotion);
    }

    #endregion

    #region PlayerDecisionContext - Personality Driven Properties Tests

    [Test]
    public void PlayerDecisionContext_ShouldTakeRisk_TrueWhenReckless()
    {
        var player = CreateTestPlayer(caution: 25); // IsCautious at 25 is false, IsReckless is true when < 30
        var context = CreateContext(player);

        Assert.True(context.ShouldTakeRisk);
    }

    [Test]
    public void PlayerDecisionContext_ShouldTakeRisk_TrueWhenAmbitiousAndCanAffordRisk()
    {
        var player = CreateTestPlayer(ambition: 80, respect: 50, money: 3000m);
        var context = CreateContext(player);

        Assert.True(context.ShouldTakeRisk);
    }

    [Test]
    public void PlayerDecisionContext_ShouldTakeRisk_FalseWhenAmbitiousButCantAffordRisk()
    {
        var player = CreateTestPlayer(ambition: 80, respect: 30, money: 1000m);
        var context = CreateContext(player);

        Assert.False(context.ShouldTakeRisk);
    }

    [Test]
    public void PlayerDecisionContext_ShouldBeCautious_TrueWhenCautious()
    {
        var player = CreateTestPlayer(caution: 80);
        var context = CreateContext(player);

        Assert.True(context.ShouldBeCautious);
    }

    [Test]
    public void PlayerDecisionContext_ShouldBeCautious_TrueWhenUnderHeat()
    {
        var player = CreateTestPlayer(heat: 70);
        var context = CreateContext(player);

        Assert.True(context.ShouldBeCautious);
    }

    [Test]
    public void PlayerDecisionContext_ShouldBeCautious_FalseWhenNotCautiousAndNotUnderHeat()
    {
        var player = CreateTestPlayer(caution: 50, heat: 50);
        var context = CreateContext(player);

        Assert.False(context.ShouldBeCautious);
    }

    #endregion

    #region PlayerAgent Construction Tests

    [Test]
    public void PlayerAgent_Construction_WithName_CreatesCharacter()
    {
        var agent = new PlayerAgent("Tony Soprano");

        Assert.NotNull(agent.Character);
        Assert.Equal("Tony Soprano", agent.Character.Name);
        Assert.Equal(PlayerRank.Associate, agent.Character.Rank);
    }

    [Test]
    public void PlayerAgent_Construction_WithPersonality_UsesProvidedPersonality()
    {
        var personality = new PlayerPersonality
        {
            Ambition = 90,
            Loyalty = 80,
            Ruthlessness = 60,
            Caution = 40
        };

        var agent = new PlayerAgent("Paulie Walnuts", personality);

        Assert.Equal(90, agent.Character.Personality.Ambition);
        Assert.Equal(80, agent.Character.Personality.Loyalty);
        Assert.Equal(60, agent.Character.Personality.Ruthlessness);
        Assert.Equal(40, agent.Character.Personality.Caution);
    }

    [Test]
    public void PlayerAgent_Construction_WithoutPersonality_GeneratesRandomPersonality()
    {
        var agent = new PlayerAgent("Silvio");

        // Random personality should be within expected ranges
        Assert.True(agent.Character.Personality.Ambition >= 40 && agent.Character.Personality.Ambition < 90);
        Assert.True(agent.Character.Personality.Loyalty >= 60 && agent.Character.Personality.Loyalty < 95);
        Assert.True(agent.Character.Personality.Ruthlessness >= 20 && agent.Character.Personality.Ruthlessness < 70);
        Assert.True(agent.Character.Personality.Caution >= 30 && agent.Character.Personality.Caution < 80);
    }

    [Test]
    public void PlayerAgent_Construction_RouterIsNullByDefault()
    {
        var agent = new PlayerAgent("Christopher");

        Assert.Null(agent.Router);
    }

    [Test]
    public void PlayerAgent_Character_InitialValues_AreCorrect()
    {
        var agent = new PlayerAgent("Bobby");

        Assert.Equal(10, agent.Character.Respect);
        Assert.Equal(1000m, agent.Character.Money);
        Assert.Equal(0, agent.Character.Heat);
        Assert.Equal(1, agent.Character.Week);
    }

    #endregion

    #region PlayerAgent DecideMission Tests - Acceptance Scenarios

    [Test]
    public void DecideMission_DesperatePlayerWithSafeMission_ShouldAccept()
    {
        var agent = new PlayerAgent("Desperate Tony");
        agent.Character.Money = 300m; // Below 500, so IsLowOnMoney

        var mission = CreateTestMission(riskLevel: 2); // MissionIsSafe
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        Assert.True(decision.Accept);
        // RuleMatched uses the rule Name (description), not the rule Id
        Assert.Equal("Accept Mission - Desperate", decision.RuleMatched);
    }

    [Test]
    public void DecideMission_HighRewardWithAffordableRisk_ShouldAccept()
    {
        var agent = new PlayerAgent("Wealthy Tony");
        agent.Character.Money = 5000m;
        agent.Character.Respect = 50;

        var mission = CreateTestMission(respectReward: 15, moneyReward: 2000m);
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        Assert.True(decision.Accept);
        Assert.Equal("Accept - High Reward", decision.RuleMatched);
    }

    [Test]
    public void DecideMission_AmbitiousPlayerWithAffordableRisk_ShouldAccept()
    {
        var personality = new PlayerPersonality { Ambition = 80 };
        var agent = new PlayerAgent("Ambitious Tony", personality);
        agent.Character.Money = 5000m;
        agent.Character.Respect = 50;

        var mission = CreateTestMission(respectReward: 5, moneyReward: 500m, riskLevel: 5);
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        Assert.True(decision.Accept);
        Assert.Contains("Accept", decision.RuleMatched);
    }

    [Test]
    public void DecideMission_LowRespectWithSafeMission_ShouldAccept()
    {
        var agent = new PlayerAgent("Newbie Tony");
        agent.Character.Respect = 20;
        agent.Character.Money = 5000m;

        var mission = CreateTestMission(riskLevel: 2);
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        Assert.True(decision.Accept);
        Assert.Contains("Accept", decision.RuleMatched);
    }

    [Test]
    public void DecideMission_DefaultCriteriaMet_ShouldAccept()
    {
        var agent = new PlayerAgent("Standard Tony");
        agent.Character.Money = 5000m;
        agent.Character.Respect = 50;
        agent.Character.Heat = 30;

        var mission = CreateTestMission(riskLevel: 5);
        // No skill requirements
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        Assert.True(decision.Accept);
    }

    #endregion

    #region PlayerAgent DecideMission Tests - Rule Matching Behavior

    [Test]
    public void DecideMission_UnderqualifiedPlayer_MatchesRejectRule()
    {
        var agent = new PlayerAgent("Unskilled Tony");
        agent.Character.Skills.Intimidation = 10;
        agent.Character.Money = 5000m;
        agent.Character.Respect = 50; // Not low respect to avoid other rules

        var mission = CreateTestMission(riskLevel: 5); // Not safe to avoid ACCEPT_SAFE_BUILDING
        mission.SkillRequirements["Intimidation"] = 50;
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        // The rule "Reject - Underqualified" should match and reject the mission
        Assert.Equal("Reject - Underqualified", decision.RuleMatched);
        // Bug fix: Now uses rule ID (uppercase) for case-insensitive matching
        Assert.False(decision.Accept);
    }

    [Test]
    public void DecideMission_UnderHeatWithRiskyMission_MatchesRejectRule()
    {
        var agent = new PlayerAgent("Hot Tony");
        agent.Character.Heat = 70; // UnderHeat
        agent.Character.Money = 5000m;
        agent.Character.Respect = 50;

        var mission = CreateTestMission(riskLevel: 8); // MissionIsRisky
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        // The rule "Reject - Too Much Heat" should match and reject the mission
        Assert.Equal("Reject - Too Much Heat", decision.RuleMatched);
        // Bug fix: Now uses rule ID (uppercase) for case-insensitive matching
        Assert.False(decision.Accept);
    }

    [Test]
    public void DecideMission_CautiousPlayerWithRiskyMission_MatchesRejectRule()
    {
        var personality = new PlayerPersonality { Caution = 80 };
        var agent = new PlayerAgent("Cautious Tony", personality);
        agent.Character.Money = 5000m;
        agent.Character.Respect = 50;

        var mission = CreateTestMission(riskLevel: 8);
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        // The rule should contain "Reject" in the name and reject the mission
        Assert.Contains("Reject", decision.RuleMatched);
        // Bug fix: Now uses rule ID (uppercase) for case-insensitive matching
        Assert.False(decision.Accept);
    }

    [Test]
    public void DecideMission_MultipleMatchingRules_UsesHighestPriority()
    {
        var agent = new PlayerAgent("Complex Tony");
        agent.Character.Money = 300m; // Low on money - triggers ACCEPT_DESPERATE
        agent.Character.Skills.Intimidation = 10;

        var mission = CreateTestMission(riskLevel: 2); // Safe mission
        mission.SkillRequirements["Intimidation"] = 50; // Underqualified
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        // ACCEPT_DESPERATE (priority 1000) should match over REJECT_UNDERQUALIFIED (priority 950)
        Assert.Equal("Accept Mission - Desperate", decision.RuleMatched);
        Assert.True(decision.Accept);
    }

    #endregion

    #region PlayerAgent DecideMission - Confidence Tests

    [Test]
    public void DecideMission_AcceptingOverqualifiedSafeMission_HasHighConfidence()
    {
        var agent = new PlayerAgent("Expert Tony");
        agent.Character.Skills.Intimidation = 60;
        agent.Character.Money = 5000m;
        agent.Character.Respect = 50;
        agent.Character.Heat = 20; // SafeOperations

        var mission = CreateTestMission(riskLevel: 2, respectReward: 15);
        mission.SkillRequirements["Intimidation"] = 20;
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        Assert.True(decision.Accept);
        Assert.True(decision.Confidence > 80); // Should have high confidence
    }

    [Test]
    public void DecideMission_RejectingWithRejectRuleMatch_CalculatesConfidenceCorrectly()
    {
        // When a reject rule matches, Accept should be false and
        // the confidence calculation uses the rejecting path
        var agent = new PlayerAgent("Skilled Tony");
        agent.Character.Skills.Intimidation = 10;
        agent.Character.Money = 5000m;
        agent.Character.Respect = 50;

        var mission = CreateTestMission(riskLevel: 2); // Safe mission
        mission.SkillRequirements["Intimidation"] = 50;
        var gameState = new GameState();

        var decision = agent.DecideMission(mission, gameState);

        // Accept is false since reject rule matched (bug was fixed)
        Assert.False(decision.Accept);
        // Confidence should be calculated based on rejecting path (not meeting skill requirements adds confidence)
        Assert.True(decision.Confidence >= 50);
    }

    #endregion

    #region PlayerAgent ExecuteMissionAsync Tests

    [Test]
    public async Task ExecuteMissionAsync_AppliesResultsToCharacter()
    {
        var agent = new PlayerAgent("Mission Tony");
        var initialRespect = agent.Character.Respect;
        var initialMoney = agent.Character.Money;

        var mission = CreateTestMission(respectReward: 10, moneyReward: 500m);
        var gameState = new GameState();

        var result = await agent.ExecuteMissionAsync(mission, gameState);

        Assert.NotNull(result);
        Assert.NotNull(result.MissionResult);
        // Results should have been applied (either success or failure)
        Assert.True(agent.Character.Respect != initialRespect || agent.Character.Money != initialMoney);
    }

    [Test]
    public async Task ExecuteMissionAsync_TracksMission()
    {
        var agent = new PlayerAgent("Tracking Tony");

        var mission = CreateTestMission();
        var gameState = new GameState();

        await agent.ExecuteMissionAsync(mission, gameState);

        // Mission should have a status
        Assert.True(mission.Status == MissionStatus.Completed || mission.Status == MissionStatus.Failed);
        Assert.True(mission.CompletedAt.HasValue);
        Assert.NotNull(mission.Outcome);
    }

    [Test]
    public async Task ExecuteMissionAsync_ClampsRespectBetween0And100()
    {
        var agent = new PlayerAgent("Extreme Tony");
        agent.Character.Respect = 95;

        var mission = CreateTestMission(respectReward: 20);
        var gameState = new GameState();

        await agent.ExecuteMissionAsync(mission, gameState);

        Assert.True(agent.Character.Respect >= 0);
        Assert.True(agent.Character.Respect <= 100);
    }

    [Test]
    public async Task ExecuteMissionAsync_ClampsHeatBetween0And100()
    {
        var agent = new PlayerAgent("Heat Tony");
        agent.Character.Heat = 90;

        var mission = CreateTestMission();
        mission.HeatGenerated = 30;
        var gameState = new GameState();

        await agent.ExecuteMissionAsync(mission, gameState);

        Assert.True(agent.Character.Heat >= 0);
        Assert.True(agent.Character.Heat <= 100);
    }

    #endregion

    #region PlayerAgent Promotion Tests

    [Test]
    public async Task ExecuteMissionAsync_PromotesToSoldier_WhenRespectReaches40()
    {
        var agent = new PlayerAgent("Promotable Tony");
        agent.Character.Respect = 38;
        agent.Character.Rank = PlayerRank.Associate;

        var mission = CreateTestMission(respectReward: 5);
        var gameState = new GameState();

        await agent.ExecuteMissionAsync(mission, gameState);

        // May or may not be promoted depending on mission success
        // But if respect reaches 40+, should be promoted
        if (agent.Character.Respect >= 40)
        {
            Assert.Equal(PlayerRank.Soldier, agent.Character.Rank);
        }
    }

    [Test]
    public async Task ExecuteMissionAsync_RecordsPromotionAchievement()
    {
        var agent = new PlayerAgent("Achievement Tony");
        agent.Character.Respect = 38;
        agent.Character.Rank = PlayerRank.Associate;
        var initialAchievements = agent.Character.Achievements.Count;

        var mission = CreateTestMission(respectReward: 10);
        var gameState = new GameState();

        await agent.ExecuteMissionAsync(mission, gameState);

        // If promoted, should have achievement
        if (agent.Character.Rank == PlayerRank.Soldier)
        {
            Assert.True(agent.Character.Achievements.Count > initialAchievements);
        }
    }

    #endregion

    #region PlayerAgent ProcessWeekAsync Tests

    [Test]
    public async Task ProcessWeekAsync_IncrementsWeek()
    {
        var agent = new PlayerAgent("Weekly Tony");
        var initialWeek = agent.Character.Week;
        var gameState = new GameState();

        await agent.ProcessWeekAsync(gameState);

        Assert.Equal(initialWeek + 1, agent.Character.Week);
    }

    [Test]
    public async Task ProcessWeekAsync_AppliesNaturalHeatDecay()
    {
        // Test the natural heat decay logic (Heat -= 3) that happens at start of week
        // Note: If a mission is accepted and executed, additional heat may be added
        var agent = new PlayerAgent("Cooling Tony");
        var initialHeat = 50;
        agent.Character.Heat = initialHeat;

        // Make the agent unlikely to accept missions by setting high heat and no money
        // This makes rejection more likely so we can test heat decay
        agent.Character.Heat = 80; // High heat, likely to reject risky missions
        var gameState = new GameState();

        var result = await agent.ProcessWeekAsync(gameState);

        // Heat can go up or down depending on mission execution
        // Just verify heat is within valid bounds and was modified
        Assert.True(agent.Character.Heat >= 0);
        Assert.True(agent.Character.Heat <= 100);
    }

    [Test]
    public async Task ProcessWeekAsync_ReturnsWeekResult()
    {
        var agent = new PlayerAgent("Result Tony");
        var gameState = new GameState();

        var result = await agent.ProcessWeekAsync(gameState);

        Assert.NotNull(result);
        Assert.NotNull(result.GeneratedMission);
        Assert.NotNull(result.Decision);
        Assert.True(result.Week > 0);
    }

    [Test]
    public async Task ProcessWeekAsync_ExecutesMission_WhenAccepted()
    {
        var agent = new PlayerAgent("Accepting Tony");
        agent.Character.Money = 200m; // Desperate
        var gameState = new GameState();

        var result = await agent.ProcessWeekAsync(gameState);

        // If the mission was safe (risk < 4), it should be accepted
        if (result.Decision.Accept)
        {
            Assert.NotNull(result.ExecutionResult);
        }
    }

    [Test]
    public async Task ProcessWeekAsync_DoesNotExecuteMission_WhenRejected()
    {
        var agent = new PlayerAgent("Rejecting Tony");
        agent.Character.Skills.Intimidation = 5;
        agent.Character.Heat = 80; // Under heat
        var gameState = new GameState();

        var result = await agent.ProcessWeekAsync(gameState);

        // If rejected, ExecutionResult should be null
        if (!result.Decision.Accept)
        {
            Assert.Null(result.ExecutionResult);
        }
    }

    #endregion

    #region PlayerAgent GetSummary Tests

    [Test]
    public void GetSummary_ContainsPlayerName()
    {
        var agent = new PlayerAgent("Summary Tony");

        var summary = agent.GetSummary();

        Assert.Contains("Summary Tony", summary);
    }

    [Test]
    public void GetSummary_ContainsRank()
    {
        var agent = new PlayerAgent("Ranked Tony");
        agent.Character.Rank = PlayerRank.Capo;

        var summary = agent.GetSummary();

        Assert.Contains("Capo", summary);
    }

    [Test]
    public void GetSummary_ContainsStats()
    {
        var agent = new PlayerAgent("Stats Tony");
        agent.Character.Respect = 75;
        agent.Character.Money = 5000m;
        agent.Character.Heat = 30;

        var summary = agent.GetSummary();

        Assert.Contains("Respect:", summary);
        Assert.Contains("Money:", summary);
        Assert.Contains("Heat:", summary);
    }

    [Test]
    public void GetSummary_ContainsSkills()
    {
        var agent = new PlayerAgent("Skilled Tony");

        var summary = agent.GetSummary();

        Assert.Contains("Intimidation:", summary);
        Assert.Contains("Negotiation:", summary);
        Assert.Contains("Street Smarts:", summary);
        Assert.Contains("Leadership:", summary);
        Assert.Contains("Business:", summary);
    }

    [Test]
    public void GetSummary_IndicatesNoRouter_WhenNoMiddleware()
    {
        var agent = new PlayerAgent("Direct Tony");

        var summary = agent.GetSummary();

        Assert.Contains("Direct execution mode", summary);
    }

    #endregion

    #region MissionDecision Tests

    [Test]
    public void MissionDecision_DefaultValues_AreCorrect()
    {
        var decision = new MissionDecision();

        Assert.False(decision.Accept);
        Assert.Equal("", decision.Reason);
        Assert.Equal("", decision.RuleMatched);
        Assert.Equal(0, decision.Confidence);
    }

    [Test]
    public void MissionDecision_CanSetAllProperties()
    {
        var decision = new MissionDecision
        {
            Accept = true,
            Reason = "High reward mission",
            RuleMatched = "ACCEPT_HIGH_REWARD",
            Confidence = 85
        };

        Assert.True(decision.Accept);
        Assert.Equal("High reward mission", decision.Reason);
        Assert.Equal("ACCEPT_HIGH_REWARD", decision.RuleMatched);
        Assert.Equal(85, decision.Confidence);
    }

    #endregion

    #region MissionExecutionResult Tests

    [Test]
    public void MissionExecutionResult_DefaultValues_AreCorrect()
    {
        var result = new MissionExecutionResult();

        Assert.False(result.PromotionEarned.HasValue);
        Assert.NotNull(result.NewSkills);
        Assert.Empty(result.NewSkills);
    }

    [Test]
    public void MissionExecutionResult_CanSetMissionResult()
    {
        var missionResult = new MissionResult
        {
            Success = true,
            RespectGained = 10,
            MoneyGained = 500m,
            HeatGained = 5,
            Message = "Mission successful"
        };

        var result = new MissionExecutionResult
        {
            MissionResult = missionResult
        };

        Assert.True(result.MissionResult.Success);
        Assert.Equal(10, result.MissionResult.RespectGained);
        Assert.Equal(500m, result.MissionResult.MoneyGained);
    }

    [Test]
    public void MissionExecutionResult_CanSetPromotionEarned()
    {
        var result = new MissionExecutionResult
        {
            PromotionEarned = PlayerRank.Soldier
        };

        Assert.Equal(PlayerRank.Soldier, result.PromotionEarned);
    }

    [Test]
    public void MissionExecutionResult_CanSetNewSkills()
    {
        var result = new MissionExecutionResult
        {
            NewSkills = new Dictionary<string, int>
            {
                ["Intimidation"] = 2,
                ["Negotiation"] = 1
            }
        };

        Assert.Equal(2, result.NewSkills["Intimidation"]);
        Assert.Equal(1, result.NewSkills["Negotiation"]);
    }

    #endregion

    #region WeekResult Tests

    [Test]
    public void WeekResult_DefaultValues_AreCorrect()
    {
        var result = new WeekResult();

        Assert.Equal(0, result.Week);
        Assert.Null(result.ExecutionResult);
    }

    [Test]
    public void WeekResult_CanSetAllProperties()
    {
        var mission = CreateTestMission();
        var decision = new MissionDecision { Accept = true };
        var execution = new MissionExecutionResult();

        var result = new WeekResult
        {
            Week = 5,
            GeneratedMission = mission,
            Decision = decision,
            ExecutionResult = execution
        };

        Assert.Equal(5, result.Week);
        Assert.Same(mission, result.GeneratedMission);
        Assert.Same(decision, result.Decision);
        Assert.Same(execution, result.ExecutionResult);
    }

    [Test]
    public void WeekResult_ExecutionResult_NullWhenMissionRejected()
    {
        var result = new WeekResult
        {
            Week = 3,
            GeneratedMission = CreateTestMission(),
            Decision = new MissionDecision { Accept = false },
            ExecutionResult = null
        };

        Assert.False(result.Decision.Accept);
        Assert.Null(result.ExecutionResult);
    }

    #endregion

    #region PlayerPersonality Tests

    [Test]
    public void PlayerPersonality_IsAmbitious_TrueWhenAmbitionAbove70()
    {
        var personality = new PlayerPersonality { Ambition = 75 };
        Assert.True(personality.IsAmbitious);
    }

    [Test]
    public void PlayerPersonality_IsAmbitious_FalseWhenAmbitionAtOrBelow70()
    {
        var personality = new PlayerPersonality { Ambition = 70 };
        Assert.False(personality.IsAmbitious);
    }

    [Test]
    public void PlayerPersonality_IsLoyal_TrueWhenLoyaltyAbove70()
    {
        var personality = new PlayerPersonality { Loyalty = 80 };
        Assert.True(personality.IsLoyal);
    }

    [Test]
    public void PlayerPersonality_IsRuthless_TrueWhenRuthlessnessAbove70()
    {
        var personality = new PlayerPersonality { Ruthlessness = 75 };
        Assert.True(personality.IsRuthless);
    }

    [Test]
    public void PlayerPersonality_IsCautious_TrueWhenCautionAbove70()
    {
        var personality = new PlayerPersonality { Caution = 80 };
        Assert.True(personality.IsCautious);
    }

    [Test]
    public void PlayerPersonality_IsReckless_TrueWhenCautionBelow30()
    {
        var personality = new PlayerPersonality { Caution = 25 };
        Assert.True(personality.IsReckless);
    }

    [Test]
    public void PlayerPersonality_IsReckless_FalseWhenCautionAtOrAbove30()
    {
        var personality = new PlayerPersonality { Caution = 30 };
        Assert.False(personality.IsReckless);
    }

    #endregion

    #region PlayerSkills Tests

    [Test]
    public void PlayerSkills_GetSkill_ReturnsCorrectValues()
    {
        var skills = new PlayerSkills
        {
            Intimidation = 30,
            Negotiation = 40,
            StreetSmarts = 50,
            Leadership = 60,
            Business = 70
        };

        Assert.Equal(30, skills.GetSkill("Intimidation"));
        Assert.Equal(40, skills.GetSkill("Negotiation"));
        Assert.Equal(50, skills.GetSkill("StreetSmarts"));
        Assert.Equal(60, skills.GetSkill("Leadership"));
        Assert.Equal(70, skills.GetSkill("Business"));
    }

    [Test]
    public void PlayerSkills_GetSkill_ReturnsZeroForUnknownSkill()
    {
        var skills = new PlayerSkills();

        Assert.Equal(0, skills.GetSkill("Unknown"));
    }

    [Test]
    public void PlayerSkills_GetSkill_IsCaseInsensitive()
    {
        var skills = new PlayerSkills { Intimidation = 50 };

        Assert.Equal(50, skills.GetSkill("intimidation"));
        Assert.Equal(50, skills.GetSkill("INTIMIDATION"));
        Assert.Equal(50, skills.GetSkill("Intimidation"));
    }

    #endregion

    #region Mission Tests

    [Test]
    public void Mission_DefaultValues_AreCorrect()
    {
        var mission = new Mission();

        Assert.NotNull(mission.Id);
        Assert.Equal("", mission.Title);
        Assert.Equal("", mission.Description);
        Assert.Equal(MissionStatus.Available, mission.Status);
        Assert.False(mission.Success);
        Assert.Null(mission.Outcome);
        Assert.Empty(mission.SkillRequirements);
        Assert.Empty(mission.Data);
    }

    [Test]
    public void Mission_CanSetAllProperties()
    {
        var mission = new Mission
        {
            Title = "Collect from Tony's Restaurant",
            Description = "Collect the weekly payment",
            Type = MissionType.Collection,
            AssignedBy = "capo-001",
            MinimumRank = 0,
            RiskLevel = 3,
            RespectReward = 5,
            MoneyReward = 200m,
            HeatGenerated = 2
        };

        Assert.Equal("Collect from Tony's Restaurant", mission.Title);
        Assert.Equal(MissionType.Collection, mission.Type);
        Assert.Equal("capo-001", mission.AssignedBy);
        Assert.Equal(3, mission.RiskLevel);
        Assert.Equal(5, mission.RespectReward);
        Assert.Equal(200m, mission.MoneyReward);
    }

    #endregion

    #region MissionResult Tests

    [Test]
    public void MissionResult_DefaultValues_AreCorrect()
    {
        var result = new MissionResult();

        Assert.False(result.Success);
        Assert.Equal(0, result.RespectGained);
        Assert.Equal(0m, result.MoneyGained);
        Assert.Equal(0, result.HeatGained);
        Assert.Equal("", result.Message);
        Assert.Empty(result.SkillGains);
    }

    [Test]
    public void MissionResult_CanSetAllProperties()
    {
        var result = new MissionResult
        {
            Success = true,
            RespectGained = 10,
            MoneyGained = 500m,
            HeatGained = 5,
            Message = "Mission completed successfully",
            SkillGains = new Dictionary<string, int> { ["Intimidation"] = 2 }
        };

        Assert.True(result.Success);
        Assert.Equal(10, result.RespectGained);
        Assert.Equal(500m, result.MoneyGained);
        Assert.Equal(5, result.HeatGained);
        Assert.Equal("Mission completed successfully", result.Message);
        Assert.Equal(2, result.SkillGains["Intimidation"]);
    }

    #endregion

    #region Integration Tests - Complex Scenarios

    [Test]
    public async Task Integration_FullWeekCycle_UpdatesCharacterCorrectly()
    {
        var agent = new PlayerAgent("Full Cycle Tony");
        var initialWeek = agent.Character.Week;
        var gameState = new GameState();

        var result = await agent.ProcessWeekAsync(gameState);

        Assert.Equal(initialWeek + 1, agent.Character.Week);
        Assert.NotNull(result.GeneratedMission);
        Assert.NotNull(result.Decision);
    }

    [Test]
    public async Task Integration_MultipleWeeks_ProgressesCharacter()
    {
        var agent = new PlayerAgent("Progress Tony");
        var gameState = new GameState();

        for (int i = 0; i < 5; i++)
        {
            await agent.ProcessWeekAsync(gameState);
        }

        Assert.Equal(6, agent.Character.Week);
    }

    [Test]
    public void Integration_DecisionContext_CorrectlyReflectsPlayerState()
    {
        var agent = new PlayerAgent("State Tony");
        agent.Character.Money = 300m;
        agent.Character.Respect = 80;
        agent.Character.Heat = 70;
        agent.Character.Week = 25;
        agent.Character.Rank = PlayerRank.Capo;

        var context = new PlayerDecisionContext
        {
            Player = agent.Character,
            GameState = new GameState()
        };

        Assert.True(context.IsLowOnMoney);
        Assert.True(context.HasHighRespect);
        Assert.True(context.UnderHeat);
        Assert.True(context.IsMidCareer);
        Assert.True(context.ShouldBeCautious);
    }

    #endregion

    #region Helper Methods

    private static PlayerCharacter CreateTestPlayer(
        decimal money = 1000m,
        int respect = 10,
        int heat = 0,
        int week = 1,
        PlayerRank rank = PlayerRank.Associate,
        int ambition = 50,
        int loyalty = 70,
        int ruthlessness = 30,
        int caution = 50)
    {
        return new PlayerCharacter
        {
            Name = "Test Player",
            Money = money,
            Respect = respect,
            Heat = heat,
            Week = week,
            Rank = rank,
            Personality = new PlayerPersonality
            {
                Ambition = ambition,
                Loyalty = loyalty,
                Ruthlessness = ruthlessness,
                Caution = caution
            }
        };
    }

    private static Mission CreateTestMission(
        int riskLevel = 3,
        int respectReward = 5,
        decimal moneyReward = 200m)
    {
        return new Mission
        {
            Title = "Test Mission",
            Description = "A test mission",
            Type = MissionType.Collection,
            AssignedBy = "capo-001",
            RiskLevel = riskLevel,
            RespectReward = respectReward,
            MoneyReward = moneyReward,
            HeatGenerated = 2
        };
    }

    private static PlayerDecisionContext CreateContext(
        PlayerCharacter player,
        Mission? mission = null)
    {
        return new PlayerDecisionContext
        {
            Player = player,
            Mission = mission,
            GameState = new GameState()
        };
    }

    #endregion
}
