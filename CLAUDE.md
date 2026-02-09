# MafiaAgentSystem Development Guide

## Quick Reference

| What | Command/Location |
|------|------------------|
| Build | `dotnet build AgentRouting/AgentRouting.sln` |
| Build LINQ | `dotnet build RulesEngine.Linq/RulesEngine.Linq/` |
| Test | `dotnet run --project Tests/TestRunner/` |
| Coverage | `dotnet exec tools/coverage/coverlet/tools/net6.0/any/coverlet.console.dll` |
| Constraints | Zero 3rd party dependencies |

## Standard Operating Procedures

Development SOPs and per-subsystem procedures are in `.claude/rules/`:

| File | Scope | Loaded |
|------|-------|--------|
| `sops.md` | General development procedures, build sequence, test failure triage | Always |
| `rulesengine-core.md` | RulesEngine Core SOPs, thread safety, API patterns | When touching `RulesEngine/` or `Tests/RulesEngine.Tests/` |
| `rulesengine-linq.md` | RulesEngine.Linq SOPs, dispatch paths, known issues | When touching `RulesEngine.Linq/` or `Tests/RulesEngine.Linq.Tests/` |
| `agentrouting.md` | AgentRouting SOPs, middleware pipeline, agent patterns | When touching `AgentRouting/AgentRouting/` or `Tests/AgentRouting.Tests/` |
| `mafiademo.md` | MafiaDemo SOPs, 8-engine topology, API gap discovery | When touching `MafiaDemo/` or `Tests/MafiaDemo.Tests/` |

**Read the relevant rules file before starting work on a subsystem.**

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

# Reading reports
./tools/coverage-report.sh                    # Full gap analysis
./tools/coverage-report.sh --summary-only     # Quick status
./tools/coverage-report.sh --detail ClassName # Method-level breakdown
```

### Current Coverage (as of 2026-02-08, 2,268 tests)

| Module | Line | Branch | Method |
|--------|------|--------|--------|
| RulesEngine | 91.71% | 77.45% | 95.57% |
| AgentRouting | 71.79% | 77.64% | 85.25% |
| MafiaDemo | 70.37% | 76.55% | 88.13% |

## Architecture

Three systems sharing patterns:

| System | Purpose | Core Abstraction |
|--------|---------|------------------|
| **RulesEngine** | Expression-based business rules | `IRule<T>` |
| **AgentRouting** | Message routing with middleware | `IAgent`, `IAgentMiddleware` |
| **RulesEngine.Linq** | EF Core-inspired LINQ rules (stabilized, 332 tests) | `IRulesContext`, `IRuleSet<T>` |

### Test Project Structure

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
- **RulesEngine.Linq**: `RulesEngine.Linq/RulesEngine.Linq/`
  - `Abstractions.cs` - Core interfaces (`IRulesContext`, `IRuleSet<T>`, `IRuleSession`, `IRule<T>`)
  - `Implementation.cs` - `RulesContext`, `RuleSet<T>`, `FactSet<T>`, `RuleSession`
  - `Rule.cs` - Fluent `Rule<T>` with expression detection
  - `Provider.cs` - `FactQueryExpression`, `FactQueryRewriter`, `FactQueryable<T>`
  - `DependencyAnalysis.cs` - `DependentRule<T>`, `DependencyGraph`, `ContextConditionProjector`
  - `Validation.cs`, `Constraints.cs`, `ClosureExtractor.cs`, `Extensions.cs`
- **Test Framework**: `Tests/TestRunner.Framework/` (Assert, attributes, TestBase, lifecycle)
- **Test Utilities**: `Tests/TestUtilities/` (TestClocks, TestAgents, TestMiddleware, etc.)

## Documentation Index

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
