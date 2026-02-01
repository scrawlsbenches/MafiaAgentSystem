# Dependency Injection & Inversion of Control Investigation

> **Created**: 2026-02-01
> **Branch**: `claude/investigate-dependency-injection-B6xCF`
> **Purpose**: Document current DI patterns and plan improvements

---

## Executive Summary

The MafiaAgentSystem codebase already uses good dependency injection patterns (constructor injection, interface abstractions, builder patterns) but has significant pain points that would benefit from a lightweight IoC container. Key issues include:

- Hard-coded internal instantiation in `AgentRouter`
- Inconsistent middleware constructor patterns
- Manual wiring in every Program.cs
- No central dependency resolution mechanism

**Constraint**: Zero third-party dependencies means we need a custom lightweight IoC implementation.

---

## Current State Analysis

### What's Working Well

| Pattern | Examples | Notes |
|---------|----------|-------|
| Interface Abstractions | `IAgent`, `IAgentLogger`, `IAgentMiddleware` | Core contracts well-defined |
| Constructor Injection | `AgentBase(IAgentLogger)`, `LoggingMiddleware(IAgentLogger)` | Services passed in constructors |
| Builder Pattern | `AgentRouterBuilder` | Fluent API for configuration |
| Testable Time | `ISystemClock`, `FakeClock` | Already added in Phase 5 |
| State Abstraction | `IStateStore`, `InMemoryStateStore` | Added in Phase 5 |

### Pain Points

#### 1. ~~Hard-coded Instantiation in AgentRouter~~ ✅ RESOLVED (P1-DI-4)

**Status**: Fixed. AgentRouter now requires all dependencies via constructor injection.

```csharp
// NEW: Clean DI constructor - no hidden instantiation
public AgentRouter(
    IAgentLogger logger,
    IMiddlewarePipeline pipeline,
    IRulesEngine<RoutingContext> routingEngine)
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    _routingEngine = routingEngine ?? throw new ArgumentNullException(nameof(routingEngine));
}

// AgentRouterBuilder creates defaults when not provided
var router = new AgentRouterBuilder().WithLogger(logger).Build();
```

#### 2. ~~Inconsistent Middleware Constructors~~ ✅ RESOLVED (P1-DI-5)

**Status**: Fixed. All middleware now have single constructors requiring all dependencies.

```csharp
// NEW: Single constructor per middleware - all dependencies required
public RateLimitMiddleware(IStateStore store, int maxRequests, TimeSpan window, ISystemClock clock)
public CachingMiddleware(IStateStore store, TimeSpan ttl, int maxEntries, ISystemClock clock)
public CircuitBreakerMiddleware(IStateStore store, int failureThreshold, TimeSpan resetTimeout, TimeSpan failureWindow, ISystemClock clock)
```

**Design Decision**: No convenience overloads. Callers provide all dependencies explicitly.

#### 3. Manual Wiring in Program.cs Files

**Files**: All demo Program.cs files

```csharp
// Every demo repeats this pattern
var logger = new ConsoleAgentLogger();
var router = new AgentRouterBuilder()
    .WithLogger(logger)
    .WithAgent(new CustomerServiceAgent(logger))
    .WithMiddleware(new RateLimitMiddleware(new InMemoryStateStore(), 100, TimeSpan.FromMinutes(1)))
    .Build();
```

**Impact**:
- Boilerplate repeated across applications
- Changes to dependencies require updates everywhere
- Easy to forget required services (e.g., IStateStore)

#### 4. Static Configuration Dependencies

**Files**: `MiddlewareDefaults.cs`, `AgentRoutingDefaults.cs`

```csharp
public static class MiddlewareDefaults
{
    public const int RateLimitDefaultMaxRequests = 100;
    // ...
}
```

**Impact**:
- No way to override defaults except via constructor parameters
- Tight coupling to static classes
- Configuration not injectable

#### 5. Logger Defaults Couple to Concrete Type

**File**: `AgentRouterBuilder.cs` (Line 47)

```csharp
_logger = new ConsoleAgentLogger();  // Hard-coded default
```

**Impact**:
- Builder coupled to console implementation
- Violates dependency inversion principle

---

## Finalized Container Design

> **Design Decisions** (2026-02-01):
> - Lambda-based registration (explicit factories, no reflection)
> - Three lifetimes: Singleton, Transient, Scoped
> - Scoped instances shared within scope (ASP.NET Core pattern)
> - No auto-wiring (user controls construction)
> - No open generics (register each type explicitly)

### Design Principles

