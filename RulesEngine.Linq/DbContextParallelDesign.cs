// =============================================================================
// DbContext Parallel Design for RulesEngine.Linq
// =============================================================================
// This file contains the design discussion example showing how RulesContext
// parallels EF Core's DbContext pattern for schema definition and dependency
// tracking in cross-fact rule evaluation.
//
// Status: Design document - not compiled into the main project
// =============================================================================
//
// DESIGN DECISIONS:
// -----------------
// 1. DOMAIN OBJECT SEPARATION
//    The domain models in this file (Agent, Territory, AgentMessage, Family)
//    are EXAMPLES ONLY. When implementing, domain objects MUST be kept in a
//    separate project from the RulesEngine.Linq library. The library should
//    have no knowledge of specific domain types - it works with generics and
//    schema configuration only. This prevents tight coupling between the
//    rules infrastructure and any particular domain.
//
// 2. SCHEMA VALIDATION
//    If a rule uses ctx.Facts<T>() for a type T that was not declared in the
//    schema (via OnModelCreating), the system MUST throw an exception. This is
//    the only way to guarantee a reliable dependency graph. Permissive behavior
//    (auto-registration or warnings) would undermine the graph's accuracy.
//
// 3. EXPLICIT + IMPLICIT DEPENDENCIES
//    Both approaches feed into the same DependencyGraph:
//    - Explicit: DependentRule<T>.WithDependency<TFact>() declares directly
//    - Implicit: Expression analysis detects ctx.Facts<T>() calls in closures
//    The schema defines what's POSSIBLE; rules declare/reveal what they USE.
//
// 4. FUTURE SERIALIZATION
//    Data structures should remain simple enough to serialize to strings for
//    a potential future rule server. Type references may need to become type
//    name strings. Don't solve now, but don't paint into a corner.
//
// =============================================================================

// =============================================================================
// PART 1: DOMAIN MODELS (Entities in EF Core terms)
// =============================================================================

namespace MafiaRules.Domain
{
    using System;
    using System.Collections.Generic;

    public enum AgentRole { Soldier, Capo, Underboss, Consigliere, Godfather }
    public enum AgentStatus { Available, Busy, Offline, Compromised }
    public enum MessageType { Request, Command, Alert, StatusReport, TerritoryRequest, Task }

    public class Agent
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AgentRole Role { get; set; }
        public AgentStatus Status { get; set; }
        public string FamilyId { get; set; } = string.Empty;
        public string SuperiorId { get; set; } = string.Empty;
        public int CurrentTaskCount { get; set; }
        public List<string> Capabilities { get; set; } = new();

        // Navigation property (like EF Core)
        public Agent? Superior { get; set; }
        public List<Agent> Subordinates { get; set; } = new();
    }

    public class Territory
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ControlledBy { get; set; } = string.Empty;  // FamilyId
        public decimal MonthlyRevenue { get; set; }
        public bool IsContested { get; set; }
    }

    public class AgentMessage
    {
        public string Id { get; set; } = string.Empty;
        public MessageType Type { get; set; }
        public string FromAgentId { get; set; } = string.Empty;
        public string ToAgentId { get; set; } = string.Empty;
        public string? TerritoryId { get; set; }
        public DateTime Timestamp { get; set; }
        public bool Blocked { get; set; }
        public string? BlockReason { get; set; }
        public List<string> Flags { get; set; } = new();

        // Navigation properties (resolved during evaluation)
        public Agent? From { get; set; }
        public Agent? To { get; set; }
        public Territory? Territory { get; set; }
    }

    public class Family
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Treasury { get; set; }
        public List<Agent> Members { get; set; } = new();
        public List<Territory> Territories { get; set; } = new();
    }
}

// =============================================================================
// PART 2: RULES CONTEXT (Like DbContext)
// =============================================================================

namespace MafiaRules
{
    using System;
    using MafiaRules.Domain;
    using RulesEngine.Linq;
    using RulesEngine.Linq.Schema;

