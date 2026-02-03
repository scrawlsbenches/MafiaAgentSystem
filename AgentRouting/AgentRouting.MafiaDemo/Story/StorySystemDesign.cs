// =============================================================================
// STORY SYSTEM DESIGN - Draft for Review
// =============================================================================
// This file contains the complete design for the dynamic story system.
// It's meant to be reviewed and refined before splitting into proper files.
//
// Key Design Goals:
// 1. Locations and NPCs have persistent state that affects mission availability
// 2. Mission outcomes trigger consequences that modify world state
// 3. Agents share intelligence that constrains what missions are offered
// 4. Story progresses through interconnected plot threads
// 5. No mission repetition - variety through dynamic generation
// =============================================================================

using RulesEngine.Core;
using AgentRouting.MafiaDemo.Missions;

namespace AgentRouting.MafiaDemo.Story;

#region Core Enums

/// <summary>
/// Location states affect what missions are available there
/// </summary>
public enum LocationState
{
    Neutral,      // Default - any mission type available
    Friendly,     // We control it - collection missions easy, lower risk
    Hostile,      // Enemy territory - high risk, special missions only
    Contested,    // Active conflict - war missions available
    Compromised,  // Under surveillance - high heat, avoid or bribe first
    Destroyed     // No longer usable - removed from mission pool
}

/// <summary>
/// NPC status determines how they can be interacted with
/// </summary>
public enum NPCStatus
{
    Active,       // Normal - available for any mission type
    Intimidated,  // Scared - won't resist, but relationship damaged
    Allied,       // On our side - provides intel, discounts, opportunities
    Hostile,      // Against us - may trigger revenge missions
    Bribed,       // Paid off - temporary alliance, may betray
    Dead,         // Eliminated - no further interaction, may have avengers
    Fled,         // Left the area - may return later with grudge
    Imprisoned,   // In jail - can potentially be freed or silenced
    Informant     // Working with Feds - high priority threat
}

/// <summary>
/// Intel reliability affects how much we trust the information
/// </summary>
public enum IntelReliability
{
    Rumor = 25,       // Street talk - might be wrong
    Observed = 50,    // Agent saw something - probably right
    Confirmed = 75,   // Multiple sources - reliable
    Absolute = 100    // Direct knowledge - certain
}

/// <summary>
/// Plot thread states for multi-mission story arcs
/// </summary>
public enum PlotState
{
    Dormant,      // Not yet triggered - waiting for activation condition
    Available,    // Can be started - player can engage
    Active,       // In progress - generates priority missions
    Completed,    // Successfully finished - rewards given
    Failed,       // Botched - negative consequences applied
    Abandoned     // Player ignored too long - opportunity lost
}

/// <summary>
/// Edge types in the story graph define causal relationships
/// </summary>
public enum StoryEdgeType
{
    Unlocks,      // Completing A makes B available
    Blocks,       // A being active prevents B
    Triggers,     // A automatically starts B
    Requires,     // B needs A completed first (prerequisite)
    Conflicts,    // A and B are mutually exclusive paths
    Chains        // A leads directly to B (same plot thread)
}

#endregion

#region World State - Core Data Structures

/// <summary>
/// Represents a physical location in the game world.
/// Locations persist across the game and remember what happened there.
///
/// DESIGN DECISION: Using a flat dictionary for history rather than a linked list
/// because we need O(1) lookup by event type and O(n) is fine for iteration
/// (history won't exceed ~50 events per location in a typical game).
/// </summary>
public class Location
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    // Current state
    public LocationState State { get; set; } = LocationState.Neutral;
    public string? OwnerId { get; set; }  // Faction ID or "player" or null
    public int LocalHeat { get; set; }     // 0-100, police attention at this spot

    // Tracking
    public int TimesVisited { get; set; }
    public int? LastVisitedWeek { get; set; }
    public List<string> EventHistory { get; set; } = new();  // Event IDs

    // NPCs present at this location (NPC ID -> relationship modifier)
    public Dictionary<string, int> ResidentNPCs { get; set; } = new();

    // Economic value (affects mission rewards)
    public decimal WeeklyValue { get; set; } = 500m;
    public decimal ProtectionCut { get; set; } = 0.4m;  // Our percentage

    // Computed properties for rules engine
    public bool IsHot => LocalHeat > 50;
    public bool IsOurs => OwnerId == "player";
    public bool IsContested => State == LocationState.Contested;
    public bool WasRecentlyVisited(int currentWeek) =>
        LastVisitedWeek.HasValue && (currentWeek - LastVisitedWeek.Value) < 3;
}

/// <summary>
/// Represents a non-player character with persistent relationships.
/// NPCs remember how the player treated them and react accordingly.
///
/// DESIGN DECISION: Relationship is a single int (-100 to 100) rather than
/// a complex multi-dimensional model. This keeps rules simple while still
/// allowing nuanced behavior through status + relationship combinations.
/// </summary>
public class NPC
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";          // "Tony Marinelli"
    public string Role { get; set; } = "";          // "restaurant_owner", "dock_worker"
    public string? Title { get; set; }              // "the shopkeeper", "the informant"

    // Location binding
    public string LocationId { get; set; } = "";
    public string? FactionId { get; set; }          // Which faction they belong to

    // Relationship state
    public int Relationship { get; set; } = 0;      // -100 (enemy) to +100 (ally)
    public NPCStatus Status { get; set; } = NPCStatus.Active;

    // History tracking
    public List<string> InteractionHistory { get; set; } = new();
    public string? LastMissionId { get; set; }
    public int? LastInteractionWeek { get; set; }
    public int TotalInteractions { get; set; }

    // Knowledge - which agents know about this NPC
    public HashSet<string> KnownByAgents { get; set; } = new();

    // Computed properties
    public bool IsAlly => Relationship > 50;
    public bool IsEnemy => Relationship < -50;
    public bool IsNeutral => Relationship >= -50 && Relationship <= 50;
    public bool CanBeIntimidated => Status == NPCStatus.Active && Relationship > -75;
    public bool WillResist => Status == NPCStatus.Hostile || Relationship < -25;

    // For display
    public string DisplayName => Title ?? Name;
}

/// <summary>
/// Represents a rival family or faction.
/// Extends the existing RivalFamily concept with territory tracking.
/// </summary>
public class Faction
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";          // "Tattaglia Family"

    // Relationship to player
    public int Hostility { get; set; } = 30;        // 0-100
    public int Respect { get; set; } = 50;          // How much they fear/respect us

    // Territory control
    public HashSet<string> ControlledLocationIds { get; set; } = new();
    public int Resources { get; set; } = 100;       // Economic/military strength

    // Personnel
    public HashSet<string> MemberNPCIds { get; set; } = new();

    // State tracking
    public bool AtWar { get; set; }
    public bool HasTruce { get; set; }
    public int? TruceExpiresWeek { get; set; }

    // Computed
    public bool IsAggressive => Hostility > 70 && !HasTruce;
    public bool IsWeak => Resources < 30;
    public bool IsStrong => Resources > 70;
}

/// <summary>
/// Central container for all world state.
/// Single source of truth for the game's persistent state.
///
/// DESIGN DECISION: Using dictionaries for O(1) lookup by ID.
/// This is critical for the rules engine which queries state frequently.
/// </summary>
public class WorldState
{
    public Dictionary<string, Location> Locations { get; } = new();
    public Dictionary<string, NPC> NPCs { get; } = new();
    public Dictionary<string, Faction> Factions { get; } = new();

    // Cross-references for efficient querying
    private Dictionary<string, List<string>> _npcsByLocation = new();
    private Dictionary<string, List<string>> _locationsByFaction = new();

    // Game time
    public int CurrentWeek { get; set; } = 1;

    #region Query Methods

    public Location? GetLocation(string id) =>
        Locations.TryGetValue(id, out var loc) ? loc : null;

    public NPC? GetNPC(string id) =>
        NPCs.TryGetValue(id, out var npc) ? npc : null;

    public Faction? GetFaction(string id) =>
        Factions.TryGetValue(id, out var faction) ? faction : null;

    public IEnumerable<NPC> GetNPCsAtLocation(string locationId) =>
        _npcsByLocation.TryGetValue(locationId, out var ids)
            ? ids.Select(id => NPCs[id])
            : Enumerable.Empty<NPC>();

    public IEnumerable<Location> GetLocationsByState(LocationState state) =>
        Locations.Values.Where(l => l.State == state);

    public IEnumerable<Location> GetAccessibleLocations() =>
        Locations.Values.Where(l =>
            l.State != LocationState.Destroyed &&
            l.State != LocationState.Hostile);

    public IEnumerable<NPC> GetNPCsByStatus(NPCStatus status) =>
        NPCs.Values.Where(n => n.Status == status);

    public IEnumerable<NPC> GetNPCsNeedingAttention(int currentWeek) =>
        NPCs.Values.Where(n =>
            n.Status == NPCStatus.Hostile ||
            (n.IsAlly && n.LastInteractionWeek < currentWeek - 4) ||
            n.Status == NPCStatus.Informant);

