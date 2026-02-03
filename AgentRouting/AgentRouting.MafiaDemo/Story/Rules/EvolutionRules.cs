// Story System - Evolution Rules
// Rules for how experiences change personas

using RulesEngine.Core;

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Context for character evolution rules.
/// Determines how experiences change a persona over time.
/// </summary>
public class EvolutionContext
{
    public Persona Persona { get; set; } = null!;
    public string ExperienceType { get; set; } = "";
    public int Intensity { get; set; }              // 0-100, how significant
    public string? InvolvesEntityId { get; set; }

    // Current trait values for conditions
    public int Trust => Persona.Trust;
    public int Caution => Persona.Caution;
    public int Aggression => Persona.Aggression;
    public int Loyalty => Persona.Loyalty;
    public int Pride => Persona.Pride;
    public int Ambition => Persona.Ambition;

    // Output - changes to apply
    public Dictionary<string, int> TraitChanges { get; set; } = new();
    public string? NewGoal { get; set; }
    public string? NewFear { get; set; }
}

/// <summary>
/// Rules for how experiences change personas.
/// </summary>
public static class EvolutionRulesSetup
{
    public static RulesEngineCore<EvolutionContext> CreateEngine()
    {
        var engine = new RulesEngineCore<EvolutionContext>();

        // === BETRAYAL EXPERIENCES ===

        engine.AddRule(
            "EVOLVE_BETRAYAL_TRUST_LOSS",
            "Betrayal Reduces Trust",
            ctx => ctx.ExperienceType == "betrayed",
            ctx => {
                ctx.TraitChanges["Trust"] = -ctx.Intensity;
                ctx.TraitChanges["Caution"] = ctx.Intensity / 2;
            },
            priority: 100);

        engine.AddRule(
            "EVOLVE_BETRAYAL_REVENGE_GOAL",
            "Severe Betrayal Creates Revenge Goal",
            ctx => ctx.ExperienceType == "betrayed" && ctx.Intensity > 70 && ctx.Pride > 50,
            ctx => {
                ctx.NewGoal = $"revenge_{ctx.InvolvesEntityId}";
                ctx.TraitChanges["Aggression"] = ctx.Intensity / 3;
            },
            priority: 110);

        // === SUCCESS EXPERIENCES ===

        engine.AddRule(
            "EVOLVE_SUCCESS_AMBITION",
            "Success Increases Ambition",
            ctx => ctx.ExperienceType == "success",
            ctx => {
                ctx.TraitChanges["Ambition"] = ctx.Intensity / 3;
                ctx.TraitChanges["Pride"] = ctx.Intensity / 4;
            },
            priority: 100);

        engine.AddRule(
            "EVOLVE_BIG_SUCCESS_REDUCES_CAUTION",
            "Big Success Reduces Caution",
            ctx => ctx.ExperienceType == "success" && ctx.Intensity > 60,
            ctx => {
                ctx.TraitChanges["Caution"] = -ctx.Intensity / 4;
            },
            priority: 105);

        // === FAILURE EXPERIENCES ===

        engine.AddRule(
            "EVOLVE_FAILURE_CAUTION",
            "Failure Increases Caution",
            ctx => ctx.ExperienceType == "failure",
            ctx => {
                ctx.TraitChanges["Caution"] = ctx.Intensity / 2;
                ctx.TraitChanges["Pride"] = -ctx.Intensity / 3;
            },
            priority: 100);

        engine.AddRule(
            "EVOLVE_REPEATED_FAILURE_FEAR",
            "Repeated Failure Creates Fear",
            ctx => ctx.ExperienceType == "failure" && ctx.Caution > 70,
            ctx => {
                ctx.NewFear = "failure";
                ctx.TraitChanges["Ambition"] = -ctx.Intensity / 4;
            },
            priority: 105);

        // === HELP EXPERIENCES ===

        engine.AddRule(
            "EVOLVE_HELPED_TRUST",
            "Being Helped Increases Trust",
            ctx => ctx.ExperienceType == "helped",
            ctx => {
                ctx.TraitChanges["Trust"] = ctx.Intensity / 2;
                ctx.TraitChanges["Loyalty"] = ctx.Intensity / 3;
            },
            priority: 100);

        // === THREAT EXPERIENCES ===

        engine.AddRule(
            "EVOLVE_THREAT_AGGRESSION",
            "Threats Increase Aggression",
            ctx => ctx.ExperienceType == "threatened",
            ctx => {
                ctx.TraitChanges["Aggression"] = ctx.Intensity / 2;
                ctx.TraitChanges["Trust"] = -ctx.Intensity / 2;
            },
            priority: 100);

        engine.AddRule(
            "EVOLVE_THREAT_CAUTIOUS_FEAR",
            "Threats Make Cautious Personas Fearful",
            ctx => ctx.ExperienceType == "threatened" && ctx.Caution > 60,
            ctx => {
                ctx.TraitChanges["Caution"] = ctx.Intensity / 3;
                ctx.NewFear = $"threat_from_{ctx.InvolvesEntityId}";
            },
            priority: 105);

        engine.AddRule(
            "EVOLVE_THREAT_PROUD_REVENGE",
            "Threats Make Proud Personas Vengeful",
            ctx => ctx.ExperienceType == "threatened" && ctx.Pride > 70,
            ctx => {
                ctx.NewGoal = $"retaliate_{ctx.InvolvesEntityId}";
            },
            priority: 105);

        return engine;
    }
}
