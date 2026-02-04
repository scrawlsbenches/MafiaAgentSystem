# MafiaAgentSystem Development Guide

## Quick Reference

| What | Command/Location |
|------|------------------|
| Build | `dotnet build AgentRouting/AgentRouting.sln` |
| Test | `dotnet run --project Tests/TestRunner/` |
| Coverage | `dotnet exec tools/coverage/coverlet/tools/net6.0/any/coverlet.console.dll` |
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

**IMPORTANT: NuGet is blocked by the proxy and will never be available.** This project has zero NuGet dependencies, so use offline restore:

```bash
# Restore (offline - no NuGet access needed for zero-dependency projects)
dotnet restore AgentRouting/AgentRouting.sln --source /nonexistent

# Restore test projects (required - they are separate from the solution)
dotnet restore Tests/RulesEngine.Tests/ --source /nonexistent
dotnet restore Tests/AgentRouting.Tests/ --source /nonexistent
dotnet restore Tests/MafiaDemo.Tests/ --source /nonexistent
dotnet restore Tests/TestRunner/ --source /nonexistent

# Build everything (after restore)
dotnet build AgentRouting/AgentRouting.sln --no-restore

# Build test projects
dotnet build Tests/RulesEngine.Tests/ --no-restore
dotnet build Tests/AgentRouting.Tests/ --no-restore
dotnet build Tests/MafiaDemo.Tests/ --no-restore
dotnet build Tests/TestRunner/ --no-restore

# Run all tests (auto-discovers built test assemblies)
dotnet run --project Tests/TestRunner/ --no-build

# Run specific test assembly
dotnet run --project Tests/TestRunner/ --no-build -- Tests/RulesEngine.Tests/bin/Debug/net8.0/RulesEngine.Tests.dll
```

The test runner has **zero NuGet dependencies** - it uses a custom lightweight test framework.

## Code Coverage

Line-by-line code coverage is available using Coverlet (included in `tools/coverage/`).

### Running Coverage

```bash
# Create coverage output directory
mkdir -p coverage

# Coverage for RulesEngine
dotnet exec tools/coverage/coverlet/tools/net6.0/any/coverlet.console.dll \
  Tests/RulesEngine.Tests/bin/Debug/net8.0/ \
  -t dotnet \
  -a 'run --project Tests/TestRunner/ --no-build -- Tests/RulesEngine.Tests/bin/Debug/net8.0/RulesEngine.Tests.dll' \
  -f cobertura \
  -o coverage/rulesengine.xml

# Coverage for AgentRouting
dotnet exec tools/coverage/coverlet/tools/net6.0/any/coverlet.console.dll \
  Tests/AgentRouting.Tests/bin/Debug/net8.0/ \
  -t dotnet \
  -a 'run --project Tests/TestRunner/ --no-build -- Tests/AgentRouting.Tests/bin/Debug/net8.0/AgentRouting.Tests.dll' \
  -f cobertura \
  -o coverage/agentrouting.xml

# Coverage for MafiaDemo
dotnet exec tools/coverage/coverlet/tools/net6.0/any/coverlet.console.dll \
  Tests/MafiaDemo.Tests/bin/Debug/net8.0/ \
  -t dotnet \
  -a 'run --project Tests/TestRunner/ --no-build -- Tests/MafiaDemo.Tests/bin/Debug/net8.0/MafiaDemo.Tests.dll' \
  -f cobertura \
  -o coverage/mafiademo.xml
```

### Reading Coverage Reports

```bash
./tools/coverage-report.sh                    # Full gap analysis
./tools/coverage-report.sh --summary-only     # Quick status
./tools/coverage-report.sh --detail ClassName # Method-level breakdown
```

### Coverage Output Formats

| Format | Flag | Use Case |
|--------|------|----------|
| Cobertura XML | `-f cobertura` | CI/CD integration, detailed reports |
| LCOV | `-f lcov` | IDE integration, line highlighting |
| JSON | `-f json` | Programmatic analysis |

### Current Coverage (as of 2026-02-03, 1,862 tests)

| Module | Line | Branch | Method |
|--------|------|--------|--------|
| RulesEngine | 91.71% | 77.45% | 95.57% |
| AgentRouting | 71.79% | 77.64% | 85.25% |
| MafiaDemo | 70.37% | 76.55% | 88.13% |

### Test Project Structure

The TestRunner is decoupled from the code under test:

```
Tests/
├── TestRunner/              # Test host (loads assemblies at runtime)
├── TestRunner.Framework/    # Shared framework (Assert, [Test], [Theory], lifecycle)
├── TestUtilities/           # Shared test helpers (TestClocks, TestAgents, etc.)
├── RulesEngine.Tests/       # Tests for RulesEngine
├── AgentRouting.Tests/      # Tests for AgentRouting
└── MafiaDemo.Tests/         # Tests for MafiaDemo
```