    /// <summary>
    /// The RulesContext - analogous to EF Core's DbContext.
    /// Defines what fact types exist and how they relate to each other.
    /// </summary>
    public class MafiaRulesContext : RulesContext
    {
        // Fact sets - analogous to DbSet<T> properties
        public IRuleSet<Agent> Agents => GetRuleSet<Agent>();
        public IRuleSet<Territory> Territories => GetRuleSet<Territory>();
        public IRuleSet<AgentMessage> Messages => GetRuleSet<AgentMessage>();
        public IRuleSet<Family> Families => GetRuleSet<Family>();

        public MafiaRulesContext() : base()
        {
            OnModelCreating(new RulesModelBuilder(this));
        }

        /// <summary>
        /// Configure the schema - analogous to DbContext.OnModelCreating
        /// </summary>
        protected virtual void OnModelCreating(RulesModelBuilder builder)
        {
            // Configure Agent fact type
            builder.FactType<Agent>(agent =>
            {
                agent.HasKey(a => a.Id);

                // Self-referential relationship: Agent -> Superior
                agent.HasOne(a => a.Superior)
                     .WithMany(a => a.Subordinates)
                     .HasForeignKey(a => a.SuperiorId);

                // Agent belongs to a Family
                agent.HasOne<Family>()
                     .WithMany(f => f.Members)
                     .HasForeignKey(a => a.FamilyId);
            });

            // Configure Territory fact type
            builder.FactType<Territory>(territory =>
            {
                territory.HasKey(t => t.Id);

                // Territory is controlled by a Family
                territory.HasOne<Family>()
                         .WithMany(f => f.Territories)
                         .HasForeignKey(t => t.ControlledBy);
            });

            // Configure AgentMessage fact type
            builder.FactType<AgentMessage>(message =>
            {
                message.HasKey(m => m.Id);

                // Message has From agent
                message.HasOne(m => m.From)
                       .WithMany()
                       .HasForeignKey(m => m.FromAgentId);

                // Message has To agent
                message.HasOne(m => m.To)
                       .WithMany()
                       .HasForeignKey(m => m.ToAgentId);

                // Message optionally references a Territory
                message.HasOne(m => m.Territory)
                       .WithMany()
                       .HasForeignKey(m => m.TerritoryId)
                       .IsOptional();
            });

            // Configure Family fact type
            builder.FactType<Family>(family =>
            {
                family.HasKey(f => f.Id);
            });

            // Register rules after schema is configured
            ConfigureRules(builder);
        }

        /// <summary>
        /// Configure rules - these can reference relationships defined above
        /// </summary>
        protected virtual void ConfigureRules(RulesModelBuilder builder)
        {
            // Rule 1: Soldiers cannot message Godfather directly
            Messages.Add(new Rule<AgentMessage>(
                "no-soldier-to-godfather",
                "Soldiers cannot message Godfather directly",
                m => m.From!.Role == AgentRole.Soldier && m.To!.Role == AgentRole.Godfather)
                .Then(m => { m.Blocked = true; m.BlockReason = "Chain of command violation"; }));

            // Rule 2: Flag hostile territory requests
            // This rule DEPENDS ON the Message -> Territory relationship
            Messages.Add(new Rule<AgentMessage>(
                "hostile-territory-flag",
                "Flag requests for territory controlled by another family",
                m => m.Type == MessageType.TerritoryRequest
                    && m.Territory != null
                    && m.Territory.ControlledBy != m.From!.FamilyId)
                .Then(m => m.Flags.Add("hostile-territory-request")));

            // Rule 3: Route tasks to least busy available soldier
            // This rule DEPENDS ON querying the Agents fact set
            Messages.Add(new DependentRule<AgentMessage>(
                "load-balance-tasks",
                "Route tasks to least busy soldier",
                m => m.Type == MessageType.Task)
                .WithDependency<Agent>(ctx => ctx.Facts<Agent>())
                .Then((m, ctx) =>
                {
                    var target = ctx.Facts<Agent>()
                        .Where(a => a.Role == AgentRole.Soldier)
                        .Where(a => a.Status == AgentStatus.Available)
                        .Where(a => a.FamilyId == m.From!.FamilyId)
                        .OrderBy(a => a.CurrentTaskCount)
                        .FirstOrDefault();

                    if (target != null)
                    {
                        m.ToAgentId = target.Id;
                        m.To = target;
                    }
                }));

            // Rule 4: Escalate repeated alerts (time-windowed query)
            // This rule DEPENDS ON querying historical Messages
            Messages.Add(new DependentRule<AgentMessage>(
                "escalate-repeated-alerts",
                "Double-escalate on 3+ alerts in 5 minutes",
                m => m.Type == MessageType.Alert)
                .WithDependency<AgentMessage>(ctx => ctx.Facts<AgentMessage>())
                .When((m, ctx) =>
                {
                    var recentAlerts = ctx.Facts<AgentMessage>()
                        .Where(prev => prev.FromAgentId == m.FromAgentId)
                        .Where(prev => prev.Type == MessageType.Alert)
                        .Where(prev => prev.Timestamp > m.Timestamp.AddMinutes(-5))
                        .Where(prev => prev.Id != m.Id)
                        .Count();
                    return recentAlerts >= 2;  // This is the 3rd+ alert
                })
                .Then((m, ctx) =>
                {
                    // Escalate to superior's superior
                    var superior = m.From?.Superior?.Superior;
                    if (superior != null)
                    {
                        m.ToAgentId = superior.Id;
                        m.To = superior;
                        m.Flags.Add("double-escalated");
                    }
                }));
        }
    }
}

