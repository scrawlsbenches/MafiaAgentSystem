namespace MafiaRules.Domain;

/// <summary>
/// Payload for territory request messages.
/// </summary>
public class TerritoryRequestPayload
{
    public string TerritoryId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public decimal? OfferedAmount { get; set; }
}
