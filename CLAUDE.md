# MafiaAgentSystem Development Guide

## Quick Reference

| What | Command/Location |
|------|------------------|
| Build | `dotnet build AgentRouting/AgentRouting.sln` |
| Test | `dotnet run --project Tests/TestRunner/` |
| Constraints | Zero 3rd party dependencies |

## Skill Files (For Complex Work)

When working on complex multi-step tasks, consult:
- **`SKILL_ORCHESTRATION.md`** - Multi-agent batch workflows, file ownership, verification gates
- **`SKILL_TASK_ANALYSIS.md`** - Breaking down work, prioritization, dependency analysis
- **`EXECUTION_PLAN.md`** - Current project state and completed batches
- **`TASK_LIST.md`** - Full prioritized task list with estimates

### Keeping Project State Updated

**IMPORTANT**: The director agent must update tracking files after sub-agents complete work:

1. **After each batch completes**:
   - Update `EXECUTION_PLAN.md` with batch status and log
   - Mark completed tasks in the execution state section
   - Commit the update

2. **When tasks are discovered to be already done**:
   - Update `TASK_LIST.md` to mark as complete or revise scope
   - Update `EXECUTION_PLAN.md` with discovery notes
   - Adjust time estimates based on actual findings

3. **Before starting new work**:
   - Read `EXECUTION_PLAN.md` to understand current state
   - Read `TASK_LIST.md` to see remaining work
   - Verify build and tests still pass

This ensures future sessions can continue seamlessly.

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
├── IRule<T>              # Sync rule contract
├── IAsyncRule<T>         # Async rule contract (for I/O operations)
├── RulesEngineCore<T>    # Thread-safe, supports both sync and async rules
├── RuleValidationException # Thrown on invalid rule registration
└── RuleExecutionResult<T>  # Detailed execution result with exception info

AgentRouting/
├── IAgent             # Agent contract
├── IAgentMiddleware   # Middleware contract (in MiddlewareInfrastructure.cs)
├── IAgentLogger       # Logging abstraction (nullable fromAgent)
├── AgentRouter        # Depends on IAgent abstraction
├── MiddlewarePipeline # Composes IAgentMiddleware
└── ISystemClock       # Testable time abstraction
```

## File Locations

- **Rules**: `RulesEngine/RulesEngine/Core/` (includes `AsyncRule.cs`, `RuleValidationException.cs`)
- **Thread Safety**: `RulesEngine/RulesEngine/Enhanced/`
- **Agents**: `AgentRouting/AgentRouting/Core/`
- **Middleware**: `AgentRouting/AgentRouting/Middleware/`
- **Configuration**: `AgentRouting/AgentRouting/Configuration/` (defaults)
- **Infrastructure**: `AgentRouting/AgentRouting/Infrastructure/` (SystemClock)
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
- `ExecuteAsync(fact, cancellationToken)` - Async execution with cancellation support

## Async Rules (IAsyncRule<T>)

For rules that perform I/O operations (database, API calls):

```csharp
// Using the builder
var asyncRule = new AsyncRuleBuilder<Order>()
    .WithId("check-inventory")
    .WithName("Check Inventory Async")
    .WithPriority(100)
    .WithCondition(async order => await inventoryService.HasStock(order.ProductId))
    .WithAction(async order => {
        await inventoryService.Reserve(order.ProductId);
        return RuleResult.Success("check-inventory");
    })
    .Build();

engine.RegisterAsyncRule(asyncRule);

// Execute both sync and async rules
var results = await engine.ExecuteAsync(order, cancellationToken);
```

## Thread Safety

`RulesEngineCore<T>` is thread-safe using `ReaderWriterLockSlim`:
- Multiple threads can execute rules concurrently (read lock)
- Rule registration/removal acquires exclusive write lock
- Implements `IDisposable` - call `Dispose()` when done

```csharp
using var engine = new RulesEngineCore<Order>();
// Safe for concurrent access
```

## Rule Validation

Rules are validated on registration:
- `null` rule throws `ArgumentNullException`
- Empty/null `Id` throws `RuleValidationException`
- Empty/null `Name` throws `RuleValidationException`
- Duplicate `Id` throws `RuleValidationException` (configurable)

```csharp
// Allow duplicate IDs if needed
var options = new RulesEngineOptions { AllowDuplicateRuleIds = true };
var engine = new RulesEngineCore<Order>(options);
```

## Performance: Sorted Rules Cache

Rules are sorted by priority once and cached. Cache invalidates automatically on:
- `RegisterRule()` / `RegisterRules()`
- `AddRule()`
- `RemoveRule()`
- `ClearRules()`

## Configuration Classes

**AgentRouting defaults** (`AgentRouting/Configuration/`):
```csharp
AgentRoutingDefaults.MaxConcurrentMessages  // Default: 100
AgentRoutingDefaults.DefaultTimeout         // Default: 30 seconds
MiddlewareDefaults.DefaultCacheSize         // Default: 1000
MiddlewareDefaults.DefaultRateLimitPerMinute // Default: 60
```

**Game timing** (`MafiaDemo/GameTimingOptions.cs`):
```csharp
GameTimingOptions.Current = GameTimingOptions.Instant; // No delays
GameTimingOptions.Current = GameTimingOptions.Fast;    // 0.25x delays
GameTimingOptions.Current = GameTimingOptions.Normal;  // Full delays
```

**Testable time** (`AgentRouting/Infrastructure/SystemClock.cs`):
```csharp
// Production
var now = SystemClock.Instance.UtcNow;

// Testing
SystemClock.Instance = new FakeClock(fixedTime);
```

## Historical Context

This codebase was built across multiple sessions, evolving from expression tree exploration to a full agent communication platform.

**Read `ORIGINS.md`** for the story of how expression trees led to the rules engine and agent routing design.

### Archives

| File | Contents |
|------|----------|
| `transcripts.zip` | Session transcripts showing development evolution |
| `Archive.zip` | Additional code files from development |
| `docs/archive/` | Historical documentation (completed reviews, workflows) |

Key transcripts:
- `2026-01-31-14-19-45-expression-trees-tutorial.txt` - Foundation
- `2026-01-31-14-39-00-rules-engine-expression-trees.txt` - Rules engine design
- `2026-01-31-16-29-36-agent-routing-systems-implementation.txt` - AgentRouting
- `2026-01-31-17-22-45-mafia-agent-hierarchy-demo.txt` - MafiaDemo

## Documentation Structure

| Document | Purpose |
|----------|---------|
| `ORIGINS.md` | Why expression trees → rules engine → agent routing |
| `TASK_LIST.md` | Remaining work with priorities |
| `EXECUTION_PLAN.md` | Completed phases and batch logs |
| `RulesEngine/ISSUES_AND_ENHANCEMENTS.md` | Design decisions with resolution status |
| `AgentRouting/MIDDLEWARE_EXPLAINED.md` | Middleware concepts tutorial |
| `AgentRouting/MIDDLEWARE_POTENTIAL.md` | Advanced middleware patterns |
| `AgentRouting/AgentRouting.MafiaDemo/ARCHITECTURE.md` | Game architecture |
