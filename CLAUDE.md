# MafiaAgentSystem Development Guide

## Quick Reference

| What | Command/Location |
|------|------------------|
| Build | `dotnet build AgentRouting/AgentRouting.sln` |
| Build LINQ | `dotnet build RulesEngine.Linq/RulesEngine.Linq/` |
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

Ubuntu 24.04 includes .NET 8.0 in its official repositories. No Microsoft repository needed.

```bash
# Update Ubuntu package lists only (avoids 403 errors from blocked PPAs)
apt-get update -o Dir::Etc::sourcelist="sources.list.d/ubuntu.sources" \
  -o Dir::Etc::sourceparts="-" -o APT::Get::List-Cleanup="0"

# Install .NET SDK 8.0 (pinned version for reproducibility)
apt-get install -y dotnet-sdk-8.0=8.0.123-0ubuntu1~24.04.1
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

# Restore RulesEngine.Linq (experimental LINQ-based rules)
dotnet restore RulesEngine.Linq/RulesEngine.Linq/ --source /nonexistent
dotnet restore Tests/RulesEngine.Linq.Tests/ --source /nonexistent

# Build everything (after restore)
dotnet build AgentRouting/AgentRouting.sln --no-restore

# Build test projects
dotnet build Tests/RulesEngine.Tests/ --no-restore
dotnet build Tests/AgentRouting.Tests/ --no-restore
dotnet build Tests/MafiaDemo.Tests/ --no-restore
dotnet build Tests/TestRunner/ --no-restore

# Build RulesEngine.Linq (experimental)
dotnet build RulesEngine.Linq/RulesEngine.Linq/ --no-restore
dotnet build Tests/RulesEngine.Linq.Tests/ --no-restore

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

### Current Coverage (as of 2026-02-08, 2,268 tests)

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
├── MafiaDemo.Tests/         # Tests for MafiaDemo
└── RulesEngine.Linq.Tests/  # Tests for RulesEngine.Linq
```

Each test project references `TestRunner.Framework`, `TestUtilities`, and its target assembly.

## Architecture

Three systems sharing patterns:

| System | Purpose | Core Abstraction |
|--------|---------|------------------|
| **RulesEngine** | Expression-based business rules | `IRule<T>` |
| **AgentRouting** | Message routing with middleware | `IAgent`, `IAgentMiddleware` |
| **RulesEngine.Linq** | EF Core-inspired LINQ rules (stabilized, 332 tests) | `IRulesContext`, `IRuleSet<T>` |

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
- **RulesEngine.Linq** (experimental): `RulesEngine.Linq/RulesEngine.Linq/`
- **RulesEngine.Linq Tests**: `Tests/RulesEngine.Linq.Tests/`

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

## RulesEngine.Linq

The successor to `RulesEngine.Core`, built from lessons learned during the original implementation. Rules are **expression trees first** — conditions are `Expression<Func<T, bool>>`, not compiled delegates. This makes them inspectable, serializable, and rewritable. The design targets a future where rules are authored locally but may execute on a remote server.

`RulesEngine.Core` remains in use by MafiaDemo (8 engines, ~98 rules) and is stable, but new rule engine work should target `RulesEngine.Linq`.

**Status:** Stabilized — 332 tests passing, 14/18 audit items fixed (2026-02-06), 4 deferred design items remaining

**Location:** `RulesEngine.Linq/RulesEngine.Linq/`

### Design Philosophy

- **Serialization is sacred** — Rules may be serialized and sent to a remote server for execution. No shortcuts that assume local execution.
- **Expression trees stay expressions** — `Expression<Func<T, bool>>` is the currency, not `Func<T, bool>`. Inspectability is the point.
- **Cross-fact queries are symbolic** — When a rule references `Facts<Agent>()`, the expression tree contains a `FactQueryExpression` marker node, not actual data. Data is substituted at evaluation time.

### Core Concepts

| Concept | Inspired By | Purpose |
|---------|-------------|---------|
| `IRulesContext` | `DbContext` | Entry point, manages rule sets and sessions |
| `IRuleSet<T>` | `DbSet<T>` | Queryable collection of rules for a fact type |
| `IRuleSession` | Unit of Work | Tracks facts, manages evaluation lifecycle |
| `Rule<T>` | Entity | Fluent rule with expression-based conditions |
| `DependentRule<T>` | Navigation property | Rule with explicit cross-fact context parameter |
| `FactQueryExpression` | SQL parameter | Serializable marker for cross-fact references |
| `DependencyGraph` | Migration ordering | Topological sort of fact type evaluation order |

