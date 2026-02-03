// Story System - Story Graph
// Manages all narrative state and progression

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// The story graph manages all narrative state and progression.
///
/// ALGORITHM NOTES:
/// - Node lookup is O(1) via dictionary
/// - Edge traversal uses adjacency list for O(degree) neighbor lookup
/// - Unlock evaluation is O(n) where n = unlockable nodes, called once per turn
/// - Plot thread updates are O(p) where p = active plots
/// </summary>
public class StoryGraph
{
    private readonly Dictionary<string, StoryNode> _nodes = new();
    private readonly Dictionary<string, List<StoryEdge>> _outgoingEdges = new();
    private readonly Dictionary<string, List<StoryEdge>> _incomingEdges = new();
    private readonly Dictionary<string, PlotThread> _plotThreads = new();

    // Event history for narrative recap
    private readonly List<StoryEvent> _eventLog = new();

    #region Node Management

    public void AddNode(StoryNode node)
    {
        _nodes[node.Id] = node;
        _outgoingEdges[node.Id] = new List<StoryEdge>();
        _incomingEdges[node.Id] = new List<StoryEdge>();
    }

    public void AddEdge(StoryEdge edge)
    {
        if (!_outgoingEdges.ContainsKey(edge.FromNodeId))
            _outgoingEdges[edge.FromNodeId] = new List<StoryEdge>();
        if (!_incomingEdges.ContainsKey(edge.ToNodeId))
            _incomingEdges[edge.ToNodeId] = new List<StoryEdge>();

        _outgoingEdges[edge.FromNodeId].Add(edge);
        _incomingEdges[edge.ToNodeId].Add(edge);
    }

    public StoryNode? GetNode(string id) =>
        _nodes.TryGetValue(id, out var node) ? node : null;

    public IEnumerable<StoryEdge> GetOutgoingEdges(string nodeId) =>
        _outgoingEdges.TryGetValue(nodeId, out var edges)
            ? edges
            : Enumerable.Empty<StoryEdge>();

    public IEnumerable<StoryEdge> GetIncomingEdges(string nodeId) =>
        _incomingEdges.TryGetValue(nodeId, out var edges)
            ? edges
            : Enumerable.Empty<StoryEdge>();

    #endregion

    #region Plot Thread Management

    public void AddPlotThread(PlotThread plot)
    {
        _plotThreads[plot.Id] = plot;
    }

    public PlotThread? GetPlotThread(string id) =>
        _plotThreads.TryGetValue(id, out var plot) ? plot : null;

    public IEnumerable<PlotThread> GetActivePlots() =>
        _plotThreads.Values.Where(p => p.State == PlotState.Active);

    public IEnumerable<PlotThread> GetAvailablePlots() =>
        _plotThreads.Values.Where(p => p.State == PlotState.Available);

    public IEnumerable<PlotThread> GetAllPlotThreads() =>
        _plotThreads.Values;

    #endregion

    #region Graph Algorithms

    /// <summary>
    /// Evaluate which nodes should become unlocked based on current world state.
    /// Called once per turn to update story progression.
    ///
    /// ALGORITHM:
    /// 1. For each dormant plot, check activation condition
    /// 2. For each locked node, check unlock condition AND prerequisite edges
    /// 3. For each unlocked node, check if it should expire
    /// 4. Process Triggers edges to auto-start dependent nodes
    /// </summary>
    public List<StoryNode> UpdateUnlocks(WorldState world)
    {
        var newlyUnlocked = new List<StoryNode>();

        // 1. Check plot thread activations
        foreach (var plot in _plotThreads.Values.Where(p => p.State == PlotState.Dormant))
        {
            if (plot.ActivationCondition?.Invoke(world) == true)
            {
                plot.State = PlotState.Available;
                plot.ActivatedAtWeek = world.CurrentWeek;
                LogEvent(new StoryEvent
                {
                    Type = StoryEventType.PlotActivated,
                    SubjectId = plot.Id,
                    Week = world.CurrentWeek
                });
            }
        }

        // 2. Check node unlocks
        foreach (var node in _nodes.Values.Where(n => !n.IsUnlocked && !n.IsCompleted && !n.IsFailed))
        {
            if (ShouldUnlock(node, world))
            {
                node.IsUnlocked = true;
                node.UnlockedAtWeek = world.CurrentWeek;
                newlyUnlocked.Add(node);
                LogEvent(new StoryEvent
                {
                    Type = StoryEventType.NodeUnlocked,
                    SubjectId = node.Id,
                    Week = world.CurrentWeek
                });
            }
        }

        // 3. Check expirations
        foreach (var node in _nodes.Values.Where(n => n.IsUnlocked && !n.IsCompleted))
        {
            if (node.HasExpired(world.CurrentWeek))
            {
                node.IsFailed = true;
                LogEvent(new StoryEvent
                {
                    Type = StoryEventType.NodeExpired,
                    SubjectId = node.Id,
                    Week = world.CurrentWeek
                });
            }
        }

        // 4. Process Triggers edges from newly unlocked nodes
        foreach (var node in newlyUnlocked)
        {
            foreach (var edge in GetOutgoingEdges(node.Id).Where(e => e.Type == StoryEdgeType.Triggers))
            {
                if (edge.DelayWeeks == 0)
                {
                    var targetNode = GetNode(edge.ToNodeId);
                    if (targetNode != null && !targetNode.IsUnlocked)
                    {
                        targetNode.IsUnlocked = true;
                        targetNode.UnlockedAtWeek = world.CurrentWeek;
                    }
                }
                // Delayed triggers handled elsewhere
            }
        }

        return newlyUnlocked;
    }

