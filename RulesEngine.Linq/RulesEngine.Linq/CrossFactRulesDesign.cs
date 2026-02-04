// =============================================================================
// Cross-Fact Rules Design for RulesEngine.Linq
// =============================================================================
// Consolidated design for cross-fact rule evaluation with dependency tracking.
// Merges concepts from DbContextParallelDesign.cs and DependencyAnalysis.cs.
//
// Status: Design implementation - iterate until complete
// =============================================================================
//
// DESIGN DECISIONS (from DbContextParallelDesign.cs):
// ---------------------------------------------------
// 1. DOMAIN OBJECT SEPARATION
//    Domain objects MUST be kept in a separate project from RulesEngine.Linq.
//    The library works with generics and schema configuration only.
//
// 2. SCHEMA VALIDATION
//    If a rule uses ctx.Facts<T>() for unregistered T, throw an exception.
//    This guarantees a reliable dependency graph.
//
// 3. EXPLICIT + IMPLICIT DEPENDENCIES
//    Both feed into the same DependencyGraph:
//    - Explicit: DependentRule<T>.DependsOn<TFact>()
//    - Implicit: Expression analysis detects ctx.Facts<T>() calls
//
// 4. FUTURE SERIALIZATION
//    Keep data structures simple enough to serialize for future rule server.
//
// =============================================================================

#nullable enable