    #endregion

    #region Mutation Methods

    public void RegisterLocation(Location location)
    {
        Locations[location.Id] = location;
        if (location.OwnerId != null)
        {
            if (!_locationsByFaction.ContainsKey(location.OwnerId))
                _locationsByFaction[location.OwnerId] = new List<string>();
            _locationsByFaction[location.OwnerId].Add(location.Id);
        }
    }

    public void RegisterNPC(NPC npc)
    {
        NPCs[npc.Id] = npc;
        if (!_npcsByLocation.ContainsKey(npc.LocationId))
            _npcsByLocation[npc.LocationId] = new List<string>();
        _npcsByLocation[npc.LocationId].Add(npc.Id);
    }

    public void MoveNPC(string npcId, string newLocationId)
    {
        var npc = GetNPC(npcId);
        if (npc == null) return;

        // Remove from old location
        if (_npcsByLocation.TryGetValue(npc.LocationId, out var oldList))
            oldList.Remove(npcId);

        // Add to new location
        npc.LocationId = newLocationId;
        if (!_npcsByLocation.ContainsKey(newLocationId))
            _npcsByLocation[newLocationId] = new List<string>();
        _npcsByLocation[newLocationId].Add(npcId);
    }

    public void TransferTerritory(string locationId, string? newOwnerId)
    {
        var location = GetLocation(locationId);
        if (location == null) return;

        // Remove from old owner
        if (location.OwnerId != null &&
            _locationsByFaction.TryGetValue(location.OwnerId, out var oldList))
            oldList.Remove(locationId);

        // Add to new owner
        location.OwnerId = newOwnerId;
        if (newOwnerId != null)
        {
            if (!_locationsByFaction.ContainsKey(newOwnerId))
                _locationsByFaction[newOwnerId] = new List<string>();
            _locationsByFaction[newOwnerId].Add(locationId);
        }
    }

    #endregion
}

#endregion

#region Story Graph - Narrative Structure

/// <summary>
/// A node in the story graph representing a potential event or mission.
///
/// DESIGN DECISION: Using Func<WorldState, bool> for unlock conditions
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

public enum StoryNodeType
{
    Mission,          // A mission the player can undertake
    Event,            // Something that happens in the world
    Consequence,      // Result of a previous action
    Opportunity,      // Time-limited chance
    Threat,           // Danger that must be addressed
    Milestone         // Story progression marker
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

public class StoryEvent
{
    public StoryEventType Type { get; set; }
    public string SubjectId { get; set; } = "";
    public int Week { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public enum StoryEventType
{
    PlotActivated,
    NodeUnlocked,
    NodeCompleted,
    NodeFailed,
    NodeExpired,
    NPCStatusChanged,
    LocationStateChanged,
    IntelReceived,
    ConsequenceApplied
}

#endregion

#region Intelligence System

/// <summary>
/// Intel represents information that agents share about the world.
///
/// DESIGN DECISION: Intel has a reliability score and expiration.
/// This lets us model uncertainty and information decay naturally.
/// Agents at different levels have different intel gathering abilities.
/// </summary>
public class Intel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public IntelType Type { get; set; }
    public string SubjectId { get; set; } = "";     // Location, NPC, or Faction ID
    public string SubjectType { get; set; } = "";   // "location", "npc", "faction"

    // Content
    public string Summary { get; set; } = "";       // Human-readable summary
    public Dictionary<string, object> Data { get; set; } = new();

    // Metadata
    public string SourceAgentId { get; set; } = "";
    public int Reliability { get; set; }            // 0-100
    public int GatheredWeek { get; set; }
    public int? ExpiresWeek { get; set; }

    // Processing
    public bool IsProcessed { get; set; }
    public bool IsActedUpon { get; set; }

    public bool IsExpired(int currentWeek) =>
        ExpiresWeek.HasValue && currentWeek > ExpiresWeek.Value;

    public bool IsReliable => Reliability >= 75;
}

public enum IntelType
{
    // Location intel
    LocationHeat,         // Police activity at location
    LocationOwnership,    // Who controls the location
    LocationOpportunity,  // Business opportunity there

    // NPC intel
    NPCLocation,          // Where an NPC is
    NPCStatus,            // NPC's current state
    NPCIntention,         // What NPC is planning
    NPCVulnerability,     // NPC weakness to exploit

    // Faction intel
    FactionMovement,      // Faction expanding/contracting
    FactionStrength,      // Faction resource level
    FactionPlans,         // What faction is planning

    // Threat intel
    ThreatWarning,        // Danger coming
    FedActivity,          // Federal investigation
    RivalPlot             // Enemy planning attack
}

/// <summary>
/// Manages all gathered intelligence.
/// Supports queries by subject, type, recency, and reliability.
/// </summary>
public class IntelRegistry
{
    private readonly List<Intel> _allIntel = new();
    private readonly Dictionary<string, List<Intel>> _bySubject = new();
    private readonly Dictionary<IntelType, List<Intel>> _byType = new();

    public void Add(Intel intel)
    {
        _allIntel.Add(intel);

        if (!_bySubject.ContainsKey(intel.SubjectId))
            _bySubject[intel.SubjectId] = new List<Intel>();
        _bySubject[intel.SubjectId].Add(intel);

        if (!_byType.ContainsKey(intel.Type))
            _byType[intel.Type] = new List<Intel>();
        _byType[intel.Type].Add(intel);
    }

    public IEnumerable<Intel> GetForSubject(string subjectId, int currentWeek) =>
        _bySubject.TryGetValue(subjectId, out var list)
            ? list.Where(i => !i.IsExpired(currentWeek))
            : Enumerable.Empty<Intel>();

    public IEnumerable<Intel> GetByType(IntelType type, int currentWeek) =>
        _byType.TryGetValue(type, out var list)
            ? list.Where(i => !i.IsExpired(currentWeek))
            : Enumerable.Empty<Intel>();

    public IEnumerable<Intel> GetRecent(int weeks, int currentWeek) =>
        _allIntel.Where(i =>
            !i.IsExpired(currentWeek) &&
            (currentWeek - i.GatheredWeek) <= weeks);

    public IEnumerable<Intel> GetReliable(int currentWeek) =>
        _allIntel.Where(i => !i.IsExpired(currentWeek) && i.IsReliable);

    public IEnumerable<Intel> GetUnprocessed() =>
        _allIntel.Where(i => !i.IsProcessed);

    public Intel? GetMostRecentForSubject(string subjectId, IntelType type, int currentWeek)
    {
        return GetForSubject(subjectId, currentWeek)
            .Where(i => i.Type == type)
            .OrderByDescending(i => i.GatheredWeek)
            .ThenByDescending(i => i.Reliability)
            .FirstOrDefault();
    }

    /// <summary>
    /// Clean up expired intel to prevent unbounded growth.
    /// </summary>
    public void PruneExpired(int currentWeek)
    {
        var expired = _allIntel.Where(i => i.IsExpired(currentWeek)).ToList();
        foreach (var intel in expired)
        {
            _allIntel.Remove(intel);
            if (_bySubject.TryGetValue(intel.SubjectId, out var bySubject))
                bySubject.Remove(intel);
            if (_byType.TryGetValue(intel.Type, out var byType))
                byType.Remove(intel);
        }
    }
}

#endregion

#region Persona System

/// <summary>
/// Personality traits that influence how an agent/NPC behaves and communicates.
/// Personas make characters feel distinct and drive emergent behavior.
///
/// DESIGN DECISION: Using a trait-based system with normalized values (0-100).
/// This allows for:
/// - Rules engine conditions based on traits
/// - Blending traits for nuanced behavior
/// - Character evolution over time
/// </summary>
public class Persona
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";              // "The Cautious Advisor"

    // === Core Personality Traits (0-100) ===

    // How they approach risk and opportunity
    public int Ambition { get; set; } = 50;             // Drive for power/wealth
    public int Caution { get; set; } = 50;              // Risk aversion
    public int Aggression { get; set; } = 50;           // Tendency toward violence

    // How they relate to others
    public int Loyalty { get; set; } = 50;              // Commitment to allies
    public int Trust { get; set; } = 50;                // Willingness to trust others
    public int Empathy { get; set; } = 50;              // Concern for others' welfare

    // How they operate
    public int Cunning { get; set; } = 50;              // Strategic thinking
    public int Patience { get; set; } = 50;             // Long-term vs short-term focus
    public int Pride { get; set; } = 50;                // Sensitivity to disrespect

    // === Communication Style ===
    public CommunicationStyle Style { get; set; } = CommunicationStyle.Neutral;
    public int Verbosity { get; set; } = 50;            // How much they talk (0=terse, 100=verbose)
    public int Honesty { get; set; } = 50;              // How truthful in communications

    // === Goals and Motivations ===
    public List<Goal> Goals { get; set; } = new();
    public List<string> Fears { get; set; } = new();    // What they want to avoid
    public List<string> Values { get; set; } = new();   // What they care about

