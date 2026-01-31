# MafiaAgentSystem Development Guide

## Build & Test
```bash
dotnet build                           # Build all
dotnet test                            # Run all tests
dotnet test RulesEngine/               # RulesEngine tests only
dotnet test AgentRouting/              # AgentRouting tests only
```

## Architecture

Two independent systems sharing patterns:

| System | Purpose | Core Abstraction |
|--------|---------|------------------|
| **RulesEngine** | Expression-based business rules | `IRule<T>` |
| **AgentRouting** | Message routing with middleware | `IAgent`, `IAgentMiddleware` |

## SOLID Extension Points

### Adding New Rules (Open/Closed)
```csharp
// Implement IRule<T> - don't modify RulesEngineCore
public class MyRule<T> : IRule<T> {
    public string Id { get; }
    public string Name { get; }
    public int Priority { get; }
    public bool Evaluate(T fact) => ...
    public RuleResult Execute(T fact) => ...
}
engine.RegisterRule(new MyRule<Order>());
```

### Adding New Middleware (Open/Closed)
```csharp
// Implement IAgentMiddleware via MiddlewareBase
public class MyMiddleware : MiddlewareBase {
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message, MessageDelegate next, CancellationToken ct) {
        // Before
        var result = await next(message, ct);
        // After
        return result;
    }
}
```

### Adding New Agents (Open/Closed)
```csharp
public class MyAgent : IAgent {
    public string Id => "my-agent";
    public string Name => "My Agent";
    public bool CanHandle(AgentMessage msg) => ...
    public Task<MessageResult> ProcessAsync(AgentMessage msg, CancellationToken ct) => ...
}
```

## Key Interfaces (Interface Segregation)

```
RulesEngine/
├── IRule<T>           # Single rule contract
├── RulesEngineCore<T> # Depends on IRule<T> abstraction
└── ThreadSafeRulesEngine<T> / LockedRulesEngine<T>  # Thread-safe variants

AgentRouting/
├── IAgent             # Agent contract
├── IAgentMiddleware   # Middleware contract (in MiddlewareInfrastructure.cs)
├── AgentRouter        # Depends on IAgent abstraction
└── MiddlewarePipeline # Composes IAgentMiddleware
```

## File Locations

- **Rules**: `RulesEngine/RulesEngine/Core/`
- **Thread Safety**: `RulesEngine/RulesEngine/Enhanced/`
- **Agents**: `AgentRouting/AgentRouting/Core/`
- **Middleware**: `AgentRouting/AgentRouting/Middleware/`
- **Tests**: `*/Tests/`

## Known Issues

1. **AgentRouting build**: `AgentRouterWithMiddleware.cs` references deleted `IMessageMiddleware` - change to `IAgentMiddleware`
2. **3 failing RulesEngine tests**: Test assertion logic issues, not code bugs

## Dependency Inversion Pattern

Constructors accept abstractions:
```csharp
// Good - depends on abstraction
public AgentRouter(IAgentLogger logger)

// Rules engine accepts IRule<T>, not concrete Rule<T>
public void RegisterRule(IRule<T> rule)
```

## Single Responsibility Boundaries

| Class | Responsibility |
|-------|---------------|
| `RulesEngineCore<T>` | Execute rules |
| `RuleValidator` | Validate rule expressions |
| `RuleAnalyzer<T>` | Analyze rule patterns |
| `AgentRouter` | Route messages to agents |
| `MiddlewarePipeline` | Compose/execute middleware |
| Each `*Middleware` | One cross-cutting concern |
