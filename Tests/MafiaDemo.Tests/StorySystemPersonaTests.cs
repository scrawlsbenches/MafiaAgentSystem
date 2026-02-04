using AgentRouting.MafiaDemo.Story;
using TestRunner.Framework;

namespace MafiaDemo.Tests;

/// <summary>
/// Tests for Story System Persona and Goal classes.
/// </summary>
public class StorySystemPersonaTests : MafiaTestBase
{
    #region Persona Default Values

    [Test]
    public void Persona_DefaultValues_AreNeutral()
    {
        // Arrange & Act
        var persona = new Persona();

        // Assert - All traits default to 50 (neutral)
        Assert.Equal(50, persona.Ambition);
        Assert.Equal(50, persona.Caution);
        Assert.Equal(50, persona.Aggression);
        Assert.Equal(50, persona.Loyalty);
        Assert.Equal(50, persona.Trust);
        Assert.Equal(50, persona.Empathy);
        Assert.Equal(50, persona.Cunning);
        Assert.Equal(50, persona.Patience);
        Assert.Equal(50, persona.Pride);
        Assert.Equal(50, persona.Verbosity);
        Assert.Equal(50, persona.Honesty);
        Assert.Equal(CommunicationStyle.Neutral, persona.Style);
    }

    #endregion

    #region Computed Trait Properties

    [Test]
    public void Persona_ComputedTraits_AboveThreshold()
    {
        // Arrange - Traits above 70 (TraitHigh threshold)
        var persona = new Persona
        {
            Ambition = 75,
            Caution = 75,
            Aggression = 80,
            Loyalty = 75,
            Trust = 90,
            Cunning = 75,
            Patience = 100,
            Pride = 85
        };

        // Assert
        Assert.True(persona.IsAmbitious);
        Assert.True(persona.IsCautious);
        Assert.True(persona.IsAggressive);
        Assert.True(persona.IsLoyal);
        Assert.True(persona.IsTrusting);
        Assert.True(persona.IsCunning);
        Assert.True(persona.IsPatient);
        Assert.True(persona.IsProud);
    }

    [Test]
    public void Persona_ComputedTraits_BelowOrAtThreshold()
    {
        // Arrange - Traits at or below 70
        var persona = new Persona
        {
            Ambition = 70,
            Caution = 50,
            Aggression = 40,
            Loyalty = 0,
            Trust = 30,
            Cunning = 69,
            Patience = 20,
            Pride = 10
        };

        // Assert
        Assert.False(persona.IsAmbitious);
        Assert.False(persona.IsCautious);
        Assert.False(persona.IsAggressive);
        Assert.False(persona.IsLoyal);
        Assert.False(persona.IsTrusting);
        Assert.False(persona.IsCunning);
        Assert.False(persona.IsPatient);
        Assert.False(persona.IsProud);
    }

    #endregion

    #region GetReactionBias Tests

    [Test]
    public void GetReactionBias_Opportunity_DependsOnAmbitionMinusCaution()
    {
        // High ambition, low caution = positive reaction to opportunity
        var ambitious = new Persona { Ambition = 80, Caution = 20 };
        Assert.True(ambitious.GetReactionBias("opportunity") > 0);
        Assert.True(Math.Abs(ambitious.GetReactionBias("opportunity") - 0.6f) < 0.01f);

        // Low ambition, high caution = negative reaction to opportunity
        var cautious = new Persona { Ambition = 20, Caution = 80 };
        Assert.True(cautious.GetReactionBias("opportunity") < 0);
        Assert.True(Math.Abs(cautious.GetReactionBias("opportunity") - (-0.6f)) < 0.01f);

        // Balanced = neutral
        var balanced = new Persona { Ambition = 50, Caution = 50 };
        Assert.True(Math.Abs(balanced.GetReactionBias("opportunity")) < 0.01f);
    }

    [Test]
    public void GetReactionBias_Threat_DependsOnAggressionMinusPatience()
    {
        // High aggression, low patience = aggressive response to threat
        var aggressive = new Persona { Aggression = 90, Patience = 10 };
        Assert.True(aggressive.GetReactionBias("threat") > 0);
        Assert.True(Math.Abs(aggressive.GetReactionBias("threat") - 0.8f) < 0.01f);

        // Low aggression, high patience = patient response to threat
        var patient = new Persona { Aggression = 10, Patience = 90 };
        Assert.True(patient.GetReactionBias("threat") < 0);
        Assert.True(Math.Abs(patient.GetReactionBias("threat") - (-0.8f)) < 0.01f);
    }

