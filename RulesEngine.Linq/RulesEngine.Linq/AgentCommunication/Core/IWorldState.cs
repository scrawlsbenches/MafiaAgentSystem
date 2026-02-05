using System;
using System.Collections.Generic;
using System.Linq;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// World state - reference data that rules query against.
/// Think of this as your "database" of agents and territories.
/// </summary>
public interface IWorldState
{
    /// <summary>
    /// Query any registered fact type.
    /// </summary>
    IQueryable<T> Facts<T>() where T : class;

    /// <summary>
    /// Find by primary key.
    /// </summary>
    T? Find<T>(object key) where T : class;

    /// <summary>
    /// Update world state (agent status changes, territory control, etc.)
    /// </summary>
    void Update<T>(T fact) where T : class;

    /// <summary>
    /// Registered fact types in world state.
    /// </summary>
    IReadOnlySet<Type> RegisteredTypes { get; }
}