    // === Biases (affect how they perceive others) ===
    public Dictionary<string, int> FactionBiases { get; set; } = new();  // Faction ID -> bias
    public Dictionary<string, int> RoleBiases { get; set; } = new();     // Role -> bias

    // === Computed Traits ===
    public bool IsAmbitious => Ambition > 70;
    public bool IsCautious => Caution > 70;
    public bool IsAggressive => Aggression > 70;
    public bool IsLoyal => Loyalty > 70;
    public bool IsTrusting => Trust > 70;
    public bool IsCunning => Cunning > 70;
    public bool IsPatient => Patience > 70;
    public bool IsProud => Pride > 70;

    /// <summary>
    /// Calculate how this persona would react to a situation.
    /// Returns a bias value that can modify rule evaluations.
    /// </summary>
    public float GetReactionBias(string situationType)
    {
        return situationType switch
        {
            "opportunity" => (Ambition - Caution) / 100f,
            "threat" => (Aggression - Patience) / 100f,
            "betrayal" => (Pride + (100 - Trust)) / 200f,
            "alliance" => (Loyalty + Trust) / 200f,
            "negotiation" => (Cunning + Patience) / 200f,
            _ => 0f
        };
    }

    /// <summary>
    /// Modify persona traits based on significant events.
    /// Characters evolve over time.
    /// </summary>
    public void ApplyExperience(string experienceType, int intensity)
    {
        switch (experienceType)
        {
            case "betrayed":
                Trust = Math.Max(0, Trust - intensity);
                Caution = Math.Min(100, Caution + intensity / 2);
                break;
            case "success":
                Ambition = Math.Min(100, Ambition + intensity / 3);
                break;
            case "failure":
                Caution = Math.Min(100, Caution + intensity / 2);
                Pride = Math.Max(0, Pride - intensity / 3);
                break;
            case "helped":
                Trust = Math.Min(100, Trust + intensity / 2);
                Loyalty = Math.Min(100, Loyalty + intensity / 3);
                break;
            case "threatened":
                Aggression = Math.Min(100, Aggression + intensity / 2);
                Trust = Math.Max(0, Trust - intensity / 2);
                break;
        }
    }
}

public enum CommunicationStyle
{
    Neutral,          // Default balanced style
    Formal,           // Professional, respectful
    Casual,           // Relaxed, friendly
    Threatening,      // Intimidating, aggressive
    Diplomatic,       // Careful, measured
    Cryptic,          // Indirect, mysterious
    Blunt             // Direct, no nonsense
}

public class Goal
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public GoalType Type { get; set; }
    public int Priority { get; set; } = 50;             // 0-100
    public string? TargetId { get; set; }               // Optional target entity
    public bool IsAchieved { get; set; }
    public bool IsFailed { get; set; }
}

public enum GoalType
{
    Survive,          // Stay alive
    Prosper,          // Gain wealth
    Power,            // Gain influence/rank
    Revenge,          // Hurt specific target
    Protect,          // Keep someone safe
    Escape,           // Leave current situation
    Loyalty,          // Serve a master
    Justice,          // Right a wrong
    Knowledge         // Learn something
}

#endregion

#region Memory System

/// <summary>
/// A single memory representing something an agent/NPC knows or experienced.
///
/// DESIGN DECISION: Memories are typed and have salience (importance).
/// High-salience memories persist longer and influence behavior more.
/// This mimics how humans remember significant events better.
/// </summary>
public class Memory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MemoryType Type { get; set; }

    // Content
    public string Summary { get; set; } = "";           // Human-readable summary
    public Dictionary<string, object> Data { get; set; } = new();

    // Context
    public int CreatedWeek { get; set; }
    public string? LocationId { get; set; }
    public string? InvolvesEntityId { get; set; }       // Who/what is this about
    public string? SourceAgentId { get; set; }          // Who told us (if learned)

    // Importance and decay
    public int Salience { get; set; } = 50;             // 0-100, how important
    public int AccessCount { get; set; }                // Times recalled
    public int? LastAccessedWeek { get; set; }

    // Emotional coloring
    public EmotionalValence Emotion { get; set; } = EmotionalValence.Neutral;
    public int EmotionalIntensity { get; set; } = 50;   // 0-100

    // Reliability
    public bool IsFirsthand { get; set; }               // Did we witness it?
    public int Confidence { get; set; } = 100;          // How sure are we?

    /// <summary>
    /// Calculate effective salience considering recency and access.
    /// </summary>
    public float GetEffectiveSalience(int currentWeek)
    {
        float base_salience = Salience / 100f;

        // Recency bonus (recent memories more accessible)
        int weeksSince = currentWeek - CreatedWeek;
        float recencyFactor = 1f / (1f + weeksSince * 0.1f);

        // Access bonus (frequently recalled memories stronger)
        float accessFactor = 1f + Math.Min(AccessCount * 0.1f, 0.5f);

        // Emotional memories persist better
        float emotionFactor = 1f + (EmotionalIntensity / 200f);

        return base_salience * recencyFactor * accessFactor * emotionFactor;
    }

    /// <summary>
    /// Should this memory be forgotten (pruned)?
    /// </summary>
    public bool ShouldForget(int currentWeek)
    {
        // High salience memories never forgotten
        if (Salience > 80) return false;

        // Emotional memories persist longer
        if (EmotionalIntensity > 70) return false;

        // Calculate effective salience
        float effective = GetEffectiveSalience(currentWeek);

        // Forget if effective salience drops too low
        return effective < 0.1f;
    }
}

public enum MemoryType
{
    // Episodic - specific events witnessed or experienced
    Interaction,      // Met/talked with someone
    Event,            // Something happened
    Mission,          // Completed a task
    Betrayal,         // Someone betrayed us
    Gift,             // Received something valuable
    Threat,           // Were threatened
    Violence,         // Witnessed/experienced violence

    // Semantic - facts and knowledge
    Fact,             // Learned information
    Location,         // Know about a place
    Person,           // Know about someone
    Relationship,     // Know about a relationship
    Secret,           // Confidential information

    // Procedural - how to do things
    Skill,            // Learned ability
    Contact,          // Know how to reach someone
    Route             // Know how to get somewhere
}

public enum EmotionalValence
{
    VeryNegative = -2,
    Negative = -1,
    Neutral = 0,
    Positive = 1,
    VeryPositive = 2
}

/// <summary>
/// Manages memories for an agent or NPC.
/// Supports recall, learning, and natural forgetting.
///
/// ALGORITHM NOTES:
/// - Recall uses salience-weighted retrieval
/// - Learning strengthens related memories
/// - Forgetting uses threshold-based pruning
/// </summary>
public class MemoryBank
{
    private readonly List<Memory> _memories = new();
    private readonly Dictionary<string, List<Memory>> _byEntity = new();
    private readonly Dictionary<string, List<Memory>> _byLocation = new();
    private readonly Dictionary<MemoryType, List<Memory>> _byType = new();

    public int Capacity { get; set; } = 100;            // Max memories before forced pruning

    #region Learning (Adding Memories)

    public void Remember(Memory memory)
    {
        _memories.Add(memory);

        // Index by entity
        if (memory.InvolvesEntityId != null)
        {
            if (!_byEntity.ContainsKey(memory.InvolvesEntityId))
                _byEntity[memory.InvolvesEntityId] = new List<Memory>();
            _byEntity[memory.InvolvesEntityId].Add(memory);
        }

        // Index by location
        if (memory.LocationId != null)
        {
            if (!_byLocation.ContainsKey(memory.LocationId))
                _byLocation[memory.LocationId] = new List<Memory>();
            _byLocation[memory.LocationId].Add(memory);
        }

        // Index by type
        if (!_byType.ContainsKey(memory.Type))
            _byType[memory.Type] = new List<Memory>();
        _byType[memory.Type].Add(memory);

        // Prune if over capacity
        if (_memories.Count > Capacity)
            PruneLeastImportant(1);
    }

    /// <summary>
    /// Learn a fact from another agent (secondhand memory).
    /// </summary>
    public void LearnFrom(Memory sourceMemory, string sourceAgentId, int currentWeek)
    {
        var learned = new Memory
        {
            Type = sourceMemory.Type,
            Summary = sourceMemory.Summary,
            Data = new Dictionary<string, object>(sourceMemory.Data),
            CreatedWeek = currentWeek,
            LocationId = sourceMemory.LocationId,
            InvolvesEntityId = sourceMemory.InvolvesEntityId,
            SourceAgentId = sourceAgentId,
            Salience = sourceMemory.Salience / 2,       // Secondhand less salient
            IsFirsthand = false,
            Confidence = sourceMemory.Confidence - 20,  // Less confident in secondhand
            Emotion = EmotionalValence.Neutral          // Secondhand less emotional
        };

        Remember(learned);
    }

    #endregion

    #region Recall (Retrieving Memories)

    /// <summary>
    /// Recall memories about a specific entity, sorted by relevance.
    /// </summary>
    public IEnumerable<Memory> RecallAbout(string entityId, int currentWeek, int limit = 5)
    {
        if (!_byEntity.TryGetValue(entityId, out var memories))
            return Enumerable.Empty<Memory>();

        return memories
            .OrderByDescending(m => m.GetEffectiveSalience(currentWeek))
            .Take(limit)
            .Select(m => {
                m.AccessCount++;
                m.LastAccessedWeek = currentWeek;
                return m;
            });
    }