// =============================================================================
// PART 3: SCHEMA BUILDER (Like ModelBuilder in EF Core)
// =============================================================================

namespace RulesEngine.Linq.Schema
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    /// <summary>
    /// Fluent API for configuring fact types and their relationships.
    /// Analogous to EF Core's ModelBuilder.
    /// </summary>
    public class RulesModelBuilder
    {
        private readonly RulesContext _context;
        private readonly Dictionary<Type, FactTypeConfiguration> _configurations = new();

        public RulesModelBuilder(RulesContext context)
        {
            _context = context;
        }

        public FactTypeBuilder<T> FactType<T>(Action<FactTypeBuilder<T>> configure) where T : class
        {
            var builder = new FactTypeBuilder<T>(this);
            configure(builder);
            _configurations[typeof(T)] = builder.Build();
            return builder;
        }

        internal void RegisterRelationship(FactRelationship relationship)
        {
            // Track in dependency graph
            var sourceConfig = GetOrCreateConfiguration(relationship.SourceType);
            sourceConfig.Dependencies.Add(relationship.TargetType);
            sourceConfig.Relationships.Add(relationship);
        }

        private FactTypeConfiguration GetOrCreateConfiguration(Type type)
        {
            if (!_configurations.TryGetValue(type, out var config))
            {
                config = new FactTypeConfiguration(type);
                _configurations[type] = config;
            }
            return config;
        }

        public DependencyGraph BuildDependencyGraph()
        {
            var graph = new DependencyGraph();
            foreach (var config in _configurations.Values)
            {
                graph.AddFactType(config.FactType, config.Dependencies);
            }
            return graph;
        }
    }

    /// <summary>
    /// Configuration for a single fact type.
    /// </summary>
    public class FactTypeConfiguration
    {
        public Type FactType { get; }
        public string? KeyProperty { get; set; }
        public HashSet<Type> Dependencies { get; } = new();
        public List<FactRelationship> Relationships { get; } = new();

        public FactTypeConfiguration(Type factType)
        {
            FactType = factType;
        }
    }

    /// <summary>
    /// Builder for configuring a fact type - analogous to EntityTypeBuilder<T>
    /// </summary>
    public class FactTypeBuilder<T> where T : class
    {
        private readonly RulesModelBuilder _modelBuilder;
        private string? _keyProperty;
        private readonly List<FactRelationship> _relationships = new();

        public FactTypeBuilder(RulesModelBuilder modelBuilder)
        {
            _modelBuilder = modelBuilder;
        }

        public FactTypeBuilder<T> HasKey<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _keyProperty = GetPropertyName(keySelector);
            return this;
        }

        public ReferenceNavigationBuilder<T, TRelated> HasOne<TRelated>(
            Expression<Func<T, TRelated?>>? navigationSelector = null) where TRelated : class
        {
            return new ReferenceNavigationBuilder<T, TRelated>(_modelBuilder, navigationSelector);
        }

        internal FactTypeConfiguration Build()
        {
            return new FactTypeConfiguration(typeof(T))
            {
                KeyProperty = _keyProperty
            };
        }

        private static string GetPropertyName<TProperty>(Expression<Func<T, TProperty>> selector)
        {
            if (selector.Body is MemberExpression member)
                return member.Member.Name;
            throw new ArgumentException("Expected property selector");
        }
    }

    /// <summary>
    /// Builder for reference (one-to-one or many-to-one) relationships
    /// </summary>
    public class ReferenceNavigationBuilder<TSource, TTarget>
        where TSource : class
        where TTarget : class
    {
        private readonly RulesModelBuilder _modelBuilder;
        private readonly Expression<Func<TSource, TTarget?>>? _navigation;

        public ReferenceNavigationBuilder(
            RulesModelBuilder modelBuilder,
            Expression<Func<TSource, TTarget?>>? navigation)
        {
            _modelBuilder = modelBuilder;
            _navigation = navigation;
        }

        public ForeignKeyBuilder<TSource, TTarget> WithMany(
            Expression<Func<TTarget, IEnumerable<TSource>>>? collectionSelector = null)
        {
            return new ForeignKeyBuilder<TSource, TTarget>(_modelBuilder, _navigation, collectionSelector);
        }

        public ForeignKeyBuilder<TSource, TTarget> WithOne(
            Expression<Func<TTarget, TSource?>>? inverseSelector = null)
        {
            return new ForeignKeyBuilder<TSource, TTarget>(_modelBuilder, _navigation, null);
        }
    }

    /// <summary>
    /// Builder for foreign key configuration
    /// </summary>
    public class ForeignKeyBuilder<TSource, TTarget>
        where TSource : class
        where TTarget : class
    {
        private readonly RulesModelBuilder _modelBuilder;
        private readonly Expression<Func<TSource, TTarget?>>? _navigation;
        private readonly Expression<Func<TTarget, IEnumerable<TSource>>>? _inverseNavigation;
        private string? _foreignKeyProperty;
        private bool _isOptional;

        public ForeignKeyBuilder(
            RulesModelBuilder modelBuilder,
            Expression<Func<TSource, TTarget?>>? navigation,
            Expression<Func<TTarget, IEnumerable<TSource>>>? inverseNavigation)
        {
            _modelBuilder = modelBuilder;
            _navigation = navigation;
            _inverseNavigation = inverseNavigation;
        }

        public ForeignKeyBuilder<TSource, TTarget> HasForeignKey<TKey>(
            Expression<Func<TSource, TKey>> foreignKeySelector)
        {
            _foreignKeyProperty = GetPropertyName(foreignKeySelector);

            // Register the relationship
            _modelBuilder.RegisterRelationship(new FactRelationship
            {
                SourceType = typeof(TSource),
                TargetType = typeof(TTarget),
                ForeignKeyProperty = _foreignKeyProperty,
                NavigationProperty = GetNavigationName(),
                IsOptional = _isOptional
            });

            return this;
        }

        public ForeignKeyBuilder<TSource, TTarget> IsOptional()
        {
            _isOptional = true;
            return this;
        }

        private string? GetNavigationName()
        {
            if (_navigation?.Body is MemberExpression member)
                return member.Member.Name;
            return null;
        }

        private static string GetPropertyName<TProperty>(Expression<Func<TSource, TProperty>> selector)
        {
            if (selector.Body is MemberExpression member)
                return member.Member.Name;
            throw new ArgumentException("Expected property selector");
        }
    }

    /// <summary>
    /// Represents a relationship between two fact types
    /// </summary>
    public class FactRelationship
    {
        public Type SourceType { get; init; } = null!;
        public Type TargetType { get; init; } = null!;
        public string ForeignKeyProperty { get; init; } = string.Empty;
        public string? NavigationProperty { get; init; }
        public bool IsOptional { get; init; }
    }
}

