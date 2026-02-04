using AgentRouting.MafiaDemo.Story;
using TestRunner.Framework;

namespace MafiaDemo.Tests;

/// <summary>
/// Tests for Story System Memory, MemoryBank, and EntityMind classes.
/// </summary>
public class StorySystemMemoryTests : MafiaTestBase
{
    #region Memory Tests

    [Test]
    public void Memory_DefaultValues()
    {
        // Arrange & Act
        var memory = new Memory();

        // Assert
        Assert.False(string.IsNullOrEmpty(memory.Id));
        Assert.Equal("", memory.Summary);
        Assert.NotNull(memory.Data);
        Assert.Equal(0, memory.CreatedWeek);
        Assert.Null(memory.LocationId);
        Assert.Null(memory.InvolvesEntityId);
        Assert.Null(memory.SourceAgentId);
        Assert.Equal(50, memory.Salience);
        Assert.Equal(0, memory.AccessCount);
        Assert.False(memory.LastAccessedWeek.HasValue);
        Assert.Equal(EmotionalValence.Neutral, memory.Emotion);
        Assert.Equal(50, memory.EmotionalIntensity);
        Assert.False(memory.IsFirsthand);
        Assert.Equal(100, memory.Confidence);
    }

    [Test]
    public void Memory_GetEffectiveSalience_BaseSalience()
    {
        // Arrange - Memory with high salience, just created
        var memory = new Memory
        {
            Salience = 100,
            CreatedWeek = 0,
            AccessCount = 0,
            EmotionalIntensity = 0 // No emotional boost
        };

        // Act
        var effective = memory.GetEffectiveSalience(0);

        // Assert - Should be close to base salience (1.0)
        Assert.True(effective > 0.9f && effective <= 1.25f, $"Expected ~1.0, got {effective}");
    }

    [Test]
    public void Memory_GetEffectiveSalience_RecencyDecay()
    {
        // Arrange - Memory created 10 weeks ago
        var memory = new Memory
        {
            Salience = 100,
            CreatedWeek = 0,
            AccessCount = 0,
            EmotionalIntensity = 0
        };

        // Act
        var week0 = memory.GetEffectiveSalience(0);
        var week10 = memory.GetEffectiveSalience(10);
        var week50 = memory.GetEffectiveSalience(50);

        // Assert - Salience should decay over time
        Assert.True(week10 < week0, $"Week 10 ({week10}) should be less than week 0 ({week0})");
        Assert.True(week50 < week10, $"Week 50 ({week50}) should be less than week 10 ({week10})");
    }

    [Test]
    public void Memory_GetEffectiveSalience_AccessBonus()
    {
        // Arrange - Memory with multiple accesses
        var recentMemory = new Memory
        {
            Salience = 50,
            CreatedWeek = 0,
            AccessCount = 0,
            EmotionalIntensity = 50
        };

        var accessedMemory = new Memory
        {
            Salience = 50,
            CreatedWeek = 0,
            AccessCount = 5, // Accessed 5 times
            EmotionalIntensity = 50
        };

        // Act
        var recentSalience = recentMemory.GetEffectiveSalience(5);
        var accessedSalience = accessedMemory.GetEffectiveSalience(5);

        // Assert - Accessed memory should have higher effective salience
        Assert.True(accessedSalience > recentSalience,
            $"Accessed memory ({accessedSalience}) should be > recent ({recentSalience})");
    }

    [Test]
    public void Memory_GetEffectiveSalience_EmotionalBoost()
    {
        // Arrange
        var neutralMemory = new Memory
        {
            Salience = 50,
            CreatedWeek = 0,
            EmotionalIntensity = 0
        };

        var emotionalMemory = new Memory
        {
            Salience = 50,
            CreatedWeek = 0,
            EmotionalIntensity = 100 // High emotional intensity
        };

        // Act
        var neutralSalience = neutralMemory.GetEffectiveSalience(5);
        var emotionalSalience = emotionalMemory.GetEffectiveSalience(5);

        // Assert - Emotional memory should have higher effective salience
        Assert.True(emotionalSalience > neutralSalience,
            $"Emotional ({emotionalSalience}) should be > neutral ({neutralSalience})");
    }

    [Test]
    public void Memory_ShouldForget_HighSalienceNeverForgotten()
    {
        // Arrange - High salience memory (> HighSalience threshold of 80)
        var memory = new Memory { Salience = 85 };

        // Act & Assert - Should not forget even after long time
        Assert.False(memory.ShouldForget(100));
    }

