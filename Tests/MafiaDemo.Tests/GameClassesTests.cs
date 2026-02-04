using AgentRouting.MafiaDemo.Game;
using AgentRouting.MafiaDemo.Rules;
using AgentRouting.MafiaDemo.Story;
using TestRunner.Framework;

namespace MafiaDemo.Tests;

/// <summary>
/// Tests for MafiaDemo Game classes to improve line coverage.
/// </summary>
public class GameClassesTests : MafiaTestBase
{
    #region AgentGoal Tests

    [Test]
    public void AgentGoal_DefaultValues()
    {
        // Arrange & Act
        var goal = new AgentGoal();

        // Assert
        Assert.Equal("", goal.GoalType);
        Assert.Equal(0, goal.Priority);
        Assert.False(goal.Completed);
    }

    [Test]
    public void AgentGoal_CanSetProperties()
    {
        // Arrange & Act
        var goal = new AgentGoal
        {
            GoalType = "EarnMoney",
            Priority = 100,
            Completed = true
        };

        // Assert
        Assert.Equal("EarnMoney", goal.GoalType);
        Assert.Equal(100, goal.Priority);
        Assert.True(goal.Completed);
    }

    #endregion

    #region PlotMissionResult Tests

    [Test]
    public void PlotMissionResult_DefaultValues()
    {
        // Arrange & Act
        var result = new PlotMissionResult();

        // Assert
        Assert.Equal("", result.NodeId);
        Assert.False(result.Success);
        Assert.Null(result.PlotThreadId);
        Assert.Null(result.PlotTitle);
        Assert.False(result.PlotCompleted);
        Assert.False(result.PlotFailed);
        Assert.Equal(0, result.RespectGained);
        Assert.Equal(0m, result.MoneyGained);
    }

    [Test]
    public void PlotMissionResult_CanSetProperties()
    {
        // Arrange & Act
        var result = new PlotMissionResult
        {
            NodeId = "node-1",
            Success = true,
            PlotThreadId = "plot-1",
            PlotTitle = "The Heist",
            PlotCompleted = true,
            PlotFailed = false,
            RespectGained = 10,
            MoneyGained = 50000m
        };

        // Assert
        Assert.Equal("node-1", result.NodeId);
        Assert.True(result.Success);
        Assert.Equal("plot-1", result.PlotThreadId);
        Assert.Equal("The Heist", result.PlotTitle);
        Assert.True(result.PlotCompleted);
        Assert.False(result.PlotFailed);
        Assert.Equal(10, result.RespectGained);
        Assert.Equal(50000m, result.MoneyGained);
    }

    #endregion

    #region RuleMetricsSummary Tests

    [Test]
    public void RuleMetricsSummary_DefaultValues()
    {
        // Arrange & Act
        var metrics = new RuleMetricsSummary();

        // Assert
        Assert.Equal(0, metrics.ExecutionCount);
        Assert.Equal(0.0, metrics.TotalExecutionTimeMs);
        Assert.Equal(0.0, metrics.AverageExecutionTimeMs);
    }

    [Test]
    public void RuleMetricsSummary_CanSetProperties()
    {
        // Arrange & Act
        var metrics = new RuleMetricsSummary
        {
            ExecutionCount = 100,
            TotalExecutionTimeMs = 500.0,
            AverageExecutionTimeMs = 5.0
        };

        // Assert
        Assert.Equal(100, metrics.ExecutionCount);
        Assert.True(Math.Abs(metrics.TotalExecutionTimeMs - 500.0) < 0.01);
        Assert.True(Math.Abs(metrics.AverageExecutionTimeMs - 5.0) < 0.01);
    }

    #endregion

    #region WorldState Additional Tests

    [Test]
    public void WorldState_Locations_CanAddAndAccess()
    {
        // Arrange
        var world = new WorldState();

        // Act
        world.Locations["warehouse"] = new Location
        {
            Id = "warehouse",
            Name = "Old Warehouse",
            State = LocationState.Neutral
        };

        // Assert
        Assert.True(world.Locations.ContainsKey("warehouse"));
        Assert.Equal("Old Warehouse", world.Locations["warehouse"].Name);
    }