Each test project references `TestRunner.Framework`, `TestUtilities`, and its target assembly.

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
├── IRulesEngine<T>       # Main engine contract
├── IRule<T>              # Sync rule contract
├── IAsyncRule<T>         # Async rule contract (for I/O operations)
├── IRulesEngineResult    # Aggregated execution results (in IResults.cs)
├── IRuleResult           # Individual rule result (in IResults.cs)
├── IRuleExecutionResult<T> # Detailed result with exception (in IResults.cs)
├── RulesEngineCore<T>    # Thread-safe implementation
├── ImmutableRulesEngine<T> # Lock-free immutable alternative
├── RuleBuilder<T>        # Fluent sync rule builder
├── AsyncRuleBuilder<T>   # Fluent async rule builder
├── CompositeRuleBuilder<T> # Combine existing rules
└── RuleValidationException # Thrown on invalid rule registration

AgentRouting/
├── IAgent             # Agent contract
├── IAgentMiddleware   # Middleware contract (in MiddlewareInfrastructure.cs)
├── IMiddlewarePipeline # Pipeline contract
├── IAgentLogger       # Logging abstraction (nullable fromAgent)
├── IStateStore        # State persistence abstraction
├── ISystemClock       # Testable time abstraction
├── IServiceContainer  # Dependency injection contract
├── ITraceSpan         # Distributed tracing (in IMiddlewareTypes.cs)
├── IMiddlewareContext # Middleware data sharing (in IMiddlewareTypes.cs)
├── IMetricsSnapshot   # Metrics reporting (in IMiddlewareTypes.cs)
├── IAnalyticsReport   # Analytics reporting (in IMiddlewareTypes.cs)
├── IWorkflowDefinition # Workflow orchestration (in IMiddlewareTypes.cs)
├── IWorkflowStage     # Workflow stages (in IMiddlewareTypes.cs)
├── AgentRouter        # Depends on IAgent abstraction
└── MiddlewarePipeline # Composes IAgentMiddleware
```

## File Locations

- **Rules Core**: `RulesEngine/RulesEngine/Core/`
  - `IRulesEngine.cs`, `IRule.cs` - Core interfaces
  - `IResults.cs` - Result interfaces (`IRuleResult`, `IRulesEngineResult`, `IRuleExecutionResult<T>`)
  - `RulesEngineCore.cs` - Main engine (includes `ImmutableRulesEngine<T>` at end)
  - `Rule.cs` - Sync rule implementation
  - `AsyncRule.cs` - Async rule implementation
  - `RuleBuilder.cs` - Fluent builders (`RuleBuilder<T>`, `CompositeRuleBuilder<T>`)
  - `DynamicRuleFactory.cs` - Configuration-based rule creation
  - `RuleValidationException.cs` - Validation errors
- **Rules Enhanced**: `RulesEngine/RulesEngine/Enhanced/`
  - `RuleValidation.cs` - Rule validator, analyzer, debuggable rules
- **Agents**: `AgentRouting/AgentRouting/Core/`
  - `Agent.cs` - `IAgent`, `AgentBase` with atomic slot acquisition
  - `AgentRouter.cs` - Message routing with middleware pipeline
  - `AgentRouterBuilder.cs` - Fluent router configuration
- **Middleware**: `AgentRouting/AgentRouting/Middleware/`
  - `CommonMiddleware.cs` - Core middleware (Logging, Validation, RateLimit, Caching, CircuitBreaker)
  - `AdvancedMiddleware.cs` - Advanced middleware (Tracing, A/B Testing, Workflow, etc.)
  - `MiddlewareInfrastructure.cs` - `IAgentMiddleware`, `MiddlewarePipeline`, `MiddlewareContext`
  - `IMiddlewareTypes.cs` - Extracted interfaces (`ITraceSpan`, `IMiddlewareContext`, `IMetricsSnapshot`, etc.)
- **Configuration**: `AgentRouting/AgentRouting/Configuration/` (defaults)
- **Infrastructure**: `AgentRouting/AgentRouting/Infrastructure/` (SystemClock, StateStore)
- **Dependency Injection**: `AgentRouting/AgentRouting/DependencyInjection/`
  - `ServiceContainer.cs` - Custom IoC container
  - `ServiceExtensions.cs` - Registration extensions (`AddAgentRouting`, `AddMiddleware<T>`, etc.)
- **Test Framework**: `Tests/TestRunner.Framework/` (Assert, attributes, TestBase, lifecycle)
- **Test Utilities**: `Tests/TestUtilities/` (TestClocks, TestAgents, TestMiddleware, etc.)
- **Test Runner**: `Tests/TestRunner/` (runtime assembly loading)
- **RulesEngine Tests**: `Tests/RulesEngine.Tests/`
- **AgentRouting Tests**: `Tests/AgentRouting.Tests/`
- **MafiaDemo Tests**: `Tests/MafiaDemo.Tests/`

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

**Scale:** 8 specialized `RulesEngineCore<T>` instances with ~98 total rules:
- `_gameRules` (GameRuleContext) - Victory/defeat, warnings
- `_agentRules` (AgentDecisionContext) - AI agent decisions (~45 rules)
- `_eventRules` (EventContext) - Random event generation
- `_valuationEngine` (TerritoryValueContext) - Economic pricing
- `_difficultyEngine` (DifficultyContext) - Adaptive difficulty
- `_strategyEngine` (RivalStrategyContext) - Rival AI
- `_chainEngine` (ChainReactionContext) - Event cascades
- `_asyncRules` (AsyncEventContext) - Time-delayed operations

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

**Fluent builder (RuleBuilder<T>):** For readable, composable conditions
```csharp
var rule = new RuleBuilder<Order>()
    .WithId("high-value-vip")
    .WithName("High Value VIP Order")
    .WithPriority(100)
    .When(o => o.Total > 1000)
    .And(o => o.Customer.IsVip)
    .Then(o => o.ApplyDiscount(0.1))
    .Build();
