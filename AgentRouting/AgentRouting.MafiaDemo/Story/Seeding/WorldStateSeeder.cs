// Story System - World State Seeder
// Creates the initial world state with locations, NPCs, and factions

namespace AgentRouting.MafiaDemo.Story;

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
