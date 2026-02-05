// =============================================================================
// Agent Communication Rules - Design Overview
// =============================================================================
//
// This file serves as a design document and contains usage examples.
// The actual implementation is organized in the AgentCommunication/ folder:
//
//   AgentCommunication/
//   ├── Core/
//   │   ├── IAgentRulesContext.cs   - Main entry point
//   │   ├── IWorldState.cs          - Reference data access
//   │   ├── IMessageSession.cs      - Transactional scope
//   │   └── IRuleRegistry.cs        - Rule definitions registry
//   ├── Rules/
//   │   ├── IAgentRule.cs           - Rule interface
//   │   ├── IRuleSet.cs             - Rule collection
//   │   ├── RuleBuilder.cs          - Fluent builder + Rule factory
//   │   └── AgentRule.cs            - Implementation
//   ├── Pipeline/
//   │   ├── IPipelineBuilder.cs     - Pipeline interfaces
//   │   ├── PipelineStages.cs       - Validate/Transform/Route/Log
//   │   └── StageResult.cs          - Stage outcomes
//   ├── Results/
//   │   ├── RuleActionResult.cs
//   │   ├── RoutePreview.cs
//   │   └── EvaluationResult.cs
//   ├── Schema/
//   │   └── IFactSchema.cs
//   └── Extensions/
//       └── MafiaDomainExtensions.cs
//
// Domain types are in Mafia.Domain project:
//   - Agent, Family, Territory, AgentMessage
//   - AgentRole, AgentStatus, MessageType enums
//
// Design Goals:
// 1. LINQ-native querying of rules and facts
// 2. Cross-fact dependencies with automatic analysis
// 3. Clean separation: WorldState vs Session vs Rules
// 4. Domain shortcuts for MafiaDemo (Agents, Territories, Messages)
// 5. Pipeline for ordered processing, Rules for declarative matching
//
// =============================================================================

using Mafia.Domain;

namespace RulesEngine.Linq.AgentCommunication;

// =============================================================================
// USAGE EXAMPLES
// =============================================================================

/// <summary>
/// Demonstrates the full API in action.
/// </summary>
public static class UsageExamples
{
    public static void ConfigureRules(IAgentRulesContext context)
    {
        var rules = context.Rules.For<AgentMessage>();

        // -----------------------------------------------------------------
        // 1. Permission rules - who can message whom
        // -----------------------------------------------------------------
        rules.Add(
            Rule.When<AgentMessage>(m =>
                m.From!.Role == AgentRole.Soldier &&
                m.To!.Role == AgentRole.Godfather)
            .Then(m => m.Block("Soldiers cannot message Godfather directly"))
            .WithReason("Chain of command enforcement")
            .WithTags("permission", "hierarchy")
            .WithPriority(100)
            .Build());

        // -----------------------------------------------------------------
        // 2. Chain of command - must go through immediate superior
        // -----------------------------------------------------------------
        rules.Add(
            Rule.When<AgentMessage>(m => m.Type == MessageType.Request)
            .And((m, ctx) =>
                !m.From!.IsImmediateSuperior(m.To!) &&
                !m.From!.IsImmediateSubordinate(m.To!) &&
                m.From!.FamilyId == m.To!.FamilyId) // Same family, wrong level
            .Then(m => m.Reroute(m.From!.Superior!))
            .WithReason("Requests must go through chain of command")
            .WithTags("routing", "hierarchy")
            .Build());

        // -----------------------------------------------------------------
        // 3. Cross-fact query - hostile territory check
        // -----------------------------------------------------------------
        rules.Add(
            Rule.When<AgentMessage>(m => m.Type == MessageType.TerritoryRequest)
            .And((m, ctx) =>
                ctx.Territories()
                    .Where(t => t.Id == (string)m.Payload["TerritoryId"])
                    .Any(t => t.ControlledBy != m.From!.FamilyId))
            .Then(m => m.Flag("hostile-territory-request"))
            .WithTags("territory", "security")
            .DependsOn<Territory>()
            .Build());

        // -----------------------------------------------------------------
        // 4. Load balancing - route to least busy soldier
        // -----------------------------------------------------------------
        rules.Add(
            Rule.When<AgentMessage>(m => m.Type == MessageType.Task)
            .And(m => m.To == null || m.To.Role == AgentRole.Soldier)
            .Then((m, ctx) =>
            {
                var target = ctx.Agents()
                    .Where(a => a.Role == AgentRole.Soldier)
                    .Where(a => a.Status == AgentStatus.Available)
                    .Where(a => a.FamilyId == m.From!.FamilyId)
                    .OrderBy(a => a.CurrentTaskCount)
                    .FirstOrDefault();

                if (target != null)
                    m.RouteTo(target);
            })
            .WithTags("routing", "load-balance")
            .Build());

        // -----------------------------------------------------------------
        // 5. Escalation - repeated alerts trigger double-escalation
        // -----------------------------------------------------------------
        rules.Add(
            Rule.When<AgentMessage>(m => m.Type == MessageType.Alert)
            .And((m, ctx) =>
                ctx.Session.MessageHistory(m.FromId, TimeSpan.FromMinutes(5))
                    .Count(prev => prev.Type == MessageType.Alert) >= 3)
            .Then(m =>
            {
                var superior = m.From!.Superior;
                if (superior?.Superior != null)
                    m.EscalateTo(superior.Superior);
            })
            .WithReason("Repeated alerts trigger double-escalation")
            .WithTags("escalation", "alert")
            .Build());

        // -----------------------------------------------------------------
        // 6. Capability-based routing
        // -----------------------------------------------------------------
        rules.Add(
            Rule.When<AgentMessage>(m => m.RequiredCapabilities.Any())
            .Then((m, ctx) =>
            {
                var target = ctx.Agents()
                    .Where(a => m.RequiredCapabilities.All(c => a.Capabilities.Contains(c)))
                    .Where(a => a.Status == AgentStatus.Available)
                    .OrderByDescending(a => a.ReputationScore)
                    .FirstOrDefault();

                if (target != null)
                    m.RouteTo(target);
                else if (m.To?.Superior != null)
                    m.Reroute(m.To.Superior); // Fallback to superior
            })
            .WithTags("routing", "capabilities")
            .Build());
    }

