namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TestRunner.Framework;
    using RulesEngine.Linq;
    using RulesEngine.Linq.Dependencies;

    /// <summary>
    /// TDD tests for integrating IFactContext with RuleSession.
    /// These tests define the expected behavior for cross-fact rule evaluation.
    /// </summary>
    public class FactContextIntegrationTests
    {
        #region Test Domain

        private class Agent
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public AgentRole Role { get; set; }
            public AgentStatus Status { get; set; }
            public int TaskCount { get; set; }
        }

        private class Message
        {
            public string Id { get; set; } = string.Empty;
            public string FromAgentId { get; set; } = string.Empty;
            public string ToAgentId { get; set; } = string.Empty;
            public MessageType Type { get; set; }
            public bool Blocked { get; set; }
            public string? BlockReason { get; set; }
        }

        private enum AgentRole { Soldier, Capo, Underboss, Godfather }
        private enum AgentStatus { Available, Busy, Offline }
        private enum MessageType { Request, Command, Alert }

        #endregion

        #region RuleSession as IFactContext

        [Test]
        public void RuleSession_CanBeCastTo_IFactContext()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            // Act & Assert
            var factContext = session as IFactContext;
            Assert.NotNull(factContext);
        }

        [Test]
        public void IFactContext_Facts_ReturnsInsertedFacts()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.Insert(new Agent { Id = "A1", Name = "Agent One" });
            session.Insert(new Agent { Id = "A2", Name = "Agent Two" });

            // Act
            var factContext = (IFactContext)session;
            var agents = factContext.Facts<Agent>().ToList();

            // Assert
            Assert.Equal(2, agents.Count);
            Assert.True(agents.Any(a => a.Id == "A1"));
            Assert.True(agents.Any(a => a.Id == "A2"));
        }

        [Test]
        public void IFactContext_Facts_IsQueryable()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.InsertAll(new[]
            {
                new Agent { Id = "A1", Status = AgentStatus.Available },
                new Agent { Id = "A2", Status = AgentStatus.Busy },
                new Agent { Id = "A3", Status = AgentStatus.Available }
            });

            // Act
            var factContext = (IFactContext)session;
            var availableAgents = factContext.Facts<Agent>()
                .Where(a => a.Status == AgentStatus.Available)
                .ToList();

            // Assert
            Assert.Equal(2, availableAgents.Count);
        }

        [Test]
        public void IFactContext_Facts_CanQueryMultipleTypes()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.Insert(new Agent { Id = "A1", Name = "Agent One" });
            session.Insert(new Message { Id = "M1", FromAgentId = "A1" });

            // Act
            var factContext = (IFactContext)session;
            var agents = factContext.Facts<Agent>().ToList();
            var messages = factContext.Facts<Message>().ToList();

            // Assert
            Assert.Equal(1, agents.Count);
            Assert.Equal(1, messages.Count);
        }

        [Test]
        public void IFactContext_RegisteredFactTypes_ReturnsInsertedTypes()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.Insert(new Agent { Id = "A1" });
            session.Insert(new Message { Id = "M1" });

            // Act
            var factContext = (IFactContext)session;
            var registeredTypes = factContext.RegisteredFactTypes;

            // Assert
            Assert.True(registeredTypes.Contains(typeof(Agent)));
            Assert.True(registeredTypes.Contains(typeof(Message)));
        }

        [Test]
        public void IFactContext_FindByKey_ReturnsMatchingFact()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.Insert(new Agent { Id = "A1", Name = "Agent One" });
            session.Insert(new Agent { Id = "A2", Name = "Agent Two" });

            // Act
            var factContext = (IFactContext)session;
            var agent = factContext.FindByKey<Agent>("A1");

            // Assert
            Assert.NotNull(agent);
            Assert.Equal("Agent One", agent!.Name);
        }

        [Test]
        public void IFactContext_FindByKey_ReturnsNullWhenNotFound()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.Insert(new Agent { Id = "A1", Name = "Agent One" });

            // Act
            var factContext = (IFactContext)session;
            var agent = factContext.FindByKey<Agent>("NOT_FOUND");

            // Assert
            Assert.Null(agent);
        }

        #endregion

        #region Cross-Fact Rule Evaluation

        // TODO: These tests will pass once DependentRule is wired to use IFactContext during evaluation

        [Test]
        public void DependentRule_CanQueryOtherFactTypes_DuringEvaluation()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            // Insert agents
            session.InsertAll(new[]
            {
                new Agent { Id = "A1", Role = AgentRole.Soldier, Status = AgentStatus.Available, TaskCount = 2 },
                new Agent { Id = "A2", Role = AgentRole.Soldier, Status = AgentStatus.Available, TaskCount = 5 },
                new Agent { Id = "A3", Role = AgentRole.Capo, Status = AgentStatus.Available }
            });

            // Insert message to evaluate
            var message = new Message { Id = "M1", Type = MessageType.Request, FromAgentId = "A1" };
            session.Insert(message);

            // Create rule that queries agents during evaluation
            // Rule: Route to least busy available soldier
            var rule = new DependentRule<Message>(
                "route-to-least-busy",
                "Route to least busy soldier",
                m => m.Type == MessageType.Request)
                .DependsOn<Agent>()
                .Then((m, ctx) =>
                {
                    var target = ctx.Facts<Agent>()
                        .Where(a => a.Role == AgentRole.Soldier)
                        .Where(a => a.Status == AgentStatus.Available)
                        .OrderBy(a => a.TaskCount)
                        .FirstOrDefault();

                    if (target != null)
                    {
                        m.ToAgentId = target.Id;
                    }
                });

            context.GetRuleSet<Message>().Add(rule);

            // Act
            var factContext = (IFactContext)session;

            // Evaluate the rule with context
            if (rule.EvaluateWithContext(message, factContext))
            {
                rule.ExecuteWithContext(message, factContext);
            }

            // Assert
            Assert.Equal("A1", message.ToAgentId); // A1 has TaskCount=2, less than A2's 5
        }

        #endregion
    }
}
