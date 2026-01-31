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
}

/// <summary>
/// Main rules engine that evaluates rules against facts
/// </summary>
public class RulesEngineCore<T>
{
    private readonly List<IRule<T>> _rules;
    private readonly RulesEngineOptions _options;
    private readonly ConcurrentDictionary<string, RulePerformanceMetrics> _metrics;
    
    public RulesEngineCore(RulesEngineOptions? options = null)
    {
        _rules = new List<IRule<T>>();
        _options = options ?? new RulesEngineOptions();
        _metrics = new ConcurrentDictionary<string, RulePerformanceMetrics>();
    }
    
    /// <summary>
    /// Registers a rule with the engine
    /// </summary>
    public void RegisterRule(IRule<T> rule)
    {
        _rules.Add(rule);
    }
    
    /// <summary>
    /// Registers multiple rules
    /// </summary>
    public void RegisterRules(params IRule<T>[] rules)
    {
        _rules.AddRange(rules);
    }
    
    /// <summary>
    /// Removes a rule by ID
    /// </summary>
    public bool RemoveRule(string ruleId)
    {
        var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule != null)
        {
            _rules.Remove(rule);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Clears all rules
    /// </summary>
    public void ClearRules()
    {
        _rules.Clear();
    }
    
    /// <summary>
    /// Gets all registered rules
    /// </summary>
    public IReadOnlyList<IRule<T>> GetRules() => _rules.AsReadOnly();
    
    /// <summary>
    /// Evaluates all applicable rules against the fact
    /// </summary>
    public RulesEngineResult Execute(T fact)
    {
        var result = new RulesEngineResult();
        var startTime = DateTime.UtcNow;
        
        // Sort rules by priority (descending)
        var sortedRules = _rules.OrderByDescending(r => r.Priority).ToList();
        
        // Limit rules if configured
        if (_options.MaxRulesToExecute.HasValue)
        {
            sortedRules = sortedRules.Take(_options.MaxRulesToExecute.Value).ToList();
        }
        
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
        return _rules.Where(r => r.Evaluate(fact)).ToList();
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
