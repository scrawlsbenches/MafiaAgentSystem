// Story System Thresholds
// Centralized constants for relationship and trait checks

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Centralized thresholds for relationship and trait checks.
/// Using constants prevents magic numbers scattered throughout the codebase.
/// </summary>
public static class Thresholds
{
    // Relationship levels
    public const int Enemy = -30;
    public const int Hostile = -50;
    public const int DeepEnemy = -75;
    public const int Stranger = 0;
    public const int Acquaintance = 30;
    public const int Friend = 50;
    public const int CloseFriend = 70;
    public const int Ally = 100;

    // Trait thresholds (when a trait is considered "high")
    public const int TraitHigh = 70;
    public const int TraitLow = 30;
    public const int TraitVeryHigh = 80;
    public const int TraitVeryLow = 20;

    // Memory thresholds
    public const int HighSalience = 80;
    public const int ModerateSalience = 50;
    public const float MinRelevanceScore = 0.2f;
    public const float ForgetThreshold = 0.1f;

    // Intel reliability
    public const int ReliableIntel = 75;
    public const int SuspectIntel = 50;

    // Location thresholds
    public const int HighHeat = 50;
    public const int DangerousHeat = 75;

    // Timing
    public const int RecentWeeks = 3;
    public const int StaleWeeks = 4;
    public const int AncientWeeks = 20;
}
