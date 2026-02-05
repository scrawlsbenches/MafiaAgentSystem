namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Session for processing messages - transactional scope.
/// </summary>
public interface IMessageSession : IDisposable
{
    Guid SessionId { get; }
    SessionPhase Phase { get; }

    /// <summary>
    /// Messages/facts in this session (snapshot before current evaluation).
    /// </summary>
    IQueryable<T> Facts<T>() where T : class;

    /// <summary>
    /// Insert a fact for evaluation.
    /// </summary>
    void Insert<T>(T fact) where T : class;

    /// <summary>
    /// Evaluate all pending facts against rules.
    /// Returns results and populates pending outbound messages.
    /// </summary>
    IEvaluationResult Evaluate();

    /// <summary>
    /// Messages created by rule actions, waiting to be dispatched.
    /// </summary>
    IReadOnlyList<object> PendingOutbound { get; }

    /// <summary>
    /// Commit session - dispatch pending messages, update world state.
    /// </summary>
    void Commit();

    /// <summary>
    /// Rollback - discard pending changes.
    /// </summary>
    void Rollback();
}

public enum SessionPhase
{
    Accepting,      // Can insert facts
    Evaluating,     // Rules are running
    Evaluated,      // Results ready, can commit/rollback
    Committed,
    RolledBack,
    Disposed
}
