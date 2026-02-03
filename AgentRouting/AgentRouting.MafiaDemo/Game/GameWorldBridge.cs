// GameWorldBridge - Synchronizes GameState with Story System WorldState
// Enables integration between existing game mechanics and the narrative layer

using AgentRouting.MafiaDemo.Story;

namespace AgentRouting.MafiaDemo.Game;

/// <summary>
/// Bridge between the existing GameState and the Story System's WorldState.
/// Handles bidirectional synchronization to keep both systems in sync.
///
/// DESIGN DECISION: Sync happens at turn boundaries rather than on every property change.
/// This is simpler and avoids complex change tracking. The trade-off is that intra-turn
/// changes are batched, but this matches the turn-based game model.
///
/// MAPPING:
/// - GameState.Territories → WorldState.Locations (by name matching)
/// - GameState.RivalFamilies → WorldState.Factions (by name matching)
/// - GameState.Week → WorldState.CurrentWeek
/// - GameState.HeatLevel → distributed to Location.LocalHeat
/// </summary>
public class GameWorldBridge
{
    private readonly Dictionary<string, string> _territoryToLocationMap = new();
    private readonly Dictionary<string, string> _rivalToFactionMap = new();

    /// <summary>
    /// Initialize the bridge by establishing mappings between game entities and story entities.
    /// Call this once after both GameState and WorldState are initialized.
    /// </summary>
    public void Initialize(GameState game, WorldState world)
    {
        // Map territories to locations by matching names
        foreach (var territory in game.Territories.Values)
        {
            var locationId = NormalizeToId(territory.Name);
            if (world.Locations.ContainsKey(locationId))
            {
                _territoryToLocationMap[territory.Name] = locationId;
            }
            else
            {
                // Create location for unmapped territory
                var location = new Location
                {
                    Id = locationId,
                    Name = territory.Name,
                    WeeklyValue = territory.WeeklyRevenue,
                    LocalHeat = territory.HeatGeneration,
                    OwnerId = "player",
                    State = territory.UnderDispute ? LocationState.Contested : LocationState.Friendly
                };
                world.RegisterLocation(location);
                _territoryToLocationMap[territory.Name] = locationId;
            }
        }

        // Map rival families to factions by matching names
        foreach (var rival in game.RivalFamilies.Values)
        {
            var factionId = NormalizeToId(rival.Name.Split(' ')[0]); // "Tattaglia Family" -> "tattaglia"
            if (world.Factions.ContainsKey(factionId))
            {
                _rivalToFactionMap[rival.Name] = factionId;
            }
            else
            {
                // Create faction for unmapped rival
                var faction = new Faction
                {
                    Id = factionId,
                    Name = rival.Name,
                    Hostility = rival.Hostility,
                    Resources = rival.Strength,
                    AtWar = rival.AtWar
                };
                world.Factions[factionId] = faction;
                _rivalToFactionMap[rival.Name] = factionId;
            }
        }
    }

    /// <summary>
    /// Sync changes from GameState to WorldState.
    /// Call this after game state modifications (e.g., after ExecuteTurnAsync).
    /// </summary>
    public void SyncToWorldState(GameState game, WorldState world)
    {
        // Sync week counter
        world.CurrentWeek = game.Week;

        // Sync territories → locations
        foreach (var territory in game.Territories.Values)
        {
            if (_territoryToLocationMap.TryGetValue(territory.Name, out var locationId))
            {
                var location = world.GetLocation(locationId);
                if (location != null)
                {
                    // Sync heat (territory heat generation affects local heat)
                    location.LocalHeat = Math.Min(100, location.LocalHeat + territory.HeatGeneration);

                    // Sync state
                    if (territory.UnderDispute && location.State != LocationState.Contested)
                    {
                        location.State = LocationState.Contested;
                    }
                    else if (!territory.UnderDispute && location.State == LocationState.Contested)
                    {
                        location.State = LocationState.Friendly;
                    }

                    // Sync ownership
                    location.OwnerId = "player";
                }
            }
        }

        // Sync rival families → factions
        foreach (var rival in game.RivalFamilies.Values)
        {
            if (_rivalToFactionMap.TryGetValue(rival.Name, out var factionId))
            {
                var faction = world.GetFaction(factionId);
                if (faction != null)
                {
                    faction.Hostility = rival.Hostility;
                    faction.Resources = rival.Strength;
                    faction.AtWar = rival.AtWar;
                }
            }
        }

        // Distribute global heat to locations (weighted by territory heat generation)
        DistributeHeatToLocations(game, world);
    }

