namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using TestRunner.Framework;
    using RulesEngine.Linq;

    public class ValidationTests
    {
        private class Order
        {
            public string Id { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public bool IsActive { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public DateTime OrderDate { get; set; }
        }

        #region Valid Expressions

        [Test]
        public void Validator_AllowsSimplePropertyAccess()
        {
            var validator = new ExpressionValidator();
            Expression<Func<Order, bool>> expr = o => o.Total > 100;

            var errors = validator.GetErrors(expr);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_AllowsMultipleConditions()
        {
            var validator = new ExpressionValidator();
            Expression<Func<Order, bool>> expr = o => o.Total > 100 && o.IsActive;

            var errors = validator.GetErrors(expr);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_AllowsStringContains()
        {
            var validator = new ExpressionValidator();
            Expression<Func<Order, bool>> expr = o => o.CustomerName.Contains("Smith");

            var errors = validator.GetErrors(expr);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_AllowsStringStartsWith()
        {
            var validator = new ExpressionValidator();
            Expression<Func<Order, bool>> expr = o => o.CustomerName.StartsWith("A");

            var errors = validator.GetErrors(expr);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_AllowsStringEndsWith()
        {
            var validator = new ExpressionValidator();
            Expression<Func<Order, bool>> expr = o => o.CustomerName.EndsWith("son");

            var errors = validator.GetErrors(expr);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_AllowsStringToLower()
        {
            var validator = new ExpressionValidator();
            Expression<Func<Order, bool>> expr = o => o.CustomerName.ToLower() == "smith";

            var errors = validator.GetErrors(expr);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_AllowsMathAbs()
        {
            var validator = new ExpressionValidator();
            Expression<Func<Order, bool>> expr = o => Math.Abs(o.Total) > 100;

            var errors = validator.GetErrors(expr);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_AllowsDateTimeComparison()
        {
            var validator = new ExpressionValidator();
            var cutoff = DateTime.UtcNow.AddDays(-30);
            Expression<Func<Order, bool>> expr = o => o.OrderDate > cutoff;

            var errors = validator.GetErrors(expr);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_AllowsClosures()
        {
            var validator = new ExpressionValidator();
            decimal threshold = 100m;
            Expression<Func<Order, bool>> expr = o => o.Total > threshold;

            var errors = validator.GetErrors(expr);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_AllowsQueryableMethods()
        {
            var validator = new ExpressionValidator();
            var orders = new[] { new Order() }.AsQueryable();

            Expression<Func<IQueryable<Order>, bool>> expr =
                q => q.Any(o => o.Total > 100);

            var errors = validator.GetErrors(expr);
            Assert.Equal(0, errors.Count);
        }

        #endregion

        #region Invalid Expressions

        [Test]
        public void Validator_RejectsInvocationExpression()
        {
            var validator = new ExpressionValidator();

            Expression<Func<Order, bool>> inner = o => o.Total > 100;
            var param = Expression.Parameter(typeof(Order), "x");
            var invocation = Expression.Invoke(inner, param);
            var lambda = Expression.Lambda<Func<Order, bool>>(invocation, param);

            var errors = validator.GetErrors(lambda);
            Assert.True(errors.Count > 0);
            Assert.True(errors.Any(e => e.Contains("InvocationExpression")));
        }

        [Test]
        public void Validator_RejectsCustomMethods()
        {
            var validator = new ExpressionValidator();
            Expression<Func<Order, bool>> expr = o => CustomHelper(o.Total);

            var errors = validator.GetErrors(expr);
            Assert.True(errors.Count > 0);
        }

        private static bool CustomHelper(decimal value) => value > 100;

        [Test]
        public void Validator_Validate_ThrowsOnInvalidExpression()
        {
            var validator = new ExpressionValidator();
            Expression<Func<Order, bool>> expr = o => CustomHelper(o.Total);

            Assert.Throws<InvalidOperationException>(() => validator.Validate(expr));
        }

        #endregion

        #region RuleBuilder Validation

        [Test]
        public void RuleBuilder_ProducesValidExpression()
        {
            var validator = new ExpressionValidator();

            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test")
                .When(o => o.Total > 100)
                .And(o => o.IsActive)
                .Build();

            var errors = validator.GetErrors(rule.Condition);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void RuleBuilder_WithNot_ProducesValidExpression()
        {
            var validator = new ExpressionValidator();

            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test")
                .When(o => o.Total > 100)
                .Not()
                .Build();

            var errors = validator.GetErrors(rule.Condition);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void RuleBuilder_WithClosure_ProducesValidExpression()
        {
            var validator = new ExpressionValidator();
            decimal threshold = 100m;

            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test")
                .When(o => o.Total > threshold)
                .Build();

            var errors = validator.GetErrors(rule.Condition);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void RuleBuilder_CombinedWithClosures_ProducesValidExpression()
        {
            var validator = new ExpressionValidator();
            decimal minValue = 100m;
            decimal maxValue = 1000m;

            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test")
                .When(o => o.Total > minValue)
                .And(o => o.Total < maxValue)
                .And(o => o.IsActive)
                .Build();

            var errors = validator.GetErrors(rule.Condition);
            Assert.Equal(0, errors.Count);
        }

        #endregion

        #region Provider Validation Integration

        [Test]
        public void Provider_ValidatesOnQuery()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            ruleSet.Add(new Rule<Order>("R1", "Test", o => o.Total > 100));

            // Should not throw - expression is valid
            var results = ruleSet.Where(r => r.Priority > 0).ToList();
            Assert.NotNull(results);
        }

        [Test]
        public void Provider_ValidatesRuleCondition()
        {
            using var context = new RulesContext();
            var provider = context.Provider;

            Expression<Func<Order, bool>> validExpr = o => o.Total > 100;
            provider.ValidateExpression(validExpr);

            // Should throw for invalid expression
            Expression<Func<Order, bool>> invalidExpr = o => CustomHelper(o.Total);
            Assert.Throws<InvalidOperationException>(() =>
                provider.ValidateExpression(invalidExpr));
        }

        #endregion
    }
}