    public static void QueryRules(IAgentRulesContext context)
    {
        var rules = context.Rules.For<AgentMessage>();

        // Query escalation rules
        var escalationRules = rules
            .Where(r => r.Tags.Contains("escalation"))
            .Where(r => r.Priority > 50)
            .OrderByDescending(r => r.Priority);

        // Preview routing for a message
        var message = new AgentMessage
        {
            Type = MessageType.Broadcast,
            Scope = "capos"
        };

        var preview = rules.WouldRoute(message);
        // preview.TargetAgent, preview.WouldBeBlocked, etc.
    }

    public static void UsePipeline(IAgentRulesContext context)
    {
        var pipeline = context.Rules.For<AgentMessage>()
            .Pipeline()
            .Add(Rule.Validate<AgentMessage>(m => m.From != null, "Sender required"))
            .Add(Rule.Validate<AgentMessage>(m => m.To != null, "Recipient required"))
            .Add(Rule.Transform<AgentMessage>(m => m.Timestamp = DateTime.UtcNow))
            .EvaluateRules() // Run all registered rules
            .Add(Rule.Log<AgentMessage>(m => $"{m.FromId} -> {m.ToId}: {m.Type}"))
            .Build();

        var message = new AgentMessage { /* ... */ };
        var result = pipeline.Process(message, context);
    }

    public static void SessionWorkflow(IAgentRulesContext context)
    {
        using var session = context.CreateSession();

        // Insert messages to process
        session.Insert(new AgentMessage
        {
            Type = MessageType.Request,
            FromId = "soldier-1",
            ToId = "godfather"
        });

        // Evaluate - rules run, messages may be blocked/rerouted
        var result = session.Evaluate();

        // Check results
        foreach (var match in result.ForType<AgentMessage>().Matches)
        {
            Console.WriteLine($"Message {match.Fact.Id} matched {match.Rules.Count} rules");
            if (match.Fact.Blocked)
                Console.WriteLine($"  BLOCKED: {match.Fact.BlockReason}");
            if (match.Fact.ReroutedTo != null)
                Console.WriteLine($"  Rerouted to: {match.Fact.ReroutedTo.Name}");
        }

        // Commit to dispatch messages and update world state
        session.Commit();
    }

    public static void AuditTrail(IAgentRulesContext context)
    {
        // Find suspicious communications
        var suspiciousComms = context.Session.Facts<AgentMessage>()
            .Where(m => m.Flags.Contains("hostile-territory-request"))
            .Select(m => new
            {
                Message = m,
                From = m.From!.Name,
                Territory = m.Payload["TerritoryId"]
            });
    }
}

// =============================================================================
// IMPLEMENTATION TASKS (TODO)
// =============================================================================

// TODO: Implement InMemoryWorldState
// - Dictionary<Type, List<object>> for fact storage
// - Navigation property resolution on load
// - Find<T>() with key lookup

// TODO: Implement InMemoryMessageSession
// - Snapshot semantics for Facts<T>() during evaluation
// - Pending outbound queue for generated messages
// - Phase state machine

// TODO: Implement InMemoryRuleRegistry
// - RuleSet<T> per fact type
// - WouldMatch/WouldRoute preview methods

// TODO: Implement PipelineBuilder<T> and MessagePipeline<T>
// - Stage list with sequential execution
// - EvaluateRules stage that invokes rule set

// TODO: Integrate with DependencyExtractor from CrossFactRulesDesign
// - Analyze expressions at rule registration
// - Detect Facts<T>() calls in conditions and actions

// TODO: Add tests following TDD pattern
// - Permission rules blocking correctly
// - Routing rules modifying message
// - Cross-fact queries finding correct facts
// - Pipeline stage ordering
