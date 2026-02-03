// Story System - Persona
// Personality traits that influence agent/NPC behavior

namespace AgentRouting.MafiaDemo.Story;

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

    // === Computed Traits (using centralized thresholds) ===
    public bool IsAmbitious => Ambition > Thresholds.TraitHigh;
    public bool IsCautious => Caution > Thresholds.TraitHigh;
    public bool IsAggressive => Aggression > Thresholds.TraitHigh;
    public bool IsLoyal => Loyalty > Thresholds.TraitHigh;
    public bool IsTrusting => Trust > Thresholds.TraitHigh;
    public bool IsCunning => Cunning > Thresholds.TraitHigh;
    public bool IsPatient => Patience > Thresholds.TraitHigh;
    public bool IsProud => Pride > Thresholds.TraitHigh;

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

/// <summary>
/// A goal that a persona is trying to achieve.
/// </summary>
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