    /// <summary>
    /// Recall memories at a specific location.
    /// </summary>
    public IEnumerable<Memory> RecallAtLocation(string locationId, int currentWeek, int limit = 5)
    {
        if (!_byLocation.TryGetValue(locationId, out var memories))
            return Enumerable.Empty<Memory>();

        return memories
            .OrderByDescending(m => m.GetEffectiveSalience(currentWeek))
            .Take(limit)
            .Select(m => {
                m.AccessCount++;
                m.LastAccessedWeek = currentWeek;
                return m;
            });
    }

    /// <summary>
    /// Recall memories of a specific type.
    /// </summary>
    public IEnumerable<Memory> RecallByType(MemoryType type, int currentWeek, int limit = 5)
    {
        if (!_byType.TryGetValue(type, out var memories))
            return Enumerable.Empty<Memory>();

        return memories
            .OrderByDescending(m => m.GetEffectiveSalience(currentWeek))
            .Take(limit);
    }

    /// <summary>
    /// Search memories by keyword in summary.
    /// </summary>
    public IEnumerable<Memory> Search(string keyword, int currentWeek, int limit = 5)
    {
        return _memories
            .Where(m => m.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.GetEffectiveSalience(currentWeek))
            .Take(limit);
    }

    /// <summary>
    /// Get the most emotionally significant memories.
    /// </summary>
    public IEnumerable<Memory> RecallEmotional(int currentWeek, int limit = 5)
    {
        return _memories
            .Where(m => m.EmotionalIntensity > 50)
            .OrderByDescending(m => m.EmotionalIntensity * m.GetEffectiveSalience(currentWeek))
            .Take(limit);
    }

    /// <summary>
    /// Check if we have any memory about an entity.
    /// </summary>
    public bool KnowsAbout(string entityId) => _byEntity.ContainsKey(entityId);

    /// <summary>
    /// Get overall sentiment toward an entity based on memories.
    /// </summary>
    public int GetSentiment(string entityId, int currentWeek)
    {
        if (!_byEntity.TryGetValue(entityId, out var memories))
            return 0;

        float totalSentiment = 0;
        float totalWeight = 0;

        foreach (var memory in memories)
        {
            float weight = memory.GetEffectiveSalience(currentWeek);
            float sentiment = (int)memory.Emotion * (memory.EmotionalIntensity / 50f);
            totalSentiment += sentiment * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? (int)(totalSentiment / totalWeight * 25) : 0;
    }

    #endregion

    #region Forgetting (Pruning)

    /// <summary>
    /// Forget old, low-salience memories.
    /// </summary>
    public void Forget(int currentWeek)
    {
        var toForget = _memories.Where(m => m.ShouldForget(currentWeek)).ToList();
        foreach (var memory in toForget)
            RemoveMemory(memory);
    }

    private void PruneLeastImportant(int count)
    {
        var toPrune = _memories
            .OrderBy(m => m.Salience)
            .ThenBy(m => m.CreatedWeek)
            .Take(count)
            .ToList();

        foreach (var memory in toPrune)
            RemoveMemory(memory);
    }

    private void RemoveMemory(Memory memory)
    {
        _memories.Remove(memory);

        if (memory.InvolvesEntityId != null && _byEntity.TryGetValue(memory.InvolvesEntityId, out var byEntity))
            byEntity.Remove(memory);

        if (memory.LocationId != null && _byLocation.TryGetValue(memory.LocationId, out var byLocation))
            byLocation.Remove(memory);

        if (_byType.TryGetValue(memory.Type, out var byType))
            byType.Remove(memory);
    }

    #endregion
}

#endregion

#region Agent Communication Protocol (Q&A)

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

public enum QuestionType
{
    // Information seeking
    WhatDoYouKnow,        // "What do you know about [entity]?"
    WhereIs,              // "Where is [entity]?"
    WhoControls,          // "Who controls [location]?"
    WhatHappened,         // "What happened at [location]?"
    HowIsRelationship,    // "How are things with [entity]?"

    // Opinion seeking
    WhatDoYouThink,       // "What do you think about [entity]?"
    ShouldWe,             // "Should we [action]?"
    CanWeTrust,           // "Can we trust [entity]?"

    // Action requests
    WillYouHelp,          // "Will you help with [action]?"
    CanYouFind,           // "Can you find out about [topic]?"
    WillYouTell,          // "Will you tell [entity] about [topic]?"

    // Loyalty tests
    WhereDoYouStand,      // "Where do your loyalties lie?"
    WouldYouBetray,       // Testing if they'd betray someone
    WhatWouldYouDo        // Hypothetical situation test
}

public enum QuestionUrgency
{
    Low,                  // Casual inquiry
    Normal,               // Standard question
    High,                 // Important, needs quick answer
    Critical              // Urgent, immediate response needed
}

/// <summary>
/// Response to a question, crafted based on persona and knowledge.
/// </summary>
public class AgentResponse
{
    public string QuestionId { get; set; } = "";
    public string ResponderId { get; set; } = "";
    public ResponseType Type { get; set; }

    // Content
    public string Content { get; set; } = "";           // The actual response
    public bool IsHonest { get; set; } = true;          // Is this truthful?
    public int ConfidenceLevel { get; set; } = 100;     // How confident in the info

    // Shared information
    public List<Memory> SharedMemories { get; set; } = new();
    public Intel? SharedIntel { get; set; }

    // Relationship effects
    public int RelationshipChange { get; set; }         // How this affects relationship
    public bool RefusedToAnswer { get; set; }
    public string? RefusalReason { get; set; }

    // Story effects
    public List<string> UnlockedNodeIds { get; set; } = new();   // Story nodes unlocked
    public List<string> TriggeredEvents { get; set; } = new();    // Events triggered
}

public enum ResponseType
{
    Answer,               // Direct answer to the question
    Partial,              // Incomplete information
    Redirect,             // "Ask someone else"
    Refuse,               // Won't answer
    Lie,                  // Deliberate misinformation
    Counter,              // Answers with a question
    Bargain               // "I'll tell you if..."
}

/// <summary>
/// Handles the Q&A protocol between agents.
/// Uses persona and memories to generate contextual responses.
///
/// ALGORITHM:
/// 1. Receive question
/// 2. Check relationship with asker (trust level)
/// 3. Search memories for relevant information
/// 4. Apply persona to decide response style
/// 5. Generate response (honest, partial, lie, refuse)
/// 6. Update memories and relationships
/// </summary>
public class ConversationEngine
{
    private readonly WorldState _world;
    private readonly StoryGraph _graph;

    public ConversationEngine(WorldState world, StoryGraph graph)
    {
        _world = world;
        _graph = graph;
    }

    /// <summary>
    /// Process a question and generate a response.
    /// </summary>
    public AgentResponse ProcessQuestion(
        AgentQuestion question,
        Persona responderPersona,
        MemoryBank responderMemories,
        int relationshipWithAsker)
    {
        var response = new AgentResponse
        {
            QuestionId = question.Id,
            ResponderId = question.ResponderId
        };

        // 1. Decide if we'll answer at all
        var willAnswer = DecideToAnswer(question, responderPersona, relationshipWithAsker);
        if (!willAnswer.answer)
        {
            response.Type = ResponseType.Refuse;
            response.RefusedToAnswer = true;
            response.RefusalReason = willAnswer.reason;
            response.Content = GenerateRefusal(responderPersona, willAnswer.reason);
            response.RelationshipChange = -5;  // Refusal hurts relationship
            return response;
        }

        // 2. Gather relevant information from memories
        var relevantMemories = GatherRelevantMemories(
            question,
            responderMemories,
            _world.CurrentWeek);

        // 3. Decide honesty level based on persona and relationship
        var honestyDecision = DecideHonesty(
            question,
            responderPersona,
            relationshipWithAsker,
            relevantMemories.Any());

        // 4. Generate the response
        if (honestyDecision.willLie)
        {
            response = GenerateLie(question, responderPersona, honestyDecision.lieReason);
        }
        else if (relevantMemories.Any())
        {
            response = GenerateHonestAnswer(question, responderPersona, relevantMemories);
        }
        else
        {
            response = GenerateUnknownResponse(question, responderPersona);
        }

        // 5. Apply relationship effects
        response.RelationshipChange = CalculateRelationshipChange(
            question,
            response,
            responderPersona);

        // 6. Check for story triggers
        CheckStoryTriggers(question, response);

        return response;
    }

