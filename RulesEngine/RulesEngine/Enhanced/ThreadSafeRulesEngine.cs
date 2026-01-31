using System.Collections.Concurrent;
using System.Collections.Immutable;
using RulesEngine.Core;

namespace RulesEngine.Enhanced;

/// <summary>
/// Thread-safe, immutable rules engine
/// Fixes: Thread safety issues with concurrent add/remove/execute
/// </summary>
public class ThreadSafeRulesEngine<T>
{
    private readonly ImmutableList<IRule<T>> _rules;
    private readonly RulesEngineOptions _options;
    private readonly ConcurrentDictionary<string, RulePerformanceMetrics> _metrics;
    
    private ThreadSafeRulesEngine(
        ImmutableList<IRule<T>> rules,
        RulesEngineOptions options,
        ConcurrentDictionary<string, RulePerformanceMetrics> metrics)
    {
        _rules = rules;
        _options = options;
        _metrics = metrics;
    }
    
    public ThreadSafeRulesEngine(RulesEngineOptions? options = null)
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
    public ThreadSafeRulesEngine<T> WithRule(IRule<T> rule)
    {
        var newRules = _rules.Add(rule);
        return new ThreadSafeRulesEngine<T>(newRules, _options, _metrics);
    }
    
    /// <summary>
    /// Returns a new engine with multiple rules added
    /// </summary>
    public ThreadSafeRulesEngine<T> WithRules(params IRule<T>[] rules)
    {
        var newRules = _rules.AddRange(rules);
        return new ThreadSafeRulesEngine<T>(newRules, _options, _metrics);
    }
    
    /// <summary>
    /// Returns a new engine without the specified rule
    /// </summary>
    public ThreadSafeRulesEngine<T> WithoutRule(string ruleId)
    {
        var newRules = _rules.RemoveAll(r => r.Id == ruleId);
        return new ThreadSafeRulesEngine<T>(newRules, _options, _metrics);
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

/// <summary>
/// Alternative: Traditional mutable engine with proper locking
/// </summary>
public class LockedRulesEngine<T> : IDisposable
{
    private readonly List<IRule<T>> _rules = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly RulesEngineOptions _options;
    
    public LockedRulesEngine(RulesEngineOptions? options = null)
    {
        _options = options ?? new RulesEngineOptions();
    }
    
    public void RegisterRule(IRule<T> rule)
    {
        _lock.EnterWriteLock();
        try
        {
            _rules.Add(rule);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
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
    
    public RulesEngineResult Execute(T fact)
    {
        _lock.EnterReadLock();
        try
        {
            var result = new RulesEngineResult();
            var sortedRules = _rules.OrderByDescending(r => r.Priority).ToList();
            
            foreach (var rule in sortedRules)
            {
                var ruleResult = rule.Execute(fact);
                result.AddRuleResult(ruleResult);
                
                if (_options.StopOnFirstMatch && ruleResult.Matched)
                {
                    break;
                }
            }
            
            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    
    public void Dispose()
    {
        _lock?.Dispose();
    }
}
