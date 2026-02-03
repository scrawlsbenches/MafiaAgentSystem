// Story System - Story Node and Related Types
// Core narrative structure elements

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// A node in the story graph representing a potential event or mission.
///
/// DESIGN DECISION: Using Func&lt;WorldState, bool&gt; for unlock conditions
/// rather than a declarative rule language. This gives us full C# power
/// for complex conditions while keeping the graph structure simple.
/// The trade-off is less serializability, but we can add that later.
/// </summary>
public class StoryNode
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public StoryNodeType Type { get; set; }

    // Activation
    public Func<WorldState, bool>? UnlockCondition { get; set; }
    public bool IsUnlocked { get; set; }
    public int? UnlockedAtWeek { get; set; }

    // Expiration (optional - some opportunities are time-limited)
    public int? ExpiresAfterWeeks { get; set; }
    public bool HasExpired(int currentWeek) =>
        ExpiresAfterWeeks.HasValue &&
        UnlockedAtWeek.HasValue &&
        (currentWeek - UnlockedAtWeek.Value) > ExpiresAfterWeeks.Value;

    // Completion tracking
    public bool IsCompleted { get; set; }
    public bool IsFailed { get; set; }

    // Associated data
    public string? PlotThreadId { get; set; }
    public string? LocationId { get; set; }
    public string? NPCId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// An edge connecting two story nodes with a causal relationship.
/// </summary>
public class StoryEdge
{
    public string FromNodeId { get; set; } = "";
    public string ToNodeId { get; set; } = "";
    public StoryEdgeType Type { get; set; }

    // Optional condition for conditional edges
    public Func<WorldState, bool>? Condition { get; set; }

    // Delay before the edge activates (in weeks)
    public int DelayWeeks { get; set; } = 0;
}

/// <summary>
/// A plot thread is a connected series of missions/events forming a story arc.
///
/// DESIGN DECISION: Plot threads are linear sequences with branching handled
/// through multiple threads rather than complex branching within a thread.
/// This keeps each thread simple while allowing emergent complexity.
/// </summary>
public class PlotThread
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";             // "The Tattaglia Vendetta"
    public string Description { get; set; } = "";

    public PlotState State { get; set; } = PlotState.Dormant;

    // Activation
    public Func<WorldState, bool>? ActivationCondition { get; set; }
    public int? ActivatedAtWeek { get; set; }
    public int? StartedAtWeek { get; set; }

    // Mission sequence
    public List<string> MissionNodeIds { get; set; } = new();
    public int CurrentMissionIndex { get; set; } = 0;

    // Rewards/Consequences
    public int RespectReward { get; set; }
    public decimal MoneyReward { get; set; }
    public Action<WorldState>? OnCompleted { get; set; }
    public Action<WorldState>? OnFailed { get; set; }

    // Priority (higher = more likely to generate missions)
    public int Priority { get; set; } = 50;

    // Computed
    public string? CurrentMissionNodeId =>
        CurrentMissionIndex < MissionNodeIds.Count
            ? MissionNodeIds[CurrentMissionIndex]
            : null;

    public float Progress =>
        MissionNodeIds.Count > 0
            ? (float)CurrentMissionIndex / MissionNodeIds.Count
            : 0f;
}
