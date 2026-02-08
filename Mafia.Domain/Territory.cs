// NOTE: These domain objects are for internal use by RulesEngine.Linq tests.
// Feel free to add properties as needed for testing scenarios.

namespace Mafia.Domain;

/// <summary>
/// A territory that can be controlled by a family.
/// </summary>
public class Territory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ControlledById { get; set; } // FamilyId (foreign key)
    public Family? ControlledBy { get; set; } // Navigation property
    public double Value { get; set; }
    public double Revenue { get; set; }
    public int HeatLevel { get; set; } // 0-100: law enforcement attention
    public string? Status { get; set; } // e.g., "occupied", "guarded", etc.
}
