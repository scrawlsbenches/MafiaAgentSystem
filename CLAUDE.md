# MafiaAgentSystem Development Guide

## Prerequisites

.NET 8.0 SDK is required. Verify installation with:
```bash
dotnet --version   # Should show 8.x.x
```

### Installing .NET 8.0 SDK

**Ubuntu/Debian:**
```bash
# Add Microsoft package repository (required on most systems)
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install SDK
sudo apt update
sudo apt install -y dotnet-sdk-8.0
```

**macOS:**
```bash
brew install --cask dotnet-sdk   # Latest LTS (currently 8.x)
# Or for a specific version:
brew install --cask dotnet-sdk@8
```

**Windows:**
Download installer from https://dot.net/download

**Other platforms:** See https://learn.microsoft.com/dotnet/core/install/

### Troubleshooting: Proxy/Restricted Environments

If `dotnet restore` or NuGet fails with connection/proxy errors:

```bash
# 1. Set proxy environment variables
export HTTP_PROXY="http://proxy:port"
export HTTPS_PROXY="http://proxy:port"

# 2. Configure apt proxy (for installation)
echo 'Acquire::http::Proxy "http://proxy:port";' | sudo tee /etc/apt/apt.conf.d/99proxy

# 3. If NuGet still fails, download packages manually:
mkdir -p /tmp/nuget-packages
# Required packages for this project:
curl -x "$HTTP_PROXY" -L -o /tmp/nuget-packages/xunit.2.9.2.nupkg \
  "https://api.nuget.org/v3-flatcontainer/xunit/2.9.2/xunit.2.9.2.nupkg"
curl -x "$HTTP_PROXY" -L -o /tmp/nuget-packages/Microsoft.NET.Test.Sdk.17.11.1.nupkg \
  "https://api.nuget.org/v3-flatcontainer/microsoft.net.test.sdk/17.11.1/microsoft.net.test.sdk.17.11.1.nupkg"
curl -x "$HTTP_PROXY" -L -o /tmp/nuget-packages/xunit.runner.visualstudio.2.8.2.nupkg \
  "https://api.nuget.org/v3-flatcontainer/xunit.runner.visualstudio/2.8.2/xunit.runner.visualstudio.2.8.2.nupkg"
curl -x "$HTTP_PROXY" -L -o /tmp/nuget-packages/coverlet.collector.6.0.2.nupkg \
  "https://api.nuget.org/v3-flatcontainer/coverlet.collector/6.0.2/coverlet.collector.6.0.2.nupkg"

# 4. Configure NuGet to use local source:
mkdir -p ~/.nuget/NuGet
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
