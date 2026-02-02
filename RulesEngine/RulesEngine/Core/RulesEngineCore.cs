using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace RulesEngine.Core;

/// <summary>
/// Configuration options for the rules engine
/// </summary>
public class RulesEngineOptions
{
    /// <summary>
    /// Stop evaluating rules after the first match
    /// </summary>
    public bool StopOnFirstMatch { get; set; } = false;

    /// <summary>
    /// Execute rules in parallel when possible
    /// </summary>
    public bool EnableParallelExecution { get; set; } = false;

    /// <summary>
    /// Track performance metrics
    /// </summary>
    public bool TrackPerformance { get; set; } = true;

    /// <summary>
    /// Maximum number of rules to execute
    /// </summary>
    public int? MaxRulesToExecute { get; set; } = null;

    /// <summary>
    /// Whether to allow duplicate rule IDs (default: false)
    /// </summary>
    public bool AllowDuplicateRuleIds { get; set; } = false;
}

/// <summary>
/// Main rules engine that evaluates rules against facts
/// </summary>
public class RulesEngineCore<T> : IRulesEngine<T>
{
    private readonly List<IRule<T>> _rules;
    private readonly List<IAsyncRule<T>> _asyncRules;
    private readonly RulesEngineOptions _options;
    private readonly ConcurrentDictionary<string, RulePerformanceMetrics> _metrics;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private IReadOnlyList<IRule<T>>? _sortedRulesCache;
    private IReadOnlyList<IAsyncRule<T>>? _sortedAsyncRulesCache;
    private bool _disposed;

    public RulesEngineCore(RulesEngineOptions? options = null)
    {
        _rules = new List<IRule<T>>();
        _asyncRules = new List<IAsyncRule<T>>();
        _options = options ?? new RulesEngineOptions();
        _metrics = new ConcurrentDictionary<string, RulePerformanceMetrics>();
    }

    /// <summary>
    /// Registers a rule with the engine
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when rule is null</exception>
    /// <exception cref="RuleValidationException">Thrown when rule fails validation</exception>
    public void RegisterRule(IRule<T> rule)
    {
        // Validation first (no lock needed)
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        if (string.IsNullOrEmpty(rule.Id))
            throw new RuleValidationException("Rule ID cannot be null or empty");
        if (string.IsNullOrEmpty(rule.Name))
            throw new RuleValidationException("Rule name cannot be null or empty", rule.Id);

        _lock.EnterWriteLock();
        try
        {
            // Check for duplicates inside the lock
            if (!_options.AllowDuplicateRuleIds && _rules.Any(r => r.Id == rule.Id))
                throw new RuleValidationException($"Rule with ID '{rule.Id}' already exists", rule.Id);
            _rules.Add(rule);
            _sortedRulesCache = null; // Invalidate cache
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Registers multiple rules
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when rules array is null</exception>
    /// <exception cref="RuleValidationException">Thrown when any rule fails validation</exception>
    public void RegisterRules(params IRule<T>[] rules)
    {
        if (rules == null) throw new ArgumentNullException(nameof(rules));

        // Validate all rules first (outside lock)
        foreach (var rule in rules)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rules), "Rules array contains null element");
            if (string.IsNullOrEmpty(rule.Id))
                throw new RuleValidationException("Rule ID cannot be null or empty");
            if (string.IsNullOrEmpty(rule.Name))
                throw new RuleValidationException("Rule name cannot be null or empty", rule.Id);
        }

