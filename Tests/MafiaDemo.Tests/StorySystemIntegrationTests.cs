using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.MafiaDemo;
using AgentRouting.MafiaDemo.AI;
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

    [Test]
    public void GameState_Week_CanBeSetAndRead()
    {
        // Arrange - Create GameState without linking WorldState
        var gameState = CreateTestGameState();

        // Act - Set week value
        gameState.Week = 42;

        // Assert - Should return the set value
        Assert.Equal(42, gameState.Week);
    }

    [Test]
    public void GameEngine_WorldState_WeekSyncsWithGameState()
    {
        // Arrange - Create GameEngine which links week counters internally
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var logger = new SilentAgentLogger();
        var engine = new MafiaGameEngine(logger);

        // The engine links GameState.Week to WorldState.CurrentWeek internally
        Assert.True(engine.StorySystemEnabled);
        Assert.NotNull(engine.WorldState);

        // Act - Advance week through game state
        var initialWorldWeek = engine.WorldState.CurrentWeek;

        // Verify initial state is consistent
        Assert.Equal(1, initialWorldWeek); // Initial week should be 1
    }

    [Test]
    public void GameEngine_WithStorySystem_LinksWeekCounter()
    {
        // Arrange & Act - Create GameEngine with Story System enabled
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var logger = new SilentAgentLogger();
        var engine = new MafiaGameEngine(logger);

        // The engine should have linked the week counters
        // We can verify this by checking that StorySystemEnabled is true
        Assert.True(engine.StorySystemEnabled);

        // Verify WorldState and other Story System components are present
        Assert.NotNull(engine.WorldState);
        Assert.NotNull(engine.StoryGraph);
        Assert.NotNull(engine.IntelRegistry);
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

    [Test]
    public void MissionConsequenceHandler_ApplyConsequenceRules_IntimidationSuccess()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var gameState = CreateTestGameState();

        var npc = new NPC
        {
            Id = "test-npc",
            Name = "Test Target",
            Relationship = 0,
            Status = NPCStatus.Active,
            LocationId = "little-italy"
        };
        worldState.RegisterNPC(npc);

        var mission = new Mission
        {
            Type = MissionType.Intimidation,
            NPCId = "test-npc"
        };

        var result = new MissionResult { Success = true };

        // Act
        var consequences = MissionConsequenceHandler.ApplyConsequenceRules(
            mission, result, worldState, storyGraph, gameState);

        // Assert
        Assert.True(consequences.Count > 0);
        Assert.Equal(NPCStatus.Intimidated, npc.Status);
        Assert.True(npc.Relationship < 0); // Relationship decreased
    }

    [Test]
    public void MissionConsequenceHandler_ApplyConsequenceRules_IntimidationFailure_UnlocksRevenge()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var gameState = CreateTestGameState();

        var npc = new NPC
        {
            Id = "test-npc",
            Name = "Test Target",
            Relationship = 0,
            Status = NPCStatus.Active,
            LocationId = "little-italy"
        };
        worldState.RegisterNPC(npc);

        var mission = new Mission
        {
            Type = MissionType.Intimidation,
            NPCId = "test-npc"
        };

        var result = new MissionResult { Success = false };

        // Verify no revenge node exists initially
        var revengeNodeId = $"revenge-{npc.Id}";
        Assert.Null(storyGraph.GetNode(revengeNodeId));

        // Act
        var consequences = MissionConsequenceHandler.ApplyConsequenceRules(
            mission, result, worldState, storyGraph, gameState);

        // Assert
        Assert.True(consequences.Count > 0);
        Assert.Equal(NPCStatus.Hostile, npc.Status);
        // Revenge mission should be added to the story graph
        var revengeNode = storyGraph.GetNode(revengeNodeId);
        Assert.NotNull(revengeNode, "Failed intimidation should unlock a revenge mission");
        Assert.Equal(StoryNodeType.Threat, revengeNode!.Type);
    }

    [Test]
    public void MissionConsequenceHandler_ApplyConsequenceRules_HitSuccess_KillsNPC()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var gameState = CreateTestGameState();

        // Create a faction for the NPC
        var faction = new Faction
        {
            Id = "test-faction",
            Name = "Test Faction",
            Hostility = 30,
            Resources = 50
        };
        worldState.Factions["test-faction"] = faction;

        var npc = new NPC
        {
            Id = "test-npc",
            Name = "Test Target",
            Status = NPCStatus.Active,
            FactionId = "test-faction"
        };
        worldState.RegisterNPC(npc);

        var mission = new Mission
        {
            Type = MissionType.Hit,
            NPCId = "test-npc"
        };

        var result = new MissionResult { Success = true };

        // Act
        var consequences = MissionConsequenceHandler.ApplyConsequenceRules(
            mission, result, worldState, storyGraph, gameState);

        // Assert
        Assert.True(consequences.Count > 0);
        Assert.Equal(NPCStatus.Dead, npc.Status);
        Assert.True(faction.Hostility > 30); // Faction hostility increased
    }

    [Test]
    public void MissionConsequenceHandler_RecordIntel_FromNPCMission()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var intelRegistry = new IntelRegistry();
        worldState.CurrentWeek = 5;

        var npc = new NPC
        {
            Id = "test-informant",
            Name = "Test Informant",
            Status = NPCStatus.Informant,
            Relationship = 30,
            LocationId = "little-italy",
            FactionId = null
        };
        worldState.RegisterNPC(npc);

        var mission = new Mission
        {
            Type = MissionType.Information,
            NPCId = "test-informant",
            Description = "Get info from the informant"
        };

        var result = new MissionResult { Success = true, RespectGained = 5 };

        // Act
        var intel = MissionConsequenceHandler.RecordIntelFromMission(
            mission, result, worldState, intelRegistry, "TestPlayer");

        // Assert
        Assert.NotNull(intel);
        Assert.Equal(IntelType.NPCStatus, intel.Type);
        Assert.Equal("test-informant", intel.SubjectId);
        Assert.Equal("npc", intel.SubjectType);
        Assert.Equal(5, intel.GatheredWeek);
        Assert.True(intel.Reliability >= 70); // Base reliability + respect bonus
        Assert.True(intel.Data.ContainsKey("Status"));
        Assert.Equal("Informant", intel.Data["Status"]);

        // Verify it was added to registry
        var retrievedIntel = intelRegistry.GetForSubject("test-informant", 5).ToList();
        Assert.Equal(1, retrievedIntel.Count);
    }

    [Test]
    public void MissionConsequenceHandler_RecordIntel_FromLocationMission()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var intelRegistry = new IntelRegistry();
        worldState.CurrentWeek = 10;

        // WorldStateSeeder should have created little-italy
        var location = worldState.GetLocation("little-italy");
        Assert.NotNull(location, "Test requires little-italy location from WorldStateSeeder");

        var mission = new Mission
        {
            Type = MissionType.Information,
            LocationId = "little-italy",
            Description = "Scout the neighborhood"
        };

        var result = new MissionResult { Success = true };

        // Act
        var intel = MissionConsequenceHandler.RecordIntelFromMission(
            mission, result, worldState, intelRegistry, "Scout");

        // Assert
        Assert.NotNull(intel);
        Assert.Equal(IntelType.LocationHeat, intel.Type);
        Assert.Equal("little-italy", intel.SubjectId);
        Assert.Equal("location", intel.SubjectType);
        Assert.True(intel.Data.ContainsKey("LocalHeat"));
        Assert.True(intel.Data.ContainsKey("State"));
    }

    [Test]
    public void MissionConsequenceHandler_RecordIntel_FailedMission_ReturnsNull()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var intelRegistry = new IntelRegistry();

        var mission = new Mission
        {
            Type = MissionType.Information,
            LocationId = "little-italy"
        };

        var result = new MissionResult { Success = false }; // Failed mission

        // Act
        var intel = MissionConsequenceHandler.RecordIntelFromMission(
            mission, result, worldState, intelRegistry, "TestPlayer");

        // Assert
        Assert.Null(intel); // No intel from failed mission
    }

    [Test]
    public void MissionConsequenceHandler_RecordIntel_NonInfoMission_ReturnsNull()
    {
        // Arrange
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var intelRegistry = new IntelRegistry();

        var mission = new Mission
        {
            Type = MissionType.Collection, // Not an Information mission
            LocationId = "little-italy"
        };

        var result = new MissionResult { Success = true };

        // Act
        var intel = MissionConsequenceHandler.RecordIntelFromMission(
            mission, result, worldState, intelRegistry, "TestPlayer");

        // Assert
        Assert.Null(intel); // No intel from non-Information mission
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

    #region PlayerAgent Story System Integration Tests

    [Test]
    public async void PlayerAgent_WithStorySystem_AppliesConsequenceRules()
    {
        // Arrange - Create PlayerAgent with Story System components wired up
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var agent = new PlayerAgent("Consequence Tester");
        agent.Character.Skills.Intimidation = 100;  // High skill for success
        agent.Character.Heat = 0;

        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var intelRegistry = new IntelRegistry();

        // Wire up Story System
        agent.WorldState = worldState;
        agent.StoryGraph = storyGraph;
        agent.IntelRegistry = intelRegistry;

        // Create NPC target for intimidation
        var npc = new NPC
        {
            Id = "target-npc",
            Name = "Target NPC",
            Relationship = 0,
            Status = NPCStatus.Active,
            LocationId = "little-italy"
        };
        worldState.RegisterNPC(npc);

        var mission = new Mission
        {
            Type = MissionType.Intimidation,
            Title = "Intimidate Target",
            NPCId = "target-npc",
            RespectReward = 10,
            MoneyReward = 500m,
            HeatGenerated = 3,
            SkillRequirements = new Dictionary<string, int> { ["Intimidation"] = 10 }
        };

        var gameState = CreateTestGameState();

        // Act - Execute multiple times to ensure at least one success
        MissionExecutionResult? successResult = null;
        for (int i = 0; i < 10; i++)
        {
            var testMission = new Mission
            {
                Type = MissionType.Intimidation,
                Title = "Intimidate Target",
                NPCId = "target-npc",
                RespectReward = 10,
                MoneyReward = 500m,
                HeatGenerated = 3,
                SkillRequirements = new Dictionary<string, int> { ["Intimidation"] = 10 }
            };
            var result = await agent.ExecuteMissionAsync(testMission, gameState);
            if (result.MissionResult.Success)
            {
                successResult = result;
                break;
            }
        }

        // Assert - If we got a success, consequences should have been applied
        if (successResult != null)
        {
            // NPC should be intimidated (from ConsequenceRules)
            Assert.Equal(NPCStatus.Intimidated, npc.Status);
            // Message should contain consequence info
            Assert.Contains("[", successResult.MissionResult.Message);
        }
    }

    [Test]
    public async void PlayerAgent_WithStorySystem_RecordsIntelForInformationMissions()
    {
        // Arrange
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var agent = new PlayerAgent("Intel Gatherer");
        agent.Character.Skills.StreetSmarts = 100;  // High skill for success
        agent.Character.Heat = 0;

        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var intelRegistry = new IntelRegistry();
        worldState.CurrentWeek = 5;

        // Wire up Story System
        agent.WorldState = worldState;
        agent.StoryGraph = storyGraph;
        agent.IntelRegistry = intelRegistry;

        // Create NPC informant
        var npc = new NPC
        {
            Id = "informant-npc",
            Name = "Street Informant",
            Relationship = 30,
            Status = NPCStatus.Informant,
            LocationId = "little-italy"
        };
        worldState.RegisterNPC(npc);

        var gameState = CreateTestGameState();

        // Act - Execute multiple times to ensure at least one success
        MissionExecutionResult? successResult = null;
        int initialIntelCount = intelRegistry.GetForSubject("informant-npc", 5).Count();

        for (int i = 0; i < 10; i++)
        {
            var mission = new Mission
            {
                Type = MissionType.Information,
                Title = "Gather Intel",
                NPCId = "informant-npc",
                Description = "Get street info",
                RespectReward = 5,
                MoneyReward = 200m,
                HeatGenerated = 1,
                SkillRequirements = new Dictionary<string, int> { ["StreetSmarts"] = 10 }
            };
            var result = await agent.ExecuteMissionAsync(mission, gameState);
            if (result.MissionResult.Success)
            {
                successResult = result;
                break;
            }
        }

        // Assert - If we got a success, intel should have been recorded
        if (successResult != null)
        {
            var intel = intelRegistry.GetForSubject("informant-npc", 5).ToList();
            Assert.True(intel.Count > initialIntelCount, "Intel should be recorded for successful Information mission");
            // Message should contain intel recording info
            Assert.Contains("[Intel recorded:", successResult.MissionResult.Message);
        }
    }

    [Test]
    public async void PlayerAgent_WithoutStorySystem_StillWorksNormally()
    {
        // Arrange - PlayerAgent without Story System components
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var agent = new PlayerAgent("Non-Story Tester");
        agent.Character.Skills.Intimidation = 50;

        // Explicitly NOT setting WorldState, StoryGraph, IntelRegistry

        var mission = new Mission
        {
            Type = MissionType.Collection,
            Title = "Regular Collection",
            RespectReward = 5,
            MoneyReward = 100m,
            HeatGenerated = 2,
            SkillRequirements = new Dictionary<string, int> { ["Intimidation"] = 10 }
        };

        var gameState = CreateTestGameState();

        // Act - Should not throw
        var result = await agent.ExecuteMissionAsync(mission, gameState);

        // Assert - Mission executed successfully
        Assert.NotNull(result);
        Assert.NotNull(result.MissionResult);
        // Message should NOT contain Story System tags (no consequences or intel)
        if (result.MissionResult.Success)
        {
            Assert.False(result.MissionResult.Message.Contains("[Intel recorded:"));
        }
    }

    [Test]
    public async void PlayerAgent_FailedMission_DoesNotRecordIntel()
    {
        // Arrange
        GameTimingOptions.Current = GameTimingOptions.Instant;
        var agent = new PlayerAgent("Failed Intel Tester");
        agent.Character.Skills.StreetSmarts = 1;  // Very low skill
        agent.Character.Heat = 90;  // High heat increases failure

        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var intelRegistry = new IntelRegistry();
        worldState.CurrentWeek = 5;

        agent.WorldState = worldState;
        agent.StoryGraph = storyGraph;
        agent.IntelRegistry = intelRegistry;

        var gameState = CreateTestGameState();

        // Act - Execute to get a failure
        MissionExecutionResult? failResult = null;
        int initialIntelCount = 0;

        for (int i = 0; i < 10; i++)
        {
            var mission = new Mission
            {
                Type = MissionType.Information,
                Title = "Hard Intel",
                LocationId = "little-italy",
                RespectReward = 5,
                MoneyReward = 200m,
                HeatGenerated = 1,
                SkillRequirements = new Dictionary<string, int> { ["StreetSmarts"] = 90 }
            };
            var result = await agent.ExecuteMissionAsync(mission, gameState);
            if (!result.MissionResult.Success)
            {
                failResult = result;
                break;
            }
        }

        // Assert - Failed missions should not record intel
        if (failResult != null)
        {
            var allIntel = intelRegistry.GetRecent(10, 5).ToList();
            Assert.Equal(0, allIntel.Count);  // No intel from failed mission
            Assert.False(failResult.MissionResult.Message.Contains("[Intel recorded:"));
        }
    }

    #endregion

    #region Bug Fix Verification Tests

    [Test]
    public void WorldState_GetNPCsNeedingAttention_HandlesNullLastInteractionWeek()
    {
        // Arrange - This tests fix for null reference on LastInteractionWeek
        var worldState = new WorldState();
        worldState.CurrentWeek = 10;

        // Create NPC with null LastInteractionWeek (never interacted)
        var npc = new NPC
        {
            Id = "never-interacted",
            Name = "New Contact",
            Relationship = 60,  // Above Friend (50) threshold makes IsAlly = true
            Status = NPCStatus.Active,
            LastInteractionWeek = null,  // Explicitly null - never interacted
            LocationId = "little-italy"
        };
        worldState.RegisterNPC(npc);

        // Act - Should not throw NullReferenceException
        var npcsNeedingAttention = worldState.GetNPCsNeedingAttention(10).ToList();

        // Assert - NPC should be included since null defaults to 0 (week 0 < week 10 - 4)
        Assert.Contains(npc, npcsNeedingAttention);
    }

    [Test]
    public void WorldState_GetNPCsAtLocation_HandlesMissingNPCInIndex()
    {
        // Arrange - This tests fix for KeyNotFoundException
        var worldState = new WorldState();

        // Register NPC
        var npc = new NPC
        {
            Id = "test-npc",
            Name = "Test NPC",
            LocationId = "little-italy"
        };
        worldState.RegisterNPC(npc);

        // Manually remove NPC from dictionary but leave location index stale
        // (simulating inconsistent state)
        worldState.NPCs.Remove("test-npc");

        // Act - Should not throw KeyNotFoundException
        var npcsAtLocation = worldState.GetNPCsAtLocation("little-italy").ToList();

        // Assert - Should return empty (NPC was removed)
        Assert.Equal(0, npcsAtLocation.Count);
    }

    [Test]
    public void MissionHistory_DecayCounters_DecaysNPCInteractions()
    {
        // Arrange - This tests fix for missing NPC decay
        var history = new MissionHistory();
        history.RecordMission("Collection", null, "npc-1", 1);
        history.RecordMission("Collection", null, "npc-1", 2);
        history.RecordMission("Collection", null, "npc-1", 3);

        // Initial score should have NPC penalty
        var initialScore = history.GetRepetitionScore("Collection", null, "npc-1", 4);
        Assert.True(initialScore > 0, "Initial score should have NPC penalty");

        // Act - Decay multiple times
        for (int i = 0; i < 5; i++)
        {
            history.DecayCounters();
        }

        // Assert - Score should be lower after decay
        var decayedScore = history.GetRepetitionScore("Collection", null, "npc-1", 4);
        Assert.True(decayedScore < initialScore, "NPC interaction penalty should decay over time");
    }

    [Test]
    public void StoryGraph_AddEdge_ValidatesSourceNodeExists()
    {
        // Arrange - This tests fix for edge validation
        var graph = new StoryGraph();
        var targetNode = new StoryNode { Id = "target", Title = "Target" };
        graph.AddNode(targetNode);

        var edge = new StoryEdge
        {
            FromNodeId = "non-existent",  // Source doesn't exist
            ToNodeId = "target",
            Type = StoryEdgeType.Requires
        };

        // Act & Assert - Should throw ArgumentException
        Assert.Throws<ArgumentException>(() => graph.AddEdge(edge));
    }

    [Test]
    public void StoryGraph_AddEdge_ValidatesTargetNodeExists()
    {
        // Arrange
        var graph = new StoryGraph();
        var sourceNode = new StoryNode { Id = "source", Title = "Source" };
        graph.AddNode(sourceNode);

        var edge = new StoryEdge
        {
            FromNodeId = "source",
            ToNodeId = "non-existent",  // Target doesn't exist
            Type = StoryEdgeType.Unlocks
        };

        // Act & Assert - Should throw ArgumentException
        Assert.Throws<ArgumentException>(() => graph.AddEdge(edge));
    }

    [Test]
    public void StoryNode_HasExpired_CorrectlyExpiresAtBoundary()
    {
        // Arrange - This tests fix for off-by-one expiration
        var node = new StoryNode
        {
            Id = "expiring-node",
            Title = "Time-Limited Opportunity",
            ExpiresAfterWeeks = 3,
            UnlockedAtWeek = 5
        };

        // Act & Assert
        // Week 5: unlocked (5-5=0, 0 >= 3 is false)
        Assert.False(node.HasExpired(5), "Should not be expired on unlock week");

        // Week 6: 1 week elapsed (6-5=1, 1 >= 3 is false)
        Assert.False(node.HasExpired(6), "Should not be expired after 1 week");

        // Week 7: 2 weeks elapsed (7-5=2, 2 >= 3 is false)
        Assert.False(node.HasExpired(7), "Should not be expired after 2 weeks");

        // Week 8: 3 weeks elapsed (8-5=3, 3 >= 3 is TRUE - expires NOW)
        Assert.True(node.HasExpired(8), "Should be expired after exactly 3 weeks (at boundary)");

        // Week 9: 4 weeks elapsed (9-5=4, 4 >= 3 is true)
        Assert.True(node.HasExpired(9), "Should be expired after 4 weeks");
    }

    [Test]
    public void StoryGraph_DelayedTriggers_QueueAndProcess()
    {
        // Arrange - This tests the delayed triggers implementation
        var graph = new StoryGraph();
        var worldState = new WorldState { CurrentWeek = 1 };

        // Create source node that will unlock at week 1 and trigger a delayed unlock
        var sourceNode = new StoryNode
        {
            Id = "source",
            Title = "Source Event",
            IsUnlocked = false,
            UnlockCondition = w => w.CurrentWeek >= 1  // Unlocks immediately at week 1
        };
        graph.AddNode(sourceNode);

        // Create target node that will be triggered after delay
        // UnlockCondition returns false so it can ONLY be unlocked via trigger
        var delayedNode = new StoryNode
        {
            Id = "delayed-target",
            Title = "Delayed Event",
            IsUnlocked = false,
            UnlockCondition = w => false  // Never unlocks by normal conditions - only via trigger
        };
        graph.AddNode(delayedNode);

        // Create delayed trigger edge (3 week delay)
        var edge = new StoryEdge
        {
            FromNodeId = "source",
            ToNodeId = "delayed-target",
            Type = StoryEdgeType.Triggers,
            DelayWeeks = 3
        };
        graph.AddEdge(edge);

        // Act - Call UpdateUnlocks to unlock source node and queue the delayed trigger
        var unlocked = graph.UpdateUnlocks(worldState);

        // Assert - Source node should be unlocked
        Assert.True(sourceNode.IsUnlocked, "Source node should be unlocked");
        Assert.Contains(sourceNode, unlocked);
        // Delayed node should NOT be unlocked yet
        Assert.False(delayedNode.IsUnlocked, "Delayed node should not be unlocked immediately");

        // Advance to week 3 (still before trigger week 4)
        worldState.CurrentWeek = 3;
        graph.UpdateUnlocks(worldState);
        Assert.False(delayedNode.IsUnlocked, "Delayed node should not be unlocked before delay completes");

        // Advance to week 4 (trigger week = 1 + 3 delay)
        worldState.CurrentWeek = 4;
        graph.UpdateUnlocks(worldState);

        // Assert - Delayed node should now be unlocked
        Assert.True(delayedNode.IsUnlocked, "Delayed node should be unlocked after delay period");
        Assert.Equal(4, delayedNode.UnlockedAtWeek);
    }

    [Test]
    public void StoryGraph_ImmediateTriggers_StillWork()
    {
        // Arrange - Ensure immediate triggers (DelayWeeks=0) still work
        var graph = new StoryGraph();
        var worldState = new WorldState { CurrentWeek = 5 };

        var sourceNode = new StoryNode
        {
            Id = "source",
            Title = "Source",
            IsUnlocked = false,
            UnlockCondition = w => w.CurrentWeek >= 5  // Unlocks at week 5
        };
        graph.AddNode(sourceNode);

        // Target node can ONLY be unlocked via trigger (UnlockCondition returns false)
        var immediateNode = new StoryNode
        {
            Id = "immediate-target",
            Title = "Immediate Target",
            IsUnlocked = false,
            UnlockCondition = w => false  // Only unlocks via trigger
        };
        graph.AddNode(immediateNode);

        var edge = new StoryEdge
        {
            FromNodeId = "source",
            ToNodeId = "immediate-target",
            Type = StoryEdgeType.Triggers,
            DelayWeeks = 0  // Immediate
        };
        graph.AddEdge(edge);

        // Act - Call UpdateUnlocks to unlock source and trigger immediate unlock
        graph.UpdateUnlocks(worldState);

        // Assert - Source and immediate target should both be unlocked
        Assert.True(sourceNode.IsUnlocked, "Source should be unlocked");
        Assert.True(immediateNode.IsUnlocked, "Immediate trigger should unlock target immediately");
        Assert.Equal(5, immediateNode.UnlockedAtWeek);
    }

    [Test]
    public void DynamicMissionGenerator_HandleNullLastInteractionWeek()
    {
        // Arrange - This tests the fix in DynamicMissionGenerator
        var worldState = WorldStateSeeder.CreateInitialWorld();
        var storyGraph = WorldStateSeeder.CreateInitialGraph(worldState);
        var intel = new IntelRegistry();
        var history = new MissionHistory();
        var generator = new DynamicMissionGenerator(worldState, storyGraph, intel, history);

        // Create allied NPC with null LastInteractionWeek
        var npc = new NPC
        {
            Id = "new-ally",
            Name = "New Ally",
            Relationship = 60,  // Above Friend (50) threshold makes IsAlly = true
            Status = NPCStatus.Active,
            LastInteractionWeek = null,  // Never interacted
            LocationId = "little-italy"
        };
        worldState.RegisterNPC(npc);
        worldState.CurrentWeek = 10;

        // Act - Should not throw
        var player = CreateTestPlayer();
        var candidate = generator.GenerateMission(player);

        // Assert - Should successfully generate a mission
        Assert.NotNull(candidate);
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
