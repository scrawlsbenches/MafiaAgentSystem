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

        private class OrderItem
        {
            public string ProductId { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }

        private class OrderWithItems
        {
            public string Id { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public List<OrderItem> Items { get; set; } = new();
        }

        #endregion

        #region ExpressionCapabilities Default Values

        [Test]
        public void ExpressionCapabilities_SupportsSubqueries_DefaultsToTrue()
        {
            var capabilities = new ExpressionCapabilities();
            Assert.True(capabilities.SupportsSubqueries);
        }

        [Test]
        public void ExpressionCapabilities_SupportsClosures_DefaultsToTrue()
        {
            var capabilities = new ExpressionCapabilities();
            Assert.True(capabilities.SupportsClosures);
        }

        [Test]
        public void ExpressionCapabilities_SupportsMethodCalls_DefaultsToTrue()
        {
            var capabilities = new ExpressionCapabilities();
            Assert.True(capabilities.SupportsMethodCalls);
        }

        #endregion

        #region InMemoryRuleProvider Capability Configuration

        [Test]
        public void InMemoryProvider_DefaultConstructor_HasAllCapabilitiesEnabled()
        {
            var provider = new InMemoryRuleProvider();
            var caps = provider.GetCapabilities();

            Assert.True(caps.SupportsSubqueries);
            Assert.True(caps.SupportsClosures);
            Assert.True(caps.SupportsMethodCalls);
        }

        [Test]
        public void InMemoryProvider_CustomCapabilities_ReturnsExactCapabilities()
        {
            var capabilities = new ExpressionCapabilities
            {
                SupportsSubqueries = false,
                SupportsClosures = false,
                SupportsMethodCalls = true
            };
            var provider = new InMemoryRuleProvider(capabilities);
            var caps = provider.GetCapabilities();

            Assert.False(caps.SupportsSubqueries);
            Assert.False(caps.SupportsClosures);
            Assert.True(caps.SupportsMethodCalls);
        }

        #endregion

        #region Closure Capability Enforcement

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

        [Test]
        public void Validator_WithClosuresDisabled_AcceptsLiteralValues()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsClosures = false };

            // Literal 100m is not a closure - it's a constant
            Expression<Func<Order, bool>> expr = o => o.Total > 100m;
            var errors = validator.GetErrors(expr, capabilities);

            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Provider_WithClosuresDisabled_ThrowsOnClosureExpression()
        {
            var capabilities = new ExpressionCapabilities { SupportsClosures = false };
            var provider = new InMemoryRuleProvider(capabilities);

            var threshold = 100m;
            Expression<Func<Order, bool>> expr = o => o.Total > threshold;

            Assert.Throws<NotImplementedException>(() => provider.ValidateExpression(expr));
        }

        #endregion

        #region Collection Navigation Subqueries

        [Test]
        public void Validator_CollectionAny_AcceptedWithSubqueriesEnabled()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = true };

            Expression<Func<OrderWithItems, bool>> expr =
                o => o.Items.Any(i => i.Quantity > 10);

            var errors = validator.GetErrors(expr, capabilities);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_CollectionAll_AcceptedWithSubqueriesEnabled()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = true };

            Expression<Func<OrderWithItems, bool>> expr =
                o => o.Items.All(i => i.UnitPrice < 100);

            var errors = validator.GetErrors(expr, capabilities);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_CollectionWhereSum_AcceptedWithSubqueriesEnabled()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = true };

            Expression<Func<OrderWithItems, bool>> expr =
                o => o.Items.Where(i => i.Quantity > 5).Sum(i => i.UnitPrice) > 1000;

            var errors = validator.GetErrors(expr, capabilities);
            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_CollectionCount_AcceptedWithSubqueriesEnabled()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = true };

            Expression<Func<OrderWithItems, bool>> expr =
                o => o.Items.Count(i => i.Quantity > 0) >= 3;

            var errors = validator.GetErrors(expr, capabilities);
            Assert.Equal(0, errors.Count);
        }

        #endregion

        #region Cross-Set Subquery Detection

        [Test]
        public void Validator_QueryableConstant_DetectedAsSubquery()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = false };

            // Create a queryable that would be captured as a constant
            var products = new List<Product>().AsQueryable();

            // This expression references an external IQueryable - a true subquery
            Expression<Func<Order, bool>> expr =
                o => products.Any(p => p.Id == o.ProductId);

            var errors = validator.GetErrors(expr, capabilities);

            Assert.True(errors.Count > 0);
            Assert.True(errors.Any(e => e.Contains("Subquery") || e.Contains("subquery")));
        }

        [Test]
        public void Validator_QueryableConstant_AcceptedWhenSubqueriesEnabled()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsSubqueries = true };

            var products = new List<Product>().AsQueryable();

            Expression<Func<Order, bool>> expr =
                o => products.Any(p => p.Id == o.ProductId);

            var errors = validator.GetErrors(expr, capabilities);

            Assert.Equal(0, errors.Count);
        }

        #endregion

        #region Session Multi-Type Evaluation

        [Test]
        public void Session_CanInsertAndEvaluateMultipleFactTypes()
        {
            using var context = new RulesContext();

            var orderRules = context.GetRuleSet<Order>();
            orderRules.Add(new Rule<Order>("HighValue", "High value order", o => o.Total > 500));

            var productRules = context.GetRuleSet<Product>();
            productRules.Add(new Rule<Product>("Expensive", "Expensive product", p => p.Price > 100));

            using var session = context.CreateSession();

            session.Insert(new Order { Id = "O1", Total = 600 });
            session.Insert(new Order { Id = "O2", Total = 200 });
            session.Insert(new Product { Id = "P1", Price = 150 });
            session.Insert(new Product { Id = "P2", Price = 50 });

            var orderResults = session.Evaluate<Order>();
            Assert.Equal(1, orderResults.FactsWithMatches.Count);
            Assert.Equal("O1", orderResults.FactsWithMatches[0].Id);
            Assert.Equal(1, orderResults.FactsWithoutMatches.Count);
            Assert.Equal("O2", orderResults.FactsWithoutMatches[0].Id);

            var productResults = session.Evaluate<Product>();
            Assert.Equal(1, productResults.FactsWithMatches.Count);
            Assert.Equal("P1", productResults.FactsWithMatches[0].Id);
        }

        [Test]
        public void Session_EvaluateAll_AggregatesAllFactTypes()
        {
            using var context = new RulesContext();

            context.GetRuleSet<Order>()
                .Add(new Rule<Order>("HighValue", "High value", o => o.Total > 500));

            context.GetRuleSet<Product>()
                .Add(new Rule<Product>("Expensive", "Expensive", p => p.Price > 100));

            using var session = context.CreateSession();

            session.Insert(new Order { Id = "O1", Total = 600 });
            session.Insert(new Product { Id = "P1", Price = 150 });

            var results = session.Evaluate();

            Assert.Equal(2, results.TotalFactsEvaluated);
            Assert.Equal(2, results.TotalMatches);
            Assert.False(results.HasErrors);
        }

        #endregion

        #region Provider Capability Enforcement Through Context

        [Test]
        public void Context_WithRestrictedClosures_RejectsClosureRule()
        {
            var capabilities = new ExpressionCapabilities { SupportsClosures = false };
            var provider = new InMemoryRuleProvider(capabilities);
            using var context = new RulesContext(provider);

            var threshold = 500m;

            // Rule with closure should fail validation when used
            var rule = new Rule<Order>("WithClosure", "Has closure", o => o.Total > threshold);

            // The rule itself compiles, but provider validation should catch it
            // when the expression is validated through the provider
            Assert.Throws<NotImplementedException>(() =>
                provider.ValidateExpression(rule.Condition));
        }

        [Test]
        public void Context_WithRestrictedClosures_AcceptsLiteralRule()
        {
            var capabilities = new ExpressionCapabilities { SupportsClosures = false };
            var provider = new InMemoryRuleProvider(capabilities);
            using var context = new RulesContext(provider);

            var rules = context.GetRuleSet<Order>();

            // Rule without closure should work
            var rule = new Rule<Order>("NoClosures", "No closures", o => o.Total > 500);
            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        #endregion

        #region Method Call Capability Enforcement

        [Test]
        public void Validator_StringContains_AcceptedWithMethodCallsEnabled()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsMethodCalls = true };

            Expression<Func<Product, bool>> expr = p => p.Name.Contains("test");
            var errors = validator.GetErrors(expr, capabilities);

            Assert.Equal(0, errors.Count);
        }

        [Test]
        public void Validator_CustomMethod_RejectedEvenWithMethodCallsEnabled()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities { SupportsMethodCalls = true };

            Expression<Func<Order, bool>> expr = o => IsHighValue(o.Total);
            var errors = validator.GetErrors(expr, capabilities);

            Assert.True(errors.Count > 0);
            Assert.True(errors.Any(e => e.Contains("not translatable")));
        }

        private static bool IsHighValue(decimal total) => total > 1000;

        #endregion

        #region Combined Capability Scenarios

        [Test]
        public void Validator_AllCapabilitiesDisabled_RejectsComplexExpression()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities
            {
                SupportsClosures = false,
                SupportsSubqueries = false,
                SupportsMethodCalls = true // Keep method calls to test closures specifically
            };

            var minPrice = 100m;
            Expression<Func<Product, bool>> expr =
                p => p.Price > minPrice && p.Name.Contains("Premium");

            var errors = validator.GetErrors(expr, capabilities);

            // Should have closure error
            Assert.True(errors.Count > 0);
            Assert.True(errors.Any(e => e.Contains("Closure")));
        }

        [Test]
        public void Validator_AllCapabilitiesEnabled_AcceptsComplexExpression()
        {
            var validator = new ExpressionValidator();
            var capabilities = new ExpressionCapabilities
            {
                SupportsClosures = true,
                SupportsSubqueries = true,
                SupportsMethodCalls = true
            };

            var minPrice = 100m;
            Expression<Func<Product, bool>> expr =
                p => p.Price > minPrice && p.Name.Contains("Premium");

            var errors = validator.GetErrors(expr, capabilities);

            Assert.Equal(0, errors.Count);
        }

        #endregion

        #region Rule Evaluation with Collection Subqueries

        [Test]
        public void Rule_WithCollectionAny_EvaluatesCorrectly()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<OrderWithItems>();

            rules.Add(new Rule<OrderWithItems>("HasBulkItem", "Has bulk item",
                o => o.Items.Any(i => i.Quantity > 100)));

            using var session = context.CreateSession();

            session.Insert(new OrderWithItems
            {
                Id = "O1",
                Items = new List<OrderItem>
                {
                    new() { ProductId = "P1", Quantity = 150 },
                    new() { ProductId = "P2", Quantity = 10 }
                }
            });

            session.Insert(new OrderWithItems
            {
                Id = "O2",
                Items = new List<OrderItem>
                {
                    new() { ProductId = "P3", Quantity = 5 }
                }
            });

            var results = session.Evaluate<OrderWithItems>();

            Assert.Equal(1, results.FactsWithMatches.Count);
            Assert.Equal("O1", results.FactsWithMatches[0].Id);
            Assert.Equal(1, results.FactsWithoutMatches.Count);
            Assert.Equal("O2", results.FactsWithoutMatches[0].Id);
        }

        [Test]
        public void Rule_WithCollectionSum_EvaluatesCorrectly()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<OrderWithItems>();

            rules.Add(new Rule<OrderWithItems>("HighValueOrder", "Total items value > 1000",
                o => o.Items.Sum(i => i.Quantity * i.UnitPrice) > 1000));

            using var session = context.CreateSession();

            session.Insert(new OrderWithItems
            {
                Id = "O1",
                Items = new List<OrderItem>
                {
                    new() { Quantity = 10, UnitPrice = 150 } // 1500 total
                }
            });

            session.Insert(new OrderWithItems
            {
                Id = "O2",
                Items = new List<OrderItem>
                {
                    new() { Quantity = 5, UnitPrice = 50 } // 250 total
                }
            });

            var results = session.Evaluate<OrderWithItems>();

            Assert.Equal(1, results.FactsWithMatches.Count);
            Assert.Equal("O1", results.FactsWithMatches[0].Id);
        }

        #endregion
    }
}