    [Test]
    public void Memory_ShouldForget_EmotionalMemoriesPersist()
    {
        // Arrange - Emotional memory (intensity > TraitHigh threshold of 70)
        var memory = new Memory
        {
            Salience = 30,
            EmotionalIntensity = 80
        };

        // Act & Assert
        Assert.False(memory.ShouldForget(100));
    }

    [Test]
    public void Memory_ShouldForget_LowSalienceEventuallyForgotten()
    {
        // Arrange - Low salience, non-emotional memory
        var memory = new Memory
        {
            Salience = 10,
            CreatedWeek = 0,
            EmotionalIntensity = 20
        };

        // Act - Should be forgotten after long time
        var shouldForget = memory.ShouldForget(500);

        // Assert
        Assert.True(shouldForget, "Low salience memory should eventually be forgotten");
    }

    #endregion

    #region MemoryBank Tests

    [Test]
    public void MemoryBank_Remember_AddsMemory()
    {
        // Arrange
        var bank = new MemoryBank();
        var memory = new Memory { Summary = "Test memory" };

        // Act
        bank.Remember(memory);

        // Assert - Can search for it
        var found = bank.Search("Test", 0, 1).ToList();
        Assert.Equal(1, found.Count);
    }

    [Test]
    public void MemoryBank_Remember_IndexesByEntity()
    {
        // Arrange
        var bank = new MemoryBank();
        var memory = new Memory
        {
            Summary = "Met with the boss",
            InvolvesEntityId = "boss-1"
        };

        // Act
        bank.Remember(memory);

        // Assert
        Assert.True(bank.KnowsAbout("boss-1"));
        Assert.False(bank.KnowsAbout("unknown"));
    }

    [Test]
    public void MemoryBank_Remember_IndexesByLocation()
    {
        // Arrange
        var bank = new MemoryBank();
        var memory = new Memory
        {
            Summary = "Meeting at the docks",
            LocationId = "docks"
        };

        // Act
        bank.Remember(memory);

        // Assert
        var found = bank.RecallAtLocation("docks", 0).ToList();
        Assert.Equal(1, found.Count);
    }

    [Test]
    public void MemoryBank_Remember_IndexesByType()
    {
        // Arrange
        var bank = new MemoryBank();
        var memory = new Memory
        {
            Summary = "Important secret",
            Type = MemoryType.Secret
        };

        // Act
        bank.Remember(memory);

        // Assert
        var found = bank.RecallByType(MemoryType.Secret, 0).ToList();
        Assert.Equal(1, found.Count);
    }

    [Test]
    public void MemoryBank_Remember_PrunesWhenOverCapacity()
    {
        // Arrange
        var bank = new MemoryBank { Capacity = 3 };

        // Add 4 memories with different salience
        bank.Remember(new Memory { Summary = "Low1", Salience = 10 });
        bank.Remember(new Memory { Summary = "High", Salience = 90 });
        bank.Remember(new Memory { Summary = "Low2", Salience = 20 });
        bank.Remember(new Memory { Summary = "Medium", Salience = 50 });

        // Assert - Should have pruned the lowest salience one
        var all = bank.Search("", 0, 10).ToList();
        Assert.Equal(3, all.Count);
        Assert.False(all.Any(m => m.Summary == "Low1"), "Lowest salience should be pruned");
    }

    [Test]
    public void MemoryBank_LearnFrom_CreatesSecondhandMemory()
    {
        // Arrange
        var bank = new MemoryBank();
        var sourceMemory = new Memory
        {
            Type = MemoryType.Fact,
            Summary = "The rival is planning an attack",
            InvolvesEntityId = "rival",
            Salience = 80,
            Confidence = 100,
            EmotionalIntensity = 70
        };

        // Act
        bank.LearnFrom(sourceMemory, "informant-1", 5);

        // Assert
        var learned = bank.RecallAbout("rival", 5).ToList();
        Assert.Equal(1, learned.Count);

        var memory = learned[0];
        Assert.False(memory.IsFirsthand);
        Assert.Equal("informant-1", memory.SourceAgentId);
        Assert.Equal(40, memory.Salience); // Half of original
        Assert.Equal(80, memory.Confidence); // 100 - 20
        Assert.Equal(EmotionalValence.Neutral, memory.Emotion); // Secondhand = neutral
    }

    [Test]
    public void MemoryBank_RecallAbout_SortsBySalience()
    {
        // Arrange
        var bank = new MemoryBank();
        bank.Remember(new Memory
        {
            InvolvesEntityId = "target",
            Summary = "Low",
            Salience = 20,
            CreatedWeek = 0
        });
        bank.Remember(new Memory
        {
            InvolvesEntityId = "target",
            Summary = "High",
            Salience = 90,
            CreatedWeek = 0
        });

        // Act
        var recalled = bank.RecallAbout("target", 0).ToList();

        // Assert
        Assert.Equal(2, recalled.Count);
        Assert.Equal("High", recalled[0].Summary); // Higher salience first
    }

