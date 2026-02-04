namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using TestRunner.Framework;
    using RulesEngine.Linq;

    /// <summary>
    /// TDD tests for Agent-to-Agent communication rules using LINQ.
    ///
    /// These tests define the aspirational API for a unified rules engine
    /// that supports LINQ-based rule definition, querying, and evaluation
    /// in the context of agent communication systems.
    ///
    /// The tests are written first to drive the implementation.
    /// Many will not compile initially - that's intentional.
    /// </summary>
    public class AgentCommunicationRulesTests
    {
        #region Test Domain - Agent Communication Model

        public enum AgentRole
        {
            Soldier,
            Capo,
            Underboss,
            Consigliere,
            Godfather
        }

        public enum AgentStatus
        {
            Available,
            Busy,
            Offline,
            Compromised
        }

        public enum MessageType
        {
            Request,
            Response,
            Command,
            Alert,
            StatusReport,
            TerritoryRequest,
            Task,
            Broadcast
        }

        public class Agent
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public AgentRole Role { get; set; }
            public AgentStatus Status { get; set; }
            public string FamilyId { get; set; } = string.Empty;
            public string SuperiorId { get; set; } = string.Empty;
            public Agent? Superior { get; set; }
            public string CapoId { get; set; } = string.Empty;
            public int CurrentTaskCount { get; set; }
            public double ReputationScore { get; set; }
            public List<string> Capabilities { get; set; } = new();
            public int HierarchyLevel => Role switch
            {
                AgentRole.Soldier => 1,
                AgentRole.Capo => 2,
                AgentRole.Underboss => 3,
                AgentRole.Consigliere => 3,
                AgentRole.Godfather => 4,
                _ => 0
            };
        }

        public class AgentMessage
        {
            public string Id { get; set; } = string.Empty;
            public MessageType Type { get; set; }
            public Agent From { get; set; } = null!;
            public Agent To { get; set; } = null!;
            public object? Payload { get; set; }
            public DateTime Timestamp { get; set; }
            public string? Scope { get; set; }
            public bool Blocked { get; set; }
            public string? BlockReason { get; set; }
            public List<string> Flags { get; set; } = new();
            public List<string> RequiredCapabilities { get; set; } = new();

            public void Flag(string flag) => Flags.Add(flag);
            public void Reroute(Agent newTarget) => To = newTarget;
            public void RouteTo(Agent target) => To = target;
            public void EscalateTo(Agent target) => To = target;
        }

        public class Territory
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string ControlledBy { get; set; } = string.Empty;
        }

        #endregion

        #region 1. Message Routing Rules - Queryable

        [Test]
        public void Rules_CanBeQueriedByTagsAndPriority()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            // Add rules with metadata
            rules.Add(new Rule<AgentMessage>("escalate-alert", "Escalate alerts", m => m.Type == MessageType.Alert)
                .WithPriority(80)
                .WithTags("escalation", "alert-handling"));

            rules.Add(new Rule<AgentMessage>("escalate-repeated", "Escalate repeated issues", m => m.Flags.Contains("repeated"))
                .WithPriority(90)
                .WithTags("escalation", "pattern-detection"));

            rules.Add(new Rule<AgentMessage>("log-all", "Log all messages", m => true)
                .WithPriority(10)
                .WithTags("audit"));

            // Query rules like data
            var escalationRules = rules
                .Where(r => r.Tags.Contains("escalation"))
                .Where(r => r.Priority > 50)
                .OrderByDescending(r => r.Priority)
                .ToList();

            Assert.Equal(2, escalationRules.Count);
            Assert.Equal("escalate-repeated", escalationRules[0].Id);  // Higher priority first
            Assert.Equal("escalate-alert", escalationRules[1].Id);
        }

        #endregion

        #region 2. Permission Rules - Who Can Message Whom

        [Test]
        public void PermissionRule_BlocksDirectMessageToGodfather()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            rules.Add(new Rule<AgentMessage>(
                "no-soldier-to-godfather",
                "Soldiers cannot message Godfather directly",
                m => m.From.Role == AgentRole.Soldier && m.To.Role == AgentRole.Godfather)
                .Then(m => { m.Blocked = true; m.BlockReason = "Chain of command violation"; }));

            using var session = context.CreateSession();

            var soldier = new Agent { Id = "S1", Role = AgentRole.Soldier };
            var godfather = new Agent { Id = "GF", Role = AgentRole.Godfather };
            var message = new AgentMessage { From = soldier, To = godfather, Type = MessageType.Request };

            session.Insert(message);
            var results = session.Evaluate<AgentMessage>();

            Assert.Equal(1, results.FactsWithMatches.Count);
            Assert.True(results.FactsWithMatches[0].Blocked);
            Assert.Equal("Chain of command violation", results.FactsWithMatches[0].BlockReason);
        }

        [Test]
        public void PermissionRule_AllowsMessageToImmediateSuperior()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            rules.Add(new Rule<AgentMessage>(
                "no-soldier-to-godfather",
                "Soldiers cannot message Godfather directly",
                m => m.From.Role == AgentRole.Soldier && m.To.Role == AgentRole.Godfather)
                .Then(m => m.Blocked = true));

            using var session = context.CreateSession();

            var soldier = new Agent { Id = "S1", Role = AgentRole.Soldier };
            var capo = new Agent { Id = "C1", Role = AgentRole.Capo };
            var message = new AgentMessage { From = soldier, To = capo, Type = MessageType.Request };

            session.Insert(message);
            var results = session.Evaluate<AgentMessage>();

            // Message to capo should not be blocked
            Assert.Equal(0, results.FactsWithMatches.Count);
            Assert.Equal(1, results.FactsWithoutMatches.Count);
            Assert.False(results.FactsWithoutMatches[0].Blocked);
        }

        #endregion

        #region 3. Chain of Command Enforcement

        [Test]
        public void ChainOfCommand_ReroutesToImmediateSuperior()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            // Rule: Messages must go through chain of command
            rules.Add(new Rule<AgentMessage>(
                "chain-of-command",
                "Reroute to immediate superior if skipping levels",
                m => m.Type == MessageType.Request
                    && m.From.Superior != null
                    && m.To.Id != m.From.Superior.Id
                    && m.To.HierarchyLevel > m.From.HierarchyLevel)
                .Then(m => m.Reroute(m.From.Superior!)));

            using var session = context.CreateSession();

            var capo = new Agent { Id = "C1", Role = AgentRole.Capo };
            var soldier = new Agent { Id = "S1", Role = AgentRole.Soldier, Superior = capo, SuperiorId = "C1" };
            var godfather = new Agent { Id = "GF", Role = AgentRole.Godfather };

            var message = new AgentMessage
            {
                From = soldier,
                To = godfather,  // Trying to skip capo
                Type = MessageType.Request
            };

            session.Insert(message);
            session.Evaluate<AgentMessage>();

            // Should be rerouted to capo
            Assert.Equal("C1", message.To.Id);
        }

        #endregion

        #region 4. Cross-Fact Queries in Rule Conditions

        [Test]
        public void CrossFactQuery_FlagHostileTerritoryRequest()
        {
            using var context = new RulesContext();

            // This is the aspirational API - rules that query other fact types
            // The rule condition references context.Facts<Territory>()

            var messageRules = context.GetRuleSet<AgentMessage>();
            var territories = context.GetRuleSet<Territory>();

            // For now, we'll simulate with a closure that captures territory data
            var enemyTerritories = new List<string> { "T2", "T3" };

            messageRules.Add(new Rule<AgentMessage>(
                "hostile-territory-flag",
                "Flag requests for hostile territory",
                m => m.Type == MessageType.TerritoryRequest
                    && m.Payload is TerritoryRequestPayload trp
                    && enemyTerritories.Contains(trp.TerritoryId))
                .Then(m => m.Flag("hostile-territory-request")));

            using var session = context.CreateSession();

            var agent = new Agent { Id = "S1", Role = AgentRole.Soldier, FamilyId = "Family1" };
            var superior = new Agent { Id = "C1", Role = AgentRole.Capo };

            var hostileRequest = new AgentMessage
            {
                From = agent,
                To = superior,
                Type = MessageType.TerritoryRequest,
                Payload = new TerritoryRequestPayload { TerritoryId = "T2" }
            };

            var friendlyRequest = new AgentMessage
            {
                From = agent,
                To = superior,
                Type = MessageType.TerritoryRequest,
                Payload = new TerritoryRequestPayload { TerritoryId = "T1" }
            };

            session.Insert(hostileRequest);
            session.Insert(friendlyRequest);

            var results = session.Evaluate<AgentMessage>();

            Assert.Equal(1, results.FactsWithMatches.Count);
            Assert.Contains("hostile-territory-request", results.FactsWithMatches[0].Flags);
        }

        public class TerritoryRequestPayload
        {
            public string TerritoryId { get; set; } = string.Empty;
        }

        #endregion

        #region 5. Load Balancing Across Agent Pool

        [Test]
        public void LoadBalancing_RoutesToLeastBusyAgent()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            // Available soldiers for load balancing
            var soldiers = new List<Agent>
            {
                new Agent { Id = "S1", Role = AgentRole.Soldier, Status = AgentStatus.Available, CurrentTaskCount = 5 },
                new Agent { Id = "S2", Role = AgentRole.Soldier, Status = AgentStatus.Available, CurrentTaskCount = 2 },
                new Agent { Id = "S3", Role = AgentRole.Soldier, Status = AgentStatus.Busy, CurrentTaskCount = 1 },
            };

            rules.Add(new Rule<AgentMessage>(
                "load-balance-tasks",
                "Route tasks to least busy available soldier",
                m => m.Type == MessageType.Task)
                .Then(m => m.RouteTo(
                    soldiers
                        .Where(a => a.Status == AgentStatus.Available)
                        .OrderBy(a => a.CurrentTaskCount)
                        .First())));

            using var session = context.CreateSession();

            var capo = new Agent { Id = "C1", Role = AgentRole.Capo };
            var taskMessage = new AgentMessage
            {
                From = capo,
                To = soldiers[0],  // Initially targeted at S1
                Type = MessageType.Task
            };

            session.Insert(taskMessage);
            session.Evaluate<AgentMessage>();

            // Should be routed to S2 (least busy available)
            Assert.Equal("S2", taskMessage.To.Id);
        }

        #endregion

        #region 6. Escalation Based on Message History

        [Test]
        public void Escalation_DoubleEscalatesOnRepeatedAlerts()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            // Track recent alerts (simulating session history)
            var recentAlerts = new List<AgentMessage>();
            var alertThreshold = 3;
            var timeWindow = TimeSpan.FromMinutes(5);

            var underboss = new Agent { Id = "UB", Role = AgentRole.Underboss };
            var capo = new Agent { Id = "C1", Role = AgentRole.Capo, Superior = underboss };
            var soldier = new Agent { Id = "S1", Role = AgentRole.Soldier, Superior = capo };

            rules.Add(new Rule<AgentMessage>(
                "double-escalate-repeated-alerts",
                "Repeated alerts trigger double-escalation",
                m => m.Type == MessageType.Alert
                    && recentAlerts.Count(prev =>
                        prev.From.Id == m.From.Id
                        && prev.Timestamp > DateTime.UtcNow.AddMinutes(-5)) >= alertThreshold - 1)
                .Then(m => m.EscalateTo(m.From.Superior!.Superior!)));

            using var session = context.CreateSession();

            // Add historical alerts
            var now = DateTime.UtcNow;
            recentAlerts.Add(new AgentMessage { From = soldier, Type = MessageType.Alert, Timestamp = now.AddMinutes(-2) });
            recentAlerts.Add(new AgentMessage { From = soldier, Type = MessageType.Alert, Timestamp = now.AddMinutes(-1) });

            // New alert (third in 5 minutes)
            var newAlert = new AgentMessage
            {
                From = soldier,
                To = capo,
                Type = MessageType.Alert,
                Timestamp = now
            };

            session.Insert(newAlert);
            session.Evaluate<AgentMessage>();

            // Should be escalated to underboss (capo's superior)
            Assert.Equal("UB", newAlert.To.Id);
        }

        #endregion

        #region 7. Dynamic Routing Based on Capabilities

        [Test]
        public void CapabilityRouting_RoutesToCapableAgent()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            var agents = new List<Agent>
            {
                new Agent { Id = "S1", Capabilities = new List<string> { "surveillance", "driving" }, ReputationScore = 8.5 },
                new Agent { Id = "S2", Capabilities = new List<string> { "enforcement", "driving" }, ReputationScore = 9.0 },
                new Agent { Id = "S3", Capabilities = new List<string> { "surveillance", "tech", "driving" }, ReputationScore = 7.5 },
            };

            rules.Add(new Rule<AgentMessage>(
                "capability-routing",
                "Route to agent with required capabilities",
                m => m.RequiredCapabilities.Any())
                .Then(m =>
                {
                    var capable = agents
                        .Where(a => m.RequiredCapabilities.All(c => a.Capabilities.Contains(c)))
                        .OrderByDescending(a => a.ReputationScore)
                        .FirstOrDefault();
                    if (capable != null) m.RouteTo(capable);
                }));

            using var session = context.CreateSession();

            var message = new AgentMessage
            {
                From = new Agent { Id = "C1", Role = AgentRole.Capo },
                To = agents[0],
                Type = MessageType.Task,
                RequiredCapabilities = new List<string> { "surveillance", "tech" }
            };

            session.Insert(message);
            session.Evaluate<AgentMessage>();

            // Should route to S3 (only one with both surveillance and tech)
            Assert.Equal("S3", message.To.Id);
        }

        #endregion

        #region 8. Query Which Rules Would Match (Without Executing)

        [Test]
        public void WouldMatch_ReturnsMatchingRulesWithoutExecution()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            var actionsExecuted = new List<string>();

            rules.Add(new Rule<AgentMessage>("alert-rule", "Handle alerts", m => m.Type == MessageType.Alert)
                .Then(m => actionsExecuted.Add("alert-rule")));

            rules.Add(new Rule<AgentMessage>("high-priority", "High priority messages", m => m.From.Role == AgentRole.Capo)
                .Then(m => actionsExecuted.Add("high-priority")));

            rules.Add(new Rule<AgentMessage>("broadcast", "Broadcast messages", m => m.Type == MessageType.Broadcast)
                .Then(m => actionsExecuted.Add("broadcast")));

            var message = new AgentMessage
            {
                Type = MessageType.Alert,
                From = new Agent { Id = "C1", Role = AgentRole.Capo },
                To = new Agent { Id = "UB", Role = AgentRole.Underboss }
            };

            // Query which rules would match without executing actions
            var matchingRules = rules.WouldMatch(message).ToList();

            Assert.Equal(2, matchingRules.Count);
            Assert.True(matchingRules.Any(r => r.Id == "alert-rule"));
            Assert.True(matchingRules.Any(r => r.Id == "high-priority"));

            // Actions should NOT have been executed
            Assert.Equal(0, actionsExecuted.Count);
        }

        #endregion

        #region 9. Rule Composition - Combining Rules

        [Test]
        public void RuleComposition_AndCombinesTwoRules()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            var urgentRule = new Rule<AgentMessage>("urgent", "Urgent messages", m => m.Type == MessageType.Alert);
            var fromCapoRule = new Rule<AgentMessage>("from-capo", "From capo", m => m.From.Role == AgentRole.Capo);

            // Compose rules with And
            var combined = urgentRule.And(fromCapoRule)
                .WithId("urgent-from-capo")
                .WithName("Urgent messages from capos")
                .Then(m => m.Flag("priority-escalation"));

            rules.Add(combined);

            using var session = context.CreateSession();

            var urgentFromCapo = new AgentMessage
            {
                Type = MessageType.Alert,
                From = new Agent { Role = AgentRole.Capo },
                To = new Agent { Role = AgentRole.Underboss }
            };

            var urgentFromSoldier = new AgentMessage
            {
                Type = MessageType.Alert,
                From = new Agent { Role = AgentRole.Soldier },
                To = new Agent { Role = AgentRole.Capo }
            };

            session.Insert(urgentFromCapo);
            session.Insert(urgentFromSoldier);

            var results = session.Evaluate<AgentMessage>();

            Assert.Equal(1, results.FactsWithMatches.Count);
            Assert.Contains("priority-escalation", urgentFromCapo.Flags);
            Assert.DoesNotContain("priority-escalation", urgentFromSoldier.Flags);
        }

        [Test]
        public void RuleComposition_OrCombinesTwoRules()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            var alertRule = new Rule<AgentMessage>("alert", "Alerts", m => m.Type == MessageType.Alert);
            var commandRule = new Rule<AgentMessage>("command", "Commands", m => m.Type == MessageType.Command);

            // Compose rules with Or
            var combined = alertRule.Or(commandRule)
                .WithId("urgent-types")
                .WithName("Urgent message types")
                .Then(m => m.Flag("urgent"));

            rules.Add(combined);

            using var session = context.CreateSession();

            session.Insert(new AgentMessage { Type = MessageType.Alert, From = new Agent(), To = new Agent() });
            session.Insert(new AgentMessage { Type = MessageType.Command, From = new Agent(), To = new Agent() });
            session.Insert(new AgentMessage { Type = MessageType.StatusReport, From = new Agent(), To = new Agent() });

            var results = session.Evaluate<AgentMessage>();

            Assert.Equal(2, results.FactsWithMatches.Count);
        }

        #endregion

        #region 10. Rule Templates with Parameters

        [Test]
        public void RuleTemplate_CreateVariationsFromTemplate()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            // Template: Block messages from agents below a certain hierarchy level to a target role
            // This is aspirational - showing what a template API could look like

            // For now, simulate with a factory method
            Rule<AgentMessage> CreateBlockRule(AgentRole minSourceRole, AgentRole targetRole, string id)
            {
                return new Rule<AgentMessage>(
                    id,
                    $"Block {minSourceRole} and below from messaging {targetRole}",
                    m => m.From.HierarchyLevel < (int)minSourceRole && m.To.Role == targetRole)
                    .Then(m => m.Blocked = true);
            }

            rules.Add(CreateBlockRule(AgentRole.Capo, AgentRole.Godfather, "block-to-godfather"));
            rules.Add(CreateBlockRule(AgentRole.Underboss, AgentRole.Godfather, "strict-block-to-godfather"));

            Assert.Equal(2, rules.Count);

            // Verify rules have different thresholds
            var regularBlock = rules.Single(r => r.Id == "block-to-godfather");
            var strictBlock = rules.Single(r => r.Id == "strict-block-to-godfather");

            Assert.NotEqual(regularBlock.Name, strictBlock.Name);
        }

        #endregion

        #region 11. Audit Trail - Facts with Matching Rules

        [Test]
        public void AuditTrail_FactsWithMatchingRulesForAnalysis()
        {
            using var context = new RulesContext();
            var rules = context.GetRuleSet<AgentMessage>();

            rules.Add(new Rule<AgentMessage>("surveillance-trigger", "Triggers surveillance",
                m => m.Type == MessageType.TerritoryRequest)
                .WithTags("surveillance", "audit"));

            rules.Add(new Rule<AgentMessage>("high-value-trigger", "High value communication",
                m => m.From.Role >= AgentRole.Underboss)
                .WithTags("surveillance", "vip"));

            using var session = context.CreateSession();

            session.Insert(new AgentMessage
            {
                Id = "M1",
                Type = MessageType.TerritoryRequest,
                From = new Agent { Role = AgentRole.Underboss },
                To = new Agent { Role = AgentRole.Godfather }
            });

            session.Insert(new AgentMessage
            {
                Id = "M2",
                Type = MessageType.StatusReport,
                From = new Agent { Role = AgentRole.Soldier },
                To = new Agent { Role = AgentRole.Capo }
            });

            // Get facts with their matching rules for audit
            var auditLog = session.Facts<AgentMessage>()
                .WithMatchingRules()
                .Where(match => match.Rules.Any(r => r.Tags.Contains("surveillance")))
                .ToList();

            Assert.Equal(1, auditLog.Count);
            Assert.Equal("M1", auditLog[0].Fact.Id);
            Assert.Equal(2, auditLog[0].Rules.Count);  // Both rules match
        }

        #endregion

        #region 12. Pipeline-Style Rule Chaining

        [Test]
        public void Pipeline_ExecutesRulesInOrder()
        {
            using var context = new RulesContext();

            var executionOrder = new List<string>();

            // Aspirational: Pipeline API for ordered rule execution
            // For now, simulate with prioritized rules
            var rules = context.GetRuleSet<AgentMessage>();

            rules.Add(new Rule<AgentMessage>("step-1-validate", "Validate sender", m => m.From != null)
                .WithPriority(100)
                .Then(m => executionOrder.Add("validate")));

            rules.Add(new Rule<AgentMessage>("step-2-timestamp", "Add timestamp", m => true)
                .WithPriority(90)
                .Then(m => { m.Timestamp = DateTime.UtcNow; executionOrder.Add("timestamp"); }));

            rules.Add(new Rule<AgentMessage>("step-3-log", "Log message", m => true)
                .WithPriority(80)
                .Then(m => executionOrder.Add("log")));

            using var session = context.CreateSession();

            session.Insert(new AgentMessage
            {
                From = new Agent { Id = "S1" },
                To = new Agent { Id = "C1" }
            });

            session.Evaluate<AgentMessage>();

            // Rules should execute in priority order (highest first)
            Assert.Equal(3, executionOrder.Count);
            Assert.Equal("validate", executionOrder[0]);
            Assert.Equal("timestamp", executionOrder[1]);
            Assert.Equal("log", executionOrder[2]);
        }

        #endregion
    }
}
