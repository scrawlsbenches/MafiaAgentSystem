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
    public string? ControlledBy { get; set; } // FamilyId
    public double Value { get; set; }
}
