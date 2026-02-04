namespace RulesEngine.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    #region Schema Builder

    public class SchemaBuilder<T> : ISchemaBuilder<T> where T : class
    {
        private readonly RulesContext _context;
        private readonly List<IRuleConstraint<T>> _constraints = new();
        private ValidationMode _validationMode = ValidationMode.OnAdd;

        internal SchemaBuilder(RulesContext context)
        {
            _context = context;
        }

        public ISchemaBuilder<T> RequireLinqCompatible()
        {
            _constraints.Add(new LinqCompatibleConstraint<T>());
            return this;
        }

        public ISchemaBuilder<T> RequirePriorityRange(int min, int max)
        {
            _constraints.Add(new PriorityRangeConstraint<T>(min, max));
            return this;
        }

        public ISchemaBuilder<T> MaxExpressionDepth(int maxDepth)
        {
            _constraints.Add(new ExpressionDepthConstraint<T>(maxDepth));
            return this;
        }

        public ISchemaBuilder<T> DisallowClosures()
        {
            _constraints.Add(new DisallowClosuresConstraint<T>());
            return this;
        }

        public ISchemaBuilder<T> AllowMethods(Type declaringType, params string[] methodNames)
        {
            _constraints.Add(new MethodWhitelistConstraint<T>(declaringType, methodNames));
            return this;
        }

        public ISchemaBuilder<T> WithConstraint(IRuleConstraint<T> constraint)
        {
            _constraints.Add(constraint);
            return this;
        }

        public ISchemaBuilder<T> ValidateOn(ValidationMode mode)
        {
            _validationMode = mode;
            return this;
        }

        public void Build()
        {
            var constrainedRuleSet = new ConstrainedRuleSet<T>(_context, _constraints, _validationMode);
            _context.RegisterRuleSet(constrainedRuleSet);
        }
    }

    #endregion

    #region Constrained RuleSet

    public class ConstrainedRuleSet<T> : RuleSet<T>, IConstrainedRuleSet<T> where T : class
    {
        private readonly List<IRuleConstraint<T>> _constraints;
        private readonly ValidationMode _validationMode;

        public ConstrainedRuleSet(RulesContext context, List<IRuleConstraint<T>> constraints, ValidationMode validationMode)
            : base(context)
        {
            _constraints = constraints;
            _validationMode = validationMode;
        }

        public override bool HasConstraints => _constraints.Count > 0;
        public ValidationMode ValidationMode => _validationMode;

        public override IReadOnlyList<IRuleConstraint<T>> GetConstraints() => _constraints.AsReadOnly();

        public override bool HasConstraint(string constraintName)
        {
            return _constraints.Any(c => c.Name == constraintName);
        }

        public new void Add(IRule<T> rule)
        {
            if (_validationMode == ValidationMode.OnAdd || _validationMode == ValidationMode.Both)
            {
                var violations = ValidateRule(rule);
                if (violations.Count > 0)
                    throw new ConstraintViolationException(rule.Id, violations);
            }
            base.Add(rule);
        }

        public override bool TryAdd(IRule<T> rule, out IReadOnlyList<ConstraintViolation> violations)
        {
            if (_validationMode == ValidationMode.OnAdd || _validationMode == ValidationMode.Both)
            {
                var violationList = ValidateRule(rule);
                violations = violationList;
                if (violations.Count > 0)
                    return false;
            }
            else
            {
                violations = Array.Empty<ConstraintViolation>();
            }

            base.Add(rule);
            return true;
        }

        internal List<ConstraintViolation> ValidateRule(IRule<T> rule)
        {
            var violations = new List<ConstraintViolation>();
            foreach (var constraint in _constraints)
            {
                var result = constraint.Validate(rule);
                if (!result.IsValid)
                {
                    foreach (var error in result.Errors)
                    {
                        violations.Add(new ConstraintViolation
                        {
                            ConstraintName = constraint.Name,
                            Message = error
                        });
                    }
                }
            }
            return violations;
        }

        internal void ValidateForEvaluation()
        {
            if (_validationMode != ValidationMode.OnEvaluate && _validationMode != ValidationMode.Both)
                return;

            foreach (var rule in this)
            {
                var violations = ValidateRule(rule);
                if (violations.Count > 0)
                    throw new ConstraintViolationException(rule.Id, violations);
            }
        }
    }

    #endregion

    #region Built-in Constraints

    public class LinqCompatibleConstraint<T> : IRuleConstraint<T> where T : class
    {
        private readonly ExpressionValidator _validator = new();

        public string Name => "LinqCompatible";

        public ConstraintResult Validate(IRule<T> rule)
        {
            var errors = _validator.GetErrors(rule.Condition);
            if (errors.Count == 0)
                return ConstraintResult.Success();

            return ConstraintResult.Failure(errors);
        }
    }

    public class PriorityRangeConstraint<T> : IRuleConstraint<T> where T : class
    {
        private readonly int _min;
        private readonly int _max;

        public PriorityRangeConstraint(int min, int max)
        {
            _min = min;
            _max = max;
        }

        public string Name => "PriorityRange";

        public ConstraintResult Validate(IRule<T> rule)
        {
            if (rule.Priority < _min || rule.Priority > _max)
                return ConstraintResult.Failure($"Priority {rule.Priority} is outside allowed range [{_min}, {_max}]");

            return ConstraintResult.Success();
        }
    }

    public class ExpressionDepthConstraint<T> : IRuleConstraint<T> where T : class
    {
        private readonly int _maxDepth;

        public ExpressionDepthConstraint(int maxDepth)
        {
            _maxDepth = maxDepth;
        }

        public string Name => "ExpressionDepth";

        public ConstraintResult Validate(IRule<T> rule)
        {
            var visitor = new DepthCalculatingVisitor();
            visitor.Visit(rule.Condition);

            if (visitor.MaxDepth > _maxDepth)
                return ConstraintResult.Failure($"Expression depth {visitor.MaxDepth} exceeds maximum allowed depth of {_maxDepth}");

            return ConstraintResult.Success();
        }

        private class DepthCalculatingVisitor : ExpressionVisitor
        {
            private int _currentDepth = 0;
            public int MaxDepth { get; private set; } = 0;

            public override Expression? Visit(Expression? node)
            {
                if (node == null) return null;

                _currentDepth++;
                MaxDepth = Math.Max(MaxDepth, _currentDepth);

                var result = base.Visit(node);

                _currentDepth--;
                return result;
            }
        }
    }

    public class DisallowClosuresConstraint<T> : IRuleConstraint<T> where T : class
    {
        public string Name => "DisallowClosures";

        public ConstraintResult Validate(IRule<T> rule)
        {
            var visitor = new ClosureDetectingVisitor();
            visitor.Visit(rule.Condition);

            if (visitor.HasClosure)
                return ConstraintResult.Failure("Expression contains closure capture which is not allowed");

            return ConstraintResult.Success();
        }

        private class ClosureDetectingVisitor : ExpressionVisitor
        {
            private readonly HashSet<ParameterExpression> _lambdaParameters = new();
            public bool HasClosure { get; private set; }

            protected override Expression VisitLambda<TDelegate>(Expression<TDelegate> node)
            {
                foreach (var param in node.Parameters)
                    _lambdaParameters.Add(param);

                return base.VisitLambda(node);
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression is ConstantExpression ce && !IsPrimitiveType(ce.Type))
                {
                    HasClosure = true;
                }
                return base.VisitMember(node);
            }

            private static bool IsPrimitiveType(Type type)
            {
                return type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
                    || type == typeof(DateTime) || type == typeof(Guid);
            }
        }
    }

    public class MethodWhitelistConstraint<T> : IRuleConstraint<T> where T : class
    {
        private readonly Type _declaringType;
        private readonly HashSet<string> _allowedMethods;

        public MethodWhitelistConstraint(Type declaringType, string[] methodNames)
        {
            _declaringType = declaringType;
            _allowedMethods = new HashSet<string>(methodNames);
        }

        public string Name => "MethodWhitelist";

        public ConstraintResult Validate(IRule<T> rule)
        {
            var visitor = new MethodCallVisitor(_declaringType, _allowedMethods);
            visitor.Visit(rule.Condition);

            if (visitor.Errors.Count > 0)
                return ConstraintResult.Failure(visitor.Errors);

            return ConstraintResult.Success();
        }

        private class MethodCallVisitor : ExpressionVisitor
        {
            private readonly Type _declaringType;
            private readonly HashSet<string> _allowedMethods;
            public List<string> Errors { get; } = new();

            public MethodCallVisitor(Type declaringType, HashSet<string> allowedMethods)
            {
                _declaringType = declaringType;
                _allowedMethods = allowedMethods;
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.DeclaringType == _declaringType && !_allowedMethods.Contains(node.Method.Name))
                {
                    Errors.Add($"Method '{node.Method.Name}' on type '{_declaringType.Name}' is not in the allowed list");
                }
                return base.VisitMethodCall(node);
            }
        }
    }

    #endregion
}