**Fast**:
- No reflection at resolve time - just dictionary lookups
- `ConcurrentDictionary` for thread-safe access without locks
- Singleton cache checked first (most common case)

**Easy to Use**:
- Fluent registration API with lambdas
- Compile-time safety (constructor changes caught at build)
- Clear, predictable behavior

### Core Interfaces

```csharp
// AgentRouting/AgentRouting/DependencyInjection/IServiceContainer.cs

public interface IServiceContainer : IDisposable
{
    // Registration (all take factory lambda)
    IServiceContainer AddSingleton<TService>(Func<IServiceContainer, TService> factory) where TService : class;
    IServiceContainer AddTransient<TService>(Func<IServiceContainer, TService> factory) where TService : class;
    IServiceContainer AddScoped<TService>(Func<IServiceContainer, TService> factory) where TService : class;

    // Convenience: register existing instance as singleton
    IServiceContainer AddSingleton<TService>(TService instance) where TService : class;

    // Resolution
    TService Resolve<TService>() where TService : class;
    bool TryResolve<TService>(out TService? service) where TService : class;
    bool IsRegistered<TService>();

    // Scoping
    IServiceScope CreateScope();
}

public interface IServiceScope : IDisposable
{
    TService Resolve<TService>() where TService : class;
    bool TryResolve<TService>(out TService? service) where TService : class;
}
```

### Lifetime Behaviors

| Lifetime | Behavior | Use Case |
|----------|----------|----------|
| **Singleton** | One instance for container lifetime | `IAgentLogger`, `ISystemClock` |
| **Transient** | New instance every resolve | Stateless services, factories |
| **Scoped** | One instance per scope, shared within | Per-request `IStateStore`, context |

### Usage Example

```csharp
// Registration - explicit lambdas, no magic
var container = new ServiceContainer()
    // Singletons - created once, cached forever
    .AddSingleton<IAgentLogger>(c => new ConsoleAgentLogger())
    .AddSingleton<ISystemClock>(c => SystemClock.Instance)
    .AddSingleton(SystemClock.Instance)  // Convenience overload

    // Transients - new instance every time
    .AddTransient<IStateStore>(c => new InMemoryStateStore())

    // Scoped - shared within scope
    .AddScoped<RateLimitMiddleware>(c => new RateLimitMiddleware(
        c.Resolve<IStateStore>(),
        100,
        TimeSpan.FromMinutes(1),
        c.Resolve<ISystemClock>()
    ))

    // Complex dependency chains work naturally
    .AddSingleton<AgentRouter>(c => new AgentRouter(
        c.Resolve<IMiddlewarePipeline>(),
        c.Resolve<IRulesEngine<RoutingContext>>(),
        c.Resolve<IAgentLogger>()
    ));

// Root resolution (singletons and transients)
var logger = container.Resolve<IAgentLogger>();

// Scoped resolution (per-request pattern)
using var scope = container.CreateScope();
var middleware1 = scope.Resolve<RateLimitMiddleware>();
var middleware2 = scope.Resolve<RateLimitMiddleware>();
// middleware1 == middleware2 (same instance within scope)

// Different scope = different instance
using var scope2 = container.CreateScope();
var middleware3 = scope2.Resolve<RateLimitMiddleware>();
// middleware3 != middleware1 (different scope)
```

### Implementation Sketch