    [Test]
    public void MemoryBank_RecallAbout_IncrementsAccessCount()
    {
        // Arrange
        var bank = new MemoryBank();
        var memory = new Memory
        {
            InvolvesEntityId = "target",
            Summary = "Important fact",
            AccessCount = 0
        };
        bank.Remember(memory);

        // Act
        var recalled = bank.RecallAbout("target", 5).ToList();

        // Assert
        Assert.Equal(1, recalled[0].AccessCount);
        Assert.Equal(5, recalled[0].LastAccessedWeek);
    }

    [Test]
    public void MemoryBank_RecallAbout_ReturnsEmptyForUnknownEntity()
    {
        // Arrange
        var bank = new MemoryBank();

        // Act
        var recalled = bank.RecallAbout("unknown", 0).ToList();

        // Assert
        Assert.Equal(0, recalled.Count);
    }

    [Test]
    public void MemoryBank_RecallAtLocation_RespectsLimit()
    {
        // Arrange
        var bank = new MemoryBank();
        for (int i = 0; i < 10; i++)
        {
            bank.Remember(new Memory
            {
                LocationId = "warehouse",
                Summary = $"Memory {i}",
                Salience = i * 10
            });
        }

        // Act
        var recalled = bank.RecallAtLocation("warehouse", 0, limit: 3).ToList();

        // Assert
        Assert.Equal(3, recalled.Count);
    }

    [Test]
    public void MemoryBank_Search_FindsByKeyword()
    {
        // Arrange
        var bank = new MemoryBank();
        bank.Remember(new Memory { Summary = "Meeting with the boss" });
        bank.Remember(new Memory { Summary = "Went to the docks" });
        bank.Remember(new Memory { Summary = "Another meeting happened" });

        // Act
        var found = bank.Search("meeting", 0).ToList();

        // Assert
        Assert.Equal(2, found.Count);
    }

    [Test]
    public void MemoryBank_RecallEmotional_SortsbyEmotionalIntensity()
    {
        // Arrange
        var bank = new MemoryBank();
        bank.Remember(new Memory
        {
            Summary = "Mild",
            EmotionalIntensity = 55, // Just above ModerateSalience (50)
            Salience = 50
        });
        bank.Remember(new Memory
        {
            Summary = "Intense",
            EmotionalIntensity = 90,
            Salience = 50
        });
        bank.Remember(new Memory
        {
            Summary = "Weak",
            EmotionalIntensity = 30 // Below threshold, won't be included
        });

        // Act
        var recalled = bank.RecallEmotional(0).ToList();

        // Assert
        Assert.Equal(2, recalled.Count);
        Assert.Equal("Intense", recalled[0].Summary);
    }

    [Test]
    public void MemoryBank_GetSentiment_PositiveMemories()
    {
        // Arrange
        var bank = new MemoryBank();
        bank.Remember(new Memory
        {
            InvolvesEntityId = "friend",
            Emotion = EmotionalValence.Positive,
            EmotionalIntensity = 80,
            Salience = 70
        });

        // Act
        var sentiment = bank.GetSentiment("friend", 0);

        // Assert
        Assert.True(sentiment > 0, $"Sentiment should be positive, got {sentiment}");
    }

    [Test]
    public void MemoryBank_GetSentiment_NegativeMemories()
    {
        // Arrange
        var bank = new MemoryBank();
        bank.Remember(new Memory
        {
            InvolvesEntityId = "enemy",
            Emotion = EmotionalValence.Negative,
            EmotionalIntensity = 80,
            Salience = 70
        });

        // Act
        var sentiment = bank.GetSentiment("enemy", 0);

        // Assert
        Assert.True(sentiment < 0, $"Sentiment should be negative, got {sentiment}");
    }

    [Test]
    public void MemoryBank_GetSentiment_UnknownEntity()
    {
        // Arrange
        var bank = new MemoryBank();

        // Act
        var sentiment = bank.GetSentiment("unknown", 0);

        // Assert
        Assert.Equal(0, sentiment);
    }

