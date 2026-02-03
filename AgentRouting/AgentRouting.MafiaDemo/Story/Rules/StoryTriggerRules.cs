// Story System - Story Trigger Rules
// Rules for when conversations trigger story events

using RulesEngine.Core;

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Context for evaluating story trigger rules.
/// Determines when conversations unlock story nodes.
/// </summary>
public class StoryTriggerContext
{
    // The conversation that happened
    public AgentQuestion Question { get; set; } = null!;
    public AgentResponse Response { get; set; } = null!;

    // Shorthand properties
    public QuestionType QuestionType => Question.Type;
    public ResponseType ResponseType => Response.Type;
    public bool WasHonest => Response.IsHonest;
    public bool SharedSecret => Response.SharedMemories.Any(m => m.Type == MemoryType.Secret);

    // Memory content checks
    public bool MentionedInformant => Response.SharedMemories
        .Any(m => m.Summary.Contains("informant", StringComparison.OrdinalIgnoreCase));
    public bool MentionedBetrayal => Response.SharedMemories
        .Any(m => m.Type == MemoryType.Betrayal);
    public bool MentionedThreat => Response.SharedMemories
        .Any(m => m.Type == MemoryType.Threat);

    // World state
    public WorldState World { get; set; } = null!;
    public StoryGraph Graph { get; set; } = null!;

    // Output - events to trigger
    public List<string> TriggeredEvents { get; set; } = new();
    public List<string> UnlockedNodes { get; set; } = new();
}

/// <summary>
/// Rules for when conversations trigger story events.
/// </summary>
public static class StoryTriggerRulesSetup
{
    public static RulesEngineCore<StoryTriggerContext> CreateEngine()
    {
        var engine = new RulesEngineCore<StoryTriggerContext>();

        // === SECRET REVELATIONS ===

        engine.AddRule(
            "TRIGGER_INFORMANT_DISCOVERED",
            "Informant Identity Revealed",
            ctx => ctx.WasHonest && ctx.MentionedInformant && ctx.SharedSecret,
            ctx => {
                ctx.TriggeredEvents.Add("informant_discovered");
                ctx.UnlockedNodes.Add("rat-hunt-plot");
            },
            priority: 100);

        engine.AddRule(
            "TRIGGER_BETRAYAL_REVEALED",
            "Betrayal Information Shared",
            ctx => ctx.WasHonest && ctx.MentionedBetrayal,
            ctx => {
                ctx.TriggeredEvents.Add("betrayal_revealed");
            },
            priority: 90);

        // === RELATIONSHIP EVENTS ===

        engine.AddRule(
            "TRIGGER_ALLIANCE_FORMED",
            "Help Agreement Reached",
            ctx => ctx.QuestionType == QuestionType.WillYouHelp &&
                   ctx.ResponseType == ResponseType.Answer,
            ctx => {
                ctx.TriggeredEvents.Add("alliance_formed");
            },
            priority: 80);

        engine.AddRule(
            "TRIGGER_TRUST_BROKEN",
            "Lie Discovered (Future)",
            ctx => ctx.ResponseType == ResponseType.Lie,
            ctx => {
                // Lie may be discovered later, flag for potential trigger
                ctx.TriggeredEvents.Add("potential_lie_discovery");
            },
            priority: 70);

        // === THREAT REVELATIONS ===

        engine.AddRule(
            "TRIGGER_THREAT_WARNING",
            "Threat Information Shared",
            ctx => ctx.WasHonest && ctx.MentionedThreat,
            ctx => {
                ctx.TriggeredEvents.Add("threat_warning_received");
            },
            priority: 85);

        // === BARGAINING EVENTS ===

        engine.AddRule(
            "TRIGGER_DEBT_CREATED",
            "Bargain Creates Debt",
            ctx => ctx.ResponseType == ResponseType.Bargain,
            ctx => {
                ctx.TriggeredEvents.Add("debt_created");
            },
            priority: 60);

        return engine;
    }
}