### Key Files

- `Abstractions.cs` - Core interfaces (`IRulesContext`, `IRuleSet<T>`, `IRuleSession`, `IRule<T>`)
- `Implementation.cs` - In-memory implementations (`RulesContext`, `RuleSet<T>`, `FactSet<T>`, `RuleSession`)
- `Rule.cs` - Fluent `Rule<T>` class with automatic `FactQueryExpression` detection
- `Provider.cs` - Expression tree infrastructure (`FactQueryExpression`, `FactQueryRewriter`, `FactQueryable<T>`)
- `DependencyAnalysis.cs` - Cross-fact analysis (`IFactContext`, `DependentRule<T>`, `DependencyGraph`, `ContextConditionProjector`)
- `Validation.cs` - Expression validation
- `Constraints.cs` - Schema constraint configuration and enforcement
- `ClosureExtractor.cs` - Closure analysis for rule serialization
- `Extensions.cs` - Helper extensions (`WouldMatch`, `WithRule`, etc.)
- `CrossFactRulesDesign.cs` - Design doc for cross-fact evaluation with dependency tracking
- `AgentCommunicationDesign.cs` - Design doc for agent communication rules API

### Expression Tree Architecture

The key insight: rule conditions contain **symbolic placeholders** for cross-fact data, not the data itself.

```
Rule condition (expression tree):
  m => m.Priority == High && agents.Any(a => a.CanHandle(m))
                              ↑
                    FactQueryExpression(typeof(Agent))
                    — a serializable marker, not actual agents

At evaluation time, FactQueryRewriter substitutes:
  FactQueryExpression(Agent) → Expression.Constant(actualAgentList)

Result: compiled delegate with real data, but the original
expression tree remains pristine for serialization/inspection.
```

**`FactQueryExpression`** (`ExpressionType.Extension`) — Custom expression node representing `Facts<T>()`. Type is `IQueryable<FactType>`. Serialization-friendly: contains only the fact type, no runtime data.

**`FactQueryRewriter`** — `ExpressionVisitor` that substitutes `FactQueryExpression` nodes with actual session data (`Expression.Constant(facts)`) at evaluation time.

**`FactQueryExpression.ContainsFactQuery()`** — Public utility that walks any expression tree and detects whether it contains cross-fact references (either `FactQueryExpression` nodes or `FactQueryable<T>` closures).

### Two Cross-Fact Query Patterns

Rules can reference other fact types in two ways, both producing the same expression tree form:

**Pattern 1: Closure capture (Rule<T>)**
```csharp
var agents = context.Facts<Agent>(); // returns FactQueryable<Agent>
var rule = new Rule<Territory>("needs-agents", "Territories Needing Agents",
    t => t.Heat > 50 && agents.Any(a => a.AssignedTerritory == t.Name));
// Expression tree captures FactQueryable → detected as FactQueryExpression
```

**Pattern 2: Explicit context (DependentRule<T>)**
```csharp
var rule = new DependentRule<Territory>("needs-agents", "Territories Needing Agents",
    (t, ctx) => t.Heat > 50 && ctx.Facts<Agent>().Any(a => a.AssignedTerritory == t.Name));
// ContextConditionProjector transforms ctx.Facts<Agent>() → FactQueryExpression
```

**`ContextConditionProjector`** unifies both patterns at the expression tree level. It visits `ctx.Facts<T>()` calls and replaces them with `FactQueryExpression` nodes, so `DependentRule<T>.Condition` returns a standard `Expression<Func<T, bool>>` with symbolic markers — identical to what Pattern 1 produces.

### Evaluation Dispatch (4 Paths)

When `RuleSession.EvaluateFactSet<T>()` evaluates each rule against each fact, it selects one of four dispatch paths:

| Path | Condition | Mechanism |
|------|-----------|-----------|
| 1. DependentRule | `rule is DependentRule<T>` | Calls `EvaluateWithContext(fact, session)` directly |
| 2. Rule<T> + rewriter | `rule is Rule<T>` and `RequiresRewriting` | Uses `Rule<T>.GetOrCompileWithRewriter(rewriter)` |
| 3. Generic rewriter | Any `IRule<T>` where `ContainsFactQuery(Condition)` | Session rewrites and caches the compiled delegate |
| 4. Standard | No cross-fact references | Calls `rule.Evaluate(fact)` directly |

