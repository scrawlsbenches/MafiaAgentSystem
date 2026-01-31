# MafiaAgentSystem Development Guide

## Prerequisites
- .NET 8.0 SDK required (`dotnet --version` should show 8.x)
- Standard install: `sudo apt install dotnet-sdk-8.0` (Ubuntu) or https://dot.net

### Microsoft Repository Install (Ubuntu 24.04)
```bash
# Download Microsoft repository configuration
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

# Install repository (as root, no sudo needed in web environment)
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Fix /tmp permissions to prevent GPG errors
chmod 1777 /tmp

# Update package lists
apt-get update 2>&1 | grep -v "403\|Forbidden" | tail -10

# Install .NET SDK 8.0
apt-get install -y dotnet-sdk-8.0 2>&1 | tail -20
```

### Restricted/Proxy Environments
If NuGet fails with proxy errors:
```bash
# 1. Configure apt proxy (if needed)
echo 'Acquire::http::Proxy "http://proxy:port";' | sudo tee /etc/apt/apt.conf.d/99proxy

# 2. If NuGet still fails, download packages manually and use local source:
mkdir -p /tmp/nuget-packages
curl -x "$HTTP_PROXY" -o /tmp/nuget-packages/xunit.2.9.2.nupkg \
  "https://api.nuget.org/v3-flatcontainer/xunit/2.9.2/xunit.2.9.2.nupkg"
# Repeat for: Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, etc.

# 3. Configure NuGet to use local source only:
cat > ~/.nuget/NuGet/NuGet.Config << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="/tmp/nuget-packages" />
  </packageSources>
</configuration>
EOF
```

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

### AgentRouting Build Errors

`AgentRouterWithMiddleware.cs` has multiple issues that prevent compilation:

1. **Wrong interface**: References `IMessageMiddleware` which doesn't exist. The correct interface is `IAgentMiddleware` (defined in `MiddlewareInfrastructure.cs`)
   - Lines 21, 44, 55, 66 need `IMessageMiddleware` → `IAgentMiddleware`

2. **Missing methods on `MiddlewarePipeline`**: The following methods are called but don't exist:
   - `ExecuteAsync()` - called on line 34, also used in tests and demos
   - `GetMiddleware()` - called on line 46

3. **Tests/demos use undefined `UseCallback()`**: `MiddlewareTests.cs` and `MiddlewareDemo/Program.cs` call `pipeline.UseCallback()` which isn't defined on `MiddlewarePipeline`

**To fix**: Either add the missing methods to `MiddlewarePipeline` or refactor `AgentRouterWithMiddleware.cs` to use the existing `Build()` method pattern

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
