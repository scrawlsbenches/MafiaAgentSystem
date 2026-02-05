// NOTE: These domain objects are for internal use by RulesEngine.Linq tests.
// Feel free to add properties as needed for testing scenarios.

namespace Mafia.Domain;

/// <summary>
/// Role in the family hierarchy.
/// </summary>
public enum AgentRole
{
    Associate,
    Soldier,
    Capo,
    Underboss,
    Consigliere,
    Godfather
}

/// <summary>
/// Current operational status of an agent.
/// </summary>
public enum AgentStatus
{
    Available,
    Busy,
    Unavailable,
    Compromised
}

/// <summary>
/// Type of message being sent between agents.
/// </summary>
public enum MessageType
{
    Request,
    Response,
    Report,
    Alert,
    Broadcast,
    Task,
    StatusReport,
    TerritoryRequest
}
