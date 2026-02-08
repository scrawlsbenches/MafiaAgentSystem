namespace RulesEngine.Linq
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using RulesEngine.Linq.Dependencies; // For IFactSchema

    #region In-Memory Provider

    /// <summary>
    /// In-memory provider that validates and executes expressions locally.
    /// </summary>
    public class InMemoryRuleProvider : IRuleProvider
    {
        private readonly ConcurrentDictionary<Expression, Delegate> _compiledCache = new();
        private readonly ExpressionValidator _validator = new();
        private readonly ExpressionCapabilities _capabilities;

        public InMemoryRuleProvider() : this(new ExpressionCapabilities()) { }

        public InMemoryRuleProvider(ExpressionCapabilities capabilities)
        {
            _capabilities = capabilities;
        }

        public ExpressionCapabilities GetCapabilities() => _capabilities;

        public void ValidateExpression(Expression expression)
        {
            _validator.Validate(expression, _capabilities);
        }

        public Func<T, bool> CompileCondition<T>(Expression<Func<T, bool>> condition)
        {
            return (Func<T, bool>)_compiledCache.GetOrAdd(condition, expr =>
            {
                _validator.Validate(expr, _capabilities);
                return ((Expression<Func<T, bool>>)expr).Compile();
            });
        }

        public IQueryable CreateQuery(Expression expression)
        {
            throw new NotSupportedException("Use typed CreateQuery<T>");
        }

        public IQueryable<T> CreateQuery<T>(Expression expression)
        {
            ValidateExpression(expression);
            return new RuleQuery<T>(this, expression);
        }

        public object? Execute(Expression expression)
        {
            ValidateExpression(expression);
            var lambda = Expression.Lambda(expression);
            return lambda.Compile().DynamicInvoke();
        }

        public TResult Execute<TResult>(Expression expression)
        {
            ValidateExpression(expression);
            var lambda = Expression.Lambda<Func<TResult>>(expression);
            return lambda.Compile()();
        }
    }

    #endregion

    #region Query Types

    /// <summary>
    /// Generic queryable wrapper used by providers.
    /// </summary>
    public class RuleQuery<T> : IQueryable<T>, IOrderedQueryable<T>
    {
        private readonly IQueryProvider _provider;
        private readonly Expression _expression;

        public RuleQuery(IQueryProvider provider, Expression expression)
        {
            _provider = provider;
            _expression = expression;
        }

        public Type ElementType => typeof(T);
        public Expression Expression => _expression;
        public IQueryProvider Provider => _provider;

        public IEnumerator<T> GetEnumerator()
        {
            return _provider.Execute<IEnumerable<T>>(_expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    #endregion

    #region RuleSet Query Provider

    /// <summary>
    /// Query provider for RuleSet queries.
    /// </summary>
    internal class RuleSetQueryProvider<T> : IQueryProvider where T : class
    {
        private readonly IRuleProvider _baseProvider;
        private readonly RuleSet<T> _ruleSet;

        public RuleSetQueryProvider(IRuleProvider baseProvider, RuleSet<T> ruleSet)
        {
            _baseProvider = baseProvider;
            _ruleSet = ruleSet;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetSequenceElementType() ?? typeof(IRule<T>);
            var queryType = typeof(RuleQuery<>).MakeGenericType(elementType);
            return (IQueryable)Activator.CreateInstance(queryType, this, expression)!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            _baseProvider.ValidateExpression(expression);
            return new RuleQuery<TElement>(this, expression);
        }

        public object? Execute(Expression expression)
        {
            return Execute<IEnumerable<IRule<T>>>(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            _baseProvider.ValidateExpression(expression);
            var rewritten = new RuleSetSourceRewriter<T>(_ruleSet).Visit(expression);
            var lambda = Expression.Lambda<Func<TResult>>(rewritten);
            return lambda.Compile()();
        }
    }

    /// <summary>
    /// Rewrites RuleSet constants to actual enumerable for execution.
    /// </summary>
    internal class RuleSetSourceRewriter<T> : ExpressionVisitor where T : class
    {
        private readonly RuleSet<T> _ruleSet;

        public RuleSetSourceRewriter(RuleSet<T> ruleSet) => _ruleSet = ruleSet;

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is RuleSet<T>)
                return Expression.Constant(_ruleSet.GetOrderedRules().AsQueryable());
            return base.VisitConstant(node);
        }
    }

    #endregion

    #region FactSet Query Provider

    /// <summary>
    /// Query provider for FactSet queries.
    /// </summary>
    internal class FactSetQueryProvider<T> : IQueryProvider where T : class
    {
        private readonly IRuleProvider _baseProvider;
        private readonly FactSet<T> _factSet;
        private readonly IRuleSet<T> _ruleSet;

        public FactSetQueryProvider(IRuleProvider baseProvider, FactSet<T> factSet, IRuleSet<T> ruleSet)
        {
            _baseProvider = baseProvider;
            _factSet = factSet;
            _ruleSet = ruleSet;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetSequenceElementType() ?? typeof(T);
            var queryType = typeof(RuleQuery<>).MakeGenericType(elementType);
            return (IQueryable)Activator.CreateInstance(queryType, this, expression)!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            _baseProvider.ValidateExpression(expression);
            return new RuleQuery<TElement>(this, expression);
        }

        public object? Execute(Expression expression) => Execute<IEnumerable<T>>(expression);

        public TResult Execute<TResult>(Expression expression)
        {
            _baseProvider.ValidateExpression(expression);
            var rewritten = new FactSetSourceRewriter<T>(_factSet).Visit(expression);
            var lambda = Expression.Lambda<Func<TResult>>(rewritten);
            return lambda.Compile()();
        }
    }

    /// <summary>
    /// Rewrites FactSet constants to actual enumerable for execution.
    /// </summary>
    internal class FactSetSourceRewriter<T> : ExpressionVisitor where T : class
    {
        private readonly FactSet<T> _factSet;

        public FactSetSourceRewriter(FactSet<T> factSet) => _factSet = factSet;

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is FactSet<T>)
                return Expression.Constant(_factSet.GetFacts().AsQueryable());
            return base.VisitConstant(node);
        }
    }

    #endregion

    #region Fact Query Expression (EF Core-inspired pattern)

    /// <summary>
    /// Custom expression node representing "query facts of type T".
    ///
    /// This is the key to closure-driven cross-fact queries. When a rule captures
    /// context.Facts&lt;Agent&gt;(), the expression tree contains this node instead of
    /// actual data. At evaluation time, FactQueryRewriter substitutes real facts.
    ///
    /// Pattern inspired by EF Core's EntityQueryRootExpression.
    ///
    /// Example expression tree for: context.Facts&lt;Agent&gt;().Any(a => a.Available)
    ///   Call(Any,
    ///     FactQueryExpression(typeof(Agent)),  &lt;-- This node
    ///     Lambda(a => a.Available))
    /// </summary>
    public class FactQueryExpression : Expression
    {
        /// <summary>
        /// Creates a new fact query expression for the specified fact type.
        /// </summary>
        /// <param name="factType">The type of facts to query (e.g., typeof(Agent))</param>
        public FactQueryExpression(Type factType)
        {
            FactType = factType ?? throw new ArgumentNullException(nameof(factType));
            Type = typeof(IQueryable<>).MakeGenericType(factType);
        }

        /// <summary>
        /// The fact type this expression queries.
        /// </summary>
        public Type FactType { get; }

        /// <summary>
        /// Returns ExpressionType.Extension for custom expression nodes.
        /// </summary>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// The type this expression evaluates to: IQueryable&lt;FactType&gt;.
        /// </summary>
        public override Type Type { get; }

        /// <summary>
        /// This expression has no children to visit.
        /// </summary>
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

        /// <summary>
        /// Indicates this node can be reduced (though we handle it via rewriting).
        /// </summary>
        public override bool CanReduce => false;

        /// <summary>
        /// String representation for debugging.
        /// </summary>
        public override string ToString() => $"Facts<{FactType.Name}>()";

        /// <summary>
        /// Checks whether an expression tree contains FactQueryExpression nodes
        /// (or closure-captured FactQueryable&lt;T&gt; instances that represent them).
        ///
        /// Used by the evaluation pipeline to detect rules that need rewriting
        /// before compilation, regardless of concrete IRule&lt;T&gt; implementation.
        /// </summary>
        public static bool ContainsFactQuery(Expression expression)
        {
            var detector = new FactQueryDetector();
            detector.Visit(expression);
            return detector.Found;
        }

        /// <summary>
        /// Visitor that detects FactQueryExpression nodes in an expression tree.
        /// Handles three patterns:
        /// 1. Direct FactQueryExpression extension nodes
        /// 2. Constant FactQueryable&lt;T&gt; instances
        /// 3. Closure member access to FactQueryable&lt;T&gt; fields
        /// </summary>
        private class FactQueryDetector : ExpressionVisitor
        {
            public bool Found { get; private set; }

            protected override Expression VisitExtension(Expression node)
            {
                if (node is FactQueryExpression)
                {
                    Found = true;
                    return node;
                }
                return base.VisitExtension(node);
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Value != null)
                {
                    var type = node.Value.GetType();
                    if (type.IsGenericType &&
                        type.GetGenericTypeDefinition() == typeof(FactQueryable<>))
                    {
                        Found = true;
                    }
                }
                return base.VisitConstant(node);
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression is ConstantExpression ce && ce.Value != null)
                {
                    try
                    {
                        var value = Expression.Lambda(node).Compile().DynamicInvoke();
                        if (value is FactQueryExpression)
                        {
                            Found = true;
                        }
                        else if (value != null)
                        {
                            var valueType = value.GetType();
                            if (valueType.IsGenericType &&
                                valueType.GetGenericTypeDefinition() == typeof(FactQueryable<>))
                            {
                                Found = true;
                            }
                        }
                    }
                    catch
                    {
                        // Conservative: if we can't evaluate the closure field, assume it
                        // might be a FactQueryable. Better to trigger unnecessary rewriting
                        // (which will throw with a clear message) than to silently skip it
                        // and send the rule down the Standard path where it fails with
                        // "cannot be enumerated directly".
                        Found = true;
                    }
                }
                return base.VisitMember(node);
            }
        }
    }

    /// <summary>
    /// IQueryable implementation that uses FactQueryExpression as its expression root.
    ///
    /// This is returned by RulesContext.Facts&lt;T&gt;(). It builds expression trees
    /// but throws if you try to enumerate it outside of rule evaluation.
    ///
    /// WHY THIS EXISTS:
    /// When you write context.Facts&lt;Agent&gt;().Where(a => a.Available), we need:
    /// 1. An IQueryable that LINQ methods can chain on
    /// 2. An Expression property that returns FactQueryExpression (not actual data)
    /// 3. Enumeration blocked until evaluation time
    /// </summary>
    public class FactQueryable<T> : IQueryable<T>, IOrderedQueryable<T> where T : class
    {
        private readonly FactQueryProvider _provider;
        private readonly Expression _expression;

        /// <summary>
        /// Creates a new FactQueryable with FactQueryExpression as its root.
        /// </summary>
        public FactQueryable(FactQueryProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _expression = new FactQueryExpression(typeof(T));
        }

        /// <summary>
        /// Creates a FactQueryable with a custom expression (for chained queries).
        /// </summary>
        internal FactQueryable(FactQueryProvider provider, Expression expression)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public Type ElementType => typeof(T);

        /// <summary>
        /// The expression tree representing this query.
        /// For the root, this is FactQueryExpression. For chained queries,
        /// this includes the full expression tree (Where, Any, etc.).
        /// </summary>
        public Expression Expression => _expression;

        public IQueryProvider Provider => _provider;

        /// <summary>
        /// Throws - enumeration only allowed during rule evaluation via rewriter.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            throw new InvalidOperationException(
                "Facts<T>() cannot be enumerated directly. " +
                "It can only be used within rule expressions and is evaluated during Session.Evaluate().");
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Query provider for FactQueryable. Builds expression trees but throws on execution.
    ///
    /// This provider is used at rule DEFINITION time. It allows LINQ methods to build
    /// expression trees, but blocks actual execution. Real execution happens at
    /// EVALUATION time when FactQueryRewriter substitutes actual session data.
    /// </summary>
    public class FactQueryProvider : IQueryProvider
    {
        private readonly IFactSchema? _schema;

        public FactQueryProvider(IFactSchema? schema = null)
        {
            _schema = schema;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetSequenceElementType() ?? typeof(object);
            var queryType = typeof(FactQueryable<>).MakeGenericType(elementType);
            // Use reflection to invoke the internal constructor (provider, expression)
            var instance = Activator.CreateInstance(queryType,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { this, expression }, null);
            return (IQueryable)instance!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            // Validate fact type is registered if we have a schema
            ValidateFactTypeIfNeeded(expression);

            // Create appropriate queryable based on element type
            var elementType = typeof(TElement);
            var queryableType = typeof(FactQueryable<>).MakeGenericType(elementType);
            var instance = Activator.CreateInstance(queryableType,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { this, expression }, null);
            return (IQueryable<TElement>)instance!;
        }

        public object? Execute(Expression expression)
        {
            throw new InvalidOperationException(
                "Facts<T>() queries cannot be executed directly. " +
                "They are evaluated during Session.Evaluate() when actual facts are available.");
        }

        public TResult Execute<TResult>(Expression expression)
        {
            throw new InvalidOperationException(
                "Facts<T>() queries cannot be executed directly. " +
                "They are evaluated during Session.Evaluate() when actual facts are available.");
        }

        private void ValidateFactTypeIfNeeded(Expression expression)
        {
            if (_schema == null) return;

            // Walk expression to find FactQueryExpression nodes and validate their types
            var validator = new FactTypeValidator(_schema);
            validator.Visit(expression);
        }

        /// <summary>
        /// Validates that all FactQueryExpression nodes reference registered fact types.
        /// </summary>
        private class FactTypeValidator : ExpressionVisitor
        {
            private readonly IFactSchema _schema;

            public FactTypeValidator(IFactSchema schema) => _schema = schema;

            protected override Expression VisitExtension(Expression node)
            {
                if (node is FactQueryExpression fqe)
                {
                    if (!_schema.IsRegistered(fqe.FactType))
                    {
                        throw new InvalidOperationException(
                            $"Fact type {fqe.FactType.Name} is not registered in the schema. " +
                            $"Call schema.RegisterFactType<{fqe.FactType.Name}>() in ConfigureSchema.");
                    }
                }
                return base.VisitExtension(node);
            }
        }
    }

    /// <summary>
    /// Rewrites FactQueryExpression nodes to actual session data at evaluation time.
    ///
    /// This is the bridge between expression trees (defined at rule creation time)
    /// and actual facts (available at evaluation time).
    ///
    /// Handles two patterns:
    /// 1. Direct FactQueryExpression nodes (from FactQueryable.Expression)
    /// 2. MemberExpression accessing a closure field that contains a FactQueryable
    ///
    /// Example:
    ///   Before: Call(Any, MemberAccess(closure, "agents"), Lambda)  // agents is FactQueryable
    ///   After:  Call(Any, Constant(agentList.AsQueryable()), Lambda)
    /// </summary>
    public class FactQueryRewriter : ExpressionVisitor
    {
        private readonly Func<Type, IQueryable> _factResolver;

        /// <summary>
        /// Creates a rewriter that resolves facts using the provided function.
        /// </summary>
        /// <param name="factResolver">
        /// Function that returns IQueryable for a given fact type.
        /// Typically: type => session.GetFactsForType(type).AsQueryable()
        /// </param>
        public FactQueryRewriter(Func<Type, IQueryable> factResolver)
        {
            _factResolver = factResolver ?? throw new ArgumentNullException(nameof(factResolver));
        }

        /// <summary>
        /// Rewrites the expression, substituting FactQueryExpression with actual data.
        /// </summary>
        public Expression Rewrite(Expression expression)
        {
            return Visit(expression);
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (node is FactQueryExpression fqe)
            {
                // Resolve actual facts from session
                var facts = _factResolver(fqe.FactType);
                return Expression.Constant(facts, fqe.Type);
            }
            return base.VisitExtension(node);
        }

        /// <summary>
        /// Handle bare ConstantExpression nodes that contain FactQueryable&lt;T&gt; instances.
        /// This mirrors FactQueryDetector.VisitConstant for completeness.
        /// Normal C# closures produce MemberAccess(Constant(DisplayClass), field), not bare constants,
        /// so this only applies to hand-built expression trees using Expression.Constant(factQueryable).
        /// </summary>
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value != null)
            {
                var type = node.Value.GetType();
                if (type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(FactQueryable<>))
                {
                    var factType = type.GetGenericArguments()[0];
                    var facts = _factResolver(factType);
                    var queryableType = typeof(IQueryable<>).MakeGenericType(factType);
                    return Expression.Constant(facts, queryableType);
                }
            }
            return base.VisitConstant(node);
        }

        /// <summary>
        /// Handle MemberExpression accessing closure fields that contain FactQueryable instances.
        /// When a rule captures context.Facts&lt;T&gt;() in a closure, the expression tree
        /// contains MemberAccess(closure, "fieldName") where the field value is a FactQueryable.
        /// </summary>
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression ce && ce.Value != null)
            {
                try
                {
                    // Evaluate the member access to get the actual value
                    var value = Expression.Lambda(node).Compile().DynamicInvoke();

                    if (value != null)
                    {
                        var valueType = value.GetType();
                        if (valueType.IsGenericType &&
                            valueType.GetGenericTypeDefinition() == typeof(FactQueryable<>))
                        {
                            // Get the fact type from FactQueryable<T>
                            var factType = valueType.GetGenericArguments()[0];

                            // Resolve actual facts from session
                            var facts = _factResolver(factType);

                            // Return a constant expression with the actual facts
                            var queryableType = typeof(IQueryable<>).MakeGenericType(factType);
                            return Expression.Constant(facts, queryableType);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Surface the error clearly. If we can't evaluate a closure field,
                    // the FactQueryable (if any) stays in the compiled tree and later
                    // throws a confusing "cannot be enumerated directly" error.
                    // Better to fail here with context about what went wrong.
                    var inner = ex is System.Reflection.TargetInvocationException tie
                        ? tie.InnerException ?? ex : ex;
                    throw new InvalidOperationException(
                        $"Failed to evaluate closure field '{node.Member.Name}' on " +
                        $"{node.Member.DeclaringType?.Name} during fact query rewriting. " +
                        $"Check that the captured variable is accessible and not null. " +
                        $"Detail: {inner.Message}", inner);
                }
            }
            return base.VisitMember(node);
        }
    }

    #endregion
}
