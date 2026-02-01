using System.Collections.Concurrent;

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
public class RulesEngineCore<T> : IDisposable
{
    private readonly List<IRule<T>> _rules;
    private readonly RulesEngineOptions _options;
    private readonly ConcurrentDictionary<string, RulePerformanceMetrics> _metrics;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private bool _disposed;

    public RulesEngineCore(RulesEngineOptions? options = null)
    {
        _rules = new List<IRule<T>>();
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
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Evaluates all matching rules and applies their actions to the fact.
    /// Unlike Execute(), this modifies the fact in-place and doesn't return results.
    /// Rules are evaluated in priority order (highest first).
    /// </summary>
    public void EvaluateAll(T fact)
    {
        List<IRule<T>> sortedRules;

        _lock.EnterReadLock();
        try
        {
            sortedRules = _rules.OrderByDescending(r => r.Priority).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }

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
    /// Removes a rule by ID
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
    /// Clears all rules
    /// </summary>
    public void ClearRules()
    {
        _lock.EnterWriteLock();
        try
        {
            _rules.Clear();
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
    /// Evaluates all applicable rules against the fact
    /// </summary>
    public RulesEngineResult Execute(T fact)
    {
        var result = new RulesEngineResult();
        var startTime = DateTime.UtcNow;

        List<IRule<T>> sortedRules;

        _lock.EnterReadLock();
        try
        {
            // Sort rules by priority (descending)
            sortedRules = _rules.OrderByDescending(r => r.Priority).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // Limit rules if configured
        if (_options.MaxRulesToExecute.HasValue)
        {
            sortedRules = sortedRules.Take(_options.MaxRulesToExecute.Value).ToList();
        }

        // Execute outside the lock to avoid holding lock during potentially long-running rule execution
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
