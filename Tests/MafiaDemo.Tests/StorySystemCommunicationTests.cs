using AgentRouting.MafiaDemo.Story;
using TestRunner.Framework;

namespace MafiaDemo.Tests;

/// <summary>
/// Tests for Story System communication classes: AgentQuestion, AgentResponse,
/// ConversationContext, and ResponseDecision.
/// </summary>
public class StorySystemCommunicationTests : MafiaTestBase
{
    #region AgentQuestion Tests

    [Test]
    public void AgentQuestion_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var question = new AgentQuestion();

        // Assert
        Assert.False(string.IsNullOrEmpty(question.Id), "Id should be auto-generated");
        Assert.Equal("", question.AskerId);
        Assert.Equal("", question.ResponderId);
        Assert.Equal("", question.Topic);
        Assert.Null(question.SubjectEntityId);
        Assert.Null(question.SubjectLocationId);
        Assert.NotNull(question.Context);
        Assert.Equal(0, question.AskedWeek);
        Assert.Equal(QuestionUrgency.Normal, question.Urgency);
        Assert.False(question.RequiresHonesty);
    }

    [Test]
    public void AgentQuestion_CanSetAllProperties()
    {
        // Arrange & Act
        var question = new AgentQuestion
        {
            Id = "custom-id",
            AskerId = "player",
            ResponderId = "npc-1",
            Type = QuestionType.WhereIs,
            Topic = "rival location",
            SubjectEntityId = "entity-1",
            SubjectLocationId = "location-1",
            AskedWeek = 5,
            Urgency = QuestionUrgency.Critical,
            RequiresHonesty = true
        };
        question.Context["extra"] = "value";

        // Assert
        Assert.Equal("custom-id", question.Id);
        Assert.Equal("player", question.AskerId);
        Assert.Equal("npc-1", question.ResponderId);
        Assert.Equal(QuestionType.WhereIs, question.Type);
        Assert.Equal("rival location", question.Topic);
        Assert.Equal("entity-1", question.SubjectEntityId);
        Assert.Equal("location-1", question.SubjectLocationId);
        Assert.Equal(5, question.AskedWeek);
        Assert.Equal(QuestionUrgency.Critical, question.Urgency);
        Assert.True(question.RequiresHonesty);
        Assert.Equal("value", question.Context["extra"]);
    }

    #endregion

    #region AgentResponse Tests

    [Test]
    public void AgentResponse_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var response = new AgentResponse();

        // Assert
        Assert.Equal("", response.QuestionId);
        Assert.Equal("", response.ResponderId);
        Assert.Equal("", response.Content);
        Assert.True(response.IsHonest);
        Assert.Equal(100, response.ConfidenceLevel);
        Assert.NotNull(response.SharedMemories);
        Assert.Null(response.SharedIntel);
        Assert.Equal(0, response.RelationshipChange);
        Assert.False(response.RefusedToAnswer);
        Assert.Null(response.RefusalReason);
        Assert.NotNull(response.UnlockedNodeIds);
        Assert.NotNull(response.TriggeredEvents);
    }

    [Test]
    public void AgentResponse_CanSetAllProperties()
    {
        // Arrange & Act
        var memory = new Memory { Summary = "Test memory" };
        var intel = new Intel { Summary = "Test intel" };

        var response = new AgentResponse
        {
            QuestionId = "q-1",
            ResponderId = "npc-1",
            Type = ResponseType.Answer,
            Content = "The target is at the docks.",
            IsHonest = false,
            ConfidenceLevel = 75,
            RelationshipChange = -10,
            RefusedToAnswer = false
        };
        response.SharedMemories.Add(memory);
        response.SharedIntel = intel;
        response.UnlockedNodeIds.Add("node-1");
        response.TriggeredEvents.Add("event-1");

        // Assert
        Assert.Equal("q-1", response.QuestionId);
        Assert.Equal("npc-1", response.ResponderId);
        Assert.Equal(ResponseType.Answer, response.Type);
        Assert.Equal("The target is at the docks.", response.Content);
        Assert.False(response.IsHonest);
        Assert.Equal(75, response.ConfidenceLevel);
        Assert.Equal(-10, response.RelationshipChange);
        Assert.Equal(1, response.SharedMemories.Count);
        Assert.Same(intel, response.SharedIntel);
        Assert.Contains("node-1", response.UnlockedNodeIds);
        Assert.Contains("event-1", response.TriggeredEvents);
    }

    [Test]
    public void AgentResponse_RefusedResponse()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            RefusedToAnswer = true,
            RefusalReason = "I don't trust you"
        };

        // Assert
        Assert.True(response.RefusedToAnswer);
        Assert.Equal("I don't trust you", response.RefusalReason);
    }

    #endregion

    #region ResponseDecision Tests

    [Test]
    public void ResponseDecision_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var decision = new ResponseDecision();

        // Assert
        Assert.True(decision.WillAnswer);
        Assert.False(decision.WillLie);
        Assert.False(decision.WillBargain);
        Assert.Null(decision.RefusalReason);
        Assert.Null(decision.LieReason);
        Assert.Equal(0, decision.RelationshipModifier);
        Assert.Null(decision.MatchedRule);
        Assert.False(decision.ForcedResponseType.HasValue);
        Assert.Null(decision.CustomResponse);
    }

    [Test]
    public void ResponseDecision_RefusalScenario()
    {
        // Arrange & Act
        var decision = new ResponseDecision
        {
            WillAnswer = false,
            RefusalReason = "Sworn to secrecy",
            MatchedRule = "LOYAL_PROTECTION"
        };

        // Assert
        Assert.False(decision.WillAnswer);
        Assert.Equal("Sworn to secrecy", decision.RefusalReason);
        Assert.Equal("LOYAL_PROTECTION", decision.MatchedRule);
    }

    [Test]
    public void ResponseDecision_LyingScenario()
    {
        // Arrange & Act
        var decision = new ResponseDecision
        {
            WillAnswer = true,
            WillLie = true,
            LieReason = "Protecting an ally",
            RelationshipModifier = -5
        };

        // Assert
        Assert.True(decision.WillAnswer);
        Assert.True(decision.WillLie);
        Assert.Equal("Protecting an ally", decision.LieReason);
        Assert.Equal(-5, decision.RelationshipModifier);
    }

    [Test]
    public void ResponseDecision_BargainingScenario()
    {
        // Arrange & Act
        var decision = new ResponseDecision
        {
            WillAnswer = true,
            WillBargain = true,
            ForcedResponseType = ResponseType.Bargain,
            CustomResponse = "What's in it for me?"
        };

        // Assert
        Assert.True(decision.WillBargain);
        Assert.Equal(ResponseType.Bargain, decision.ForcedResponseType);
        Assert.Equal("What's in it for me?", decision.CustomResponse);
    }

    #endregion

    #region ConversationContext Tests

    [Test]
    public void ConversationContext_DelegatesQuestionProperties()
    {
        // Arrange
        var question = new AgentQuestion
        {
            Type = QuestionType.WhatDoYouKnow,
            Urgency = QuestionUrgency.Critical,
            RequiresHonesty = true
        };

        // Act
        var context = new ConversationContext { Question = question };

        // Assert
        Assert.Equal(QuestionType.WhatDoYouKnow, context.QuestionType);
        Assert.Equal(QuestionUrgency.Critical, context.Urgency);
        Assert.True(context.IsSensitive);
    }

    [Test]
    public void ConversationContext_DelegatesPersonaProperties()
    {
        // Arrange
        var persona = new Persona
        {
            Pride = 80,
            Caution = 75,
            Loyalty = 90,
            Cunning = 75,
            Trust = 30,
            Aggression = 70,
            Honesty = 55
        };

        // Act
        var context = new ConversationContext
        {
            Question = new AgentQuestion(),
            Persona = persona
        };

        // Assert
        Assert.True(context.IsProud);      // Pride > 70
        Assert.True(context.IsCautious);   // Caution > 70
        Assert.True(context.IsLoyal);      // Loyalty > 70
        Assert.True(context.IsCunning);    // Cunning > 70
        Assert.False(context.IsTrusting);  // Trust <= 70
        Assert.False(context.IsAggressive); // Aggression <= 70
        Assert.Equal(55, context.Honesty);
    }

    [Test]
    public void ConversationContext_RelationshipCategories()
    {
        // Arrange
        var question = new AgentQuestion();
        var persona = new Persona();

        // Test enemy (relationship < -30)
        var enemyContext = new ConversationContext
        {
            Question = question,
            Persona = persona,
            RelationshipWithAsker = -50
        };
        Assert.True(enemyContext.IsEnemy);
        Assert.False(enemyContext.IsStranger);
        Assert.False(enemyContext.IsFriend);
        Assert.False(enemyContext.IsCloseFriend);

        // Test stranger (relationship between -30 and 20)
        var strangerContext = new ConversationContext
        {
            Question = question,
            Persona = persona,
            RelationshipWithAsker = 0
        };
        Assert.False(strangerContext.IsEnemy);
        Assert.True(strangerContext.IsStranger);
        Assert.False(strangerContext.IsFriend);
        Assert.False(strangerContext.IsCloseFriend);

        // Test friend (relationship > 20)
        var friendContext = new ConversationContext
        {
            Question = question,
            Persona = persona,
            RelationshipWithAsker = 50
        };
        Assert.False(friendContext.IsEnemy);
        Assert.False(friendContext.IsStranger);
        Assert.True(friendContext.IsFriend);
        Assert.False(friendContext.IsCloseFriend);

        // Test close friend (relationship > 70)
        var closeFriendContext = new ConversationContext
        {
            Question = question,
            Persona = persona,
            RelationshipWithAsker = 80
        };
        Assert.False(closeFriendContext.IsEnemy);
        Assert.False(closeFriendContext.IsStranger);
        Assert.True(closeFriendContext.IsFriend);
        Assert.True(closeFriendContext.IsCloseFriend);
    }

    [Test]
    public void ConversationContext_MemoryState()
    {
        // Arrange
        var context = new ConversationContext
        {
            Question = new AgentQuestion(),
            Persona = new Persona(),
            Memories = new MemoryBank()
        };

        // No memories
        Assert.False(context.HasRelevantMemories);
        Assert.Equal(0, context.BestMemoryConfidence);

        // Add relevant memories
        context.RelevantMemories.Add(new Memory { Confidence = 80, Type = MemoryType.Fact });
        context.RelevantMemories.Add(new Memory { Confidence = 90, Type = MemoryType.Secret });

        Assert.True(context.HasRelevantMemories);
        Assert.True(context.HasSecretMemories);
        Assert.Equal(90, context.BestMemoryConfidence);
    }

    [Test]
    public void ConversationContext_SubjectProtection()
    {
        // Arrange
        var question = new AgentQuestion { SubjectEntityId = "ally-1" };

        // Not protecting (low loyalty)
        var lowLoyaltyContext = new ConversationContext
        {
            Question = question,
            Persona = new Persona(),
            LoyaltyToSubject = 30,
            RelationshipWithAsker = 0
        };
        Assert.Equal("ally-1", lowLoyaltyContext.SubjectEntityId);
        Assert.False(lowLoyaltyContext.IsProtectingSubject);

        // Protecting (high loyalty to subject, asker is not friend)
        var protectingContext = new ConversationContext
        {
            Question = question,
            Persona = new Persona(),
            LoyaltyToSubject = 70,
            RelationshipWithAsker = 0
        };
        Assert.True(protectingContext.IsProtectingSubject);

        // Not protecting if asker is friend
        var friendContext = new ConversationContext
        {
            Question = question,
            Persona = new Persona(),
            LoyaltyToSubject = 70,
            RelationshipWithAsker = 50 // Friend
        };
        Assert.False(friendContext.IsProtectingSubject);
    }

    [Test]
    public void ConversationContext_DefaultDecision()
    {
        // Arrange
        var context = new ConversationContext
        {
            Question = new AgentQuestion(),
            Persona = new Persona()
        };

        // Assert - Decision should have default values
        Assert.NotNull(context.Decision);
        Assert.True(context.Decision.WillAnswer);
        Assert.False(context.Decision.WillLie);
    }

    #endregion
}