    [Test]
    public void WorldState_CurrentWeek_CanBeSet()
    {
        // Arrange
        var world = new WorldState();

        // Act
        world.CurrentWeek = 10;

        // Assert
        Assert.Equal(10, world.CurrentWeek);
    }

    #endregion

    #region IntelRegistry Additional Tests

    [Test]
    public void IntelRegistry_Add_StoresIntel()
    {
        // Arrange
        var registry = new IntelRegistry();
        var intel = new Intel
        {
            Id = "intel-1",
            Type = IntelType.NPCLocation,
            SubjectId = "target-1",
            Summary = "Target spotted at docks",
            GatheredWeek = 5
        };

        // Act
        registry.Add(intel);

        // Assert
        var retrieved = registry.GetForSubject("target-1", 5).ToList();
        Assert.Equal(1, retrieved.Count);
        Assert.Equal("Target spotted at docks", retrieved[0].Summary);
    }

    [Test]
    public void IntelRegistry_GetByType_FiltersCorrectly()
    {
        // Arrange
        var registry = new IntelRegistry();
        registry.Add(new Intel { Type = IntelType.NPCLocation, SubjectId = "s1", GatheredWeek = 1 });
        registry.Add(new Intel { Type = IntelType.LocationHeat, SubjectId = "s2", GatheredWeek = 1 });
        registry.Add(new Intel { Type = IntelType.NPCLocation, SubjectId = "s3", GatheredWeek = 1 });

        // Act
        var npcLocations = registry.GetByType(IntelType.NPCLocation, 1).ToList();

        // Assert
        Assert.Equal(2, npcLocations.Count);
    }

    [Test]
    public void IntelRegistry_GetRecent_FiltersOldIntel()
    {
        // Arrange
        var registry = new IntelRegistry();
        registry.Add(new Intel { SubjectId = "s1", GatheredWeek = 1 });
        registry.Add(new Intel { SubjectId = "s2", GatheredWeek = 5 });
        registry.Add(new Intel { SubjectId = "s3", GatheredWeek = 10 });

        // Act - Get intel from last 2 weeks at week 10
        var recent = registry.GetRecent(2, 10).ToList();

        // Assert
        Assert.Equal(1, recent.Count); // Only week 10 intel
    }

    [Test]
    public void IntelRegistry_GetReliable_FiltersUnreliable()
    {
        // Arrange
        var registry = new IntelRegistry();
        registry.Add(new Intel { SubjectId = "s1", Reliability = 90, GatheredWeek = 1 });
        registry.Add(new Intel { SubjectId = "s2", Reliability = 30, GatheredWeek = 1 });

        // Act
        var reliable = registry.GetReliable(1).ToList();

        // Assert
        Assert.Equal(1, reliable.Count);
        Assert.Equal("s1", reliable[0].SubjectId);
    }

    [Test]
    public void IntelRegistry_GetMostRecentForSubject_ReturnsLatest()
    {
        // Arrange
        var registry = new IntelRegistry();
        registry.Add(new Intel { SubjectId = "target", Type = IntelType.NPCStatus, Summary = "Old", GatheredWeek = 1 });
        registry.Add(new Intel { SubjectId = "target", Type = IntelType.NPCStatus, Summary = "New", GatheredWeek = 5 });

        // Act
        var mostRecent = registry.GetMostRecentForSubject("target", IntelType.NPCStatus, 10);

        // Assert
        Assert.NotNull(mostRecent);
        Assert.Equal("New", mostRecent.Summary);
    }

    [Test]
    public void IntelRegistry_PruneExpired_RemovesOldIntel()
    {
        // Arrange
        var registry = new IntelRegistry();
        registry.Add(new Intel { SubjectId = "s1", GatheredWeek = 1, ExpiresWeek = 5 });
        registry.Add(new Intel { SubjectId = "s2", GatheredWeek = 10 });

        // Act - Prune at week 10 (intel from week 1 with expiry week 5 is expired)
        registry.PruneExpired(10);

        // Assert
        var all = registry.GetForSubject("s1", 10).ToList();
        Assert.Equal(0, all.Count); // s1 was pruned
    }