// =============================================================================
// PART 4: DEPENDENCY GRAPH
// =============================================================================

namespace RulesEngine.Linq.Schema
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Tracks dependencies between fact types for evaluation ordering.
    /// </summary>
    public class DependencyGraph
    {
        private readonly Dictionary<Type, HashSet<Type>> _dependencies = new();
        private readonly Dictionary<Type, HashSet<Type>> _dependents = new();  // Reverse lookup

        public void AddFactType(Type factType, IEnumerable<Type> dependsOn)
        {
            if (!_dependencies.ContainsKey(factType))
                _dependencies[factType] = new HashSet<Type>();

            foreach (var dep in dependsOn)
            {
                _dependencies[factType].Add(dep);

                if (!_dependents.ContainsKey(dep))
                    _dependents[dep] = new HashSet<Type>();
                _dependents[dep].Add(factType);
            }
        }

        /// <summary>
        /// Get the order in which fact types should be loaded/resolved.
        /// Uses topological sort - dependencies come before dependents.
        /// </summary>
        public IReadOnlyList<Type> GetLoadOrder()
        {
            var result = new List<Type>();
            var visited = new HashSet<Type>();
            var visiting = new HashSet<Type>();  // For cycle detection

            foreach (var type in _dependencies.Keys)
            {
                Visit(type, visited, visiting, result);
            }

            return result;
        }

        private void Visit(Type type, HashSet<Type> visited, HashSet<Type> visiting, List<Type> result)
        {
            if (visited.Contains(type)) return;

            if (visiting.Contains(type))
                throw new InvalidOperationException($"Circular dependency detected involving {type.Name}");

            visiting.Add(type);

            if (_dependencies.TryGetValue(type, out var deps))
            {
                foreach (var dep in deps)
                {
                    Visit(dep, visited, visiting, result);
                }
            }

            visiting.Remove(type);
            visited.Add(type);
            result.Add(type);
        }

        /// <summary>
        /// Get all types that depend on the given type.
        /// Useful for knowing what to re-evaluate when facts change.
        /// </summary>
        public IReadOnlySet<Type> GetDependents(Type factType)
        {
            return _dependents.TryGetValue(factType, out var deps)
                ? deps
                : new HashSet<Type>();
        }

        /// <summary>
        /// Get all types that the given type depends on.
        /// </summary>
        public IReadOnlySet<Type> GetDependencies(Type factType)
        {
            return _dependencies.TryGetValue(factType, out var deps)
                ? deps
                : new HashSet<Type>();
        }

        /// <summary>
        /// Validate that there are no missing dependencies.
        /// </summary>
        public ValidationResult Validate()
        {
            var errors = new List<string>();
            var allTypes = _dependencies.Keys.ToHashSet();

            foreach (var (type, deps) in _dependencies)
            {
                foreach (var dep in deps)
                {
                    if (!allTypes.Contains(dep))
                    {
                        errors.Add($"{type.Name} depends on {dep.Name} which is not registered");
                    }
                }
            }

            return new ValidationResult(errors.Count == 0, errors);
        }

        public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);
    }
}

