---
paths:
  - "AgentRouting/AgentRouting/Core/**/*.cs"
  - "AgentRouting/AgentRouting/Middleware/**/*.cs"
  - "AgentRouting/AgentRouting/Configuration/**/*.cs"
  - "AgentRouting/AgentRouting/Infrastructure/**/*.cs"
  - "AgentRouting/AgentRouting/DependencyInjection/**/*.cs"
  - "Tests/AgentRouting.Tests/**/*.cs"
---

# AgentRouting — SOPs and Reference

## Before Modifying Code

1. Identify which layer:
   - `Core/` — `IAgent`, `AgentBase`, `AgentRouter`, `AgentRouterBuilder` (high impact)
   - `Middleware/` — pipeline infrastructure and individual middleware (medium impact)
   - `Configuration/` — defaults (low impact, but affects runtime behavior)
   - `Infrastructure/` — `SystemClock`, `StateStore` (testability layer)
   - `DependencyInjection/` — `ServiceContainer`, extensions (composition root)
2. MafiaDemo consumes AgentRouting heavily — changes here can break the game
3. Run baseline: `dotnet run --project Tests/TestRunner/ --no-build -- Tests/AgentRouting.Tests/bin/Debug/net8.0/AgentRouting.Tests.dll`

## Adding New Middleware

Follow this procedure — middleware is the primary extension point:

1. Create class extending `MiddlewareBase` in `Middleware/`
2. Override `InvokeAsync(AgentMessage message, MessageDelegate next, CancellationToken ct)`
3. Always call `await next(message, ct)` unless intentionally short-circuiting
4. One middleware = one cross-cutting concern (logging, caching, rate limiting, etc.)
5. Add tests that verify both the pass-through and short-circuit behaviors
6. Register via `ServiceExtensions.AddMiddleware<T>()`

## Adding New Agents

1. Implement `IAgent` or extend `AgentBase`
2. `AgentBase` provides atomic slot acquisition — prefer it over raw `IAgent`
3. `CanHandle(msg)` determines routing — must be deterministic and fast
4. `ProcessAsync(msg, ct)` contains the logic
5. Register with `AgentRouter` or use `AgentRouterBuilder`

## Middleware Pipeline Order Matters

Middleware executes in registration order. The pipeline wraps each middleware around the next:

```
Request → Logging → Validation → RateLimit → [Agent] → RateLimit → Validation → Logging → Response
```

If you reorder middleware registration, behavior changes. Verify:
- Logging should be outermost (captures everything)
- Validation before business logic (fail fast)
- Caching before expensive operations (skip work)

## Key Interfaces

| Interface | File | Purpose |
|-----------|------|---------|
| `IAgent` | `Core/Agent.cs` | Agent contract |
| `IAgentMiddleware` | `Middleware/MiddlewareInfrastructure.cs` | Middleware contract |
| `IMiddlewarePipeline` | `Middleware/MiddlewareInfrastructure.cs` | Pipeline contract |
| `IAgentLogger` | `Core/AgentRouter.cs` | Logging abstraction |
| `IStateStore` | `Infrastructure/` | State persistence |
| `ISystemClock` | `Infrastructure/SystemClock.cs` | Testable time |
| `IServiceContainer` | `DependencyInjection/ServiceContainer.cs` | IoC container |

## Configuration Defaults

When changing defaults, understand the impact:
- `AgentRoutingDefaults.MaxConcurrentMessages` = 100
- `AgentRoutingDefaults.DefaultTimeout` = 30 seconds
- `MiddlewareDefaults.DefaultCacheSize` = 1000
- `MiddlewareDefaults.DefaultRateLimitPerMinute` = 60

These are used by middleware constructors. Changing them changes behavior for all consumers.

## Testable Time

All time-dependent code uses `ISystemClock`, not `DateTime.UtcNow`. Tests use `TestClocks` from `Tests/TestUtilities/`. When adding time-dependent logic, inject `ISystemClock` — never call `DateTime` directly.

## After Changes

1. Run AgentRouting tests
2. Run MafiaDemo tests (primary consumer): `dotnet run --project Tests/TestRunner/ --no-build -- Tests/MafiaDemo.Tests/bin/Debug/net8.0/MafiaDemo.Tests.dll`
