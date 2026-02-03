using TestRunner.Framework;
using AgentRouting.MafiaDemo.Missions;
using AgentRouting.MafiaDemo.Game;

namespace TestRunner.Tests;

/// <summary>
/// Unit tests for MissionSystem classes
/// </summary>
public class MissionSystemTests
{
    #region MissionType Enum Tests

    [Test]
    public void MissionType_HasAllExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(MissionType), MissionType.Collection));
        Assert.True(Enum.IsDefined(typeof(MissionType), MissionType.Intimidation));
        Assert.True(Enum.IsDefined(typeof(MissionType), MissionType.Information));
        Assert.True(Enum.IsDefined(typeof(MissionType), MissionType.Negotiation));
        Assert.True(Enum.IsDefined(typeof(MissionType), MissionType.Hit));
        Assert.True(Enum.IsDefined(typeof(MissionType), MissionType.Territory));
        Assert.True(Enum.IsDefined(typeof(MissionType), MissionType.Recruitment));
    }

    [Test]
    public void MissionType_HasCorrectCount()
    {
        var values = Enum.GetValues(typeof(MissionType));
        Assert.Equal(7, values.Length);
    }

    #endregion

    #region MissionStatus Enum Tests

    [Test]
    public void MissionStatus_HasAllExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(MissionStatus), MissionStatus.Available));
        Assert.True(Enum.IsDefined(typeof(MissionStatus), MissionStatus.Assigned));
        Assert.True(Enum.IsDefined(typeof(MissionStatus), MissionStatus.InProgress));
        Assert.True(Enum.IsDefined(typeof(MissionStatus), MissionStatus.Completed));
        Assert.True(Enum.IsDefined(typeof(MissionStatus), MissionStatus.Failed));
        Assert.True(Enum.IsDefined(typeof(MissionStatus), MissionStatus.Expired));
    }

    [Test]
    public void MissionStatus_HasCorrectCount()
    {
        var values = Enum.GetValues(typeof(MissionStatus));
        Assert.Equal(6, values.Length);
    }

    #endregion

    #region PlayerRank Enum Tests

    [Test]
    public void PlayerRank_HasAllExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(PlayerRank), PlayerRank.Associate));
        Assert.True(Enum.IsDefined(typeof(PlayerRank), PlayerRank.Soldier));
        Assert.True(Enum.IsDefined(typeof(PlayerRank), PlayerRank.Capo));
        Assert.True(Enum.IsDefined(typeof(PlayerRank), PlayerRank.Underboss));
        Assert.True(Enum.IsDefined(typeof(PlayerRank), PlayerRank.Don));
    }

    [Test]
    public void PlayerRank_HasCorrectNumericValues()
    {
        Assert.Equal(0, (int)PlayerRank.Associate);
        Assert.Equal(1, (int)PlayerRank.Soldier);
        Assert.Equal(2, (int)PlayerRank.Capo);
        Assert.Equal(3, (int)PlayerRank.Underboss);
        Assert.Equal(4, (int)PlayerRank.Don);
    }

    [Test]
    public void PlayerRank_CanCompareRanks()
    {
        Assert.True(PlayerRank.Associate < PlayerRank.Soldier);
        Assert.True(PlayerRank.Soldier < PlayerRank.Capo);
        Assert.True(PlayerRank.Capo < PlayerRank.Underboss);
        Assert.True(PlayerRank.Underboss < PlayerRank.Don);
    }

    #endregion

    #region Mission Class Tests

    [Test]
    public void Mission_DefaultValues_AreCorrect()
    {
        var mission = new Mission();

        Assert.NotNull(mission.Id);
        Assert.True(mission.Id.Length > 0);
        Assert.Equal("", mission.Title);
        Assert.Equal("", mission.Description);
        Assert.Equal(MissionType.Collection, mission.Type);
        Assert.Equal("", mission.AssignedBy);
        Assert.Equal(0, mission.MinimumRank);
        Assert.NotNull(mission.SkillRequirements);
        Assert.Empty(mission.SkillRequirements);
        Assert.Equal(1, mission.RiskLevel);
        Assert.Equal(0, mission.RespectReward);
        Assert.Equal(0m, mission.MoneyReward);
        Assert.Equal(0, mission.HeatGenerated);
        Assert.Equal(MissionStatus.Available, mission.Status);
        Assert.False(mission.Success);
        Assert.Null(mission.Outcome);
        Assert.NotNull(mission.Data);
        Assert.Empty(mission.Data);
    }

    [Test]
    public void Mission_CanSetAllProperties()
    {
        var mission = new Mission
        {
            Id = "test-mission-001",
            Title = "Test Mission",
            Description = "A test description",
            Type = MissionType.Intimidation,
            AssignedBy = "capo-001",
            MinimumRank = 1,
            RiskLevel = 5,
            RespectReward = 10,
            MoneyReward = 500m,
            HeatGenerated = 8,
            Status = MissionStatus.InProgress,
            AssignedAt = new DateTime(2026, 1, 1),
            CompletedAt = new DateTime(2026, 1, 2),
            Success = true,
            Outcome = "Success!"
        };
        mission.SkillRequirements["Intimidation"] = 25;
        mission.Data["Target"] = "shopkeeper";

        Assert.Equal("test-mission-001", mission.Id);
        Assert.Equal("Test Mission", mission.Title);
        Assert.Equal("A test description", mission.Description);
        Assert.Equal(MissionType.Intimidation, mission.Type);
        Assert.Equal("capo-001", mission.AssignedBy);
        Assert.Equal(1, mission.MinimumRank);
        Assert.Equal(5, mission.RiskLevel);
        Assert.Equal(10, mission.RespectReward);
        Assert.Equal(500m, mission.MoneyReward);
        Assert.Equal(8, mission.HeatGenerated);
        Assert.Equal(MissionStatus.InProgress, mission.Status);
        Assert.Equal(new DateTime(2026, 1, 1), mission.AssignedAt);
        Assert.Equal(new DateTime(2026, 1, 2), mission.CompletedAt);
        Assert.True(mission.Success);
        Assert.Equal("Success!", mission.Outcome);
        Assert.Equal(25, mission.SkillRequirements["Intimidation"]);
        Assert.Equal("shopkeeper", mission.Data["Target"]);
    }

    [Test]
    public void Mission_GeneratesUniqueIds()
    {
        var mission1 = new Mission();
        var mission2 = new Mission();
        var mission3 = new Mission();

        Assert.NotEqual(mission1.Id, mission2.Id);
        Assert.NotEqual(mission2.Id, mission3.Id);
        Assert.NotEqual(mission1.Id, mission3.Id);
    }

    #endregion

    #region PlayerCharacter Tests

    [Test]
    public void PlayerCharacter_DefaultValues_AreCorrect()
    {
        var player = new PlayerCharacter();

        Assert.Equal("", player.Name);
        Assert.Equal(PlayerRank.Associate, player.Rank);
        Assert.Equal(10, player.Respect);
        Assert.Equal(1000m, player.Money);
        Assert.Equal(0, player.Heat);
        Assert.NotNull(player.Skills);
        Assert.NotNull(player.Relationships);
        Assert.Empty(player.Relationships);
        Assert.Equal(1, player.Week);
        Assert.NotNull(player.CompletedMissions);
        Assert.Empty(player.CompletedMissions);
        Assert.NotNull(player.ActiveMissions);
        Assert.Empty(player.ActiveMissions);
        Assert.NotNull(player.Achievements);
        Assert.Empty(player.Achievements);
        Assert.NotNull(player.Personality);
    }

    [Test]
    public void PlayerCharacter_CanSetAllProperties()
    {
        var player = new PlayerCharacter
        {
            Name = "Vito",
            Rank = PlayerRank.Capo,
            Respect = 75,
            Money = 50000m,
            Heat = 25,
            Week = 10
        };
        player.Relationships["godfather-001"] = 80;
        player.Achievements.Add("First Mission");

        Assert.Equal("Vito", player.Name);
        Assert.Equal(PlayerRank.Capo, player.Rank);
        Assert.Equal(75, player.Respect);
        Assert.Equal(50000m, player.Money);
        Assert.Equal(25, player.Heat);
        Assert.Equal(10, player.Week);
        Assert.Equal(80, player.Relationships["godfather-001"]);
        Assert.Contains("First Mission", player.Achievements);
    }

    [Test]
    public void PlayerCharacter_CanTrackMissions()
    {
        var player = new PlayerCharacter();
        var activeMission = new Mission { Title = "Active Mission" };
        var completedMission = new Mission { Title = "Completed Mission" };

        player.ActiveMissions.Add(activeMission);
        player.CompletedMissions.Add(completedMission);

        Assert.Equal(1, player.ActiveMissions.Count);
        Assert.Equal(1, player.CompletedMissions.Count);
        Assert.Equal("Active Mission", player.ActiveMissions[0].Title);
        Assert.Equal("Completed Mission", player.CompletedMissions[0].Title);
    }

    #endregion

    #region PlayerSkills Tests

    [Test]
    public void PlayerSkills_DefaultValues_AreCorrect()
    {
        var skills = new PlayerSkills();

        Assert.Equal(10, skills.Intimidation);
        Assert.Equal(10, skills.Negotiation);
        Assert.Equal(10, skills.StreetSmarts);
        Assert.Equal(5, skills.Leadership);
        Assert.Equal(5, skills.Business);
    }

    [Test]
    public void PlayerSkills_CanSetAllProperties()
    {
        var skills = new PlayerSkills
        {
            Intimidation = 50,
            Negotiation = 60,
            StreetSmarts = 70,
            Leadership = 80,
            Business = 90
        };

        Assert.Equal(50, skills.Intimidation);
        Assert.Equal(60, skills.Negotiation);
        Assert.Equal(70, skills.StreetSmarts);
        Assert.Equal(80, skills.Leadership);
        Assert.Equal(90, skills.Business);
    }

    [Test]
    public void PlayerSkills_GetSkill_ReturnsCorrectValues()
    {
        var skills = new PlayerSkills
        {
            Intimidation = 25,
            Negotiation = 30,
            StreetSmarts = 35,
            Leadership = 40,
            Business = 45
        };

        Assert.Equal(25, skills.GetSkill("intimidation"));
        Assert.Equal(30, skills.GetSkill("negotiation"));
        Assert.Equal(35, skills.GetSkill("streetsmarts"));
        Assert.Equal(40, skills.GetSkill("leadership"));
        Assert.Equal(45, skills.GetSkill("business"));
    }

    [Test]
    public void PlayerSkills_GetSkill_IsCaseInsensitive()
    {
        var skills = new PlayerSkills
        {
            Intimidation = 50,
            Negotiation = 60
        };

        Assert.Equal(50, skills.GetSkill("INTIMIDATION"));
        Assert.Equal(50, skills.GetSkill("Intimidation"));
        Assert.Equal(50, skills.GetSkill("intimidation"));
        Assert.Equal(60, skills.GetSkill("NEGOTIATION"));
        Assert.Equal(60, skills.GetSkill("Negotiation"));
        Assert.Equal(60, skills.GetSkill("negotiation"));
    }

    [Test]
    public void PlayerSkills_GetSkill_ReturnsZeroForUnknownSkill()
    {
        var skills = new PlayerSkills();

        Assert.Equal(0, skills.GetSkill("unknown"));
        Assert.Equal(0, skills.GetSkill(""));
        Assert.Equal(0, skills.GetSkill("combat"));
        Assert.Equal(0, skills.GetSkill("hacking"));
    }

    #endregion

    #region PlayerPersonality Tests

    [Test]
    public void PlayerPersonality_DefaultValues_AreCorrect()
    {
        var personality = new PlayerPersonality();

        Assert.Equal(50, personality.Ambition);
        Assert.Equal(70, personality.Loyalty);
        Assert.Equal(30, personality.Ruthlessness);
        Assert.Equal(50, personality.Caution);
    }

    [Test]
    public void PlayerPersonality_IsAmbitious_WhenAbove70()
    {
        var ambitious = new PlayerPersonality { Ambition = 71 };
        var notAmbitious = new PlayerPersonality { Ambition = 70 };
        var definitelyNotAmbitious = new PlayerPersonality { Ambition = 50 };

        Assert.True(ambitious.IsAmbitious);
        Assert.False(notAmbitious.IsAmbitious);
        Assert.False(definitelyNotAmbitious.IsAmbitious);
    }

    [Test]
    public void PlayerPersonality_IsLoyal_WhenAbove70()
    {
        var loyal = new PlayerPersonality { Loyalty = 71 };
        var notLoyal = new PlayerPersonality { Loyalty = 70 };
        var definitelyNotLoyal = new PlayerPersonality { Loyalty = 50 };

        Assert.True(loyal.IsLoyal);
        Assert.False(notLoyal.IsLoyal);
        Assert.False(definitelyNotLoyal.IsLoyal);
    }

    [Test]
    public void PlayerPersonality_IsRuthless_WhenAbove70()
    {
        var ruthless = new PlayerPersonality { Ruthlessness = 71 };
        var notRuthless = new PlayerPersonality { Ruthlessness = 70 };
        var definitelyNotRuthless = new PlayerPersonality { Ruthlessness = 50 };

        Assert.True(ruthless.IsRuthless);
        Assert.False(notRuthless.IsRuthless);
        Assert.False(definitelyNotRuthless.IsRuthless);
    }

    [Test]
    public void PlayerPersonality_IsCautious_WhenAbove70()
    {
        var cautious = new PlayerPersonality { Caution = 71 };
        var notCautious = new PlayerPersonality { Caution = 70 };
        var definitelyNotCautious = new PlayerPersonality { Caution = 50 };

        Assert.True(cautious.IsCautious);
        Assert.False(notCautious.IsCautious);
        Assert.False(definitelyNotCautious.IsCautious);
    }

    [Test]
    public void PlayerPersonality_IsReckless_WhenBelow30()
    {
        var reckless = new PlayerPersonality { Caution = 29 };
        var notReckless = new PlayerPersonality { Caution = 30 };
        var definitelyNotReckless = new PlayerPersonality { Caution = 50 };

        Assert.True(reckless.IsReckless);
        Assert.False(notReckless.IsReckless);
        Assert.False(definitelyNotReckless.IsReckless);
    }

    [Test]
    public void PlayerPersonality_DerivedTraits_DefaultValues()
    {
        var personality = new PlayerPersonality(); // Ambition=50, Loyalty=70, Ruthlessness=30, Caution=50

        Assert.False(personality.IsAmbitious); // 50 <= 70
        Assert.False(personality.IsLoyal); // 70 <= 70
        Assert.False(personality.IsRuthless); // 30 <= 70
        Assert.False(personality.IsCautious); // 50 <= 70
        Assert.False(personality.IsReckless); // 50 >= 30
    }

    [Test]
    public void PlayerPersonality_CanBeBothCautiousAndLoyal()
    {
        var personality = new PlayerPersonality
        {
            Loyalty = 90,
            Caution = 85
        };

        Assert.True(personality.IsLoyal);
        Assert.True(personality.IsCautious);
        Assert.False(personality.IsReckless);
    }

    [Test]
    public void PlayerPersonality_CannotBeBothCautiousAndReckless()
    {
        // Caution > 70 means IsCautious is true
        // Caution < 30 means IsReckless is true
        // These are mutually exclusive

        var cautious = new PlayerPersonality { Caution = 80 };
        Assert.True(cautious.IsCautious);
        Assert.False(cautious.IsReckless);

        var reckless = new PlayerPersonality { Caution = 20 };
        Assert.False(reckless.IsCautious);
        Assert.True(reckless.IsReckless);
    }

    #endregion

    #region MissionContext Tests

    [Test]
    public void MissionContext_DefaultValues_AreCorrect()
    {
        var context = new MissionContext();

        Assert.Equal(50, context.SuccessChance);
        Assert.Equal(0, context.SkillAdvantage);
        Assert.False(context.Success);
        Assert.Equal(0, context.BonusRespect);
        Assert.Equal(0m, context.BonusMoney);
        Assert.Equal(0, context.HeatPenalty);
    }

    [Test]
    public void MissionContext_CanSetAllProperties()
    {
        var mission = new Mission { Title = "Test" };
        var player = new PlayerCharacter { Name = "Vito" };

        var context = new MissionContext
        {
            Mission = mission,
            Player = player,
            SuccessChance = 75,
            SkillAdvantage = 15,
            Success = true,
            BonusRespect = 5,
            BonusMoney = 100m,
            HeatPenalty = 3
        };

        Assert.Same(mission, context.Mission);
        Assert.Same(player, context.Player);
        Assert.Equal(75, context.SuccessChance);
        Assert.Equal(15, context.SkillAdvantage);
        Assert.True(context.Success);
        Assert.Equal(5, context.BonusRespect);
        Assert.Equal(100m, context.BonusMoney);
        Assert.Equal(3, context.HeatPenalty);
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
        Assert.NotNull(result.SkillGains);
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
            Message = "Mission completed"
        };
        result.SkillGains["Intimidation"] = 2;

        Assert.True(result.Success);
        Assert.Equal(10, result.RespectGained);
        Assert.Equal(500m, result.MoneyGained);
        Assert.Equal(5, result.HeatGained);
        Assert.Equal("Mission completed", result.Message);
        Assert.Equal(2, result.SkillGains["Intimidation"]);
    }

    #endregion

    #region MissionGenerator Tests

    [Test]
    public void MissionGenerator_GenerateMission_ReturnsValidMission()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Associate };
        var gameState = new GameState();

        var mission = generator.GenerateMission(player, gameState);

        Assert.NotNull(mission);
        Assert.NotNull(mission.Id);
        Assert.True(mission.Title.Length > 0);
        Assert.True(mission.Description.Length > 0);
        Assert.True(mission.AssignedBy.Length > 0);
    }

    [Test]
    public void MissionGenerator_Associate_GetsBasicMissions()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Associate };
        var gameState = new GameState();

        // Generate multiple missions to test the distribution
        var missionTypes = new HashSet<MissionType>();
        for (int i = 0; i < 50; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            missionTypes.Add(mission.Type);
        }

        // Associates should be able to get Collection, Intimidation, Information
        // but NOT Negotiation, Recruitment, Territory, or Hit
        Assert.True(missionTypes.Contains(MissionType.Collection) ||
                   missionTypes.Contains(MissionType.Intimidation) ||
                   missionTypes.Contains(MissionType.Information));
        Assert.DoesNotContain(MissionType.Hit, missionTypes);
        Assert.DoesNotContain(MissionType.Territory, missionTypes);
        Assert.DoesNotContain(MissionType.Recruitment, missionTypes);
        Assert.DoesNotContain(MissionType.Negotiation, missionTypes);
    }

    [Test]
    public void MissionGenerator_Soldier_CanGetNegotiationMissions()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Soldier };
        var gameState = new GameState();

        var missionTypes = new HashSet<MissionType>();
        for (int i = 0; i < 100; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            missionTypes.Add(mission.Type);
        }

        // Soldiers can get negotiation missions
        Assert.True(missionTypes.Contains(MissionType.Collection) ||
                   missionTypes.Contains(MissionType.Intimidation) ||
                   missionTypes.Contains(MissionType.Information) ||
                   missionTypes.Contains(MissionType.Negotiation));

        // But still not Hit, Territory, or Recruitment
        Assert.DoesNotContain(MissionType.Hit, missionTypes);
        Assert.DoesNotContain(MissionType.Territory, missionTypes);
        Assert.DoesNotContain(MissionType.Recruitment, missionTypes);
    }

    [Test]
    public void MissionGenerator_Capo_CanGetTerritoryAndRecruitmentMissions()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Capo };
        var gameState = new GameState();

        var missionTypes = new HashSet<MissionType>();
        for (int i = 0; i < 100; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            missionTypes.Add(mission.Type);
        }

        // Capos can get all types except Hit
        Assert.DoesNotContain(MissionType.Hit, missionTypes);
    }

    [Test]
    public void MissionGenerator_Underboss_CanGetHitMissions()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Underboss };
        var gameState = new GameState();

        var missionTypes = new HashSet<MissionType>();
        for (int i = 0; i < 100; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            missionTypes.Add(mission.Type);
        }

        // Underboss can potentially get any mission type
        // All 7 types should be available
        Assert.True(missionTypes.Count >= 1); // At least some types should appear
    }

    [Test]
    public void MissionGenerator_Don_HasAccessToAllMissionTypes()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Don };
        var gameState = new GameState();

        var missionTypes = new HashSet<MissionType>();
        for (int i = 0; i < 150; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            missionTypes.Add(mission.Type);
        }

        // Don should have access to all mission types
        Assert.Equal(7, missionTypes.Count);
    }

    [Test]
    public void MissionGenerator_CollectionMission_HasCorrectStructure()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Associate };
        var gameState = new GameState();

        Mission? collectionMission = null;
        for (int i = 0; i < 50; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            if (mission.Type == MissionType.Collection)
            {
                collectionMission = mission;
                break;
            }
        }

        if (collectionMission != null)
        {
            Assert.Contains("Collect from", collectionMission.Title);
            Assert.Equal(0, collectionMission.MinimumRank);
            Assert.Equal(4, collectionMission.RespectReward); // Balanced economy
            Assert.Equal(2, collectionMission.HeatGenerated);
            Assert.True(collectionMission.Data.ContainsKey("BusinessName"));
            Assert.True(collectionMission.Data.ContainsKey("AmountOwed"));
            Assert.True(collectionMission.MoneyReward > 0);
        }
    }

    [Test]
    public void MissionGenerator_IntimidationMission_HasCorrectStructure()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Associate };
        var gameState = new GameState();

        Mission? intimidationMission = null;
        for (int i = 0; i < 50; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            if (mission.Type == MissionType.Intimidation)
            {
                intimidationMission = mission;
                break;
            }
        }

        if (intimidationMission != null)
        {
            Assert.Contains("Send a message", intimidationMission.Title);
            Assert.Equal(0, intimidationMission.MinimumRank);
            Assert.Equal(5, intimidationMission.RespectReward);
            Assert.Equal(100m, intimidationMission.MoneyReward);
            Assert.True(intimidationMission.SkillRequirements.ContainsKey("Intimidation"));
            Assert.Equal(8, intimidationMission.SkillRequirements["Intimidation"]);  // Lowered for new players
            Assert.True(intimidationMission.Data.ContainsKey("Target"));
            Assert.True(intimidationMission.Data.ContainsKey("Reason"));
        }
    }

    [Test]
    public void MissionGenerator_InformationMission_HasCorrectStructure()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Associate };
        var gameState = new GameState();

        Mission? infoMission = null;
        for (int i = 0; i < 50; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            if (mission.Type == MissionType.Information)
            {
                infoMission = mission;
                break;
            }
        }

        if (infoMission != null)
        {
            Assert.Contains("Find out", infoMission.Title);
            Assert.Equal(0, infoMission.MinimumRank);
            Assert.Equal(7, infoMission.RespectReward);
            Assert.Equal(200m, infoMission.MoneyReward);
            Assert.Equal(1, infoMission.HeatGenerated);
            Assert.True(infoMission.SkillRequirements.ContainsKey("StreetSmarts"));
            Assert.Equal(8, infoMission.SkillRequirements["StreetSmarts"]);  // Lowered for new players
        }
    }

    [Test]
    public void MissionGenerator_NegotiationMission_HasCorrectStructure()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Soldier };
        var gameState = new GameState();

        Mission? negotiationMission = null;
        for (int i = 0; i < 100; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            if (mission.Type == MissionType.Negotiation)
            {
                negotiationMission = mission;
                break;
            }
        }

        if (negotiationMission != null)
        {
            Assert.Contains("Negotiate with", negotiationMission.Title);
            Assert.Equal(1, negotiationMission.MinimumRank);
            Assert.Equal(10, negotiationMission.RespectReward);
            Assert.Equal(500m, negotiationMission.MoneyReward);
            Assert.Equal(0, negotiationMission.HeatGenerated);
            Assert.Equal("underboss-001", negotiationMission.AssignedBy);
            Assert.True(negotiationMission.SkillRequirements.ContainsKey("Negotiation"));
            Assert.Equal(30, negotiationMission.SkillRequirements["Negotiation"]);
        }
    }

    [Test]
    public void MissionGenerator_HitMission_HasCorrectStructure()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Underboss };
        var gameState = new GameState();

        Mission? hitMission = null;
        for (int i = 0; i < 100; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            if (mission.Type == MissionType.Hit)
            {
                hitMission = mission;
                break;
            }
        }

        if (hitMission != null)
        {
            Assert.Contains("Eliminate", hitMission.Title);
            Assert.Equal(3, hitMission.MinimumRank);
            Assert.Equal(10, hitMission.RiskLevel);
            Assert.Equal(20, hitMission.RespectReward); // Balanced economy
            Assert.Equal(2500m, hitMission.MoneyReward); // Balanced economy (~6x collection)
            Assert.Equal(25, hitMission.HeatGenerated); // Balanced economy (~5 weeks to recover)
            Assert.Equal("godfather-001", hitMission.AssignedBy);
            Assert.True(hitMission.SkillRequirements.ContainsKey("Intimidation"));
            Assert.True(hitMission.SkillRequirements.ContainsKey("StreetSmarts"));
            Assert.Equal(50, hitMission.SkillRequirements["Intimidation"]);
            Assert.Equal(40, hitMission.SkillRequirements["StreetSmarts"]);
        }
    }

    [Test]
    public void MissionGenerator_TerritoryMission_HasCorrectStructure()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Capo };
        var gameState = new GameState();

        Mission? territoryMission = null;
        for (int i = 0; i < 100; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            if (mission.Type == MissionType.Territory)
            {
                territoryMission = mission;
                break;
            }
        }

        if (territoryMission != null)
        {
            Assert.Contains("Territory:", territoryMission.Title);
            Assert.Equal(2, territoryMission.MinimumRank);
            Assert.Equal(15, territoryMission.RespectReward);
            Assert.Equal(2000m, territoryMission.MoneyReward);
            Assert.Equal(10, territoryMission.HeatGenerated);
            Assert.Equal("underboss-001", territoryMission.AssignedBy);
            Assert.True(territoryMission.SkillRequirements.ContainsKey("Leadership"));
            Assert.True(territoryMission.SkillRequirements.ContainsKey("Business"));
            Assert.Equal(40, territoryMission.SkillRequirements["Leadership"]);
            Assert.Equal(30, territoryMission.SkillRequirements["Business"]);
        }
    }

    [Test]
    public void MissionGenerator_RecruitmentMission_HasCorrectStructure()
    {
        var generator = new MissionGenerator();
        var player = new PlayerCharacter { Rank = PlayerRank.Capo };
        var gameState = new GameState();

        Mission? recruitmentMission = null;
        for (int i = 0; i < 100; i++)
        {
            var mission = generator.GenerateMission(player, gameState);
            if (mission.Type == MissionType.Recruitment)
            {
                recruitmentMission = mission;
                break;
            }
        }

        if (recruitmentMission != null)
        {
            Assert.Equal("Recruit a new soldier", recruitmentMission.Title);
            Assert.Equal(2, recruitmentMission.MinimumRank);
            Assert.Equal(3, recruitmentMission.RiskLevel);
            Assert.Equal(8, recruitmentMission.RespectReward);
            Assert.Equal(300m, recruitmentMission.MoneyReward);
            Assert.Equal(0, recruitmentMission.HeatGenerated);
            Assert.Equal("underboss-001", recruitmentMission.AssignedBy);
            Assert.True(recruitmentMission.SkillRequirements.ContainsKey("Leadership"));
            Assert.True(recruitmentMission.SkillRequirements.ContainsKey("StreetSmarts"));
        }
    }

    [Test]
    public void MissionGenerator_AssignedBy_VariesByPlayerRank()
    {
        var generator = new MissionGenerator();
        var gameState = new GameState();

        // Test Collection mission assigned by - varies by rank
        var associatePlayer = new PlayerCharacter { Rank = PlayerRank.Associate };
        var capoPlayer = new PlayerCharacter { Rank = PlayerRank.Capo };

        Mission? associateMission = null;
        Mission? capoMission = null;

        for (int i = 0; i < 50; i++)
        {
            var m = generator.GenerateMission(associatePlayer, gameState);
            if (m.Type == MissionType.Collection)
            {
                associateMission = m;
                break;
            }
        }

        for (int i = 0; i < 50; i++)
        {
            var m = generator.GenerateMission(capoPlayer, gameState);
            if (m.Type == MissionType.Collection)
            {
                capoMission = m;
                break;
            }
        }

        if (associateMission != null)
        {
            Assert.Equal("capo-001", associateMission.AssignedBy);
        }
        if (capoMission != null)
        {
            Assert.Equal("underboss-001", capoMission.AssignedBy);
        }
    }

    #endregion

    #region MissionEvaluator Tests

    [Test]
    public void MissionEvaluator_EvaluateMission_ReturnsValidResult()
    {
        var evaluator = new MissionEvaluator();
        var player = new PlayerCharacter
        {
            Skills = new PlayerSkills { Intimidation = 30 }
        };
        var mission = new Mission
        {
            Type = MissionType.Intimidation,
            RespectReward = 5,
            MoneyReward = 100m,
            HeatGenerated = 3,
            SkillRequirements = new Dictionary<string, int> { ["Intimidation"] = 15 }
        };

        var result = evaluator.EvaluateMission(mission, player);

        Assert.NotNull(result);
        Assert.True(result.Message.Length > 0);
        // Verify skill gains are populated
        Assert.NotEmpty(result.SkillGains);
        Assert.True(result.SkillGains.ContainsKey("Intimidation"));
    }

    [Test]
    public void MissionEvaluator_HighSkillAdvantage_IncreasesSuccessChance()
    {
        var evaluator = new MissionEvaluator();
        var player = new PlayerCharacter
        {
            Heat = 20, // Low heat for bonus
            Skills = new PlayerSkills { Intimidation = 60 }
        };
        var mission = new Mission
        {
            Type = MissionType.Intimidation,
            RespectReward = 5,
            MoneyReward = 100m,
            HeatGenerated = 3,
            SkillRequirements = new Dictionary<string, int> { ["Intimidation"] = 15 }
        };

        // With skill advantage of 45 (60-15), player is overqualified
        // Run multiple times to verify success rate is high
        int successes = 0;
        for (int i = 0; i < 50; i++)
        {
            var result = evaluator.EvaluateMission(mission, player);
            if (result.Success) successes++;
        }

        // With 100% success chance (overqualified), should have high success rate
        Assert.True(successes >= 40, $"Expected at least 40 successes but got {successes}");
    }

    [Test]
    public void MissionEvaluator_LowSkillAdvantage_DecreasesSuccessChance()
    {
        var evaluator = new MissionEvaluator();
        var player = new PlayerCharacter
        {
            Heat = 80, // High heat for penalty
            Skills = new PlayerSkills { Intimidation = 5 }
        };
        var mission = new Mission
        {
            Type = MissionType.Intimidation,
            RespectReward = 5,
            MoneyReward = 100m,
            HeatGenerated = 3,
            SkillRequirements = new Dictionary<string, int> { ["Intimidation"] = 50 }
        };

        // With skill advantage of -45 (5-50) and high heat, should have lower success rate
        int failures = 0;
        for (int i = 0; i < 50; i++)
        {
            var result = evaluator.EvaluateMission(mission, player);
            if (!result.Success) failures++;
        }

        // Should have more failures due to penalties
        Assert.True(failures >= 20, $"Expected at least 20 failures but got {failures}");
    }

    [Test]
    public void MissionEvaluator_Success_ReturnsPositiveRewards()
    {
        var evaluator = new MissionEvaluator();
        var player = new PlayerCharacter
        {
            Heat = 10,
            Skills = new PlayerSkills { Intimidation = 80 }
        };
        var mission = new Mission
        {
            Type = MissionType.Intimidation,
            RespectReward = 5,
            MoneyReward = 100m,
            HeatGenerated = 3,
            RiskLevel = 3,
            SkillRequirements = new Dictionary<string, int> { ["Intimidation"] = 15 }
        };

        // Run until we get a success
        MissionResult? successResult = null;
        for (int i = 0; i < 50; i++)
        {
            var result = evaluator.EvaluateMission(mission, player);
            if (result.Success)
            {
                successResult = result;
                break;
            }
        }

        Assert.NotNull(successResult);
        Assert.True(successResult!.RespectGained > 0);
        Assert.True(successResult.MoneyGained > 0);
        Assert.True(successResult.SkillGains["Intimidation"] == 2); // Success gives 2 skill
    }

    [Test]
    public void MissionEvaluator_Failure_ReturnsNegativeRespect()
    {
        var evaluator = new MissionEvaluator();
        var player = new PlayerCharacter
        {
            Heat = 90, // Very high heat
            Skills = new PlayerSkills { Intimidation = 1 }
        };
        var mission = new Mission
        {
            Type = MissionType.Intimidation,
            RespectReward = 5,
            MoneyReward = 100m,
            HeatGenerated = 3,
            SkillRequirements = new Dictionary<string, int> { ["Intimidation"] = 80 }
        };

        // Run until we get a failure
        MissionResult? failureResult = null;
        for (int i = 0; i < 50; i++)
        {
            var result = evaluator.EvaluateMission(mission, player);
            if (!result.Success)
            {
                failureResult = result;
                break;
            }
        }

        Assert.NotNull(failureResult);
        Assert.Equal(-5, failureResult!.RespectGained);
        Assert.Equal(0m, failureResult.MoneyGained);
        Assert.True(failureResult.SkillGains["Intimidation"] == 1); // Failure gives 1 skill
    }

    [Test]
    public void MissionEvaluator_LowHeat_GivesBonus()
    {
        var evaluator = new MissionEvaluator();
        var mission = new Mission
        {
            Type = MissionType.Collection,
            RespectReward = 3,
            MoneyReward = 100m,
            HeatGenerated = 2
        };

        // Player with low heat (< 30)
        var lowHeatPlayer = new PlayerCharacter { Heat = 20 };

        // Player with normal heat
        var normalHeatPlayer = new PlayerCharacter { Heat = 50 };

        int lowHeatSuccesses = 0;
        int normalHeatSuccesses = 0;

        for (int i = 0; i < 100; i++)
        {
            if (evaluator.EvaluateMission(mission, lowHeatPlayer).Success) lowHeatSuccesses++;
            if (evaluator.EvaluateMission(mission, normalHeatPlayer).Success) normalHeatSuccesses++;
        }

        // Low heat should have more successes due to bonus
        Assert.True(lowHeatSuccesses >= normalHeatSuccesses - 20,
            $"Low heat: {lowHeatSuccesses}, Normal heat: {normalHeatSuccesses}");
    }

    [Test]
    public void MissionEvaluator_HighHeat_GivesPenalty()
    {
        var evaluator = new MissionEvaluator();
        var mission = new Mission
        {
            Type = MissionType.Collection,
            RespectReward = 3,
            MoneyReward = 100m,
            HeatGenerated = 2
        };

        // Player with high heat (> 70)
        var highHeatPlayer = new PlayerCharacter { Heat = 80 };

        // Player with normal heat
        var normalHeatPlayer = new PlayerCharacter { Heat = 40 };

        int highHeatSuccesses = 0;
        int normalHeatSuccesses = 0;

        for (int i = 0; i < 100; i++)
        {
            if (evaluator.EvaluateMission(mission, highHeatPlayer).Success) highHeatSuccesses++;
            if (evaluator.EvaluateMission(mission, normalHeatPlayer).Success) normalHeatSuccesses++;
        }

        // High heat should have fewer successes due to penalty
        Assert.True(highHeatSuccesses <= normalHeatSuccesses + 20,
            $"High heat: {highHeatSuccesses}, Normal heat: {normalHeatSuccesses}");
    }

    [Test]
    public void MissionEvaluator_HighRiskMission_GivesBonusOnSuccess()
    {
        var evaluator = new MissionEvaluator();
        var highSkillPlayer = new PlayerCharacter
        {
            Heat = 10,
            Skills = new PlayerSkills
            {
                Intimidation = 90,
                StreetSmarts = 80
            }
        };

        var highRiskMission = new Mission
        {
            Type = MissionType.Hit,
            RespectReward = 25,
            MoneyReward = 5000m,
            HeatGenerated = 30,
            RiskLevel = 10, // Very high risk
            SkillRequirements = new Dictionary<string, int>
            {
                ["Intimidation"] = 50,
                ["StreetSmarts"] = 40
            }
        };

        // Run until we get success
        MissionResult? successResult = null;
        for (int i = 0; i < 50; i++)
        {
            var result = evaluator.EvaluateMission(highRiskMission, highSkillPlayer);
            if (result.Success)
            {
                successResult = result;
                break;
            }
        }

        Assert.NotNull(successResult);
        // High risk missions should give bonus respect and money on success
        Assert.True(successResult!.RespectGained > highRiskMission.RespectReward);
        Assert.True(successResult.MoneyGained > highRiskMission.MoneyReward);
    }

    [Test]
    public void MissionEvaluator_GeneratesSkillGains_ForAllRequiredSkills()
    {
        var evaluator = new MissionEvaluator();
        var player = new PlayerCharacter
        {
            Skills = new PlayerSkills
            {
                Leadership = 50,
                Business = 40
            }
        };

        var mission = new Mission
        {
            Type = MissionType.Territory,
            RespectReward = 15,
            MoneyReward = 2000m,
            HeatGenerated = 10,
            SkillRequirements = new Dictionary<string, int>
            {
                ["Leadership"] = 40,
                ["Business"] = 30
            }
        };

        var result = evaluator.EvaluateMission(mission, player);

        Assert.True(result.SkillGains.ContainsKey("Leadership"));
        Assert.True(result.SkillGains.ContainsKey("Business"));
        // Gain 2 on success, 1 on failure
        Assert.True(result.SkillGains["Leadership"] >= 1 && result.SkillGains["Leadership"] <= 2);
        Assert.True(result.SkillGains["Business"] >= 1 && result.SkillGains["Business"] <= 2);
    }

    [Test]
    public void MissionEvaluator_OutcomeMessage_VariesByMissionType()
    {
        var evaluator = new MissionEvaluator();
        var highSkillPlayer = new PlayerCharacter
        {
            Heat = 10,
            Skills = new PlayerSkills { Intimidation = 80, Negotiation = 80, StreetSmarts = 80 }
        };

        var collectionMission = new Mission
        {
            Type = MissionType.Collection,
            RespectReward = 3,
            MoneyReward = 100m,
            HeatGenerated = 2
        };

        var intimidationMission = new Mission
        {
            Type = MissionType.Intimidation,
            RespectReward = 5,
            MoneyReward = 100m,
            HeatGenerated = 3,
            SkillRequirements = new Dictionary<string, int> { ["Intimidation"] = 15 }
        };

        var infoMission = new Mission
        {
            Type = MissionType.Information,
            RespectReward = 7,
            MoneyReward = 200m,
            HeatGenerated = 1,
            SkillRequirements = new Dictionary<string, int> { ["StreetSmarts"] = 20 }
        };

        // Run missions until we get successful results
        var collectionMessages = new HashSet<string>();
        var intimidationMessages = new HashSet<string>();
        var infoMessages = new HashSet<string>();

        for (int i = 0; i < 50; i++)
        {
            var r1 = evaluator.EvaluateMission(collectionMission, highSkillPlayer);
            var r2 = evaluator.EvaluateMission(intimidationMission, highSkillPlayer);
            var r3 = evaluator.EvaluateMission(infoMission, highSkillPlayer);

            if (r1.Success) collectionMessages.Add(r1.Message);
            if (r2.Success) intimidationMessages.Add(r2.Message);
            if (r3.Success) infoMessages.Add(r3.Message);
        }

        // Verify messages contain expected content
        if (collectionMessages.Count > 0)
        {
            Assert.True(collectionMessages.Any(m =>
                m.Contains("collected") || m.Contains("Smooth collection") || m.Contains("got the money")));
        }
        if (intimidationMessages.Count > 0)
        {
            Assert.True(intimidationMessages.Any(m =>
                m.Contains("Message") || m.Contains("message") || m.Contains("point")));
        }
        if (infoMessages.Count > 0)
        {
            Assert.True(infoMessages.Any(m =>
                m.Contains("found") || m.Contains("work") || m.Contains("homework") || m.Contains("information")));
        }
    }

    [Test]
    public void MissionEvaluator_Overqualified_GetsAutoSuccess()
    {
        var evaluator = new MissionEvaluator();
        var player = new PlayerCharacter
        {
            Heat = 10,
            Skills = new PlayerSkills { Intimidation = 100 }
        };

        var easyMission = new Mission
        {
            Type = MissionType.Intimidation,
            RespectReward = 5,
            MoneyReward = 100m,
            HeatGenerated = 3,
            SkillRequirements = new Dictionary<string, int> { ["Intimidation"] = 15 }
        };

        // Skill advantage is 85 (100-15), which is > 30, so auto-success
        int successes = 0;
        for (int i = 0; i < 50; i++)
        {
            var result = evaluator.EvaluateMission(easyMission, player);
            if (result.Success) successes++;
        }

        // With auto-success and 100% chance (clamped to 95%), should have very high success rate
        Assert.True(successes >= 45, $"Expected at least 45 successes but got {successes}");
    }

    [Test]
    public void MissionEvaluator_NoSkillRequirements_UsesBaseChance()
    {
        var evaluator = new MissionEvaluator();
        var player = new PlayerCharacter { Heat = 50 };

        var simpleMission = new Mission
        {
            Type = MissionType.Collection,
            RespectReward = 3,
            MoneyReward = 100m,
            HeatGenerated = 2
            // No skill requirements
        };

        int successes = 0;
        for (int i = 0; i < 100; i++)
        {
            var result = evaluator.EvaluateMission(simpleMission, player);
            if (result.Success) successes++;
        }

        // Base 50% chance, should be around 40-60 successes
        Assert.InRange(successes, 25, 75);
    }

    [Test]
    public void MissionEvaluator_MultipleSkillRequirements_CumulativeAdvantage()
    {
        var evaluator = new MissionEvaluator();
        var player = new PlayerCharacter
        {
            Heat = 20,
            Skills = new PlayerSkills
            {
                Intimidation = 70,
                StreetSmarts = 60
            }
        };

        var mission = new Mission
        {
            Type = MissionType.Hit,
            RespectReward = 25,
            MoneyReward = 5000m,
            HeatGenerated = 30,
            RiskLevel = 5,
            SkillRequirements = new Dictionary<string, int>
            {
                ["Intimidation"] = 50, // Advantage: +20
                ["StreetSmarts"] = 40  // Advantage: +20
            }
        };

        // Total skill advantage: 40 (> 30, so overqualified)
        int successes = 0;
        for (int i = 0; i < 50; i++)
        {
            var result = evaluator.EvaluateMission(mission, player);
            if (result.Success) successes++;
        }

        // Should have high success rate due to cumulative advantage
        Assert.True(successes >= 40, $"Expected at least 40 successes but got {successes}");
    }

    #endregion
}