    private (bool answer, string? reason) DecideToAnswer(
        AgentQuestion question,
        Persona persona,
        int relationship)
    {
        // Low trust + sensitive question = refuse
        if (relationship < -30 && question.Type == QuestionType.CanWeTrust)
            return (false, "I don't discuss such matters with you.");

        // Proud persona refuses to answer demands
        if (persona.IsProud && question.Urgency == QuestionUrgency.Critical)
            return (false, "Don't presume to demand answers from me.");

        // Cautious persona refuses to share dangerous info
        if (persona.IsCautious && question.RequiresHonesty)
        {
            if (question.Type == QuestionType.WhoControls ||
                question.Type == QuestionType.WhereDoYouStand)
                return (false, "Some things are better left unsaid.");
        }

        // Low relationship + loyalty questions
        if (relationship < 0 && question.Type == QuestionType.WillYouHelp)
            return (false, "Why would I help you?");

        return (true, null);
    }

    private List<Memory> GatherRelevantMemories(
        AgentQuestion question,
        MemoryBank memories,
        int currentWeek)
    {
        var relevant = new List<Memory>();

        // Search by entity
        if (question.SubjectEntityId != null)
        {
            relevant.AddRange(memories.RecallAbout(question.SubjectEntityId, currentWeek, 3));
        }

        // Search by location
        if (question.SubjectLocationId != null)
        {
            relevant.AddRange(memories.RecallAtLocation(question.SubjectLocationId, currentWeek, 3));
        }

        // Search by topic keyword
        if (!string.IsNullOrEmpty(question.Topic))
        {
            relevant.AddRange(memories.Search(question.Topic, currentWeek, 2));
        }

        return relevant.Distinct().ToList();
    }

    private (bool willLie, string? lieReason) DecideHonesty(
        AgentQuestion question,
        Persona persona,
        int relationship,
        bool hasInformation)
    {
        // Very low honesty trait = likely to lie
        if (persona.Honesty < 30 && hasInformation)
        {
            // Cunning persona lies strategically
            if (persona.IsCunning)
                return (true, "strategic_advantage");
        }

        // Negative relationship + sensitive topic = might lie
        if (relationship < -20 && question.Type == QuestionType.WhatDoYouKnow)
        {
            // Roll against honesty
            var lieChance = (100 - persona.Honesty) / 100f * (Math.Abs(relationship) / 100f);
            if (Random.Shared.NextDouble() < lieChance)
                return (true, "distrust");
        }

        // Loyalty to someone else might cause lies
        if (persona.IsLoyal && question.SubjectEntityId != null)
        {
            // Check if subject is someone we're loyal to
            var factionBias = persona.FactionBiases.GetValueOrDefault(question.SubjectEntityId);
            if (factionBias > 50 && relationship < 30)
                return (true, "protecting_ally");
        }

        return (false, null);
    }

    private AgentResponse GenerateHonestAnswer(
        AgentQuestion question,
        Persona persona,
        List<Memory> memories)
    {
        var response = new AgentResponse
        {
            QuestionId = question.Id,
            ResponderId = question.AskerId,  // Will be fixed by caller
            Type = ResponseType.Answer,
            IsHonest = true,
            SharedMemories = memories,
            ConfidenceLevel = memories.Max(m => m.Confidence)
        };

        // Generate content based on persona style
        response.Content = FormatResponse(question, memories, persona);

        return response;
    }

    private AgentResponse GenerateLie(
        AgentQuestion question,
        Persona persona,
        string? lieReason)
    {
        return new AgentResponse
        {
            QuestionId = question.Id,
            Type = ResponseType.Lie,
            IsHonest = false,
            Content = GenerateMisinformation(question, persona),
            ConfidenceLevel = 80  // Lies are delivered confidently
        };
    }

    private AgentResponse GenerateUnknownResponse(
        AgentQuestion question,
        Persona persona)
    {
        var response = new AgentResponse
        {
            QuestionId = question.Id,
            Type = ResponseType.Partial,
            IsHonest = true,
            ConfidenceLevel = 0
        };

        response.Content = persona.Style switch
        {
            CommunicationStyle.Blunt => "I don't know.",
            CommunicationStyle.Formal => "I regret that I have no information on this matter.",
            CommunicationStyle.Diplomatic => "That's not something I have insight into, I'm afraid.",
            CommunicationStyle.Cryptic => "Some things remain hidden, even from me.",
            _ => "I'm not sure about that."
        };

        return response;
    }

    private string GenerateRefusal(Persona persona, string? reason)
    {
        return persona.Style switch
        {
            CommunicationStyle.Threatening => "You don't want to keep asking questions like that.",
            CommunicationStyle.Formal => "I must decline to discuss this matter.",
            CommunicationStyle.Blunt => "No.",
            CommunicationStyle.Cryptic => "Not all questions deserve answers.",
            _ => reason ?? "I'd rather not say."
        };
    }

    private string GenerateMisinformation(AgentQuestion question, Persona persona)
    {
        // Generate plausible but false information
        return question.Type switch
        {
            QuestionType.WhereIs => "Last I heard, they were in Brooklyn.",
            QuestionType.WhoControls => "The Barzinis have that territory now.",
            QuestionType.WhatHappened => "Nothing unusual that I know of.",
            QuestionType.CanWeTrust => "Absolutely, they're completely reliable.",
            _ => "I've heard differently, but I can't say more."
        };
    }

    private string FormatResponse(AgentQuestion question, List<Memory> memories, Persona persona)
    {
        // Build response from memories, styled by persona
        var info = memories.FirstOrDefault();
        if (info == null) return "I'm not certain.";

        var prefix = persona.Style switch
        {
            CommunicationStyle.Formal => "I can confirm that ",
            CommunicationStyle.Casual => "Yeah, so ",
            CommunicationStyle.Cryptic => "What I can tell you is ",
            CommunicationStyle.Blunt => "",
            _ => ""
        };

        return prefix + info.Summary;
    }

    private int CalculateRelationshipChange(
        AgentQuestion question,
        AgentResponse response,
        Persona persona)
    {
        int change = 0;

        // Honest helpful answers improve relationship
        if (response.IsHonest && response.Type == ResponseType.Answer)
            change += 5;

        // Lies damage relationship if discovered (handled elsewhere)

        // High urgency questions answered = bigger impact
        if (question.Urgency == QuestionUrgency.Critical && response.Type == ResponseType.Answer)
            change += 10;

        return change;
    }

    private void CheckStoryTriggers(AgentQuestion question, AgentResponse response)
    {
        // Certain Q&A combinations unlock story nodes

        // Example: Asking about the informant reveals the "rat hunt" plot
        if (question.Type == QuestionType.WhatDoYouKnow &&
            response.IsHonest &&
            response.SharedMemories.Any(m => m.Type == MemoryType.Secret))
        {
            // Could trigger story events
            var secretMemory = response.SharedMemories.First(m => m.Type == MemoryType.Secret);
            if (secretMemory.Summary.Contains("informant", StringComparison.OrdinalIgnoreCase))
            {
                response.TriggeredEvents.Add("informant_discovered");
            }
        }
    }
}

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

#endregion

#region Consequence System

/// <summary>
/// Context for evaluating consequence rules.
/// Passed to the RulesEngine after mission completion.
/// </summary>
public class ConsequenceContext
{
    // Mission info
    public string MissionId { get; set; } = "";
    public string MissionType { get; set; } = "";
    public bool Success { get; set; }

    // Involved entities
    public Location? Location { get; set; }
    public NPC? TargetNPC { get; set; }
    public Faction? TargetFaction { get; set; }

    // World access
    public WorldState World { get; set; } = null!;
    public StoryGraph Graph { get; set; } = null!;

    // Player state
    public int PlayerRespect { get; set; }
    public int PlayerHeat { get; set; }