namespace RulesEngine.Linq.CrossFact
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    // =========================================================================
    // PART 1: CORE INTERFACES
    // =========================================================================

    #region Core Interfaces

    /// <summary>
    /// Provides access to facts during rule evaluation.
    /// Rules receive this to query other fact types.
    /// </summary>
    public interface IFactContext
    {
        /// <summary>
        /// Query facts of type T. Type must be registered in schema.
        /// </summary>
        /// <exception cref="InvalidOperationException">If T is not registered.</exception>
        IQueryable<T> Facts<T>() where T : class;

        /// <summary>
        /// Find a single fact by its key. Returns null if not found.
        /// </summary>
        T? FindByKey<T>(object key) where T : class;

        /// <summary>
        /// All registered fact types in the schema.
        /// </summary>
        IReadOnlySet<Type> RegisteredFactTypes { get; }
    }

    /// <summary>
    /// Schema for a single fact type.
    /// </summary>
    public interface IFactTypeSchema
    {
        Type FactType { get; }
        string? KeyProperty { get; }
        IReadOnlyDictionary<string, NavigationInfo> Navigations { get; }
    }

    /// <summary>
    /// Full schema containing all registered fact types.
    /// </summary>
    public interface IFactSchema
    {
        IReadOnlySet<Type> RegisteredTypes { get; }
        IFactTypeSchema? GetFactTypeSchema(Type type);
        bool IsRegistered(Type type);

        /// <summary>
        /// Throws InvalidOperationException if type not registered.
        /// </summary>
        void ValidateType(Type type);
    }

    /// <summary>
    /// Rule that supports cross-fact dependencies.
    /// </summary>
    public interface ICrossFactRule<T> : IRule<T> where T : class
    {
        /// <summary>
        /// All dependencies (explicit + detected from analysis).
        /// </summary>
        IReadOnlySet<Type> Dependencies { get; }

        /// <summary>
        /// Whether this rule requires IFactContext for evaluation.
        /// </summary>
        bool RequiresContext { get; }

        /// <summary>
        /// Evaluate with context access.
        /// </summary>
        bool EvaluateWithContext(T fact, IFactContext context);

        /// <summary>
        /// Execute with context access.
        /// </summary>
        RuleResult ExecuteWithContext(T fact, IFactContext context);

        /// <summary>
        /// Analyze expressions and detect dependencies. Called at registration.
        /// </summary>
        void AnalyzeDependencies(IFactSchema schema);
    }

    #endregion

    // =========================================================================
    // PART 2: NAVIGATION INFO
    // =========================================================================

    #region Navigation Info

    /// <summary>
    /// Information about a navigation property relationship.
    /// </summary>
    public sealed class NavigationInfo
    {
        public string PropertyName { get; init; } = string.Empty;
        public Type TargetType { get; init; } = null!;
        public string ForeignKeyProperty { get; init; } = string.Empty;
        public bool IsCollection { get; init; }
        public bool IsOptional { get; init; }

        // For serialization (design decision #4)
        public string TargetTypeName => TargetType?.FullName ?? string.Empty;
    }

    /// <summary>
    /// Represents a relationship between two fact types.
    /// </summary>
    public sealed class FactRelationship
    {
        public Type SourceType { get; init; } = null!;
        public Type TargetType { get; init; } = null!;
        public string ForeignKeyProperty { get; init; } = string.Empty;
        public string? NavigationProperty { get; init; }
        public bool IsOptional { get; init; }

        // For serialization
        public string SourceTypeName => SourceType?.FullName ?? string.Empty;
        public string TargetTypeName => TargetType?.FullName ?? string.Empty;
    }

    #endregion

    // =========================================================================
    // PART 3: DEPENDENCY GRAPH
    // =========================================================================

    #region Dependency Graph

    /// <summary>
    /// Tracks dependencies between fact types for evaluation ordering.
    /// Uses topological sort to determine load order.
    /// </summary>
    public class DependencyGraph
    {
        private readonly Dictionary<Type, HashSet<Type>> _dependencies = new();
        private readonly Dictionary<Type, HashSet<Type>> _dependents = new();
        private List<Type>? _cachedLoadOrder;

        /// <summary>
        /// Register a fact type with optional dependencies.
        /// </summary>
        public void AddFactType(Type factType, IEnumerable<Type>? dependsOn = null)
        {
            InvalidateCache();

            if (!_dependencies.ContainsKey(factType))
                _dependencies[factType] = new HashSet<Type>();

            if (dependsOn == null) return;

            foreach (var dep in dependsOn)
            {
                AddDependencyInternal(factType, dep);
            }
        }

        /// <summary>
        /// Add a dependency: fromType depends on toType.
        /// </summary>
        public void AddDependency(Type fromType, Type toType)
        {
            InvalidateCache();

            if (!_dependencies.ContainsKey(fromType))
                _dependencies[fromType] = new HashSet<Type>();

            AddDependencyInternal(fromType, toType);
        }

        private void AddDependencyInternal(Type fromType, Type toType)
        {
            _dependencies[fromType].Add(toType);

            if (!_dependents.ContainsKey(toType))
                _dependents[toType] = new HashSet<Type>();
            _dependents[toType].Add(fromType);
        }

        /// <summary>
        /// Get load order via topological sort. Dependencies come before dependents.
        /// </summary>
        public IReadOnlyList<Type> GetLoadOrder()
        {
            if (_cachedLoadOrder != null)
                return _cachedLoadOrder;

            var result = new List<Type>();
            var visited = new HashSet<Type>();
            var visiting = new HashSet<Type>();

            foreach (var type in _dependencies.Keys)
            {
                TopologicalVisit(type, visited, visiting, result);
            }

            _cachedLoadOrder = result;
            return result;
        }

        private void TopologicalVisit(
            Type type,
            HashSet<Type> visited,
            HashSet<Type> visiting,
            List<Type> result)
        {
            if (visited.Contains(type)) return;

            if (visiting.Contains(type))
            {
                throw new CircularDependencyException(type);
            }

            visiting.Add(type);

            if (_dependencies.TryGetValue(type, out var deps))
            {
                foreach (var dep in deps)
                {
                    TopologicalVisit(dep, visited, visiting, result);
                }
            }

            visiting.Remove(type);
            visited.Add(type);
            result.Add(type);
        }

        /// <summary>
        /// Get all types that depend on the given type.
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
        /// Check if adding a dependency would create a cycle.
        /// </summary>
        public bool WouldCreateCycle(Type fromType, Type toType)
        {
            var visited = new HashSet<Type>();
            return HasPathTo(toType, fromType, visited);
        }

        private bool HasPathTo(Type from, Type to, HashSet<Type> visited)
        {
            if (from == to) return true;
            if (visited.Contains(from)) return false;

            visited.Add(from);

            if (_dependencies.TryGetValue(from, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (HasPathTo(dep, to, visited))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Validate the graph against a schema.
        /// </summary>
        public DependencyValidationResult Validate(IFactSchema schema)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            foreach (var (type, deps) in _dependencies)
            {
                if (!schema.IsRegistered(type))
                {
                    errors.Add($"Type {type.Name} is in dependency graph but not registered in schema");
                }

                foreach (var dep in deps)
                {
                    if (!schema.IsRegistered(dep))
                    {
                        errors.Add($"{type.Name} depends on {dep.Name} which is not registered in schema");
                    }
                }
            }

            foreach (var registeredType in schema.RegisteredTypes)
            {
                if (!_dependencies.ContainsKey(registeredType))
                {
                    warnings.Add($"Type {registeredType.Name} is registered but has no dependency information");
                }
            }

            return new DependencyValidationResult(errors.Count == 0, errors, warnings);
        }

        private void InvalidateCache() => _cachedLoadOrder = null;
    }

    public sealed record DependencyValidationResult(
        bool IsValid,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Warnings);

    public sealed class CircularDependencyException : InvalidOperationException
    {
        public Type InvolvedType { get; }

        public CircularDependencyException(Type type)
            : base($"Circular dependency detected involving {type.Name}")
        {
            InvolvedType = type;
        }
    }

    #endregion

    // =========================================================================
    // PART 4: SCHEMA IMPLEMENTATION
    // =========================================================================

    #region Schema Implementation

    /// <summary>
    /// In-memory implementation of IFactSchema.
    /// </summary>
    public class FactSchema : IFactSchema
    {
        private readonly Dictionary<Type, FactTypeSchema> _typeSchemas = new();
        private readonly DependencyGraph _dependencyGraph = new();

        public IReadOnlySet<Type> RegisteredTypes => _typeSchemas.Keys.ToHashSet();
        public DependencyGraph DependencyGraph => _dependencyGraph;

        public void RegisterType<T>(Action<FactTypeSchemaBuilder<T>>? configure = null) where T : class
        {
            var builder = new FactTypeSchemaBuilder<T>(this);
            configure?.Invoke(builder);
            var schema = builder.Build();
            _typeSchemas[typeof(T)] = schema;
            _dependencyGraph.AddFactType(typeof(T));
        }

        public IFactTypeSchema? GetFactTypeSchema(Type type)
        {
            return _typeSchemas.TryGetValue(type, out var schema) ? schema : null;
        }

        public bool IsRegistered(Type type) => _typeSchemas.ContainsKey(type);

        public void ValidateType(Type type)
        {
            if (!IsRegistered(type))
            {
                throw new FactTypeNotRegisteredException(type);
            }
        }

        internal void AddNavigationDependency(Type sourceType, Type targetType)
        {
            _dependencyGraph.AddDependency(sourceType, targetType);
        }
    }

    public sealed class FactTypeNotRegisteredException : InvalidOperationException
    {
        public Type FactType { get; }

        public FactTypeNotRegisteredException(Type type)
            : base($"Type {type.Name} is not registered in the schema. " +
                   $"Call schema.RegisterType<{type.Name}>() in OnModelCreating.")
        {
            FactType = type;
        }
    }

    /// <summary>
    /// Schema information for a single fact type.
    /// </summary>
    public sealed class FactTypeSchema : IFactTypeSchema
    {
        public Type FactType { get; init; } = null!;
        public string? KeyProperty { get; init; }
        public IReadOnlyDictionary<string, NavigationInfo> Navigations { get; init; }
            = new Dictionary<string, NavigationInfo>();
        public IReadOnlyList<FactRelationship> Relationships { get; init; }
            = Array.Empty<FactRelationship>();
    }

    /// <summary>
    /// Builder for configuring a fact type's schema.
    /// </summary>
    public sealed class FactTypeSchemaBuilder<T> where T : class
    {
        private readonly FactSchema _schema;
        private string? _keyProperty;
        private readonly Dictionary<string, NavigationInfo> _navigations = new();
        private readonly List<FactRelationship> _relationships = new();

        public FactTypeSchemaBuilder(FactSchema schema)
        {
            _schema = schema;
        }

        public FactTypeSchemaBuilder<T> HasKey<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _keyProperty = GetPropertyName(keySelector);
            return this;
        }

        public NavigationBuilder<T, TTarget> HasOne<TTarget>(
            Expression<Func<T, TTarget?>> navigationSelector) where TTarget : class
        {
            var propertyName = GetPropertyName(navigationSelector);
            return new NavigationBuilder<T, TTarget>(this, _schema, propertyName, isCollection: false);
        }

        public NavigationBuilder<T, TTarget> HasMany<TTarget>(
            Expression<Func<T, IEnumerable<TTarget>>> navigationSelector) where TTarget : class
        {
            var propertyName = GetPropertyName(navigationSelector);
            return new NavigationBuilder<T, TTarget>(this, _schema, propertyName, isCollection: true);
        }

        internal void AddNavigation(NavigationInfo info, FactRelationship relationship)
        {
            _navigations[info.PropertyName] = info;
            _relationships.Add(relationship);
        }

        internal FactTypeSchema Build()
        {
            return new FactTypeSchema
            {
                FactType = typeof(T),
                KeyProperty = _keyProperty,
                Navigations = new Dictionary<string, NavigationInfo>(_navigations),
                Relationships = _relationships.ToList()
            };
        }

        private static string GetPropertyName<TProperty>(Expression<Func<T, TProperty>> selector)
        {
            if (selector.Body is MemberExpression member)
                return member.Member.Name;
            throw new ArgumentException("Expected property selector expression");
        }
    }

    /// <summary>
    /// Builder for navigation properties.
    /// </summary>
    public sealed class NavigationBuilder<TSource, TTarget>
        where TSource : class
        where TTarget : class
    {
        private readonly FactTypeSchemaBuilder<TSource> _parent;
        private readonly FactSchema _schema;
        private readonly string _propertyName;
        private readonly bool _isCollection;
        private string? _foreignKeyProperty;
        private bool _isOptional;

        internal NavigationBuilder(
            FactTypeSchemaBuilder<TSource> parent,
            FactSchema schema,
            string propertyName,
            bool isCollection)
        {
            _parent = parent;
            _schema = schema;
            _propertyName = propertyName;
            _isCollection = isCollection;
        }

        public NavigationBuilder<TSource, TTarget> WithForeignKey<TKey>(
            Expression<Func<TSource, TKey>> foreignKeySelector)
        {
            _foreignKeyProperty = GetPropertyName(foreignKeySelector);
            return this;
        }

        public NavigationBuilder<TSource, TTarget> IsOptional()
        {
            _isOptional = true;
            return this;
        }

        public FactTypeSchemaBuilder<TSource> WithMany(
            Expression<Func<TTarget, IEnumerable<TSource>>>? inverseSelector = null)
        {
            Complete();
            return _parent;
        }

        public FactTypeSchemaBuilder<TSource> WithOne(
            Expression<Func<TTarget, TSource?>>? inverseSelector = null)
        {
            Complete();
            return _parent;
        }

        private void Complete()
        {
            var navInfo = new NavigationInfo
            {
                PropertyName = _propertyName,
                TargetType = typeof(TTarget),
                ForeignKeyProperty = _foreignKeyProperty ?? string.Empty,
                IsCollection = _isCollection,
                IsOptional = _isOptional
            };

            var relationship = new FactRelationship
            {
                SourceType = typeof(TSource),
                TargetType = typeof(TTarget),
                ForeignKeyProperty = _foreignKeyProperty ?? string.Empty,
                NavigationProperty = _propertyName,
                IsOptional = _isOptional
            };

            _parent.AddNavigation(navInfo, relationship);
            _schema.AddNavigationDependency(typeof(TSource), typeof(TTarget));
        }

        private static string GetPropertyName<TProperty>(Expression<Func<TSource, TProperty>> selector)
        {
            if (selector.Body is MemberExpression member)
                return member.Member.Name;
            throw new ArgumentException("Expected property selector expression");
        }
    }

    #endregion

    // =========================================================================
    // PART 5: DEPENDENCY ANALYSIS RESULT
    // =========================================================================

    #region Dependency Analysis

    /// <summary>
    /// Result of analyzing a rule expression for dependencies.
    /// </summary>
    public sealed class DependencyAnalysisResult
    {
        public IReadOnlySet<Type> FactTypeDependencies { get; init; } = new HashSet<Type>();
        public IReadOnlyList<string> NavigationPaths { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public bool HasDependencies => FactTypeDependencies.Count > 0;

        public static DependencyAnalysisResult Empty { get; } = new();

        public DependencyAnalysisResult Merge(DependencyAnalysisResult other)
        {
            var mergedDeps = new HashSet<Type>(FactTypeDependencies);
            foreach (var dep in other.FactTypeDependencies)
                mergedDeps.Add(dep);

            return new DependencyAnalysisResult
            {
                FactTypeDependencies = mergedDeps,
                NavigationPaths = NavigationPaths.Concat(other.NavigationPaths).ToList(),
                Warnings = Warnings.Concat(other.Warnings).ToList()
            };
        }
    }

    #endregion

    // =========================================================================
    // PART 6: EXPRESSION DEPENDENCY EXTRACTOR
    // =========================================================================

    #region Dependency Extractor

    /// <summary>
    /// Extracts fact type dependencies from rule expressions.
    /// Handles three patterns:
    /// 1. Navigation properties (m.From.Role) - cross-referenced with schema
    /// 2. Context parameter calls (ctx.Facts&lt;T&gt;()) - direct method detection
    /// 3. Captured closures (context.Facts&lt;T&gt;()) - closure + method detection
    /// </summary>
    public sealed class DependencyExtractor : ExpressionVisitor
    {
        private readonly IFactSchema _schema;
        private readonly HashSet<Type> _detectedDependencies = new();
        private readonly List<string> _navigationPaths = new();
        private readonly List<string> _warnings = new();

        public DependencyExtractor(IFactSchema schema)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        /// <summary>
        /// Analyze an expression and extract all fact type dependencies.
        /// </summary>
        public DependencyAnalysisResult Analyze(Expression expression)
        {
            _detectedDependencies.Clear();
            _navigationPaths.Clear();
            _warnings.Clear();

            Visit(expression);

            return new DependencyAnalysisResult
            {
                FactTypeDependencies = new HashSet<Type>(_detectedDependencies),
                NavigationPaths = _navigationPaths.ToList(),
                Warnings = _warnings.ToList()
            };
        }

        /// <summary>
        /// Pattern 2 &amp; 3: Detect Facts&lt;T&gt;() and FindByKey&lt;T&gt;() calls.
        /// </summary>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (IsFactContextMethod(node, "Facts") || IsFactContextMethod(node, "FindByKey"))
            {
                var factType = node.Method.GetGenericArguments()[0];

                // Design decision #2: Throw if not registered
                if (!_schema.IsRegistered(factType))
                {
                    throw new FactTypeNotRegisteredException(factType);
                }

                _detectedDependencies.Add(factType);
            }

            return base.VisitMethodCall(node);
        }

        /// <summary>
        /// Pattern 1: Detect navigation property access.
        /// </summary>
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member is PropertyInfo property)
            {
                var declaringType = node.Expression?.Type;

                if (declaringType != null && _schema.IsRegistered(declaringType))
                {
                    var typeSchema = _schema.GetFactTypeSchema(declaringType);

                    if (typeSchema?.Navigations.TryGetValue(property.Name, out var navInfo) == true)
                    {
                        _detectedDependencies.Add(navInfo.TargetType);
                        _navigationPaths.Add($"{declaringType.Name}.{property.Name} -> {navInfo.TargetType.Name}");
                    }
                }
            }

            return base.VisitMember(node);
        }

        /// <summary>
        /// Pattern 3: Detect captured closures containing IFactContext.
        /// </summary>
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value != null && IsClosureClass(node.Value.GetType()))
            {
                AnalyzeClosureFields(node.Value, node.Value.GetType());
            }

            return base.VisitConstant(node);
        }

        private bool IsFactContextMethod(MethodCallExpression node, string methodName)
        {
            if (!node.Method.IsGenericMethod) return false;
            if (node.Method.Name != methodName) return false;

            var declaringType = node.Method.DeclaringType;
            if (declaringType == null) return false;

            return typeof(IFactContext).IsAssignableFrom(declaringType) ||
                   declaringType.GetInterfaces().Any(i => i == typeof(IFactContext));
        }

        private static bool IsClosureClass(Type type)
        {
            return type.Name.Contains("<>") ||
                   type.Name.Contains("DisplayClass") ||
                   type.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Any();
        }

        private void AnalyzeClosureFields(object closure, Type closureType)
        {
            foreach (var field in closureType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(IFactContext).IsAssignableFrom(field.FieldType))
                {
                    _warnings.Add($"Detected captured IFactContext in closure field: {field.Name}");
                }

                // Handle nested closures
                if (IsClosureClass(field.FieldType))
                {
                    var nestedClosure = field.GetValue(closure);
                    if (nestedClosure != null)
                    {
                        AnalyzeClosureFields(nestedClosure, field.FieldType);
                    }
                }
            }
        }
    }

    #endregion

    // =========================================================================
    // PART 7: CROSS-FACT RULE IMPLEMENTATION
    // =========================================================================

    #region CrossFactRule

    /// <summary>
    /// Rule that supports dependencies on other fact types.
    /// Supports both explicit declaration and implicit detection.
    /// </summary>
    public class CrossFactRule<T> : ICrossFactRule<T> where T : class
    {
        private readonly string _id;
        private readonly string _name;
        private readonly Expression<Func<T, bool>>? _simpleCondition;
        private readonly Expression<Func<T, IFactContext, bool>>? _contextCondition;
        private Action<T>? _simpleAction;
        private Action<T, IFactContext>? _contextAction;

        private readonly HashSet<Type> _explicitDependencies = new();
        private DependencyAnalysisResult? _analysisResult;
        private Func<T, bool>? _compiledSimpleCondition;
        private Func<T, IFactContext, bool>? _compiledContextCondition;

        private int _priority;
        private readonly HashSet<string> _tags = new();
        private string? _reason;

        public CrossFactRule(string id, string name, Expression<Func<T, bool>> condition)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _simpleCondition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public CrossFactRule(string id, string name, Expression<Func<T, IFactContext, bool>> condition)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _contextCondition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        // IRule<T> implementation
        public string Id => _id;
        public string Name => _name;
        public int Priority => _priority;
        public IReadOnlySet<string> Tags => _tags;

        public Expression<Func<T, bool>> Condition =>
            _simpleCondition ?? throw new InvalidOperationException(
                "This rule uses a context-aware condition. Use EvaluateWithContext instead.");

        // ICrossFactRule<T> implementation
        public IReadOnlySet<Type> Dependencies
        {
            get
            {
                var all = new HashSet<Type>(_explicitDependencies);
                if (_analysisResult != null)
                {
                    foreach (var dep in _analysisResult.FactTypeDependencies)
                        all.Add(dep);
                }
                return all;
            }
        }

        public bool RequiresContext => _contextCondition != null || _contextAction != null;

        // Fluent configuration

        /// <summary>
        /// Explicitly declare a dependency on a fact type.
        /// </summary>
        public CrossFactRule<T> DependsOn<TFact>() where TFact : class
        {
            _explicitDependencies.Add(typeof(TFact));
            return this;
        }

        /// <summary>
        /// Add an additional context-aware condition.
        /// </summary>
        public CrossFactRule<T> And(Expression<Func<T, IFactContext, bool>> condition)
        {
            // TODO: Combine with existing condition
            // For now, replace (should chain with AND logic)
            throw new NotImplementedException(
                "Combining conditions not yet implemented. " +
                "Use constructor with context condition instead.");
        }

        public CrossFactRule<T> WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        public CrossFactRule<T> WithTags(params string[] tags)
        {
            foreach (var tag in tags)
                _tags.Add(tag);
            return this;
        }

        public CrossFactRule<T> WithReason(string reason)
        {
            _reason = reason;
            return this;
        }

        public CrossFactRule<T> Then(Action<T> action)
        {
            _simpleAction = action;
            return this;
        }

        public CrossFactRule<T> Then(Action<T, IFactContext> action)
        {
            _contextAction = action;
            return this;
        }

        /// <summary>
        /// Analyze expressions and detect implicit dependencies.
        /// Called at rule registration time.
        /// </summary>
        public void AnalyzeDependencies(IFactSchema schema)
        {
            var extractor = new DependencyExtractor(schema);
            var result = DependencyAnalysisResult.Empty;

            if (_simpleCondition != null)
            {
                result = extractor.Analyze(_simpleCondition);
            }

            if (_contextCondition != null)
            {
                result = result.Merge(extractor.Analyze(_contextCondition));
            }

            _analysisResult = result;

            // Validate explicit dependencies are registered
            foreach (var dep in _explicitDependencies)
            {
                schema.ValidateType(dep);
            }
        }

        // Evaluation methods

        public bool Evaluate(T fact)
        {
            if (_simpleCondition == null)
                throw new InvalidOperationException(
                    "This rule requires context. Use EvaluateWithContext instead.");

            _compiledSimpleCondition ??= _simpleCondition.Compile();
            return _compiledSimpleCondition(fact);
        }

        public bool EvaluateWithContext(T fact, IFactContext context)
        {
            // First check simple condition if present
            if (_simpleCondition != null)
            {
                _compiledSimpleCondition ??= _simpleCondition.Compile();
                if (!_compiledSimpleCondition(fact))
                    return false;
            }

            // Then check context condition if present
            if (_contextCondition != null)
            {
                _compiledContextCondition ??= _contextCondition.Compile();
                return _compiledContextCondition(fact, context);
            }

            return true;
        }

        public RuleResult Execute(T fact)
        {
            if (_simpleAction != null)
            {
                _simpleAction(fact);
            }
            return RuleResult.Success(_id);
        }

        public RuleResult ExecuteWithContext(T fact, IFactContext context)
        {
            if (_contextAction != null)
            {
                _contextAction(fact, context);
            }
            else if (_simpleAction != null)
            {
                _simpleAction(fact);
            }
            return RuleResult.Success(_id);
        }
    }

    #endregion

    // =========================================================================
    // PART 8: STATIC RULE FACTORY
    // =========================================================================

    #region Rule Factory

    /// <summary>
    /// Static factory for creating cross-fact rules with fluent API.
    /// </summary>
    public static class CrossFactRuleFactory
    {
        private static int _autoIdCounter;

        /// <summary>
        /// Create a rule with simple condition.
        /// </summary>
        public static CrossFactRule<T> When<T>(Expression<Func<T, bool>> condition) where T : class
        {
            var id = $"rule-{Interlocked.Increment(ref _autoIdCounter)}";
            return new CrossFactRule<T>(id, id, condition);
        }

        /// <summary>
        /// Create a rule with context-aware condition.
        /// </summary>
        public static CrossFactRule<T> When<T>(
            Expression<Func<T, IFactContext, bool>> condition) where T : class
        {
            var id = $"rule-{Interlocked.Increment(ref _autoIdCounter)}";
            return new CrossFactRule<T>(id, id, condition);
        }

        /// <summary>
        /// Create a named rule with simple condition.
        /// </summary>
        public static CrossFactRule<T> Create<T>(
            string id,
            string name,
            Expression<Func<T, bool>> condition) where T : class
        {
            return new CrossFactRule<T>(id, name, condition);
        }

        /// <summary>
        /// Create a named rule with context-aware condition.
        /// </summary>
        public static CrossFactRule<T> Create<T>(
            string id,
            string name,
            Expression<Func<T, IFactContext, bool>> condition) where T : class
        {
            return new CrossFactRule<T>(id, name, condition);
        }
    }

    #endregion

    // =========================================================================
    // PART 9: FACT CONTEXT IMPLEMENTATION
    // =========================================================================

    #region FactContext Implementation

    /// <summary>
    /// In-memory implementation of IFactContext for rule evaluation.
    /// </summary>
    public class InMemoryFactContext : IFactContext
    {
        private readonly IFactSchema _schema;
        private readonly Dictionary<Type, object> _factSets = new();

        public InMemoryFactContext(IFactSchema schema)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public IReadOnlySet<Type> RegisteredFactTypes => _schema.RegisteredTypes;

        public IQueryable<T> Facts<T>() where T : class
        {
            _schema.ValidateType(typeof(T));

            if (!_factSets.TryGetValue(typeof(T), out var set))
            {
                set = new List<T>();
                _factSets[typeof(T)] = set;
            }

            return ((List<T>)set).AsQueryable();
        }

        public T? FindByKey<T>(object key) where T : class
        {
            _schema.ValidateType(typeof(T));

            var typeSchema = _schema.GetFactTypeSchema(typeof(T));
            if (typeSchema?.KeyProperty == null)
            {
                throw new InvalidOperationException(
                    $"Type {typeof(T).Name} does not have a key property configured.");
            }

            var keyProperty = typeof(T).GetProperty(typeSchema.KeyProperty);
            if (keyProperty == null)
            {
                throw new InvalidOperationException(
                    $"Key property {typeSchema.KeyProperty} not found on {typeof(T).Name}.");
            }

            return Facts<T>().FirstOrDefault(f =>
                Equals(keyProperty.GetValue(f), key));
        }

        /// <summary>
        /// Add facts to the context.
        /// </summary>
        public void AddFacts<T>(IEnumerable<T> facts) where T : class
        {
            _schema.ValidateType(typeof(T));

            if (!_factSets.TryGetValue(typeof(T), out var set))
            {
                set = new List<T>();
                _factSets[typeof(T)] = set;
            }

            ((List<T>)set).AddRange(facts);
        }

        /// <summary>
        /// Add a single fact to the context.
        /// </summary>
        public void AddFact<T>(T fact) where T : class
        {
            _schema.ValidateType(typeof(T));

            if (!_factSets.TryGetValue(typeof(T), out var set))
            {
                set = new List<T>();
                _factSets[typeof(T)] = set;
            }

            ((List<T>)set).Add(fact);
        }
    }

    #endregion

    // =========================================================================
    // PART 10: NAVIGATION RESOLVER
    // =========================================================================

    #region Navigation Resolver

    /// <summary>
    /// Resolves navigation properties based on schema configuration.
    /// Generic implementation not tied to specific domain types.
    /// </summary>
    public class NavigationResolver
    {
        private readonly IFactSchema _schema;
        private readonly DependencyGraph _dependencyGraph;

        public NavigationResolver(IFactSchema schema, DependencyGraph dependencyGraph)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _dependencyGraph = dependencyGraph ?? throw new ArgumentNullException(nameof(dependencyGraph));
        }

        /// <summary>
        /// Resolve all navigation properties in the context.
        /// Processes types in dependency order.
        /// </summary>
        public void ResolveAll(InMemoryFactContext context)
        {
            var order = _dependencyGraph.GetLoadOrder();

            foreach (var type in order)
            {
                ResolveNavigationsForType(type, context);
            }
        }

        private void ResolveNavigationsForType(Type type, InMemoryFactContext context)
        {
            var typeSchema = _schema.GetFactTypeSchema(type);
            if (typeSchema == null) return;

            // Get facts via reflection (since we don't know T at compile time)
            var factsMethod = typeof(InMemoryFactContext)
                .GetMethod(nameof(InMemoryFactContext.Facts))!
                .MakeGenericMethod(type);

            var queryable = factsMethod.Invoke(context, null);
            if (queryable == null) return;

            // Convert to list for enumeration
            var toListMethod = typeof(Enumerable)
                .GetMethod(nameof(Enumerable.ToList))!
                .MakeGenericMethod(type);

            var facts = (System.Collections.IEnumerable)toListMethod.Invoke(null, new[] { queryable })!;

            foreach (var fact in facts)
            {
                ResolveNavigationsForFact(fact, typeSchema, context);
            }
        }

        private void ResolveNavigationsForFact(object fact, IFactTypeSchema typeSchema, InMemoryFactContext context)
        {
            foreach (var nav in typeSchema.Navigations.Values)
            {
                if (string.IsNullOrEmpty(nav.ForeignKeyProperty)) continue;

                // Get foreign key value
                var fkProperty = fact.GetType().GetProperty(nav.ForeignKeyProperty);
                if (fkProperty == null) continue;

                var fkValue = fkProperty.GetValue(fact);
                if (fkValue == null && nav.IsOptional) continue;
                if (fkValue == null) continue;

                // Find target by key
                var findMethod = typeof(InMemoryFactContext)
                    .GetMethod(nameof(InMemoryFactContext.FindByKey))!
                    .MakeGenericMethod(nav.TargetType);

                var target = findMethod.Invoke(context, new[] { fkValue });

                // Set navigation property
                var navProperty = fact.GetType().GetProperty(nav.PropertyName);
                if (navProperty != null && target != null)
                {
                    navProperty.SetValue(fact, target);
                }
            }
        }
    }

    #endregion

    // =========================================================================
    // PART 11: CROSS-FACT RULE SET
    // =========================================================================

    #region CrossFactRuleSet

    /// <summary>
    /// Rule set that tracks cross-fact dependencies.
    /// Analyzes rules at registration time.
    /// </summary>
    public class CrossFactRuleSet<T> where T : class
    {
        private readonly List<ICrossFactRule<T>> _rules = new();
        private readonly IFactSchema _schema;
        private readonly DependencyGraph _ruleDependencies = new();

        public CrossFactRuleSet(IFactSchema schema)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public IReadOnlyList<ICrossFactRule<T>> Rules => _rules;

        /// <summary>
        /// Add a rule. Analyzes dependencies at registration time.
        /// </summary>
        public void Add(ICrossFactRule<T> rule)
        {
            // Analyze dependencies (design decision #2: fail fast)
            rule.AnalyzeDependencies(_schema);

            // Track dependencies
            foreach (var dep in rule.Dependencies)
            {
                _ruleDependencies.AddDependency(typeof(T), dep);
            }

            _rules.Add(rule);
        }

        /// <summary>
        /// Get rules ordered by priority (descending).
        /// </summary>
        public IEnumerable<ICrossFactRule<T>> GetOrderedRules()
        {
            return _rules.OrderByDescending(r => r.Priority);
        }

        /// <summary>
        /// Get all fact type dependencies for rules in this set.
        /// </summary>
        public IReadOnlySet<Type> GetAllDependencies()
        {
            var deps = new HashSet<Type>();
            foreach (var rule in _rules)
            {
                foreach (var dep in rule.Dependencies)
                    deps.Add(dep);
            }
            return deps;
        }
    }

    #endregion

    // =========================================================================
    // PART 12: TODO - INTEGRATION WITH EXISTING RULESCONTEXT
    // =========================================================================

    #region Integration TODOs

    // TODO: Integrate CrossFactRuleSet with existing IRuleSet<T>
    // - Option A: Make IRuleSet<T> support ICrossFactRule<T>
    // - Option B: Create new ICrossFactRuleSet<T> interface
    // - Option C: Adapter pattern

    // TODO: Integrate FactSchema with existing RulesContext
    // - RulesContext should hold a FactSchema
    // - OnModelCreating should configure the schema
    // - CreateSession should use schema for validation

    // TODO: Integrate NavigationResolver with RuleSession
    // - Session.Evaluate should call resolver before rule evaluation
    // - Session should implement IFactContext

    // TODO: Add support for the proposed API patterns:
    // - context.Rules<T>() - query rules
    // - context.Agents() - shorthand for context.Facts<Agent>()
    // - Pipeline builder (Validate/Transform/Route/Log)
    // - WouldRoute preview

    // TODO: Self-referential dependency handling
    // - Message rule querying Message history
    // - Need to track separately or warn

    // TODO: Performance optimizations
    // - Cache navigation resolution
    // - Parallel rule evaluation when dependencies allow
    // - Expression compilation caching (partially done)

    #endregion
}