// =============================================================================
// PART 5: ENHANCED RULE WITH DEPENDENCIES
// =============================================================================

namespace RulesEngine.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    /// <summary>
    /// Rule that can declare dependencies on other fact types.
    /// </summary>
    public class DependentRule<T> : Rule<T> where T : class
    {
        private readonly List<Type> _factDependencies = new();
        private Func<T, IFactContext, bool>? _dependentCondition;
        private Action<T, IFactContext>? _dependentAction;

        public DependentRule(string id, string name, Expression<Func<T, bool>> condition)
            : base(id, name, condition)
        {
        }

        public IReadOnlyList<Type> FactDependencies => _factDependencies;

        public DependentRule<T> WithDependency<TFact>(
            Func<IFactContext, IQueryable<TFact>> factSelector) where TFact : class
        {
            _factDependencies.Add(typeof(TFact));
            return this;
        }

        public DependentRule<T> When(Func<T, IFactContext, bool> condition)
        {
            _dependentCondition = condition;
            return this;
        }

        public new DependentRule<T> Then(Action<T, IFactContext> action)
        {
            _dependentAction = action;
            return this;
        }

        public bool Evaluate(T fact, IFactContext context)
        {
            // First check the simple condition
            if (!base.Evaluate(fact)) return false;

            // Then check the dependent condition if present
            if (_dependentCondition != null)
                return _dependentCondition(fact, context);

            return true;
        }

        public RuleResult Execute(T fact, IFactContext context)
        {
            if (_dependentAction != null)
            {
                _dependentAction(fact, context);
                return RuleResult.Success(Id);
            }
            return base.Execute(fact);
        }
    }

    /// <summary>
    /// Provides access to facts during rule evaluation.
    /// Analogous to how EF Core provides access to related entities.
    /// </summary>
    public interface IFactContext
    {
        IQueryable<TFact> Facts<TFact>() where TFact : class;
        TFact? FindByKey<TFact>(object key) where TFact : class;
    }
}