    // For rule actions to record what they did
    public List<string> AppliedConsequences { get; set; } = new();
}

/// <summary>
/// Defines consequences using the RulesEngine pattern.
/// Each rule checks a condition and applies world state changes.
///
/// EXAMPLE CONSEQUENCES:
/// - Intimidation success: NPC becomes Intimidated, relationship -20
/// - Intimidation fail: NPC becomes Hostile, may trigger revenge mission
/// - Collection from allied NPC: Relationship +5, loyalty bonus
/// - Hit success: Target NPC dies, faction hostility +30
/// </summary>
public static class ConsequenceRulesSetup
{
    public static void RegisterConsequenceRules(RulesEngineCore<ConsequenceContext> engine)
    {
        // === INTIMIDATION CONSEQUENCES ===

        engine.AddRule(
            "CONSEQUENCE_INTIMIDATION_SUCCESS",
            "Successful Intimidation",
            ctx => ctx.MissionType == "Intimidation" && ctx.Success && ctx.TargetNPC != null,
            ctx => {
                ctx.TargetNPC!.Status = NPCStatus.Intimidated;
                ctx.TargetNPC.Relationship -= 20;
                ctx.TargetNPC.Relationship = Math.Max(-100, ctx.TargetNPC.Relationship);
                if (ctx.Location != null)
                    ctx.Location.State = LocationState.Friendly;
                ctx.AppliedConsequences.Add($"{ctx.TargetNPC.Name} is now intimidated");
            },
            priority: 100);

        engine.AddRule(
            "CONSEQUENCE_INTIMIDATION_FAIL",
            "Failed Intimidation",
            ctx => ctx.MissionType == "Intimidation" && !ctx.Success && ctx.TargetNPC != null,
            ctx => {
                ctx.TargetNPC!.Status = NPCStatus.Hostile;
                ctx.TargetNPC.Relationship -= 40;
                ctx.TargetNPC.Relationship = Math.Max(-100, ctx.TargetNPC.Relationship);
                // Unlock revenge mission
                ctx.Graph.AddNode(new StoryNode
                {
                    Id = $"revenge-{ctx.TargetNPC.Id}",
                    Title = $"{ctx.TargetNPC.Name} Seeks Revenge",
                    Type = StoryNodeType.Threat,
                    IsUnlocked = true,
                    UnlockedAtWeek = ctx.World.CurrentWeek,
                    NPCId = ctx.TargetNPC.Id,
                    ExpiresAfterWeeks = 8
                });
                ctx.AppliedConsequences.Add($"{ctx.TargetNPC.Name} is now hostile and may seek revenge");
            },
            priority: 100);

        // === COLLECTION CONSEQUENCES ===

        engine.AddRule(
            "CONSEQUENCE_COLLECTION_ALLY",
            "Collection from Ally",
            ctx => ctx.MissionType == "Collection" && ctx.Success &&
                   ctx.TargetNPC != null && ctx.TargetNPC.IsAlly,
            ctx => {
                ctx.TargetNPC!.Relationship += 5;  // Appreciates smooth business
                ctx.TargetNPC.Relationship = Math.Min(100, ctx.TargetNPC.Relationship);
                ctx.AppliedConsequences.Add($"{ctx.TargetNPC.Name} appreciates the professional approach");
            },
            priority: 90);

        engine.AddRule(
            "CONSEQUENCE_COLLECTION_FAIL",
            "Failed Collection",
            ctx => ctx.MissionType == "Collection" && !ctx.Success,
            ctx => {
                if (ctx.Location != null)
                    ctx.Location.LocalHeat += 10;  // Disturbance attracted attention
                if (ctx.TargetNPC != null)
                    ctx.TargetNPC.Relationship -= 10;  // They don't respect failure
                ctx.AppliedConsequences.Add("Failed collection attracted attention");
            },
            priority: 90);

        // === HIT CONSEQUENCES ===

        engine.AddRule(
            "CONSEQUENCE_HIT_SUCCESS",
            "Successful Hit",
            ctx => ctx.MissionType == "Hit" && ctx.Success && ctx.TargetNPC != null,
            ctx => {
                ctx.TargetNPC!.Status = NPCStatus.Dead;
                if (ctx.TargetFaction != null)
                {
                    ctx.TargetFaction.Hostility += 30;
                    ctx.TargetFaction.Hostility = Math.Min(100, ctx.TargetFaction.Hostility);
                    ctx.TargetFaction.Resources -= 10;  // Lost a member
                }
                ctx.AppliedConsequences.Add($"{ctx.TargetNPC.Name} has been eliminated");
            },
            priority: 100);

        // === INFORMATION CONSEQUENCES ===

        engine.AddRule(
            "CONSEQUENCE_INTEL_GATHERED",
            "Intel Gathered",
            ctx => ctx.MissionType == "Information" && ctx.Success,
            ctx => {
                // Add intel to registry (done separately)
                if (ctx.Location != null)
                    ctx.Location.TimesVisited++;
                ctx.AppliedConsequences.Add("Gathered valuable intelligence");
            },
            priority: 80);

        // === NEGOTIATION CONSEQUENCES ===

        engine.AddRule(
            "CONSEQUENCE_NEGOTIATION_SUCCESS",
            "Successful Negotiation",
            ctx => ctx.MissionType == "Negotiation" && ctx.Success && ctx.TargetFaction != null,
            ctx => {
                ctx.TargetFaction!.Hostility -= 20;
                ctx.TargetFaction.Hostility = Math.Max(0, ctx.TargetFaction.Hostility);
                ctx.TargetFaction.HasTruce = true;
                ctx.TargetFaction.TruceExpiresWeek = ctx.World.CurrentWeek + 12;
                ctx.AppliedConsequences.Add($"Negotiated truce with {ctx.TargetFaction.Name}");
            },
            priority: 100);

        engine.AddRule(
            "CONSEQUENCE_NEGOTIATION_FAIL",
            "Failed Negotiation",
            ctx => ctx.MissionType == "Negotiation" && !ctx.Success && ctx.TargetFaction != null,
            ctx => {
                ctx.TargetFaction!.Hostility += 15;  // Insulted by poor negotiation
                ctx.TargetFaction.Hostility = Math.Min(100, ctx.TargetFaction.Hostility);
                ctx.AppliedConsequences.Add($"Failed negotiation angered {ctx.TargetFaction.Name}");
            },
            priority: 100);

        // === TERRITORY CONSEQUENCES ===

        engine.AddRule(
            "CONSEQUENCE_TERRITORY_TAKEN",
            "Territory Captured",
            ctx => ctx.MissionType == "Territory" && ctx.Success && ctx.Location != null,
            ctx => {
                ctx.World.TransferTerritory(ctx.Location!.Id, "player");
                ctx.Location.State = LocationState.Friendly;
                if (ctx.TargetFaction != null)
                {
                    ctx.TargetFaction.Hostility += 40;
                    ctx.TargetFaction.Resources -= 15;
                }
                ctx.AppliedConsequences.Add($"Took control of {ctx.Location.Name}");
            },
            priority: 100);
    }
}

#endregion

#region Dynamic Mission Generator

/// <summary>
/// Tracks mission history to prevent repetition.
/// Uses sliding windows and decay for natural variety.
/// </summary>
public class MissionHistory
{
    private readonly Queue<string> _recentMissionTypes = new();
    private readonly Dictionary<string, int> _locationVisitCounts = new();
    private readonly Dictionary<string, int> _npcInteractionCounts = new();
    private readonly Dictionary<string, int> _lastVisitWeek = new();

    private const int TYPE_MEMORY = 5;      // Remember last 5 mission types
    private const int LOCATION_DECAY = 4;   // Location cooldown in weeks

    public void RecordMission(string missionType, string? locationId, string? npcId, int week)
    {
        // Track mission types
        _recentMissionTypes.Enqueue(missionType);
        if (_recentMissionTypes.Count > TYPE_MEMORY)
            _recentMissionTypes.Dequeue();

        // Track locations
        if (locationId != null)
        {
            _locationVisitCounts[locationId] = _locationVisitCounts.GetValueOrDefault(locationId) + 1;
            _lastVisitWeek[locationId] = week;
        }

        // Track NPCs
        if (npcId != null)
        {
            _npcInteractionCounts[npcId] = _npcInteractionCounts.GetValueOrDefault(npcId) + 1;
        }
    }

    /// <summary>
    /// Calculate a penalty score for a mission based on how repetitive it would be.
    /// Returns 0.0 (fresh) to 1.0 (very repetitive).
    /// </summary>
    public float GetRepetitionScore(string missionType, string? locationId, string? npcId, int currentWeek)
    {
        float score = 0f;

        // Penalize recently used mission types
        int typeCount = _recentMissionTypes.Count(t => t == missionType);
        score += typeCount * 0.15f;

        // Penalize recently visited locations
        if (locationId != null && _lastVisitWeek.TryGetValue(locationId, out int lastWeek))
        {
            int weeksSince = currentWeek - lastWeek;
            if (weeksSince < LOCATION_DECAY)
                score += (LOCATION_DECAY - weeksSince) * 0.1f;
        }

        // Penalize over-used NPCs
        if (npcId != null && _npcInteractionCounts.TryGetValue(npcId, out int npcCount))
        {
            score += Math.Min(npcCount * 0.05f, 0.3f);
        }

        return Math.Min(score, 1.0f);
    }

    /// <summary>
    /// Decay counts over time (call each turn).
    /// </summary>
    public void DecayCounters()
    {
        // Slowly decay location visit counts
        var locations = _locationVisitCounts.Keys.ToList();
        foreach (var loc in locations)
        {
            _locationVisitCounts[loc] = Math.Max(0, _locationVisitCounts[loc] - 1);
            if (_locationVisitCounts[loc] == 0)
                _locationVisitCounts.Remove(loc);
        }
    }
}

/// <summary>
/// Generates missions dynamically based on world state, story graph, and history.
///
/// ALGORITHM:
/// 1. Gather candidate missions from multiple sources:
///    - Story graph available missions
///    - Active plot thread missions
///    - World state opportunities (hostile NPCs, hot locations, etc.)
///    - Fallback template missions
/// 2. Score each candidate by:
///    - Relevance to current world state
///    - Repetition penalty
///    - Plot priority
///    - Player capability match
/// 3. Select highest scoring mission with some randomness for variety
/// </summary>
public class DynamicMissionGenerator
{
    private readonly WorldState _world;
    private readonly StoryGraph _graph;
    private readonly IntelRegistry _intel;
    private readonly MissionHistory _history;
    private readonly Random _random = new();

