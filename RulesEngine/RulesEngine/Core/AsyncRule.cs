namespace RulesEngine.Core;

/// <summary>
/// Interface for rules that require asynchronous evaluation or execution.
/// Use this for rules that perform I/O operations like database queries or API calls.
/// </summary>
public interface IAsyncRule<T>
{
    /// <summary>
    /// Unique identifier for the rule
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name of the rule
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Priority for rule execution (higher = earlier)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Asynchronously evaluates whether this rule applies to the given fact.
    /// </summary>
    Task<bool> EvaluateAsync(T fact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously executes the rule action on the given fact.
    /// </summary>
    Task<RuleResult> ExecuteAsync(T fact, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base implementation of an async rule.
/// </summary>
public class AsyncRule<T> : IAsyncRule<T>
{
    private readonly Func<T, CancellationToken, Task<bool>> _condition;
    private readonly Func<T, CancellationToken, Task<RuleResult>> _action;

    /// <summary>
    /// Unique identifier for the rule
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Human-readable name of the rule
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Priority for rule execution (higher = earlier)
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Creates a new async rule with the specified condition and action.
    /// </summary>
    /// <param name="id">Unique rule identifier</param>
    /// <param name="name">Human-readable rule name</param>
    /// <param name="condition">Async predicate that determines if rule applies</param>
    /// <param name="action">Async action to execute when rule matches</param>
    /// <param name="priority">Rule priority (higher = evaluated first)</param>
    public AsyncRule(
        string id,
        string name,
        Func<T, CancellationToken, Task<bool>> condition,
        Func<T, CancellationToken, Task<RuleResult>> action,
        int priority = 0)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _action = action ?? throw new ArgumentNullException(nameof(action));
        Priority = priority;
    }

    /// <summary>
    /// Asynchronously evaluates whether this rule applies to the given fact.
    /// </summary>
    public Task<bool> EvaluateAsync(T fact, CancellationToken cancellationToken = default)
        => _condition(fact, cancellationToken);

    /// <summary>
    /// Asynchronously executes the rule action on the given fact.
    /// </summary>
    public Task<RuleResult> ExecuteAsync(T fact, CancellationToken cancellationToken = default)
        => _action(fact, cancellationToken);
}

/// <summary>
/// Fluent builder for creating async rules.
/// </summary>
public class AsyncRuleBuilder<T>
{
    private string? _id;
    private string? _name;
    private int _priority;
    private Func<T, CancellationToken, Task<bool>>? _condition;
    private Func<T, CancellationToken, Task<RuleResult>>? _action;

    /// <summary>
    /// Sets the unique identifier for the rule.
    /// </summary>
    public AsyncRuleBuilder<T> WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the human-readable name for the rule.
    /// </summary>
    public AsyncRuleBuilder<T> WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the priority for the rule (higher = evaluated first).
    /// </summary>
    public AsyncRuleBuilder<T> WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>
    /// Sets the async condition with cancellation token support.
    /// </summary>
    public AsyncRuleBuilder<T> WithCondition(Func<T, CancellationToken, Task<bool>> condition)
    {
        _condition = condition;
        return this;
    }

    /// <summary>
    /// Simplified condition that doesn't need cancellation token.
    /// </summary>
    public AsyncRuleBuilder<T> WithCondition(Func<T, Task<bool>> condition)
    {
        _condition = (fact, _) => condition(fact);
        return this;
    }

    /// <summary>
    /// Sets the async action with cancellation token support.
    /// </summary>
    public AsyncRuleBuilder<T> WithAction(Func<T, CancellationToken, Task<RuleResult>> action)
    {
        _action = action;
        return this;
    }

    /// <summary>
    /// Simplified action that doesn't need cancellation token.
    /// </summary>
    public AsyncRuleBuilder<T> WithAction(Func<T, Task<RuleResult>> action)
    {
        _action = (fact, _) => action(fact);
        return this;
    }

    /// <summary>
    /// Builds the async rule with the configured settings.
    /// </summary>
    /// <exception cref="RuleValidationException">Thrown when required properties are not set</exception>
    public IAsyncRule<T> Build()
    {
        if (string.IsNullOrEmpty(_id))
            throw new RuleValidationException("Rule ID is required");
        if (string.IsNullOrEmpty(_name))
            throw new RuleValidationException("Rule name is required", _id);
        if (_condition == null)
            throw new RuleValidationException("Rule condition is required", _id);
        if (_action == null)
            throw new RuleValidationException("Rule action is required", _id);

        return new AsyncRule<T>(_id, _name, _condition, _action, _priority);
    }
}