Path 3 is the generic fallback — any custom `IRule<T>` implementation that has `FactQueryExpression` nodes in its `Condition` will automatically get rewriter support without needing to implement anything special.

### DependencyGraph and Evaluation Ordering

When rules reference other fact types, evaluation order matters (e.g., Agent rules may depend on Territory facts being evaluated first).

```csharp
// Schema configuration (EF Core OnModelCreating analog)
context.ConfigureSchema(schema => {
    schema.RegisterFactType<Territory>();
    schema.RegisterFactType<Agent>(cfg => cfg.DependsOn<Territory>()); // Agent rules depend on Territory
});

// DependencyGraph provides topological ordering
var order = graph.GetLoadOrder(); // [Territory, Agent] — territories first
```

`DependencyExtractor` can also auto-detect dependencies by walking rule expression trees for `FactQueryExpression` nodes.

### Fluent Rule API

```csharp
// Create rules with fluent configuration
var rule = new Rule<AgentMessage>(
    "escalate-high-priority",
    "Escalate High Priority Messages",
    m => m.Priority == Priority.High && m.RequiresApproval)
    .WithPriority(100)
    .WithTags("escalation", "approval")
    .Then(m => m.EscalatedTo = m.Sender.Supervisor);

// Compose rules with And/Or
var combinedRule = urgentRule.And(securityClearanceRule);
var eitherRule = weekendRule.Or(holidayRule);
```

### Session-Based Evaluation

```csharp
using var context = new RulesContext();
var rules = context.GetRuleSet<AgentMessage>();

rules.Add(highPriorityRule);
rules.Add(escalationRule);

using var session = context.CreateSession();
session.InsertAll(messages);

var result = session.Evaluate<AgentMessage>();
foreach (var match in result.Matches)
{
    Console.WriteLine($"Message {match.Fact.Id} matched {match.Rules.Count} rules");
}
```

### Rule Preview (WouldMatch)

```csharp
// Preview which rules would match without executing actions
var matchingRules = ruleSet.WouldMatch(message);
foreach (var rule in matchingRules)
{
    Console.WriteLine($"Would match: {rule.Name} (priority {rule.Priority})");
}
```

### Future: Remote Execution

The expression tree architecture enables a serialization story:

1. **Author locally** — Write rules using familiar LINQ/lambda syntax
2. **Serialize** — Expression trees (including `FactQueryExpression` markers) can be serialized to JSON/binary
3. **Send to server** — Rules travel as data, not compiled code
4. **Evaluate remotely** — Server deserializes expression trees, substitutes its own fact data via `FactQueryRewriter`, evaluates

This is not yet implemented, but the architecture is designed for it. Key pieces in place:
- `FactQueryExpression` is a pure data node (just a `Type`)
- `ClosureExtractor` analyzes captured variables for serialization readiness
- `ContextConditionProjector` ensures all cross-fact references use the symbolic form

### Agent Communication Patterns (Tests)

The test file `Tests/RulesEngine.Linq.Tests/AgentCommunicationRulesTests.cs` demonstrates:
- Permission rules based on agent roles
- Chain of command routing
- Load balancing by workload
- Escalation workflows
- Rule composition for complex conditions
- Territory-based message filtering
- Audit trail tracking

### Audit Status (2026-02-06)

Full audit in `RulesEngine.Linq/AUDIT_2026-02-05.md`. 332 tests across 13 test files.

**Fixed (14/18):** Stale cache on re-evaluation (P0), `TotalRulesEvaluated` always 0, `ClearSessionCache` never called, `ContainsFactQuery` per-fact perf, `Evaluate<T>` state transition, detector/rewriter asymmetry, `GetFacts()` live list exposure, cycle detection untested, error accumulation untested, projector error paths, session lifecycle edge cases, dead code removal.

**Deferred (4/18):**

| # | Issue | Priority | Reason Deferred |
|---|-------|----------|-----------------|
| 7 | `DependentRule.Execute()` skips condition check | P3 | Session handles it; trap for direct callers only |
| 8 | `FindByKey()` ignores `HasKey()` schema config | P2 | Requires schema access from session — larger API change |
| 9 | `RegisteredFactTypes` returns inserted types, not registered | P2 | Same — requires schema access from session |
| 14 | False-positive test naming | P3 | Low impact |

