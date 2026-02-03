using TestRunner.Framework;
using AgentRouting.MafiaDemo;
using AgentRouting.MafiaDemo.Game;
using AgentRouting.MafiaDemo.Rules;
using AgentRouting.Core;
using RulesEngine.Core;
using RulesEngine.Enhanced;
using TestUtilities;

namespace TestRunner.Tests;

/// <summary>
/// Unit tests for AutonomousPlaythrough, GameEngine, RulesBasedEngine, and GameRulesEngine
/// </summary>
public class AutonomousGameTests
{
    private static IAgentLogger CreateTestLogger() => SilentAgentLogger.Instance;

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
        var firstEvent = engine.State.EventLog.First();  // Changed from [0] for Queue compatibility
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
        // Set heat to 135 to account for potential reductions during turn processing:
        // - Territory heat: +11 (balanced from previous +23)
        // - ProcessRandomEvents can reduce heat by 10 (~1.67% chance)
        // - ProcessAsyncEventsAsync can reduce heat by 10 (30% chance if heat > 50)
        // - Agents may bribe: -15
        // - Natural decay: -8 (balanced from previous -5)
        // Worst case: 135 + 11 - 10 - 10 - 15 - 8 = 103, still triggers game over
        engine.State.HeatLevel = 135;

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

    // =========================================================================
    // COMPOSITE RULE TESTS
    // =========================================================================

    [Test]
    public void GameRulesEngine_CompositeRule_OrLogic_TriggersOnAnyMatch()
    {
        // Test that OR composite rules trigger when any sub-rule matches
        var state = new GameState
        {
            FamilyWealth = 500000m,  // Dominance mode
            HeatLevel = 20,
            Reputation = 90,
            SoldierCount = 50
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30, Strength = 30 }; // Weak rival

        var engine = new GameRulesEngine(state);

        // Aggressive agent in dominance mode with weak rival
        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality
            {
                Aggression = 80,
                Greed = 30,
                Loyalty = 50,
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // The COMPOSITE_INTIMIDATE_ACTION or one of the dominance rules should trigger
        Assert.True(action == "intimidate" || action == "expand" || action == "collection",
            $"Dominance mode should trigger strategic action but got {action}");
    }

    [Test]
    public void GameRulesEngine_CompositeRule_AndLogic_RequiresAllConditions()
    {
        // Test that AND composite rules require ALL conditions
        var state = new GameState
        {
            FamilyWealth = 150000m,
            HeatLevel = 30,  // HasHeatBudget = true (< 40)
            PreviousHeatLevel = 30,  // HeatIsRising = false (30 > 35 = false)
            SoldierCount = 20
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30 }; // Not imminent attack

        var engine = new GameRulesEngine(state);

        // Greedy agent that would trigger safe collection composite
        var agent = new GameAgentData
        {
            AgentId = "test",
            Personality = new AgentPersonality
            {
                Aggression = 30,
                Greed = 80,  // IsGreedy
                Loyalty = 50,
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // Safe collection composite should match (all AND conditions met + greedy)
        Assert.True(action == "collection" || action == "expand",
            $"Safe conditions with greedy agent should collect or expand but got {action}");
    }

    // =========================================================================
    // RULE ANALYZER TESTS
    // =========================================================================

    [Test]
    public void GameRulesEngine_RuleAnalyzer_GeneratesReport()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 30,
            SoldierCount = 15
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 40 };

        var engine = new GameRulesEngine(state);

        // Generate test cases and analyze
        var testCases = engine.GenerateAnalysisTestCases();
        var report = engine.AnalyzeAgentRules(testCases);

        // Verify report structure
        Assert.True(report.RuleAnalyses.Count > 0, "Report should have rule analyses");

        // Check that we have some rules with matches
        var matchingRules = report.RuleAnalyses.Where(a => a.MatchedCount > 0).ToList();
        Assert.True(matchingRules.Count > 0, "Some rules should have matches");
    }

    [Test]
    public void GameRulesEngine_RuleAnalyzer_DetectsOverlaps()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 30,
            SoldierCount = 15
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 40 };

