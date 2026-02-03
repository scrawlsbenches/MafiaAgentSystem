// Story System - Consequence Rules
// Rules for mission consequences

using RulesEngine.Core;

namespace AgentRouting.MafiaDemo.Story;

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