    /// <summary>
    /// Sync changes from WorldState back to GameState.
    /// Call this to propagate story system changes to game mechanics.
    /// </summary>
    public void SyncFromWorldState(WorldState world, GameState game)
    {
        // Sync week counter
        game.Week = world.CurrentWeek;

        // Sync locations → territories
        foreach (var (territoryName, locationId) in _territoryToLocationMap)
        {
            var location = world.GetLocation(locationId);
            if (location != null && game.Territories.TryGetValue(GetTerritoryKey(territoryName), out var territory))
            {
                territory.UnderDispute = location.State == LocationState.Contested;
            }
        }

        // Sync factions → rival families
        foreach (var (rivalName, factionId) in _rivalToFactionMap)
        {
            var faction = world.GetFaction(factionId);
            if (faction != null)
            {
                var rival = game.RivalFamilies.Values.FirstOrDefault(r => r.Name == rivalName);
                if (rival != null)
                {
                    rival.Hostility = faction.Hostility;
                    rival.Strength = faction.Resources;
                    rival.AtWar = faction.AtWar;
                }
            }
        }
    }

    /// <summary>
    /// Distribute the global heat level to individual locations.
    /// Higher-value and higher-heat-generation territories get more attention.
    /// </summary>
    private void DistributeHeatToLocations(GameState game, WorldState world)
    {
        if (game.Territories.Count == 0) return;

        // Calculate total heat generation weight
        var totalWeight = game.Territories.Values.Sum(t => t.HeatGeneration + 1);

        foreach (var territory in game.Territories.Values)
        {
            if (_territoryToLocationMap.TryGetValue(territory.Name, out var locationId))
            {
                var location = world.GetLocation(locationId);
                if (location != null)
                {
                    // Location gets proportional share of global heat
                    var weight = (float)(territory.HeatGeneration + 1) / totalWeight;
                    var heatShare = (int)(game.HeatLevel * weight);

                    // Blend with existing local heat (don't replace entirely)
                    location.LocalHeat = Math.Min(100, (location.LocalHeat + heatShare) / 2 + heatShare / 2);
                }
            }
        }
    }

    /// <summary>
    /// Get the territory key used in GameState.Territories dictionary.
    /// </summary>
    private static string GetTerritoryKey(string territoryName)
    {
        // GameState uses keys like "little-italy", "docks", "bronx"
        return NormalizeToId(territoryName);
    }

    /// <summary>
    /// Convert a display name to a normalized ID.
    /// "Little Italy" → "little-italy"
    /// "Tattaglia Family" → "tattaglia-family"
    /// </summary>
    private static string NormalizeToId(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "");
    }

    /// <summary>
    /// Check if we have established mappings.
    /// </summary>
    public bool IsInitialized => _territoryToLocationMap.Count > 0 || _rivalToFactionMap.Count > 0;

    /// <summary>
    /// Get the location ID mapped to a territory name.
    /// </summary>
    public string? GetLocationId(string territoryName) =>
        _territoryToLocationMap.TryGetValue(territoryName, out var id) ? id : null;

    /// <summary>
    /// Get the faction ID mapped to a rival family name.
    /// </summary>
    public string? GetFactionId(string rivalName) =>
        _rivalToFactionMap.TryGetValue(rivalName, out var id) ? id : null;
}
