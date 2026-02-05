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
                (a, ctx) => ctx.Facts<Territory>().Any(t => t.ControlledById == a.FamilyId))
                .Then(a => { });

            // Act
            context.GetRuleSet<AgentMessage>().Add(rule1);
            context.GetRuleSet<Agent>().Add(rule2);

            // Assert - DependencyGraph should track: AgentMessage -> Agent -> Territory
            var graph = context.DependencyGraph!;
            var loadOrder = graph.GetLoadOrder();

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
            var loadOrder = context.DependencyGraph!.GetLoadOrder();

            // Assert - Agent should come before AgentMessage
            Assert.True(loadOrder.IndexOf(typeof(Agent)) < loadOrder.IndexOf(typeof(AgentMessage)));
        }

        [Test]
        public void Session_Evaluate_EvaluatesFactTypesInDependencyOrder()
        {
            // Arrange
            var evaluationOrder = new List<Type>();

            using var context = new RulesContext();
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Territory>();
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<AgentMessage>();
            });

            // Create rules that track evaluation order
            // AgentMessage depends on Agent
            var messageRule = new DependentRule<AgentMessage>(
                "message-rule",
                "Track message evaluation",
                (m, ctx) => ctx.Facts<Agent>().Any())
                .Then(m => evaluationOrder.Add(typeof(AgentMessage)));

            // Agent depends on Territory
            var agentRule = new DependentRule<Agent>(
                "agent-rule",
                "Track agent evaluation",
                (a, ctx) => ctx.Facts<Territory>().Any())
                .Then(a => evaluationOrder.Add(typeof(Agent)));

            // Territory has no dependencies
            var territoryRule = new Rule<Territory>(
                "territory-rule",
                "Track territory evaluation",
                t => true)
                .WithAction(t => evaluationOrder.Add(typeof(Territory)));

            context.GetRuleSet<AgentMessage>().Add(messageRule);
            context.GetRuleSet<Agent>().Add(agentRule);
            context.GetRuleSet<Territory>().Add(territoryRule);

            using var session = context.CreateSession();
            session.Insert(new Territory { Id = "T1", Name = "Downtown" });
            session.Insert(new Agent { Id = "A1", Status = AgentStatus.Available });
            session.Insert(new AgentMessage { Id = "M1", Type = MessageType.Request });

            // Act
            session.Evaluate();

            // Assert - Evaluation order should follow dependencies: Territory → Agent → AgentMessage
            Assert.Equal(3, evaluationOrder.Count);
            Assert.True(
                evaluationOrder.IndexOf(typeof(Territory)) < evaluationOrder.IndexOf(typeof(Agent)),
                "Territory should be evaluated before Agent");
            Assert.True(
                evaluationOrder.IndexOf(typeof(Agent)) < evaluationOrder.IndexOf(typeof(AgentMessage)),
                "Agent should be evaluated before AgentMessage");
        }

        #endregion

        #region Closure-Captured Cross-Fact Queries (EF Core-inspired pattern)

        [Test]
        public void ClosureCapture_RuleWithFactsAny_MatchesWhenAgentAvailable()
        {
            // Arrange
            using var context = new RulesContext();

            // Capture context.Facts<Agent>() in a closure - this returns FactQueryable
            // whose Expression is FactQueryExpression (not actual data)
            var agents = context.Facts<Agent>();

            // Create rule that uses the captured queryable
            // At definition time: agents.Any(...) builds an expression tree with FactQueryExpression
            // At evaluation time: FactQueryRewriter substitutes actual session data
            var rule = new Rule<AgentMessage>(
                "route-if-available",
                "Route if any agent available",
                m => m.Type == MessageType.Request && agents.Any(a => a.Status == AgentStatus.Available))
                .Then(m => m.Flags.Add("routed-closure"));

            context.GetRuleSet<AgentMessage>().Add(rule);

            using var session = context.CreateSession();

            // Insert an available agent
            session.Insert(new Agent { Id = "A1", Status = AgentStatus.Available });

            // Insert a message
            var message = new AgentMessage { Id = "M1", Type = MessageType.Request };
            session.Insert(message);

            // Act
            var result = session.Evaluate<AgentMessage>();

            // Assert - Rule should match because there's an available agent
            Assert.Equal(1, result.Matches.Count);
            Assert.True(message.Flags.Contains("routed-closure"));
        }

        [Test]
        public void ClosureCapture_RuleWithFactsAny_DoesNotMatchWhenNoAgentAvailable()
        {
            // Arrange
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            var rule = new Rule<AgentMessage>(
                "route-if-available",
                "Route if any agent available",
                m => m.Type == MessageType.Request && agents.Any(a => a.Status == AgentStatus.Available))
                .Then(m => m.Flags.Add("routed-closure"));

            context.GetRuleSet<AgentMessage>().Add(rule);

            using var session = context.CreateSession();

            // Insert agent that is NOT available
            session.Insert(new Agent { Id = "A1", Status = AgentStatus.Busy });

            var message = new AgentMessage { Id = "M1", Type = MessageType.Request };
            session.Insert(message);

            // Act
            var result = session.Evaluate<AgentMessage>();

            // Assert - Rule should NOT match because no agent is available
            Assert.Equal(0, result.Matches.Count);
            Assert.False(message.Flags.Contains("routed-closure"));
        }

        [Test]
        public void ClosureCapture_RuleWithComplexQuery_WorksCorrectly()
        {
            // Arrange
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            // More complex query: check if any soldier has low task count
            var rule = new Rule<AgentMessage>(
                "route-to-idle-soldier",
                "Route if idle soldier exists",
                m => m.Type == MessageType.Request &&
                     agents.Any(a => a.Role == AgentRole.Soldier &&
                                     a.Status == AgentStatus.Available &&
                                     a.CurrentTaskCount < 3))
                .Then(m => m.Flags.Add("idle-soldier-found"));

            context.GetRuleSet<AgentMessage>().Add(rule);

            using var session = context.CreateSession();

            // Insert soldiers with varying task counts
            session.InsertAll(new[]
            {
                new Agent { Id = "A1", Role = AgentRole.Soldier, Status = AgentStatus.Available, CurrentTaskCount = 5 },
                new Agent { Id = "A2", Role = AgentRole.Soldier, Status = AgentStatus.Available, CurrentTaskCount = 2 }, // This one matches
                new Agent { Id = "A3", Role = AgentRole.Capo, Status = AgentStatus.Available, CurrentTaskCount = 0 } // Wrong role
            });

            var message = new AgentMessage { Id = "M1", Type = MessageType.Request };
            session.Insert(message);

            // Act
            var result = session.Evaluate<AgentMessage>();

            // Assert
            Assert.Equal(1, result.Matches.Count);
            Assert.True(message.Flags.Contains("idle-soldier-found"));
        }

        [Test]
        public void ClosureCapture_RuleRequiresRewriting_IsTrue()
        {
            // Arrange
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            // Rule with closure-captured Facts<T>()
            var ruleWithClosure = new Rule<AgentMessage>(
                "with-closure",
                "Has closure",
                m => agents.Any());

            // Rule without closure
            var ruleWithoutClosure = new Rule<AgentMessage>(
                "no-closure",
                "No closure",
                m => m.Type == MessageType.Request);

            // Assert
            Assert.True(ruleWithClosure.RequiresRewriting, "Rule with closure should require rewriting");
            Assert.False(ruleWithoutClosure.RequiresRewriting, "Rule without closure should not require rewriting");
        }

        [Test]
        public void ClosureCapture_DirectEvaluate_ThrowsForRuleRequiringRewriting()
        {
            // Arrange
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            var rule = new Rule<AgentMessage>(
                "needs-rewriting",
                "Needs rewriting",
                m => agents.Any());

            var message = new AgentMessage { Id = "M1" };

            // Act & Assert - Direct Evaluate should throw
            Assert.Throws<InvalidOperationException>(() => rule.Evaluate(message));
        }

        [Test]
        public void ClosureCapture_MultipleFactTypes_WorksTogether()
        {
            // Arrange
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();
            var territories = context.Facts<Territory>();

            // Rule that queries both Agent and Territory
            var rule = new Rule<AgentMessage>(
                "multi-fact-check",
                "Check agents and territories",
                m => agents.Any(a => a.Status == AgentStatus.Available) &&
                     territories.Any(t => t.Value > 1000)) // High value territory
                .Then(m => m.Flags.Add("safe-route-available"));

            context.GetRuleSet<AgentMessage>().Add(rule);

            using var session = context.CreateSession();

            session.Insert(new Agent { Id = "A1", Status = AgentStatus.Available });
            session.Insert(new Territory { Id = "T1", Name = "Downtown", Value = 5000 });

            var message = new AgentMessage { Id = "M1", Type = MessageType.Request };
            session.Insert(message);

            // Act
            var result = session.Evaluate<AgentMessage>();

            // Assert
            Assert.Equal(1, result.Matches.Count);
            Assert.True(message.Flags.Contains("safe-route-available"));
        }

        [Test]
        public void ClosureCapture_EmptyFactSet_AnyReturnsFalse()
        {
            // Arrange
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            var rule = new Rule<AgentMessage>(
                "check-agents",
                "Check for agents",
                m => agents.Any()) // No agents inserted
                .Then(m => m.Flags.Add("has-agents"));

            context.GetRuleSet<AgentMessage>().Add(rule);

            using var session = context.CreateSession();

            // Don't insert any agents - fact set will be empty

            var message = new AgentMessage { Id = "M1" };
            session.Insert(message);

            // Act
            var result = session.Evaluate<AgentMessage>();

            // Assert - Rule should not match because no agents exist
            Assert.Equal(0, result.Matches.Count);
            Assert.False(message.Flags.Contains("has-agents"));
        }

        [Test]
        public void ClosureCapture_WithCount_WorksCorrectly()
        {
            // Arrange
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            // Rule that uses Count()
            var rule = new Rule<AgentMessage>(
                "need-backup",
                "Request backup if few agents",
                m => m.Type == MessageType.Alert && agents.Count(a => a.Status == AgentStatus.Available) < 2)
                .Then(m => m.Flags.Add("backup-requested"));

            context.GetRuleSet<AgentMessage>().Add(rule);

            using var session = context.CreateSession();

            // Insert only one available agent
            session.Insert(new Agent { Id = "A1", Status = AgentStatus.Available });

            var message = new AgentMessage { Id = "M1", Type = MessageType.Alert };
            session.Insert(message);

            // Act
            var result = session.Evaluate<AgentMessage>();

            // Assert
            Assert.Equal(1, result.Matches.Count);
            Assert.True(message.Flags.Contains("backup-requested"));
        }

        [Test]
        public void ClosureCapture_SessionCacheIsUsed_ForSameSession()
        {
            // Arrange
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            var rule = new Rule<AgentMessage>(
                "cached-rule",
                "Uses cache",
                m => agents.Any(a => a.Status == AgentStatus.Available));

            context.GetRuleSet<AgentMessage>().Add(rule);

            using var session = context.CreateSession();

            session.Insert(new Agent { Id = "A1", Status = AgentStatus.Available });
            session.Insert(new AgentMessage { Id = "M1" });
            session.Insert(new AgentMessage { Id = "M2" });

            // Act - Evaluate twice, should use cached compiled condition
            var result = session.Evaluate<AgentMessage>();

            // Assert - Both messages should match
            Assert.Equal(2, result.Matches.Count);
        }

        [Test]
        public void ClosureCapture_MixedWithRegularRules_WorksTogether()
        {
            // Arrange
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            // Closure-based rule
            var closureRule = new Rule<AgentMessage>(
                "closure-rule",
                "Closure based",
                m => m.Type == MessageType.Request && agents.Any(a => a.Status == AgentStatus.Available))
                .WithPriority(100)
                .Then(m => m.Flags.Add("closure-matched"));

            // Regular rule (no closure)
            var regularRule = new Rule<AgentMessage>(
                "regular-rule",
                "Regular rule",
                m => m.Type == MessageType.Alert)
                .WithPriority(50)
                .Then(m => m.Flags.Add("regular-matched"));

            context.GetRuleSet<AgentMessage>().Add(closureRule);
            context.GetRuleSet<AgentMessage>().Add(regularRule);

            using var session = context.CreateSession();

            session.Insert(new Agent { Id = "A1", Status = AgentStatus.Available });

            var request = new AgentMessage { Id = "M1", Type = MessageType.Request };
            var alert = new AgentMessage { Id = "M2", Type = MessageType.Alert };
            session.InsertAll(new[] { request, alert });

            // Act
            var result = session.Evaluate<AgentMessage>();

            // Assert
            Assert.Equal(2, result.Matches.Count);
            Assert.True(request.Flags.Contains("closure-matched"));
            Assert.True(alert.Flags.Contains("regular-matched"));
        }

        #endregion

        #region Schema-Defined Dependencies with Both Cross-Fact Patterns

        [Test]
        public void BothCrossFactPatterns_WorkWithSchemaDefinedDependencies()
        {
            // Arrange
            using var context = new RulesContext();

            // Schema defines fact types AND their relationships (dependencies)
            // DependsOn<Agent> on Territory means: Territory depends on Agent
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<Territory>(cfg =>
                {
                    // Territory depends on Agent (agents are assigned to territories)
                    // This SHOULD automatically register Territory -> Agent in dependency graph
                    cfg.DependsOn<Agent>();
                });
            });

            // Get queryable for closure capture (Pattern 2)
            var agents = context.Facts<Agent>();

            // PATTERN 1: DependentRule - explicit context parameter
            var occupiedRule = new DependentRule<Territory>(
                "occupied", "Territory is occupied",
                (territory, ctx) => ctx.Facts<Agent>().Any(a => a.TerritoryId == territory.Id))
                .Then(t => t.Status = "occupied");

            // PATTERN 2: Rule<T> - closure capture (EF Core style)
            var guardedRule = new Rule<Territory>(
                "guarded", "Territory has a guard",
                t => agents.Any(a => a.Role == AgentRole.Soldier && a.TerritoryId == t.Id))
                .Then(t => t.Status = (t.Status ?? "") + "-guarded");

            // Both rules work - add to ruleset
            var rules = context.GetRuleSet<Territory>();
            rules.Add(occupiedRule);
            rules.Add(guardedRule);

            // Dependencies should be tracked from BOTH sources:
            // 1. Schema navigations (Territory -> Agent via HasOne)
            // 2. Rule analysis (Territory rules -> Agent)
            var loadOrder = context.DependencyGraph!.GetLoadOrder();

            // Agent should load before Territory (Territory depends on Agent)
            var agentIndex = loadOrder.IndexOf(typeof(Agent));
            var territoryIndex = loadOrder.IndexOf(typeof(Territory));

            Assert.True(agentIndex >= 0, "Agent should be in load order");
            Assert.True(territoryIndex >= 0, "Territory should be in load order");
            Assert.True(agentIndex < territoryIndex,
                $"Agent (index {agentIndex}) should load before Territory (index {territoryIndex})");

            // Evaluate session - both patterns execute correctly
            using var session = context.CreateSession();
            session.InsertAll(new[]
            {
                new Agent { Id = "a1", Name = "Tony", Role = AgentRole.Soldier, TerritoryId = "downtown" },
                new Agent { Id = "a2", Name = "Sal", Role = AgentRole.Capo, TerritoryId = "docks" }
            });
            session.InsertAll(new[]
            {
                new Territory { Id = "downtown", Name = "Downtown" },
                new Territory { Id = "docks", Name = "Docks" },
                new Territory { Id = "airport", Name = "Airport" }
            });

            var result = session.Evaluate<Territory>();

            // Downtown: occupied (a1) + guarded (a1 is Soldier)
            // Docks: occupied (a2) + not guarded (a2 is Capo, not Soldier)
            // Airport: neither
            Assert.Equal(2, result.FactsWithMatches.Count);

            var downtown = result.FactsWithMatches.First(t => t.Id == "downtown");
            Assert.Equal("occupied-guarded", downtown.Status);

            var docks = result.FactsWithMatches.First(t => t.Id == "docks");
            Assert.Equal("occupied", docks.Status);
        }

        [Test]
        public void SchemaDependencies_AutomaticallyWireToDependencyGraph()
        {
            // Arrange
            using var context = new RulesContext();

            // Configure schema with DependsOn declarations
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<Territory>(cfg =>
                {
                    // This should automatically add Territory -> Agent dependency
                    cfg.DependsOn<Agent>();
                });
                schema.RegisterFactType<AgentMessage>(cfg =>
                {
                    // This should automatically add AgentMessage -> Agent dependency
                    cfg.DependsOn<Agent>();
                });
            });

            // Act - no rules added, just schema
            var graph = context.DependencyGraph!;
            var loadOrder = graph.GetLoadOrder();

            // Assert - Schema dependencies should have been wired to dependency graph
            // Agent has no dependencies, so it comes first
            // Territory depends on Agent (via DependsOn<Agent>)
            // AgentMessage depends on Agent (via DependsOn<Agent>)

            var agentIndex = loadOrder.IndexOf(typeof(Agent));
            var territoryIndex = loadOrder.IndexOf(typeof(Territory));
            var messageIndex = loadOrder.IndexOf(typeof(AgentMessage));

            Assert.True(agentIndex >= 0, "Agent should be in load order");
            Assert.True(territoryIndex >= 0, "Territory should be in load order");
            Assert.True(messageIndex >= 0, "AgentMessage should be in load order");

            Assert.True(agentIndex < territoryIndex,
                "Agent should come before Territory (Territory.DependsOn<Agent>)");
            Assert.True(agentIndex < messageIndex,
                "Agent should come before AgentMessage (AgentMessage.DependsOn<Agent>)");
        }

        [Test]
        public void ClosurePattern_DependencyDetected_AtRuleRegistration()
        {
            // Arrange
            using var context = new RulesContext();

            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<Territory>();
            });

            // Capture for closure
            var agents = context.Facts<Agent>();

            // Create rule with closure-captured Facts<Agent>()
            var rule = new Rule<Territory>(
                "closure-dep",
                "Closure-based dependency",
                t => agents.Any(a => a.TerritoryId == t.Id));

            // Act - adding the rule should trigger dependency analysis
            context.GetRuleSet<Territory>().Add(rule);

            // Assert - The closure-captured Facts<Agent>() should be detected
            var graph = context.DependencyGraph!;
            var territoryDeps = graph.GetDependencies(typeof(Territory));

            Assert.True(territoryDeps.Contains(typeof(Agent)),
                "Territory should depend on Agent (detected from closure capture)");
        }

        #endregion

        #region Condition Projection (DependentRule → IRule<T>.Condition contract)

        [Test]
        public void DependentRule_WithContextCondition_ConditionDoesNotThrow()
        {
            // Arrange - a rule with (m, ctx) => ... condition
            var rule = new DependentRule<AgentMessage>(
                "test", "Test rule",
                (m, ctx) => ctx.Facts<Agent>().Any(a => a.Status == AgentStatus.Available));

            // Act & Assert - accessing Condition should NOT throw
            var condition = rule.Condition;
            Assert.NotNull(condition);
        }

        [Test]
        public void DependentRule_WithSimpleCondition_ConditionWorksDirectly()
        {
            // Arrange - a rule with simple (m) => ... condition
            var rule = new DependentRule<AgentMessage>(
                "test", "Test rule",
                (AgentMessage m) => m.Type == MessageType.Request);

            // Act
            var condition = rule.Condition;
            var compiled = condition.Compile();

            // Assert - simple condition compiles and evaluates normally
            Assert.True(compiled(new AgentMessage { Type = MessageType.Request }));
            Assert.False(compiled(new AgentMessage { Type = MessageType.Alert }));
        }

        [Test]
        public void DependentRule_ContextCondition_ExposesOriginalExpression()
        {
            // Arrange
            var rule = new DependentRule<AgentMessage>(
                "test", "Test",
                (m, ctx) => ctx.Facts<Agent>().Any());

            // Act
            var contextCondition = rule.ContextCondition;

            // Assert - the original two-parameter expression is accessible
            Assert.NotNull(contextCondition);
            Assert.Equal(2, contextCondition!.Parameters.Count);
        }

        [Test]
        public void DependentRule_ContextCondition_IsNullForSimpleCondition()
        {
            // Arrange
            var rule = new DependentRule<AgentMessage>(
                "test", "Test",
                (AgentMessage m) => m.Type == MessageType.Request);

            // Act & Assert
            Assert.Null(rule.ContextCondition);
        }

        [Test]
        public void DependentRule_ProjectedCondition_IsSingleParameter()
        {
            // Arrange
            var rule = new DependentRule<AgentMessage>(
                "test", "Test",
                (m, ctx) => m.Type == MessageType.Request &&
                            ctx.Facts<Agent>().Any(a => a.Status == AgentStatus.Available));

            // Act
            var condition = rule.Condition;

            // Assert - projected to single-parameter lambda
            Assert.Equal(1, condition.Parameters.Count);
            Assert.Equal(typeof(AgentMessage), condition.Parameters[0].Type);
        }

        [Test]
        public void DependentRule_ProjectedCondition_ContainsFactQueryExpression()
        {
            // Arrange
            var rule = new DependentRule<AgentMessage>(
                "test", "Test",
                (m, ctx) => ctx.Facts<Agent>().Any(a => a.Status == AgentStatus.Available));

            // Act
            var condition = rule.Condition;

            // Assert - the projected expression contains FactQueryExpression for Agent
            var detector = new FactQueryExpressionFinder();
            detector.Visit(condition);
            Assert.True(detector.FoundTypes.Contains(typeof(Agent)),
                "Projected condition should contain FactQueryExpression for Agent");
        }

        [Test]
        public void DependentRule_ProjectedCondition_MultipleFactTypes()
        {
            // Arrange - rule that queries both Agent and Territory
            var rule = new DependentRule<AgentMessage>(
                "test", "Test",
                (m, ctx) => ctx.Facts<Agent>().Any(a => a.Status == AgentStatus.Available) &&
                            ctx.Facts<Territory>().Any(t => t.Value > 1000));

            // Act
            var condition = rule.Condition;

            // Assert - both fact types appear as FactQueryExpression
            var detector = new FactQueryExpressionFinder();
            detector.Visit(condition);
            Assert.True(detector.FoundTypes.Contains(typeof(Agent)),
                "Should contain FactQueryExpression for Agent");
            Assert.True(detector.FoundTypes.Contains(typeof(Territory)),
                "Should contain FactQueryExpression for Territory");
        }

        [Test]
        public void DependentRule_ProjectedCondition_WorksWithRewriter()
        {
            // Arrange
            using var context = new RulesContext();

            var rule = new DependentRule<AgentMessage>(
                "test", "Test",
                (m, ctx) => m.Type == MessageType.Request &&
                            ctx.Facts<Agent>().Any(a => a.Status == AgentStatus.Available));

            context.GetRuleSet<AgentMessage>().Add(rule);

            using var session = context.CreateSession();
            session.Insert(new Agent { Id = "A1", Status = AgentStatus.Available });

            var message = new AgentMessage { Id = "M1", Type = MessageType.Request };
            session.Insert(message);

            // Act - the projected Condition should be rewritable with FactQueryRewriter
            var projected = rule.Condition;
            var rewriter = new FactQueryRewriter(type =>
            {
                // Provide actual facts from session
                var factContext = (IFactContext)session;
                if (type == typeof(Agent))
                    return factContext.Facts<Agent>();
                return Enumerable.Empty<object>().AsQueryable();
            });

            var rewritten = (System.Linq.Expressions.Expression<Func<AgentMessage, bool>>)rewriter.Rewrite(projected);
            var compiled = rewritten.Compile();

            // Assert - should evaluate correctly after rewriting
            Assert.True(compiled(message));
        }

        /// <summary>
        /// Helper visitor that finds FactQueryExpression nodes and records their fact types.
        /// </summary>
        private class FactQueryExpressionFinder : System.Linq.Expressions.ExpressionVisitor
        {
            public HashSet<Type> FoundTypes { get; } = new();

            protected override System.Linq.Expressions.Expression VisitExtension(
                System.Linq.Expressions.Expression node)
            {
                if (node is FactQueryExpression fqe)
                {
                    FoundTypes.Add(fqe.FactType);
                }
                return base.VisitExtension(node);
            }
        }

        #endregion
    }
}
