// Story System - Agent Question
// Represents a question one agent asks another

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Represents a question one agent asks another.
/// Questions can seek information, request action, or test loyalty.
///
/// DESIGN DECISION: Questions are typed and carry context.
/// The respondent uses their persona, memories, and relationship
/// to craft an appropriate response.
/// </summary>
public class AgentQuestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AskerId { get; set; } = "";
    public string ResponderId { get; set; } = "";
    public QuestionType Type { get; set; }

    // Content
    public string Topic { get; set; } = "";             // What's being asked about
    public string? SubjectEntityId { get; set; }        // Entity the question is about
    public string? SubjectLocationId { get; set; }      // Location the question is about
    public Dictionary<string, object> Context { get; set; } = new();

    // Metadata
    public int AskedWeek { get; set; }
    public QuestionUrgency Urgency { get; set; } = QuestionUrgency.Normal;
    public bool RequiresHonesty { get; set; }           // Is lying risky?
}
