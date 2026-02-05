namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TestRunner.Framework;
    using RulesEngine.Linq;
    using RulesEngine.Linq.Dependencies;
    using Mafia.Domain;

    /// <summary>
    /// TDD tests for integrating IFactContext with RuleSession.
    /// These tests define the expected behavior for cross-fact rule evaluation.
    /// Uses Mafia.Domain types (Agent, AgentMessage, etc.) for realistic scenarios.
    /// </summary>
    public class FactContextIntegrationTests
    {
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
            session.Insert(new AgentMessage { Id = "M1", FromId = "A1" });

            // Act
            var factContext = (IFactContext)session;
            var agents = factContext.Facts<Agent>().ToList();
            var messages = factContext.Facts<AgentMessage>().ToList();

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
            session.Insert(new AgentMessage { Id = "M1" });

            // Act
            var factContext = (IFactContext)session;
            var registeredTypes = factContext.RegisteredFactTypes;

            // Assert
            Assert.True(registeredTypes.Contains(typeof(Agent)));
            Assert.True(registeredTypes.Contains(typeof(AgentMessage)));
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

        [Test]
        public void Session_Evaluate_AutomaticallyPassesContextToDependentRules()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            // Insert agents with different task counts
            session.InsertAll(new[]
            {
                new Agent { Id = "A1", Role = AgentRole.Soldier, Status = AgentStatus.Available, CurrentTaskCount = 2 },
                new Agent { Id = "A2", Role = AgentRole.Soldier, Status = AgentStatus.Available, CurrentTaskCount = 5 },
                new Agent { Id = "A3", Role = AgentRole.Capo, Status = AgentStatus.Available }
            });

            // Insert message to evaluate
            var message = new AgentMessage { Id = "M1", Type = MessageType.Request, FromId = "A1" };
            session.Insert(message);

            // Create rule with context-aware condition that queries agents
            var rule = new DependentRule<AgentMessage>(
                "route-to-available",
                "Route to available soldier",
                (m, ctx) => m.Type == MessageType.Request &&
                            ctx.Facts<Agent>().Any(a => a.Status == AgentStatus.Available))
                .DependsOn<Agent>()
                .Then((m, ctx) =>
                {
                    var target = ctx.Facts<Agent>()
                        .Where(a => a.Role == AgentRole.Soldier)
                        .Where(a => a.Status == AgentStatus.Available)
                        .OrderBy(a => a.CurrentTaskCount)
                        .FirstOrDefault();

                    if (target != null)
                    {
                        m.RouteTo(target);
                    }
                });

            context.GetRuleSet<AgentMessage>().Add(rule);

            // Act - This should automatically pass the session as IFactContext
            var result = session.Evaluate<AgentMessage>();

            // Assert
            Assert.Equal(1, result.Matches.Count);
            Assert.Equal("A1", message.ToId); // Routed to least busy soldier
        }

        [Test]
        public void Session_Evaluate_WorksWithMixedRuleTypes()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            session.InsertAll(new[]
            {
                new Agent { Id = "A1", Status = AgentStatus.Available }
            });

            var message1 = new AgentMessage { Id = "M1", Type = MessageType.Request };
            var message2 = new AgentMessage { Id = "M2", Type = MessageType.Alert };
            session.InsertAll(new[] { message1, message2 });

            // Add a simple rule (no context needed)
            var simpleRule = new Rule<AgentMessage>(
                "alert-handler",
                "Handle alerts",
                m => m.Type == MessageType.Alert)
                .WithAction(m => m.Flags.Add("alert-processed"));

            // Add a context-aware DependentRule
            var contextRule = new DependentRule<AgentMessage>(
                "route-if-available",
                "Route if agents available",
                (m, ctx) => m.Type == MessageType.Request &&
                            ctx.Facts<Agent>().Any(a => a.Status == AgentStatus.Available))
                .DependsOn<Agent>()
                .Then(m => m.Flags.Add("routed-to-available"));

            context.GetRuleSet<AgentMessage>().Add(simpleRule);
            context.GetRuleSet<AgentMessage>().Add(contextRule);

            // Act
            var result = session.Evaluate<AgentMessage>();

            // Assert - Both rules should have matched
            Assert.Equal(2, result.Matches.Count);
            Assert.True(message2.Flags.Contains("alert-processed")); // Alert handled by simple rule
            Assert.True(message1.Flags.Contains("routed-to-available")); // Request handled by context rule
        }

        // Legacy test - manual context passing (keeping for reference)
        [Test]
        public void DependentRule_CanQueryOtherFactTypes_DuringEvaluation()
        {
            // Arrange
            using var context = new RulesContext();
            using var session = context.CreateSession();

            // Insert agents with different task counts
            session.InsertAll(new[]
            {
                new Agent { Id = "A1", Role = AgentRole.Soldier, Status = AgentStatus.Available, CurrentTaskCount = 2 },
                new Agent { Id = "A2", Role = AgentRole.Soldier, Status = AgentStatus.Available, CurrentTaskCount = 5 },
                new Agent { Id = "A3", Role = AgentRole.Capo, Status = AgentStatus.Available }
            });

            // Insert message to evaluate
            var message = new AgentMessage { Id = "M1", Type = MessageType.Request, FromId = "A1" };
            session.Insert(message);

            // Create rule that queries agents during evaluation
            // Rule: Route to least busy available soldier
            var rule = new DependentRule<AgentMessage>(
                "route-to-least-busy",
                "Route to least busy soldier",
                m => m.Type == MessageType.Request)
                .DependsOn<Agent>()
                .Then((m, ctx) =>
                {
                    var target = ctx.Facts<Agent>()
                        .Where(a => a.Role == AgentRole.Soldier)
                        .Where(a => a.Status == AgentStatus.Available)
                        .OrderBy(a => a.CurrentTaskCount)
                        .FirstOrDefault();

                    if (target != null)
                    {
                        m.RouteTo(target);
                    }
                });

            context.GetRuleSet<AgentMessage>().Add(rule);

            // Act
            var factContext = (IFactContext)session;

            // Evaluate the rule with context
            if (rule.EvaluateWithContext(message, factContext))
            {
                rule.ExecuteWithContext(message, factContext);
            }

            // Assert - A1 has CurrentTaskCount=2, less than A2's 5
            Assert.Equal("A1", message.ToId);
        }

        #endregion

        #region AnalyzeDependencies at Registration

        [Test]
        public void RuleSet_Add_CallsAnalyzeDependencies_WhenSchemaConfigured()
        {
            // Arrange
            using var context = new RulesContext();

            // Configure schema with fact types
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<AgentMessage>();
            });

            // Create a rule that depends on Agent
            var rule = new DependentRule<AgentMessage>(
                "route-message",
                "Route based on agents",
                (m, ctx) => ctx.Facts<Agent>().Any(a => a.Status == AgentStatus.Available))
                .DependsOn<Agent>()
                .Then(m => { });

            // Before adding: only explicit dependencies
            var beforeDeps = rule.AllDependencies;
            Assert.Equal(1, beforeDeps.Count); // Only explicit Agent dependency

            // Act
            context.GetRuleSet<AgentMessage>().Add(rule);

            // Assert - After adding with schema, analysis should have detected Facts<Agent>() call
            var afterDeps = rule.AllDependencies;
            Assert.True(afterDeps.Contains(typeof(Agent)));

            // The rule should have been analyzed (has analysis result)
            Assert.NotNull(rule.AnalysisResult);
        }

        [Test]
        public void RuleSet_Add_ValidatesDependencies_AgainstSchema()
        {
            // Arrange
            using var context = new RulesContext();

            // Configure schema WITHOUT Agent registered
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<AgentMessage>();
                // Note: Agent is NOT registered
            });

            // Create a rule that explicitly depends on Agent (not in schema)
            var rule = new DependentRule<AgentMessage>(
                "invalid-rule",
                "Has unregistered dependency",
                m => m.Type == MessageType.Request)
                .DependsOn<Agent>()  // Agent not in schema!
                .Then(m => { });

            // Act & Assert - Should throw because Agent is not in schema
            Assert.Throws<InvalidOperationException>(() =>
            {
                context.GetRuleSet<AgentMessage>().Add(rule);
            });
        }

        [Test]
        public void RuleSet_Add_DetectsDependencies_FromContextFactsCall()
        {
            // Arrange
            using var context = new RulesContext();

            // Configure schema with all fact types
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<AgentMessage>();
            });

            // Create a rule that queries Facts<Agent>() but doesn't explicitly declare it
            var rule = new DependentRule<AgentMessage>(
                "detect-deps",
                "Detect dependencies from expression",
                (m, ctx) => ctx.Facts<Agent>().Count() > 0)
                // Note: No explicit .DependsOn<Agent>() call
                .Then(m => { });

            // Act
            context.GetRuleSet<AgentMessage>().Add(rule);

            // Assert - Dependency on Agent should be detected from expression
            Assert.True(rule.AllDependencies.Contains(typeof(Agent)));
        }

        #endregion

        #region DependencyGraph Integration

        [Test]
        public void RulesContext_HasDependencyGraph_WhenSchemaConfigured()
        {
            // Arrange
            using var context = new RulesContext();

            // Act
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<AgentMessage>();
            });

            // Assert
            Assert.NotNull(context.DependencyGraph);
        }

        [Test]
        public void DependencyGraph_TracksRuleDependencies_WhenRulesAdded()
        {
            // Arrange
            using var context = new RulesContext();
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<Territory>();
                schema.RegisterFactType<AgentMessage>();
            });

            // Create rule: AgentMessage depends on Agent
            var rule1 = new DependentRule<AgentMessage>(
                "route-message",
                "Route based on agents",
                (m, ctx) => ctx.Facts<Agent>().Any(a => a.Status == AgentStatus.Available))
                .Then(m => { });

            // Create rule: Agent depends on Territory (for territory-based assignment)
            var rule2 = new DependentRule<Agent>(
                "assign-territory",
                "Assign based on territory",
                (a, ctx) => ctx.Facts<Territory>().Any(t => t.ControlledBy == a.FamilyId))
                .Then(a => { });

            // Act
            context.GetRuleSet<AgentMessage>().Add(rule1);
            context.GetRuleSet<Agent>().Add(rule2);

            // Assert - DependencyGraph should track: AgentMessage -> Agent -> Territory
            var graph = context.DependencyGraph!;
            var loadOrder = graph.GetLoadOrder().ToList();

            // Territory should come before Agent, Agent before AgentMessage
            var territoryIndex = loadOrder.IndexOf(typeof(Territory));
            var agentIndex = loadOrder.IndexOf(typeof(Agent));
            var messageIndex = loadOrder.IndexOf(typeof(AgentMessage));

            Assert.True(territoryIndex < agentIndex, "Territory should load before Agent");
            Assert.True(agentIndex < messageIndex, "Agent should load before AgentMessage");
        }

        [Test]
        public void DependencyGraph_GetLoadOrder_ReturnsCorrectOrder()
        {
            // Arrange
            using var context = new RulesContext();
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<AgentMessage>();
            });

            // AgentMessage rule depends on Agent
            var rule = new DependentRule<AgentMessage>(
                "check-agents",
                "Check available agents",
                (m, ctx) => ctx.Facts<Agent>().Any())
                .Then(m => { });

            context.GetRuleSet<AgentMessage>().Add(rule);

            // Act
            var loadOrder = context.DependencyGraph!.GetLoadOrder().ToList();

            // Assert - Agent should come before AgentMessage
            Assert.True(loadOrder.IndexOf(typeof(Agent)) < loadOrder.IndexOf(typeof(AgentMessage)));
        }

        #endregion
    }
}
