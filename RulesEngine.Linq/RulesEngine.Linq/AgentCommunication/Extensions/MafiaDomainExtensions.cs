using Mafia.Domain;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Extension methods providing domain-specific shortcuts.
/// These make rule expressions more readable.
/// </summary>
public static class MafiaDomainExtensions
{
    // --- World State Shortcuts ---

    public static IQueryable<Agent> Agents(this IWorldState world)
        => world.Facts<Agent>();

    public static IQueryable<Territory> Territories(this IWorldState world)
        => world.Facts<Territory>();

    public static IQueryable<Family> Families(this IWorldState world)
        => world.Facts<Family>();

    // --- Context-level shortcuts (for use in rule expressions) ---

    public static IQueryable<Agent> Agents(this IAgentRulesContext context)
        => context.World.Agents();

    public static IQueryable<Territory> Territories(this IAgentRulesContext context)
        => context.World.Territories();

    // --- Session Shortcuts ---

    public static IQueryable<AgentMessage> Messages(this IMessageSession session)
        => session.Facts<AgentMessage>();

    public static IQueryable<AgentMessage> MessageHistory(
        this IMessageSession session,
        string agentId,
        TimeSpan lookback)
    {
        var cutoff = DateTime.UtcNow - lookback;
        return session.Messages()
            .Where(m => m.FromId == agentId && m.Timestamp >= cutoff);
    }

    // --- Agent Navigation ---

    /// <summary>
    /// Get the chain of command from agent up to the top.
    /// Returns: [agent, superior, superior's superior, ..., godfather]
    /// </summary>
    public static IEnumerable<Agent> ChainOfCommand(
        this IQueryable<Agent> agents,
        Agent from)
    {
        var current = from;
        while (current != null)
        {
            yield return current;
            current = current.SuperiorId != null
                ? agents.FirstOrDefault(a => a.Id == current.SuperiorId)
                : null;
        }
    }

    /// <summary>
    /// Get the chain of command between two agents.
    /// Returns path from 'from' up to common ancestor down to 'to'.
    /// </summary>
    public static IEnumerable<Agent> ChainOfCommand(
        this IQueryable<Agent> agents,
        Agent from,
        Agent to)
    {
        var fromChain = agents.ChainOfCommand(from).ToList();
        var toChain = agents.ChainOfCommand(to).ToList();

        // Find common ancestor
        var commonAncestor = fromChain.FirstOrDefault(a => toChain.Contains(a));
        if (commonAncestor == null)
            return Enumerable.Empty<Agent>(); // Different families

        // Path up from 'from' to ancestor, then down to 'to'
        var upPath = fromChain.TakeWhile(a => a.Id != commonAncestor.Id).ToList();
        upPath.Add(commonAncestor);

        var downPath = toChain.TakeWhile(a => a.Id != commonAncestor.Id).Reverse();

        return upPath.Concat(downPath);
    }

    /// <summary>
    /// Check if 'from' can directly message 'to' in chain of command.
    /// </summary>
    public static bool IsImmediateSuperior(this Agent from, Agent to)
        => from.SuperiorId == to.Id;

    public static bool IsImmediateSubordinate(this Agent from, Agent to)
        => to.SuperiorId == from.Id;
}