```

**Composite builder (CompositeRuleBuilder<T>):** For combining existing rules
```csharp
var composite = new CompositeRuleBuilder<Order>()
    .WithId("complex-approval")
    .WithName("Complex Approval Rule")
    .WithOperator(CompositeOperator.Or)
    .AddRule(highValueRule)
    .AddRule(riskyCategoryRule)
    .Build();
```

**Execution modes:**
- `Execute(fact)` - Returns results, rules don't modify fact
- `EvaluateAll(fact)` - Applies all matching rules, modifies fact in-place
- `ExecuteAsync(fact, cancellationToken)` - Async execution with cancellation support

## RulesEngine Options

Configure engine behavior via `RulesEngineOptions`:

```csharp
var options = new RulesEngineOptions
{
    StopOnFirstMatch = true,           // Stop after first matching rule
    EnableParallelExecution = false,   // Execute rules in parallel
    TrackPerformance = true,           // Track execution metrics
    MaxRulesToExecute = null,          // Limit rules executed (null = no limit)
    AllowDuplicateRuleIds = false      // Allow same ID for multiple rules
};
var engine = new RulesEngineCore<Order>(options);
```

**StopOnFirstMatch behavior:**
- Sequential: Breaks loop after first match
- Parallel: Uses linked CancellationTokenSource to stop other threads
- Async: Checks flag between async rule evaluations

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

**⚠️ CAVEAT:** The `TrackPerformance` method has a thread-safety bug - see Known Issues below.

## Immutable Rules Engine

For lock-free concurrent access, use `ImmutableRulesEngine<T>`:

**⚠️ CAVEAT:** `ImmutableRulesEngine` is missing validation and has the same `TrackPerformance` bug.

```csharp
var engine1 = new ImmutableRulesEngine<Order>();
var engine2 = engine1.WithRule(rule1);  // Returns NEW engine
var engine3 = engine2.WithRule(rule2);  // Each is independent

// All three can be used concurrently without locks
```

**When to use:**
- High read-to-write ratio (few rule changes, many executions)
- Functional programming patterns
- Snapshot isolation required

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

## Known Issues and Caveats

**IMPORTANT:** Review these before modifying related code. See `CODE_REVIEW_BUGS.txt` for full details.

### Critical Thread-Safety Bugs

| Component | Issue | Location |
|-----------|-------|----------|
| `RulesEngineCore.TrackPerformance` | `ConcurrentDictionary.AddOrUpdate` mutates existing object in place - race condition | `RulesEngineCore.cs:666-693` |
| `ImmutableRulesEngine.TrackPerformance` | Same bug as above | `RulesEngineCore.cs:1066-1094` |
| `ServiceContainer.Resolve` | Singleton factory may be invoked multiple times concurrently | `ServiceContainer.cs:72-84` |
| `AgentRouter.RegisterAgent` | Not thread-safe - no synchronization | `AgentRouter.cs:62-66` |

### Logic Errors

| Component | Issue |
|-----------|-------|
| `CompositeRule.Execute` | Evaluates child rules 2-3 times unnecessarily |
| `ImmutableRulesEngine.WithRule` | Missing validation (null check, ID/Name validation, duplicate check) |

### Memory/Resource Issues

| Component | Issue |
|-----------|-------|
| `MetricsMiddleware._processingTimes` | `ConcurrentBag<long>` grows unboundedly |
| `StoryGraph._eventLog` | `List<StoryEvent>` grows unboundedly |
| `DistributedTracingMiddleware._spans` | `ConcurrentBag<TraceSpan>` grows unboundedly |

### Weak Implementations

| Component | Issue |
|-----------|-------|
| `SystemClock.Instance` | Static mutable state causes test interference in parallel execution |
| `ABTestingMiddleware._random` | Uses non-thread-safe `System.Random` - use `Random.Shared` instead |
| `MessageQueueMiddleware.ProcessBatch` | Uses `async void` - exceptions crash application |
| `SanitizationMiddleware.SanitizeInput` | Trivially bypassable XSS filter |

### Test Considerations

When writing tests that modify global state:
- `SystemClock.Instance` - restore after test, or use DI instead
- `GameTimingOptions.Current` - restore after test
- Parallel test execution may cause interference with these singletons
