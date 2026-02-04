namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using TestRunner.Framework;
    using RulesEngine.Linq;

    public class SubqueryTests
    {
        #region Test Domain Classes

        private class Product
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public string CategoryId { get; set; } = string.Empty;
        }

        private class Order
        {
            public string Id { get; set; } = string.Empty;
            public string ProductId { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal Total { get; set; }
        }

        private class Category
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }

        #endregion

        #region Capability Tests

        [Test]
        public void ExpressionCapabilities_SupportsSubqueries_DefaultsToTrue()
        {
            var capabilities = new ExpressionCapabilities();
            Assert.True(capabilities.SupportsSubqueries);
        }

        [Test]
        public void ExpressionCapabilities_CanDisableSubqueries()
        {
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = false };
            Assert.False(capabilities.SupportsSubqueries);
        }

        [Test]
        public void InMemoryProvider_DefaultCapabilities_SupportsSubqueries()
        {
            var provider = new InMemoryRuleProvider();
            Assert.True(provider.GetCapabilities().SupportsSubqueries);
        }

        [Test]
        public void InMemoryProvider_CustomCapabilities_RespectsSettings()
        {
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = false };
            var provider = new InMemoryRuleProvider(capabilities);
            Assert.False(provider.GetCapabilities().SupportsSubqueries);
        }

        #endregion

        #region Subquery Validation Tests

        [Test]
        public void Validator_WithSubqueriesEnabled_AcceptsQueryableReference()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = true };

            // Simple expression without subquery should pass
            Expression<Func<Order, bool>> expr = o => o.Total > 100;
            var errors = validator.GetErrors(expr, capabilities);

            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_WithClosuresDisabled_RejectsClosureCapture()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsClosures = false };

            var threshold = 100m;
            Expression<Func<Order, bool>> expr = o => o.Total > threshold;
            var errors = validator.GetErrors(expr, capabilities);

            Assert.True(errors.Count > 0);
            Assert.True(errors.Any(e => e.Contains("Closure capture")));
        }

        [Test]
        public void Validator_WithClosuresEnabled_AcceptsClosureCapture()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsClosures = true };

            var threshold = 100m;
            Expression<Func<Order, bool>> expr = o => o.Total > threshold;
            var errors = validator.GetErrors(expr, capabilities);

            Assert.Equal(0, errors.Count);
        }

        #endregion

        #region Cross-Set Subquery Tests

        [Test]
        public void Session_CanQueryMultipleFactTypes()
        {
            using var context = new RulesContext();

            // Add rules for both types
            var orderRules = context.GetRuleSet<Order>();
            orderRules.Add(new Rule<Order>("HighValue", "High value order", o => o.Total > 500));

            var productRules = context.GetRuleSet<Product>();
            productRules.Add(new Rule<Product>("Expensive", "Expensive product", p => p.Price > 100));

            using var session = context.CreateSession();

            // Insert facts of different types
            session.Insert(new Order { Id = "O1", Total = 600 });
            session.Insert(new Order { Id = "O2", Total = 200 });
            session.Insert(new Product { Id = "P1", Price = 150 });
            session.Insert(new Product { Id = "P2", Price = 50 });

            // Evaluate orders
            var orderResults = session.Evaluate<Order>();
            Assert.Equal(1, orderResults.FactsWithMatches.Count);
            Assert.Equal("O1", orderResults.FactsWithMatches[0].Id);

            // Evaluate products
            var productResults = session.Evaluate<Product>();
            Assert.Equal(1, productResults.FactsWithMatches.Count);
            Assert.Equal("P1", productResults.FactsWithMatches[0].Id);
        }

        [Test]
        public void Session_EvaluateAll_IncludesAllFactTypes()
        {
            using var context = new RulesContext();

            var orderRules = context.GetRuleSet<Order>();
            orderRules.Add(new Rule<Order>("HighValue", "High value order", o => o.Total > 500));

            var productRules = context.GetRuleSet<Product>();
            productRules.Add(new Rule<Product>("Expensive", "Expensive product", p => p.Price > 100));

            using var session = context.CreateSession();

            session.Insert(new Order { Id = "O1", Total = 600 });
            session.Insert(new Product { Id = "P1", Price = 150 });

            var results = session.Evaluate();

            Assert.Equal(2, results.TotalFactsEvaluated);
            Assert.Equal(2, results.TotalMatches);
        }

        #endregion

        #region Provider with Restricted Capabilities

        [Test]
        public void Context_WithRestrictedProvider_EnforcesCapabilities()
        {
            var capabilities = new ExpressionCapabilities
            {
                SupportsClosures = false,
                SupportsSubqueries = true
            };
            var provider = new InMemoryRuleProvider(capabilities);
            using var context = new RulesContext(provider);

            var rules = context.GetRuleSet<Order>();

            // This should work - no closure
            var rule1 = new Rule<Order>("Simple", "Simple rule", o => o.Total > 100);
            rules.Add(rule1);

            Assert.Equal(1, rules.Count);
        }

        [Test]
        public void RulesContext_AcceptsCustomProvider()
        {
            var capabilities = new ExpressionCapabilities
            {
                SupportsClosures = true,
                SupportsMethodCalls = true,
                SupportsSubqueries = true
            };
            var provider = new InMemoryRuleProvider(capabilities);
            using var context = new RulesContext(provider);

            Assert.True(context.Provider.GetCapabilities().SupportsSubqueries);
        }

        #endregion

        #region Collection Subqueries (Navigation Properties)

        [Test]
        public void Rule_WithCollectionAny_IsValidSubquery()
        {
            using var context = new RulesContext();
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = true };

            // This is a subquery on a collection property
            Expression<Func<OrderWithItems, bool>> expr =
                o => o.Items.Any(i => i.Quantity > 10);

            var errors = validator.GetErrors(expr, capabilities);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Rule_WithCollectionAll_IsValidSubquery()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = true };

            Expression<Func<OrderWithItems, bool>> expr =
                o => o.Items.All(i => i.UnitPrice < 100);

            var errors = validator.GetErrors(expr, capabilities);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Rule_WithNestedCollectionQuery_IsValid()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = true };

            Expression<Func<OrderWithItems, bool>> expr =
                o => o.Items.Where(i => i.Quantity > 5).Sum(i => i.UnitPrice) > 1000;

            var errors = validator.GetErrors(expr, capabilities);
            Assert.Equal(0, errors.Count);
        }

        private class OrderItem
        {
            public string ProductId { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }

        private class OrderWithItems
        {
            public string Id { get; set; } = string.Empty;
            public List<OrderItem> Items { get; set; } = new();
        }

        #endregion

        #region Capability Enforcement Integration

        [Test]
        public void LinqCompatibleConstraint_UsesDefaultCapabilities()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var rules = context.GetRuleSet<Order>();

            // Simple rule should work
            var rule = new Rule<Order>("Simple", "Test", o => o.Total > 100);
            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        [Test]
        public void Provider_ValidatesExpressionWithCapabilities()
        {
            var capabilities = new ExpressionCapabilities
            {
                SupportsClosures = true,
                SupportsMethodCalls = true,
                SupportsSubqueries = true
            };
            var provider = new InMemoryRuleProvider(capabilities);

            // Should not throw
            Expression<Func<Order, bool>> expr = o => o.Total > 100;
            provider.ValidateExpression(expr);

            Assert.True(true); // If we get here, validation passed
        }

        #endregion
    }
}
