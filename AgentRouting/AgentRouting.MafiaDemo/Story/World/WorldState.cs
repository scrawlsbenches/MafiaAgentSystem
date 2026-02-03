// Story System - World State Container
// Central container for all world state

namespace AgentRouting.MafiaDemo.Story;

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

    // Personas and memories for all entities (NPCs, player, agents)
    private readonly Dictionary<string, EntityMind> _minds = new();

    // Game time
    public int CurrentWeek { get; set; } = 1;

    #region EntityMind Registry

    /// <summary>
    /// Get or create an EntityMind for any entity (NPC, player, agent).
    /// Ensures every entity can have a persona and memories.
    /// </summary>
    public EntityMind GetMind(string entityId)
    {
        if (!_minds.TryGetValue(entityId, out var mind))
        {
            mind = new EntityMind { EntityId = entityId };
            _minds[entityId] = mind;
        }
        return mind;
    }

    /// <summary>
    /// Register an entity with a pre-configured mind.
    /// </summary>
    public void RegisterMind(string entityId, EntityMind mind)
    {
        mind.EntityId = entityId;
        _minds[entityId] = mind;
    }

    /// <summary>
    /// Check if an entity has a registered mind.
    /// </summary>
    public bool HasMind(string entityId) => _minds.ContainsKey(entityId);

    /// <summary>
    /// Get all entity minds (for serialization, iteration, etc.)
    /// </summary>
    public IEnumerable<EntityMind> GetAllMinds() => _minds.Values;

    #endregion

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
