// Story System - Story Event
// Represents events that occur in the narrative

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// A story event for logging narrative history.
/// </summary>
public class StoryEvent
{
    public StoryEventType Type { get; set; }
    public string SubjectId { get; set; } = "";
    public int Week { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}