// =============================================================================
// PART 6: SESSION WITH NAVIGATION RESOLUTION
// =============================================================================

namespace RulesEngine.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MafiaRules;
    using MafiaRules.Domain;
    using RulesEngine.Linq.Schema;

    /// <summary>
    /// Enhanced session that resolves navigation properties before evaluation.
    /// Analogous to EF Core's change tracker resolving relationships.
    /// </summary>
    public class NavigationResolvingSession : IRuleSession, IFactContext
    {
        private readonly MafiaRulesContext _context;
        private readonly Dictionary<Type, object> _factSets = new();
        private readonly DependencyGraph _dependencyGraph;
        private bool _navigationResolved;

        public NavigationResolvingSession(MafiaRulesContext context, DependencyGraph dependencyGraph)
        {
            _context = context;
            _dependencyGraph = dependencyGraph;
        }

        public Guid SessionId { get; } = Guid.NewGuid();
        public SessionState State { get; private set; } = SessionState.Active;

        public IFactSet<T> Facts<T>() where T : class
        {
            if (!_factSets.TryGetValue(typeof(T), out var set))
            {
                set = new FactSet<T>((RulesContext)_context, _context.GetRuleSet<T>());
                _factSets[typeof(T)] = set;
            }
            return (IFactSet<T>)set;
        }

        // IFactContext implementation for rules
        IQueryable<TFact> IFactContext.Facts<TFact>()
        {
            return Facts<TFact>().AsQueryable();
        }

        public TFact? FindByKey<TFact>(object key) where TFact : class
        {
            // Simple key lookup - would use configured key property in real impl
            var keyStr = key.ToString();
            return Facts<TFact>().FirstOrDefault(f =>
                f.GetType().GetProperty("Id")?.GetValue(f)?.ToString() == keyStr);
        }

        public void Insert<T>(T fact) where T : class => Facts<T>().Add(fact);
        public void InsertAll<T>(IEnumerable<T> facts) where T : class => Facts<T>().AddRange(facts);

        /// <summary>
        /// Resolve all navigation properties based on foreign keys.
        /// Called automatically before evaluation.
        /// </summary>
        public void ResolveNavigations()
        {
            if (_navigationResolved) return;

            // Process in dependency order
            var order = _dependencyGraph.GetLoadOrder();

            foreach (var type in order)
            {
                if (type == typeof(AgentMessage))
                    ResolveMessageNavigations();
                else if (type == typeof(Agent))
                    ResolveAgentNavigations();
                // Add other types as needed
            }

            _navigationResolved = true;
        }

        private void ResolveMessageNavigations()
        {
            var messages = Facts<AgentMessage>();
            var agents = Facts<Agent>().ToDictionary(a => a.Id);
            var territories = Facts<Territory>().ToDictionary(t => t.Id);

            foreach (var message in messages)
            {
                if (agents.TryGetValue(message.FromAgentId, out var from))
                    message.From = from;

                if (agents.TryGetValue(message.ToAgentId, out var to))
                    message.To = to;

                if (message.TerritoryId != null && territories.TryGetValue(message.TerritoryId, out var territory))
                    message.Territory = territory;
            }
        }

        private void ResolveAgentNavigations()
        {
            var agents = Facts<Agent>().ToDictionary(a => a.Id);

            foreach (var agent in agents.Values)
            {
                if (!string.IsNullOrEmpty(agent.SuperiorId) && agents.TryGetValue(agent.SuperiorId, out var superior))
                {
                    agent.Superior = superior;
                    superior.Subordinates.Add(agent);
                }
            }
        }

        public IEvaluationResult Evaluate()
        {
            ResolveNavigations();
            // ... evaluation logic using IFactContext
            throw new NotImplementedException("Full evaluation not yet implemented");
        }

        public IEvaluationResult<T> Evaluate<T>() where T : class
        {
            ResolveNavigations();
            // ... evaluation logic
            throw new NotImplementedException("Full evaluation not yet implemented");
        }

        public void Commit() => State = SessionState.Committed;
        public void Rollback() => State = SessionState.RolledBack;
        public void Dispose() => State = SessionState.Disposed;
    }
}

