namespace RulesEngine.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    /// <summary>
    /// Standard rule implementation with expression-based condition.
    /// </summary>
    public class Rule<T> : IRule<T> where T : class
    {
        private readonly Func<T, bool> _compiledCondition;
        private readonly List<Action<T>> _actions = new();

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Priority { get; }
        public Expression<Func<T, bool>> Condition { get; }

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
            _compiledCondition = condition.Compile();
        }

        public Rule<T> WithAction(Action<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _actions.Add(action);
            return this;
        }

        public bool Evaluate(T fact)
        {
            if (fact == null) return false;
            try
            {
                return _compiledCondition(fact);
            }
            catch
            {
                return false;
            }
        }

        public RuleResult Execute(T fact)
        {
            bool matched;
            try
            {
                matched = _compiledCondition(fact);
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
}
