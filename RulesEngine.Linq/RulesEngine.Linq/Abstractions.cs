namespace RulesEngine.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    #region Context and Session

    /// <summary>
    /// The execution context for rule operations. Backend-agnostic interface
    /// that can be implemented by in-memory, remote, or distributed providers.
    /// </summary>
    public interface IRulesContext : IDisposable
    {
        IRuleSession CreateSession();
        IRuleSet<T> GetRuleSet<T>() where T : class;
        void RegisterRuleSet<T>(IRuleSet<T> ruleSet) where T : class;
        bool HasRuleSet<T>() where T : class;
    }

    /// <summary>
    /// Represents a unit of work for rule evaluation.
    /// Tracks facts, collects results, manages evaluation lifecycle.
    /// </summary>
    public interface IRuleSession : IDisposable
    {
        Guid SessionId { get; }
        SessionState State { get; }

        IFactSet<T> Facts<T>() where T : class;
        void Insert<T>(T fact) where T : class;
        void InsertAll<T>(IEnumerable<T> facts) where T : class;

        IEvaluationResult Evaluate();
        IEvaluationResult<T> Evaluate<T>() where T : class;

        void Commit();
        void Rollback();
    }

    public enum SessionState
    {
        Active,
        Evaluating,
        Committed,
        RolledBack,
        Disposed
    }

    #endregion

    #region Rule and Fact Sets

    /// <summary>
    /// A queryable collection of rules for a specific fact type.
    /// Analogous to DbSet&lt;T&gt; in EF Core.
    /// </summary>
    public interface IRuleSet<T> : IQueryable<IRule<T>> where T : class
    {
        string Name { get; }
        Type FactType { get; }

        void Add(IRule<T> rule);
        void AddRange(IEnumerable<IRule<T>> rules);
        bool Remove(string ruleId);
        void Clear();

        IRule<T>? FindById(string ruleId);
        bool Contains(string ruleId);
        int Count { get; }
    }

    /// <summary>
    /// A queryable collection of facts within a session.
    /// Facts can be queried directly or filtered through rules.
    /// </summary>
    public interface IFactSet<T> : IQueryable<T> where T : class
    {
        int Count { get; }
        void Add(T fact);
        void AddRange(IEnumerable<T> facts);
        bool Remove(T fact);
        void Clear();

        IQueryable<T> Where(IRule<T> rule);
        IQueryable<FactRuleMatch<T>> WithMatchingRules();
    }

    #endregion

    #region Provider

    /// <summary>
    /// Provider abstraction for executing rule queries.
    /// In-memory implementation now, server implementation later.
    /// </summary>
    public interface IRuleProvider : IQueryProvider
    {
        void ValidateExpression(Expression expression);
        Func<T, bool> CompileCondition<T>(Expression<Func<T, bool>> condition);
        ExpressionCapabilities GetCapabilities();
    }

    /// <summary>
    /// Describes what expressions this provider can handle.
    /// Used for capability negotiation when switching backends.
    /// </summary>
    public class ExpressionCapabilities
    {
        public bool SupportsClosures { get; init; } = true;
        public bool SupportsMethodCalls { get; init; } = true;
        public bool SupportsSubqueries { get; init; } = false;
        public IReadOnlySet<string> SupportedMethods { get; init; } = new HashSet<string>();
    }

    #endregion

    #region Rules

    /// <summary>
    /// Core rule interface defining a rule that can be evaluated against facts of type T.
    /// </summary>
    public interface IRule<T>
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        int Priority { get; }
        Expression<Func<T, bool>> Condition { get; }

        bool Evaluate(T fact);
        RuleResult Execute(T fact);
    }

    #endregion

    #region Results

    /// <summary>
    /// Represents a fact paired with rules that matched it.
    /// </summary>
    public class FactRuleMatch<T> where T : class
    {
        public required T Fact { get; init; }
        public required IReadOnlyList<IRule<T>> MatchedRules { get; init; }
        public required IReadOnlyList<RuleResult> Results { get; init; }
    }

    /// <summary>
    /// Aggregate evaluation results across all fact types.
    /// </summary>
    public interface IEvaluationResult
    {
        Guid SessionId { get; }
        TimeSpan Duration { get; }
        int TotalFactsEvaluated { get; }
        int TotalRulesEvaluated { get; }
        int TotalMatches { get; }
        IReadOnlyList<EvaluationError> Errors { get; }
        bool HasErrors { get; }

        IEvaluationResult<T> ForType<T>() where T : class;
    }

    /// <summary>
    /// Evaluation results for a specific fact type.
    /// </summary>
    public interface IEvaluationResult<T> where T : class
    {
        IReadOnlyList<FactRuleMatch<T>> Matches { get; }
        IReadOnlyList<T> FactsWithMatches { get; }
        IReadOnlyList<T> FactsWithoutMatches { get; }
        IReadOnlyDictionary<string, int> MatchCountByRule { get; }
    }

    public class RuleResult
    {
        public string RuleId { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public bool Matched { get; set; }
        public bool ActionExecuted { get; set; }
        public DateTime ExecutedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Outputs { get; set; } = new();

        public static RuleResult Success(string ruleId, string ruleName) => new()
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Matched = true,
            ActionExecuted = true,
            ExecutedAt = DateTime.UtcNow
        };

        public static RuleResult NoMatch(string ruleId, string ruleName) => new()
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Matched = false,
            ActionExecuted = false,
            ExecutedAt = DateTime.UtcNow
        };

        public static RuleResult Error(string ruleId, string ruleName, string error) => new()
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Matched = false,
            ActionExecuted = false,
            ExecutedAt = DateTime.UtcNow,
            ErrorMessage = error
        };
    }

    public class EvaluationError
    {
        public required string RuleId { get; init; }
        public required object Fact { get; init; }
        public required Exception Exception { get; init; }
    }

    #endregion
}
