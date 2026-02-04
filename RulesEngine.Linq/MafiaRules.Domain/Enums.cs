namespace MafiaRules.Domain;

/// <summary>
/// Roles within the mafia hierarchy, ordered by rank.
/// </summary>
public enum AgentRole
{
    Soldier = 1,
    Capo = 2,
    Underboss = 3,
    Consigliere = 3,  // Same level as Underboss
    Godfather = 4
}

/// <summary>
/// Current operational status of an agent.
/// </summary>
public enum AgentStatus
{
    Available,
    Busy,
    Offline,
    Compromised
}

/// <summary>
/// Types of messages that can be sent between agents.
/// </summary>
public enum MessageType
{
    Request,
    Response,
    Command,
    Alert,
    StatusReport,
    TerritoryRequest,
    Task,
    Broadcast
}