```csharp
// AgentRouting/AgentRouting/DependencyInjection/ServiceContainer.cs

public enum Lifetime { Singleton, Transient, Scoped }

internal record ServiceDescriptor(Lifetime Lifetime, Func<IServiceContainer, object> Factory);

public class ServiceContainer : IServiceContainer
{
    private readonly ConcurrentDictionary<Type, ServiceDescriptor> _descriptors = new();
    private readonly ConcurrentDictionary<Type, object> _singletons = new();
    private bool _disposed;

    public IServiceContainer AddSingleton<TService>(Func<IServiceContainer, TService> factory)
        where TService : class
    {
        _descriptors[typeof(TService)] = new(Lifetime.Singleton, c => factory(c));
        return this;
    }

    public IServiceContainer AddSingleton<TService>(TService instance) where TService : class
    {
        _singletons[typeof(TService)] = instance;
        _descriptors[typeof(TService)] = new(Lifetime.Singleton, _ => instance);
        return this;
    }

    public IServiceContainer AddTransient<TService>(Func<IServiceContainer, TService> factory)
        where TService : class
    {
        _descriptors[typeof(TService)] = new(Lifetime.Transient, c => factory(c));
        return this;
    }

    public IServiceContainer AddScoped<TService>(Func<IServiceContainer, TService> factory)
        where TService : class
    {
        _descriptors[typeof(TService)] = new(Lifetime.Scoped, c => factory(c));
        return this;
    }

    public TService Resolve<TService>() where TService : class
    {
        var type = typeof(TService);

        // Fast path: singleton cache
        if (_singletons.TryGetValue(type, out var cached))
            return (TService)cached;

        if (!_descriptors.TryGetValue(type, out var descriptor))
            throw new InvalidOperationException($"Service '{type.Name}' is not registered.");

        if (descriptor.Lifetime == Lifetime.Scoped)
            throw new InvalidOperationException(
                $"Scoped service '{type.Name}' cannot be resolved from root container. Use CreateScope().");

        var instance = (TService)descriptor.Factory(this);

        if (descriptor.Lifetime == Lifetime.Singleton)
            _singletons.TryAdd(type, instance);

        return instance;
    }

    public IServiceScope CreateScope() => new ServiceScope(this, _descriptors);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var singleton in _singletons.Values.OfType<IDisposable>())
            singleton.Dispose();

        _singletons.Clear();
    }

    // ... TryResolve, IsRegistered implementations
}

internal class ServiceScope : IServiceScope
{
    private readonly IServiceContainer _root;
    private readonly ConcurrentDictionary<Type, ServiceDescriptor> _descriptors;
    private readonly ConcurrentDictionary<Type, object> _scopedInstances = new();
    private bool _disposed;

    public ServiceScope(IServiceContainer root, ConcurrentDictionary<Type, ServiceDescriptor> descriptors)
    {
        _root = root;
        _descriptors = descriptors;
    }

    public TService Resolve<TService>() where TService : class
    {
        var type = typeof(TService);

        if (!_descriptors.TryGetValue(type, out var descriptor))
            throw new InvalidOperationException($"Service '{type.Name}' is not registered.");

        // Singletons resolve from root
        if (descriptor.Lifetime == Lifetime.Singleton)
            return _root.Resolve<TService>();

        // Transients always new
        if (descriptor.Lifetime == Lifetime.Transient)
            return (TService)descriptor.Factory(_root);

        // Scoped: check cache, create if missing
        return (TService)_scopedInstances.GetOrAdd(type, _ => descriptor.Factory(_root));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var scoped in _scopedInstances.Values.OfType<IDisposable>())
            scoped.Dispose();

        _scopedInstances.Clear();
    }
}
```

### Error Messages

Clear errors for common mistakes:

| Scenario | Error Message |
|----------|---------------|
| Service not registered | `"Service 'IFoo' is not registered."` |
| Scoped from root | `"Scoped service 'Foo' cannot be resolved from root container. Use CreateScope()."` |
| Disposed container | `"Cannot resolve from disposed container."` |

### Estimated Implementation Size

| Component | Lines |
|-----------|-------|
| `IServiceContainer` | ~25 |
| `IServiceScope` | ~10 |
| `ServiceContainer` | ~80 |
| `ServiceScope` | ~50 |
| **Total** | **~165 lines** |

---

## Service Registration Extensions

```csharp
// Proposed: AgentRouting/AgentRouting/DependencyInjection/ServiceExtensions.cs

public static class ServiceExtensions
{
    public static IServiceContainer AddAgentRouting(this IServiceContainer services)
    {
        services.RegisterSingleton<ISystemClock>(SystemClock.Instance);
        services.Register<IStateStore, InMemoryStateStore>();
        services.Register<IAgentLogger, ConsoleAgentLogger>();
        return services;
    }

    public static IServiceContainer AddMiddleware<TMiddleware>(this IServiceContainer services)
        where TMiddleware : IAgentMiddleware
    {
        services.Register<IAgentMiddleware, TMiddleware>();
        return services;
    }
}
```

### Refactored AgentRouter ✅ IMPLEMENTED (P1-DI-4)

```csharp
// ACTUAL implementation in AgentRouter.cs

public class AgentRouter
{
    private readonly IMiddlewarePipeline _pipeline;
    private readonly IRulesEngine<RoutingContext> _routingEngine;
    private readonly IAgentLogger _logger;

    // Pure DI constructor - all dependencies required
    public AgentRouter(
        IAgentLogger logger,
        IMiddlewarePipeline pipeline,
        IRulesEngine<RoutingContext> routingEngine)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _routingEngine = routingEngine ?? throw new ArgumentNullException(nameof(routingEngine));
    }
}

// AgentRouterBuilder creates defaults when not provided
public class AgentRouterBuilder
{
    public AgentRouter Build()
    {
        var logger = _logger ?? new ConsoleAgentLogger();
        var pipeline = _pipeline ?? new MiddlewarePipeline();
        var routingEngine = _routingEngine ?? new RulesEngineCore<RoutingContext>(
            new RulesEngineOptions { StopOnFirstMatch = true, TrackPerformance = true });

        return new AgentRouter(logger, pipeline, routingEngine);
    }
}
```