    [Test]
    public void MemoryBank_Forget_RemovesOldLowSalienceMemories()
    {
        // Arrange
        var bank = new MemoryBank();
        bank.Remember(new Memory
        {
            Summary = "Important",
            Salience = 85, // High salience (> 80 threshold) - won't forget
            CreatedWeek = 0
        });
        bank.Remember(new Memory
        {
            Summary = "Unimportant",
            Salience = 10, // Low salience
            EmotionalIntensity = 20, // Not emotional
            CreatedWeek = 0
        });

        // Act - Forget at a far future week
        bank.Forget(500);

        // Assert
        var remaining = bank.Search("", 500, 10).ToList();
        Assert.Equal(1, remaining.Count);
        Assert.Equal("Important", remaining[0].Summary);
    }

    #endregion

    #region EntityMind Tests

    [Test]
    public void EntityMind_DefaultValues()
    {
        // Arrange & Act
        var mind = new EntityMind();

        // Assert
        Assert.Equal("", mind.EntityId);
        Assert.NotNull(mind.Persona);
        Assert.NotNull(mind.Memories);
    }

    [Test]
    public void EntityMind_RecordInteraction_CreatesFirsthandMemory()
    {
        // Arrange
        var mind = new EntityMind { EntityId = "player" };

        // Act
        mind.RecordInteraction(
            otherEntityId: "npc-1",
            summary: "Had a friendly conversation",
            emotion: EmotionalValence.Positive,
            emotionalIntensity: 60,
            week: 5,
            locationId: "bar"
        );

        // Assert
        Assert.True(mind.Memories.KnowsAbout("npc-1"));

        var memories = mind.Memories.RecallAbout("npc-1", 5).ToList();
        Assert.Equal(1, memories.Count);

        var memory = memories[0];
        Assert.Equal(MemoryType.Interaction, memory.Type);
        Assert.Equal("Had a friendly conversation", memory.Summary);
        Assert.Equal("npc-1", memory.InvolvesEntityId);
        Assert.Equal("bar", memory.LocationId);
        Assert.Equal(5, memory.CreatedWeek);
        Assert.True(memory.IsFirsthand);
        Assert.Equal(100, memory.Confidence);
        Assert.Equal(EmotionalValence.Positive, memory.Emotion);
        Assert.Equal(60, memory.EmotionalIntensity);
        Assert.Equal(80, memory.Salience); // 50 + 60/2
    }

    [Test]
    public void EntityMind_LearnFact_CreatesFactMemory()
    {
        // Arrange
        var mind = new EntityMind { EntityId = "player" };

        // Act - Learn from another source
        mind.LearnFact(
            fact: "The rival family is planning a heist",
            aboutEntityId: "rival-family",
            week: 10,
            sourceAgentId: "informant",
            confidence: 70
        );

        // Assert
        Assert.True(mind.Memories.KnowsAbout("rival-family"));

        var memories = mind.Memories.RecallAbout("rival-family", 10).ToList();
        Assert.Equal(1, memories.Count);

        var memory = memories[0];
        Assert.Equal(MemoryType.Fact, memory.Type);
        Assert.Equal("The rival family is planning a heist", memory.Summary);
        Assert.False(memory.IsFirsthand);
        Assert.Equal("informant", memory.SourceAgentId);
        Assert.Equal(70, memory.Confidence);
        Assert.Equal(40, memory.Salience);
    }

    [Test]
    public void EntityMind_LearnFact_FirsthandWhenNoSource()
    {
        // Arrange
        var mind = new EntityMind { EntityId = "player" };

        // Act - Learn directly (no source)
        mind.LearnFact(
            fact: "I saw the shipment arrive",
            aboutEntityId: null,
            week: 10,
            sourceAgentId: null, // No source = firsthand
            confidence: 95
        );

        // Assert
        var memories = mind.Memories.Search("shipment", 10).ToList();
        Assert.Equal(1, memories.Count);
        Assert.True(memories[0].IsFirsthand);
        Assert.Null(memories[0].SourceAgentId);
    }

    [Test]
    public void EntityMind_MultipleInteractions_BuildsRelationshipHistory()
    {
        // Arrange
        var mind = new EntityMind { EntityId = "player" };

        // Act - Multiple interactions over time
        mind.RecordInteraction("ally", "First meeting", EmotionalValence.Neutral, 30, 1);
        mind.RecordInteraction("ally", "Helped with job", EmotionalValence.Positive, 70, 3);
        mind.RecordInteraction("ally", "Saved my life", EmotionalValence.Positive, 100, 5);

        // Assert
        var memories = mind.Memories.RecallAbout("ally", 10, limit: 10).ToList();
        Assert.Equal(3, memories.Count);

        // Sentiment should be positive
        var sentiment = mind.Memories.GetSentiment("ally", 10);
        Assert.True(sentiment > 0);
    }

    #endregion
}