    [Test]
    public void IntelRegistry_GetUnprocessed_FiltersProcessed()
    {
        // Arrange
        var registry = new IntelRegistry();
        registry.Add(new Intel { SubjectId = "s1", IsProcessed = false, GatheredWeek = 1 });
        registry.Add(new Intel { SubjectId = "s2", IsProcessed = true, GatheredWeek = 1 });
        registry.Add(new Intel { SubjectId = "s3", IsProcessed = false, GatheredWeek = 1 });

        // Act
        var unprocessed = registry.GetUnprocessed().ToList();

        // Assert
        Assert.Equal(2, unprocessed.Count);
    }

    #endregion

    #region AssociateAgent Additional Tests

    [Test]
    public void DecisionType_EnumValues_AreCorrect()
    {
        // Test enum values exist
        Assert.Equal(0, (int)DecisionType.Wait);
        Assert.Equal(1, (int)DecisionType.SendMessage);
        Assert.Equal(2, (int)DecisionType.CollectMoney);
        Assert.Equal(3, (int)DecisionType.RecruitSoldier);
        Assert.Equal(4, (int)DecisionType.ExpandTerritory);
        Assert.Equal(5, (int)DecisionType.OrderHit);
    }

    [Test]
    public void AgentPersonality_DefaultValues()
    {
        // Arrange & Act
        var personality = new AgentPersonality();

        // Assert
        Assert.Equal(50, personality.Aggression);
        Assert.Equal(50, personality.Greed);
        Assert.Equal(80, personality.Loyalty);
        Assert.Equal(50, personality.Ambition);
    }

    [Test]
    public void AgentPersonality_CanSetProperties()
    {
        // Arrange & Act
        var personality = new AgentPersonality
        {
            Aggression = 90,
            Greed = 70,
            Loyalty = 95,
            Ambition = 80
        };

        // Assert
        Assert.Equal(90, personality.Aggression);
        Assert.Equal(70, personality.Greed);
        Assert.Equal(95, personality.Loyalty);
        Assert.Equal(80, personality.Ambition);
    }

    #endregion

    #region PlotMissionInfo Tests

    [Test]
    public void PlotMissionInfo_DefaultValues()
    {
        // Arrange & Act
        var info = new PlotMissionInfo();

        // Assert
        Assert.Equal("", info.NodeId);
        Assert.Equal("", info.PlotThreadId);
        Assert.Equal("", info.PlotTitle);
        Assert.Equal("", info.MissionTitle);
        Assert.Equal("", info.MissionDescription);
        Assert.Equal("", info.MissionType);
        Assert.Null(info.LocationId);
        Assert.False(info.IsFirstMission);
        Assert.Equal(0f, info.PlotProgress);
    }

    [Test]
    public void PlotMissionInfo_CanSetProperties()
    {
        // Arrange & Act
        var info = new PlotMissionInfo
        {
            NodeId = "node-1",
            PlotThreadId = "plot-1",
            PlotTitle = "The Bank Job",
            MissionTitle = "Case the Bank",
            MissionDescription = "Scout the bank for weaknesses",
            MissionType = "Reconnaissance",
            LocationId = "downtown-bank",
            IsFirstMission = true,
            PlotProgress = 0.5f
        };

        // Assert
        Assert.Equal("node-1", info.NodeId);
        Assert.Equal("plot-1", info.PlotThreadId);
        Assert.Equal("The Bank Job", info.PlotTitle);
        Assert.Equal("Case the Bank", info.MissionTitle);
        Assert.Equal("Scout the bank for weaknesses", info.MissionDescription);
        Assert.Equal("Reconnaissance", info.MissionType);
        Assert.Equal("downtown-bank", info.LocationId);
        Assert.True(info.IsFirstMission);
        Assert.True(Math.Abs(info.PlotProgress - 0.5f) < 0.01f);
    }

    #endregion

    #region GamePhase Enum Tests

    [Test]
    public void GamePhase_EnumValues_AreCorrect()
    {
        Assert.Equal(0, (int)GamePhase.Survival);
        Assert.Equal(1, (int)GamePhase.Accumulation);
        Assert.Equal(2, (int)GamePhase.Growth);
        Assert.Equal(3, (int)GamePhase.Dominance);
    }

    #endregion
}
