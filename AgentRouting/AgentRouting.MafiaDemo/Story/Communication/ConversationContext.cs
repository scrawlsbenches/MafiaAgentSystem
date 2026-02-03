// Story System - Conversation Context
// Context for evaluating conversation rules

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Context for evaluating conversation rules.
/// Contains all information needed to decide how to respond to a question.
/// </summary>
public class ConversationContext
{
    // The question being asked
    public AgentQuestion Question { get; set; } = null!;
    public QuestionType QuestionType => Question.Type;
    public QuestionUrgency Urgency => Question.Urgency;
    public bool IsSensitive => Question.RequiresHonesty;

    // The responder's state
    public Persona Persona { get; set; } = null!;
    public MemoryBank Memories { get; set; } = null!;
    public int RelationshipWithAsker { get; set; }

    // Computed persona properties for rules (delegates to Persona)
    public bool IsProud => Persona.IsProud;
    public bool IsCautious => Persona.IsCautious;
    public bool IsLoyal => Persona.IsLoyal;
    public bool IsCunning => Persona.IsCunning;
    public bool IsTrusting => Persona.IsTrusting;
    public bool IsAggressive => Persona.IsAggressive;
    public int Honesty => Persona.Honesty;

    // Relationship categories (using centralized thresholds)
    public bool IsEnemy => RelationshipWithAsker < Thresholds.Enemy;
    public bool IsStranger => RelationshipWithAsker >= Thresholds.Enemy && RelationshipWithAsker <= Thresholds.Acquaintance;
    public bool IsFriend => RelationshipWithAsker > Thresholds.Acquaintance;
    public bool IsCloseFriend => RelationshipWithAsker > Thresholds.CloseFriend;

    // Memory state
    public List<Memory> RelevantMemories { get; set; } = new();
    public bool HasRelevantMemories => RelevantMemories.Count > 0;
    public bool HasSecretMemories => RelevantMemories.Any(m => m.Type == MemoryType.Secret);
    public int BestMemoryConfidence => RelevantMemories.Max(m => (int?)m.Confidence) ?? 0;

    // Subject relationships (for loyalty checks)
    public string? SubjectEntityId => Question.SubjectEntityId;
    public int LoyaltyToSubject { get; set; }  // How loyal to the entity being asked about
    public bool IsProtectingSubject => LoyaltyToSubject > 50 && !IsFriend;

    // Output - set by rules
    public ResponseDecision Decision { get; set; } = new();
}

/// <summary>
/// The decision made by conversation rules.
/// </summary>
public class ResponseDecision
{
    public bool WillAnswer { get; set; } = true;
    public bool WillLie { get; set; }
    public bool WillBargain { get; set; }
    public string? RefusalReason { get; set; }
    public string? LieReason { get; set; }
    public int RelationshipModifier { get; set; }
    public string? MatchedRule { get; set; }

    // Response style overrides
    public ResponseType? ForcedResponseType { get; set; }
    public string? CustomResponse { get; set; }
}
