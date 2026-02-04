namespace MafiaRules.Domain;

/// <summary>
/// Represents a geographic territory that can be controlled by a family.
/// </summary>
public class Territory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ControlledBy { get; set; } = string.Empty;  // FamilyId
    public decimal MonthlyRevenue { get; set; }
    public bool IsContested { get; set; }
}
