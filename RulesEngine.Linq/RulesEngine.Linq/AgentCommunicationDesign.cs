// =============================================================================
// Agent Communication Rules - Design Overview
// =============================================================================
//
// This file serves as the design document for the AgentCommunication rules API.
// The actual implementation is organized in the AgentCommunication/ folder.
// Usage examples have been moved to unit tests in:
//   Tests/RulesEngine.Linq.Tests/AgentCommunicationDesignTests.cs
//
// =============================================================================
// FOLDER STRUCTURE
// =============================================================================
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
// =============================================================================
// DESIGN GOALS
// =============================================================================
//
// 1. LINQ-native querying of rules and facts
//    - Rules are queryable: rules.Where(r => r.Tags.Contains("escalation"))
//    - Facts are queryable: context.Agents().Where(a => a.Status == Available)
//
// 2. Cross-fact dependencies with automatic analysis
//    - Rules can reference other fact types via context parameter
//    - DependsOn<T>() explicitly declares dependencies
//    - DependencyExtractor analyzes expressions for implicit dependencies
//
// 3. Clean separation: WorldState vs Session vs Rules
//    - WorldState: relatively static reference data (Agents, Territories)
//    - Session: transactional scope for message processing
//    - Rules: definitions that can be queried and modified
//
// 4. Domain shortcuts for MafiaDemo
//    - context.Agents() instead of context.World.Facts<Agent>()
//    - context.Territories() for territory lookups
//    - agent.IsImmediateSuperior(other) for hierarchy checks
//
// 5. Pipeline for ordered processing, Rules for declarative matching
//    - Pipeline: Validate -> Transform -> EvaluateRules -> Log
//    - Rules: declarative conditions with priority-based ordering
//
// =============================================================================
// THREE SCOPES
// =============================================================================
//
// 1. WorldState (reference data)
//    - Loaded at context creation
//    - Cached for rule evaluation
//    - Contains: Agents, Territories, Families
//    - Accessed via: context.World.Facts<T>() or context.Agents()
//
// 2. Session (transactional)
//    - Created for each batch of messages
//    - Phases: Accepting -> Evaluating -> Evaluated -> Committed
//    - Supports: Insert, Evaluate, Commit, Rollback
//    - Snapshot semantics during evaluation
//
// 3. Rules (definitions)
//    - Queryable collection per fact type
//    - Support: Add, Remove, WouldMatch, WouldRoute
//    - Can create Pipeline for sequential processing
//
// =============================================================================
// FLUENT RULE API
// =============================================================================
//
// Simple condition:
//   Rule.When<AgentMessage>(m => m.Type == MessageType.Alert)
//       .Then(m => m.Flag("urgent"))
//       .Build()
//
// With metadata:
//   Rule.When<AgentMessage>(m => ...)
//       .WithId("rule-id")
//       .WithName("Human-readable name")
//       .WithPriority(100)
//       .WithTags("tag1", "tag2")
//       .WithReason("Audit trail reason")
//       .Build()
//
// Multiple conditions (AND):
//   Rule.When<AgentMessage>(m => m.Type == MessageType.Request)
//       .And(m => m.From.Role == AgentRole.Soldier)
//       .And(m => m.To.Role >= AgentRole.Underboss)
//       .Then(m => m.Flag("needs-approval"))
//       .Build()
//
// Context-aware condition:
//   Rule.When<AgentMessage>(m => m.Type == MessageType.TerritoryRequest)
//       .And((m, ctx) => ctx.Territories()
//           .Any(t => t.Id == m.Payload["TerritoryId"] && t.ControlledBy != m.From.FamilyId))
//       .Then(m => m.Flag("hostile-territory"))
//       .DependsOn<Territory>()
//       .Build()
//
// Context-aware action:
//   Rule.When<AgentMessage>(m => m.Type == MessageType.Task)
//       .Then((m, ctx) => {
//           var target = ctx.Agents()
//               .Where(a => a.Status == AgentStatus.Available)
//               .OrderBy(a => a.CurrentTaskCount)
//               .FirstOrDefault();
//           if (target != null) m.RouteTo(target);
//       })
//       .Build()
//
// =============================================================================
// PIPELINE API
// =============================================================================
//
// var pipeline = context.Rules.For<AgentMessage>()
//     .Pipeline()
//     .Add(Rule.Validate<AgentMessage>(m => m.From != null, "Sender required"))
//     .Add(Rule.Validate<AgentMessage>(m => m.To != null, "Recipient required"))
//     .Add(Rule.Transform<AgentMessage>(m => m.Timestamp = DateTime.UtcNow))
//     .EvaluateRules()  // Run all registered rules
//     .Add(Rule.Log<AgentMessage>(m => $"{m.FromId} -> {m.ToId}: {m.Type}"))
//     .Build();
//
// var result = pipeline.Process(message, context);
//
// =============================================================================
// SESSION WORKFLOW
// =============================================================================
//
// using var session = context.CreateSession();
//
// // Insert messages to process
// session.Insert(message1);
// session.Insert(message2);
//
// // Evaluate - rules run, messages may be blocked/rerouted
// var result = session.Evaluate();
//
// // Check results
// foreach (var match in result.ForType<AgentMessage>().Matches)
// {
//     Console.WriteLine($"Message {match.Fact.Id} matched {match.Rules.Count} rules");
// }
//
// // Commit to dispatch messages and update world state
// session.Commit();
//
// =============================================================================
// IMPLEMENTATION TASKS (TODO)
// =============================================================================
//
// TODO: Implement InMemoryWorldState
// - Dictionary<Type, List<object>> for fact storage
// - Navigation property resolution on load
// - Find<T>() with key lookup
//
// TODO: Implement InMemoryMessageSession
// - Snapshot semantics for Facts<T>() during evaluation
// - Pending outbound queue for generated messages
// - Phase state machine
//
// TODO: Implement InMemoryRuleRegistry
// - RuleSet<T> per fact type
// - WouldMatch/WouldRoute preview methods
//
// TODO: Implement PipelineBuilder<T> and MessagePipeline<T>
// - Stage list with sequential execution
// - EvaluateRules stage that invokes rule set
//
// TODO: Integrate with DependencyExtractor from CrossFactRulesDesign
// - Analyze expressions at rule registration
// - Detect Facts<T>() calls in conditions and actions
//
