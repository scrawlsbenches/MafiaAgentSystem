namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Linq;
    using TestRunner.Framework;
    using RulesEngine.Linq;

    public class RulesContextTests
    {
        #region Test Entities

        private class Order
        {
            public string Id { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public bool IsActive { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public DateTime OrderDate { get; set; }
        }

        private class Customer
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool IsVip { get; set; }
        }

        #endregion

        #region Context Creation

        [Test]
        public void RulesContext_CanBeCreated()
        {
            using var context = new RulesContext();
            Assert.NotNull(context);
        }

        [Test]
        public void RulesContext_HasProvider()
        {
            using var context = new RulesContext();
            Assert.NotNull(context.Provider);
        }

        [Test]
        public void RulesContext_GetRuleSet_ReturnsRuleSet()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();
            Assert.NotNull(ruleSet);
            Assert.Equal(typeof(Order), ruleSet.FactType);
        }

        [Test]
        public void RulesContext_GetRuleSet_ReturnsSameInstance()
        {
            using var context = new RulesContext();
            var ruleSet1 = context.GetRuleSet<Order>();
            var ruleSet2 = context.GetRuleSet<Order>();
            Assert.Same(ruleSet1, ruleSet2);
        }

        [Test]
        public void RulesContext_GetRuleSet_DifferentTypesReturnDifferentSets()
        {
            using var context = new RulesContext();
            var orderRules = context.GetRuleSet<Order>();
            var customerRules = context.GetRuleSet<Customer>();
            Assert.NotSame(orderRules, customerRules);
        }

        [Test]
        public void RulesContext_HasRuleSet_ReturnsFalseInitially()
        {
            using var context = new RulesContext();
            Assert.False(context.HasRuleSet<Order>());
        }

        [Test]
        public void RulesContext_HasRuleSet_ReturnsTrueAfterGet()
        {
            using var context = new RulesContext();
            _ = context.GetRuleSet<Order>();
            Assert.True(context.HasRuleSet<Order>());
        }

        #endregion

        #region Session Creation

        [Test]
        public void RulesContext_CreateSession_ReturnsSession()
        {
            using var context = new RulesContext();
            using var session = context.CreateSession();
            Assert.NotNull(session);
        }

        [Test]
        public void RulesContext_CreateSession_HasUniqueId()
        {
            using var context = new RulesContext();
            using var session1 = context.CreateSession();
            using var session2 = context.CreateSession();
            Assert.NotEqual(session1.SessionId, session2.SessionId);
        }

        [Test]
        public void RulesContext_CreateSession_InitialStateIsActive()
        {
            using var context = new RulesContext();
            using var session = context.CreateSession();
            Assert.Equal(SessionState.Active, session.State);
        }

        #endregion

        #region Extension Methods

        [Test]
        public void RulesContext_Rules_ExtensionReturnsRuleSet()
        {
            using var context = new RulesContext();
            var ruleSet = context.Rules<Order>();
            Assert.NotNull(ruleSet);
        }

        #endregion
    }
}
