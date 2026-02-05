namespace RulesEngine.Linq
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    /// <summary>
    /// Standard rule implementation with expression-based condition.
    /// Supports fluent configuration, composition, and tagging.
    ///
    /// CLOSURE-DRIVEN QUERIES:
    /// Rules can capture context.Facts&lt;T&gt;() in their expressions. These create
    /// FactQueryExpression nodes that are resolved at evaluation time.
    ///
    /// Example:
    ///   Rule.When&lt;Message&gt;(m => context.Facts&lt;Agent&gt;().Any(a => a.Available))
    ///
    /// At creation time, context.Facts&lt;Agent&gt;() returns a FactQueryable whose
    /// Expression property is FactQueryExpression. This gets embedded in the rule's
    /// expression tree.
    ///
    /// At evaluation time, FactQueryRewriter substitutes actual session data before
    /// the expression is compiled and executed.
    /// </summary>
    public class Rule<T> : IRule<T> where T : class
    {
        private Func<T, bool>? _compiledCondition;
        private readonly bool _requiresRewriting;
        private readonly List<Action<T>> _actions = new();
        private readonly List<string> _tags = new();

        // Cache for rewritten+compiled conditions per session
        // Key is session ID to avoid stale data between evaluations
        // ConcurrentDictionary for thread-safe access during concurrent evaluations
        private readonly ConcurrentDictionary<Guid, Func<T, bool>> _sessionCompiledCache = new();

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int Priority { get; private set; }
        public Expression<Func<T, bool>> Condition { get; }
        public IReadOnlyList<string> Tags => _tags;

        /// <summary>
        /// Whether this rule's condition contains FactQueryExpression nodes
        /// that need to be rewritten with actual session data before evaluation.
        /// </summary>
        public bool RequiresRewriting => _requiresRewriting;

        public Rule(string id, string name, Expression<Func<T, bool>> condition,
            string description = "", int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Rule ID cannot be null or empty", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Rule name cannot be null or empty", nameof(name));
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            Id = id;
            Name = name;
            Description = description;
            Priority = priority;
            Condition = condition;

            // Check if expression contains FactQueryExpression nodes
            _requiresRewriting = ContainsFactQueryExpression(condition);

            // Only compile immediately if no rewriting needed
            if (!_requiresRewriting)
            {
                _compiledCondition = condition.Compile();
            }
        }

        // Private constructor for cloning/composition
        private Rule(string id, string name, Expression<Func<T, bool>> condition,
            Func<T, bool>? compiledCondition, bool requiresRewriting, string description, int priority,
            List<string> tags, List<Action<T>> actions)
        {
            Id = id;
            Name = name;
            Description = description;
            Priority = priority;
            Condition = condition;
            _compiledCondition = compiledCondition;
            _requiresRewriting = requiresRewriting;
            _tags = new List<string>(tags);
            _actions = new List<Action<T>>(actions);
        }

        /// <summary>
        /// Checks if an expression contains FactQueryExpression nodes.
        /// Delegates to the shared utility on FactQueryExpression.
        /// </summary>
        private static bool ContainsFactQueryExpression(Expression expression)
            => FactQueryExpression.ContainsFactQuery(expression);

        #region Fluent Configuration

        public Rule<T> WithAction(Action<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _actions.Add(action);
            return this;
        }

        public Rule<T> Then(Action<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _actions.Add(action);
            return this;
        }

        public Rule<T> WithPriority(int priority)
        {
            Priority = priority;
            return this;
        }

        public Rule<T> WithTags(params string[] tags)
        {
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag) && !_tags.Contains(tag))
                        _tags.Add(tag);
                }
            }
            return this;
        }

        public Rule<T> WithId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Rule ID cannot be null or empty", nameof(id));
            Id = id;
            return this;
        }

        public Rule<T> WithName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Rule name cannot be null or empty", nameof(name));
            Name = name;
            return this;
        }

        public Rule<T> WithDescription(string description)
        {
            Description = description ?? string.Empty;
            return this;
        }

        public Rule<T> WithReason(string reason)
        {
            // Alias for WithDescription for more natural reading
            return WithDescription(reason);
        }

        #endregion

        #region Rule Composition

        public Rule<T> And(Rule<T> other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            var combined = CombineExpressions(Condition, other.Condition, Expression.AndAlso);
            var combinedRequiresRewriting = _requiresRewriting || other._requiresRewriting ||
                                            ContainsFactQueryExpression(combined);

            // Only compile if no rewriting needed
            Func<T, bool>? compiledCombined = combinedRequiresRewriting ? null : combined.Compile();

            var composedTags = new List<string>(_tags);
            foreach (var tag in other._tags)
            {
                if (!composedTags.Contains(tag))
                    composedTags.Add(tag);
            }

            var composedActions = new List<Action<T>>(_actions);
            composedActions.AddRange(other._actions);

            return new Rule<T>(
                $"{Id}_AND_{other.Id}",
                $"{Name} AND {other.Name}",
                combined,
                compiledCombined,
                combinedRequiresRewriting,
                Description,
                Math.Max(Priority, other.Priority),
                composedTags,
                composedActions
            );
        }

        public Rule<T> Or(Rule<T> other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            var combined = CombineExpressions(Condition, other.Condition, Expression.OrElse);
            var combinedRequiresRewriting = _requiresRewriting || other._requiresRewriting ||
                                            ContainsFactQueryExpression(combined);

            // Only compile if no rewriting needed
            Func<T, bool>? compiledCombined = combinedRequiresRewriting ? null : combined.Compile();

            var composedTags = new List<string>(_tags);
            foreach (var tag in other._tags)
            {
                if (!composedTags.Contains(tag))
                    composedTags.Add(tag);
            }

            var composedActions = new List<Action<T>>(_actions);
            composedActions.AddRange(other._actions);

            return new Rule<T>(
                $"{Id}_OR_{other.Id}",
                $"{Name} OR {other.Name}",
                combined,
                compiledCombined,
                combinedRequiresRewriting,
                Description,
                Math.Max(Priority, other.Priority),
                composedTags,
                composedActions
            );
        }

        private static Expression<Func<T, bool>> CombineExpressions(
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right,
            Func<Expression, Expression, BinaryExpression> combiner)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
            var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
            var combined = combiner(leftBody, rightBody);
            return Expression.Lambda<Func<T, bool>>(combined, parameter);
        }

        private static Expression ReplaceParameter(
            Expression expression,
            ParameterExpression oldParameter,
            ParameterExpression newParameter)
        {
            return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
        }

        #endregion

        #region Evaluation

        /// <summary>
        /// Evaluates the rule condition against the fact.
        /// For rules that require rewriting (contain FactQueryExpression),
        /// this will throw - use EvaluateWithRewriter instead.
        /// </summary>
        public bool Evaluate(T fact)
        {
            if (fact == null) return false;

            if (_requiresRewriting)
            {
                throw new InvalidOperationException(
                    $"Rule '{Name}' contains cross-fact queries (context.Facts<T>()) and requires " +
                    "rewriting before evaluation. Use EvaluateWithRewriter or evaluate through Session.Evaluate().");
            }

            try
            {
                return _compiledCondition!(fact);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Evaluates the rule condition with a rewriter that substitutes FactQueryExpression
        /// nodes with actual session data.
        /// </summary>
        /// <param name="fact">The fact to evaluate against.</param>
        /// <param name="rewriter">The rewriter that resolves FactQueryExpression to actual data.</param>
        /// <param name="sessionId">Session ID for caching compiled conditions.</param>
        public bool EvaluateWithRewriter(T fact, FactQueryRewriter rewriter, Guid sessionId)
        {
            if (fact == null) return false;

            try
            {
                var compiled = GetOrCompileWithRewriter(rewriter, sessionId);
                return compiled(fact);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a compiled condition for the given session, rewriting if necessary.
        /// Results are cached per session ID.
        /// </summary>
        private Func<T, bool> GetOrCompileWithRewriter(FactQueryRewriter rewriter, Guid sessionId)
        {
            // If no rewriting needed, use the pre-compiled condition
            if (!_requiresRewriting && _compiledCondition != null)
            {
                return _compiledCondition;
            }

            // Check cache for this session
            if (_sessionCompiledCache.TryGetValue(sessionId, out var cached))
            {
                return cached;
            }

            // Rewrite the expression tree to substitute actual facts
            var rewrittenExpression = rewriter.Rewrite(Condition);
            var rewrittenLambda = (Expression<Func<T, bool>>)rewrittenExpression;
            var compiled = rewrittenLambda.Compile();

            // Cache for this session
            _sessionCompiledCache[sessionId] = compiled;

            return compiled;
        }

        /// <summary>
        /// Clears the compiled condition cache for a specific session.
        /// Should be called when a session is disposed.
        /// </summary>
        public void ClearSessionCache(Guid sessionId)
        {
            _sessionCompiledCache.TryRemove(sessionId, out _);
        }

        public RuleResult Execute(T fact)
        {
            if (_requiresRewriting)
            {
                throw new InvalidOperationException(
                    $"Rule '{Name}' contains cross-fact queries (context.Facts<T>()) and requires " +
                    "rewriting before execution. Use ExecuteWithRewriter or execute through Session.Evaluate().");
            }

            bool matched;
            try
            {
                matched = _compiledCondition!(fact);
                if (!matched)
                    return RuleResult.NoMatch(Id, Name);
            }
            catch (Exception ex)
            {
                return RuleResult.Error(Id, Name, ex.Message);
            }

            try
            {
                foreach (var action in _actions)
                    action(fact);

                return RuleResult.Success(Id, Name);
            }
            catch (Exception ex)
            {
                return new RuleResult
                {
                    RuleId = Id,
                    RuleName = Name,
                    Matched = true,
                    ActionExecuted = false,
                    ExecutedAt = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Executes the rule with a rewriter that substitutes FactQueryExpression
        /// nodes with actual session data.
        /// </summary>
        public RuleResult ExecuteWithRewriter(T fact, FactQueryRewriter rewriter, Guid sessionId)
        {
            bool matched;
            try
            {
                matched = EvaluateWithRewriter(fact, rewriter, sessionId);
                if (!matched)
                    return RuleResult.NoMatch(Id, Name);
            }
            catch (Exception ex)
            {
                return RuleResult.Error(Id, Name, ex.Message);
            }

            try
            {
                foreach (var action in _actions)
                    action(fact);

                return RuleResult.Success(Id, Name);
            }
            catch (Exception ex)
            {
                return new RuleResult
                {
                    RuleId = Id,
                    RuleName = Name,
                    Matched = true,
                    ActionExecuted = false,
                    ExecutedAt = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
            }
        }

        #endregion

        #region Helper Classes

        private class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;

            public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _oldParameter ? _newParameter : base.VisitParameter(node);
            }
        }

        #endregion
    }

    /// <summary>
    /// Fluent builder for creating rules.
    /// </summary>
    public class RuleBuilder<T> where T : class
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private int _priority;
        private Expression<Func<T, bool>>? _condition;
        private readonly List<Action<T>> _actions = new();
        private readonly List<string> _tags = new();

        public RuleBuilder<T> WithId(string id)
        {
            _id = id;
            return this;
        }

        public RuleBuilder<T> WithName(string name)
        {
            _name = name;
            return this;
        }

        public RuleBuilder<T> WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public RuleBuilder<T> WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        public RuleBuilder<T> WithTags(params string[] tags)
        {
            if (tags != null)
                _tags.AddRange(tags.Where(t => !string.IsNullOrWhiteSpace(t)));
            return this;
        }

        public RuleBuilder<T> When(Expression<Func<T, bool>> condition)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            _condition = condition;
            return this;
        }

        public RuleBuilder<T> And(Expression<Func<T, bool>> condition)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (_condition == null)
                throw new InvalidOperationException("Call When() before And()");

            _condition = CombineWithAnd(_condition, condition);
            return this;
        }

        public RuleBuilder<T> Or(Expression<Func<T, bool>> condition)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (_condition == null)
                throw new InvalidOperationException("Call When() before Or()");

            _condition = CombineWithOr(_condition, condition);
            return this;
        }

        public RuleBuilder<T> Not()
        {
            if (_condition == null)
                throw new InvalidOperationException("Call When() before Not()");

            var parameter = _condition.Parameters[0];
            var negated = Expression.Not(_condition.Body);
            _condition = Expression.Lambda<Func<T, bool>>(negated, parameter);
            return this;
        }

        public RuleBuilder<T> Then(Action<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _actions.Add(action);
            return this;
        }

        public Rule<T> Build()
        {
            if (_condition == null)
                throw new InvalidOperationException("Rule must have a condition. Call When().");

            if (string.IsNullOrWhiteSpace(_id))
                _id = Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(_name))
                _name = _id;

            var rule = new Rule<T>(_id, _name, _condition, _description, _priority);

            if (_tags.Count > 0)
                rule.WithTags(_tags.ToArray());

            foreach (var action in _actions)
                rule.WithAction(action);

            return rule;
        }

        private static Expression<Func<T, bool>> CombineWithAnd(
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
            var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
            var combined = Expression.AndAlso(leftBody, rightBody);
            return Expression.Lambda<Func<T, bool>>(combined, parameter);
        }

        private static Expression<Func<T, bool>> CombineWithOr(
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
            var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
            var combined = Expression.OrElse(leftBody, rightBody);
            return Expression.Lambda<Func<T, bool>>(combined, parameter);
        }

        private static Expression ReplaceParameter(
            Expression expression,
            ParameterExpression oldParameter,
            ParameterExpression newParameter)
        {
            return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
        }

        private class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;

            public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _oldParameter ? _newParameter : base.VisitParameter(node);
            }
        }
    }

    /// <summary>
    /// Static factory for creating rules with a fluent syntax.
    /// </summary>
    public static class Rule
    {
        public static RuleBuilder<T> When<T>(Expression<Func<T, bool>> condition) where T : class
        {
            return new RuleBuilder<T>().When(condition);
        }

        public static RuleBuilder<T> For<T>() where T : class
        {
            return new RuleBuilder<T>();
        }
    }

    /// <summary>
    /// Result of matching a fact against rules, including which rules matched.
    /// </summary>
    public class FactRuleMatch<T> where T : class
    {
        public required T Fact { get; init; }
        public required IReadOnlyList<IRule<T>> MatchedRules { get; init; }
        public required IReadOnlyList<RuleResult> Results { get; init; }

        // Alias for convenience in tests
        public IReadOnlyList<IRule<T>> Rules => MatchedRules;

        public bool HasMatches => MatchedRules.Count > 0;
    }

    /// <summary>
    /// Queryable wrapper for facts that can include matching rule information.
    /// </summary>
    public class FactWithRulesQueryable<T> where T : class
    {
        private readonly IEnumerable<T> _facts;
        private readonly IEnumerable<IRule<T>> _rules;

        public FactWithRulesQueryable(IEnumerable<T> facts, IEnumerable<IRule<T>> rules)
        {
            _facts = facts;
            _rules = rules;
        }

        public IEnumerable<FactRuleMatch<T>> WithMatchingRules()
        {
            foreach (var fact in _facts)
            {
                var matchingRules = _rules.Where(r => r.Evaluate(fact)).ToList();
                var results = matchingRules.Select(r => r.Execute(fact)).ToList();
                yield return new FactRuleMatch<T>
                {
                    Fact = fact,
                    MatchedRules = matchingRules,
                    Results = results
                };
            }
        }

        public IEnumerable<FactRuleMatch<T>> Where(Func<FactRuleMatch<T>, bool> predicate)
        {
            return WithMatchingRules().Where(predicate);
        }
    }
}
