namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TestRunner.Framework;
    using RulesEngine.Linq;

    public class NavigationPropertyTests
    {
        #region Fact Types with Navigation Properties

        private class Customer
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool IsVip { get; set; }
            public decimal CreditLimit { get; set; }
        }

        private class OrderItem
        {
            public string ProductId { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }

        private class Order
        {
            public string Id { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public string Status { get; set; } = string.Empty;
            public Customer Customer { get; set; } = new();
            public List<OrderItem> Items { get; set; } = new();
        }

        #endregion

        #region Simple Navigation Property Access

        [Test]
        public void Rule_CanAccessNavigationProperty()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<Order>();

            var rule = new Rule<Order>("VipOrder", "Order from VIP",
                o => o.Customer.IsVip);

            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        [Test]
        public void Rule_CanAccessNestedNavigationProperty()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<Order>();

            var rule = new Rule<Order>("HighCreditOrder", "Order from high credit customer",
                o => o.Customer.CreditLimit > 10000);

            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        [Test]
        public void Rule_CanCombineNavigationWithDirectProperty()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<Order>();

            var rule = new Rule<Order>("VipLargeOrder", "Large order from VIP",
                o => o.Customer.IsVip && o.Total > 1000);

            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        #endregion

        #region Collection Navigation Properties

        [Test]
        public void Rule_CanUseAnyOnCollection()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<Order>();

            var rule = new Rule<Order>("HasBulkItem", "Has item with quantity over 100",
                o => o.Items.Any(i => i.Quantity > 100));

            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        [Test]
        public void Rule_CanUseAllOnCollection()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<Order>();

            var rule = new Rule<Order>("AllItemsSmall", "All items under 10 quantity",
                o => o.Items.All(i => i.Quantity < 10));

            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        [Test]
        public void Rule_CanUseCountOnCollection()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<Order>();

            var rule = new Rule<Order>("ManyItems", "Has more than 5 items",
                o => o.Items.Count > 5);

            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        [Test]
        public void Rule_CanUseSumOnCollection()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<Order>();

            var rule = new Rule<Order>("HighQuantityOrder", "Total quantity over 1000",
                o => o.Items.Sum(i => i.Quantity) > 1000);

            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        #endregion

        #region Evaluation with Navigation Properties

        [Test]
        public void Evaluate_NavigationProperty_MatchesCorrectFacts()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<Order>();

            rules.Add(new Rule<Order>("VipOrder", "VIP Order", o => o.Customer.IsVip));

            using var session = context.CreateSession();
            session.Insert(new Order
            {
                Id = "O1",
                Customer = new Customer { Id = "C1", IsVip = true }
            });
            session.Insert(new Order
            {
                Id = "O2",
                Customer = new Customer { Id = "C2", IsVip = false }
            });

            var results = session.Evaluate<Order>();

            Assert.Equal(1, results.FactsWithMatches.Count);
            Assert.Equal("O1", results.FactsWithMatches[0].Id);
        }

        [Test]
        public void Evaluate_CollectionNavigation_MatchesCorrectFacts()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<Order>();

            rules.Add(new Rule<Order>("BulkOrder", "Has bulk item",
                o => o.Items.Any(i => i.Quantity > 100)));

            using var session = context.CreateSession();
            session.Insert(new Order
            {
                Id = "O1",
                Items = new List<OrderItem>
                {
                    new() { ProductId = "P1", Quantity = 150 }
                }
            });
            session.Insert(new Order
            {
                Id = "O2",
                Items = new List<OrderItem>
                {
                    new() { ProductId = "P2", Quantity = 5 }
                }
            });

            var results = session.Evaluate<Order>();

            Assert.Equal(1, results.FactsWithMatches.Count);
            Assert.Equal("O1", results.FactsWithMatches[0].Id);
        }

        [Test]
        public void Evaluate_ComplexNavigationExpression_MatchesCorrectFacts()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<Order>();

            rules.Add(new Rule<Order>("VipBulkOrder", "VIP with bulk item",
                o => o.Customer.IsVip && o.Items.Any(i => i.Quantity > 100)));

            using var session = context.CreateSession();

            session.Insert(new Order
            {
                Id = "O1",
                Customer = new Customer { IsVip = true },
                Items = new List<OrderItem> { new() { Quantity = 150 } }
            });

            session.Insert(new Order
            {
                Id = "O2",
                Customer = new Customer { IsVip = true },
                Items = new List<OrderItem> { new() { Quantity = 5 } }
            });

            session.Insert(new Order
            {
                Id = "O3",
                Customer = new Customer { IsVip = false },
                Items = new List<OrderItem> { new() { Quantity = 150 } }
            });

            var results = session.Evaluate<Order>();

            Assert.Equal(1, results.FactsWithMatches.Count);
            Assert.Equal("O1", results.FactsWithMatches[0].Id);
        }

        #endregion

        #region Constraint Validation on Navigation Expressions

        [Test]
        public void LinqCompatible_AcceptsNavigationPropertyAccess()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var rules = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("VipOrder", "VIP", o => o.Customer.IsVip);

            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        [Test]
        public void LinqCompatible_AcceptsCollectionLinqMethods()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var rules = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("BulkOrder", "Bulk",
                o => o.Items.Any(i => i.Quantity > 100));

            rules.Add(rule);

            Assert.Equal(1, rules.Count);
        }

        [Test]
        public void LinqCompatible_RejectsCustomMethodInNestedExpression()
        {
            using var context = new RulesContext();
            context.ConfigureRuleSet<Order>()
                .RequireLinqCompatible()
                .Build();

            var rules = context.GetRuleSet<Order>();
            var rule = new Rule<Order>("BadRule", "Bad",
                o => o.Items.Any(i => CustomCheck(i.Quantity)));

            Assert.Throws<ConstraintViolationException>(() => rules.Add(rule));
        }

        private static bool CustomCheck(int qty) => qty > 100;

        #endregion
    }
}
