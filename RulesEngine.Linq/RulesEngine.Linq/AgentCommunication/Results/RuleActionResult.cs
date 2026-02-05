using Mafia.Domain;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Result of rule action execution.
/// </summary>
public class RuleActionResult
{
    public bool Executed { get; init; }
    public bool Blocked { get; init; }
    public Agent? ReroutedTo { get; init; }
    public IReadOnlyList<string> Flags { get; init; } = Array.Empty<string>();
    public AgentMessage? GeneratedMessage { get; init; }

    public static RuleActionResult Success() => new() { Executed = true };
    public static RuleActionResult Block() => new() { Blocked = true };
    public static RuleActionResult Reroute(Agent to) => new() { Executed = true, ReroutedTo = to };
}
