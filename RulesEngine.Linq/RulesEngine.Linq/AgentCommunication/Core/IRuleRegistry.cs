namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Registry of rule definitions - queryable collection.
/// </summary>
public interface IRuleRegistry
{
    /// <summary>
    /// Get rules for a specific fact type.
    /// </summary>
    IRuleSet<T> For<T>() where T : class;

    /// <summary>
    /// All registered rule sets.
    /// </summary>
    IEnumerable<Type> RegisteredFactTypes { get; }
}
