using System;
using System.Collections.Generic;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Schema for fact types and their relationships.
/// </summary>
public interface IFactSchema
{
    IReadOnlySet<Type> RegisteredTypes { get; }
    bool IsRegistered(Type type);
    void ValidateType(Type type);
}