    [Test]
    public void GetReactionBias_Betrayal_DependsOnPrideAndDistrust()
    {
        // High pride, low trust = strong reaction to betrayal
        var proud = new Persona { Pride = 100, Trust = 0 };
        Assert.True(proud.GetReactionBias("betrayal") > 0);
        Assert.True(Math.Abs(proud.GetReactionBias("betrayal") - 1.0f) < 0.01f);

        // Low pride, high trust = weak reaction to betrayal
        var trusting = new Persona { Pride = 0, Trust = 100 };
        Assert.True(trusting.GetReactionBias("betrayal") < 0.5f);
    }

    [Test]
    public void GetReactionBias_Alliance_DependsOnLoyaltyAndTrust()
    {
        // High loyalty and trust = positive alliance reaction
        var loyal = new Persona { Loyalty = 100, Trust = 100 };
        Assert.True(loyal.GetReactionBias("alliance") > 0.5f);
        Assert.True(Math.Abs(loyal.GetReactionBias("alliance") - 1.0f) < 0.01f);

        // Low loyalty and trust = negative alliance reaction
        var distrustful = new Persona { Loyalty = 0, Trust = 0 };
        Assert.True(Math.Abs(distrustful.GetReactionBias("alliance")) < 0.01f);
    }

    [Test]
    public void GetReactionBias_Negotiation_DependsOnCunningAndPatience()
    {
        // High cunning and patience = good negotiator
        var negotiator = new Persona { Cunning = 80, Patience = 80 };
        Assert.True(negotiator.GetReactionBias("negotiation") > 0.5f);

        // Low cunning and patience = poor negotiator
        var impulsive = new Persona { Cunning = 20, Patience = 20 };
        Assert.True(impulsive.GetReactionBias("negotiation") < 0.5f);
    }

    [Test]
    public void GetReactionBias_UnknownSituation_ReturnsZero()
    {
        // Arrange
        var persona = new Persona();

        // Act & Assert
        Assert.Equal(0f, persona.GetReactionBias("unknown"));
        Assert.Equal(0f, persona.GetReactionBias(""));
        Assert.Equal(0f, persona.GetReactionBias("random_situation"));
    }

    #endregion

    #region ApplyExperience Tests

    [Test]
    public void ApplyExperience_Betrayed_DecreasessTrustIncreasesCaution()
    {
        // Arrange
        var persona = new Persona { Trust = 50, Caution = 50 };

        // Act
        persona.ApplyExperience("betrayed", 20);

        // Assert
        Assert.Equal(30, persona.Trust);  // -20
        Assert.Equal(60, persona.Caution); // +10 (intensity / 2)
    }

    [Test]
    public void ApplyExperience_Betrayed_ClampsAtZero()
    {
        // Arrange
        var persona = new Persona { Trust = 10, Caution = 95 };

        // Act
        persona.ApplyExperience("betrayed", 30);

        // Assert
        Assert.Equal(0, persona.Trust);  // Clamped at 0
        Assert.Equal(100, persona.Caution); // Clamped at 100
    }

    [Test]
    public void ApplyExperience_Success_IncreasesAmbition()
    {
        // Arrange
        var persona = new Persona { Ambition = 50 };

        // Act
        persona.ApplyExperience("success", 30);

        // Assert
        Assert.Equal(60, persona.Ambition); // +10 (intensity / 3)
    }

    [Test]
    public void ApplyExperience_Failure_IncreasesCautionDecreasesPride()
    {
        // Arrange
        var persona = new Persona { Caution = 50, Pride = 50 };

        // Act
        persona.ApplyExperience("failure", 30);

        // Assert
        Assert.Equal(65, persona.Caution); // +15 (intensity / 2)
        Assert.Equal(40, persona.Pride);   // -10 (intensity / 3)
    }

    [Test]
    public void ApplyExperience_Helped_IncreasesTrustAndLoyalty()
    {
        // Arrange
        var persona = new Persona { Trust = 50, Loyalty = 50 };

        // Act
        persona.ApplyExperience("helped", 30);

        // Assert
        Assert.Equal(65, persona.Trust);   // +15 (intensity / 2)
        Assert.Equal(60, persona.Loyalty); // +10 (intensity / 3)
    }

    [Test]
    public void ApplyExperience_Threatened_IncreasesAggressionDecreasesTrust()
    {
        // Arrange
        var persona = new Persona { Aggression = 50, Trust = 50 };

        // Act
        persona.ApplyExperience("threatened", 40);

        // Assert
        Assert.Equal(70, persona.Aggression); // +20 (intensity / 2)
        Assert.Equal(30, persona.Trust);      // -20 (intensity / 2)
    }

