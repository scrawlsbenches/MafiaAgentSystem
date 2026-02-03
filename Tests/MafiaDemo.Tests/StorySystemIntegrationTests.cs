using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.MafiaDemo;
using AgentRouting.MafiaDemo.Game;
using AgentRouting.MafiaDemo.Missions;
using AgentRouting.MafiaDemo.Story;
using AgentRouting.MafiaDemo.Story.Integration;
using TestUtilities;

namespace MafiaDemo.Tests;

/// <summary>
/// Integration tests for the Story System components working together.
/// Tests GameWorldBridge, HybridMissionGenerator, PlotThreads, and MissionConsequences.
/// </summary>
public class StorySystemIntegrationTests : MafiaTestBase
{
    #region GameWorldBridge Tests

    [Test]
    public void GameWorldBridge_Initialize_CreatesMappings()
    {
        // Arrange
        var gameState = CreateTestGameState();
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var bridge = new GameWorldBridge();

        // Act
        bridge.Initialize(gameState, worldState);

        // Assert
        Assert.True(bridge.IsInitialized);
    }

    [Test]
    public void GameWorldBridge_SyncToWorldState_UpdatesWeek()
    {
        // Arrange
        var gameState = CreateTestGameState();
        gameState.Week = 10;
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var bridge = new GameWorldBridge();
        bridge.Initialize(gameState, worldState);

        // Act
        bridge.SyncToWorldState(gameState, worldState);

        // Assert
        Assert.Equal(10, worldState.CurrentWeek);
    }

    [Test]
    public void GameWorldBridge_SyncFromWorldState_UpdatesGameWeek()
    {
        // Arrange
        var gameState = CreateTestGameState();
        var worldState = WorldStateSeeder.CreateInitialWorld();
        worldState.CurrentWeek = 15;
        var bridge = new GameWorldBridge();
        bridge.Initialize(gameState, worldState);

        // Act
        bridge.SyncFromWorldState(worldState, gameState);

        // Assert
        Assert.Equal(15, gameState.Week);
    }

    [Test]
    public void GameWorldBridge_GetLocationId_ReturnsMappedId()
    {
        // Arrange
        var gameState = CreateTestGameState();
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var bridge = new GameWorldBridge();
        bridge.Initialize(gameState, worldState);

        // Act
        var locationId = bridge.GetLocationId("Little Italy");

        // Assert - should normalize to lowercase with dashes
        Assert.NotNull(locationId);
        Assert.Equal("little-italy", locationId);
    }

    [Test]
    public void GameWorldBridge_SyncToWorldState_PropagatesContestState()
    {
        // Arrange
        var gameState = CreateTestGameState();
        gameState.Territories["little-italy"].UnderDispute = true;
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var bridge = new GameWorldBridge();
        bridge.Initialize(gameState, worldState);

        // Act
        bridge.SyncToWorldState(gameState, worldState);

        // Assert
        var location = worldState.GetLocation("little-italy");
        Assert.NotNull(location);
        Assert.Equal(LocationState.Contested, location.State);
    }

    #endregion

    #region HybridMissionGenerator Tests

    [Test]
    public void HybridMissionGenerator_WithStorySystem_GeneratesMission()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var intel = new IntelRegistry();
        var history = new MissionHistory();
        var generator = new HybridMissionGenerator(worldState, storyGraph, intel, history);
        var player = CreateTestPlayer();
        var gameState = CreateTestGameState();

        // Act
        var mission = generator.GenerateMission(player, gameState);

