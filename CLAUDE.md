# MafiaAgentSystem Development Guide

## Prerequisites
- .NET 8.0 SDK required (`dotnet --version` should show 8.x)

### Install .NET SDK 8.0 (Ubuntu 24.04)
```bash
# Download Microsoft repository configuration
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

# Install repository (as root, no sudo needed in web environment)
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Fix /tmp permissions to prevent GPG errors
chmod 1777 /tmp

# Update package lists (Microsoft repo only)
apt-get update -o Dir::Etc::sourcelist="sources.list.d/microsoft-prod.list" -o Dir::Etc::sourceparts="-" -o APT::Get::List-Cleanup="0"

# Install .NET SDK 8.0
apt-get install -y dotnet-sdk-8.0 2>&1 | tail -20
```

## Build & Test
```bash
dotnet build                              # Build all
dotnet run --project Tests/TestRunner/    # Run all tests (zero dependencies)
```

The test runner has **zero NuGet dependencies** - it uses a custom lightweight test framework in `Tests/TestRunner/Framework/`.

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
- **Tests**: `Tests/TestRunner/Tests/`
- **Test Framework**: `Tests/TestRunner/Framework/`

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

## MafiaDemo Game

The `AgentRouting.MafiaDemo` project is a **test bed** for exercising the RulesEngine and AgentRouting systems. It simulates a mafia family hierarchy with autonomous agents making decisions.

**Purpose:** Find API gaps and areas for improvement in the core libraries through real-world usage patterns.

**Key namespaces:**
- `AgentRouting.MafiaDemo.Game` - GameState, Territory, RivalFamily, AutonomousAgent base class
- `AgentRouting.MafiaDemo.Rules` - Rules engine integration for game logic
- `AgentRouting.MafiaDemo.Missions` - Mission system with player progression
- `AgentRouting.MafiaDemo.AI` - PlayerAgent with rules-driven decision making
- `AgentRouting.MafiaDemo.Autonomous` - NPC agents (Godfather, Underboss, etc.)

## RulesEngine API Patterns

**Full control (IRule<T>):** For complex rules with custom logic
```csharp
engine.RegisterRule(new MyCustomRule<Order>());
```

**Inline convenience (AddRule):** For quick rule definitions
```csharp
engine.AddRule("RULE_ID", "Rule Name",
    ctx => ctx.Amount > 1000,           // condition
    ctx => ctx.RequiresApproval = true, // action
    priority: 100);
```

**Execution modes:**
- `Execute(fact)` - Returns results, rules don't modify fact
- `EvaluateAll(fact)` - Applies all matching rules, modifies fact in-place

## Historical Context

This codebase was built across multiple sessions. Key transcripts are in `transcripts.zip`:
- Expression trees tutorial → RulesEngine foundation
- Agent routing implementation → AgentRouting core
- Middleware system → Pipeline architecture
- MafiaDemo → Game that exercises both systems

If files appear missing, check transcripts for code that may not have been saved to disk.