    public DynamicMissionGenerator(WorldState world, StoryGraph graph, IntelRegistry intel, MissionHistory history)
    {
        _world = world;
        _graph = graph;
        _intel = intel;
        _history = history;
    }

    public MissionCandidate GenerateMission(PlayerCharacter player)
    {
        var candidates = new List<MissionCandidate>();

        // 1. Get missions from story graph
        candidates.AddRange(GetGraphMissions());

        // 2. Get missions from active plots
        candidates.AddRange(GetPlotMissions());

        // 3. Get missions from world state
        candidates.AddRange(GetWorldStateMissions(player));

        // 4. Generate fallback missions if needed
        if (candidates.Count < 3)
        {
            candidates.AddRange(GenerateFallbackMissions(player, 3 - candidates.Count));
        }

        // Score all candidates
        foreach (var candidate in candidates)
        {
            candidate.Score = CalculateScore(candidate, player);
        }

        // Select with weighted randomness
        return SelectWeighted(candidates);
    }

    private IEnumerable<MissionCandidate> GetGraphMissions()
    {
        return _graph.GetAvailableMissions(_world).Select(node => new MissionCandidate
        {
            Source = MissionSource.StoryGraph,
            NodeId = node.Id,
            MissionType = node.Metadata.GetValueOrDefault("MissionType")?.ToString() ?? "Information",
            Title = node.Title,
            Description = node.Description,
            LocationId = node.LocationId,
            NPCId = node.NPCId,
            PlotThreadId = node.PlotThreadId,
            Priority = node.PlotThreadId != null ? 80 : 50
        });
    }

    private IEnumerable<MissionCandidate> GetPlotMissions()
    {
        foreach (var plot in _graph.GetActivePlots())
        {
            if (plot.CurrentMissionNodeId != null)
            {
                var node = _graph.GetNode(plot.CurrentMissionNodeId);
                if (node != null && node.IsUnlocked && !node.IsCompleted)
                {
                    yield return new MissionCandidate
                    {
                        Source = MissionSource.PlotThread,
                        NodeId = node.Id,
                        MissionType = node.Metadata.GetValueOrDefault("MissionType")?.ToString() ?? "Information",
                        Title = $"[{plot.Title}] {node.Title}",
                        Description = node.Description,
                        LocationId = node.LocationId,
                        NPCId = node.NPCId,
                        PlotThreadId = plot.Id,
                        Priority = plot.Priority + 20  // Plot missions get bonus
                    };
                }
            }
        }
    }

    private IEnumerable<MissionCandidate> GetWorldStateMissions(PlayerCharacter player)
    {
        // Hostile NPCs create revenge/conflict missions
        foreach (var npc in _world.GetNPCsByStatus(NPCStatus.Hostile).Take(2))
        {
            yield return new MissionCandidate
            {
                Source = MissionSource.WorldState,
                MissionType = "Intimidation",
                Title = $"Deal with {npc.Name}",
                Description = $"{npc.Name} has become a problem. Handle it.",
                LocationId = npc.LocationId,
                NPCId = npc.Id,
                Priority = 70
            };
        }

        // High-heat locations create bribe/lay-low missions
        foreach (var loc in _world.GetLocationsByState(LocationState.Compromised).Take(1))
        {
            yield return new MissionCandidate
            {
                Source = MissionSource.WorldState,
                MissionType = "Negotiation",
                Title = $"Cool down {loc.Name}",
                Description = $"{loc.Name} is too hot. Bribe officials or lay low.",
                LocationId = loc.Id,
                Priority = 60
            };
        }

        // Allied NPCs we haven't visited create relationship missions
        foreach (var npc in _world.NPCs.Values
            .Where(n => n.IsAlly && n.LastInteractionWeek < _world.CurrentWeek - 4)
            .Take(1))
        {
            yield return new MissionCandidate
            {
                Source = MissionSource.WorldState,
                MissionType = "Collection",
                Title = $"Check in with {npc.Name}",
                Description = $"It's been a while since we visited {npc.Name}. Maintain the relationship.",
                LocationId = npc.LocationId,
                NPCId = npc.Id,
                Priority = 40
            };
        }

        // Intel-driven opportunities
        var opportunities = _intel.GetByType(IntelType.LocationOpportunity, _world.CurrentWeek)
            .Where(i => i.IsReliable && !i.IsActedUpon)
            .Take(1);

        foreach (var intel in opportunities)
        {
            var loc = _world.GetLocation(intel.SubjectId);
            if (loc != null)
            {
                yield return new MissionCandidate
                {
                    Source = MissionSource.Intel,
                    MissionType = "Territory",
                    Title = $"Opportunity at {loc.Name}",
                    Description = intel.Summary,
                    LocationId = loc.Id,
                    Priority = 65
                };
            }
        }
    }

    private IEnumerable<MissionCandidate> GenerateFallbackMissions(PlayerCharacter player, int count)
    {
        var accessibleLocations = _world.GetAccessibleLocations().ToList();
        var activeNPCs = _world.GetNPCsByStatus(NPCStatus.Active).ToList();

        for (int i = 0; i < count && accessibleLocations.Count > 0; i++)
        {
            var location = accessibleLocations[_random.Next(accessibleLocations.Count)];
            var npcsHere = _world.GetNPCsAtLocation(location.Id).ToList();
            var npc = npcsHere.Count > 0 ? npcsHere[_random.Next(npcsHere.Count)] : null;

            var missionType = PickFallbackMissionType(player);

            yield return new MissionCandidate
            {
                Source = MissionSource.Generated,
                MissionType = missionType,
                Title = GenerateFallbackTitle(missionType, location, npc),
                Description = GenerateFallbackDescription(missionType, location, npc),
                LocationId = location.Id,
                NPCId = npc?.Id,
                Priority = 30
            };
        }
    }

    private string PickFallbackMissionType(PlayerCharacter player)
    {
        // Weight by what player hasn't done recently
        var types = new[] { "Collection", "Intimidation", "Information", "Negotiation" };
        var weights = types.Select(t =>
            1.0f - _history.GetRepetitionScore(t, null, null, _world.CurrentWeek)
        ).ToArray();

        float total = weights.Sum();
        float roll = (float)_random.NextDouble() * total;
        float cumulative = 0;

        for (int i = 0; i < types.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return types[i];
        }

        return types[0];
    }

    private string GenerateFallbackTitle(string missionType, Location location, NPC? npc)
    {
        return missionType switch
        {
            "Collection" => $"Collect from {location.Name}",
            "Intimidation" => npc != null
                ? $"Send a message to {npc.DisplayName}"
                : $"Show presence at {location.Name}",
            "Information" => $"Scout {location.Name}",
            "Negotiation" => npc != null
                ? $"Talk to {npc.DisplayName}"
                : $"Meet contact at {location.Name}",
            _ => $"Visit {location.Name}"
        };
    }

    private string GenerateFallbackDescription(string missionType, Location location, NPC? npc)
    {
        return missionType switch
        {
            "Collection" => $"Collect the weekly payment from {location.Name}.",
            "Intimidation" => npc != null
                ? $"{npc.DisplayName} needs to understand how things work around here."
                : $"Make our presence known at {location.Name}.",
            "Information" => $"Keep your ears open at {location.Name}. Report anything interesting.",
            "Negotiation" => npc != null
                ? $"Have a conversation with {npc.DisplayName}. Find common ground."
                : $"Meet with a contact at {location.Name}.",
            _ => $"Handle business at {location.Name}."
        };
    }

    private float CalculateScore(MissionCandidate candidate, PlayerCharacter player)
    {
        float score = candidate.Priority;

        // Apply repetition penalty
        float repetition = _history.GetRepetitionScore(
            candidate.MissionType,
            candidate.LocationId,
            candidate.NPCId,
            _world.CurrentWeek);
        score *= (1.0f - repetition * 0.5f);  // Up to 50% penalty for repetition

        // Bonus for plot missions
        if (candidate.PlotThreadId != null)
            score *= 1.2f;

        // Bonus for intel-driven missions
        if (candidate.Source == MissionSource.Intel)
            score *= 1.1f;

        // Slight random variance for variety
        score *= 0.9f + (float)_random.NextDouble() * 0.2f;

        return score;
    }