**Design Decision**: No convenience factory on AgentRouter. All code uses `AgentRouterBuilder` for construction.

---

## Task Breakdown

### P1-DI: Core DI/IoC Tasks (High Priority)

| Task ID | Name | Est. Hours | Status |
|---------|------|------------|--------|
| P1-DI-1 | Create lightweight IoC container | 3-4h | ✅ Complete (37 tests) |
| P1-DI-2 | Add IMiddlewarePipeline interface | 2h | ✅ Complete |
| P1-DI-3 | Add IRulesEngine interface | 2h | ✅ Complete |
| P1-DI-4 | Refactor AgentRouter for DI | 3-4h | ✅ Complete |
| P1-DI-5 | Standardize middleware constructors | 3-4h | ✅ Complete |
| P1-DI-6 | Create service registration extensions | 2-3h | ⏳ Pending |
| P1-DI-7 | Update demos to use container | 2-3h | ⏳ Pending |
| P1-DI-8 | Add DI tests | 2-3h | ✅ Complete (in P1-DI-1) |

**Total Estimated**: 19-25 hours | **Completed**: ~5 hours | **Remaining**: ~10-15 hours

### Dependency Graph

```
P1-DI-1 ─┬─→ P1-DI-5
         ├─→ P1-DI-6 ─→ P1-DI-7
         └─→ P1-DI-8

P1-DI-2 ─┬─→ P1-DI-4
P1-DI-3 ─┘
```

### Parallelization Opportunities

**Batch A** (Parallel - different files):
- P1-DI-1: ServiceContainer.cs (new)
- P1-DI-2: IMiddlewarePipeline.cs (new)
- P1-DI-3: IRulesEngine.cs (new)

**Batch B** (After Batch A):
- P1-DI-4: AgentRouter.cs refactoring
- P1-DI-5: Middleware constructor updates

**Batch C** (After Batch B):
- P1-DI-6: ServiceExtensions.cs (new)
- P1-DI-7: Demo Program.cs updates
- P1-DI-8: DI tests

---

## Files to Create/Modify

### New Files

| Path | Purpose |
|------|---------|
| `AgentRouting/AgentRouting/DependencyInjection/IServiceContainer.cs` | Container interface |
| `AgentRouting/AgentRouting/DependencyInjection/ServiceContainer.cs` | Implementation |
| `AgentRouting/AgentRouting/DependencyInjection/ServiceExtensions.cs` | Registration helpers |
| `AgentRouting/AgentRouting/Core/IMiddlewarePipeline.cs` | Pipeline interface |
| `RulesEngine/RulesEngine/Core/IRulesEngine.cs` | Engine interface |
| `Tests/TestRunner/Tests/DependencyInjectionTests.cs` | Container tests |

### Files to Modify

| Path | Changes |
|------|---------|
| `AgentRouter.cs` | Accept injected dependencies |
| `RateLimitMiddleware.cs` | Simplify constructors |
| `CachingMiddleware.cs` | Simplify constructors |
| `CircuitBreakerMiddleware.cs` | Simplify constructors |
| `AgentRouterBuilder.cs` | Use container if provided |
| `RulesEngineCore.cs` | Implement IRulesEngine<T> |
| `MiddlewarePipeline.cs` | Implement IMiddlewarePipeline |
| All demo `Program.cs` | Use container registration |

---

## Backwards Compatibility Strategy

1. **Factory Methods**: Add `AgentRouter.Create()` for simple construction
2. **Default Implementations**: Container provides defaults (ConsoleAgentLogger, InMemoryStateStore)
3. **Optional Container**: Builder can work with or without container
4. **Interface Additions**: Existing classes implement new interfaces without breaking changes

---

## Success Criteria

- [x] `IServiceContainer` interface and implementation complete (P1-DI-1)
- [x] AgentRouter accepts injected dependencies (P1-DI-4)
- [x] Middleware constructors simplified and consistent (P1-DI-5)
- [ ] All demos use container registration (P1-DI-7)
- [x] Build succeeds with 0 errors
- [x] All tests pass (221 tests)
- [x] New DI tests added (37 tests in P1-DI-1)

---

## Interface Extraction Analysis

Beyond the IoC container work, several concrete classes would benefit from interface extraction.

### Already Planned (in P1-DI) ✅ COMPLETE

