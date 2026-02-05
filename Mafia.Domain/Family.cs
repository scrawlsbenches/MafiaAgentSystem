// NOTE: These domain objects are for internal use by RulesEngine.Linq tests.
// Feel free to add properties as needed for testing scenarios.

namespace Mafia.Domain;

/// <summary>
/// A crime family.
/// </summary>
public class Family
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string GodfatherId { get; set; } = string.Empty;
}
