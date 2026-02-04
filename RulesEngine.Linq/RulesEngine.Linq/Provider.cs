namespace RulesEngine.Linq
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    #region In-Memory Provider

    /// <summary>
    /// In-memory provider that validates and executes expressions locally.
    /// </summary>
    public class InMemoryRuleProvider : IRuleProvider
    {
        private readonly ConcurrentDictionary<Expression, Delegate> _compiledCache = new();
        private readonly ExpressionValidator _validator = new();

        public ExpressionCapabilities GetCapabilities() => new()
        {
            SupportsClosures = true,
            SupportsMethodCalls = true,
            SupportsSubqueries = true,
            SupportedMethods = ExpressionValidator.TranslatableMethods
        };

        public void ValidateExpression(Expression expression)
        {
            _validator.Validate(expression);
        }

        public Func<T, bool> CompileCondition<T>(Expression<Func<T, bool>> condition)
        {
            return (Func<T, bool>)_compiledCache.GetOrAdd(condition, expr =>
            {
                ValidateExpression(expr);
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
}
