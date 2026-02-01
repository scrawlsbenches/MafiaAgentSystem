using TestRunner.Framework;
using AgentRouting.MafiaDemo.Game;
using AgentRouting.MafiaDemo.Rules;

namespace TestRunner.Tests;

/// <summary>
/// Integration tests for MafiaDemo game engine and rules
/// </summary>
public class MafiaDemoIntegrationTests
{
    #region GameState Tests

    [Test]
    public void GameState_InitialValues_AreCorrect()
    {
        var state = new GameState();

        Assert.Equal(100000m, state.FamilyWealth);
        Assert.Equal(50, state.Reputation);
        Assert.Equal(0, state.HeatLevel);
        Assert.Equal(1, state.Week);
        Assert.False(state.GameOver);
        Assert.Null(state.GameOverReason);
    }

    [Test]
    public void GameState_ComputedProperties_WorkCorrectly()
    {
        var state = new GameState
        {
            Week = 10,
            SoldierCount = 8
        };
        state.Territories["Downtown"] = new Territory { WeeklyRevenue = 5000 };
        state.Territories["Uptown"] = new Territory { WeeklyRevenue = 3000 };

        Assert.Equal(10, state.Day);
        Assert.Equal(8000m, state.TotalRevenue);
        Assert.Equal(2, state.TerritoryCount);
    }

    #endregion

    #region GameRuleContext Tests

    [Test]
    public void GameRuleContext_FinancialHelpers_WorkCorrectly()
    {
        var state = new GameState();

        // Weak financially
        var weakContext = new GameRuleContext { State = state, Wealth = 40000m };
        Assert.True(weakContext.IsWeakFinancially);
        Assert.False(weakContext.IsStrongFinancially);
        Assert.False(weakContext.IsRichFinancially);

        // Strong financially
        var strongContext = new GameRuleContext { State = state, Wealth = 250000m };
        Assert.False(strongContext.IsWeakFinancially);
        Assert.True(strongContext.IsStrongFinancially);
        Assert.False(strongContext.IsRichFinancially);

        // Rich financially
        var richContext = new GameRuleContext { State = state, Wealth = 600000m };
        Assert.False(richContext.IsWeakFinancially);
        Assert.True(richContext.IsStrongFinancially);
        Assert.True(richContext.IsRichFinancially);
    }

    [Test]
    public void GameRuleContext_ReputationHelpers_WorkCorrectly()
    {
        var state = new GameState();

        var lowRepContext = new GameRuleContext { State = state, Reputation = 20 };
        Assert.True(lowRepContext.HasLowReputation);
        Assert.False(lowRepContext.HasHighReputation);

        var highRepContext = new GameRuleContext { State = state, Reputation = 80 };
        Assert.False(highRepContext.HasLowReputation);
        Assert.True(highRepContext.HasHighReputation);
    }

    [Test]
    public void GameRuleContext_HeatHelpers_WorkCorrectly()
    {
        var state = new GameState();

        var normalHeat = new GameRuleContext { State = state, Heat = 40 };
        Assert.False(normalHeat.IsUnderHeat);
        Assert.False(normalHeat.IsSevereHeat);

        var underHeat = new GameRuleContext { State = state, Heat = 60 };
        Assert.True(underHeat.IsUnderHeat);
        Assert.False(underHeat.IsSevereHeat);

        var severeHeat = new GameRuleContext { State = state, Heat = 90 };
        Assert.True(severeHeat.IsUnderHeat);
        Assert.True(severeHeat.IsSevereHeat);
    }

    [Test]
    public void GameRuleContext_GamePhaseHelpers_WorkCorrectly()
    {
        var state = new GameState();

        var early = new GameRuleContext { State = state, Week = 5 };
        Assert.True(early.IsEarlyGame);
        Assert.False(early.IsMidGame);
        Assert.False(early.IsLateGame);

        var mid = new GameRuleContext { State = state, Week = 20 };
        Assert.False(mid.IsEarlyGame);
        Assert.True(mid.IsMidGame);
        Assert.False(mid.IsLateGame);

        var late = new GameRuleContext { State = state, Week = 40 };
        Assert.False(late.IsEarlyGame);
        Assert.False(late.IsMidGame);
        Assert.True(late.IsLateGame);
    }

    [Test]
    public void GameRuleContext_CombinationChecks_WorkCorrectly()
    {
        var state = new GameState();

        // Vulnerable: weak financially AND (low rep OR under heat)
        var vulnerable = new GameRuleContext
        {
            State = state,
            Wealth = 40000m,
            Reputation = 25,
            Heat = 30
        };
        Assert.True(vulnerable.IsVulnerable);

        // Dominant: strong financially AND high rep AND many territories
        var dominant = new GameRuleContext
        {
            State = state,
            Wealth = 300000m,
            Reputation = 80,
            TerritoryCount = 6
        };
        Assert.True(dominant.IsDominant);

        // Can expand: strong financially AND heat < 60
        var canExpand = new GameRuleContext
        {
            State = state,
            Wealth = 250000m,
            Heat = 50
        };
        Assert.True(canExpand.CanExpand);

        // Needs to lay low: severe heat
        var layLow = new GameRuleContext
        {
            State = state,
            Heat = 85
        };
        Assert.True(layLow.NeedsToLayLow);
    }

