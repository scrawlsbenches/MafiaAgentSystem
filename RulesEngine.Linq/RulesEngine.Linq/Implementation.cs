namespace RulesEngine.Linq
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using RulesEngine.Linq.Dependencies;

    #region RulesContext

    /// <summary>
    /// In-memory implementation of IRulesContext.
    /// The entry point for all rule operations in the current process.
    /// </summary>
    public class RulesContext : IRulesContext
    {
        private readonly ConcurrentDictionary<Type, object> _ruleSets = new();
        private readonly IRuleProvider _provider;
        private FactSchema? _schema;
        private DependencyGraph? _dependencyGraph;
        private FactQueryProvider? _factQueryProvider;
        private RuleSession? _currentSession;
        private bool _disposed;

        public RulesContext() : this(new InMemoryRuleProvider()) { }

        public RulesContext(IRuleProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public IRuleProvider Provider => _provider;

        /// <summary>
        /// The schema for this context, if configured.
        /// Used for dependency validation and analysis at registration time.
        /// </summary>
        public IFactSchema? Schema => _schema;

        /// <summary>
        /// The dependency graph for this context.
        /// Tracks dependencies between fact types for load ordering.
        /// Created when ConfigureSchema is called.
        /// </summary>
        public DependencyGraph? DependencyGraph => _dependencyGraph;

        /// <summary>
        /// Returns an IQueryable for facts of type T.
        ///
        /// This is the key method for closure-driven cross-fact queries.
        /// Rules capture this context and call Facts&lt;T&gt;() in their expressions.
        ///
        /// At DEFINITION time: Returns a FactQueryable with FactQueryExpression as its root.
        /// The expression tree contains this symbolic reference, not actual data.
        ///
        /// At EVALUATION time: FactQueryRewriter substitutes actual session data.
        ///
        /// Example usage in a rule:
        ///   Rule.When&lt;Message&gt;(m => context.Facts&lt;Agent&gt;().Any(a => a.Available))
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if enumerated directly (must be used within rule expressions).
        /// </exception>
        public IQueryable<T> Facts<T>() where T : class
        {
            // Create provider lazily, with schema for validation if available
            _factQueryProvider ??= new FactQueryProvider(_schema);
            return new FactQueryable<T>(_factQueryProvider);
        }

        /// <summary>
        /// The current session, if one is active.
        /// Used internally for fact resolution during evaluation.
        /// </summary>
        internal RuleSession? CurrentSession => _currentSession;

        /// <summary>
        /// Sets the current session. Called by RuleSession during creation.
        /// </summary>
        internal void SetCurrentSession(RuleSession? session)
        {
            _currentSession = session;
        }

        public IRuleSet<T> GetRuleSet<T>() where T : class
        {
            return (IRuleSet<T>)_ruleSets.GetOrAdd(typeof(T), _ => new RuleSet<T>(this));
        }

        public void RegisterRuleSet<T>(IRuleSet<T> ruleSet) where T : class
        {
            if (!_ruleSets.TryAdd(typeof(T), ruleSet))
                throw new InvalidOperationException($"RuleSet for {typeof(T).Name} already exists");
        }

        public bool HasRuleSet<T>() where T : class => _ruleSets.ContainsKey(typeof(T));

        public ISchemaBuilder<T> ConfigureRuleSet<T>() where T : class
        {
            return new SchemaBuilder<T>(this);
        }

        /// <summary>
        /// Configure the schema for this context.
        /// The schema defines which fact types are valid and their relationships.
        /// Rules added after configuration will be validated against the schema.
        /// </summary>
        public void ConfigureSchema(Action<IFactSchemaBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            if (_schema != null) throw new InvalidOperationException("Schema has already been configured.");

            _schema = new FactSchema();
            _dependencyGraph = new DependencyGraph();
            var builder = new FactSchemaBuilderAdapter(_schema);
            configure(builder);

            // Wire schema-declared dependencies to the graph
            _schema.WireDependenciesToGraph(_dependencyGraph);
        }

        public IRuleSession CreateSession() => new RuleSession(this);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ruleSets.Clear();
        }
    }

    /// <summary>
    /// Builder interface for configuring the fact schema.
    /// </summary>
    public interface IFactSchemaBuilder
    {
        /// <summary>
        /// Register a fact type in the schema.
        /// </summary>
        void RegisterFactType<T>() where T : class;

        /// <summary>
        /// Register a fact type with configuration.
        /// </summary>
        void RegisterFactType<T>(Action<FactTypeSchemaBuilder<T>> configure) where T : class;
    }

    /// <summary>
    /// Adapter to use FactSchema with the builder interface.
    /// </summary>
    internal class FactSchemaBuilderAdapter : IFactSchemaBuilder
    {
        private readonly FactSchema _schema;

        public FactSchemaBuilderAdapter(FactSchema schema)
        {
            _schema = schema;
        }

        public void RegisterFactType<T>() where T : class
        {
            _schema.RegisterType<T>();
        }

        public void RegisterFactType<T>(Action<FactTypeSchemaBuilder<T>> configure) where T : class
        {
            _schema.RegisterType(configure);
        }
    }

    #endregion

    #region RuleSet

    /// <summary>
    /// In-memory queryable collection of rules.
    /// </summary>
    public class RuleSet<T> : IRuleSet<T> where T : class
    {
        private readonly List<IRule<T>> _rules = new();
        private readonly Dictionary<string, IRule<T>> _rulesById = new();
        private readonly RulesContext _context;
        private readonly object _lock = new();

        public RuleSet(RulesContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Name = typeof(T).Name + "Rules";
        }

        public string Name { get; }
        public Type FactType => typeof(T);
        public int Count { get { lock (_lock) return _rules.Count; } }

        public Type ElementType => typeof(IRule<T>);
        public Expression Expression => Expression.Constant(this);
        public IQueryProvider Provider => new RuleSetQueryProvider<T>(_context.Provider, this);

        public virtual void Add(IRule<T> rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            // If schema is configured, analyze and validate dependencies
            if (_context.Schema != null)
            {
                if (rule is DependentRule<T> dependentRule)
                {
                    dependentRule.AnalyzeDependencies(_context.Schema);

                    // Update the dependency graph with detected dependencies
                    if (_context.DependencyGraph != null)
                    {
                        _context.DependencyGraph.AddFactType(typeof(T), dependentRule.AllDependencies);
                    }
                }
                else if (rule is Rule<T> ruleT && ruleT.RequiresRewriting)
                {
                    // Rule<T> with closure-captured cross-fact queries needs dependency analysis
                    var extractor = new Dependencies.DependencyExtractor(_context.Schema);
                    var analysis = extractor.Analyze<T>(ruleT.Condition);

                    // Update the dependency graph with detected dependencies
                    if (_context.DependencyGraph != null && analysis.HasDependencies)
                    {
                        _context.DependencyGraph.AddFactType(typeof(T), analysis.FactTypeDependencies);
                    }
                }
            }

            lock (_lock)
            {
                if (_rulesById.ContainsKey(rule.Id))
                    throw new InvalidOperationException($"Rule with ID '{rule.Id}' already exists");
                _rules.Add(rule);
                _rulesById[rule.Id] = rule;
            }
        }

        public void AddRange(IEnumerable<IRule<T>> rules)
        {
            foreach (var rule in rules) Add(rule);
        }

        public bool Remove(string ruleId)
        {
            lock (_lock)
            {
                if (_rulesById.TryGetValue(ruleId, out var rule))
                {
                    _rules.Remove(rule);
                    _rulesById.Remove(ruleId);
                    return true;
                }
                return false;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _rules.Clear();
                _rulesById.Clear();
            }
        }

        public IRule<T>? FindById(string ruleId)
        {
            lock (_lock)
            {
                return _rulesById.TryGetValue(ruleId, out var rule) ? rule : null;
            }
        }

        public bool Contains(string ruleId)
        {
            lock (_lock) return _rulesById.ContainsKey(ruleId);
        }

        public virtual bool HasConstraints => false;

        public virtual IReadOnlyList<IRuleConstraint<T>> GetConstraints() => Array.Empty<IRuleConstraint<T>>();

        public virtual bool HasConstraint(string constraintName) => false;

        public virtual bool TryAdd(IRule<T> rule, out IReadOnlyList<ConstraintViolation> violations)
        {
            violations = Array.Empty<ConstraintViolation>();
            Add(rule);
            return true;
        }

        public IEnumerator<IRule<T>> GetEnumerator()
        {
            List<IRule<T>> snapshot;
            lock (_lock) snapshot = _rules.ToList();
            return snapshot.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal IReadOnlyList<IRule<T>> GetOrderedRules()
        {
            lock (_lock) return _rules.OrderByDescending(r => r.Priority).ToList();
        }
    }

    #endregion

    #region FactSet

    /// <summary>
    /// In-memory fact collection with rule-based querying.
    /// </summary>
    public class FactSet<T> : IFactSet<T> where T : class
    {
        private readonly List<T> _facts = new();
        private readonly RulesContext _context;
        private readonly IRuleSet<T> _ruleSet;
        private readonly FactSetQueryProvider<T> _provider;

        public FactSet(RulesContext context, IRuleSet<T> ruleSet)
        {
            _context = context;
            _ruleSet = ruleSet;
            _provider = new FactSetQueryProvider<T>(context.Provider, this, ruleSet);
        }

        public int Count => _facts.Count;
        public Type ElementType => typeof(T);
        public Expression Expression => Expression.Constant(this);
        public IQueryProvider Provider => _provider;

        public void Add(T fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            _facts.Add(fact);
        }

        public void AddRange(IEnumerable<T> facts)
        {
            foreach (var fact in facts) Add(fact);
        }

        public bool Remove(T fact) => _facts.Remove(fact);
        public void Clear() => _facts.Clear();

        public IQueryable<T> Where(IRule<T> rule)
        {
            _context.Provider.ValidateExpression(rule.Condition);
            var compiled = rule.Condition.Compile();
            return _facts.Where(compiled).AsQueryable();
        }

        public IQueryable<FactRuleMatch<T>> WithMatchingRules()
        {
            var rules = ((RuleSet<T>)_ruleSet).GetOrderedRules();

            return _facts.Select(fact =>
            {
                var matched = rules.Where(r => r.Evaluate(fact)).ToList();
                var results = matched.Select(r => r.Execute(fact)).ToList();
                return new FactRuleMatch<T>
                {
                    Fact = fact,
                    MatchedRules = matched,
                    Results = results
                };
            }).Where(m => m.MatchedRules.Count > 0).AsQueryable();
        }

        public IEnumerator<T> GetEnumerator() => _facts.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal IReadOnlyList<T> GetFacts() => _facts.AsReadOnly();
    }

    #endregion

    #region RuleSession

    /// <summary>
    /// In-memory session implementation with unit of work semantics.
    /// Also implements IFactContext to allow cross-fact queries during rule evaluation.
    /// </summary>
    public class RuleSession : IRuleSession, Dependencies.IFactContext
    {
        private readonly RulesContext _context;
        private readonly ConcurrentDictionary<Type, object> _factSets = new();
        private readonly List<EvaluationError> _errors = new();
        private SessionState _state = SessionState.Active;

        // Cache for rewritten conditions of generic IRule<T> implementations
        // that contain FactQueryExpression but aren't Rule<T> (which has its own cache)
        private readonly ConcurrentDictionary<string, Delegate> _rewrittenConditionCache = new();

        public RuleSession(RulesContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            SessionId = Guid.NewGuid();

            // Register this session as the current session on the context
            // This allows closure-captured context.Facts<T>() calls to resolve via this session
            _context.SetCurrentSession(this);
        }

        public Guid SessionId { get; }
        public SessionState State => _state;

        public IFactSet<T> Facts<T>() where T : class
        {
            ThrowIfNotActiveOrEvaluating();
            return (IFactSet<T>)_factSets.GetOrAdd(typeof(T),
                _ => new FactSet<T>(_context, _context.GetRuleSet<T>()));
        }

        public void Insert<T>(T fact) where T : class
        {
            ThrowIfNotActive();
            Facts<T>().Add(fact);
        }

        public void InsertAll<T>(IEnumerable<T> facts) where T : class
        {
            ThrowIfNotActive();
            Facts<T>().AddRange(facts);
        }

        public IEvaluationResult Evaluate()
        {
            ThrowIfNotActive();
            _state = SessionState.Evaluating;

            var startTime = DateTime.UtcNow;
            var results = new Dictionary<Type, object>();
            int totalFacts = 0, totalRules = 0, totalMatches = 0;

            // Determine evaluation order: use dependency graph if available, otherwise arbitrary
            IEnumerable<Type> typesToEvaluate;
            if (_context.DependencyGraph != null)
            {
                // Evaluate in dependency order (dependencies first)
                var loadOrder = _context.DependencyGraph.GetLoadOrder();
                // Include types from load order that have fact sets, plus any fact sets not in load order
                var orderedTypes = loadOrder.Where(t => _factSets.ContainsKey(t)).ToList();
                var remainingTypes = _factSets.Keys.Where(t => !loadOrder.Contains(t));
                typesToEvaluate = orderedTypes.Concat(remainingTypes);
            }
            else
            {
                typesToEvaluate = _factSets.Keys;
            }

            foreach (var type in typesToEvaluate)
            {
                if (!_factSets.TryGetValue(type, out var factSetObj))
                    continue;

                var evaluateMethod = typeof(RuleSession)
                    .GetMethod(nameof(EvaluateFactSet), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(type);

                object typeResult;
                try
                {
                    typeResult = evaluateMethod.Invoke(this, new[] { factSetObj })!;
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    throw ex.InnerException;
                }
                results[type] = typeResult;

                var matchesProperty = typeResult!.GetType().GetProperty("Matches")!;
                var matches = (IList)matchesProperty.GetValue(typeResult)!;

                var factSetType = factSetObj.GetType();
                var countProperty = factSetType.GetProperty("Count")!;
                totalFacts += (int)countProperty.GetValue(factSetObj)!;
                totalMatches += matches.Count;

                var rulesEvaluatedProperty = typeResult.GetType().GetProperty("RulesEvaluated");
                if (rulesEvaluatedProperty != null)
                    totalRules += (int)rulesEvaluatedProperty.GetValue(typeResult)!;
            }

            _state = SessionState.Active;

            return new EvaluationResultImpl
            {
                SessionId = SessionId,
                Duration = DateTime.UtcNow - startTime,
                TotalFactsEvaluated = totalFacts,
                TotalRulesEvaluated = totalRules,
                TotalMatches = totalMatches,
                Errors = _errors.ToList(),
                TypedResults = results
            };
        }

        public IEvaluationResult<T> Evaluate<T>() where T : class
        {
            ThrowIfNotActive();
            _state = SessionState.Evaluating;

            try
            {
                var factSet = Facts<T>();
                return EvaluateFactSet((FactSet<T>)factSet);
            }
            finally
            {
                _state = SessionState.Active;
            }
        }

        /// <summary>
        /// Dispatch classification for rules. Determined once per rule (not per fact).
        /// </summary>
        private enum RuleDispatchKind
        {
            Dependent,        // DependentRule<T> — uses EvaluateWithContext
            ClosureRewriting, // Rule<T> with RequiresRewriting — uses EvaluateWithRewriter
            GenericRewriting, // Any IRule<T> with FactQueryExpression — session rewrites
            Standard          // No cross-fact references — direct Evaluate
        }

        private EvaluationResultImpl<T> EvaluateFactSet<T>(FactSet<T> factSet) where T : class
        {
            var ruleSet = _context.GetRuleSet<T>();

            if (ruleSet is ConstrainedRuleSet<T> constrainedRuleSet)
            {
                constrainedRuleSet.ValidateForEvaluation();
            }

            var rules = ((RuleSet<T>)ruleSet).GetOrderedRules();
            var matches = new List<FactRuleMatch<T>>();
            var factsWithMatches = new List<T>();
            var factsWithoutMatches = new List<T>();
            var matchCountByRule = new Dictionary<string, int>();

            // Create rewriter for rules containing FactQueryExpression (closure-captured cross-fact queries)
            var rewriter = CreateFactQueryRewriter();

            // Clear stale caches from previous evaluations on this session (#2).
            // Caches bake fact data into compiled delegates via Expression.Constant.
            // If facts changed since last evaluation, stale delegates see old data.
            _rewrittenConditionCache.Clear();
            foreach (var rule in rules)
            {
                if (rule is Rule<T> ruleT)
                    ruleT.ClearSessionCache(SessionId);
            }

            // Pre-classify rules for dispatch — once per rule, not per fact (#5).
            // ContainsFactQuery walks the entire expression tree, so hoisting it
            // outside the fact loop avoids O(facts × rules × tree-depth) waste.
            var classifiedRules = new List<(IRule<T> rule, RuleDispatchKind kind)>(rules.Count);
            foreach (var rule in rules)
            {
                RuleDispatchKind kind;
                if (rule is DependentRule<T>)
                    kind = RuleDispatchKind.Dependent;
                else if (rule is Rule<T> rt && rt.RequiresRewriting)
                    kind = RuleDispatchKind.ClosureRewriting;
                else if (FactQueryExpression.ContainsFactQuery(rule.Condition))
                    kind = RuleDispatchKind.GenericRewriting;
                else
                    kind = RuleDispatchKind.Standard;
                classifiedRules.Add((rule, kind));
            }

            foreach (var fact in factSet)
            {
                var matchedRules = new List<IRule<T>>();
                var ruleResults = new List<RuleResult>();

                foreach (var (rule, kind) in classifiedRules)
                {
                    try
                    {
                        bool matched;
                        RuleResult result;

                        switch (kind)
                        {
                            case RuleDispatchKind.Dependent:
                            {
                                var dependentRule = (DependentRule<T>)rule;
                                matched = dependentRule.EvaluateWithContext(fact, this);
                                if (!matched) continue;
                                result = dependentRule.ExecuteWithContext(fact, this);
                                break;
                            }

                            case RuleDispatchKind.ClosureRewriting:
                            {
                                var ruleT = (Rule<T>)rule;
                                matched = ruleT.EvaluateWithRewriter(fact, rewriter, SessionId);
                                if (!matched) continue;
                                result = ruleT.ExecuteWithRewriter(fact, rewriter, SessionId);
                                break;
                            }

                            case RuleDispatchKind.GenericRewriting:
                            {
                                var compiled = GetOrCompileRewrittenCondition(rule, rewriter);
                                matched = compiled(fact);
                                if (!matched) continue;
                                result = rule.Execute(fact);
                                break;
                            }

                            default: // Standard
                            {
                                matched = rule.Evaluate(fact);
                                if (!matched) continue;
                                result = rule.Execute(fact);
                                break;
                            }
                        }

                        matchedRules.Add(rule);
                        ruleResults.Add(result);
                        matchCountByRule[rule.Id] = matchCountByRule.GetValueOrDefault(rule.Id) + 1;
                    }
                    catch (Exception ex)
                    {
                        _errors.Add(new EvaluationError
                        {
                            RuleId = rule.Id,
                            Fact = fact,
                            Exception = ex
                        });
                    }
                }

                if (matchedRules.Count > 0)
                {
                    factsWithMatches.Add(fact);
                    matches.Add(new FactRuleMatch<T>
                    {
                        Fact = fact,
                        MatchedRules = matchedRules,
                        Results = ruleResults
                    });
                }
                else
                {
                    factsWithoutMatches.Add(fact);
                }
            }

            return new EvaluationResultImpl<T>
            {
                Matches = matches,
                FactsWithMatches = factsWithMatches,
                FactsWithoutMatches = factsWithoutMatches,
                MatchCountByRule = matchCountByRule,
                RulesEvaluated = rules.Count
            };
        }

        public void Commit()
        {
            ThrowIfNotActive();
            _state = SessionState.Committed;
        }

        public void Rollback()
        {
            ThrowIfNotActive();
            _factSets.Clear();
            _errors.Clear();
            _state = SessionState.RolledBack;
        }

        public void Dispose()
        {
            if (_state == SessionState.Disposed) return;

            // Clear session-specific caches from rules to prevent memory leaks (#3).
            // Each session leaves compiled delegates (holding fact data via closures)
            // in Rule<T>._sessionCompiledCache. Clean them up.
            ClearAllRuleSessionCaches();
            _rewrittenConditionCache.Clear();

            // Clear this session from the context
            if (_context.CurrentSession == this)
                _context.SetCurrentSession(null);

            _factSets.Clear();
            _state = SessionState.Disposed;
        }

        /// <summary>
        /// Clears session-specific compiled caches from all rules across all fact types.
        /// </summary>
        private void ClearAllRuleSessionCaches()
        {
            foreach (var factType in _factSets.Keys)
            {
                try
                {
                    var clearMethod = typeof(RuleSession)
                        .GetMethod(nameof(ClearRuleSessionCachesForType),
                            BindingFlags.NonPublic | BindingFlags.Instance)!
                        .MakeGenericMethod(factType);
                    clearMethod.Invoke(this, null);
                }
                catch
                {
                    // Best-effort cleanup during disposal
                }
            }
        }

        private void ClearRuleSessionCachesForType<T>() where T : class
        {
            var ruleSet = _context.GetRuleSet<T>();
            foreach (var rule in ruleSet)
            {
                if (rule is Rule<T> ruleT)
                    ruleT.ClearSessionCache(SessionId);
            }
        }

        /// <summary>
        /// Gets facts of a specific type as IQueryable.
        /// Used by FactQueryRewriter to substitute actual data for FactQueryExpression nodes.
        /// </summary>
        internal IQueryable GetFactsAsQueryable(Type factType)
        {
            if (!_factSets.TryGetValue(factType, out var factSetObj))
            {
                // Return empty queryable for types with no facts
                var emptyListType = typeof(List<>).MakeGenericType(factType);
                var emptyList = Activator.CreateInstance(emptyListType)!;
                var asQueryableMethod = typeof(Queryable).GetMethod(nameof(Queryable.AsQueryable),
                    new[] { typeof(IEnumerable<>).MakeGenericType(factType) });

                // Use reflection to call AsQueryable on empty list
                var genericAsQueryable = typeof(Queryable)
                    .GetMethods()
                    .First(m => m.Name == nameof(Queryable.AsQueryable) && m.IsGenericMethod)
                    .MakeGenericMethod(factType);

                return (IQueryable)genericAsQueryable.Invoke(null, new[] { emptyList })!;
            }

            // Get the facts from the FactSet via reflection
            var factSetType = factSetObj.GetType();
            var getFactsMethod = factSetType.GetMethod("GetFacts", BindingFlags.Instance | BindingFlags.NonPublic);
            var facts = getFactsMethod!.Invoke(factSetObj, null);

            // Convert to IQueryable
            var queryableMethod = typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == nameof(Queryable.AsQueryable) && m.IsGenericMethod)
                .MakeGenericMethod(factType);

            return (IQueryable)queryableMethod.Invoke(null, new[] { facts })!;
        }

        /// <summary>
        /// Creates a FactQueryRewriter that resolves FactQueryExpression nodes
        /// to actual facts from this session.
        /// </summary>
        internal FactQueryRewriter CreateFactQueryRewriter()
        {
            return new FactQueryRewriter(type => GetFactsAsQueryable(type));
        }

        /// <summary>
        /// Rewrites, compiles, and caches a rule's Condition expression.
        /// Used by the generic rewriter dispatch for IRule&lt;T&gt; implementations
        /// that contain FactQueryExpression but aren't Rule&lt;T&gt; (which has its own cache).
        /// </summary>
        private Func<T, bool> GetOrCompileRewrittenCondition<T>(IRule<T> rule, FactQueryRewriter rewriter) where T : class
        {
            if (_rewrittenConditionCache.TryGetValue(rule.Id, out var cached))
                return (Func<T, bool>)cached;

            var rewritten = rewriter.Rewrite(rule.Condition);
            var lambda = (Expression<Func<T, bool>>)rewritten;
            var compiled = lambda.Compile();

            _rewrittenConditionCache[rule.Id] = compiled;
            return compiled;
        }

        private void ThrowIfNotActive()
        {
            if (_state != SessionState.Active)
                throw new InvalidOperationException($"Session is {_state}, expected Active");
        }

        private void ThrowIfNotActiveOrEvaluating()
        {
            if (_state != SessionState.Active && _state != SessionState.Evaluating)
                throw new InvalidOperationException($"Session is {_state}, expected Active or Evaluating");
        }

        #region IFactContext Implementation

        /// <summary>
        /// Returns queryable facts of type T for cross-fact rule evaluation.
        /// </summary>
        IQueryable<T> Dependencies.IFactContext.Facts<T>()
        {
            // IFactSet<T> already implements IQueryable<T>
            return Facts<T>();
        }

        /// <summary>
        /// Find a fact by its key. Uses the "Id" property by convention.
        /// </summary>
        T? Dependencies.IFactContext.FindByKey<T>(object key) where T : class
        {
            var keyString = key?.ToString();
            if (string.IsNullOrEmpty(keyString))
                return null;

            // Convention: look for "Id" property
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty == null)
                return null;

            // Enumerate to avoid expression tree issues with reflection
            foreach (var fact in Facts<T>())
            {
                var idValue = idProperty.GetValue(fact);
                if (idValue != null && idValue.ToString() == keyString)
                    return fact;
            }

            return null;
        }

        /// <summary>
        /// Returns all fact types that have been inserted into this session.
        /// </summary>
        IReadOnlySet<Type> Dependencies.IFactContext.RegisteredFactTypes =>
            _factSets.Keys.ToHashSet();

        #endregion
    }

    #endregion

    #region Result Implementations

    internal class EvaluationResultImpl : IEvaluationResult
    {
        public required Guid SessionId { get; init; }
        public required TimeSpan Duration { get; init; }
        public required int TotalFactsEvaluated { get; init; }
        public required int TotalRulesEvaluated { get; init; }
        public required int TotalMatches { get; init; }
        public required IReadOnlyList<EvaluationError> Errors { get; init; }
        public bool HasErrors => Errors.Count > 0;

        internal Dictionary<Type, object> TypedResults { get; init; } = new();

        public IEvaluationResult<T> ForType<T>() where T : class
        {
            if (TypedResults.TryGetValue(typeof(T), out var result))
                return (IEvaluationResult<T>)result;

            return new EvaluationResultImpl<T>
            {
                Matches = Array.Empty<FactRuleMatch<T>>(),
                FactsWithMatches = Array.Empty<T>(),
                FactsWithoutMatches = Array.Empty<T>(),
                MatchCountByRule = new Dictionary<string, int>()
            };
        }
    }

    internal class EvaluationResultImpl<T> : IEvaluationResult<T> where T : class
    {
        public required IReadOnlyList<FactRuleMatch<T>> Matches { get; init; }
        public required IReadOnlyList<T> FactsWithMatches { get; init; }
        public required IReadOnlyList<T> FactsWithoutMatches { get; init; }
        public required IReadOnlyDictionary<string, int> MatchCountByRule { get; init; }

        /// <summary>
        /// Number of rules evaluated for this fact type.
        /// Used by RuleSession.Evaluate() to compute TotalRulesEvaluated.
        /// </summary>
        public int RulesEvaluated { get; init; }
    }

    #endregion
}