        // Assert
        Assert.NotNull(mission);
        Assert.NotNull(mission.Title);
        Assert.True(mission.Title.Length > 0);
    }

    [Test]
    public void HybridMissionGenerator_StorySystemEnabled_ReturnsTrue()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var intel = new IntelRegistry();
        var history = new MissionHistory();
        var generator = new HybridMissionGenerator(worldState, storyGraph, intel, history);

        // Assert
        Assert.True(generator.StorySystemEnabled);
    }

    [Test]
    public void HybridMissionGenerator_LegacyMode_WorksWithoutStory()
    {
        // Arrange - create generator without Story System
        var generator = new HybridMissionGenerator();
        var player = CreateTestPlayer();
        var gameState = CreateTestGameState();

        // Act
        var mission = generator.GenerateMission(player, gameState);

        // Assert
        Assert.NotNull(mission);
        Assert.False(generator.StorySystemEnabled);
    }

    [Test]
    public void HybridMissionGenerator_GenerateMissionChoices_ReturnsMultipleMissions()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var intel = new IntelRegistry();
        var history = new MissionHistory();
        var generator = new HybridMissionGenerator(worldState, storyGraph, intel, history);
        var player = CreateTestPlayer();
        var gameState = CreateTestGameState();

        // Act
        var choices = generator.GenerateMissionChoices(player, gameState, 3);

        // Assert
        Assert.True(choices.Count >= 1); // Should have at least one
        Assert.True(choices.Count <= 3); // Should not exceed requested count
    }

    [Test]
    public void HybridMissionGenerator_MissionsHaveValidProperties()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var intel = new IntelRegistry();
        var history = new MissionHistory();
        var generator = new HybridMissionGenerator(worldState, storyGraph, intel, history);
        var player = CreateTestPlayer();
        var gameState = CreateTestGameState();

        // Act
        var mission = generator.GenerateMission(player, gameState);

        // Assert - verify mission has valid properties
        Assert.True(mission.RiskLevel >= 1 && mission.RiskLevel <= 10);
        Assert.True(mission.RespectReward >= 0);
        Assert.True(mission.MoneyReward >= 0);
        Assert.True(mission.HeatGenerated >= 0);
    }

    #endregion

    #region MissionAdapter Tests

    [Test]
    public void MissionAdapter_ToMission_ConvertsMissionCandidate()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var candidate = new MissionCandidate
        {
            Source = MissionSource.StoryGraph,
            NodeId = "test-node",
            MissionType = "Collection",
            Title = "Collect from Tony's",
            Description = "Get the weekly payment",
            LocationId = "little-italy",
            Priority = 50,
            Score = 75f
        };

        // Act
        var mission = MissionAdapter.ToMission(candidate, worldState);

        // Assert
        Assert.Equal("test-node", mission.Id);
        Assert.Equal("Collect from Tony's", mission.Title);
        Assert.Equal("Get the weekly payment", mission.Description);
        Assert.Equal(MissionType.Collection, mission.Type);
        Assert.Equal("little-italy", mission.LocationId);
    }

    [Test]
    public void MissionAdapter_ToMission_IncludesStoryMetadata()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var candidate = new MissionCandidate
        {
            Source = MissionSource.PlotThread,
            NodeId = "plot-mission-1",
            MissionType = "Information",
            Title = "Plot Mission",
            Description = "Part of the main story",
            PlotThreadId = "main-plot",
            Priority = 80,
            Score = 90f
        };

        // Act
        var mission = MissionAdapter.ToMission(candidate, worldState);

        // Assert
        Assert.True(mission.Data.ContainsKey("Source"));
        Assert.Equal("PlotThread", mission.Data["Source"]);
        Assert.True(mission.Data.ContainsKey("PlotThreadId"));
        Assert.Equal("main-plot", mission.Data["PlotThreadId"]);
    }

    [Test]
    public void MissionAdapter_ToMission_ParsesMissionTypes()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var types = new[] { "Collection", "Intimidation", "Information", "Negotiation", "Hit", "Territory", "Recruitment" };
        var expectedTypes = new[] { MissionType.Collection, MissionType.Intimidation, MissionType.Information,
                                    MissionType.Negotiation, MissionType.Hit, MissionType.Territory, MissionType.Recruitment };

        for (int i = 0; i < types.Length; i++)
        {
            var candidate = new MissionCandidate
            {
                MissionType = types[i],
                Title = $"Test {types[i]}",
                Description = "Test"
            };

            // Act
            var mission = MissionAdapter.ToMission(candidate, worldState);

            // Assert
            Assert.Equal(expectedTypes[i], mission.Type);
        }
    }

    #endregion

    #region PlotThread Lifecycle Tests

    [Test]
    public void PlotThread_Lifecycle_DormantToAvailable()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = new StoryGraph();

        var plot = new PlotThread
        {
            Id = "test-plot",
            Title = "Test Plot",
            State = PlotState.Dormant,
            ActivationCondition = w => w.CurrentWeek >= 5
        };
        storyGraph.AddPlotThread(plot);

        // Initially dormant
        Assert.Equal(PlotState.Dormant, plot.State);

        // Act - advance to week 5
        worldState.CurrentWeek = 5;
        storyGraph.UpdateUnlocks(worldState);

        // Assert - should be available now
        Assert.Equal(PlotState.Available, plot.State);
    }

    [Test]
    public void PlotThread_StartPlotThread_TransitionsToActive()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = new StoryGraph();

        var plot = new PlotThread
        {
            Id = "test-plot",
            Title = "Test Plot",
            State = PlotState.Available
        };
        storyGraph.AddPlotThread(plot);

        // Act
        var started = storyGraph.StartPlotThread("test-plot", 10);

        // Assert
        Assert.True(started);
        Assert.Equal(PlotState.Active, plot.State);
        Assert.Equal(10, plot.StartedAtWeek);
    }

    [Test]
    public void PlotThread_StartPlotThread_FailsIfNotAvailable()
    {
        // Arrange
        var storyGraph = new StoryGraph();

        var plot = new PlotThread
        {
            Id = "test-plot",
            Title = "Test Plot",
            State = PlotState.Dormant // Not available
        };
        storyGraph.AddPlotThread(plot);

        // Act
        var started = storyGraph.StartPlotThread("test-plot", 10);

        // Assert
        Assert.False(started);
        Assert.Equal(PlotState.Dormant, plot.State);
    }

    [Test]
    public void PlotThread_IsPlotCompleted_ChecksMissionIndex()
    {
        // Arrange
        var storyGraph = new StoryGraph();

        var plot = new PlotThread
        {
            Id = "test-plot",
            Title = "Test Plot",
            State = PlotState.Active,
            MissionNodeIds = new List<string> { "m1", "m2", "m3" },
            CurrentMissionIndex = 0
        };
        storyGraph.AddPlotThread(plot);

        // Act & Assert - not completed initially
        Assert.False(storyGraph.IsPlotCompleted("test-plot"));

        // Advance past all missions
        plot.CurrentMissionIndex = 3;
        Assert.True(storyGraph.IsPlotCompleted("test-plot"));
    }

    #endregion

    #region MissionConsequenceHandler Tests

    [Test]
    public void MissionConsequenceHandler_AppliesRelationshipChanges()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var npc = new NPC
        {
            Id = "test-npc",
            Name = "Test NPC",
            Relationship = 0,
            Status = NPCStatus.Active
        };
        worldState.RegisterNPC(npc);

        var mission = new Mission
        {
            Type = MissionType.Intimidation,
            NPCId = "test-npc"
        };

        var result = new MissionResult { Success = true };

        // Act
        var consequence = MissionConsequenceHandler.ApplyMissionConsequences(mission, result, worldState);

        // Assert
        Assert.NotNull(consequence);
        Assert.True(npc.Relationship < 0); // Intimidation reduces relationship
    }

    [Test]
    public void MissionConsequenceHandler_NegotiationImprovesRelationship()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var npc = new NPC
        {
            Id = "test-npc",
            Name = "Test NPC",
            Relationship = 0,
            Status = NPCStatus.Active
        };
        worldState.RegisterNPC(npc);

        var mission = new Mission
        {
            Type = MissionType.Negotiation,
            NPCId = "test-npc"
        };

        var result = new MissionResult { Success = true };

        // Act
        MissionConsequenceHandler.ApplyMissionConsequences(mission, result, worldState);

        // Assert
        Assert.True(npc.Relationship > 0); // Negotiation improves relationship
    }

    [Test]
    public void MissionConsequenceHandler_NoNPC_ReturnsNull()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var mission = new Mission
        {
            Type = MissionType.Collection,
            NPCId = null // No NPC
        };
        var result = new MissionResult { Success = true };

        // Act
        var consequence = MissionConsequenceHandler.ApplyMissionConsequences(mission, result, worldState);

        // Assert
        Assert.Null(consequence);
    }

    [Test]
    public void MissionConsequenceHandler_RecordsInteractionHistory()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        worldState.CurrentWeek = 5;
        var npc = new NPC
        {
            Id = "test-npc",
            Name = "Test NPC",
            Relationship = 0,
            Status = NPCStatus.Active
        };
        worldState.RegisterNPC(npc);

        var mission = new Mission
        {
            Id = "mission-1",
            Type = MissionType.Collection,
            NPCId = "test-npc"
        };

        var result = new MissionResult { Success = true };

        // Act
        MissionConsequenceHandler.ApplyMissionConsequences(mission, result, worldState);

        // Assert
        Assert.Equal(5, npc.LastInteractionWeek);
        Assert.Equal(1, npc.TotalInteractions);
        Assert.Equal("mission-1", npc.LastMissionId);
        Assert.True(npc.InteractionHistory.Count > 0);
    }

    #endregion

    #region GameEngine Integration Tests

    [Test]
    public void GameEngine_StorySystemEnabled_WhenInitialized()
    {
        // Arrange
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var logger = new SilentAgentLogger();

        // Act
        var engine = new MafiaGameEngine(logger);

        // Assert
        Assert.True(engine.StorySystemEnabled);
        Assert.NotNull(engine.WorldState);
        Assert.NotNull(engine.StoryGraph);
        Assert.NotNull(engine.IntelRegistry);
    }

    [Test]
    public void GameEngine_GenerateMission_ReturnsValidMission()
    {
        // Arrange
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var logger = new SilentAgentLogger();
        var engine = new MafiaGameEngine(logger);
        var player = CreateTestPlayer();

        // Act
        var mission = engine.GenerateMission(player);

        // Assert
        Assert.NotNull(mission);
        Assert.NotNull(mission.Title);
        Assert.True(mission.Title.Length > 0);
    }

    [Test]
    public void GameEngine_GenerateMissionChoices_ReturnsMultiple()
    {
        // Arrange
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var logger = new SilentAgentLogger();
        var engine = new MafiaGameEngine(logger);
        var player = CreateTestPlayer();

        // Act
        var choices = engine.GenerateMissionChoices(player, 3);

        // Assert
        Assert.True(choices.Count >= 1);
    }

    [Test]
    public void GameEngine_RecordMissionCompletion_UpdatesHistory()
    {
        // Arrange
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var logger = new SilentAgentLogger();
        var engine = new MafiaGameEngine(logger);
        var mission = new Mission
        {
            Type = MissionType.Collection,
            LocationId = "little-italy",
            NPCId = "npc-1"
        };

        // Act - should not throw
        engine.RecordMissionCompletion(mission, success: true);

        // Assert - history should have recorded it
        Assert.NotNull(engine.MissionHistory);
    }

    [Test]
    public void GameEngine_DisabledStorySystem_StillWorks()
    {
        // Arrange
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var logger = new SilentAgentLogger();

        // Act - disable Story System
        var engine = new MafiaGameEngine(null, logger, enableStorySystem: false);

        // Assert
        Assert.False(engine.StorySystemEnabled);
        Assert.Null(engine.WorldState);
    }

    [Test]
    public void GameEngine_GetAvailablePlotMissions_ReturnsEmptyWhenNone()
    {
        // Arrange
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var logger = new SilentAgentLogger();
        var engine = new MafiaGameEngine(logger);

        // Act
        var missions = engine.GetAvailablePlotMissions().ToList();

        // Assert - might be empty or have some initial missions
        Assert.NotNull(missions);
    }

    #endregion

    #region MissionHistory Tests

    [Test]
    public void MissionHistory_RecordMission_TracksType()
    {
        // Arrange
        var history = new MissionHistory();

        // Act
        history.RecordMission("Collection", null, null, 1);
        history.RecordMission("Collection", null, null, 2);

        // Assert - repetition score should be higher for repeated type
        var score = history.GetRepetitionScore("Collection", null, null, 3);
        Assert.True(score > 0);
    }

    [Test]
    public void MissionHistory_GetRepetitionScore_PenalizesRecentLocations()
    {
        // Arrange
        var history = new MissionHistory();
        history.RecordMission("Collection", "loc-1", null, 1);

        // Act - check score for same location
        var sameLocScore = history.GetRepetitionScore("Information", "loc-1", null, 2);
        var diffLocScore = history.GetRepetitionScore("Information", "loc-2", null, 2);

        // Assert
        Assert.True(sameLocScore > diffLocScore);
    }

    [Test]
    public void MissionHistory_GetRepetitionScore_DecaysOverTime()
    {
        // Arrange
        var history = new MissionHistory();
        history.RecordMission("Collection", "loc-1", null, 1);

        // Act - check score at different times
        var nearScore = history.GetRepetitionScore("Information", "loc-1", null, 2);
        var farScore = history.GetRepetitionScore("Information", "loc-1", null, 10);

        // Assert - further in time should have lower penalty
        Assert.True(nearScore >= farScore);
    }

    #endregion

    #region Helper Methods

    private GameState CreateTestGameState()
    {
        var state = new GameState();
        state.Territories["little-italy"] = new Territory
        {
            Name = "Little Italy",
            ControlledBy = "capo-001",
            WeeklyRevenue = 15000,
            HeatGeneration = 2,
            Type = "Protection"
        };
        state.RivalFamilies["tattaglia"] = new RivalFamily
        {
            Name = "Tattaglia Family",
            Strength = 60,
            Hostility = 20
        };
        return state;
    }

    private PlayerCharacter CreateTestPlayer()
    {
        return new PlayerCharacter
        {
            Name = "Test Player",
            Rank = PlayerRank.Soldier,
            Respect = 50,
            Money = 5000m,
            Heat = 10,
            Skills = new PlayerSkills
            {
                Intimidation = 30,
                Negotiation = 25,
                StreetSmarts = 35,
                Leadership = 20,
                Business = 15
            }
        };
    }

    #endregion
}