    #endregion

    #region AgentDecisionContext Tests

    [Test]
    public void AgentDecisionContext_PersonalityHelpers_WorkCorrectly()
    {
        var state = new GameState();

        var aggressive = new AgentDecisionContext
        {
            GameState = state,
            Aggression = 80
        };
        Assert.True(aggressive.IsAggressive);

        var greedy = new AgentDecisionContext
        {
            GameState = state,
            Greed = 80
        };
        Assert.True(greedy.IsGreedy);

        var ambitious = new AgentDecisionContext
        {
            GameState = state,
            Ambition = 80
        };
        Assert.True(ambitious.IsAmbitious);

        var loyal = new AgentDecisionContext
        {
            GameState = state,
            Loyalty = 90
        };
        Assert.True(loyal.IsLoyal);
    }

    [Test]
    public void AgentDecisionContext_ComplexPersonality_WorksCorrectly()
    {
        var state = new GameState();

        // Hot-headed: high aggression AND low loyalty
        var hotHeaded = new AgentDecisionContext
        {
            GameState = state,
            Aggression = 90,
            Loyalty = 50
        };
        Assert.True(hotHeaded.IsHotHeaded);

        // Calculating: low aggression AND high ambition
        var calculating = new AgentDecisionContext
        {
            GameState = state,
            Aggression = 30,
            Ambition = 70
        };
        Assert.True(calculating.IsCalculating);

        // Family first: high loyalty AND low greed
        var familyFirst = new AgentDecisionContext
        {
            GameState = state,
            Loyalty = 95,
            Greed = 40
        };
        Assert.True(familyFirst.IsFamilyFirst);
    }

    [Test]
    public void AgentDecisionContext_ContextualChecks_WorkCorrectly()
    {
        // Family needs money
        var needsMoneyState = new GameState { FamilyWealth = 50000m };
        var needsMoney = new AgentDecisionContext { GameState = needsMoneyState };
        Assert.True(needsMoney.FamilyNeedsMoney);

        // Family under threat - high heat
        var heatState = new GameState { HeatLevel = 70 };
        var underHeatThreat = new AgentDecisionContext { GameState = heatState };
        Assert.True(underHeatThreat.FamilyUnderThreat);

        // Family under threat - hostile rivals
        var rivalState = new GameState();
        rivalState.RivalFamilies["Barzini"] = new RivalFamily { Hostility = 85 };
        var underRivalThreat = new AgentDecisionContext { GameState = rivalState };
        Assert.True(underRivalThreat.FamilyUnderThreat);

        // Can take risks
        var safeState = new GameState { FamilyWealth = 200000m, HeatLevel = 30 };
        var canRisk = new AgentDecisionContext { GameState = safeState };
        Assert.True(canRisk.CanTakeRisks);
    }

    #endregion

    #region EventContext Tests

    [Test]
    public void EventContext_LikelihoodHelpers_WorkCorrectly()
    {
        var policeAttention = new EventContext { Heat = 70 };
        Assert.True(policeAttention.PoliceAttentionHigh);

        var wealthy = new EventContext { Wealth = 400000m };
        Assert.True(wealthy.WealthyTarget);

        var weak = new EventContext { Reputation = 30 };
        Assert.True(weak.WeakPosition);

        var tense = new EventContext { RivalHostilityMax = 80 };
        Assert.True(tense.TenseSituation);
    }

    #endregion

    #region RulesBasedGameEngine Tests

    [Test]
    public void RulesBasedGameEngine_Construction_SetsUpRulesCorrectly()
    {
        var state = new GameState();
        var engine = new RulesBasedGameEngine(state);

        Assert.NotNull(engine);
        Assert.Same(state, engine.State);
    }

    [Test]
    public void RulesBasedGameEngine_EvaluateGameRules_ReturnsMatchingRules()
    {
        var state = new GameState
        {
            Week = 52,
            FamilyWealth = 600000m,
            Reputation = 80,
            HeatLevel = 20
        };
        state.Territories["Downtown"] = new Territory { Name = "Downtown" };

        var engine = new RulesBasedGameEngine(state);
        var events = engine.EvaluateGameRules();

        // Should match victory rules
        Assert.NotEmpty(events);
    }

