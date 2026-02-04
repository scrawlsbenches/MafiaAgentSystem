namespace MafiaRules.Domain;

/// <summary>
/// Represents a mafia family organization.
/// </summary>
public class Family
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Treasury { get; set; }

    // Navigation properties
    public List<Agent> Members { get; set; } = new();
    public List<Territory> Territories { get; set; } = new();
}