// =============================================================================
// PART 7: USAGE EXAMPLE
// =============================================================================

namespace MafiaRules.Example
{
    using System;
    using System.Linq;
    using MafiaRules.Domain;
    using RulesEngine.Linq;

    public class UsageExample
    {
        public void DemonstrateUsage()
        {
            // Create context (like new MyDbContext())
            using var context = new MafiaRulesContext();

            // Create session (like a unit of work)
            using var session = context.CreateSession();

            // Insert reference data (like seeding)
            var godfather = new Agent { Id = "GF", Name = "Don Corleone", Role = AgentRole.Godfather, FamilyId = "Corleone" };
            var underboss = new Agent { Id = "UB", Name = "Sonny", Role = AgentRole.Underboss, FamilyId = "Corleone", SuperiorId = "GF" };
            var capo = new Agent { Id = "C1", Name = "Clemenza", Role = AgentRole.Capo, FamilyId = "Corleone", SuperiorId = "UB" };
            var soldier = new Agent { Id = "S1", Name = "Rocco", Role = AgentRole.Soldier, FamilyId = "Corleone", SuperiorId = "C1", Status = AgentStatus.Available };

            session.Insert(godfather);
            session.Insert(underboss);
            session.Insert(capo);
            session.Insert(soldier);

            // Insert territories
            session.Insert(new Territory { Id = "T1", Name = "Brooklyn", ControlledBy = "Corleone" });
            session.Insert(new Territory { Id = "T2", Name = "Bronx", ControlledBy = "Barzini" });  // Enemy territory

            // Insert message to evaluate
            var message = new AgentMessage
            {
                Id = "M1",
                Type = MessageType.TerritoryRequest,
                FromAgentId = "S1",
                ToAgentId = "C1",
                TerritoryId = "T2",  // Requesting enemy territory
                Timestamp = DateTime.UtcNow
            };
            session.Insert(message);

            // Evaluate - navigation properties resolved automatically
            var result = session.Evaluate<AgentMessage>();

            // Check results
            foreach (var match in result.Matches)
            {
                Console.WriteLine($"Message {match.Fact.Id}:");
                Console.WriteLine($"  From: {match.Fact.From?.Name}");
                Console.WriteLine($"  To: {match.Fact.To?.Name}");
                Console.WriteLine($"  Territory: {match.Fact.Territory?.Name}");
                Console.WriteLine($"  Flags: {string.Join(", ", match.Fact.Flags)}");
                Console.WriteLine($"  Matched rules: {string.Join(", ", match.MatchedRules.Select(r => r.Id))}");
            }
            // Expected output:
            // Message M1:
            //   From: Rocco
            //   To: Clemenza
            //   Territory: Bronx
            //   Flags: hostile-territory-request
            //   Matched rules: hostile-territory-flag
        }
    }
}

// =============================================================================
// KEY PARALLELS WITH EF CORE
// =============================================================================
//
// | EF Core                | RulesEngine.Linq       | Purpose                      |
// |------------------------|------------------------|------------------------------|
// | DbContext              | RulesContext           | Entry point, manages schema  |
// | DbSet<T>               | IRuleSet<T>            | Collection of entities/facts |
// | OnModelCreating        | OnModelCreating        | Configure schema             |
// | ModelBuilder           | RulesModelBuilder      | Fluent configuration API     |
// | EntityTypeBuilder<T>   | FactTypeBuilder<T>     | Configure single type        |
// | HasOne/HasMany         | HasOne/WithMany        | Define relationships         |
// | HasForeignKey          | HasForeignKey          | Specify FK property          |
// | Navigation properties  | Navigation properties  | Traverse relationships       |
// | Change tracker         | ResolveNavigations     | Populate nav properties      |
// | SaveChanges            | Evaluate               | Execute (persist vs evaluate)|
//
// =============================================================================