    [Test]
    public void RulesBasedGameEngine_GetAgentAction_ReturnsAction()
    {
        var state = new GameState
        {
            FamilyWealth = 50000m,
            HeatLevel = 30
        };

        var engine = new RulesBasedGameEngine(state);

        var greedyAgent = new GameAgentData
        {
            AgentId = "test-agent",
            Personality = new AgentPersonality
            {
                Greed = 80,
                Aggression = 50,
                Loyalty = 50,
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(greedyAgent);

        // Greedy agent with family needing money should collect
        Assert.NotNull(action);
        // Either "collection" or "wait" depending on rule matching
        Assert.True(action == "collection" || action == "wait");
    }

    [Test]
    public void RulesBasedGameEngine_GenerateEvents_ReturnsEvents()
    {
        var state = new GameState
        {
            Week = 15,
            HeatLevel = 70,
            Reputation = 30
        };
        state.RivalFamilies["Barzini"] = new RivalFamily
        {
            Name = "Barzini",
            Hostility = 75
        };

        var engine = new RulesBasedGameEngine(state);
        var events = engine.GenerateEvents();

        // High heat and weak position should generate events
        Assert.NotNull(events);
    }

    #endregion

    #region GameAgentData Tests

    [Test]
    public void AgentPersonality_DefaultValues_AreBalanced()
    {
        var personality = new AgentPersonality();

        Assert.Equal(50, personality.Aggression);
        Assert.Equal(50, personality.Greed);
        Assert.Equal(80, personality.Loyalty);
        Assert.Equal(50, personality.Ambition);
    }

    [Test]
    public void GameAgentData_CanBeCreatedWithPersonality()
    {
        var agent = new GameAgentData
        {
            AgentId = "capo-001",
            Personality = new AgentPersonality
            {
                Aggression = 70,
                Greed = 60,
                Loyalty = 90,
                Ambition = 80
            }
        };

        Assert.Equal("capo-001", agent.AgentId);
        Assert.Equal(70, agent.Personality.Aggression);
        Assert.Equal(60, agent.Personality.Greed);
        Assert.Equal(90, agent.Personality.Loyalty);
        Assert.Equal(80, agent.Personality.Ambition);
    }

    #endregion

    #region Territory and RivalFamily Tests

    [Test]
    public void Territory_CanBeCreated()
    {
        var territory = new Territory
        {
            Name = "Little Italy",
            ControlledBy = "capo-001",
            WeeklyRevenue = 5000m,
            HeatGeneration = 2,
            Type = "Protection",
            UnderDispute = false
        };

        Assert.Equal("Little Italy", territory.Name);
        Assert.Equal("capo-001", territory.ControlledBy);
        Assert.Equal(5000m, territory.WeeklyRevenue);
        Assert.Equal(2, territory.HeatGeneration);
        Assert.Equal("Protection", territory.Type);
        Assert.False(territory.UnderDispute);
    }

    [Test]
    public void RivalFamily_CanBeCreated()
    {
        var rival = new RivalFamily
        {
            Name = "Tattaglia",
            Strength = 60,
            Hostility = 40,
            AtWar = false
        };

        Assert.Equal("Tattaglia", rival.Name);
        Assert.Equal(60, rival.Strength);
        Assert.Equal(40, rival.Hostility);
        Assert.False(rival.AtWar);
    }

    #endregion

    #region DecisionType and AgentDecision Tests

    [Test]
    public void AgentDecision_CanBeCreatedWithMessage()
    {
        var decision = new AgentDecision
        {
            Type = DecisionType.SendMessage,
            Reason = "Weekly report"
        };

        Assert.Equal(DecisionType.SendMessage, decision.Type);
        Assert.Equal("Weekly report", decision.Reason);
    }

    [Test]
    public void DecisionType_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(DecisionType), DecisionType.Wait));
        Assert.True(Enum.IsDefined(typeof(DecisionType), DecisionType.SendMessage));
        Assert.True(Enum.IsDefined(typeof(DecisionType), DecisionType.CollectMoney));
        Assert.True(Enum.IsDefined(typeof(DecisionType), DecisionType.RecruitSoldier));
        Assert.True(Enum.IsDefined(typeof(DecisionType), DecisionType.ExpandTerritory));
        Assert.True(Enum.IsDefined(typeof(DecisionType), DecisionType.OrderHit));
        Assert.True(Enum.IsDefined(typeof(DecisionType), DecisionType.MakePeace));
        Assert.True(Enum.IsDefined(typeof(DecisionType), DecisionType.Negotiate));
    }

    #endregion

    #region GameEvent Tests

    [Test]
    public void GameEvent_CanBeCreatedWithData()
    {
        var gameEvent = new GameEvent
        {
            Type = "Collection",
            Description = "Weekly collection completed",
            InvolvedAgent = "capo-001"
        };
        gameEvent.Data["Amount"] = 5000m;

        Assert.Equal("Collection", gameEvent.Type);
        Assert.Equal("Weekly collection completed", gameEvent.Description);
        Assert.Equal("capo-001", gameEvent.InvolvedAgent);
        Assert.Equal(5000m, gameEvent.Data["Amount"]);
    }

    #endregion
}