    private bool ShouldUnlock(StoryNode node, WorldState world)
    {
        // Check node's own unlock condition
        if (node.UnlockCondition != null && !node.UnlockCondition(world))
            return false;

        // Check all Requires edges (prerequisites)
        var requiresEdges = GetIncomingEdges(node.Id)
            .Where(e => e.Type == StoryEdgeType.Requires);

        foreach (var edge in requiresEdges)
        {
            var prereq = GetNode(edge.FromNodeId);
            if (prereq == null || !prereq.IsCompleted)
                return false;
        }

        // Check no Blocks edges are active
        var blocksEdges = GetIncomingEdges(node.Id)
            .Where(e => e.Type == StoryEdgeType.Blocks);

        foreach (var edge in blocksEdges)
        {
            var blocker = GetNode(edge.FromNodeId);
            if (blocker != null && blocker.IsUnlocked && !blocker.IsCompleted && !blocker.IsFailed)
                return false;  // Blocked by active node
        }

        return true;
    }

    /// <summary>
    /// Get all currently available mission nodes.
    /// </summary>
    public IEnumerable<StoryNode> GetAvailableMissions(WorldState world) =>
        _nodes.Values.Where(n =>
            n.Type == StoryNodeType.Mission &&
            n.IsUnlocked &&
            !n.IsCompleted &&
            !n.IsFailed &&
            !n.HasExpired(world.CurrentWeek));

    /// <summary>
    /// Mark a node as completed and process outgoing edges.
    /// </summary>
    public void CompleteNode(string nodeId, WorldState world, bool success)
    {
        var node = GetNode(nodeId);
        if (node == null) return;

        if (success)
        {
            node.IsCompleted = true;
            LogEvent(new StoryEvent
            {
                Type = StoryEventType.NodeCompleted,
                SubjectId = nodeId,
                Week = world.CurrentWeek
            });

            // Process Unlocks edges
            foreach (var edge in GetOutgoingEdges(nodeId).Where(e => e.Type == StoryEdgeType.Unlocks))
            {
                var targetNode = GetNode(edge.ToNodeId);
                if (targetNode != null)
                {
                    // Will be picked up in next UpdateUnlocks cycle
                }
            }

            // Update plot thread progress
            if (node.PlotThreadId != null)
            {
                var plot = GetPlotThread(node.PlotThreadId);
                if (plot != null && plot.CurrentMissionNodeId == nodeId)
                {
                    plot.CurrentMissionIndex++;
                    if (plot.CurrentMissionIndex >= plot.MissionNodeIds.Count)
                    {
                        plot.State = PlotState.Completed;
                        plot.OnCompleted?.Invoke(world);
                    }
                }
            }
        }
        else
        {
            node.IsFailed = true;
            LogEvent(new StoryEvent
            {
                Type = StoryEventType.NodeFailed,
                SubjectId = nodeId,
                Week = world.CurrentWeek
            });

            // Plot thread failure
            if (node.PlotThreadId != null)
            {
                var plot = GetPlotThread(node.PlotThreadId);
                if (plot != null)
                {
                    plot.State = PlotState.Failed;
                    plot.OnFailed?.Invoke(world);
                }
            }
        }
    }

    #endregion

    #region Event Log

    public void LogEvent(StoryEvent evt)
    {
        _eventLog.Add(evt);
    }

    public IEnumerable<StoryEvent> GetRecentEvents(int count) =>
        _eventLog.TakeLast(count);

    public IEnumerable<StoryEvent> GetEventsForSubject(string subjectId) =>
        _eventLog.Where(e => e.SubjectId == subjectId);

    #endregion
}