Items 8 and 9 are a single architectural change (pass schema reference to session).

### Migration Roadmap from RulesEngine.Core

Features to port from the legacy engine before MafiaDemo can migrate:

| Feature | Legacy (`RulesEngine.Core`) | Status in LINQ |
|---------|----------------------------|----------------|
| Async rules | `IAsyncRule<T>`, `ExecuteAsync` | **Not yet ported** |
| Performance tracking | `TrackPerformance`, `GetMetrics()` | **Not yet ported** |
| Parallel execution | `EnableParallelExecution` option | **Not yet ported** |
| MafiaDemo integration | 8 engines, ~98 rules | **Not yet started** |

Features already improved in LINQ over legacy:

| Feature | Legacy | LINQ |
|---------|--------|------|
| Conditions | `Func<T, bool>` (opaque) | `Expression<Func<T, bool>>` (inspectable) |
| Fact types | One per engine instance | Multi-fact sessions with cross-fact queries |
| Schema validation | None | Constraints, expression depth, closure restrictions |
| Rule composition | `CompositeRuleBuilder<T>` | `Rule<T>.And()` / `.Or()` on expression trees |
| Session lifecycle | Stateless `Execute(fact)` | Insert/Evaluate/Commit/Rollback |
| Serialization | N/A | Architecture ready, not yet implemented |

### Open Design Issues (2026-02-09 Review)

Issues identified during the stabilization review that should be addressed:

**`IConstrainedRuleSet<T>` duplicates `IRuleSet<T>` members (interface smell)**
`IRuleSet<T>` already declares `HasConstraints`, `GetConstraints()`, `HasConstraint()`, and `TryAdd()`. `IConstrainedRuleSet<T>` extends it and redeclares the same members plus `ValidationMode`. Either the constraint members should live only on the extended interface, or the extended interface should be removed.

**`Rule<T>` swallows exceptions silently (observability gap)**
`Rule<T>.Evaluate()` catches all exceptions and returns `false`. `Rule<T>.Execute()` catches and returns `RuleResult.Error`. In contrast, `DependentRule<T>` has no try-catch — exceptions propagate to the session's error handler and appear in `IEvaluationResult.Errors`. This means session error reporting is blind to `Rule<T>` failures. For the successor engine, this asymmetry should be resolved so all rule types report errors consistently.

**No code coverage measurement**
The CLAUDE.md coverage table tracks RulesEngine, AgentRouting, and MafiaDemo. RulesEngine.Linq has no measured coverage data yet. Should be added to the coverage workflow.

**Session thread safety is undocumented**
`RuleSession` uses `ConcurrentDictionary` for fact sets and rewrite cache, but `_errors` is a plain `List<>` and `_state` is a plain field with no synchronization. Sessions are likely intended to be single-threaded, but this isn't documented in the interface or class.

### Recommended Next Steps

1. **Fix deferred design flaws (#7, #8, #9)** — Pass schema to session, wire `FindByKey` to `HasKey` config, fix `RegisteredFactTypes`. Single API change covers #8 and #9.
2. **Resolve `Rule<T>` error swallowing** — Make exception reporting consistent across `Rule<T>` and `DependentRule<T>` so `IEvaluationResult.Errors` captures all failures.
3. **Clean up `IConstrainedRuleSet<T>` duplication** — Remove redundant constraint members from either `IRuleSet<T>` or the extended interface.
4. **Add async rule support** — Port `IAsyncRule<T>` from legacy engine. Required before MafiaDemo can migrate.
5. **Add coverage measurement** — Wire RulesEngine.Linq into the Coverlet workflow.
6. **Integrate with MafiaDemo** — Migrate one of the 8 rule engines to stress-test the LINQ API with real data.
7. **Skip for now:** Serialization/remote execution (no consumer), full `IQueryable<T>` provider (symbolic marker suffices).

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

## Immutable Rules Engine

For lock-free concurrent access, use `ImmutableRulesEngine<T>`:

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
| `RulesEngine.Linq/AUDIT_2026-02-05.md` | Bugs, design flaws, test gaps with priority ordering |
