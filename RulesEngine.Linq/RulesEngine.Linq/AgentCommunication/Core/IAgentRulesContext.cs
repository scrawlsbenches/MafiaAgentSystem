using System;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// The main entry point for agent communication rules.
/// Provides access to three scopes: WorldState, Session, and Rules.
/// </summary>
public interface IAgentRulesContext : IDisposable
{
    /// <summary>
    /// World state - relatively static reference data.
    /// Agents, Territories, Families, Capabilities.
    /// Loaded at context creation, cached for rule evaluation.
    /// </summary>
    IWorldState World { get; }

    /// <summary>
    /// Current evaluation session - transactional scope.
    /// Messages being processed, events being evaluated.
    /// </summary>
    IMessageSession Session { get; }

    /// <summary>
    /// Rule definitions - queryable and modifiable.
    /// </summary>
    IRuleRegistry Rules { get; }

    /// <summary>
    /// Schema configuration for fact types and relationships.
    /// </summary>
    IFactSchema Schema { get; }

    /// <summary>
    /// Create a new evaluation session.
    /// </summary>
    IMessageSession CreateSession();
}