    [Test]
    public void ApplyExperience_UnknownType_DoesNothing()
    {
        // Arrange
        var persona = new Persona
        {
            Ambition = 50, Caution = 50, Trust = 50,
            Loyalty = 50, Pride = 50, Aggression = 50
        };

        // Act
        persona.ApplyExperience("unknown", 100);

        // Assert - All values unchanged
        Assert.Equal(50, persona.Ambition);
        Assert.Equal(50, persona.Caution);
        Assert.Equal(50, persona.Trust);
        Assert.Equal(50, persona.Loyalty);
        Assert.Equal(50, persona.Pride);
        Assert.Equal(50, persona.Aggression);
    }

    #endregion

    #region Persona Collections

    [Test]
    public void Persona_Goals_CanBeAddedAndAccessed()
    {
        // Arrange
        var persona = new Persona();
        var goal = new Goal
        {
            Id = "goal-1",
            Description = "Become the boss",
            Type = GoalType.Power,
            Priority = 100
        };

        // Act
        persona.Goals.Add(goal);

        // Assert
        Assert.Equal(1, persona.Goals.Count);
        Assert.Equal("goal-1", persona.Goals[0].Id);
    }

    [Test]
    public void Persona_FactionBiases_CanBeSetAndAccessed()
    {
        // Arrange
        var persona = new Persona();

        // Act
        persona.FactionBiases["RivalFamily"] = -50;
        persona.FactionBiases["AlliedFamily"] = 30;

        // Assert
        Assert.Equal(-50, persona.FactionBiases["RivalFamily"]);
        Assert.Equal(30, persona.FactionBiases["AlliedFamily"]);
    }

    [Test]
    public void Persona_RoleBiases_CanBeSetAndAccessed()
    {
        // Arrange
        var persona = new Persona();

        // Act
        persona.RoleBiases["Police"] = -30;
        persona.RoleBiases["Merchant"] = 10;

        // Assert
        Assert.Equal(-30, persona.RoleBiases["Police"]);
        Assert.Equal(10, persona.RoleBiases["Merchant"]);
    }

    [Test]
    public void Persona_FearsAndValues_CanBeAdded()
    {
        // Arrange
        var persona = new Persona();

        // Act
        persona.Fears.Add("Betrayal");
        persona.Fears.Add("Poverty");
        persona.Values.Add("Family");
        persona.Values.Add("Honor");

        // Assert
        Assert.Equal(2, persona.Fears.Count);
        Assert.Equal(2, persona.Values.Count);
        Assert.Contains("Betrayal", persona.Fears);
        Assert.Contains("Family", persona.Values);
    }

    #endregion

    #region Goal Tests

    [Test]
    public void Goal_DefaultValues()
    {
        // Arrange & Act
        var goal = new Goal();

        // Assert
        Assert.Equal("", goal.Id);
        Assert.Equal("", goal.Description);
        Assert.Equal(50, goal.Priority);
        Assert.Null(goal.TargetId);
        Assert.False(goal.IsAchieved);
        Assert.False(goal.IsFailed);
    }

    [Test]
    public void Goal_CanSetAllProperties()
    {
        // Arrange & Act
        var goal = new Goal
        {
            Id = "goal-eliminate",
            Description = "Eliminate the rival boss",
            Type = GoalType.Revenge,
            Priority = 90,
            TargetId = "rival-boss",
            IsAchieved = false,
            IsFailed = false
        };

        // Assert
        Assert.Equal("goal-eliminate", goal.Id);
        Assert.Equal("Eliminate the rival boss", goal.Description);
        Assert.Equal(GoalType.Revenge, goal.Type);
        Assert.Equal(90, goal.Priority);
        Assert.Equal("rival-boss", goal.TargetId);
    }

    [Test]
    public void Goal_AchievedStatus()
    {
        // Arrange
        var goal = new Goal { Id = "goal-1" };

        // Act
        goal.IsAchieved = true;

        // Assert
        Assert.True(goal.IsAchieved);
        Assert.False(goal.IsFailed);
    }

    [Test]
    public void Goal_FailedStatus()
    {
        // Arrange
        var goal = new Goal { Id = "goal-1" };

        // Act
        goal.IsFailed = true;

        // Assert
        Assert.False(goal.IsAchieved);
        Assert.True(goal.IsFailed);
    }

    #endregion
}
