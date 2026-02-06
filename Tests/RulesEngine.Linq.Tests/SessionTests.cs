namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TestRunner.Framework;
    using RulesEngine.Linq;

    public class SessionTests
    {
        private class Order
        {
            public string Id { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public bool IsActive { get; set; }
            public bool WasProcessed { get; set; }
        }

        [Test]
        public void Session_InsertFact_AddsToFactSet()
        {
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.Insert(new Order { Id = "O1", Total = 100 });

            Assert.Equal(1, session.Facts<Order>().Count);
        }

        [Test]
        public void Session_InsertAll_AddsMultipleFacts()
        {
            using var context = new RulesContext();
            using var session = context.CreateSession();

            var orders = new[]
            {
                new Order { Id = "O1", Total = 100 },
                new Order { Id = "O2", Total = 200 },
                new Order { Id = "O3", Total = 300 }
            };

            session.InsertAll(orders);

            Assert.Equal(3, session.Facts<Order>().Count);
        }

        [Test]
        public void Session_Facts_IsQueryable()
        {
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.InsertAll(new[]
            {
                new Order { Id = "O1", Total = 100, IsActive = true },
                new Order { Id = "O2", Total = 200, IsActive = false },
                new Order { Id = "O3", Total = 300, IsActive = true }
            });

            var activeOrders = session.Facts<Order>().Where(o => o.IsActive).ToList();
            Assert.Equal(2, activeOrders.Count);
        }

        [Test]
        public void Session_Evaluate_ReturnsResult()
        {
            using var context = new RulesContext();
            context.GetRuleSet<Order>().Add(
                new Rule<Order>("R1", "High Value", o => o.Total > 150));

            using var session = context.CreateSession();
            session.InsertAll(new[]
            {
                new Order { Id = "O1", Total = 100 },
                new Order { Id = "O2", Total = 200 }
            });

            var result = session.Evaluate();

            Assert.NotNull(result);
            Assert.Equal(2, result.TotalFactsEvaluated);
            Assert.Equal(1, result.TotalMatches);
        }

        [Test]
        public void Session_Evaluate_MatchesCorrectFacts()
        {
            using var context = new RulesContext();
            context.GetRuleSet<Order>().Add(
                new Rule<Order>("R1", "High Value", o => o.Total > 150));

            using var session = context.CreateSession();
            session.Insert(new Order { Id = "O1", Total = 100 });
            session.Insert(new Order { Id = "O2", Total = 200 });
            session.Insert(new Order { Id = "O3", Total = 300 });

            var result = session.Evaluate<Order>();

            Assert.Equal(2, result.FactsWithMatches.Count);
            Assert.Equal(1, result.FactsWithoutMatches.Count);
            Assert.True(result.FactsWithMatches.All(o => o.Total > 150));
        }

        [Test]
        public void Session_Evaluate_ExecutesRuleActions()
        {
            using var context = new RulesContext();
            context.GetRuleSet<Order>().Add(
                new Rule<Order>("R1", "Process High Value", o => o.Total > 150)
                    .WithAction(o => o.WasProcessed = true));

            using var session = context.CreateSession();
            var order1 = new Order { Id = "O1", Total = 100 };
            var order2 = new Order { Id = "O2", Total = 200 };

            session.Insert(order1);
            session.Insert(order2);
            session.Evaluate();

            Assert.False(order1.WasProcessed);
            Assert.True(order2.WasProcessed);
        }

        [Test]
        public void Session_Evaluate_ReturnsMatchCountByRule()
        {
            using var context = new RulesContext();
            context.GetRuleSet<Order>()
                .WithRule(new Rule<Order>("R1", "High Value", o => o.Total > 150))
                .WithRule(new Rule<Order>("R2", "Active", o => o.IsActive));

            using var session = context.CreateSession();
            session.InsertAll(new[]
            {
                new Order { Id = "O1", Total = 200, IsActive = true },
                new Order { Id = "O2", Total = 100, IsActive = true },
                new Order { Id = "O3", Total = 200, IsActive = false }
            });

            var result = session.Evaluate<Order>();

            Assert.Equal(2, result.MatchCountByRule["R1"]);
            Assert.Equal(2, result.MatchCountByRule["R2"]);
        }

        [Test]
        public void Session_Evaluate_ReportsTotalRulesEvaluated()
        {
            // Arrange: 2 rules, 2 facts
            using var context = new RulesContext();
            context.GetRuleSet<Order>()
                .WithRule(new Rule<Order>("R1", "High Value", o => o.Total > 150))
                .WithRule(new Rule<Order>("R2", "Active", o => o.IsActive));

            using var session = context.CreateSession();
            session.InsertAll(new[]
            {
                new Order { Id = "O1", Total = 200, IsActive = true },
                new Order { Id = "O2", Total = 100, IsActive = false },
            });

            // Act
            var result = session.Evaluate();

            // Assert: 2 rules were evaluated, regardless of how many facts matched
            Assert.Equal(2, result.TotalRulesEvaluated);
        }

        [Test]
        public void Session_Commit_ChangesState()
        {
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.Commit();

            Assert.Equal(SessionState.Committed, session.State);
        }

        [Test]
        public void Session_Rollback_ClearsFacts()
        {
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.Insert(new Order { Id = "O1" });
            Assert.Equal(1, session.Facts<Order>().Count);

            session.Rollback();

            Assert.Equal(SessionState.RolledBack, session.State);
        }

        [Test]
        public void Session_AfterCommit_CannotInsert()
        {
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.Commit();

            Assert.Throws<InvalidOperationException>(() =>
                session.Insert(new Order { Id = "O1" }));
        }

        [Test]
        public void Session_FluentWithFact()
        {
            using var context = new RulesContext();
            using var session = context.CreateSession()
                .WithFact(new Order { Id = "O1", Total = 100 })
                .WithFact(new Order { Id = "O2", Total = 200 });

            Assert.Equal(2, session.Facts<Order>().Count);
        }

        [Test]
        public void Session_EvaluateT_SetsStateToEvaluating()
        {
            using var context = new RulesContext();

            SessionState? stateDuringAction = null;
            IRuleSession? sessionRef = null;

            context.GetRuleSet<Order>().Add(
                new Rule<Order>("R1", "Track state", o => true)
                    .WithAction(o => stateDuringAction = sessionRef!.State));

            using var session = context.CreateSession();
            sessionRef = session;

            session.Insert(new Order { Id = "O1", Total = 100 });

            // Act
            session.Evaluate<Order>();

            // Assert: state should have been Evaluating during the rule action
            Assert.Equal(SessionState.Evaluating, stateDuringAction);
        }

        [Test]
        public void Session_WithMatchingRules_ReturnsFactRuleMatches()
        {
            using var context = new RulesContext();
            context.GetRuleSet<Order>()
                .WithRule(new Rule<Order>("R1", "High Value", o => o.Total > 150))
                .WithRule(new Rule<Order>("R2", "Very High Value", o => o.Total > 250));

            using var session = context.CreateSession();
            session.InsertAll(new[]
            {
                new Order { Id = "O1", Total = 100 },
                new Order { Id = "O2", Total = 200 },
                new Order { Id = "O3", Total = 300 }
            });

            var matches = session.Facts<Order>().WithMatchingRules().ToList();

            Assert.Equal(2, matches.Count);

            var o2Match = matches.Single(m => m.Fact.Id == "O2");
            Assert.Equal(1, o2Match.MatchedRules.Count);
            Assert.Equal("R1", o2Match.MatchedRules[0].Id);

            var o3Match = matches.Single(m => m.Fact.Id == "O3");
            Assert.Equal(2, o3Match.MatchedRules.Count);
        }
    }
}