        _lock.EnterWriteLock();
        try
        {
            foreach (var rule in rules)
            {
                if (!_options.AllowDuplicateRuleIds && _rules.Any(r => r.Id == rule.Id))
                    throw new RuleValidationException($"Rule with ID '{rule.Id}' already exists", rule.Id);
                _rules.Add(rule);
            }
            _sortedRulesCache = null; // Invalidate cache
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Convenience method for inline rule creation with condition and action
    /// </summary>
    /// <param name="id">Unique rule identifier</param>
    /// <param name="name">Human-readable rule name</param>
    /// <param name="condition">Predicate that determines if rule applies</param>
    /// <param name="action">Action to execute when rule matches (modifies fact in-place)</param>
    /// <param name="priority">Rule priority (higher = evaluated first)</param>
    /// <exception cref="RuleValidationException">Thrown when rule fails validation</exception>
    public void AddRule(string id, string name, Func<T, bool> condition, Action<T> action, int priority = 0)
    {
        // Validation first (no lock needed)
        if (string.IsNullOrEmpty(id))
            throw new RuleValidationException("Rule ID cannot be null or empty");
        if (string.IsNullOrEmpty(name))
            throw new RuleValidationException("Rule name cannot be null or empty", id);
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        _lock.EnterWriteLock();
        try
        {
            if (!_options.AllowDuplicateRuleIds && _rules.Any(r => r.Id == id))
                throw new RuleValidationException($"Rule with ID '{id}' already exists", id);
            _rules.Add(new ActionRule<T>(id, name, condition, action, priority));
            _sortedRulesCache = null; // Invalidate cache
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Registers an async rule with the engine
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when rule is null</exception>
    /// <exception cref="RuleValidationException">Thrown when rule fails validation</exception>
    public void RegisterAsyncRule(IAsyncRule<T> rule)
    {
        // Validation first (no lock needed)
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        if (string.IsNullOrEmpty(rule.Id))
            throw new RuleValidationException("Async rule ID cannot be null or empty");
        if (string.IsNullOrEmpty(rule.Name))
            throw new RuleValidationException("Async rule name cannot be null or empty", rule.Id);

        _lock.EnterWriteLock();
        try
        {
            // Check for duplicates across both sync and async rules
            if (!_options.AllowDuplicateRuleIds &&
                (_rules.Any(r => r.Id == rule.Id) || _asyncRules.Any(r => r.Id == rule.Id)))
                throw new RuleValidationException($"Rule with ID '{rule.Id}' already exists", rule.Id);

            _asyncRules.Add(rule);
            _sortedAsyncRulesCache = null; // Invalidate async cache
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Registers multiple async rules
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when rules array is null</exception>
    /// <exception cref="RuleValidationException">Thrown when any rule fails validation</exception>
    public void RegisterAsyncRules(params IAsyncRule<T>[] rules)
    {
        if (rules == null) throw new ArgumentNullException(nameof(rules));

        // Validate all rules first (outside lock)
        foreach (var rule in rules)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rules), "Rules array contains null element");
            if (string.IsNullOrEmpty(rule.Id))
                throw new RuleValidationException("Async rule ID cannot be null or empty");
            if (string.IsNullOrEmpty(rule.Name))
                throw new RuleValidationException("Async rule name cannot be null or empty", rule.Id);
        }

        _lock.EnterWriteLock();
        try
        {
            foreach (var rule in rules)
            {
                if (!_options.AllowDuplicateRuleIds &&
                    (_rules.Any(r => r.Id == rule.Id) || _asyncRules.Any(r => r.Id == rule.Id)))
                    throw new RuleValidationException($"Rule with ID '{rule.Id}' already exists", rule.Id);
                _asyncRules.Add(rule);
            }
            _sortedAsyncRulesCache = null; // Invalidate cache
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets all registered async rules
    /// </summary>
    public IReadOnlyList<IAsyncRule<T>> GetAsyncRules()
    {
        _lock.EnterReadLock();
        try
        {
            return _asyncRules.ToList().AsReadOnly();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Evaluates all matching rules and applies their actions to the fact.
    /// Unlike Execute(), this modifies the fact in-place and doesn't return results.
    /// Rules are evaluated in priority order (highest first).
    /// </summary>
    public void EvaluateAll(T fact)
    {
        var sortedRules = GetSortedRules();

        // Execute outside the lock to avoid holding lock during potentially long-running rule execution
        foreach (var rule in sortedRules)
        {
            if (rule.Evaluate(fact))
            {
                rule.Execute(fact);
            }
        }
    }

    /// <summary>
    /// Removes a rule by ID (checks both sync and async rules)
    /// </summary>
    public bool RemoveRule(string ruleId)
    {
        _lock.EnterWriteLock();
        try
        {
            var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
            if (rule != null)
            {
                _rules.Remove(rule);
                _sortedRulesCache = null; // Invalidate cache
                return true;
            }

            var asyncRule = _asyncRules.FirstOrDefault(r => r.Id == ruleId);
            if (asyncRule != null)
            {
                _asyncRules.Remove(asyncRule);
                _sortedAsyncRulesCache = null; // Invalidate cache
                return true;
            }

            return false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all rules (both sync and async)
    /// </summary>
    public void ClearRules()
    {
        _lock.EnterWriteLock();
        try
        {
            _rules.Clear();
            _asyncRules.Clear();
            _sortedRulesCache = null; // Invalidate cache
            _sortedAsyncRulesCache = null; // Invalidate async cache
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets all registered rules
    /// </summary>
    public IReadOnlyList<IRule<T>> GetRules()
    {
        _lock.EnterReadLock();
        try
        {
            return _rules.ToList().AsReadOnly();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets or creates the cached sorted rules list
    /// </summary>
    private IReadOnlyList<IRule<T>> GetSortedRules()
    {
        // Capture in local variable to avoid TOCTOU race condition
        // (another thread could set _sortedRulesCache = null between check and return)
        var cache = _sortedRulesCache;
        if (cache != null)
            return cache;

        _lock.EnterWriteLock();
        try
        {
            // Double-check after acquiring write lock
            cache = _sortedRulesCache;
            if (cache != null)
                return cache;

            _sortedRulesCache = _rules.OrderByDescending(r => r.Priority).ToList().AsReadOnly();
            return _sortedRulesCache;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets or creates the cached sorted async rules list
    /// </summary>
    private IReadOnlyList<IAsyncRule<T>> GetSortedAsyncRules()
    {
        // Capture in local variable to avoid TOCTOU race condition
        var cache = _sortedAsyncRulesCache;
        if (cache != null)
            return cache;

        _lock.EnterWriteLock();
        try
        {
            // Double-check after acquiring write lock
            cache = _sortedAsyncRulesCache;
            if (cache != null)
                return cache;

            _sortedAsyncRulesCache = _asyncRules.OrderByDescending(r => r.Priority).ToList().AsReadOnly();
            return _sortedAsyncRulesCache;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Evaluates all applicable rules against the fact
    /// </summary>
    public RulesEngineResult Execute(T fact)
    {
        var result = new RulesEngineResult();
        var startTime = DateTime.UtcNow;

        var cachedRules = GetSortedRules();

        // Limit rules if configured
        IEnumerable<IRule<T>> rulesToExecute = cachedRules;
        if (_options.MaxRulesToExecute.HasValue)
        {
            rulesToExecute = cachedRules.Take(_options.MaxRulesToExecute.Value);
        }

        // Execute outside the lock to avoid holding lock during potentially long-running rule execution
        if (_options.EnableParallelExecution)
        {
            ExecuteParallel(fact, rulesToExecute, result);
        }
        else
        {
            ExecuteSequential(fact, rulesToExecute, result);
        }

        result.TotalExecutionTime = DateTime.UtcNow - startTime;
        return result;
    }

    /// <summary>
    /// Asynchronously executes all applicable rules against the fact with cancellation support.
    /// Processes sync rules first, then async rules, both in priority order.
    /// </summary>
    /// <param name="fact">The fact to evaluate rules against</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A collection of rule execution results</returns>
    public async Task<IEnumerable<RuleExecutionResult<T>>> ExecuteAsync(
        T fact,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = new List<RuleExecutionResult<T>>();
        var stopOnMatch = false;

        // Process sync rules first
        var rulesToExecute = GetSortedRules();

        // Limit rules if configured
        IEnumerable<IRule<T>> limitedRules = rulesToExecute;
        if (_options.MaxRulesToExecute.HasValue)
        {
            limitedRules = rulesToExecute.Take(_options.MaxRulesToExecute.Value);
        }

        foreach (var rule in limitedRules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Yield to allow cancellation between rules
            await Task.Yield();

            try
            {
                bool matches = rule.Evaluate(fact);
                if (matches)
                {
                    var result = rule.Execute(fact);
                    results.Add(new RuleExecutionResult<T>(rule, result, true));

                    if (_options.StopOnFirstMatch)
                    {
                        stopOnMatch = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new RuleExecutionResult<T>(rule,
                    RuleResult.Failure(rule.Id, ex.Message), false, ex));
            }
        }

        // If we stopped on first match, don't process async rules
        if (stopOnMatch)
            return results;

        // Process async rules
        var asyncRulesToExecute = GetSortedAsyncRules();

        foreach (var asyncRule in asyncRulesToExecute)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                bool matches = await asyncRule.EvaluateAsync(fact, cancellationToken);
                if (matches)
                {
                    var result = await asyncRule.ExecuteAsync(fact, cancellationToken);
                    results.Add(new RuleExecutionResult<T>(asyncRule, result, true));

                    if (_options.StopOnFirstMatch)
                        break;
                }
            }
            catch (Exception ex)
            {
                results.Add(new RuleExecutionResult<T>(asyncRule,
                    RuleResult.Failure(asyncRule.Id, ex.Message), false, ex));
            }
        }

        return results;
    }

    private void ExecuteSequential(T fact, IEnumerable<IRule<T>> rules, RulesEngineResult result)
    {
        foreach (var rule in rules)
        {
            var ruleStart = DateTime.UtcNow;
            var ruleResult = rule.Execute(fact);
            var duration = DateTime.UtcNow - ruleStart;
            
            result.AddRuleResult(ruleResult);
            
            if (_options.TrackPerformance)
            {
                TrackPerformance(rule.Id, duration);
            }
            
            if (_options.StopOnFirstMatch && ruleResult.Matched)
            {
                break;
            }
        }
    }
    
    private void ExecuteParallel(T fact, IEnumerable<IRule<T>> rules, RulesEngineResult result)
    {
        var results = new ConcurrentBag<RuleResult>();
        
        Parallel.ForEach(rules, rule =>
        {
            var ruleStart = DateTime.UtcNow;
            var ruleResult = rule.Execute(fact);
            var duration = DateTime.UtcNow - ruleStart;
            
            results.Add(ruleResult);
            
            if (_options.TrackPerformance)
            {
                TrackPerformance(rule.Id, duration);
            }
        });
        
        foreach (var r in results.OrderByDescending(r => r.Matched))
        {
            result.AddRuleResult(r);
        }
    }
    
    /// <summary>
    /// Checks which rules would match without executing actions
    /// </summary>
    public List<IRule<T>> GetMatchingRules(T fact)
    {
        List<IRule<T>> rulesCopy;

        _lock.EnterReadLock();
        try
        {
            rulesCopy = _rules.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // Evaluate outside the lock to avoid holding lock during potentially long-running evaluation
        return rulesCopy.Where(r => r.Evaluate(fact)).ToList();
    }

    /// <summary>
    /// Gets performance metrics for a specific rule
    /// </summary>
    public RulePerformanceMetrics? GetMetrics(string ruleId)
    {
        return _metrics.TryGetValue(ruleId, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Gets all performance metrics
    /// </summary>
    public Dictionary<string, RulePerformanceMetrics> GetAllMetrics()
    {
        return new Dictionary<string, RulePerformanceMetrics>(_metrics);
    }

    /// <summary>
    /// Disposes the rules engine and releases all resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the rules engine
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _lock?.Dispose();
            }
            _disposed = true;
        }
    }

    private void TrackPerformance(string ruleId, TimeSpan duration)
    {
        _metrics.AddOrUpdate(
            ruleId,
            _ => new RulePerformanceMetrics
            {
                RuleId = ruleId,
                ExecutionCount = 1,
                TotalExecutionTime = duration,
                AverageExecutionTime = duration,
                MinExecutionTime = duration,
                MaxExecutionTime = duration
            },
            (_, existing) =>
            {
                existing.ExecutionCount++;
                existing.TotalExecutionTime += duration;
                existing.AverageExecutionTime = TimeSpan.FromTicks(
                    existing.TotalExecutionTime.Ticks / existing.ExecutionCount);
                existing.MinExecutionTime = duration < existing.MinExecutionTime 
                    ? duration 
                    : existing.MinExecutionTime;
                existing.MaxExecutionTime = duration > existing.MaxExecutionTime 
                    ? duration 
                    : existing.MaxExecutionTime;
                return existing;
            }
        );
    }
}

/// <summary>
/// Result of executing the rules engine
/// </summary>
public class RulesEngineResult
{
    private readonly List<RuleResult> _ruleResults = new();
    
    public IReadOnlyList<RuleResult> RuleResults => _ruleResults.AsReadOnly();
    public TimeSpan TotalExecutionTime { get; set; }
    
    public int TotalRulesEvaluated => _ruleResults.Count;
    public int MatchedRules => _ruleResults.Count(r => r.Matched);
    public int ExecutedActions => _ruleResults.Count(r => r.ActionExecuted);
    public int Errors => _ruleResults.Count(r => r.ErrorMessage != null);
    
    public void AddRuleResult(RuleResult result)
    {
        _ruleResults.Add(result);
    }
    
    public List<RuleResult> GetMatchedRules()
    {
        return _ruleResults.Where(r => r.Matched).ToList();
    }
    
    public List<RuleResult> GetErrors()
    {
        return _ruleResults.Where(r => r.ErrorMessage != null).ToList();
    }
}

/// <summary>
/// Performance metrics for a rule
/// </summary>
public class RulePerformanceMetrics
{
    public string RuleId { get; set; } = string.Empty;
    public int ExecutionCount { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; }
    public TimeSpan MaxExecutionTime { get; set; }
}

/// <summary>
/// Internal rule implementation that wraps condition and action delegates.
/// Used by the AddRule convenience method for inline rule definitions.
/// </summary>
internal class ActionRule<T> : IRule<T>
{
    private readonly Func<T, bool> _condition;
    private readonly Action<T> _action;

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int Priority { get; }

    public ActionRule(string id, string name, Func<T, bool> condition, Action<T> action, int priority)
    {
        Id = id;
        Name = name;
        Description = name; // Use name as default description for inline rules
        _condition = condition;
        _action = action;
        Priority = priority;
    }

    public bool Evaluate(T fact) => _condition(fact);

    public RuleResult Execute(T fact)
    {
        var matched = _condition(fact);
        if (matched)
        {
            try
            {
                _action(fact);
                return new RuleResult
                {
                    RuleId = Id,
                    RuleName = Name,
                    Matched = true,
                    ActionExecuted = true
                };
            }
            catch (Exception ex)
            {
                return new RuleResult
                {
                    RuleId = Id,
                    RuleName = Name,
                    Matched = true,
                    ActionExecuted = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        return new RuleResult
        {
            RuleId = Id,
            RuleName = Name,
            Matched = false,
            ActionExecuted = false
        };
    }
}

/// <summary>
/// Result of an individual rule execution in async context.
/// Supports both sync rules (IRule) and async rules (IAsyncRule).
/// </summary>
/// <typeparam name="T">The type of fact the rule evaluated</typeparam>
public class RuleExecutionResult<T>
{
    /// <summary>
    /// The sync rule that was executed (null if async rule was executed)
    /// </summary>
    public IRule<T>? Rule { get; }

    /// <summary>
    /// The async rule that was executed (null if sync rule was executed)
    /// </summary>
    public IAsyncRule<T>? AsyncRule { get; }

    /// <summary>
    /// The ID of the rule that was executed
    /// </summary>
    public string RuleId => Rule?.Id ?? AsyncRule?.Id ?? "unknown";

    /// <summary>
    /// The name of the rule that was executed
    /// </summary>
    public string RuleName => Rule?.Name ?? AsyncRule?.Name ?? "unknown";

    /// <summary>
    /// Whether this result is from an async rule
    /// </summary>
    public bool IsAsyncRule => AsyncRule != null;

    /// <summary>
    /// The result of the rule execution
    /// </summary>
    public RuleResult Result { get; }

    /// <summary>
    /// Whether the rule was successfully executed without exceptions
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// The exception that occurred during execution, if any
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Creates a result for a sync rule execution
    /// </summary>
    public RuleExecutionResult(IRule<T> rule, RuleResult result, bool success, Exception? exception = null)
    {
        Rule = rule;
        Result = result;
        Success = success;
        Exception = exception;
    }

    /// <summary>
    /// Creates a result for an async rule execution
    /// </summary>
    public RuleExecutionResult(IAsyncRule<T> asyncRule, RuleResult result, bool success, Exception? exception = null)
    {
        AsyncRule = asyncRule;
        Result = result;
        Success = success;
        Exception = exception;
    }
}

// =============================================================================
// IMMUTABLE THREAD-SAFE VARIANT
// =============================================================================

/// <summary>
/// Thread-safe, immutable rules engine using immutable collections.
/// Returns new instances on modifications rather than mutating in place.
/// Use this when you need lock-free concurrent access with immutable semantics.
/// For mutable access with locking, use RulesEngineCore instead.
/// </summary>
public class ImmutableRulesEngine<T>
{
    private readonly ImmutableList<IRule<T>> _rules;
    private readonly RulesEngineOptions _options;
    private readonly ConcurrentDictionary<string, RulePerformanceMetrics> _metrics;

    private ImmutableRulesEngine(
        ImmutableList<IRule<T>> rules,
        RulesEngineOptions options,
        ConcurrentDictionary<string, RulePerformanceMetrics> metrics)
    {
        _rules = rules;
        _options = options;
        _metrics = metrics;
    }

    public ImmutableRulesEngine(RulesEngineOptions? options = null)
        : this(
            ImmutableList<IRule<T>>.Empty,
            options ?? new RulesEngineOptions(),
            new ConcurrentDictionary<string, RulePerformanceMetrics>())
    {
    }

    /// <summary>
    /// Returns a new engine with the rule added (immutable pattern)
    /// Thread-safe: No shared mutable state
    /// </summary>
    public ImmutableRulesEngine<T> WithRule(IRule<T> rule)
    {
        var newRules = _rules.Add(rule);
        return new ImmutableRulesEngine<T>(newRules, _options, _metrics);
    }

    /// <summary>
    /// Returns a new engine with multiple rules added
    /// </summary>
    public ImmutableRulesEngine<T> WithRules(params IRule<T>[] rules)
    {
        var newRules = _rules.AddRange(rules);
        return new ImmutableRulesEngine<T>(newRules, _options, _metrics);
    }

    /// <summary>
    /// Returns a new engine without the specified rule
    /// </summary>
    public ImmutableRulesEngine<T> WithoutRule(string ruleId)
    {
        var newRules = _rules.RemoveAll(r => r.Id == ruleId);
        return new ImmutableRulesEngine<T>(newRules, _options, _metrics);
    }

    /// <summary>
    /// Execute all rules - fully thread-safe
    /// Multiple threads can call this simultaneously
    /// </summary>
    public RulesEngineResult Execute(T fact)
    {
        var result = new RulesEngineResult();
        var startTime = DateTime.UtcNow;

        // Snapshot rules (already immutable, so this is safe)
        var sortedRules = _rules
            .OrderByDescending(r => r.Priority)
            .ToList();

        if (_options.EnableParallelExecution)
        {
            ExecuteParallel(fact, sortedRules, result);
        }
        else
        {
            ExecuteSequential(fact, sortedRules, result);
        }

        result.TotalExecutionTime = DateTime.UtcNow - startTime;
        return result;
    }

    private void ExecuteSequential(T fact, List<IRule<T>> rules, RulesEngineResult result)
    {
        foreach (var rule in rules)
        {
            var ruleStart = DateTime.UtcNow;
            var ruleResult = rule.Execute(fact);
            var duration = DateTime.UtcNow - ruleStart;

            result.AddRuleResult(ruleResult);

            if (_options.TrackPerformance)
            {
                TrackPerformance(rule.Id, duration);
            }

            if (_options.StopOnFirstMatch && ruleResult.Matched)
            {
                break;
            }
        }
    }

    private void ExecuteParallel(T fact, List<IRule<T>> rules, RulesEngineResult result)
    {
        var results = new ConcurrentBag<(RuleResult result, TimeSpan duration)>();

        Parallel.ForEach(rules, rule =>
        {
            var ruleStart = DateTime.UtcNow;
            var ruleResult = rule.Execute(fact);
            var duration = DateTime.UtcNow - ruleStart;

            results.Add((ruleResult, duration));

            if (_options.TrackPerformance)
            {
                TrackPerformance(rule.Id, duration);
            }
        });

        foreach (var (ruleResult, _) in results.OrderByDescending(r => r.result.Matched))
        {
            result.AddRuleResult(ruleResult);
        }
    }

    private void TrackPerformance(string ruleId, TimeSpan duration)
    {
        _metrics.AddOrUpdate(
            ruleId,
            _ => new RulePerformanceMetrics
            {
                RuleId = ruleId,
                ExecutionCount = 1,
                TotalExecutionTime = duration,
                AverageExecutionTime = duration,
                MinExecutionTime = duration,
                MaxExecutionTime = duration
            },
            (_, existing) =>
            {
                existing.ExecutionCount++;
                existing.TotalExecutionTime += duration;
                existing.AverageExecutionTime = TimeSpan.FromTicks(
                    existing.TotalExecutionTime.Ticks / existing.ExecutionCount);
                existing.MinExecutionTime = duration < existing.MinExecutionTime
                    ? duration
                    : existing.MinExecutionTime;
                existing.MaxExecutionTime = duration > existing.MaxExecutionTime
                    ? duration
                    : existing.MaxExecutionTime;
                return existing;
            }
        );
    }

    public IReadOnlyList<IRule<T>> GetRules() => _rules;

    public RulePerformanceMetrics? GetMetrics(string ruleId)
    {
        return _metrics.TryGetValue(ruleId, out var metrics) ? metrics : null;
    }

    public Dictionary<string, RulePerformanceMetrics> GetAllMetrics()
    {
        return new Dictionary<string, RulePerformanceMetrics>(_metrics);
    }
}
