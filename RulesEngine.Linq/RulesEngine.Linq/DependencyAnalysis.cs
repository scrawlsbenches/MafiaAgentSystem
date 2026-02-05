// =============================================================================
// Dependency Analysis for RulesEngine.Linq
// =============================================================================
// This file implements dependency tracking and expression analysis for cross-fact
// rule evaluation. It enables rules to query facts of different types while
// maintaining a reliable dependency graph for evaluation ordering.
//
// Status: Implementation in progress - contains TODOs for incomplete sections
// =============================================================================
//
// KEY CONCEPTS:
// - Schema defines what fact types exist and their relationships
// - Rules declare (explicitly) or reveal (implicitly) their dependencies
// - DependencyGraph tracks all dependencies and provides evaluation order
// - Expression analysis detects implicit dependencies at registration time
//
// =============================================================================

namespace RulesEngine.Linq.Dependencies
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using RulesEngine.Linq; // For FactQueryExpression

    #region Interfaces

    /// <summary>
    /// Provides access to facts during rule evaluation.
    /// Rules receive this as a parameter to query other fact types.
    /// </summary>
    public interface IFactContext
    {
        /// <summary>
        /// Query facts of type T. The type must be registered in the schema.
        /// </summary>
        IQueryable<T> Facts<T>() where T : class;

        /// <summary>
        /// Find a single fact by its key. Returns null if not found.
        /// </summary>
        T? FindByKey<T>(object key) where T : class;

        /// <summary>
        /// Get all registered fact types in the schema.
        /// </summary>
        IReadOnlySet<Type> RegisteredFactTypes { get; }
    }

    /// <summary>
    /// Result of dependency analysis on a rule expression.
    /// </summary>
    public class DependencyAnalysisResult
    {
        public IReadOnlySet<Type> FactTypeDependencies { get; init; } = new HashSet<Type>();
        public IReadOnlyList<string> NavigationPaths { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public bool HasDependencies => FactTypeDependencies.Count > 0;

        public static DependencyAnalysisResult Empty { get; } = new();
    }

    /// <summary>
    /// Schema information for a single fact type, used during analysis.
    /// </summary>
    public interface IFactTypeSchema
    {
        Type FactType { get; }
        string? KeyProperty { get; }
        IReadOnlyDictionary<string, NavigationInfo> Navigations { get; }
    }

    /// <summary>
    /// Information about a navigation property.
    /// </summary>
    public class NavigationInfo
    {
        public string PropertyName { get; init; } = string.Empty;
        public Type TargetType { get; init; } = null!;
        public string ForeignKeyProperty { get; init; } = string.Empty;
        public bool IsCollection { get; init; }
        public bool IsOptional { get; init; }
    }

    /// <summary>
    /// Full schema containing all registered fact types and their relationships.
    /// </summary>
    public interface IFactSchema
    {
        IReadOnlySet<Type> RegisteredTypes { get; }
        IFactTypeSchema? GetFactTypeSchema(Type type);
        bool IsRegistered(Type type);
        void ValidateType(Type type); // Throws if not registered
    }

    #endregion

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
        /// Register a fact type with its dependencies.
        /// </summary>
        public void AddFactType(Type factType, IEnumerable<Type>? dependsOn = null)
        {
            InvalidateCache();

            if (!_dependencies.ContainsKey(factType))
                _dependencies[factType] = new HashSet<Type>();

            if (dependsOn == null) return;

            foreach (var dep in dependsOn)
            {
                _dependencies[factType].Add(dep);

                // Ensure dependency type is also in the graph (with no deps of its own yet)
                if (!_dependencies.ContainsKey(dep))
                    _dependencies[dep] = new HashSet<Type>();

                if (!_dependents.ContainsKey(dep))
                    _dependents[dep] = new HashSet<Type>();
                _dependents[dep].Add(factType);
            }
        }

        /// <summary>
        /// Add a dependency from one type to another.
        /// </summary>
        public void AddDependency(Type fromType, Type toType)
        {
            InvalidateCache();

            if (!_dependencies.ContainsKey(fromType))
                _dependencies[fromType] = new HashSet<Type>();
            _dependencies[fromType].Add(toType);

            if (!_dependents.ContainsKey(toType))
                _dependents[toType] = new HashSet<Type>();
            _dependents[toType].Add(fromType);
        }

        /// <summary>
        /// Get the order in which fact types should be loaded/resolved.
        /// Dependencies come before dependents (topological sort).
        /// Returns a read-only list that supports IndexOf.
        /// </summary>
        public IList<Type> GetLoadOrder()
        {
            if (_cachedLoadOrder != null)
                return _cachedLoadOrder.AsReadOnly();

            var result = new List<Type>();
            var visited = new HashSet<Type>();
            var visiting = new HashSet<Type>();

            foreach (var type in _dependencies.Keys)
            {
                TopologicalVisit(type, visited, visiting, result);
            }

            _cachedLoadOrder = result;
            return result.AsReadOnly();
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
                throw new InvalidOperationException(
                    $"Circular dependency detected involving {type.Name}. " +
                    $"Check the dependency chain for cycles.");
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
        /// Check if adding a dependency would create a cycle.
        /// </summary>
        public bool WouldCreateCycle(Type fromType, Type toType)
        {
            // If toType already depends on fromType (directly or transitively),
            // adding fromType -> toType would create a cycle
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
        /// Validate the graph for completeness and consistency.
        /// </summary>
        public DependencyValidationResult Validate(IFactSchema schema)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Check all dependencies are registered
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

            // Check for orphan types (registered but no dependencies tracked)
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

    public record DependencyValidationResult(
        bool IsValid,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Warnings);

    #endregion

    #region Expression Analysis

    /// <summary>
    /// Extracts fact type dependencies from rule expressions.
    /// Handles three patterns:
    /// 1. Navigation properties (m.From.Role) - cross-referenced with schema
    /// 2. Context parameter calls (ctx.Facts&lt;T&gt;()) - direct method detection
    /// 3. Captured closures (context.Facts&lt;T&gt;()) - closure + method detection
    /// </summary>
    public class DependencyExtractor : ExpressionVisitor
    {
        private readonly IFactSchema _schema;
        private readonly HashSet<Type> _detectedDependencies = new();
        private readonly List<string> _navigationPaths = new();
        private readonly List<string> _warnings = new();

        // Track the current parameter type for navigation analysis
        private Type? _currentFactType;

        public DependencyExtractor(IFactSchema schema)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        /// <summary>
        /// Analyze an expression and extract all fact type dependencies.
        /// </summary>
        public DependencyAnalysisResult Analyze<TFact>(Expression expression) where TFact : class
        {
            _currentFactType = typeof(TFact);
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
        /// Detect FactQueryExpression nodes (closure-captured Facts&lt;T&gt;() calls).
        /// These nodes are created when a rule captures context.Facts&lt;T&gt;() in a closure.
        /// </summary>
        protected override Expression VisitExtension(Expression node)
        {
            if (node is FactQueryExpression fqe)
            {
                // Validate against schema
                if (!_schema.IsRegistered(fqe.FactType))
                {
                    throw new InvalidOperationException(
                        $"Rule references Facts<{fqe.FactType.Name}>() but {fqe.FactType.Name} is not registered in the schema. " +
                        $"Register it in OnModelCreating before using it in rules.");
                }

                _detectedDependencies.Add(fqe.FactType);
            }
            return base.VisitExtension(node);
        }

        /// <summary>
        /// Detect ctx.Facts&lt;T&gt;() method calls.
        /// </summary>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Pattern 2 & 3: Detect Facts<T>() calls
            if (IsFactsMethodCall(node))
            {
                var factType = node.Method.GetGenericArguments()[0];

                // Validate against schema
                if (!_schema.IsRegistered(factType))
                {
                    throw new InvalidOperationException(
                        $"Rule references Facts<{factType.Name}>() but {factType.Name} is not registered in the schema. " +
                        $"Register it in OnModelCreating before using it in rules.");
                }

                _detectedDependencies.Add(factType);
            }

            // TODO: Detect other IFactContext methods like FindByKey<T>()
            if (IsFindByKeyMethodCall(node))
            {
                var factType = node.Method.GetGenericArguments()[0];

                if (!_schema.IsRegistered(factType))
                {
                    throw new InvalidOperationException(
                        $"Rule references FindByKey<{factType.Name}>() but {factType.Name} is not registered in the schema.");
                }

                _detectedDependencies.Add(factType);
            }

            return base.VisitMethodCall(node);
        }

        /// <summary>
        /// Detect navigation property access (m.From, m.Territory, etc.)
        /// </summary>
        protected override Expression VisitMember(MemberExpression node)
        {
            // Pattern 1: Navigation property detection
            // We need to check if this member access is a navigation property
            // by cross-referencing with the schema

            if (node.Member is PropertyInfo property)
            {
                var declaringType = node.Expression?.Type;

                if (declaringType != null && _schema.IsRegistered(declaringType))
                {
                    var typeSchema = _schema.GetFactTypeSchema(declaringType);

                    if (typeSchema?.Navigations.TryGetValue(property.Name, out var navInfo) == true)
                    {
                        // This is a navigation property - add the target type as dependency
                        _detectedDependencies.Add(navInfo.TargetType);
                        _navigationPaths.Add($"{declaringType.Name}.{property.Name} -> {navInfo.TargetType.Name}");
                    }
                }
            }

            return base.VisitMember(node);
        }

        /// <summary>
        /// Handle closures - detect captured variables that implement IFactContext
        /// or FactQueryable instances.
        /// </summary>
        protected override Expression VisitConstant(ConstantExpression node)
        {
            // Pattern 3: Captured closure detection
            // When a lambda captures a variable, it becomes a field on a compiler-generated class
            // We need to identify when that captured variable implements IFactContext

            if (node.Value != null)
            {
                var type = node.Value.GetType();

                // Check for FactQueryable<T> instances (closure-captured context.Facts<T>())
                if (type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(FactQueryable<>))
                {
                    var factType = type.GetGenericArguments()[0];
                    if (!_schema.IsRegistered(factType))
                    {
                        throw new InvalidOperationException(
                            $"Rule references Facts<{factType.Name}>() but {factType.Name} is not registered in the schema. " +
                            $"Register it in OnModelCreating before using it in rules.");
                    }
                    _detectedDependencies.Add(factType);
                }

                // Check if this is a closure class (compiler-generated)
                if (IsClosureClass(type))
                {
                    AnalyzeClosureFields(node.Value, type);
                }
            }

            return base.VisitConstant(node);
        }

        private bool IsFactsMethodCall(MethodCallExpression node)
        {
            if (!node.Method.IsGenericMethod) return false;
            if (node.Method.Name != "Facts") return false;

            // Check if it's on IFactContext or a type that implements it
            var declaringType = node.Method.DeclaringType;
            if (declaringType == null) return false;

            return typeof(IFactContext).IsAssignableFrom(declaringType) ||
                   declaringType.GetInterfaces().Any(i => i == typeof(IFactContext));
        }

        private bool IsFindByKeyMethodCall(MethodCallExpression node)
        {
            if (!node.Method.IsGenericMethod) return false;
            if (node.Method.Name != "FindByKey") return false;

            var declaringType = node.Method.DeclaringType;
            if (declaringType == null) return false;

            return typeof(IFactContext).IsAssignableFrom(declaringType) ||
                   declaringType.GetInterfaces().Any(i => i == typeof(IFactContext));
        }

        private bool IsClosureClass(Type type)
        {
            // Compiler-generated closure classes have specific naming patterns
            // In C#, they're typically named like <>c__DisplayClass or similar
            return type.Name.Contains("<>") ||
                   type.Name.Contains("DisplayClass") ||
                   type.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Any();
        }

        private void AnalyzeClosureFields(object closure, Type closureType)
        {
            // Look for fields that implement IFactContext
            foreach (var field in closureType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(IFactContext).IsAssignableFrom(field.FieldType))
                {
                    // Found a captured IFactContext - we'll detect its usage via VisitMethodCall
                    // Just note it for now
                    _warnings.Add($"Detected captured IFactContext in closure field: {field.Name}");
                }

                // TODO: Handle nested closures (closure capturing another closure)
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

    #region Schema Implementation

    /// <summary>
    /// In-memory implementation of IFactSchema.
    /// Built by RulesModelBuilder during OnModelCreating.
    /// </summary>
    public class FactSchema : IFactSchema
    {
        private readonly Dictionary<Type, FactTypeSchema> _typeSchemas = new();

        public IReadOnlySet<Type> RegisteredTypes => _typeSchemas.Keys.ToHashSet();

        public void RegisterType<T>(Action<FactTypeSchemaBuilder<T>>? configure = null) where T : class
        {
            var builder = new FactTypeSchemaBuilder<T>();
            configure?.Invoke(builder);
            _typeSchemas[typeof(T)] = builder.Build();
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
                throw new InvalidOperationException(
                    $"Type {type.Name} is not registered in the schema. " +
                    $"Call builder.FactType<{type.Name}>() in OnModelCreating.");
            }
        }
    }

    /// <summary>
    /// Schema information for a single fact type.
    /// </summary>
    public class FactTypeSchema : IFactTypeSchema
    {
        public Type FactType { get; init; } = null!;
        public string? KeyProperty { get; init; }
        public IReadOnlyDictionary<string, NavigationInfo> Navigations { get; init; }
            = new Dictionary<string, NavigationInfo>();
    }

    /// <summary>
    /// Builder for configuring a fact type's schema.
    /// </summary>
    public class FactTypeSchemaBuilder<T> where T : class
    {
        private string? _keyProperty;
        private readonly Dictionary<string, NavigationInfo> _navigations = new();

        public FactTypeSchemaBuilder<T> HasKey<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _keyProperty = GetPropertyName(keySelector);
            return this;
        }

        public NavigationBuilder<T, TTarget> HasOne<TTarget>(
            Expression<Func<T, TTarget?>> navigationSelector) where TTarget : class
        {
            var propertyName = GetPropertyName(navigationSelector);
            return new NavigationBuilder<T, TTarget>(this, propertyName, isCollection: false);
        }

        public NavigationBuilder<T, TTarget> HasMany<TTarget>(
            Expression<Func<T, IEnumerable<TTarget>>> navigationSelector) where TTarget : class
        {
            var propertyName = GetPropertyName(navigationSelector);
            return new NavigationBuilder<T, TTarget>(this, propertyName, isCollection: true);
        }

        internal void AddNavigation(string propertyName, NavigationInfo info)
        {
            _navigations[propertyName] = info;
        }

        internal FactTypeSchema Build()
        {
            return new FactTypeSchema
            {
                FactType = typeof(T),
                KeyProperty = _keyProperty,
                Navigations = new Dictionary<string, NavigationInfo>(_navigations)
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
    /// Builder for configuring navigation properties.
    /// </summary>
    public class NavigationBuilder<TSource, TTarget>
        where TSource : class
        where TTarget : class
    {
        private readonly FactTypeSchemaBuilder<TSource> _parent;
        private readonly string _propertyName;
        private readonly bool _isCollection;
        private string? _foreignKeyProperty;
        private bool _isOptional;

        internal NavigationBuilder(
            FactTypeSchemaBuilder<TSource> parent,
            string propertyName,
            bool isCollection)
        {
            _parent = parent;
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
            _parent.AddNavigation(_propertyName, new NavigationInfo
            {
                PropertyName = _propertyName,
                TargetType = typeof(TTarget),
                ForeignKeyProperty = _foreignKeyProperty ?? string.Empty,
                IsCollection = _isCollection,
                IsOptional = _isOptional
            });
        }

        private static string GetPropertyName<TProperty>(Expression<Func<TSource, TProperty>> selector)
        {
            if (selector.Body is MemberExpression member)
                return member.Member.Name;
            throw new ArgumentException("Expected property selector expression");
        }
    }

    #endregion

    #region Rule Registration Integration

    /// <summary>
    /// Extension methods for integrating dependency analysis with rule registration.
    /// </summary>
    public static class DependencyAnalysisExtensions
    {
        /// <summary>
        /// Analyze a rule's condition expression and extract dependencies.
        /// Call this at rule registration time.
        /// </summary>
        public static DependencyAnalysisResult AnalyzeDependencies<T>(
            this Expression<Func<T, bool>> condition,
            IFactSchema schema) where T : class
        {
            var extractor = new DependencyExtractor(schema);
            return extractor.Analyze<T>(condition);
        }

        /// <summary>
        /// Analyze a context-aware rule condition and extract dependencies.
        /// </summary>
        public static DependencyAnalysisResult AnalyzeDependencies<T>(
            this Expression<Func<T, IFactContext, bool>> condition,
            IFactSchema schema) where T : class
        {
            var extractor = new DependencyExtractor(schema);
            return extractor.Analyze<T>(condition);
        }

        // TODO: Add method to update DependencyGraph based on analysis result
        // TODO: Add method to validate all rules in a RuleSet have valid dependencies
    }

    #endregion

    #region Dependent Rule

    /// <summary>
    /// A rule that declares dependencies on other fact types.
    /// Supports both explicit declaration and implicit detection.
    /// </summary>
    public class DependentRule<T> : IRule<T> where T : class
    {
        private readonly string _id;
        private readonly string _name;
        private readonly Expression<Func<T, bool>>? _simpleCondition;
        private readonly Expression<Func<T, IFactContext, bool>>? _contextCondition;
        private Action<T>? _simpleAction;
        private Action<T, IFactContext>? _contextAction;

        private readonly HashSet<Type> _explicitDependencies = new();
        private DependencyAnalysisResult? _analysisResult;

        private int _priority;
        private readonly HashSet<string> _tags = new();

        // Cached compiled delegates for performance
        private Func<T, bool>? _compiledSimpleCondition;
        private Func<T, IFactContext, bool>? _compiledContextCondition;

        public DependentRule(string id, string name, Expression<Func<T, bool>> condition)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _simpleCondition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public DependentRule(string id, string name, Expression<Func<T, IFactContext, bool>> condition)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _contextCondition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        // IRule<T> implementation
        public string Id => _id;
        public string Name => _name;
        public string Description => _name;
        public int Priority => _priority;
        public IReadOnlyList<string> Tags => _tags.ToList();

        public Expression<Func<T, bool>> Condition =>
            _simpleCondition ?? throw new InvalidOperationException(
                "This rule uses a context-aware condition. Use EvaluateWithContext instead.");

        /// <summary>
        /// All dependencies (explicit + detected).
        /// </summary>
        public IReadOnlySet<Type> AllDependencies
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

        /// <summary>
        /// The result of dependency analysis, if analyzed.
        /// Null until AnalyzeDependencies() is called.
        /// </summary>
        public DependencyAnalysisResult? AnalysisResult => _analysisResult;

        /// <summary>
        /// Explicitly declare a dependency on a fact type.
        /// </summary>
        public DependentRule<T> DependsOn<TFact>() where TFact : class
        {
            _explicitDependencies.Add(typeof(TFact));
            return this;
        }

        /// <summary>
        /// Set the rule priority.
        /// </summary>
        public DependentRule<T> WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        /// <summary>
        /// Add tags to the rule.
        /// </summary>
        public DependentRule<T> WithTags(params string[] tags)
        {
            foreach (var tag in tags)
                _tags.Add(tag);
            return this;
        }

        /// <summary>
        /// Set the action to execute when the rule matches.
        /// </summary>
        public DependentRule<T> Then(Action<T> action)
        {
            _simpleAction = action ?? throw new ArgumentNullException(nameof(action));
            return this;
        }

        /// <summary>
        /// Set the context-aware action to execute when the rule matches.
        /// </summary>
        public DependentRule<T> Then(Action<T, IFactContext> action)
        {
            _contextAction = action ?? throw new ArgumentNullException(nameof(action));
            return this;
        }

        /// <summary>
        /// Analyze the rule's expressions and detect implicit dependencies.
        /// Called at registration time.
        /// </summary>
        public void AnalyzeDependencies(IFactSchema schema)
        {
            if (_simpleCondition != null)
            {
                _analysisResult = _simpleCondition.AnalyzeDependencies(schema);
            }
            else if (_contextCondition != null)
            {
                _analysisResult = _contextCondition.AnalyzeDependencies(schema);
            }

            // Validate explicit dependencies are registered
            foreach (var dep in _explicitDependencies)
            {
                schema.ValidateType(dep);
            }
        }

        // IRule<T> methods
        public bool Evaluate(T fact)
        {
            if (_simpleCondition == null)
                throw new InvalidOperationException(
                    "This rule requires context. Use EvaluateWithContext instead.");

            // Use cached delegate for performance
            _compiledSimpleCondition ??= _simpleCondition.Compile();
            return _compiledSimpleCondition(fact);
        }

        public bool EvaluateWithContext(T fact, IFactContext context)
        {
            if (_contextCondition != null)
            {
                // Use cached delegate for performance
                _compiledContextCondition ??= _contextCondition.Compile();
                return _compiledContextCondition(fact, context);
            }

            // Fall back to simple condition
            return Evaluate(fact);
        }

        public RuleResult Execute(T fact)
        {
            if (_simpleAction != null)
            {
                _simpleAction(fact);
                return RuleResult.Success(_id, _name);
            }
            return RuleResult.Success(_id, _name);
        }

        public RuleResult ExecuteWithContext(T fact, IFactContext context)
        {
            if (_contextAction != null)
            {
                _contextAction(fact, context);
                return RuleResult.Success(_id, _name);
            }

            // Fall back to simple action
            return Execute(fact);
        }
    }

    #endregion

    #region TODO: Integration Points

    // TODO: Integrate with existing RulesContext
    // - RulesContext should hold a FactSchema
    // - RulesContext should hold a DependencyGraph
    // - Schema should be built in OnModelCreating
    // - Graph should be populated from schema relationships + rule analysis

    // TODO: Integrate with RuleSession
    // - Session should use DependencyGraph.GetLoadOrder() to determine resolution order
    // - Session should resolve navigation properties before rule evaluation
    // - Session should provide IFactContext to rules during evaluation

    // TODO: Integrate with RuleSet
    // - RuleSet.Add should call AnalyzeDependencies on the rule
    // - RuleSet should update the DependencyGraph with detected dependencies

    // TODO: Static Rule.When<T>() factory
    // - Should return a builder that produces DependentRule<T>
    // - Should support both simple and context-aware conditions
    // Example:
    //   Rule.When<Message>(m => m.Type == MessageType.Alert)
    //       .And((m, ctx) => ctx.Facts<Agent>().Any(...))
    //       .Then((m, ctx) => ...)

    // TODO: Handle self-referential dependencies
    // - A rule on Message can query other Messages (for history)
    // - This creates Message -> Message dependency
    // - Need to ensure the query excludes the current fact (m.Id != current.Id)
    // - Consider adding a warning or requiring explicit acknowledgment

    // TODO: Performance optimization
    // - Cache compiled delegates
    // - Consider lazy analysis (only analyze if dependencies are queried)
    // - Consider parallel rule evaluation when dependencies allow

    #endregion
}
