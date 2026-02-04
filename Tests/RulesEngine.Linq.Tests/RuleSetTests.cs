namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Linq;
    using TestRunner.Framework;
    using RulesEngine.Linq;

    public class RuleSetTests
    {
        private class Order
        {
            public string Id { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public bool IsActive { get; set; }
        }

        [Test]
        public void RuleSet_AddRule_IncreasesCount()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            var rule = new Rule<Order>("R1", "Test Rule", o => o.Total > 100);
            ruleSet.Add(rule);

            Assert.Equal(1, ruleSet.Count);
        }

        [Test]
        public void RuleSet_AddRule_CanFindById()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            var rule = new Rule<Order>("R1", "Test Rule", o => o.Total > 100);
            ruleSet.Add(rule);

            var found = ruleSet.FindById("R1");
            Assert.NotNull(found);
            Assert.Equal("R1", found!.Id);
        }

        [Test]
        public void RuleSet_AddRule_DuplicateIdThrows()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            var rule1 = new Rule<Order>("R1", "Rule 1", o => o.Total > 100);
            var rule2 = new Rule<Order>("R1", "Rule 2", o => o.Total > 200);

            ruleSet.Add(rule1);
            Assert.Throws<InvalidOperationException>(() => ruleSet.Add(rule2));
        }

        [Test]
        public void RuleSet_RemoveRule_DecreasesCount()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            ruleSet.Add(new Rule<Order>("R1", "Rule 1", o => o.Total > 100));
            ruleSet.Add(new Rule<Order>("R2", "Rule 2", o => o.Total > 200));

            Assert.Equal(2, ruleSet.Count);
            ruleSet.Remove("R1");
            Assert.Equal(1, ruleSet.Count);
        }

        [Test]
        public void RuleSet_RemoveRule_ReturnsTrueIfFound()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            ruleSet.Add(new Rule<Order>("R1", "Rule 1", o => o.Total > 100));

            Assert.True(ruleSet.Remove("R1"));
            Assert.False(ruleSet.Remove("R1"));
        }

        [Test]
        public void RuleSet_Clear_RemovesAllRules()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            ruleSet.Add(new Rule<Order>("R1", "Rule 1", o => o.Total > 100));
            ruleSet.Add(new Rule<Order>("R2", "Rule 2", o => o.Total > 200));

            ruleSet.Clear();
            Assert.Equal(0, ruleSet.Count);
        }

        [Test]
        public void RuleSet_Contains_ReturnsTrueForExistingRule()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            ruleSet.Add(new Rule<Order>("R1", "Rule 1", o => o.Total > 100));

            Assert.True(ruleSet.Contains("R1"));
            Assert.False(ruleSet.Contains("R2"));
        }

        [Test]
        public void RuleSet_IsQueryable()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            ruleSet.Add(new Rule<Order>("R1", "Low Value", o => o.Total > 100, priority: 1));
            ruleSet.Add(new Rule<Order>("R2", "High Value", o => o.Total > 1000, priority: 2));
            ruleSet.Add(new Rule<Order>("R3", "Medium Value", o => o.Total > 500, priority: 1));

            var highPriorityRules = ruleSet.Where(r => r.Priority > 1).ToList();
            Assert.Equal(1, highPriorityRules.Count);
            Assert.Equal("R2", highPriorityRules[0].Id);
        }

        [Test]
        public void RuleSet_QueryByName()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            ruleSet.Add(new Rule<Order>("R1", "High Value Order", o => o.Total > 1000));
            ruleSet.Add(new Rule<Order>("R2", "Low Value Order", o => o.Total < 100));
            ruleSet.Add(new Rule<Order>("R3", "Active Order", o => o.IsActive));

            var valueRules = ruleSet.Where(r => r.Name.Contains("Value")).ToList();
            Assert.Equal(2, valueRules.Count);
        }

        [Test]
        public void RuleSet_OrderByPriority()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>();

            ruleSet.Add(new Rule<Order>("R1", "Rule 1", o => o.Total > 100, priority: 1));
            ruleSet.Add(new Rule<Order>("R2", "Rule 2", o => o.Total > 200, priority: 3));
            ruleSet.Add(new Rule<Order>("R3", "Rule 3", o => o.Total > 300, priority: 2));

            var ordered = ruleSet.OrderByDescending(r => r.Priority).ToList();
            Assert.Equal("R2", ordered[0].Id);
            Assert.Equal("R3", ordered[1].Id);
            Assert.Equal("R1", ordered[2].Id);
        }

        [Test]
        public void RuleSet_FluentWithRule()
        {
            using var context = new RulesContext();
            var ruleSet = context.GetRuleSet<Order>()
                .WithRule(new Rule<Order>("R1", "Rule 1", o => o.Total > 100))
                .WithRule(new Rule<Order>("R2", "Rule 2", o => o.Total > 200));

            Assert.Equal(2, ruleSet.Count);
        }
    }
}