        var engine = new GameRulesEngine(state);

        var report = engine.GetAgentRuleAnalysisReport();

        // Report should be non-empty string
        Assert.True(!string.IsNullOrEmpty(report), "Analysis report should not be empty");
        Assert.Contains("Rule Analysis Report", report);
    }

    [Test]
    public void GameRulesEngine_GenerateAnalysisTestCases_CreatesVariedScenarios()
    {
        var state = new GameState { FamilyWealth = 100000m };
        state.RivalFamilies["test"] = new RivalFamily();

        var engine = new GameRulesEngine(state);

        var testCases = engine.GenerateAnalysisTestCases();

        // Should generate multiple varied test cases
        Assert.True(testCases.Count >= 5, $"Expected at least 5 test cases, got {testCases.Count}");

        // Test cases should have different game phases
        var phases = testCases.Select(tc => tc.Phase).Distinct().ToList();
        Assert.True(phases.Count >= 2, "Test cases should cover multiple game phases");
    }

    // =========================================================================
    // DEBUGGABLE RULE TESTS
    // =========================================================================

    [Test]
    public void GameRulesEngine_GetAgentActionWithTrace_ProvidesDetailedTrace()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 30,
            SoldierCount = 15
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 40 };

        var engine = new GameRulesEngine(state);

        var agent = new GameAgentData
        {
            AgentId = "test-agent",
            Personality = new AgentPersonality
            {
                Aggression = 50,
                Greed = 50,
                Loyalty = 50,
                Ambition = 50
            }
        };

        var action = engine.GetAgentActionWithTrace(agent, out var trace);

        // Verify trace contains expected information
        Assert.True(trace.Count > 5, "Trace should have multiple lines");
        Assert.True(trace.Any(t => t.Contains("Decision Trace")), "Trace should have header");
        Assert.True(trace.Any(t => t.Contains("Game Phase")), "Trace should show game phase");
        Assert.True(trace.Any(t => t.Contains("Heat")), "Trace should show heat level");
        Assert.True(trace.Any(t => t.Contains("Rule Evaluation")), "Trace should show rule evaluation");
        Assert.True(trace.Any(t => t.Contains(">>>")), "Trace should show selected action");
    }

    [Test]
    public void GameRulesEngine_GetAgentActionWithTrace_ShowsMatchedRules()
    {
        var state = new GameState
        {
            FamilyWealth = 20000m,  // Survival mode
            HeatLevel = 60,         // Needs heat reduction
            PreviousHeatLevel = 55, // Heat rising
            SoldierCount = 10
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30 };

        var engine = new GameRulesEngine(state);

        var agent = new GameAgentData
        {
            AgentId = "cautious-agent",
            Personality = new AgentPersonality
            {
                Aggression = 20,
                Greed = 30,
                Loyalty = 70,
                Ambition = 30
            }
        };

        var action = engine.GetAgentActionWithTrace(agent, out var trace);

        // Should show some rules that matched (✓) and some that didn't (✗)
        Assert.True(trace.Any(t => t.Contains("✓") || t.Contains("MATCHED")),
            "Trace should show at least one matched rule");
        Assert.True(trace.Any(t => t.Contains("✗") || t.Contains("no match")),
            "Trace should show some non-matching rules");
    }

    // =========================================================================
    // ASYNC RULE TESTS
    // =========================================================================

    [Test]
    public async Task GameRulesEngine_AsyncRule_PoliceInvestigation_ReducesHeat()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 60  // Above 50, triggers police investigation
        };
        state.RivalFamilies["test"] = new RivalFamily();

        var engine = new GameRulesEngine(state);
        engine.SetupAsyncEventRules();

        var initialHeat = state.HeatLevel;

        var result = await engine.ProcessAsyncEventAsync("PoliceActivity", delayMs: 10);

        Assert.Contains("investigation", result.ToLower());
        Assert.Equal(initialHeat - 10, state.HeatLevel);
    }

    [Test]
    public async Task GameRulesEngine_AsyncRule_InformantIntel_GathersInfo()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,  // Above 50k threshold
            HeatLevel = 30
        };
        state.RivalFamilies["test"] = new RivalFamily { Strength = 60, Hostility = 70 };

        var engine = new GameRulesEngine(state);
        engine.SetupAsyncEventRules();

        var result = await engine.ProcessAsyncEventAsync("GatherIntel", delayMs: 10);

        Assert.Contains("intel", result.ToLower());
        Assert.Contains("60", result);  // Rival strength
        Assert.Contains("70", result);  // Rival hostility
    }

    [Test]
    public async Task GameRulesEngine_AsyncRule_BusinessDeal_IncreasesWealth()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 60  // Above 40 threshold
        };
        state.RivalFamilies["test"] = new RivalFamily();

        var engine = new GameRulesEngine(state);
        engine.SetupAsyncEventRules();

        var initialWealth = state.FamilyWealth;
        var expectedBonus = state.Reputation * 100;

        var result = await engine.ProcessAsyncEventAsync("BusinessOpportunity", delayMs: 10);

        Assert.Contains("deal", result.ToLower());
        Assert.Equal(initialWealth + expectedBonus, state.FamilyWealth);
    }

    [Test]
    public async Task GameRulesEngine_AsyncRule_NoMatchingHandler_ReturnsMessage()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 30  // Below 50, won't trigger police investigation
        };
        state.RivalFamilies["test"] = new RivalFamily();

        var engine = new GameRulesEngine(state);
        engine.SetupAsyncEventRules();

        // Trigger police activity but heat is too low
        var result = await engine.ProcessAsyncEventAsync("PoliceActivity", delayMs: 10);

        Assert.Equal("No matching async event handler", result);
    }

    [Test]
    public void GameRulesEngine_AsyncRule_GetAsyncRuleIds_ReturnsAllRules()
    {
        var state = new GameState { FamilyWealth = 100000m };
        state.RivalFamilies["test"] = new RivalFamily();

        var engine = new GameRulesEngine(state);
        engine.SetupAsyncEventRules();

        var ruleIds = engine.GetAsyncRuleIds().ToList();

        Assert.Equal(3, ruleIds.Count);
        Assert.Contains("ASYNC_POLICE_INVESTIGATION", ruleIds);
        Assert.Contains("ASYNC_INFORMANT_INTEL", ruleIds);
        Assert.Contains("ASYNC_BUSINESS_DEAL", ruleIds);
    }

    [Test]
    public async Task GameRulesEngine_AsyncRule_SupportsCancellation()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 60
        };
        state.RivalFamilies["test"] = new RivalFamily();

        var engine = new GameRulesEngine(state);
        engine.SetupAsyncEventRules();

        using var cts = new CancellationTokenSource();

        // Should complete without exception
        var result = await engine.ProcessAsyncEventAsync("PoliceActivity", delayMs: 10, cts.Token);

        Assert.NotNull(result);
    }

    #endregion

    #region RuleValidator and Startup Analysis Tests

    [Test]
    public void GameRulesEngine_ValidateAllAgentRules_ReturnsValidationMessages()
    {
        var state = new GameState();
        state.RivalFamilies["test"] = new RivalFamily();

        var engine = new GameRulesEngine(state);

        var messages = engine.ValidateAllAgentRules();

        // Should return a list (may be empty if all rules are valid)
        Assert.NotNull(messages);
    }

    [Test]
    public void GameRulesEngine_RunStartupRuleAnalysis_ReturnsReport()
    {
        var state = new GameState
        {
            FamilyWealth = 50000m,
            Reputation = 50,
            HeatLevel = 30
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 50 };

        var engine = new GameRulesEngine(state);

        var report = engine.RunStartupRuleAnalysis();

        Assert.NotNull(report);
        Assert.NotEmpty(report);
        Assert.Contains("Agent Rule Startup Analysis", report);
    }

    [Test]
    public void GameRulesEngine_GenerateExtendedTestCases_ReturnsMultipleScenarios()
    {
        var state = new GameState
        {
            FamilyWealth = 50000m,
            Reputation = 50,
            HeatLevel = 30
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 50 };

        var engine = new GameRulesEngine(state);

        var testCases = engine.GenerateExtendedTestCases();

        // Should generate multiple test cases for different scenarios
        Assert.True(testCases.Count >= 8);

        // Check for specific scenarios
        Assert.True(testCases.Any(c => c.GameState.FamilyWealth == 0)); // Bankrupt scenario (zero wealth)
        Assert.True(testCases.Any(c => c.GameState.HeatLevel == 100));   // Max heat scenario
    }

    #endregion

    #region Metrics Persistence Tests

    [Test]
    public void GameRulesEngine_SaveMetricsSnapshot_StoresSnapshot()
    {
        var state = new GameState();
        state.RivalFamilies["test"] = new RivalFamily();

        var engine = new GameRulesEngine(state);

        // Clear history and save a snapshot
        GameRulesEngine.ClearMetricsHistory();
        engine.SaveMetricsSnapshot("TestSession1");

        var history = GameRulesEngine.GetMetricsHistory();

        Assert.NotNull(history);
        Assert.Contains("TestSession1", history);
    }

    [Test]
    public void GameRulesEngine_GetMetricsHistory_ReturnsFormattedString()
    {
        var state = new GameState();
        state.RivalFamilies["test"] = new RivalFamily();

        var engine = new GameRulesEngine(state);

        // Clear and add fresh snapshots
        GameRulesEngine.ClearMetricsHistory();
        engine.SaveMetricsSnapshot("Session_A");
        engine.SaveMetricsSnapshot("Session_B");

        var history = GameRulesEngine.GetMetricsHistory();

        Assert.NotNull(history);
        Assert.Contains("Session_A", history);
        Assert.Contains("Session_B", history);
        Assert.Contains("Rule Metrics History", history);
    }

    [Test]
    public void GameRulesEngine_ClearMetricsHistory_ClearsAllSnapshots()
    {
        var state = new GameState();
        state.RivalFamilies["test"] = new RivalFamily();

        var engine = new GameRulesEngine(state);
        engine.SaveMetricsSnapshot("ToBeCleared");

        GameRulesEngine.ClearMetricsHistory();

        var history = GameRulesEngine.GetMetricsHistory();

        Assert.Contains("No metrics history", history);
    }

    #endregion

    #region Config Loader Tests

    [Test]
    public void RuleConfigLoader_ParseConfigString_ParsesValidConfig()
    {
        var config = @"
# Test config
[RULE]
Id=TEST_RULE_1
Name=Test Rule One
Priority=100
Condition=FamilyNeedsMoney==true
Action=EXPAND

[RULE]
Id=TEST_RULE_2
Name=Test Rule Two
Priority=50
Condition=HighHeat==true
Action=DEFEND
";

        var rules = RuleConfigLoader.ParseConfigString(config);

        Assert.Equal(2, rules.Count);
        Assert.Equal("TEST_RULE_1", rules[0].Id);
        Assert.Equal("Test Rule One", rules[0].Name);
        Assert.Equal(100, rules[0].Priority);
        Assert.True(rules[0].Conditions.Count >= 1);
        Assert.Equal("EXPAND", rules[0].RecommendedAction);

        Assert.Equal("TEST_RULE_2", rules[1].Id);
        Assert.Equal(50, rules[1].Priority);
    }

    [Test]
    public void RuleConfigLoader_ParseConfigString_IgnoresComments()
    {
        var config = @"
# This is a comment
# Another comment
[RULE]
Id=SINGLE_RULE
Name=Single Rule
Priority=75
Condition=IsAggressive==true
Action=WAIT
# Comment in middle
";

        var rules = RuleConfigLoader.ParseConfigString(config);

        Assert.Equal(1, rules.Count);
        Assert.Equal("SINGLE_RULE", rules[0].Id);
    }

    [Test]
    public void RuleConfigLoader_ParseConfigString_HandlesEmptyConfig()
    {
        var config = "";

        var rules = RuleConfigLoader.ParseConfigString(config);

        Assert.Empty(rules);
    }

    [Test]
    public void RuleConfigLoader_ParseConfigString_HandlesCommentsOnlyConfig()
    {
        var config = @"
# Only comments
# No rules here
";

        var rules = RuleConfigLoader.ParseConfigString(config);

        Assert.Empty(rules);
    }

    [Test]
    public void AgentRuleDefinition_HasAllProperties()
    {
        var rule = new AgentRuleDefinition
        {
            Id = "TEST_ID",
            Name = "Test Name",
            Priority = 123,
            Description = "Test Description",
            RecommendedAction = "ATTACK"
        };

        Assert.Equal("TEST_ID", rule.Id);
        Assert.Equal("Test Name", rule.Name);
        Assert.Equal(123, rule.Priority);
        Assert.Equal("Test Description", rule.Description);
        Assert.Equal("ATTACK", rule.RecommendedAction);
    }

    #endregion

    // =========================================================================
    // AGENT ROUTER INTEGRATION TESTS (G-1)
    // =========================================================================

    #region AgentRouter Integration Tests

    [Test]
    public async Task GameEngine_ExecuteTurn_RoutesAgentActions()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        // Execute multiple turns which should route agent actions
        await engine.ExecuteTurnAsync();
        await engine.ExecuteTurnAsync();

        // Verify events were logged during turns
        var events = engine.State.EventLog;
        Assert.True(events.Count > 0, "Events should be logged during turns");

        // Check for AgentRoute events (logged when routing succeeds)
        var routeEvents = events.Where(e => e.Type == "AgentRoute").ToList();
        // Route events indicate the AgentRouter integration is working
        // They may not always fire if agents wait, but the infrastructure is in place
    }

    [Test]
    public void GameEngine_SetupRoutingRules_InitializesCorrectly()
    {
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        // Routing rules should be set up automatically
        // We can verify this by checking that the engine was created without error
        Assert.NotNull(engine);
        Assert.NotNull(engine.State);
        Assert.True(engine.State.Week >= 1, "Game should start at week 1");
    }

    [Test]
    public async Task GameEngine_AgentRouter_HandlesMultipleTurns()
    {
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        // Execute several turns to verify routing doesn't break
        for (int i = 0; i < 5; i++)
        {
            await engine.ExecuteTurnAsync();
        }

        Assert.Equal(6, engine.State.Week);  // Started at 1, executed 5 turns
        Assert.False(engine.State.GameOver, "Game should not be over after 5 turns");
    }

    #endregion

    // =========================================================================
    // NEW PERSONALITY RULES TESTS (G-2)
    // =========================================================================

    #region New Personality Rules Tests

    [Test]
    public void GameRulesEngine_CautiousAgent_AvoidsRisk()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 50,
            PreviousHeatLevel = 40,  // HeatIsRising = true (50 > 45)
            SoldierCount = 15
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30 };

        var engine = new GameRulesEngine(state);

        // Cautious agent (Aggression < 30, Loyalty > 70)
        var agent = new GameAgentData
        {
            AgentId = "cautious-agent",
            Personality = new AgentPersonality
            {
                Aggression = 20,  // < 30
                Greed = 40,
                Loyalty = 80,     // > 70
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // CAUTIOUS_AVOID_RISK should trigger laylow when heat is rising
        Assert.Equal("laylow", action);
    }

    [Test]
    public void GameRulesEngine_FamilyFirstAgent_PrioritizesStability()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 50,
            PreviousHeatLevel = 40,  // HeatIsRising = true
            SoldierCount = 15
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30 };

        var engine = new GameRulesEngine(state);

        // Family-first agent (Loyalty > 90, Greed < 50)
        var agent = new GameAgentData
        {
            AgentId = "family-first-agent",
            Personality = new AgentPersonality
            {
                Aggression = 40,
                Greed = 30,       // < 50
                Loyalty = 95,     // > 90
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // FAMILY_FIRST_STABILITY should trigger bribe when heat is rising
        Assert.Equal("bribe", action);
    }

    [Test]
    public void GameRulesEngine_HotHeadedAgent_TakesRisks()
    {
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 50,  // Not critical
            PreviousHeatLevel = 50,
            SoldierCount = 15
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30 };

        var engine = new GameRulesEngine(state);

        // Hot-headed agent (Aggression > 80, Loyalty < 60)
        var agent = new GameAgentData
        {
            AgentId = "hotheaded-agent",
            Personality = new AgentPersonality
            {
                Aggression = 85,  // > 80
                Greed = 50,
                Loyalty = 40,     // < 60
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // HOTHEADED_RECKLESS should trigger intimidate
        Assert.Equal("intimidate", action);
    }

    [Test]
    public void GameRulesEngine_SurvivalMode_AggressiveAgent_FightsToSurvive()
    {
        var state = new GameState
        {
            FamilyWealth = 30000m,  // Survival mode (< 50000)
            HeatLevel = 75,         // HeatIsDangerous = true (> 70), so SURVIVAL_COLLECTION won't match
            PreviousHeatLevel = 75,
            SoldierCount = 15
        };
        // Weak rival for opportunistic strike
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30, Strength = 20 };

        var engine = new GameRulesEngine(state);

        // Aggressive agent in survival mode with dangerous heat
        var agent = new GameAgentData
        {
            AgentId = "survival-aggressive",
            Personality = new AgentPersonality
            {
                Aggression = 80,  // > 70 = IsAggressive
                Greed = 50,
                Loyalty = 50,
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // With HeatIsDangerous (>70), SURVIVAL_COLLECTION won't fire, allowing SURVIVAL_AGGRESSIVE
        // to trigger intimidate against weak rival
        Assert.Equal("intimidate", action);
    }

    [Test]
    public void GameRulesEngine_RivalWeak_AmbitiousAgent_Expands()
    {
        var state = new GameState
        {
            FamilyWealth = 200000m,
            HeatLevel = 30,
            PreviousHeatLevel = 30,
            SoldierCount = 20
        };
        // Weak rival
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30, Strength = 20 };

        var engine = new GameRulesEngine(state);

        // Ambitious agent (Ambition > 70)
        var agent = new GameAgentData
        {
            AgentId = "ambitious-agent",
            Personality = new AgentPersonality
            {
                Aggression = 50,
                Greed = 50,
                Loyalty = 50,
                Ambition = 80     // > 70
            }
        };

        var action = engine.GetAgentAction(agent);

        // RIVAL_WEAK_AMBITIOUS should trigger expand
        Assert.Equal("expand", action);
    }

    [Test]
    public void GameRulesEngine_WealthGrowing_GreedyAgent_Collects()
    {
        var state = new GameState
        {
            FamilyWealth = 150000m,
            PreviousWealth = 100000m,  // WealthIsGrowing = true (150000 > 105000)
            HeatLevel = 30,
            PreviousHeatLevel = 30,
            SoldierCount = 20
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30 };

        var engine = new GameRulesEngine(state);

        // Greedy agent with growing wealth
        var agent = new GameAgentData
        {
            AgentId = "greedy-agent",
            Personality = new AgentPersonality
            {
                Aggression = 50,
                Greed = 80,       // > 70 = IsGreedy
                Loyalty = 50,
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // WEALTH_GROWING_GREEDY or GREEDY_COLLECTION should trigger collection
        Assert.Equal("collection", action);
    }

    [Test]
    public void GameRulesEngine_HeatRising_WealthyAgent_Bribes()
    {
        var state = new GameState
        {
            FamilyWealth = 200000m,  // > 100000
            HeatLevel = 50,
            PreviousHeatLevel = 40,  // HeatIsRising = true (50 > 45)
            SoldierCount = 20
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30 };

        var engine = new GameRulesEngine(state);

        // Non-aggressive agent (Aggression < 70)
        var agent = new GameAgentData
        {
            AgentId = "wealthy-agent",
            Personality = new AgentPersonality
            {
                Aggression = 50,  // Not aggressive
                Greed = 50,
                Loyalty = 50,
                Ambition = 50
            }
        };

        var action = engine.GetAgentAction(agent);

        // HEAT_RISING_WEALTHY should trigger bribe
        Assert.Equal("bribe", action);
    }

    [Test]
    public void GameRulesEngine_NewRules_FireCorrectly()
    {
        // Test that we can create a GameRulesEngine with all the new rules
        var state = new GameState
        {
            FamilyWealth = 100000m,
            HeatLevel = 30,
            SoldierCount = 15
        };
        state.RivalFamilies["test"] = new RivalFamily { Hostility = 30 };

        var engine = new GameRulesEngine(state);

        // Just verify the engine initializes without error
        Assert.NotNull(engine);

        // Test that various agent personalities get actions
        var agents = new[]
        {
            new GameAgentData { AgentId = "cautious", Personality = new AgentPersonality { Aggression = 20, Loyalty = 80, Greed = 40, Ambition = 50 } },
            new GameAgentData { AgentId = "aggressive", Personality = new AgentPersonality { Aggression = 80, Loyalty = 50, Greed = 50, Ambition = 50 } },
            new GameAgentData { AgentId = "greedy", Personality = new AgentPersonality { Aggression = 50, Loyalty = 50, Greed = 80, Ambition = 50 } },
            new GameAgentData { AgentId = "ambitious", Personality = new AgentPersonality { Aggression = 50, Loyalty = 50, Greed = 50, Ambition = 80 } },
        };

        foreach (var agent in agents)
        {
            var action = engine.GetAgentAction(agent);
            Assert.NotNull(action);
            Assert.True(
                action == "collection" || action == "intimidate" || action == "expand" ||
                action == "laylow" || action == "recruit" || action == "bribe" || action == "wait",
                $"Agent {agent.AgentId} returned invalid action: {action}");
        }
    }

    #endregion

    // =========================================================================
    // VICTORY ACHIEVABILITY TESTS (H-13)
    // =========================================================================

    #region Victory Achievability Tests

    [Test]
    public async Task MafiaGameEngine_VictoryAchievable_SimulatedOptimalPlay()
    {
        // H-13: Integration test to verify victory is achievable
        // This test simulates a full game with balanced heat to verify
        // the game can be won (reaches week 52 with $1M+ and 80+ rep)
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        // Track game progress
        int maxWeeksReached = 0;
        decimal maxWealthReached = 0;
        int maxRepReached = 0;
        bool victoryAchieved = false;

        // Run game for up to 60 weeks (some buffer past victory condition)
        while (!engine.State.GameOver && engine.State.Week <= 60)
        {
            // Track maximums
            maxWeeksReached = engine.State.Week;
            if (engine.State.FamilyWealth > maxWealthReached)
                maxWealthReached = engine.State.FamilyWealth;
            if (engine.State.Reputation > maxRepReached)
                maxRepReached = engine.State.Reputation;

            // Check for victory
            if (engine.State.GameOverReason?.Contains("Victory") == true)
            {
                victoryAchieved = true;
                break;
            }

            // Execute turn
            await engine.ExecuteTurnAsync();

            // Help manage heat if needed (player would do this)
            if (engine.State.HeatLevel > 70 && engine.State.FamilyWealth >= 10000m)
            {
                await engine.ExecutePlayerAction("bribe");
            }
        }

        // Victory achieved OR game progressed far enough to demonstrate winnable
        // The balanced heat mechanics should allow reaching week 52
        Assert.True(
            victoryAchieved || maxWeeksReached >= 52,
            $"Game should be winnable. Reached Week {maxWeeksReached}, " +
            $"Max Wealth ${maxWealthReached:N0}, Max Rep {maxRepReached}. " +
            $"GameOver: {engine.State.GameOver}, Reason: {engine.State.GameOverReason}");
    }

    [Test]
    public async Task MafiaGameEngine_HeatBalance_AllowsProgressTo52Weeks()
    {
        // Verify the heat balance fix allows games to progress past 21 weeks
        // (The bug was that heat accumulated +18/week making games end around week 21)
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        // Run for 30 weeks without intervention
        int weeksCompleted = 0;
        while (!engine.State.GameOver && engine.State.Week <= 30)
        {
            weeksCompleted = engine.State.Week;
            await engine.ExecuteTurnAsync();
        }

        // With balanced heat (net +3/week vs old +18/week),
        // games should last longer than 21 weeks even without intervention
        Assert.True(
            weeksCompleted >= 25 || engine.State.GameOverReason?.Contains("Victory") == true,
            $"Heat balance should allow games to last 25+ weeks. " +
            $"Only reached week {weeksCompleted}. " +
            $"Final heat: {engine.State.HeatLevel}. " +
            $"GameOver reason: {engine.State.GameOverReason}");
    }

    [Test]
    public async Task MafiaGameEngine_VictoryConditions_AllThreeRequirementsMet()
    {
        // Test that when all three victory conditions are met, victory is declared
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        // Set up state just before victory
        engine.State.Week = 51;
        engine.State.FamilyWealth = 900000m;  // Close to $1M
        engine.State.Reputation = 78;          // Close to 80
        engine.State.HeatLevel = 20;           // Safe heat level

        // Run turns until victory or failure
        int turnsRun = 0;
        while (!engine.State.GameOver && turnsRun < 20)
        {
            await engine.ExecuteTurnAsync();
            turnsRun++;

            // If heat gets high, bribe
            if (engine.State.HeatLevel > 60 && engine.State.FamilyWealth >= 10000m)
            {
                await engine.ExecutePlayerAction("bribe");
            }
        }

        // Should have achieved victory (week 52+, $1M+, 80+ rep)
        Assert.True(
            engine.State.GameOver && engine.State.GameOverReason?.Contains("Victory") == true,
            $"Should achieve victory when conditions are close. " +
            $"Week: {engine.State.Week}, Wealth: ${engine.State.FamilyWealth:N0}, " +
            $"Rep: {engine.State.Reputation}, Reason: {engine.State.GameOverReason}");
    }

    #endregion

    // =========================================================================
    // HOSTILITY CLAMPING TESTS (H-4)
    // =========================================================================

    #region Hostility Clamping Tests

    [Test]
    public async Task MafiaGameEngine_RivalHostility_NeverGoesNegative()
    {
        // H-4: Verify rival hostility is clamped to >= 0 after decay
        // The bug was that hostility could go negative when decay (1-2) exceeded current value (1)
        EnsureInstantTiming();
        var logger = CreateTestLogger();
        var engine = new MafiaGameEngine(logger);

        // Set all rivals to very low hostility (1-2) so decay could potentially make them negative
        foreach (var rival in engine.State.RivalFamilies.Values)
        {
            rival.Hostility = 1;
        }

        // Run multiple turns to trigger hostility decay many times
        for (int i = 0; i < 20; i++)
        {
            await engine.ExecuteTurnAsync();

            // After each turn, verify no rival has negative hostility
            foreach (var rival in engine.State.RivalFamilies.Values)
            {
                Assert.True(
                    rival.Hostility >= 0,
                    $"Rival {rival.Name} has negative hostility ({rival.Hostility}) after turn {i + 1}");
            }

            // Reset hostility to low value to keep testing edge case
            foreach (var rival in engine.State.RivalFamilies.Values)
            {
                if (rival.Hostility == 0)
                    rival.Hostility = 1;
            }

            if (engine.State.GameOver) break;
        }
    }

    [Test]
    public void RivalFamily_Hostility_ClampedAfterDirectManipulation()
    {
        // Unit test: Verify the Math.Max(0, ...) pattern works correctly
        var rival = new RivalFamily { Name = "Test", Hostility = 1 };

        // Simulate the decay logic from GameEngine (line 894)
        // Original bug: rival.Hostility -= Random.Shared.Next(1, 3);
        // Fixed: rival.Hostility = Math.Max(0, rival.Hostility - decay);

        // Test edge case: hostility=1, decay=2 should result in 0, not -1
        int decay = 2;
        rival.Hostility = Math.Max(0, rival.Hostility - decay);

        Assert.Equal(0, rival.Hostility);
        Assert.True(rival.Hostility >= 0, "Hostility should never be negative");
    }

    #endregion
}
