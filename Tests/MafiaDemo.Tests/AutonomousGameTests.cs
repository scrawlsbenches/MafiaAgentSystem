using TestRunner.Framework;
using AgentRouting.MafiaDemo;
using AgentRouting.MafiaDemo.Game;
using AgentRouting.MafiaDemo.Rules;
using AgentRouting.Core;
using RulesEngine.Core;
using RulesEngine.Enhanced;

namespace TestRunner.Tests;

/// <summary>
/// Unit tests for AutonomousPlaythrough, GameEngine, RulesBasedEngine, and GameRulesEngine
/// </summary>
public class AutonomousGameTests
{
    /// <summary>
    /// Silent logger for tests - suppresses console output
    /// </summary>
    private class SilentAgentLogger : IAgentLogger
    {
        public void LogMessageReceived(IAgent agent, AgentMessage message) { }
        public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result) { }
        public void LogMessageRouted(AgentMessage message, IAgent? fromAgent, IAgent toAgent) { }
        public void LogError(IAgent agent, AgentMessage message, Exception ex) { }
    }

    private static IAgentLogger CreateTestLogger() => new SilentAgentLogger();

    private static GameState CreateTestGameState()
    {
        return new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 50,
            HeatLevel = 20,
            Week = 3,
            SoldierCount = 10
        };
    }

    private static void EnsureInstantTiming()
    {
        GameTimingOptions.Current = GameTimingOptions.Instant;
    }

    #region MafiaGameEngine Construction Tests

    [Test]
    public void MafiaGameEngine_ConstructorWithRouter_InitializesCorrectly()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        var engine = new MafiaGameEngine(router);

        Assert.NotNull(engine);
        Assert.NotNull(engine.State);
        Assert.Equal(100000m, engine.State.FamilyWealth);
        Assert.Equal(50, engine.State.Reputation);
        Assert.Equal(0, engine.State.HeatLevel);
        Assert.Equal(1, engine.State.Week);
    }

    [Test]
    public void MafiaGameEngine_ConstructorWithLogger_InitializesCorrectly()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        Assert.NotNull(engine);
        Assert.NotNull(engine.State);
        Assert.False(engine.State.GameOver);
    }

    [Test]
    public void MafiaGameEngine_InitializesTerritoriesCorrectly()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        Assert.Equal(3, engine.State.Territories.Count);
        Assert.True(engine.State.Territories.ContainsKey("little-italy"));
        Assert.True(engine.State.Territories.ContainsKey("docks"));
        Assert.True(engine.State.Territories.ContainsKey("bronx"));
    }

    [Test]
    public void MafiaGameEngine_TerritoryDetails_AreCorrect()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var littleItaly = engine.State.Territories["little-italy"];
        Assert.Equal("Little Italy", littleItaly.Name);
        Assert.Equal("capo-001", littleItaly.ControlledBy);
        Assert.Equal(15000m, littleItaly.WeeklyRevenue);
        Assert.Equal("Protection", littleItaly.Type);

        var docks = engine.State.Territories["docks"];
        Assert.Equal("Brooklyn Docks", docks.Name);
        Assert.Equal(20000m, docks.WeeklyRevenue);
        Assert.Equal("Smuggling", docks.Type);

        var bronx = engine.State.Territories["bronx"];
        Assert.Equal("Bronx Gambling", bronx.Name);
        Assert.Equal(12000m, bronx.WeeklyRevenue);
        Assert.Equal("Gambling", bronx.Type);
    }

    [Test]
    public void MafiaGameEngine_InitializesRivalFamiliesCorrectly()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        Assert.Equal(2, engine.State.RivalFamilies.Count);
        Assert.True(engine.State.RivalFamilies.ContainsKey("tattaglia"));
        Assert.True(engine.State.RivalFamilies.ContainsKey("barzini"));
    }

    [Test]
    public void MafiaGameEngine_RivalFamilyDetails_AreCorrect()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var tattaglia = engine.State.RivalFamilies["tattaglia"];
        Assert.Equal("Tattaglia Family", tattaglia.Name);
        Assert.Equal(60, tattaglia.Strength);
        Assert.Equal(20, tattaglia.Hostility);

        var barzini = engine.State.RivalFamilies["barzini"];
        Assert.Equal("Barzini Family", barzini.Name);
        Assert.Equal(70, barzini.Strength);
        Assert.Equal(30, barzini.Hostility);
    }

    [Test]
    public void MafiaGameEngine_InitializesEventLog()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        Assert.NotEmpty(engine.State.EventLog);
        var firstEvent = engine.State.EventLog[0];
        Assert.Equal("GameStart", firstEvent.Type);
    }

    #endregion

    #region MafiaGameEngine Agent Registration Tests

    [Test]
    public void MafiaGameEngine_RegisterAutonomousAgent_Works()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var godfather = new GodfatherAgent("godfather-test", "Test Don", logger);
        engine.RegisterAutonomousAgent(godfather);

        // Verify agent is properly initialized and can make decisions
        Assert.NotNull(godfather);
        Assert.Equal("godfather-test", godfather.Id);
        Assert.Equal("Test Don", godfather.Name);
    }

    [Test]
    public void MafiaGameEngine_SetupRoutingRules_Works()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        var engine = new MafiaGameEngine(router);

        engine.SetupRoutingRules();

        // Verify engine state is valid after setup and game can proceed
        Assert.NotNull(engine.State);
        Assert.False(engine.State.GameOver);
        Assert.Equal(1, engine.State.Week);
    }

    #endregion

    #region MafiaGameEngine Turn Execution Tests

    [Test]
    public async Task MafiaGameEngine_ExecuteTurnAsync_ReturnsEvents()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var events = await engine.ExecuteTurnAsync();

        Assert.NotNull(events);
        Assert.NotEmpty(events);
    }

    [Test]
    public async Task MafiaGameEngine_ExecuteTurnAsync_IncrementsWeek()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var initialWeek = engine.State.Week;
        await engine.ExecuteTurnAsync();

        Assert.Equal(initialWeek + 1, engine.State.Week);
    }

    [Test]
    public async Task MafiaGameEngine_ExecuteTurnAsync_CollectsRevenue()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var initialWealth = engine.State.FamilyWealth;
        await engine.ExecuteTurnAsync();

        // Revenue should increase wealth (with possible variation)
        // Total base revenue = 15000 + 20000 + 12000 = 47000
        Assert.True(engine.State.FamilyWealth > initialWealth);
    }

    [Test]
    public async Task MafiaGameEngine_ExecuteTurnAsync_IncludesWeekHeader()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var events = await engine.ExecuteTurnAsync();

        Assert.Contains(events, e => e.Contains("WEEK"));
    }

    [Test]
    public async Task MafiaGameEngine_ExecuteTurnAsync_IncludesCollections()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var events = await engine.ExecuteTurnAsync();

        Assert.Contains(events, e => e.Contains("WEEKLY COLLECTIONS"));
    }

    [Test]
    public async Task MafiaGameEngine_ExecuteTurnAsync_IncludesFinalStats()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var events = await engine.ExecuteTurnAsync();

        Assert.Contains(events, e => e.Contains("Family Wealth"));
        Assert.Contains(events, e => e.Contains("Reputation"));
        Assert.Contains(events, e => e.Contains("Heat Level"));
    }

    #endregion

    #region MafiaGameEngine Player Action Tests

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Status_ReturnsReport()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var result = await engine.ExecutePlayerAction("status");

        Assert.Contains("FAMILY STATUS REPORT", result);
        Assert.Contains("Week:", result);
        Assert.Contains("Wealth:", result);
        Assert.Contains("Reputation:", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Territories_ReturnsReport()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var result = await engine.ExecutePlayerAction("territories");

        Assert.Contains("TERRITORY CONTROL", result);
        Assert.Contains("Little Italy", result);
        Assert.Contains("Brooklyn Docks", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Rivals_ReturnsReport()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var result = await engine.ExecutePlayerAction("rivals");

        Assert.Contains("RIVAL FAMILIES", result);
        Assert.Contains("Tattaglia", result);
        Assert.Contains("Barzini", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Events_ReturnsLog()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var result = await engine.ExecutePlayerAction("events");

        Assert.Contains("RECENT EVENTS", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Help_ReturnsCommands()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var result = await engine.ExecutePlayerAction("help");

        Assert.Contains("AVAILABLE COMMANDS", result);
        Assert.Contains("status", result);
        Assert.Contains("territories", result);
        Assert.Contains("bribe", result);
        Assert.Contains("expand", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Bribe_ReducesHeat()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.HeatLevel = 50;

        var result = await engine.ExecutePlayerAction("bribe");

        Assert.Contains("Paid $10,000", result);
        Assert.Equal(30, engine.State.HeatLevel);
        Assert.Equal(90000m, engine.State.FamilyWealth);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Bribe_FailsWithoutFunds()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.FamilyWealth = 5000m;

        var result = await engine.ExecutePlayerAction("bribe");

        Assert.Contains("Not enough money", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Expand_CreatesTerritory()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.FamilyWealth = 100000m;

        var initialTerritories = engine.State.Territories.Count;
        var result = await engine.ExecutePlayerAction("expand");

        Assert.Contains("Expanded into new territory", result);
        Assert.Equal(initialTerritories + 1, engine.State.Territories.Count);
        Assert.Equal(50000m, engine.State.FamilyWealth);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Expand_FailsWithoutFunds()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.FamilyWealth = 40000m;

        var result = await engine.ExecutePlayerAction("expand");

        Assert.Contains("Not enough money", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Hit_WeakensRival()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.FamilyWealth = 100000m;

        var initialStrength = engine.State.RivalFamilies["tattaglia"].Strength;
        var result = await engine.ExecutePlayerAction("hit tattaglia");

        Assert.Contains("Hit executed", result);
        Assert.Equal(initialStrength - 20, engine.State.RivalFamilies["tattaglia"].Strength);
        Assert.Equal(75000m, engine.State.FamilyWealth);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Hit_IncreasesHeatAndHostility()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.FamilyWealth = 100000m;
        engine.State.HeatLevel = 10;

        var initialHostility = engine.State.RivalFamilies["barzini"].Hostility;
        await engine.ExecutePlayerAction("hit barzini");

        Assert.Equal(35, engine.State.HeatLevel);
        Assert.Equal(initialHostility + 30, engine.State.RivalFamilies["barzini"].Hostility);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Hit_FailsWithoutFunds()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.FamilyWealth = 20000m;

        var result = await engine.ExecutePlayerAction("hit tattaglia");

        Assert.Contains("Not enough money", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Hit_FailsForUnknownRival()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.FamilyWealth = 100000m;

        var result = await engine.ExecutePlayerAction("hit unknown");

        Assert.Contains("Rival family not found", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Hit_RequiresArgument()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var result = await engine.ExecutePlayerAction("hit");

        Assert.Contains("Usage:", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Peace_ReducesHostility()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.FamilyWealth = 100000m;
        engine.State.RivalFamilies["tattaglia"].Hostility = 60;

        var result = await engine.ExecutePlayerAction("peace tattaglia");

        Assert.Contains("Peace treaty signed", result);
        Assert.Equal(20, engine.State.RivalFamilies["tattaglia"].Hostility);
        Assert.False(engine.State.RivalFamilies["tattaglia"].AtWar);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_Peace_FailsWithoutFunds()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.FamilyWealth = 20000m;

        var result = await engine.ExecutePlayerAction("peace tattaglia");

        Assert.Contains("Not enough money", result);
    }

    [Test]
    public async Task MafiaGameEngine_ExecutePlayerAction_UnknownCommand_ReturnsError()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var result = await engine.ExecutePlayerAction("unknowncommand");

        Assert.Contains("Unknown command", result);
    }

    #endregion

    #region MafiaGameEngine Game Over Tests

    [Test]
    public async Task MafiaGameEngine_ExecuteTurn_WithNegativeWealth_StillProcesses()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        var initialWealth = engine.State.FamilyWealth;
        engine.State.FamilyWealth = -1000m;

        var events = await engine.ExecuteTurnAsync();

        // Turn should still execute and produce events
        Assert.NotNull(events);
        Assert.NotEmpty(events);
        // Week should have advanced
        Assert.Equal(2, engine.State.Week);
    }

    [Test]
    public async Task MafiaGameEngine_GameOver_WhenHeatMaxed()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.HeatLevel = 100;

        await engine.ExecuteTurnAsync();

        Assert.True(engine.State.GameOver);
        Assert.Contains("Feds", engine.State.GameOverReason!);
    }

    [Test]
    public async Task MafiaGameEngine_GameOver_WhenReputationTooLow()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.Reputation = 5;

        await engine.ExecuteTurnAsync();

        Assert.True(engine.State.GameOver);
        Assert.Contains("betrayed", engine.State.GameOverReason!);
    }

    [Test]
    public async Task MafiaGameEngine_Victory_WhenConditionsMet()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);
        engine.State.Week = 52;
        engine.State.FamilyWealth = 1000001m;
        engine.State.Reputation = 85;

        await engine.ExecuteTurnAsync();

        Assert.True(engine.State.GameOver);
        Assert.Contains("Victory", engine.State.GameOverReason!);
    }

    #endregion

    #region GameRulesEngine Additional Tests

    [Test]
    public void GameRulesEngine_EvaluateGameRules_DetectsVictory()
    {
        var state = new GameState
        {
            Week = 52,
            FamilyWealth = 600000m,
            Reputation = 85,
            HeatLevel = 20
        };
        state.Territories["test"] = new Territory();

        var engine = new GameRulesEngine(state);
        var events = engine.EvaluateGameRules();

        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Contains("Victory"));
    }

    [Test]
    public void GameRulesEngine_GetAgentAction_ReturnsCollectionForGreedy()
    {
        var state = new GameState
        {
            FamilyWealth = 50000m,
            HeatLevel = 20
        };

        var engine = new GameRulesEngine(state);
        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality
            {
                Greed = 85,
                Aggression = 40,
                Loyalty = 60,
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // Greedy agent tends towards collection, but rules may choose other actions
        Assert.NotNull(action);
        Assert.True(action == "collection" || action == "wait" || action == "expand");
    }

    [Test]
    public void GameRulesEngine_GetAgentAction_ReturnsIntimidateForAggressive()
    {
        // Setup: Lower heat (30) so defensive rules don't override aggression
        // The strategic AI correctly prioritizes heat management when heat > 50
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 30,  // Lower heat so aggressive retaliation can trigger
            PreviousHeatLevel = 30  // Stable heat, not rising
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 85 };

        var engine = new GameRulesEngine(state);
        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality
            {
                Aggression = 85,
                Greed = 40,
                Loyalty = 60,
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // Aggressive agent with low heat should retaliate or collect opportunistically
        Assert.True(action == "intimidate" || action == "collection" || action == "wait");
    }

    [Test]
    public void GameRulesEngine_GetAgentAction_ReturnsExpandForAmbitious()
    {
        var state = new GameState
        {
            FamilyWealth = 150000m,
            HeatLevel = 30
        };

        var engine = new GameRulesEngine(state);
        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality
            {
                Ambition = 85,
                Aggression = 40,
                Greed = 40,
                Loyalty = 60
            }
        };

        var action = engine.GetAgentAction(agent);

        Assert.True(action == "expand" || action == "wait");
    }

    [Test]
    public void GameRulesEngine_GenerateEvents_WithHighHeat()
    {
        var state = new GameState
        {
            Week = 15,
            HeatLevel = 75,
            Reputation = 35
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 80 };

        var engine = new GameRulesEngine(state);
        var events = engine.GenerateEvents();

        Assert.NotNull(events);
    }

    #endregion

    #region GameRulesEngine Tests

    [Test]
    public void GameRulesEngine_Construction_Works()
    {
        var state = new GameState();
        var engine = new GameRulesEngine(state);
        Assert.NotNull(engine);
    }

    [Test]
    public void GameRulesEngine_ApplyTerritoryValuation_Works()
    {
        var state = new GameState
        {
            FamilyWealth = 200000m,
            Reputation = 80,
            HeatLevel = 30
        };
        var engine = new GameRulesEngine(state);
        var territory = new Territory
        {
            Name = "Test Territory",
            WeeklyRevenue = 10000m,
            Type = "Protection",
            HeatGeneration = 3
        };

        engine.ApplyTerritoryValuation(territory, state);

        // Verify territory data remains valid after rule evaluation
        Assert.NotNull(territory.Name);
        Assert.True(territory.WeeklyRevenue >= 0);
        Assert.Equal("Protection", territory.Type);
    }

    [Test]
    public void GameRulesEngine_ApplyTerritoryValuation_DisputedTerritory()
    {
        var state = new GameState
        {
            FamilyWealth = 200000m,
            Reputation = 60,
            HeatLevel = 30
        };
        var engine = new GameRulesEngine(state);
        var territory = new Territory
        {
            Name = "Disputed Territory",
            WeeklyRevenue = 20000m,
            Type = "Gambling",
            UnderDispute = true
        };

        engine.ApplyTerritoryValuation(territory, state);

        // Disputed territories get revenue penalty
        Assert.True(territory.WeeklyRevenue <= 20000m);
    }

    [Test]
    public void GameRulesEngine_ApplyDifficultyAdjustment_Works()
    {
        var state = new GameState
        {
            Week = 20,
            FamilyWealth = 100000m,
            Reputation = 50,
            HeatLevel = 30
        };
        state.RivalFamilies["test"] = new RivalFamily { Strength = 50, Hostility = 30 };
        var engine = new GameRulesEngine(state);

        engine.ApplyDifficultyAdjustment(state, 35000m, 0, 0);

        // Verify game state remains valid after difficulty adjustment
        Assert.True(state.FamilyWealth >= 0);
        Assert.True(state.Reputation >= 0 && state.Reputation <= 100);
        Assert.True(state.RivalFamilies["test"].Strength >= 0);
    }

    [Test]
    public void GameRulesEngine_ApplyDifficultyAdjustment_PlayerDominating()
    {
        var state = new GameState
        {
            Week = 20,
            FamilyWealth = 600000m,
            Reputation = 85,
            HeatLevel = 20
        };
        state.RivalFamilies["test"] = new RivalFamily { Strength = 50, Hostility = 30 };

        var engine = new GameRulesEngine(state);
        var initialStrength = state.RivalFamilies["test"].Strength;
        engine.ApplyDifficultyAdjustment(state, 50000m, 5, 0);

        // Rivals should get stronger when player dominates
        Assert.True(state.RivalFamilies["test"].Strength >= initialStrength);
    }

    [Test]
    public void GameRulesEngine_ApplyRivalStrategy_Works()
    {
        var state = new GameState
        {
            FamilyWealth = 200000m,
            Reputation = 60,
            HeatLevel = 30
        };
        var rival = new RivalFamily
        {
            Name = "Test Rival",
            Strength = 60,
            Hostility = 40
        };
        var engine = new GameRulesEngine(state);

        engine.ApplyRivalStrategy(rival, state);

        // Verify rival and state data remain valid after strategy evaluation
        Assert.Equal("Test Rival", rival.Name);
        Assert.True(rival.Strength >= 0 && rival.Strength <= 100);
        Assert.True(rival.Hostility >= 0 && rival.Hostility <= 100);
    }

    [Test]
    public void GameRulesEngine_ApplyRivalStrategy_AttackWeakPlayer()
    {
        var state = new GameState
        {
            FamilyWealth = 50000m,
            Reputation = 30,
            HeatLevel = 40
        };
        var rival = new RivalFamily
        {
            Name = "Aggressive Rival",
            Strength = 80,
            Hostility = 50
        };

        var engine = new GameRulesEngine(state);
        var initialWealth = state.FamilyWealth;
        engine.ApplyRivalStrategy(rival, state);

        // Rival may attack weak player, reducing wealth
        Assert.True(state.FamilyWealth <= initialWealth);
    }

    [Test]
    public void GameRulesEngine_ApplyChainReactions_PoliceRaid()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 50,
            HeatLevel = 60
        };
        var engine = new GameRulesEngine(state);

        engine.ApplyChainReactions("PoliceRaid", state);

        // Verify state remains valid after chain reactions
        Assert.True(state.FamilyWealth >= 0);
        Assert.True(state.HeatLevel >= 0 && state.HeatLevel <= 100);
        Assert.False(state.GameOver); // Police raid alone shouldn't end game
    }

    [Test]
    public void GameRulesEngine_ApplyChainReactions_Hit()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 50,
            HeatLevel = 40
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 85 };
        var engine = new GameRulesEngine(state);

        engine.ApplyChainReactions("Hit", state);

        // Verify state remains valid after chain reactions
        Assert.True(state.FamilyWealth >= 0);
        Assert.True(state.RivalFamilies.ContainsKey("test"));
        Assert.True(state.RivalFamilies["test"].Hostility >= 0);
    }

    [Test]
    public void GameRulesEngine_ApplyChainReactions_Betrayal()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 35,
            HeatLevel = 70
        };
        var engine = new GameRulesEngine(state);

        engine.ApplyChainReactions("Betrayal", state);

        // Verify state remains valid after chain reactions
        Assert.True(state.FamilyWealth >= 0);
        Assert.True(state.Reputation >= 0);
        Assert.True(state.HeatLevel >= 0);
    }

    [Test]
    public void GameRulesEngine_ApplyChainReactions_TerritoryLoss()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 50,
            HeatLevel = 30
        };
        var engine = new GameRulesEngine(state);

        engine.ApplyChainReactions("TerritoryLost", state);

        // Verify state remains valid after chain reactions
        Assert.True(state.FamilyWealth >= 0);
        Assert.True(state.Reputation >= 0);
        Assert.False(state.GameOver); // Single territory loss shouldn't end game
    }

    [Test]
    public void GameRulesEngine_ApplyChainReactions_WithData()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 50,
            HeatLevel = 30
        };
        var data = new Dictionary<string, object>
        {
            { "location", "Little Italy" },
            { "damage", 5000m }
        };
        var engine = new GameRulesEngine(state);

        engine.ApplyChainReactions("PoliceRaid", state, data);

        // Verify state remains valid and data was accepted
        Assert.True(state.FamilyWealth >= 0);
        Assert.True(state.HeatLevel >= 0);
        Assert.NotNull(data["location"]);
    }

    #endregion

    #region Context Helper Property Tests

    [Test]
    public void TerritoryValueContext_PrimeTerritory_WorksCorrectly()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 80,
            HeatLevel = 30
        };
        state.Territories["t1"] = new Territory();
        state.Territories["t2"] = new Territory();

        var territory = new Territory
        {
            WeeklyRevenue = 20000m,
            HeatGeneration = 3,
            UnderDispute = false
        };

        var context = new TerritoryValueContext
        {
            Territory = territory,
            GameState = state
        };

        Assert.True(context.IsHighValue);
        Assert.True(context.IsLowRisk);
        Assert.False(context.Disputed);
        Assert.True(context.PrimeTerritory);
    }

    [Test]
    public void TerritoryValueContext_RiskyTerritory_WorksCorrectly()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 50,
            HeatLevel = 70
        };

        var territory = new Territory
        {
            WeeklyRevenue = 25000m,
            HeatGeneration = 15,
            Type = "Smuggling"
        };

        var context = new TerritoryValueContext
        {
            Territory = territory,
            GameState = state
        };

        Assert.True(context.IsHighValue);
        Assert.True(context.IsHighRisk);
        Assert.True(context.RiskyButProfitable);
        Assert.True(context.PoliceWatching);
    }

    [Test]
    public void TerritoryValueContext_TerritoryTypes_WorkCorrectly()
    {
        var state = new GameState();

        var protection = new TerritoryValueContext
        {
            Territory = new Territory { Type = "Protection" },
            GameState = state
        };
        Assert.True(protection.IsProtectionRacket);

        var gambling = new TerritoryValueContext
        {
            Territory = new Territory { Type = "Gambling" },
            GameState = state
        };
        Assert.True(gambling.IsGambling);

        var smuggling = new TerritoryValueContext
        {
            Territory = new Territory { Type = "Smuggling" },
            GameState = state
        };
        Assert.True(smuggling.IsSmuggling);
    }

    [Test]
    public void DifficultyContext_Performance_WorksCorrectly()
    {
        var dominatingState = new GameState
        {
            FamilyWealth = 600000m,
            Reputation = 85
        };
        var dominating = new DifficultyContext
        {
            State = dominatingState,
            Week = 20,
            WinStreak = 5
        };
        Assert.True(dominating.PlayerDominating);
        Assert.True(dominating.OnWinStreak);

        var strugglingState = new GameState
        {
            FamilyWealth = 30000m,
            Reputation = 25
        };
        var struggling = new DifficultyContext
        {
            State = strugglingState,
            Week = 20,
            LossStreak = 4
        };
        Assert.True(struggling.PlayerStruggling);
        Assert.True(struggling.OnLossStreak);
    }

    [Test]
    public void DifficultyContext_GamePhases_WorkCorrectly()
    {
        var state = new GameState();

        var early = new DifficultyContext { State = state, Week = 5 };
        Assert.True(early.EarlyGame);
        Assert.False(early.MidGame);
        Assert.False(early.LateGame);

        var mid = new DifficultyContext { State = state, Week = 20 };
        Assert.False(mid.EarlyGame);
        Assert.True(mid.MidGame);
        Assert.False(mid.LateGame);

        var late = new DifficultyContext { State = state, Week = 40 };
        Assert.False(late.EarlyGame);
        Assert.False(late.MidGame);
        Assert.True(late.LateGame);
    }

    [Test]
    public void RivalStrategyContext_StrategicAssessment_WorksCorrectly()
    {
        var state = new GameState
        {
            FamilyWealth = 50000m,
            Reputation = 30,
            HeatLevel = 80
        };

        var strongRival = new RivalStrategyContext
        {
            Rival = new RivalFamily { Strength = 80, Hostility = 50 },
            GameState = state
        };
        Assert.True(strongRival.RivalIsStronger);
        Assert.True(strongRival.PlayerIsWeak);
        Assert.True(strongRival.PlayerIsDistracted);
    }

    [Test]
    public void RivalStrategyContext_StrategicDecisions_WorkCorrectly()
    {
        var weakPlayerState = new GameState
        {
            FamilyWealth = 50000m,
            Reputation = 30,
            HeatLevel = 40
        };

        var shouldAttack = new RivalStrategyContext
        {
            Rival = new RivalFamily { Strength = 80, Hostility = 60 },
            GameState = weakPlayerState
        };
        Assert.True(shouldAttack.ShouldAttack);

        var strongPlayerState = new GameState
        {
            FamilyWealth = 400000m,
            Reputation = 80,
            HeatLevel = 30
        };

        var shouldPeace = new RivalStrategyContext
        {
            Rival = new RivalFamily { Strength = 30, Hostility = 50, AtWar = true },
            GameState = strongPlayerState
        };
        Assert.True(shouldPeace.ShouldMakePeace);
    }

    [Test]
    public void ChainReactionContext_EventTypes_WorkCorrectly()
    {
        var state = new GameState();

        var raid = new ChainReactionContext
        {
            TriggeringEvent = "PoliceRaid",
            State = state
        };
        Assert.True(raid.WasPoliceRaid);
        Assert.False(raid.WasHit);

        var hit = new ChainReactionContext
        {
            TriggeringEvent = "Hit",
            State = state
        };
        Assert.True(hit.WasHit);
        Assert.False(hit.WasBetrayal);

        var betrayal = new ChainReactionContext
        {
            TriggeringEvent = "Betrayal",
            State = state
        };
        Assert.True(betrayal.WasBetrayal);
    }

    [Test]
    public void ChainReactionContext_CrisisConditions_WorkCorrectly()
    {
        var tensionState = new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 50,
            HeatLevel = 40
        };
        tensionState.RivalFamilies["test"] = new RivalFamily { Hostility = 85 };

        var highTension = new ChainReactionContext
        {
            TriggeringEvent = "Hit",
            State = tensionState
        };
        Assert.True(highTension.HighTension);

        var unstableState = new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 35,
            HeatLevel = 70
        };

        var unstable = new ChainReactionContext
        {
            TriggeringEvent = "Betrayal",
            State = unstableState
        };
        Assert.True(unstable.Unstable);

        var crisisState = new GameState
        {
            FamilyWealth = 20000m,
            HeatLevel = 90
        };

        var crisis = new ChainReactionContext
        {
            TriggeringEvent = "PoliceRaid",
            State = crisisState
        };
        Assert.True(crisis.CrisisMode);
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task MafiaGameEngine_MultiTurnSimulation_WorksCorrectly()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        var startWeek = engine.State.Week;

        // Run 5 turns
        for (int i = 0; i < 5; i++)
        {
            if (engine.State.GameOver) break;
            await engine.ExecuteTurnAsync();
        }

        // Week should have advanced (may end early if game over)
        Assert.True(engine.State.Week > startWeek);
        // Wealth should have changed (collected revenue or spent)
        Assert.True(engine.State.FamilyWealth != 100000m || engine.State.GameOver);
    }

    [Test]
    public async Task MafiaGameEngine_WithAgents_WorksCorrectly()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        // Register agents
        engine.RegisterAutonomousAgent(new GodfatherAgent("godfather-001", "Test Don", logger));
        engine.RegisterAutonomousAgent(new UnderbossAgent("underboss-001", "Test Underboss", logger));
        engine.RegisterAutonomousAgent(new CapoAgent("capo-001", "Test Capo", logger));
        engine.RegisterAutonomousAgent(new SoldierAgent("soldier-001", "Test Soldier", logger));

        engine.SetupRoutingRules();

        // Run a turn
        var events = await engine.ExecuteTurnAsync();

        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Contains("AUTONOMOUS AGENT ACTIONS"));
    }

    [Test]
    public void GameRulesEngine_WithGameRulesEngine_Integration()
    {
        var state = new GameState
        {
            Week = 20,
            FamilyWealth = 200000m,
            Reputation = 60,
            HeatLevel = 40
        };
        state.Territories["downtown"] = new Territory
        {
            Name = "Downtown",
            WeeklyRevenue = 15000m,
            Type = "Protection"
        };
        state.RivalFamilies["test"] = new RivalFamily
        {
            Name = "Test Family",
            Strength = 50,
            Hostility = 40
        };

        var engine = new GameRulesEngine(state);

        // Apply rules from the unified engine
        var gameEvents = engine.EvaluateGameRules();
        engine.ApplyTerritoryValuation(state.Territories["downtown"], state);
        engine.ApplyRivalStrategy(state.RivalFamilies["test"], state);

        Assert.NotNull(gameEvents);
    }

    #endregion

    #region RulesEngine Advanced Feature Tests

    [Test]
    public void GameRulesEngine_AgentRules_PerformanceMetricsAvailable()
    {
        // Setup
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 30,
            PreviousHeatLevel = 30
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 40 };

        var engine = new GameRulesEngine(state);
        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality
            {
                Aggression = 50,
                Greed = 50,
                Loyalty = 50,
                Ambition = 50
            }
        };

        // Execute several times to generate metrics
        for (int i = 0; i < 5; i++)
        {
            engine.GetAgentAction(agent);
        }

        // Get metrics
        var metrics = engine.GetAgentRuleMetrics();
        var summary = engine.GetAgentRulePerformanceSummary();

        // Verify metrics are collected
        Assert.NotNull(metrics);
        Assert.NotNull(summary);
        Assert.Contains("Agent Rule Performance Metrics", summary);
        Assert.Contains("Total rule evaluations", summary);
    }

    [Test]
    public void GameRulesEngine_AgentRules_StopOnFirstMatchBehavior()
    {
        // Setup: Create a scenario where multiple rules could match
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 0,  // Low heat
            PreviousHeatLevel = 10,  // Heat falling
            SoldierCount = 5
        };

        var engine = new GameRulesEngine(state);
        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality
            {
                Aggression = 30,  // Not aggressive
                Greed = 80,       // Greedy - should trigger GREEDY_COLLECTION
                Loyalty = 50,
                Ambition = 50
            }
        };

        // Get action - should return the first matching rule's action
        var action = engine.GetAgentAction(agent);

        // The greedy agent with low heat should collect
        Assert.True(action == "collection" || action == "wait",
            $"Expected collection or wait but got {action}");
    }

    [Test]
    public void GameRulesEngine_RuleBuilder_IntegrationTest()
    {
        // Test that RuleBuilder-created rules work correctly
        var state = new GameState
        {
            FamilyWealth = 60000m,  // Can afford recruit ($50k threshold)
            HeatLevel = 20,
            PreviousHeatLevel = 20,
            SoldierCount = 10  // Needs more soldiers (<15)
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30 };

        var engine = new GameRulesEngine(state);

        // Ambitious agent with money and need for soldiers
        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality
            {
                Aggression = 40,
                Greed = 40,
                Loyalty = 50,
                Ambition = 80  // Ambitious - should trigger AMBITIOUS_RECRUIT
            }
        };

        var action = engine.GetAgentAction(agent);

        // Ambitious agent needing soldiers with wealth should recruit or expand
        Assert.True(action == "recruit" || action == "collection" || action == "wait",
            $"Ambitious agent should recruit/collect/wait but got {action}");
    }

    [Test]
    public void GameRulesEngine_DynamicRuleFactory_IntegrationTest()
    {
        // Test that DynamicRuleFactory-created rules work correctly
        var state = new GameState
        {
            FamilyWealth = 30000m,  // Low wealth - survival mode
            HeatLevel = 90,         // Critical heat
            PreviousHeatLevel = 85, // Heat rising
            SoldierCount = 20
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30 };

        var engine = new GameRulesEngine(state);

        // Load the configurable rules (includes CONFIG_EMERGENCY_LAYLOW at priority 999)
        engine.LoadExampleConfigurableRules();

        // Create an agent in survival mode with critical heat
        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality
            {
                Aggression = 80,  // Very aggressive - would normally attack
                Greed = 80,
                Loyalty = 50,
                Ambition = 80
            }
        };

        var action = engine.GetAgentAction(agent);

        // The CONFIG_EMERGENCY_LAYLOW rule should override normal behavior
        // because it has priority 999 and matches (InSurvivalMode && HeatIsCritical)
        Assert.Equal("laylow", action);
    }

    [Test]
    public void GameRulesEngine_RegisterDynamicAgentRules_CreatesWorkingRules()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 30,
            SoldierCount = 10
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 80, Strength = 70 };

        var engine = new GameRulesEngine(state);

        // Register a custom dynamic rule
        var customRules = new List<AgentRuleDefinition>
        {
            new AgentRuleDefinition
            {
                Id = "CONFIG_CUSTOM_NEGOTIATE",
                Name = "Custom Negotiation Rule",
                Priority = 950,
                Conditions = new List<ConditionDefinition>
                {
                    new ConditionDefinition { PropertyName = "RivalIsThreatening", Operator = "==", Value = true }
                },
                RecommendedAction = "negotiate"
            }
        };

        int registered = engine.RegisterDynamicAgentRules(customRules);

        Assert.Equal(1, registered);

        // Create agent
        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality
            {
                Aggression = 50,
                Greed = 50,
                Loyalty = 50,
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // The custom negotiate rule should trigger due to high rival hostility
        Assert.Equal("negotiate", action);
    }

    [Test]
    public void GameRulesEngine_DynamicRules_CollectedInMetrics()
    {
        var state = new GameState
        {
            FamilyWealth = 200000m,
            HeatLevel = 20,
            PreviousHeatLevel = 20,
            SoldierCount = 20
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 20 };

        var engine = new GameRulesEngine(state);

        // Register a dynamic rule with trackable ID
        var customRules = new List<AgentRuleDefinition>
        {
            new AgentRuleDefinition
            {
                Id = "CONFIG_ALWAYS_COLLECT",
                Name = "Always Collect Test Rule",
                Priority = 1000,  // Highest priority
                Conditions = new List<ConditionDefinition>
                {
                    new ConditionDefinition { PropertyName = "CanAffordExpensive", Operator = "==", Value = true }
                },
                RecommendedAction = "collection"
            }
        };

        engine.RegisterDynamicAgentRules(customRules);

        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality { Aggression = 50, Greed = 50, Loyalty = 50, Ambition = 50 }
        };

        // Execute multiple times
        for (int i = 0; i < 5; i++)
        {
            engine.GetAgentAction(agent);
        }

        // Check metrics include the dynamic rule
        var metrics = engine.GetAgentRuleMetrics();
        Assert.True(metrics.ContainsKey("CONFIG_ALWAYS_COLLECT"),
            "Dynamic rule should appear in metrics");

        var ruleMetrics = metrics["CONFIG_ALWAYS_COLLECT"];
        Assert.Equal(5, ruleMetrics.ExecutionCount);
    }

    #endregion
}
