// =============================================================================
// MafiaOperationsExample.cs — Comprehensive RulesEngine.Linq API Example
// =============================================================================
//
// This test class demonstrates the full RulesEngine.Linq API through the lens
// of a Mafia family's operations center. Each test method teaches a concept,
// building from simple to complex:
//
//   1. Territory assessment     — Simple rules, single fact type
//   2. Agent deployment         — Cross-fact queries via closure capture
//   3. Message routing          — DependentRule with explicit context
//   4. Chain of command         — Multiple rules, composition, priorities
//   5. Full operations cycle    — Schema, dependencies, evaluation ordering
//   6. Adapting to intel        — Re-evaluation after state changes
//
// The domain:
//   - Territory: Turf the family controls. Has heat (law enforcement attention),
//                revenue, and value. Rules flag risky territories and assess needs.
//   - Agent:     Family members from Soldier to Godfather. Have roles, status,
//                task counts. Rules assign them to territories and manage workload.
//   - AgentMessage: Communications between agents. Rules route messages through
//                   the chain of command, enforce protocol, and escalate alerts.
//
// WHY EXPRESSION TREES MATTER:
//   Every rule condition is an Expression<Func<T, bool>> — inspectable data,
//   not compiled code. Cross-fact references use FactQueryExpression markers,
//   not actual data. This means rules are:
//     - Serializable (send to a remote server for evaluation)
//     - Inspectable (analyze what a rule checks without running it)
//     - Rewritable  (substitute different data at evaluation time)
//
// =============================================================================

namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TestRunner.Framework;
    using RulesEngine.Linq;
    using RulesEngine.Linq.Dependencies;
    using Mafia.Domain;

    public class MafiaOperationsExample
    {
        // =====================================================================
        // THE WORLD: Corleone family state used across examples
        // =====================================================================

        private static Territory[] CreateTerritories() => new[]
        {
            new Territory { Id = "downtown",  Name = "Downtown",      Value = 50000, Revenue = 8000,  HeatLevel = 80, ControlledById = "corleone" },
            new Territory { Id = "docks",     Name = "The Docks",     Value = 30000, Revenue = 12000, HeatLevel = 30, ControlledById = "corleone" },
            new Territory { Id = "suburb",    Name = "West Suburbs",  Value = 20000, Revenue = 5000,  HeatLevel = 10, ControlledById = "corleone" },
            new Territory { Id = "airport",   Name = "Airport Strip", Value = 40000, Revenue = 15000, HeatLevel = 60, ControlledById = "corleone" },
            new Territory { Id = "rival-turf",Name = "East Side",     Value = 35000, Revenue = 0,     HeatLevel = 90, ControlledById = "barzini" },
        };

        private static Agent[] CreateAgents() => new[]
        {
            new Agent { Id = "vito",   Name = "Vito Corleone",  Role = AgentRole.Godfather,  Status = AgentStatus.Available, CurrentTaskCount = 0, FamilyId = "corleone", TerritoryId = "downtown" },
            new Agent { Id = "tom",    Name = "Tom Hagen",      Role = AgentRole.Consigliere,Status = AgentStatus.Available, CurrentTaskCount = 2, FamilyId = "corleone", TerritoryId = "downtown" },
            new Agent { Id = "sonny",  Name = "Sonny Corleone", Role = AgentRole.Underboss,  Status = AgentStatus.Available, CurrentTaskCount = 1, FamilyId = "corleone", TerritoryId = "downtown" },
            new Agent { Id = "clemenza",Name = "Peter Clemenza",Role = AgentRole.Capo,       Status = AgentStatus.Available, CurrentTaskCount = 3, FamilyId = "corleone", TerritoryId = "docks" },
            new Agent { Id = "tessio", Name = "Sal Tessio",     Role = AgentRole.Capo,       Status = AgentStatus.Busy,      CurrentTaskCount = 5, FamilyId = "corleone", TerritoryId = "airport" },
            new Agent { Id = "paulie", Name = "Paulie Gatto",   Role = AgentRole.Soldier,    Status = AgentStatus.Available, CurrentTaskCount = 1, FamilyId = "corleone", TerritoryId = "docks" },
            new Agent { Id = "rocco",  Name = "Rocco Lampone",  Role = AgentRole.Soldier,    Status = AgentStatus.Available, CurrentTaskCount = 0, FamilyId = "corleone", TerritoryId = "suburb" },
            new Agent { Id = "carlo",  Name = "Carlo Rizzi",    Role = AgentRole.Soldier,    Status = AgentStatus.Compromised,CurrentTaskCount = 0, FamilyId = "corleone", TerritoryId = "downtown" },
        };

        // =====================================================================
        // 1. TERRITORY ASSESSMENT — Simple rules, single fact type
        // =====================================================================
        //
        // The simplest use case: evaluate facts of one type against rules.
        // No cross-fact queries. Shows: RulesContext, Rule<T>, session lifecycle.

        [Test]
        public void Act1_TerritoryAssessment_SimpleRules()
        {
            // --- Setup: create context and define rules ---
            using var context = new RulesContext();

            // Get the rule set for Territory (created on first access)
            var rules = context.GetRuleSet<Territory>();

            // Rule 1: Flag high-heat territories
            rules.Add(new Rule<Territory>(
                "heat-warning",
                "Flag territories with dangerous heat levels",
                t => t.HeatLevel > 70)
                .WithPriority(100)
                .WithTags("risk", "heat")
                .Then(t => t.Status = "high-heat"));

            // Rule 2: Identify profitable territories (using fluent builder)
            rules.Add(new Rule<Territory>(
                "high-earner",
                "Flag territories earning above threshold",
                t => t.Revenue > 10000)
                .WithPriority(50)
                .WithTags("financial")
                .Then(t =>
                {
                    // Actions can do anything — mutate the fact, log, trigger side effects.
                    // Here we append to status to show multiple rules can fire on the same fact.
                    t.Status = t.Status != null ? t.Status + ",high-earner" : "high-earner";
                }));

            // Rule 3: Flag enemy territory
            rules.Add(new Rule<Territory>(
                "enemy-turf",
                "Not our territory",
                t => t.ControlledById != "corleone")
                .WithPriority(200)
                .Then(t => t.Status = "enemy"));

            // --- Evaluate: create session, insert facts, run rules ---
            using var session = context.CreateSession();
            var territories = CreateTerritories();
            session.InsertAll(territories);

            // Evaluate<T>() runs all Territory rules against all Territory facts
            var result = session.Evaluate<Territory>();

            // --- Inspect results ---

            // Downtown: heat 80 → "high-heat" (heat-warning matched)
            var downtown = territories.First(t => t.Id == "downtown");
            Assert.Equal("high-heat", downtown.Status);

            // Docks: heat 30, revenue 12000 → "high-earner" (high-earner matched)
            var docks = territories.First(t => t.Id == "docks");
            Assert.Equal("high-earner", docks.Status);

            // Airport: heat 60, revenue 15000 → "high-earner" (heat-warning didn't fire, high-earner did)
            var airport = territories.First(t => t.Id == "airport");
            Assert.Equal("high-earner", airport.Status);

            // Suburb: heat 10, revenue 5000 → no rules matched, status unchanged
            var suburb = territories.First(t => t.Id == "suburb");
            Assert.Null(suburb.Status);

            // East Side: enemy territory → "enemy" (highest priority rule wins first)
            // Note: enemy-turf has priority 200, so it runs first.
            // Then heat-warning (priority 100) runs and overwrites to "high-heat".
            // Then high-earner would check but revenue is 0.
            // Rules execute in priority order (highest first), each mutating the fact.
            var eastSide = territories.First(t => t.Id == "rival-turf");
            Assert.Equal("high-heat", eastSide.Status);

            // Result metadata
            Assert.Equal(5, result.FactsWithMatches.Count + result.FactsWithoutMatches.Count);
            Assert.Equal(4, result.FactsWithMatches.Count);  // 4 territories matched at least one rule
            Assert.Equal(1, result.FactsWithoutMatches.Count); // suburb matched nothing
        }

        // =====================================================================
        // 2. AGENT DEPLOYMENT — Cross-fact queries via closure capture
        // =====================================================================
        //
        // Rules that reference other fact types. Here, Agent rules query
        // Territory facts to make assignment decisions. The closure capture
        // pattern: context.Facts<T>() returns a FactQueryable whose expression
        // tree contains FactQueryExpression — a serializable marker node.
        //
        // At evaluation time, the rewriter substitutes actual session data.

        [Test]
        public void Act2_AgentDeployment_ClosureCapturedCrossFactQueries()
        {
            using var context = new RulesContext();

            // Capture cross-fact queryable BEFORE defining rules.
            // This returns FactQueryable<Territory>, NOT actual data.
            // Its Expression property is FactQueryExpression(typeof(Territory)).
            var territories = context.Facts<Territory>();

            // Rule: Soldiers in high-heat territories should be flagged for reassignment.
            // The lambda captures `territories` — at evaluation time, the rewriter
            // substitutes actual Territory facts from the session.
            context.GetRuleSet<Agent>().Add(new Rule<Agent>(
                "heat-reassign",
                "Reassign soldiers from high-heat zones",
                a => a.Role == AgentRole.Soldier
                     && a.Status != AgentStatus.Compromised
                     && territories.Any(t => t.Id == a.TerritoryId && t.HeatLevel > 70))
                .WithPriority(100)
                .Then(a => a.Capabilities.Add("needs-reassignment")));

            // Rule: Idle soldiers (low task count) in safe territories are deployment-ready.
            context.GetRuleSet<Agent>().Add(new Rule<Agent>(
                "deployment-ready",
                "Mark idle soldiers in safe zones as deployment-ready",
                a => a.Role == AgentRole.Soldier
                     && a.Status == AgentStatus.Available
                     && a.CurrentTaskCount < 2
                     && territories.Any(t => t.Id == a.TerritoryId && t.HeatLevel < 50))
                .WithPriority(50)
                .Then(a => a.Capabilities.Add("deployment-ready")));

            // --- Evaluate ---
            using var session = context.CreateSession();
            session.InsertAll(CreateTerritories());

            var agents = CreateAgents();
            session.InsertAll(agents);

            var result = session.Evaluate<Agent>();

            // Paulie is a soldier at the docks (heat 30), task count 1
            // → deployment-ready (safe zone, low tasks)
            var paulie = agents.First(a => a.Id == "paulie");
            Assert.True(paulie.Capabilities.Contains("deployment-ready"));
            Assert.False(paulie.Capabilities.Contains("needs-reassignment"));

            // Rocco is a soldier in suburbs (heat 10), task count 0
            // → deployment-ready
            var rocco = agents.First(a => a.Id == "rocco");
            Assert.True(rocco.Capabilities.Contains("deployment-ready"));

            // Carlo is a soldier in downtown (heat 80) but Status is Compromised
            // → neither rule fires (compromised excluded from heat-reassign,
            //   and not Available for deployment-ready)
            var carlo = agents.First(a => a.Id == "carlo");
            Assert.False(carlo.Capabilities.Contains("needs-reassignment"));
            Assert.False(carlo.Capabilities.Contains("deployment-ready"));

            // Vito/Tom/Sonny/Clemenza/Tessio are not soldiers → no rules fire
            var vito = agents.First(a => a.Id == "vito");
            Assert.Equal(0, vito.Capabilities.Count);
        }

        // =====================================================================
        // 3. MESSAGE ROUTING — DependentRule with explicit context parameter
        // =====================================================================
        //
        // The second cross-fact pattern: DependentRule<T> receives an IFactContext
        // parameter. The condition is (fact, ctx) => ... and can call ctx.Facts<T>()
        // for on-demand queries. Advantage: explicit dependency declaration via
        // .DependsOn<T>(), and the context is available in both condition and action.

        [Test]
        public void Act3_MessageRouting_DependentRuleWithExplicitContext()
        {
            using var context = new RulesContext();

            // DependentRule: route requests to the least-busy available soldier.
            // The (msg, ctx) lambda receives the session as IFactContext at evaluation time.
            context.GetRuleSet<AgentMessage>().Add(new DependentRule<AgentMessage>(
                "route-to-soldier",
                "Route request to least-busy available soldier",
                (msg, ctx) => msg.Type == MessageType.Request
                              && ctx.Facts<Agent>().Any(a => a.Role == AgentRole.Soldier
                                                            && a.Status == AgentStatus.Available))
                .DependsOn<Agent>()
                .WithPriority(100)
                .Then((msg, ctx) =>
                {
                    // Context is available in the action too — query live session data.
                    var target = ctx.Facts<Agent>()
                        .Where(a => a.Role == AgentRole.Soldier && a.Status == AgentStatus.Available)
                        .OrderBy(a => a.CurrentTaskCount)
                        .First();
                    msg.RouteTo(target);
                    msg.Flag("routed");
                }));

            // DependentRule: escalate alerts to the underboss.
            context.GetRuleSet<AgentMessage>().Add(new DependentRule<AgentMessage>(
                "escalate-alert",
                "Escalate alerts to underboss",
                (msg, ctx) => msg.Type == MessageType.Alert
                              && ctx.Facts<Agent>().Any(a => a.Role == AgentRole.Underboss
                                                            && a.Status == AgentStatus.Available))
                .DependsOn<Agent>()
                .WithPriority(200)
                .Then((msg, ctx) =>
                {
                    var underboss = ctx.Facts<Agent>()
                        .First(a => a.Role == AgentRole.Underboss && a.Status == AgentStatus.Available);
                    msg.EscalateTo(underboss);
                    msg.Flag("escalated");
                }));

            // --- Evaluate ---
            using var session = context.CreateSession();
            session.InsertAll(CreateAgents());

            var request = new AgentMessage { Id = "msg-1", Type = MessageType.Request, FromId = "clemenza" };
            var alert   = new AgentMessage { Id = "msg-2", Type = MessageType.Alert,   FromId = "tessio" };
            var report  = new AgentMessage { Id = "msg-3", Type = MessageType.Report,  FromId = "paulie" };
            session.InsertAll(new[] { request, alert, report });

            var result = session.Evaluate<AgentMessage>();

            // Request → routed to Rocco (task count 0, lowest among available soldiers)
            Assert.Equal("rocco", request.ToId);
            Assert.True(request.Flags.Contains("routed"));

            // Alert → escalated to Sonny (the underboss)
            Assert.Equal("sonny", alert.EscalatedToId);
            Assert.True(alert.Flags.Contains("escalated"));

            // Report → no rules matched (no rule handles Report type)
            Assert.False(report.Flags.Contains("routed"));
            Assert.False(report.Flags.Contains("escalated"));

            // Result metadata
            Assert.Equal(2, result.FactsWithMatches.Count);
            Assert.Equal(1, result.FactsWithoutMatches.Count);
        }

        // =====================================================================
        // 4. CHAIN OF COMMAND — Rule composition, priorities, mixed rule types
        // =====================================================================
        //
        // Real operations need layered rules: protocol enforcement first,
        // then routing, then logging. Shows: rule priorities control ordering,
        // Rule.And()/Or() for composition, both rule types mixed together.

        [Test]
        public void Act4_ChainOfCommand_CompositionAndPriorities()
        {
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            // LAYER 1 (Priority 300): Protocol enforcement — block violations
            // Soldiers cannot message the Godfather directly.
            context.GetRuleSet<AgentMessage>().Add(new Rule<AgentMessage>(
                "no-skip-chain",
                "Block soldiers messaging Godfather directly",
                msg => msg.Type == MessageType.Request
                       && agents.Any(a => a.Id == msg.FromId && a.Role == AgentRole.Soldier)
                       && agents.Any(a => a.Id == msg.ToId && a.Role == AgentRole.Godfather))
                .WithPriority(300)
                .WithTags("protocol", "security")
                .Then(msg => msg.Block("Chain of command violation: soldiers report to capos")));

            // LAYER 2 (Priority 200): Reroute blocked messages to sender's capo
            context.GetRuleSet<AgentMessage>().Add(new DependentRule<AgentMessage>(
                "reroute-to-capo",
                "Reroute blocked messages through chain of command",
                (msg, ctx) => msg.Blocked
                              && ctx.Facts<Agent>().Any(a => a.Id == msg.FromId && a.CapoId != null))
                .DependsOn<Agent>()
                .WithPriority(200)
                .Then((msg, ctx) =>
                {
                    var sender = ctx.Facts<Agent>().First(a => a.Id == msg.FromId);
                    var capo = ctx.Facts<Agent>().FirstOrDefault(a => a.Id == sender.CapoId);
                    if (capo != null)
                    {
                        msg.Reroute(capo);
                        msg.Flag("rerouted-to-capo");
                    }
                }));

            // LAYER 3 (Priority 100): Compose rules for special handling
            // High-priority alerts from compromised agents need immediate attention.
            var fromCompromised = new Rule<AgentMessage>(
                "from-compromised",
                "Message from compromised agent",
                msg => agents.Any(a => a.Id == msg.FromId && a.Status == AgentStatus.Compromised));

            var isAlert = new Rule<AgentMessage>(
                "is-alert",
                "Message is an alert",
                msg => msg.Type == MessageType.Alert);

            // Compose with And() — both conditions must be true
            var compromisedAlert = fromCompromised.And(isAlert)
                .WithPriority(100)
                .Then(msg => msg.Flag("compromised-source-alert"));

            context.GetRuleSet<AgentMessage>().Add(compromisedAlert);

            // --- Set up agents with chain of command ---
            var agentList = CreateAgents();
            // Set Paulie's capo to Clemenza
            agentList.First(a => a.Id == "paulie").CapoId = "clemenza";

            using var session = context.CreateSession();
            session.InsertAll(agentList);

            // Paulie (soldier) tries to message Vito (godfather) directly
            var violation = new AgentMessage
            {
                Id = "msg-violation", Type = MessageType.Request,
                FromId = "paulie", ToId = "vito"
            };

            // Carlo (compromised) sends an alert
            var compromisedMsg = new AgentMessage
            {
                Id = "msg-compromised", Type = MessageType.Alert,
                FromId = "carlo"
            };

            session.InsertAll(new[] { violation, compromisedMsg });

            var result = session.Evaluate<AgentMessage>();

            // Paulie's message: blocked (priority 300), then rerouted to Clemenza (priority 200)
            Assert.True(violation.Blocked);
            Assert.Equal("Chain of command violation: soldiers report to capos", violation.BlockReason);
            Assert.Equal("clemenza", violation.ReroutedToId);
            Assert.True(violation.Flags.Contains("rerouted-to-capo"));

            // Carlo's alert: flagged as compromised source (composed rule matched)
            Assert.True(compromisedMsg.Flags.Contains("compromised-source-alert"));

            // Verify the composed rule's ID shows its lineage
            Assert.Equal("from-compromised_AND_is-alert", compromisedAlert.Id);
        }

        // =====================================================================
        // 5. FULL OPERATIONS — Schema, dependencies, evaluation ordering
        // =====================================================================
        //
        // The full picture: schema defines fact types and their dependencies.
        // DependencyGraph provides topological sort for evaluation ordering.
        // session.Evaluate() evaluates ALL fact types in dependency order.
        //
        // This is the "production" pattern: Territory rules fire first (no deps),
        // then Agent rules (depend on Territory results), then Message rules
        // (depend on Agent state after Agent rules fired).

        [Test]
        public void Act5_FullOperations_SchemaAndDependencyOrdering()
        {
            using var context = new RulesContext();

            // --- Define schema: fact types and their relationships ---
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Territory>();
                schema.RegisterFactType<Agent>(cfg => cfg.DependsOn<Territory>());
                schema.RegisterFactType<AgentMessage>(cfg => cfg.DependsOn<Agent>());
            });

            // Verify dependency graph was built
            var graph = context.DependencyGraph;
            Assert.NotNull(graph);

            var loadOrder = graph!.GetLoadOrder();
            Assert.True(loadOrder.IndexOf(typeof(Territory)) < loadOrder.IndexOf(typeof(Agent)),
                "Territory should evaluate before Agent");
            Assert.True(loadOrder.IndexOf(typeof(Agent)) < loadOrder.IndexOf(typeof(AgentMessage)),
                "Agent should evaluate before AgentMessage");

            // --- Territory rules (no dependencies) ---
            context.GetRuleSet<Territory>().Add(new Rule<Territory>(
                "assess-heat",
                "Flag hot territories",
                t => t.HeatLevel > 70 && t.ControlledById == "corleone")
                .Then(t => t.Status = "high-heat"));

            // --- Agent rules (depend on Territory — territories evaluated first) ---
            // Use DependentRule to query territories that were just assessed.
            context.GetRuleSet<Agent>().Add(new DependentRule<Agent>(
                "soldier-in-hot-zone",
                "Flag soldiers assigned to high-heat territories",
                (a, ctx) => a.Role == AgentRole.Soldier
                            && a.Status == AgentStatus.Available
                            && ctx.Facts<Territory>().Any(t => t.Id == a.TerritoryId
                                                               && t.Status == "high-heat"))
                .DependsOn<Territory>()
                .Then(a => a.Capabilities.Add("in-danger-zone")));

            // --- Message rules (depend on Agent — agents evaluated first) ---
            // Use DependentRule to query agent state AFTER agent rules fired.
            // HashSet.Contains() works naturally in cross-fact predicates.
            context.GetRuleSet<AgentMessage>().Add(new DependentRule<AgentMessage>(
                "hot-zone-request",
                "Flag requests to agents in hot zones",
                (msg, ctx) => msg.Type == MessageType.Request
                              && ctx.Facts<Agent>().Any(a => a.Id == msg.ToId
                                                             && a.Capabilities.Contains("in-danger-zone")))
                .DependsOn<Agent>()
                .Then(msg => msg.Flag("destination-in-hot-zone")));

            // --- Create session with all facts ---
            using var session = context.CreateSession();
            var territories = CreateTerritories();
            var agents = CreateAgents();
            session.InsertAll(territories);
            session.InsertAll(agents);

            // A message addressed to a soldier in a hot zone
            // (We won't know it's hot until Territory rules run and set Status)
            var msg = new AgentMessage
            {
                Id = "ops-msg", Type = MessageType.Request,
                FromId = "clemenza", ToId = "paulie"  // Paulie is at the docks (heat 30, NOT hot)
            };
            session.Insert(msg);

            // --- Evaluate ALL fact types in dependency order ---
            var result = session.Evaluate();

            // Check for errors first to diagnose any evaluation failures
            if (result.HasErrors)
            {
                var errorDetails = string.Join("; ", result.Errors.Select(e =>
                    $"Rule '{e.RuleId}' on {e.Fact}: {e.Exception.Message}"));
                Assert.False(result.HasErrors, $"Unexpected errors: {errorDetails}");
            }

            // Verify evaluation order worked:
            // 1. Territory rules ran: downtown (heat 80) flagged as "high-heat"
            Assert.Equal("high-heat", territories.First(t => t.Id == "downtown").Status);

            // 2. Agent rules ran AFTER territories: no soldiers in downtown are available
            //    (Carlo is compromised, Vito/Tom/Sonny aren't soldiers)
            //    Paulie is at docks (heat 30, no "high-heat" status) → not flagged
            var paulie = agents.First(a => a.Id == "paulie");
            Assert.False(paulie.Capabilities.Contains("in-danger-zone"));

            // 3. Message rules ran AFTER agents: Paulie not in hot zone → message not flagged
            Assert.False(msg.Flags.Contains("destination-in-hot-zone"));

            // Verify overall result
            Assert.True(result.TotalFactsEvaluated > 0);
            Assert.True(result.TotalRulesEvaluated > 0);
        }

        // =====================================================================
        // 6. ADAPTING TO INTEL — Re-evaluation after state changes
        // =====================================================================
        //
        // Real operations aren't one-shot. New intel arrives, agents get
        // reassigned, facts change. Sessions support re-evaluation: insert
        // new facts and evaluate again. Caches are invalidated automatically.

        [Test]
        public void Act6_AdaptingToIntel_ReEvaluationAfterStateChanges()
        {
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            // Rule: route requests to available soldiers
            context.GetRuleSet<AgentMessage>().Add(new Rule<AgentMessage>(
                "route-to-available",
                "Route to any available soldier",
                msg => msg.Type == MessageType.Request
                       && agents.Any(a => a.Role == AgentRole.Soldier
                                         && a.Status == AgentStatus.Available))
                .Then(msg => msg.Flag("soldier-available")));

            using var session = context.CreateSession();

            // Initially: no agents in session. Insert a request.
            var msg = new AgentMessage { Id = "urgent-1", Type = MessageType.Request };
            session.Insert(msg);

            // First evaluation: no soldiers → rule doesn't match
            var result1 = session.Evaluate<AgentMessage>();
            Assert.Equal(0, result1.Matches.Count);
            Assert.False(msg.Flags.Contains("soldier-available"));

            // Intel arrives: Rocco is available
            session.Insert(new Agent
            {
                Id = "rocco", Name = "Rocco Lampone",
                Role = AgentRole.Soldier, Status = AgentStatus.Available
            });

            // Re-evaluate: same message, now a soldier exists
            // The rewriter re-resolves Facts<Agent>() with the updated session data.
            // Stale compiled delegates are cleared automatically.
            var result2 = session.Evaluate<AgentMessage>();
            Assert.Equal(1, result2.Matches.Count);
            Assert.True(msg.Flags.Contains("soldier-available"));
        }

        // =====================================================================
        // 7. PREVIEW & INSPECTION — WouldMatch, rule metadata, results
        // =====================================================================
        //
        // Before committing to an operation, preview which rules would fire.
        // Inspect rule metadata (tags, priorities). Examine detailed results.

        [Test]
        public void Act7_PreviewAndInspection_WouldMatchAndMetadata()
        {
            using var context = new RulesContext();

            var rules = context.GetRuleSet<Territory>();

            rules.Add(new Rule<Territory>(
                "high-value", "High value target",
                t => t.Value > 35000)
                .WithPriority(100)
                .WithTags("financial", "priority"));

            rules.Add(new Rule<Territory>(
                "safe-zone", "Low heat safe zone",
                t => t.HeatLevel < 20)
                .WithPriority(50)
                .WithTags("safety", "operational"));

            rules.Add(new Rule<Territory>(
                "cash-cow", "High revenue territory",
                t => t.Revenue > 10000)
                .WithPriority(75)
                .WithTags("financial", "revenue"));

            // --- WouldMatch: preview without executing actions ---
            var suburb = new Territory
            {
                Id = "suburb", Name = "West Suburbs",
                Value = 20000, Revenue = 5000, HeatLevel = 10
            };

            var matching = rules.WouldMatch(suburb).ToList();

            // Suburb matches "safe-zone" (heat 10 < 20) but not the others
            Assert.Equal(1, matching.Count);
            Assert.Equal("safe-zone", matching[0].Id);

            // --- Query rules by metadata (IRuleSet is IQueryable) ---
            var financialRules = rules.Where(r => r.Tags.Contains("financial")).ToList();
            Assert.Equal(2, financialRules.Count);

            var highPriorityRules = rules.Where(r => r.Priority >= 75).ToList();
            Assert.Equal(2, highPriorityRules.Count);

            // --- Find specific rule by ID ---
            var found = rules.FindById("cash-cow");
            Assert.NotNull(found);
            Assert.Equal("High revenue territory", found!.Name);

            // --- Detailed evaluation results ---
            using var session = context.CreateSession();
            var airport = new Territory
            {
                Id = "airport", Name = "Airport Strip",
                Value = 40000, Revenue = 15000, HeatLevel = 60
            };
            session.Insert(airport);

            var result = session.Evaluate<Territory>();

            // Airport matches "high-value" (40000 > 35000) and "cash-cow" (15000 > 10000)
            Assert.Equal(1, result.FactsWithMatches.Count);
            Assert.Equal(0, result.FactsWithoutMatches.Count);

            var match = result.Matches[0];
            Assert.Equal(2, match.MatchedRules.Count);

            // MatchCountByRule shows per-rule hit counts across all facts
            Assert.Equal(1, result.MatchCountByRule["high-value"]);
            Assert.Equal(1, result.MatchCountByRule["cash-cow"]);
        }

        // =====================================================================
        // 8. ERROR HANDLING — Resilient evaluation
        // =====================================================================
        //
        // Rules can fail. The engine captures errors without stopping evaluation.
        // Other rules continue to fire. Errors are collected in the result.
        //
        // NOTE: DependentRule exceptions propagate to the error handler.
        // Rule<T> has internal try-catch that returns false — its errors don't
        // reach the session. Use DependentRule when error visibility matters.

        [Test]
        public void Act8_ErrorHandling_ResilientEvaluation()
        {
            using var context = new RulesContext();

            // A DependentRule that matches but whose action throws.
            // The condition must be translatable (provider validates at Add() time),
            // so the "bug" lives in the action, not the condition. This simulates
            // a rule that evaluates correctly but fails during execution — e.g.,
            // a database write, an external API call, or a state mutation that
            // encounters unexpected data.
            context.GetRuleSet<AgentMessage>().Add(new DependentRule<AgentMessage>(
                "broken-rule",
                "This rule has a bug",
                (msg, ctx) => true)
                .Then(msg => BrokenAction())
                .WithPriority(200));

            // A working rule that should still fire
            context.GetRuleSet<AgentMessage>().Add(new Rule<AgentMessage>(
                "always-log",
                "Log all messages",
                msg => true)
                .WithPriority(50)
                .Then(msg => msg.Flag("logged")));

            using var session = context.CreateSession();
            var msg = new AgentMessage { Id = "msg-1", Type = MessageType.Request };
            session.Insert(msg);

            // Evaluate() catches the error and continues
            var result = session.Evaluate();

            // The broken rule produced an error
            Assert.True(result.HasErrors);
            Assert.Equal(1, result.Errors.Count);
            Assert.Equal("broken-rule", result.Errors[0].RuleId);
            Assert.Contains("Database connection lost", result.Errors[0].Exception.Message);

            // The working rule still fired
            Assert.True(msg.Flags.Contains("logged"));
            Assert.Equal(1, result.TotalMatches);
        }

        // =====================================================================
        // 9. THE BIG PICTURE — Full operation with all features
        // =====================================================================
        //
        // Putting it all together: schema, dependencies, both cross-fact patterns,
        // composition, priorities, error handling, re-evaluation — one scenario
        // that exercises the entire API surface.
        //
        // Scenario: The Corleone family receives intelligence that downtown
        // heat is rising. The operations center evaluates all territories,
        // reassigns agents, and routes incoming messages accordingly.

        [Test]
        public void Act9_TheBigPicture_FullOperationAllFeatures()
        {
            // === SETUP: Schema with dependency chain ===
            using var context = new RulesContext();

            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Territory>();
                schema.RegisterFactType<Agent>(cfg => cfg.DependsOn<Territory>());
                schema.RegisterFactType<AgentMessage>(cfg => cfg.DependsOn<Agent>());
            });

            // === TERRITORY RULES ===
            var territoryRules = context.GetRuleSet<Territory>();

            territoryRules.Add(new Rule<Territory>(
                "assess-risk",
                "Assess territory risk level",
                t => t.ControlledById == "corleone" && t.HeatLevel > 60)
                .WithPriority(100)
                .Then(t => t.Status = "at-risk"));

            territoryRules.Add(new Rule<Territory>(
                "safe-ops",
                "Mark safe operational territories",
                t => t.ControlledById == "corleone" && t.HeatLevel <= 30 && t.Revenue > 3000)
                .WithPriority(50)
                .Then(t => t.Status = "safe-ops"));

            // === AGENT RULES (depend on Territory results) ===
            var agentRules = context.GetRuleSet<Agent>();

            // DependentRule: reassign soldiers from at-risk territories
            agentRules.Add(new DependentRule<Agent>(
                "evacuate-at-risk",
                "Reassign soldiers from at-risk territories",
                (a, ctx) => a.Role == AgentRole.Soldier
                            && a.Status == AgentStatus.Available
                            && ctx.Facts<Territory>().Any(t => t.Id == a.TerritoryId
                                                               && t.Status == "at-risk"))
                .DependsOn<Territory>()
                .WithPriority(100)
                .Then((a, ctx) =>
                {
                    // Find a safe territory to reassign to
                    var safe = ctx.Facts<Territory>()
                        .FirstOrDefault(t => t.Status == "safe-ops");
                    if (safe != null)
                    {
                        a.TerritoryId = safe.Id;
                        a.Capabilities.Add("reassigned");
                    }
                }));

            // Closure capture: mark agents with available capacity.
            var allTerritories = context.Facts<Territory>();
            agentRules.Add(new Rule<Agent>(
                "capacity-check",
                "Flag agents with available capacity in safe zones",
                a => a.Status == AgentStatus.Available
                     && a.CurrentTaskCount < 3
                     && allTerritories.Any(t => t.Id == a.TerritoryId
                                                && (t.Status == "safe-ops" || t.HeatLevel <= 30)))
                .WithPriority(50)
                .Then(a => a.Capabilities.Add("deployment-ready")));

            // === MESSAGE RULES (depend on Agent results) ===
            var messageRules = context.GetRuleSet<AgentMessage>();

            // Route requests to agents with capacity (set by agent rules above).
            // HashSet.Contains() works naturally in cross-fact predicates.
            messageRules.Add(new DependentRule<AgentMessage>(
                "smart-route",
                "Route to agents with available capacity",
                (msg, ctx) => msg.Type == MessageType.Request
                              && !msg.Blocked
                              && ctx.Facts<Agent>().Any(a => a.Capabilities.Contains("deployment-ready")))
                .DependsOn<Agent>()
                .WithPriority(100)
                .Then((msg, ctx) =>
                {
                    var target = ctx.Facts<Agent>()
                        .Where(a => a.Capabilities.Contains("deployment-ready"))
                        .OrderBy(a => a.CurrentTaskCount)
                        .First();
                    msg.RouteTo(target);
                    msg.Flag("smart-routed");
                }));

            // === LOAD FACTS ===
            using var session = context.CreateSession();
            var territories = CreateTerritories();
            var agents = CreateAgents();
            session.InsertAll(territories);
            session.InsertAll(agents);

            var request = new AgentMessage
            {
                Id = "ops-request", Type = MessageType.Request,
                FromId = "clemenza"
            };
            session.Insert(request);

            // === EVALUATE (respects dependency order) ===
            var result = session.Evaluate();

            // Check for errors first to diagnose any evaluation failures
            if (result.HasErrors)
            {
                var errorDetails = string.Join("; ", result.Errors.Select(e =>
                    $"Rule '{e.RuleId}' on {e.Fact}: {e.Exception.Message}"));
                Assert.False(result.HasErrors, $"Unexpected errors: {errorDetails}");
            }

            // === VERIFY THE CASCADE ===

            // Step 1: Territory rules fired first
            var downtown = territories.First(t => t.Id == "downtown");
            Assert.Equal("at-risk", downtown.Status); // Heat 80 > 60

            var docks = territories.First(t => t.Id == "docks");
            Assert.Equal("safe-ops", docks.Status); // Heat 30, Revenue 12000

            var suburb = territories.First(t => t.Id == "suburb");
            Assert.Equal("safe-ops", suburb.Status); // Heat 10, Revenue 5000

            // Step 2: Agent rules fired second (using territory results)
            // No soldiers in downtown are available (Carlo is compromised),
            // so evacuate-at-risk doesn't fire for anyone.

            // Paulie: docks (safe-ops), task count 1 < 3 → deployment-ready
            var paulie = agents.First(a => a.Id == "paulie");
            Assert.True(paulie.Capabilities.Contains("deployment-ready"));

            // Rocco: suburb (safe-ops), task count 0 < 3 → deployment-ready
            var rocco = agents.First(a => a.Id == "rocco");
            Assert.True(rocco.Capabilities.Contains("deployment-ready"));

            // Clemenza: docks (safe-ops), but task count 3 (not < 3) → not deployment-ready
            var clemenza = agents.First(a => a.Id == "clemenza");
            Assert.False(clemenza.Capabilities.Contains("deployment-ready"));

            // Step 3: Message rules fired last (using agent results)
            // Request routed to agent with capacity, lowest task count → Rocco (0 tasks)
            Assert.Equal("rocco", request.ToId);
            Assert.True(request.Flags.Contains("smart-routed"));

            // Overall health check
            Assert.False(result.HasErrors);
            Assert.True(result.TotalMatches > 0);
            Assert.True(result.TotalRulesEvaluated > 0);
        }

        /// <summary>
        /// Simulates a broken action (e.g., database write failure, API error).
        /// Used in Act 8 to demonstrate resilient evaluation — the session captures
        /// the error and continues evaluating remaining rules.
        /// </summary>
        private static void BrokenAction()
        {
            throw new InvalidOperationException("Database connection lost");
        }

        // =====================================================================
        // 10. WHY EXPRESSION TREES — The serialization story
        // =====================================================================
        //
        // This test doesn't prove serialization (not yet implemented).
        // It proves the ARCHITECTURE supports it: rule conditions are
        // expression trees with FactQueryExpression markers, not compiled
        // delegates baked to local data. They're inspectable data.

        [Test]
        public void Act10_WhyExpressionTrees_InspectableSerializableRules()
        {
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            // Create a rule with a cross-fact closure
            var rule = new Rule<AgentMessage>(
                "route-if-available",
                "Route if agent available",
                msg => msg.Type == MessageType.Request
                       && agents.Any(a => a.Status == AgentStatus.Available));

            // The rule's Condition is an Expression, not a Func.
            // We can inspect it without evaluating.
            var condition = rule.Condition;

            // It's a lambda with one parameter
            Assert.Equal(1, condition.Parameters.Count);
            Assert.Equal(typeof(AgentMessage), condition.Parameters[0].Type);

            // The rule knows it needs rewriting (contains FactQueryExpression)
            Assert.True(rule.RequiresRewriting);

            // FactQueryExpression.ContainsFactQuery detects cross-fact references
            Assert.True(FactQueryExpression.ContainsFactQuery(condition));

            // DependentRule: same inspectability, explicit about what it needs
            var depRule = new DependentRule<AgentMessage>(
                "dep-route",
                "Dependent route",
                (msg, ctx) => ctx.Facts<Agent>().Any(a => a.Status == AgentStatus.Available));

            // The projected condition replaces ctx.Facts<T>() with FactQueryExpression
            var projected = depRule.Condition;
            Assert.Equal(1, projected.Parameters.Count);  // Projected to single-param
            Assert.True(FactQueryExpression.ContainsFactQuery(projected));

            // The original two-parameter expression is still available
            var original = depRule.ContextCondition;
            Assert.NotNull(original);
            Assert.Equal(2, original!.Parameters.Count);  // (msg, ctx)

            // KEY INSIGHT: Both patterns produce the same expression tree form:
            //   m => ... FactQueryExpression(typeof(Agent)) ...
            //
            // This is the serialization-ready form. A remote server receives this
            // expression tree, substitutes its own Agent data via FactQueryRewriter,
            // compiles, and evaluates — without ever seeing the original closure
            // or context parameter.
        }
    }
}
