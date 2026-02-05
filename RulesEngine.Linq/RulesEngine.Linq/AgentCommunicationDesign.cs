// =============================================================================
// Agent Communication Rules - Unified Design
// =============================================================================
//
// Design Goals:
// 1. LINQ-native querying of rules and facts
// 2. Cross-fact dependencies with automatic analysis
// 3. Clean separation: WorldState vs Session vs Rules
// 4. Domain shortcuts for MafiaDemo (Agents, Territories, Messages)
// 5. Pipeline for ordered processing, Rules for declarative matching
//
// Status: Design implementation - iterate until complete
// =============================================================================

#nullable enable

namespace RulesEngine.Linq.AgentCommunication
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;

    // =========================================================================
    // PART 1: CORE CONTEXT - Three Distinct Scopes
    // =========================================================================

    #region Core Context

    /// <summary>
    /// The main entry point for agent communication rules.
    /// Provides access to three scopes: WorldState, Session, and Rules.
    /// </summary>
    public interface IAgentRulesContext : IDisposable
    {
        /// <summary>
        /// World state - relatively static reference data.
        /// Agents, Territories, Families, Capabilities.
        /// Loaded at context creation, cached for rule evaluation.
        /// </summary>
        IWorldState World { get; }

        /// <summary>
        /// Current evaluation session - transactional scope.
        /// Messages being processed, events being evaluated.
        /// </summary>
        IMessageSession Session { get; }

        /// <summary>
        /// Rule definitions - queryable and modifiable.
        /// </summary>
        IRuleRegistry Rules { get; }

        /// <summary>
        /// Schema configuration for fact types and relationships.
        /// </summary>
        IFactSchema Schema { get; }

        /// <summary>
        /// Create a new evaluation session.
        /// </summary>
        IMessageSession CreateSession();
    }

    /// <summary>
    /// World state - reference data that rules query against.
    /// Think of this as your "database" of agents and territories.
    /// </summary>
    public interface IWorldState
    {
        /// <summary>
        /// Query any registered fact type.
        /// </summary>
        IQueryable<T> Facts<T>() where T : class;

        /// <summary>
        /// Find by primary key.
        /// </summary>
        T? Find<T>(object key) where T : class;

        /// <summary>
        /// Update world state (agent status changes, territory control, etc.)
        /// </summary>
        void Update<T>(T fact) where T : class;

        /// <summary>
        /// Registered fact types in world state.
        /// </summary>
        IReadOnlySet<Type> RegisteredTypes { get; }
    }

    /// <summary>
    /// Session for processing messages - transactional scope.
    /// </summary>
    public interface IMessageSession : IDisposable
    {
        Guid SessionId { get; }
        SessionPhase Phase { get; }

        /// <summary>
        /// Messages/facts in this session (snapshot before current evaluation).
        /// </summary>
        IQueryable<T> Facts<T>() where T : class;

        /// <summary>
        /// Insert a fact for evaluation.
        /// </summary>
        void Insert<T>(T fact) where T : class;

        /// <summary>
        /// Evaluate all pending facts against rules.
        /// Returns results and populates pending outbound messages.
        /// </summary>
        IEvaluationResult Evaluate();

        /// <summary>
        /// Messages created by rule actions, waiting to be dispatched.
        /// </summary>
        IReadOnlyList<object> PendingOutbound { get; }

        /// <summary>
        /// Commit session - dispatch pending messages, update world state.
        /// </summary>
        void Commit();

        /// <summary>
        /// Rollback - discard pending changes.
        /// </summary>
        void Rollback();
    }

    public enum SessionPhase
    {
        Accepting,      // Can insert facts
        Evaluating,     // Rules are running
        Evaluated,      // Results ready, can commit/rollback
        Committed,
        RolledBack,
        Disposed
    }

    /// <summary>
    /// Registry of rule definitions - queryable collection.
    /// </summary>
    public interface IRuleRegistry
    {
        /// <summary>
        /// Get rules for a specific fact type.
        /// </summary>
        IRuleSet<T> For<T>() where T : class;

        /// <summary>
        /// All registered rule sets.
        /// </summary>
        IEnumerable<Type> RegisteredFactTypes { get; }
    }

    #endregion

    // =========================================================================
    // PART 2: DOMAIN SHORTCUTS - MafiaDemo Specific
    // =========================================================================

    #region Domain Shortcuts

    /// <summary>
    /// Extension methods providing domain-specific shortcuts.
    /// These make rule expressions more readable.
    /// </summary>
    public static class MafiaDomainExtensions
    {
        // --- World State Shortcuts ---

        public static IQueryable<Agent> Agents(this IWorldState world)
            => world.Facts<Agent>();

        public static IQueryable<Territory> Territories(this IWorldState world)
            => world.Facts<Territory>();

        public static IQueryable<Family> Families(this IWorldState world)
            => world.Facts<Family>();

        // --- Context-level shortcuts (for use in rule expressions) ---

        public static IQueryable<Agent> Agents(this IAgentRulesContext context)
            => context.World.Agents();

        public static IQueryable<Territory> Territories(this IAgentRulesContext context)
            => context.World.Territories();

        // --- Session Shortcuts ---

        public static IQueryable<AgentMessage> Messages(this IMessageSession session)
            => session.Facts<AgentMessage>();

        public static IQueryable<AgentMessage> MessageHistory(
            this IMessageSession session,
            string agentId,
            TimeSpan lookback)
        {
            var cutoff = DateTime.UtcNow - lookback;
            return session.Messages()
                .Where(m => m.FromId == agentId && m.Timestamp >= cutoff);
        }

        // --- Agent Navigation ---

        /// <summary>
        /// Get the chain of command from agent up to the top.
        /// Returns: [agent, superior, superior's superior, ..., godfather]
        /// </summary>
        public static IEnumerable<Agent> ChainOfCommand(
            this IQueryable<Agent> agents,
            Agent from)
        {
            var current = from;
            while (current != null)
            {
                yield return current;
                current = current.SuperiorId != null
                    ? agents.FirstOrDefault(a => a.Id == current.SuperiorId)
                    : null;
            }
        }

        /// <summary>
        /// Get the chain of command between two agents.
        /// Returns path from 'from' up to common ancestor down to 'to'.
        /// </summary>
        public static IEnumerable<Agent> ChainOfCommand(
            this IQueryable<Agent> agents,
            Agent from,
            Agent to)
        {
            var fromChain = agents.ChainOfCommand(from).ToList();
            var toChain = agents.ChainOfCommand(to).ToList();

            // Find common ancestor
            var commonAncestor = fromChain.FirstOrDefault(a => toChain.Contains(a));
            if (commonAncestor == null)
                return Enumerable.Empty<Agent>(); // Different families

            // Path up from 'from' to ancestor, then down to 'to'
            var upPath = fromChain.TakeWhile(a => a.Id != commonAncestor.Id).ToList();
            upPath.Add(commonAncestor);

            var downPath = toChain.TakeWhile(a => a.Id != commonAncestor.Id).Reverse();

            return upPath.Concat(downPath);
        }

        /// <summary>
        /// Check if 'from' can directly message 'to' in chain of command.
        /// </summary>
        public static bool IsImmediateSuperior(this Agent from, Agent to)
            => from.SuperiorId == to.Id;

        public static bool IsImmediateSubordinate(this Agent from, Agent to)
            => to.SuperiorId == from.Id;
    }

    #endregion

    // =========================================================================
    // PART 3: RULE DEFINITION - Fluent API
    // =========================================================================

    #region Rule Definition

    /// <summary>
    /// Queryable collection of rules for a fact type.
    /// Note: Does not extend IQueryable to avoid conflict with existing IRuleSet in Abstractions.cs.
    /// Query methods are provided directly on the interface.
    /// </summary>
    public interface IRuleSet<T> where T : class
    {
        void Add(IAgentRule<T> rule);
        void AddRange(IEnumerable<IAgentRule<T>> rules);
        bool Remove(string ruleId);
        IAgentRule<T>? FindById(string ruleId);

        /// <summary>
        /// Get all rules as a queryable collection.
        /// </summary>
        IQueryable<IAgentRule<T>> AsQueryable();

        /// <summary>
        /// Filter rules by predicate.
        /// </summary>
        IEnumerable<IAgentRule<T>> Where(Func<IAgentRule<T>, bool> predicate);

        /// <summary>
        /// Preview which rules would match a fact without executing actions.
        /// </summary>
        IEnumerable<IAgentRule<T>> WouldMatch(T fact);

        /// <summary>
        /// For routing rules: preview where a message would be routed.
        /// </summary>
        RoutePreview<T> WouldRoute(T fact);

        /// <summary>
        /// Create a processing pipeline for this fact type.
        /// </summary>
        IPipelineBuilder<T> Pipeline();
    }

    /// <summary>
    /// Rule that can participate in cross-fact queries.
    /// </summary>
    public interface IAgentRule<T> where T : class
    {
        string Id { get; }
        string Name { get; }
        int Priority { get; }
        IReadOnlySet<string> Tags { get; }
        string? Reason { get; }

        /// <summary>
        /// Fact types this rule depends on (detected + explicit).
        /// </summary>
        IReadOnlySet<Type> Dependencies { get; }

        /// <summary>
        /// Evaluate condition with context access.
        /// </summary>
        bool Evaluate(T fact, IAgentRulesContext context);

        /// <summary>
        /// Execute action with context access.
        /// </summary>
        RuleActionResult Execute(T fact, IAgentRulesContext context);
    }

    /// <summary>
    /// Result of rule action execution.
    /// </summary>
    public class RuleActionResult
    {
        public bool Executed { get; init; }
        public bool Blocked { get; init; }
        public Agent? ReroutedTo { get; init; }
        public IReadOnlyList<string> Flags { get; init; } = Array.Empty<string>();
        public AgentMessage? GeneratedMessage { get; init; }

        public static RuleActionResult Success() => new() { Executed = true };
        public static RuleActionResult Block() => new() { Blocked = true };
        public static RuleActionResult Reroute(Agent to) => new() { Executed = true, ReroutedTo = to };
    }

    /// <summary>
    /// Preview of routing decisions.
    /// </summary>
    public class RoutePreview<T> where T : class
    {
        public T Fact { get; init; } = default!;
        public IReadOnlyList<IAgentRule<T>> MatchedRules { get; init; } = Array.Empty<IAgentRule<T>>();
        public Agent? TargetAgent { get; init; }
        public bool WouldBeBlocked { get; init; }
        public string? BlockReason { get; init; }
        public IReadOnlyList<string> AppliedFlags { get; init; } = Array.Empty<string>();
    }

    #endregion

    // =========================================================================
    // PART 4: FLUENT RULE BUILDER
    // =========================================================================

    #region Fluent Builder

    /// <summary>
    /// Static factory for creating rules with fluent syntax.
    /// </summary>
    public static class Rule
    {
        private static int _autoId;

        /// <summary>
        /// Start building a rule with a simple condition.
        /// </summary>
        public static RuleBuilder<T> When<T>(Expression<Func<T, bool>> condition) where T : class
            => new RuleBuilder<T>().When(condition);

        /// <summary>
        /// Start building a rule with a context-aware condition.
        /// Context is captured from the enclosing scope for clean syntax.
        /// </summary>
        public static RuleBuilder<T> When<T>(
            Expression<Func<T, IAgentRulesContext, bool>> condition) where T : class
            => new RuleBuilder<T>().When(condition);

        /// <summary>
        /// Create a validation rule for pipelines.
        /// </summary>
        public static PipelineStage<T> Validate<T>(
            Expression<Func<T, bool>> condition,
            string errorMessage) where T : class
            => new ValidationStage<T>(condition, errorMessage);

        /// <summary>
        /// Create a transformation rule for pipelines.
        /// </summary>
        public static PipelineStage<T> Transform<T>(
            Action<T> transformation) where T : class
            => new TransformStage<T>(transformation);

        /// <summary>
        /// Create a routing rule for pipelines.
        /// </summary>
        public static PipelineStage<T> Route<T>(
            Func<T, IAgentRulesContext, Agent?> resolver) where T : class
            => new RouteStage<T>(resolver);

        /// <summary>
        /// Create a logging rule for pipelines.
        /// </summary>
        public static PipelineStage<T> Log<T>(
            Func<T, string> formatter) where T : class
            => new LogStage<T>(formatter);

        internal static string NextId() => $"rule-{Interlocked.Increment(ref _autoId)}";
    }

    /// <summary>
    /// Fluent builder for constructing rules.
    /// </summary>
    public class RuleBuilder<T> where T : class
    {
        private string _id = Rule.NextId();
        private string _name = string.Empty;
        private int _priority;
        private readonly HashSet<string> _tags = new();
        private string? _reason;

        private readonly List<Expression> _conditions = new();
        private readonly List<Expression> _contextConditions = new();
        private Action<T>? _simpleAction;
        private Action<T, IAgentRulesContext>? _contextAction;
        private readonly HashSet<Type> _explicitDependencies = new();

        public RuleBuilder<T> WithId(string id)
        {
            _id = id;
            return this;
        }

        public RuleBuilder<T> WithName(string name)
        {
            _name = name;
            return this;
        }

        public RuleBuilder<T> WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        public RuleBuilder<T> WithTags(params string[] tags)
        {
            foreach (var tag in tags) _tags.Add(tag);
            return this;
        }

        public RuleBuilder<T> WithReason(string reason)
        {
            _reason = reason;
            return this;
        }

        public RuleBuilder<T> When(Expression<Func<T, bool>> condition)
        {
            _conditions.Add(condition);
            return this;
        }

        public RuleBuilder<T> When(Expression<Func<T, IAgentRulesContext, bool>> condition)
        {
            _contextConditions.Add(condition);
            return this;
        }

        public RuleBuilder<T> And(Expression<Func<T, bool>> condition)
            => When(condition);

        public RuleBuilder<T> And(Expression<Func<T, IAgentRulesContext, bool>> condition)
            => When(condition);

        public RuleBuilder<T> DependsOn<TFact>() where TFact : class
        {
            _explicitDependencies.Add(typeof(TFact));
            return this;
        }

        public RuleBuilder<T> Then(Action<T> action)
        {
            _simpleAction = action;
            return this;
        }

        public RuleBuilder<T> Then(Action<T, IAgentRulesContext> action)
        {
            _contextAction = action;
            return this;
        }

        public IAgentRule<T> Build()
        {
            if (string.IsNullOrEmpty(_name))
                _name = _id;

            return new AgentRule<T>(
                _id,
                _name,
                _priority,
                _tags,
                _reason,
                _conditions,
                _contextConditions,
                _simpleAction,
                _contextAction,
                _explicitDependencies);
        }

    }

    #endregion

    // =========================================================================
    // PART 5: PIPELINE - Ordered Processing Stages
    // =========================================================================

    #region Pipeline

    /// <summary>
    /// Builder for creating processing pipelines.
    /// Unlike rules (declarative matching), pipelines are sequential stages.
    /// </summary>
    public interface IPipelineBuilder<T> where T : class
    {
        IPipelineBuilder<T> Add(PipelineStage<T> stage);
        IPipelineBuilder<T> AddRule(IAgentRule<T> rule);

        /// <summary>
        /// Invoke rule evaluation as a pipeline stage.
        /// </summary>
        IPipelineBuilder<T> EvaluateRules();

        IMessagePipeline<T> Build();
    }

    /// <summary>
    /// Compiled pipeline ready for execution.
    /// </summary>
    public interface IMessagePipeline<T> where T : class
    {
        PipelineResult<T> Process(T fact, IAgentRulesContext context);
    }

    /// <summary>
    /// Base class for pipeline stages.
    /// </summary>
    public abstract class PipelineStage<T> where T : class
    {
        public abstract string Name { get; }
        public abstract StageResult Execute(T fact, IAgentRulesContext context);
    }

    public class StageResult
    {
        public bool Continue { get; init; } = true;
        public bool Blocked { get; init; }
        public string? Error { get; init; }
        public IReadOnlyDictionary<string, object> Outputs { get; init; }
            = new Dictionary<string, object>();

        public static StageResult Ok() => new();
        public static StageResult Stop() => new() { Continue = false };
        public static StageResult Block(string reason) => new() { Blocked = true, Error = reason };
        public static StageResult Fail(string error) => new() { Continue = false, Error = error };
    }

    public class PipelineResult<T> where T : class
    {
        public T Fact { get; init; } = default!;
        public bool Completed { get; init; }
        public bool Blocked { get; init; }
        public string? Error { get; init; }
        public IReadOnlyList<string> StagesExecuted { get; init; } = Array.Empty<string>();
        public IReadOnlyDictionary<string, object> Outputs { get; init; }
            = new Dictionary<string, object>();
    }

    // Concrete stage implementations
    internal class ValidationStage<T> : PipelineStage<T> where T : class
    {
        private readonly Func<T, bool> _condition;
        private readonly string _errorMessage;

        public ValidationStage(Expression<Func<T, bool>> condition, string errorMessage)
        {
            _condition = condition.Compile();
            _errorMessage = errorMessage;
        }

        public override string Name => "Validate";

        public override StageResult Execute(T fact, IAgentRulesContext context)
            => _condition(fact) ? StageResult.Ok() : StageResult.Block(_errorMessage);
    }

    internal class TransformStage<T> : PipelineStage<T> where T : class
    {
        private readonly Action<T> _transform;

        public TransformStage(Action<T> transform) => _transform = transform;
        public override string Name => "Transform";

        public override StageResult Execute(T fact, IAgentRulesContext context)
        {
            _transform(fact);
            return StageResult.Ok();
        }
    }

    internal class RouteStage<T> : PipelineStage<T> where T : class
    {
        private readonly Func<T, IAgentRulesContext, Agent?> _resolver;

        public RouteStage(Func<T, IAgentRulesContext, Agent?> resolver)
            => _resolver = resolver;

        public override string Name => "Route";

        public override StageResult Execute(T fact, IAgentRulesContext context)
        {
            var target = _resolver(fact, context);
            return new StageResult
            {
                Continue = true,
                Outputs = new Dictionary<string, object>
                {
                    ["RoutedTo"] = target!
                }
            };
        }
    }

    internal class LogStage<T> : PipelineStage<T> where T : class
    {
        private readonly Func<T, string> _formatter;

        public LogStage(Func<T, string> formatter) => _formatter = formatter;
        public override string Name => "Log";

        public override StageResult Execute(T fact, IAgentRulesContext context)
        {
            var message = _formatter(fact);
            // Actual logging would go here
            return new StageResult
            {
                Continue = true,
                Outputs = new Dictionary<string, object> { ["LogMessage"] = message }
            };
        }
    }

    #endregion

    // =========================================================================
    // PART 6: DOMAIN TYPES - MafiaDemo
    // =========================================================================

    #region Domain Types

    public enum AgentRole
    {
        Associate,
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
        Unavailable,
        Compromised
    }

    public enum MessageType
    {
        Request,
        Response,
        Report,
        Alert,
        Broadcast,
        Task,
        StatusReport,
        TerritoryRequest
    }

    /// <summary>
    /// An agent in the family hierarchy.
    /// </summary>
    public class Agent
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AgentRole Role { get; set; }
        public AgentStatus Status { get; set; } = AgentStatus.Available;
        public string FamilyId { get; set; } = string.Empty;
        public string? SuperiorId { get; set; }
        public string? CapoId { get; set; } // For soldiers: their capo
        public int CurrentTaskCount { get; set; }
        public double ReputationScore { get; set; } = 1.0;
        public HashSet<string> Capabilities { get; set; } = new();

        // Navigation (resolved by context)
        public Agent? Superior { get; set; }
        public Family? Family { get; set; }
    }

    /// <summary>
    /// A crime family.
    /// </summary>
    public class Family
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GodfatherId { get; set; } = string.Empty;
    }

    /// <summary>
    /// A territory that can be controlled.
    /// </summary>
    public class Territory
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ControlledBy { get; set; } // FamilyId
        public double Value { get; set; }
    }

    /// <summary>
    /// A message between agents - the primary fact type for rules.
    /// </summary>
    public class AgentMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public MessageType Type { get; set; }
        public string FromId { get; set; } = string.Empty;
        public string ToId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Content { get; set; }
        public Dictionary<string, object> Payload { get; set; } = new();
        public HashSet<string> RequiredCapabilities { get; set; } = new();
        public string? Scope { get; set; } // For broadcasts

        // Mutable state (modified by rules)
        public bool Blocked { get; set; }
        public string? BlockReason { get; set; }
        public string? ReroutedToId { get; set; }
        public HashSet<string> Flags { get; set; } = new();
        public string? EscalatedToId { get; set; }

        // Navigation (resolved by context)
        public Agent? From { get; set; }
        public Agent? To { get; set; }
        public Agent? ReroutedTo { get; set; }
        public Agent? EscalatedTo { get; set; }

        // Fluent mutation methods for rules
        public void Block(string reason)
        {
            Blocked = true;
            BlockReason = reason;
        }

        public void Reroute(Agent target)
        {
            ReroutedToId = target.Id;
            ReroutedTo = target;
        }

        public void RouteTo(Agent target)
        {
            ToId = target.Id;
            To = target;
        }

        public void Flag(string flag) => Flags.Add(flag);

        public void EscalateTo(Agent target)
        {
            EscalatedToId = target.Id;
            EscalatedTo = target;
        }
    }

    #endregion

    // =========================================================================
    // PART 7: SCHEMA AND FACT CONTEXT (from CrossFactRulesDesign)
    // =========================================================================

    #region Schema

    /// <summary>
    /// Schema for fact types and their relationships.
    /// </summary>
    public interface IFactSchema
    {
        IReadOnlySet<Type> RegisteredTypes { get; }
        bool IsRegistered(Type type);
        void ValidateType(Type type);
    }

    #endregion

    // =========================================================================
    // PART 8: EVALUATION RESULTS
    // =========================================================================

    #region Evaluation Results

    /// <summary>
    /// Aggregate evaluation results across all fact types.
    /// </summary>
    public interface IEvaluationResult
    {
        Guid SessionId { get; }
        TimeSpan Duration { get; }
        int TotalFactsEvaluated { get; }
        int TotalRulesEvaluated { get; }
        int TotalMatches { get; }
        bool HasErrors { get; }

        IEvaluationResult<T> ForType<T>() where T : class;
    }

    /// <summary>
    /// Evaluation results for a specific fact type.
    /// </summary>
    public interface IEvaluationResult<T> where T : class
    {
        IReadOnlyList<FactRuleMatch<T>> Matches { get; }
        IReadOnlyList<T> FactsWithMatches { get; }
        IReadOnlyList<T> FactsWithoutMatches { get; }
    }

    /// <summary>
    /// A fact and the rules that matched it.
    /// </summary>
    public class FactRuleMatch<T> where T : class
    {
        public T Fact { get; init; } = default!;
        public IReadOnlyList<IAgentRule<T>> Rules { get; init; } = Array.Empty<IAgentRule<T>>();
    }

    #endregion

    // =========================================================================
    // PART 9: USAGE EXAMPLES
    // =========================================================================

    #region Usage Examples

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

    #endregion

    // =========================================================================
    // PART 10: IMPLEMENTATION SKETCH
    // =========================================================================

    #region Implementation Sketch

    /// <summary>
    /// Concrete rule implementation with dependency analysis.
    /// </summary>
    internal class AgentRule<T> : IAgentRule<T> where T : class
    {
        private readonly List<Expression> _simpleConditions;
        private readonly List<Expression> _contextConditions;
        private readonly Action<T>? _simpleAction;
        private readonly Action<T, IAgentRulesContext>? _contextAction;
        private readonly HashSet<Type> _explicitDependencies;

        // Compiled delegates (lazy)
        private List<Func<T, bool>>? _compiledSimple;
        private List<Func<T, IAgentRulesContext, bool>>? _compiledContext;
        private HashSet<Type>? _detectedDependencies;

        public AgentRule(
            string id,
            string name,
            int priority,
            HashSet<string> tags,
            string? reason,
            List<Expression> simpleConditions,
            List<Expression> contextConditions,
            Action<T>? simpleAction,
            Action<T, IAgentRulesContext>? contextAction,
            HashSet<Type> explicitDependencies)
        {
            Id = id;
            Name = name;
            Priority = priority;
            Tags = tags;
            Reason = reason;
            _simpleConditions = simpleConditions;
            _contextConditions = contextConditions;
            _simpleAction = simpleAction;
            _contextAction = contextAction;
            _explicitDependencies = explicitDependencies;
        }

        public string Id { get; }
        public string Name { get; }
        public int Priority { get; }
        public IReadOnlySet<string> Tags { get; }
        public string? Reason { get; }

        public IReadOnlySet<Type> Dependencies
        {
            get
            {
                // Combine explicit + detected
                var all = new HashSet<Type>(_explicitDependencies);
                if (_detectedDependencies != null)
                {
                    foreach (var dep in _detectedDependencies)
                        all.Add(dep);
                }
                return all;
            }
        }

        public bool Evaluate(T fact, IAgentRulesContext context)
        {
            EnsureCompiled();

            // Check all simple conditions
            foreach (var condition in _compiledSimple!)
            {
                if (!condition(fact))
                    return false;
            }

            // Check all context conditions
            foreach (var condition in _compiledContext!)
            {
                if (!condition(fact, context))
                    return false;
            }

            return true;
        }

        public RuleActionResult Execute(T fact, IAgentRulesContext context)
        {
            if (_contextAction != null)
            {
                _contextAction(fact, context);
            }
            else if (_simpleAction != null)
            {
                _simpleAction(fact);
            }

            return RuleActionResult.Success();
        }

        private void EnsureCompiled()
        {
            if (_compiledSimple != null) return;

            _compiledSimple = _simpleConditions
                .Cast<Expression<Func<T, bool>>>()
                .Select(e => e.Compile())
                .ToList();

            _compiledContext = _contextConditions
                .Cast<Expression<Func<T, IAgentRulesContext, bool>>>()
                .Select(e => e.Compile())
                .ToList();

            // TODO: Use DependencyExtractor to analyze expressions
            // and populate _detectedDependencies
        }
    }

    #endregion

    // =========================================================================
    // PART 11: TODO - IMPLEMENTATION TASKS
    // =========================================================================

    #region Implementation TODOs

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

    #endregion
}
