namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using TestRunner.Framework;
    using RulesEngine.Linq;

    public class SchemaConstraintTests
    {
        private class Order
        {
            public string Id { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public bool IsActive { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        #region Schema Configuration API

        [Test]
        public void Context_ConfigureRuleSet_ReturnsSchemaBuilder()
        {
            using var context = new RulesContext();

            var builder = context.ConfigureRuleSet<Order>();

            Assert.NotNull(builder);
        }

        [Test]
        public void SchemaBuilder_RequireLinqCompatible_ReturnsBuilder()
        {
            using var context = new RulesContext();

            var builder = context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible();

            Assert.NotNull(builder);
        }

        [Test]
        public void SchemaBuilder_FluentConfiguration()
        {
            using var context = new RulesContext();

            var builder = context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .RequirePriorityRange(0, 100)
                .MaxExpressionDepth(5);

            Assert.NotNull(builder);
        }

        [Test]
        public void SchemaBuilder_Build_CreatesConfiguredRuleSet()
        {
            using var context = new RulesContext();

            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            Assert.NotNull(ruleSet);
            Assert.True(ruleSet.HasConstraints);
        }

        #endregion

        #region LINQ Compatibility Constraint

        [Test]
        public void LinqCompatible_AcceptsSimplePropertyAccess()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100);

            ruleSet.Add(rule);

            Assert.Equal(1, ruleSet.Count);
        }

        [Test]
        public void LinqCompatible_RejectsInvocationExpression()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var ruleSet = context.GetRuleSet<Order>();

            Expression<Func<Order, bool>> inner = o => o.Total > 100;
            var param = Expression.Parameter(typeof(Order), "x");
            var invocation = Expression.Invoke(inner, param);
            var condition = Expression.Lambda<Func<Order, bool>>(invocation, param);

            var rule = new Rule<Order>("R1", "Test", condition);

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        [Test]
        public void LinqCompatible_RejectsCustomMethods()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => CustomHelper(o.Total));

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        private static bool CustomHelper(decimal value) => value > 100;

        [Test]
        public void LinqCompatible_AcceptsAllowedStringMethods()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Status.Contains("Active"));

            ruleSet.Add(rule);

            Assert.Equal(1, ruleSet.Count);
        }

        #endregion

        #region Priority Range Constraint

        [Test]
        public void PriorityRange_AcceptsValidPriority()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequirePriorityRange(0, 100)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100, priority: 50);

            ruleSet.Add(rule);

            Assert.Equal(1, ruleSet.Count);
        }

        [Test]
        public void PriorityRange_RejectsPriorityBelowMin()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequirePriorityRange(0, 100)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100, priority: -1);

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        [Test]
        public void PriorityRange_RejectsPriorityAboveMax()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequirePriorityRange(0, 100)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100, priority: 101);

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        #endregion

        #region Expression Depth Constraint

        [Test]
        public void MaxExpressionDepth_AcceptsShallowExpression()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .MaxExpressionDepth(5)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100 && o.IsActive);

            ruleSet.Add(rule);

            Assert.Equal(1, ruleSet.Count);
        }

        [Test]
        public void MaxExpressionDepth_RejectsDeeplyNestedExpression()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .MaxExpressionDepth(2)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();

            // Deeply nested: ((((o.Total > 100) && o.IsActive) || o.Status == "X") && o.Id != "")
            var rule = new Rule<Order>("R1", "Test",
                o => (((o.Total > 100) && o.IsActive) || o.Status == "X") && o.Id != "");

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        #endregion

        #region Closure Constraint

        [Test]
        public void DisallowClosures_AcceptsConstantValues()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .DisallowClosures()
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100);

            ruleSet.Add(rule);

            Assert.Equal(1, ruleSet.Count);
        }

        [Test]
        public void DisallowClosures_RejectsClosureCapture()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .DisallowClosures()
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            decimal threshold = 100m;
            var rule = new Rule<Order>("R1", "Test", o => o.Total > threshold);

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        [Test]
        public void AllowClosures_AcceptsClosureCapture()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            decimal threshold = 100m;
            var rule = new Rule<Order>("R1", "Test", o => o.Total > threshold);

            ruleSet.Add(rule);

            Assert.Equal(1, ruleSet.Count);
        }

        #endregion

        #region Method Whitelist Constraint

        [Test]
        public void AllowMethods_RestrictsToSpecifiedMethods()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .AllowMethods(typeof(string), "Contains", "StartsWith")
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Status.Contains("Active"));

            ruleSet.Add(rule);

            Assert.Equal(1, ruleSet.Count);
        }

        [Test]
        public void AllowMethods_RejectsNonWhitelistedMethods()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .AllowMethods(typeof(string), "Contains")
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Status.ToUpper() == "ACTIVE");

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        #endregion

        #region Custom Constraint

        [Test]
        public void WithConstraint_AcceptsCustomConstraint()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .WithConstraint(new IdPrefixConstraint<Order>("R"))
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100);

            ruleSet.Add(rule);

            Assert.Equal(1, ruleSet.Count);
        }

        [Test]
        public void WithConstraint_CustomConstraintCanReject()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .WithConstraint(new IdPrefixConstraint<Order>("R"))
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("X1", "Test", o => o.Total > 100);

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        private class IdPrefixConstraint<T> : IRuleConstraint<T> where T : class
        {
            private readonly string _requiredPrefix;

            public IdPrefixConstraint(string requiredPrefix)
            {
                _requiredPrefix = requiredPrefix;
            }

            public string Name => "IdPrefixConstraint";

            public ConstraintResult Validate(IRule<T> rule)
            {
                if (rule.Id.StartsWith(_requiredPrefix))
                    return ConstraintResult.Success();

                return ConstraintResult.Failure($"Rule ID must start with '{_requiredPrefix}'");
            }
        }

        #endregion

        #region Validation Mode

        [Test]
        public void ValidationMode_OnAdd_ValidatesAtAddTime()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequirePriorityRange(0, 100)
                .ValidateOn(ValidationMode.OnAdd)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100, priority: 101);

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        [Test]
        public void ValidationMode_OnEvaluate_AllowsAddButFailsEvaluate()
        {
            // OnEvaluate mode defers constraint validation to evaluation time.
            // Note: Provider-level expression validation (translatability) always
            // runs at Add() time regardless of ValidationMode — it's about correctness,
            // not policy. Constraint validation (priority ranges, expression depth, etc.)
            // is what ValidationMode controls.
            //
            // Here we use RequirePriorityRange instead of RequireLinqCompatible,
            // because priority is a policy constraint that can legitimately be deferred.
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequirePriorityRange(0, 100)
                .ValidateOn(ValidationMode.OnEvaluate)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            // Priority 101 violates the range — but OnEvaluate defers the check
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100, priority: 101);

            ruleSet.Add(rule);
            Assert.Equal(1, ruleSet.Count);

            using var session = context.CreateSession();
            session.Insert(new Order { Total = 150 });

            Assert.Throws<ConstraintViolationException>(() => session.Evaluate());
        }

        [Test]
        public void ValidationMode_Both_ValidatesAtBothTimes()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .ValidateOn(ValidationMode.Both)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => CustomHelper(o.Total));

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        #endregion

        #region Multiple Constraints

        [Test]
        public void MultipleConstraints_AllMustPass()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .RequirePriorityRange(0, 100)
                .MaxExpressionDepth(10)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100, priority: 50);

            ruleSet.Add(rule);

            Assert.Equal(1, ruleSet.Count);
        }

        [Test]
        public void MultipleConstraints_FailsIfAnyFails()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .RequirePriorityRange(0, 100)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100, priority: 150);

            Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));
        }

        #endregion

        #region Constraint Result Details

        [Test]
        public void ConstraintViolationException_ContainsDetails()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequirePriorityRange(0, 100)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100, priority: 150);

            var ex = Assert.Throws<ConstraintViolationException>(() => ruleSet.Add(rule));

            Assert.NotNull(ex.RuleId);
            Assert.Equal("R1", ex.RuleId);
            Assert.NotNull(ex.ConstraintName);
            Assert.True(ex.Violations.Count > 0);
        }

        [Test]
        public void TryAdd_ReturnsFalseWithViolations()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequirePriorityRange(0, 100)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100, priority: 150);

            var result = ruleSet.TryAdd(rule, out var violations);

            Assert.False(result);
            Assert.True(violations.Count > 0);
        }

        [Test]
        public void TryAdd_ReturnsTrueWhenValid()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequirePriorityRange(0, 100)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("R1", "Test", o => o.Total > 100, priority: 50);

            var result = ruleSet.TryAdd(rule, out var violations);

            Assert.True(result);
            Assert.Equal(0, violations.Count);
        }

        #endregion

        #region Schema Inspection

        [Test]
        public void RuleSet_GetConstraints_ReturnsConfiguredConstraints()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .RequirePriorityRange(0, 100)
                .Build();

            var ruleSet = context.GetRuleSet<Order>();
            var constraints = ruleSet.GetConstraints();

            Assert.Equal(2, constraints.Count);
        }

        [Test]
        public void RuleSet_HasConstraint_ChecksForSpecificConstraint()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var ruleSet = context.GetRuleSet<Order>();

            Assert.True(ruleSet.HasConstraint("LinqCompatible"));
            Assert.False(ruleSet.HasConstraint("PriorityRange"));
        }

        #endregion
    }
}