| Interface | Class | Task | Status |
|-----------|-------|------|--------|
| `IRulesEngine<T>` | `RulesEngineCore<T>` | P1-DI-3 | ✅ Complete |
| `IMiddlewarePipeline` | `MiddlewarePipeline` | P1-DI-2 | ✅ Complete |

### High Priority Additions

#### 1. IRulesEngineResult

**File**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`

**Why**: Returned from `RulesEngineCore.Execute()`, contains mutable state, different result types might be needed for different execution strategies.

```csharp
public interface IRulesEngineResult
{
    IReadOnlyList<RuleResult> RuleResults { get; }
    TimeSpan TotalExecutionTime { get; }
    int TotalRulesEvaluated { get; }
    int MatchedRules { get; }
    int ExecutedActions { get; }
    int Errors { get; }
    List<RuleResult> GetMatchedRules();
}
```

#### 2. IRuleExecutionResult<T>

**File**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`

**Why**: Returned from `ExecuteAsync`, generic container for both sync and async rule results.

```csharp
public interface IRuleExecutionResult<T>
{
    IRule<T>? Rule { get; }
    IAsyncRule<T>? AsyncRule { get; }
    string RuleId { get; }
    RuleResult ExecutionResult { get; }
    bool WasEvaluated { get; }
}
```

### Medium Priority Additions

#### 3. ITraceSpan

**File**: `AgentRouting/AgentRouting/Middleware/AdvancedMiddleware.cs`

**Why**: Used in `DistributedTracingMiddleware`, could support multiple tracing backends (Jaeger, Zipkin).

```csharp
public interface ITraceSpan
{
    string TraceId { get; }
    string SpanId { get; }
    string? ParentSpanId { get; }
    string ServiceName { get; }
    string OperationName { get; }
    DateTime StartTime { get; }
    TimeSpan Duration { get; set; }
    bool Success { get; set; }
    IDictionary<string, string> Tags { get; }
}
```

#### 4. IMiddlewareContext

**File**: `AgentRouting/AgentRouting/Middleware/MiddlewareInfrastructure.cs`

**Why**: Middleware data storage, could have distributed implementations.

```csharp
public interface IMiddlewareContext
{
    T? Get<T>(string key);
    void Set<T>(string key, T value);
    bool TryGet<T>(string key, out T? value);
}
```

#### 5. IMetricsSnapshot

**File**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs`

**Why**: Returned from `MetricsMiddleware.GetSnapshot()`, supports different metrics formats.

```csharp
public interface IMetricsSnapshot
{
    int TotalMessages { get; }
    int SuccessCount { get; }
    int FailureCount { get; }
    double SuccessRate { get; }
    double AverageProcessingTimeMs { get; }
    long MinProcessingTimeMs { get; }
    long MaxProcessingTimeMs { get; }
}
```

#### 6. IAnalyticsReport

**File**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs`

**Why**: Returned from `AnalyticsMiddleware.GetReport()`, different analytics backends.

```csharp
public interface IAnalyticsReport
{
    int TotalMessages { get; }
    IReadOnlyDictionary<string, int> CategoryCounts { get; }
    IReadOnlyDictionary<string, int> AgentWorkload { get; }
}
```

#### 7. IWorkflowDefinition / IWorkflowStage

**File**: `AgentRouting/AgentRouting/Middleware/AdvancedMiddleware.cs`

**Why**: Workflow orchestration abstraction, different workflow engine implementations.

```csharp
public interface IWorkflowDefinition
{
    string Id { get; }
    IReadOnlyList<IWorkflowStage> Stages { get; }
}

public interface IWorkflowStage
{
    string Name { get; }
    Func<AgentMessage, Task<bool>>? OnEnter { get; }
    IReadOnlyList<(string condition, string nextStage)> Transitions { get; }
}
```

### Lower Priority

| Interface | Class | Reason |
|-----------|-------|--------|
| `IRulePerformanceMetrics` | `RulePerformanceMetrics` | Performance tracking data |
| `IRoutingContext` | `RoutingContext` | Context for routing rules |

### Not Recommended

These don't need interfaces:
- **Configuration classes** (`AgentRoutingDefaults`, `MiddlewareDefaults`) - static holders
- **Options classes** (`RulesEngineOptions`) - immutable config
- **Simple data classes** (`AgentMessage`, `MessageResult`) - well-designed data holders
- **Exception classes** - correctly designed as concrete

---

## References

- Phase 5 work: ISystemClock and IStateStore additions
- CLAUDE.md: Dependency Inversion Pattern section
- ORIGINS.md: Architecture history