    private MissionCandidate SelectWeighted(List<MissionCandidate> candidates)
    {
        if (candidates.Count == 0)
            throw new InvalidOperationException("No mission candidates available");

        // Sort by score descending
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Pick from top 3 with weighted probability
        var top = candidates.Take(3).ToList();
        float totalScore = top.Sum(c => c.Score);
        float roll = (float)_random.NextDouble() * totalScore;
        float cumulative = 0;

        foreach (var candidate in top)
        {
            cumulative += candidate.Score;
            if (roll <= cumulative)
                return candidate;
        }

        return top[0];
    }
}

public class MissionCandidate
{
    public MissionSource Source { get; set; }
    public string? NodeId { get; set; }
    public string MissionType { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? LocationId { get; set; }
    public string? NPCId { get; set; }
    public string? PlotThreadId { get; set; }
    public int Priority { get; set; }
    public float Score { get; set; }
}

public enum MissionSource
{
    StoryGraph,   // From story node
    PlotThread,   // From active plot
    WorldState,   // From world conditions
    Intel,        // From gathered intelligence
    Generated     // Fallback generation
}

// Reusing existing PlayerCharacter from MissionSystem.cs
// public class PlayerCharacter { ... }

#endregion

#region World State Seeding

/// <summary>
/// Creates the initial world state with locations, NPCs, and factions.
/// This replaces the static mission template strings with persistent entities.
/// </summary>
public static class WorldStateSeeder
{
    public static WorldState CreateInitialWorld()
    {
        var world = new WorldState();

        // === LOCATIONS ===
        // Based on existing mission templates but now persistent

        var locations = new[]
        {
            new Location { Id = "tonys-restaurant", Name = "Tony's Restaurant", WeeklyValue = 720m },
            new Location { Id = "luigis-bakery", Name = "Luigi's Bakery", WeeklyValue = 450m },
            new Location { Id = "marinos-deli", Name = "Marino's Deli", WeeklyValue = 550m },
            new Location { Id = "sals-bar", Name = "Sal's Bar", WeeklyValue = 900m },
            new Location { Id = "vinnies-grocery", Name = "Vinnie's Grocery", WeeklyValue = 380m },
            new Location { Id = "angelos-butcher", Name = "Angelo's Butcher Shop", WeeklyValue = 480m },
            new Location { Id = "the-docks", Name = "The Docks", WeeklyValue = 2000m, LocalHeat = 20 },
            new Location { Id = "brooklyn-warehouse", Name = "Brooklyn Warehouse", WeeklyValue = 1500m },
            new Location { Id = "little-italy", Name = "Little Italy", WeeklyValue = 1200m },
            new Location { Id = "hells-kitchen", Name = "Hell's Kitchen", WeeklyValue = 800m, State = LocationState.Contested },
        };

        foreach (var loc in locations)
            world.RegisterLocation(loc);

        // === NPCs ===
        // Give persistent identities to previously anonymous targets

        var npcs = new[]
        {
            // Restaurant/Business owners
            new NPC { Id = "tony-marinelli", Name = "Tony Marinelli", Role = "restaurant_owner",
                      Title = "the restaurant owner", LocationId = "tonys-restaurant" },
            new NPC { Id = "luigi-ferrari", Name = "Luigi Ferrari", Role = "baker",
                      Title = "the baker", LocationId = "luigis-bakery" },
            new NPC { Id = "sal-benedetto", Name = "Sal Benedetto", Role = "bar_owner",
                      Title = "the bar owner", LocationId = "sals-bar" },
            new NPC { Id = "vinnie-costello", Name = "Vinnie Costello", Role = "shopkeeper",
                      Title = "the shopkeeper", LocationId = "vinnies-grocery" },
            new NPC { Id = "angelo-russo", Name = "Angelo Russo", Role = "butcher",
                      Title = "the butcher", LocationId = "angelos-butcher" },

            // Dock workers
            new NPC { Id = "jimmy-the-dock", Name = "Jimmy 'The Dock' Malone", Role = "dock_worker",
                      Title = "the dock foreman", LocationId = "the-docks" },
            new NPC { Id = "pete-longshoreman", Name = "Pete Caruso", Role = "dock_worker",
                      Title = "the longshoreman", LocationId = "the-docks" },

            // Potential informants
            new NPC { Id = "eddie-rats", Name = "Eddie 'The Rat' Falcone", Role = "informant",
                      Title = "the informant", LocationId = "little-italy", Relationship = -20 },

            // Rival associates
            new NPC { Id = "tommy-tattaglia", Name = "Tommy Tattaglia", Role = "rival_associate",
                      Title = "the Tattaglia associate", LocationId = "hells-kitchen",
                      FactionId = "tattaglia", Relationship = -50 },
        };

        foreach (var npc in npcs)
            world.RegisterNPC(npc);

        // === FACTIONS ===

        world.Factions["tattaglia"] = new Faction
        {
            Id = "tattaglia",
            Name = "Tattaglia Family",
            Hostility = 40,
            Resources = 80,
            ControlledLocationIds = new HashSet<string> { "hells-kitchen" }
        };

        world.Factions["barzini"] = new Faction
        {
            Id = "barzini",
            Name = "Barzini Family",
            Hostility = 30,
            Resources = 100,
        };

        return world;
    }

    public static StoryGraph CreateInitialGraph(WorldState world)
    {
        var graph = new StoryGraph();

        // === SEED PLOT THREADS ===

        // The Dock Thief - introductory plot
        var dockThiefPlot = new PlotThread
        {
            Id = "dock-thief",
            Title = "The Dock Thief",
            Description = "Someone's been stealing from our dock operations.",
            State = PlotState.Available,
            Priority = 60,
            MissionNodeIds = new List<string> { "dock-thief-1", "dock-thief-2", "dock-thief-3" },
            RespectReward = 25,
            MoneyReward = 2000m
        };
        graph.AddPlotThread(dockThiefPlot);

        // Add plot mission nodes
        graph.AddNode(new StoryNode
        {
            Id = "dock-thief-1",
            Title = "Investigate the Docks",
            Description = "Find out who's been stealing from our operations.",
            Type = StoryNodeType.Mission,
            PlotThreadId = "dock-thief",
            LocationId = "the-docks",
            IsUnlocked = true,
            Metadata = new Dictionary<string, object> { ["MissionType"] = "Information" }
        });

        graph.AddNode(new StoryNode
        {
            Id = "dock-thief-2",
            Title = "Confront the Thief",
            Description = "We know who it is. Time to have a conversation.",
            Type = StoryNodeType.Mission,
            PlotThreadId = "dock-thief",
            LocationId = "the-docks",
            Metadata = new Dictionary<string, object> { ["MissionType"] = "Intimidation" }
        });

        graph.AddNode(new StoryNode
        {
            Id = "dock-thief-3",
            Title = "Recover the Goods",
            Description = "Get back what was stolen and make an example.",
            Type = StoryNodeType.Mission,
            PlotThreadId = "dock-thief",
            LocationId = "brooklyn-warehouse",
            Metadata = new Dictionary<string, object> { ["MissionType"] = "Collection" }
        });

        // Link the plot missions
        graph.AddEdge(new StoryEdge { FromNodeId = "dock-thief-1", ToNodeId = "dock-thief-2", Type = StoryEdgeType.Unlocks });
        graph.AddEdge(new StoryEdge { FromNodeId = "dock-thief-2", ToNodeId = "dock-thief-3", Type = StoryEdgeType.Unlocks });

        // === DORMANT PLOTS (activated by world state) ===

        // Tattaglia Expansion - activates when they become aggressive
        var tattagliaPlot = new PlotThread
        {
            Id = "tattaglia-expansion",
            Title = "The Tattaglia Expansion",
            Description = "The Tattaglias are making moves on our territory.",
            State = PlotState.Dormant,
            Priority = 80,
            ActivationCondition = w => w.Factions["tattaglia"].Hostility > 60,
            MissionNodeIds = new List<string> { "tattaglia-1", "tattaglia-2", "tattaglia-3" },
            RespectReward = 50,
            MoneyReward = 5000m
        };
        graph.AddPlotThread(tattagliaPlot);

        return graph;
    }
}

#endregion

#region Integration Notes

/*
INTEGRATION WITH EXISTING SYSTEMS:

1. GameEngine Integration:
   - Initialize WorldState and StoryGraph at game start
   - Call StoryGraph.UpdateUnlocks() each turn
   - Call MissionHistory.DecayCounters() each turn
   - Use DynamicMissionGenerator instead of old MissionGenerator
   - Apply consequences after mission completion via RulesEngine

2. AgentRouter Integration:
   - Create IntelMessage extending AgentMessage
   - Agents generate intel in their ProcessMessageAsync
   - Intel routes through hierarchy and populates IntelRegistry
   - IntelRegistry is queried by DynamicMissionGenerator

3. RulesEngine Integration:
   - ConsequenceRulesSetup registers consequence rules
   - After mission completion, evaluate consequences
   - Consequences modify WorldState and StoryGraph

4. PlayerAgent Integration:
   - Replace MissionGenerator.GenerateMission with DynamicMissionGenerator
   - Pass WorldState to ProcessWeekAsync
   - Record missions in MissionHistory

ALGORITHM COMPLEXITY SUMMARY:
- WorldState queries: O(1) for ID lookup, O(n) for filtered queries
- StoryGraph.UpdateUnlocks: O(n) nodes + O(e) edges per turn
- DynamicMissionGenerator: O(c) where c = candidate count (~10-20)
- MissionHistory scoring: O(1) per mission
- IntelRegistry queries: O(n) where n = intel count, pruned periodically

MEMORY CONSIDERATIONS:
- WorldState: Fixed size based on world design (~50 entities)
- StoryGraph: Grows with events but nodes are small
- IntelRegistry: Pruned periodically, ~100 intel max
- MissionHistory: Fixed-size queues, bounded dictionaries
*/

#endregion
