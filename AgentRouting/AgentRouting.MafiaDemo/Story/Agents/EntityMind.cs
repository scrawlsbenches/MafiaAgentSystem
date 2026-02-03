// Story System - EntityMind
// Combines persona and memories for any entity

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Extension to WorldState to support personas and memories for all entities.
/// </summary>
public class EntityMind
{
    public string EntityId { get; set; } = "";
    public Persona Persona { get; set; } = new();
    public MemoryBank Memories { get; set; } = new();

    /// <summary>
    /// Record an interaction with another entity.
    /// </summary>
    public void RecordInteraction(string otherEntityId, string summary,
        EmotionalValence emotion, int emotionalIntensity, int week, string? locationId = null)
    {
        Memories.Remember(new Memory
        {
            Type = MemoryType.Interaction,
            Summary = summary,
            InvolvesEntityId = otherEntityId,
            LocationId = locationId,
            CreatedWeek = week,
            Salience = 50 + emotionalIntensity / 2,
            Emotion = emotion,
            EmotionalIntensity = emotionalIntensity,
            IsFirsthand = true,
            Confidence = 100
        });
    }

    /// <summary>
    /// Learn a fact from another source.
    /// </summary>
    public void LearnFact(string fact, string? aboutEntityId, int week,
        string? sourceAgentId = null, int confidence = 80)
    {
        Memories.Remember(new Memory
        {
            Type = MemoryType.Fact,
            Summary = fact,
            InvolvesEntityId = aboutEntityId,
            CreatedWeek = week,
            SourceAgentId = sourceAgentId,
            Salience = 40,
            IsFirsthand = sourceAgentId == null,
            Confidence = confidence
        });
    }
}
