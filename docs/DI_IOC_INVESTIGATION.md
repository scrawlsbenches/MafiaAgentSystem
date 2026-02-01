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

#### 1. Hard-coded Instantiation in AgentRouter

**File**: `AgentRouting/AgentRouting/Core/AgentRouter.cs` (Lines 17, 23)

```csharp
private readonly MiddlewarePipeline _pipeline = new();  // Created internally
private readonly RulesEngineCore<RoutingContext> _routingEngine = new RulesEngineCore<RoutingContext>(...);
```

**Impact**:
- Tightly couples AgentRouter to concrete implementations
- Cannot substitute alternative rules engine or pipeline
- Difficult to test in isolation

#### 2. Inconsistent Middleware Constructors

**Files**: `RateLimitMiddleware`, `CachingMiddleware`, `CircuitBreakerMiddleware`

```csharp
// Multiple overloads with different dependencies
public RateLimitMiddleware(IStateStore store, int maxRequests, TimeSpan window, ISystemClock? clock = null)
public RateLimitMiddleware(int maxRequests, TimeSpan window)  // Uses static defaults
```

**Impact**:
- Complex overload chains hide dependencies
- `IStateStore` is required but not obvious from simpler overloads
- Inconsistent whether clock is injected or uses static instance

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

## Proposed Architecture

### Lightweight IoC Container

Since we cannot use third-party dependencies, we need a minimal custom IoC container.

```csharp
// Proposed: AgentRouting/AgentRouting/DependencyInjection/ServiceContainer.cs

public interface IServiceContainer
{
    void Register<TService, TImplementation>() where TImplementation : TService;
    void RegisterSingleton<TService>(TService instance);
    void RegisterFactory<TService>(Func<IServiceContainer, TService> factory);
    TService Resolve<TService>();
    object Resolve(Type serviceType);
}

public class ServiceContainer : IServiceContainer, IDisposable
{
    private readonly Dictionary<Type, Func<IServiceContainer, object>> _factories = new();
    private readonly Dictionary<Type, object> _singletons = new();
    // ...
}
```

### Service Registration Extensions

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

### Refactored AgentRouter

```csharp
// Proposed changes to AgentRouter.cs

public class AgentRouter : IDisposable
{
    private readonly IMiddlewarePipeline _pipeline;
    private readonly IRulesEngine<RoutingContext> _routingEngine;
    private readonly IAgentLogger _logger;

    // Constructor injection
    public AgentRouter(
        IMiddlewarePipeline pipeline,
        IRulesEngine<RoutingContext> routingEngine,
        IAgentLogger logger)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _routingEngine = routingEngine ?? throw new ArgumentNullException(nameof(routingEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Convenience factory for backwards compatibility
    public static AgentRouter Create(IAgentLogger? logger = null)
    {
        return new AgentRouter(
            new MiddlewarePipeline(),
            new RulesEngineCore<RoutingContext>(),
            logger ?? new ConsoleAgentLogger());
    }
}
```

---

## Task Breakdown

### P1-DI: Core DI/IoC Tasks (High Priority)

| Task ID | Name | Est. Hours | Dependencies |
|---------|------|------------|--------------|
| P1-DI-1 | Create lightweight IoC container | 3-4h | None |
| P1-DI-2 | Add IMiddlewarePipeline interface | 2h | None |
| P1-DI-3 | Add IRulesEngine interface | 2h | None |
| P1-DI-4 | Refactor AgentRouter for DI | 3-4h | P1-DI-2, P1-DI-3 |
| P1-DI-5 | Standardize middleware constructors | 3-4h | P1-DI-1 |
| P1-DI-6 | Create service registration extensions | 2-3h | P1-DI-1 |
| P1-DI-7 | Update demos to use container | 2-3h | P1-DI-6 |
| P1-DI-8 | Add DI tests | 2-3h | P1-DI-1 |

**Total Estimated**: 19-25 hours

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

- [ ] `IServiceContainer` interface and implementation complete
- [ ] AgentRouter accepts injected dependencies
- [ ] Middleware constructors simplified and consistent
- [ ] All demos use container registration
- [ ] Build succeeds with 0 errors
- [ ] All tests pass (184+)
- [ ] New DI tests added (10+ tests)

---

## Interface Extraction Analysis

Beyond the IoC container work, several concrete classes would benefit from interface extraction.

### Already Planned (in P1-DI)

| Interface | Class | Task |
|-----------|-------|------|
| `IRulesEngine<T>` | `RulesEngineCore<T>` | P1-DI-3 |
| `IMiddlewarePipeline` | `MiddlewarePipeline` | P1-DI-2 |

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
