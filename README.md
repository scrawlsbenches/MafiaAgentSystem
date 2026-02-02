# MafiaAgentSystem

A multi-agent communication framework with a rules engine, built in C# with zero external dependencies.

## Overview

MafiaAgentSystem consists of two core libraries and a demonstration application:

| Component | Description |
|-----------|-------------|
| **RulesEngine** | Expression tree-based business rules engine |
| **AgentRouting** | Multi-agent message routing with middleware pipeline |
| **MafiaDemo** | Mafia family simulation demonstrating both systems |

## Architecture

### Solution Structure

```mermaid
flowchart TB
    subgraph Solutions["Solutions"]
        RulesSln["RulesEngine.sln"]
        AgentSln["AgentRouting.sln"]
    end

    subgraph CoreLibraries["Core Libraries"]
        RulesEngine["RulesEngine<br/>(Expression tree rules)"]
        AgentRouting["AgentRouting<br/>(Message routing + middleware)"]
    end

    subgraph DemoApps["Demo Applications"]
        RE_Demo["RulesEngine.Demo"]
        RE_AgentDemo["RulesEngine.AgentDemo"]
        AR_MiddlewareDemo["AgentRouting.MiddlewareDemo"]
        AR_AdvancedDemo["AgentRouting.AdvancedMiddlewareDemo"]
        MafiaDemo["AgentRouting.MafiaDemo"]
    end

    subgraph TestProjects["Test Projects"]
        RE_Tests["RulesEngine.Tests"]
        AR_Tests["AgentRouting.Tests"]
        MD_Tests["MafiaDemo.Tests"]
        TestFramework["TestRunner.Framework"]
    end

    AgentRouting -->|"references"| RulesEngine

    RE_Demo --> RulesEngine
    RE_AgentDemo --> RulesEngine
    AR_MiddlewareDemo --> AgentRouting
    AR_AdvancedDemo --> AgentRouting
    MafiaDemo --> AgentRouting
    MafiaDemo --> RulesEngine

    RE_Tests --> RulesEngine
    RE_Tests --> TestFramework
    AR_Tests --> AgentRouting
    AR_Tests --> TestFramework
    MD_Tests --> MafiaDemo
    MD_Tests --> TestFramework
```

### RulesEngine Class Hierarchy

```mermaid
classDiagram
    class IRule~T~ {
        <<interface>>
        +Id: string
        +Name: string
        +Priority: int
        +Evaluate(T fact) bool
        +Execute(T fact) RuleResult
    }

    class Rule~T~ {
        -_compiledCondition: Func~T,bool~
        -_actions: List~Action~T~~
        +Condition: Expression
        +WithAction(Action~T~) Rule~T~
    }

    class CompositeRule~T~ {
        -_rules: List~IRule~T~~
        -_operator: CompositeOperator
    }

    class IAsyncRule~T~ {
        <<interface>>
        +EvaluateAsync(T, CancellationToken) Task~bool~
        +ExecuteAsync(T, CancellationToken) Task~RuleResult~
    }

    class IRulesEngine~T~ {
        <<interface>>
        +RegisterRule(IRule~T~)
        +RegisterAsyncRule(IAsyncRule~T~)
        +Execute(T) RulesEngineResult
        +ExecuteAsync(T, CancellationToken) Task
    }

    class RulesEngineCore~T~ {
        -_rules: List~IRule~T~~
        -_asyncRules: List~IAsyncRule~T~~
        -_options: RulesEngineOptions
        +EvaluateAll(T)
        +GetMetrics(string) RulePerformanceMetrics
    }

    class ImmutableRulesEngine~T~ {
        -_rules: ImmutableList~IRule~T~~
        +WithRule(IRule~T~) ImmutableRulesEngine~T~
        +WithoutRule(string) ImmutableRulesEngine~T~
    }

    class RuleBuilder~T~ {
        +WithId(string) RuleBuilder~T~
        +When(Expression) RuleBuilder~T~
        +And(Expression) RuleBuilder~T~
        +Or(Expression) RuleBuilder~T~
        +Then(Action~T~) RuleBuilder~T~
        +Build() Rule~T~
    }

    IRule~T~ <|.. Rule~T~
    IRule~T~ <|.. CompositeRule~T~
    IRulesEngine~T~ <|.. RulesEngineCore~T~
    RuleBuilder~T~ ..> Rule~T~ : creates
```

### AgentRouting Class Hierarchy

```mermaid
classDiagram
    class IAgent {
        <<interface>>
        +Id: string
        +Name: string
        +Capabilities: AgentCapabilities
        +ProcessMessageAsync(AgentMessage, CancellationToken) Task~MessageResult~
        +CanHandle(AgentMessage) bool
    }

    class AgentBase {
        <<abstract>>
        #Logger: IAgentLogger
        #HandleMessageAsync(AgentMessage, CancellationToken) Task~MessageResult~
    }

    class AgentRouter {
        -_agents: List~IAgent~
        -_routingEngine: IRulesEngine~RoutingContext~
        -_pipeline: IMiddlewarePipeline
        +RegisterAgent(IAgent)
        +UseMiddleware(IAgentMiddleware)
        +RouteMessageAsync(AgentMessage, CancellationToken) Task~MessageResult~
    }

    class IAgentMiddleware {
        <<interface>>
        +InvokeAsync(AgentMessage, MessageDelegate, CancellationToken) Task~MessageResult~
    }

    class MiddlewareBase {
        <<abstract>>
        #ContinueAsync(AgentMessage, MessageDelegate, CancellationToken)
        #ShortCircuit(string) MessageResult
    }

    class IMiddlewarePipeline {
        <<interface>>
        +Use(IAgentMiddleware) IMiddlewarePipeline
        +Build(MessageDelegate) MessageDelegate
    }

    IAgent <|.. AgentBase
    IAgentMiddleware <|.. MiddlewareBase
    AgentRouter --> IAgent : routes to
    AgentRouter --> IMiddlewarePipeline : uses
    AgentRouter --> IRulesEngine : uses for routing
```

### Message Flow & Middleware Pipeline

```mermaid
flowchart LR
    subgraph Client["Client"]
        Sender["Message Sender"]
    end

    subgraph Pipeline["Middleware Pipeline"]
        direction TB
        M1["LoggingMiddleware"]
        M2["ValidationMiddleware"]
        M3["RateLimitMiddleware"]
        M4["AuthenticationMiddleware"]
        M5["CircuitBreakerMiddleware"]
        M6["MetricsMiddleware"]
    end

    subgraph Router["AgentRouter"]
        Rules["RulesEngineCore"]
        Routing["Route Selection"]
    end

    subgraph Agents["Agents"]
        A1["Agent 1"]
        A2["Agent 2"]
        A3["Agent N"]
    end

    Sender -->|"AgentMessage"| M1
    M1 --> M2 --> M3 --> M4 --> M5 --> M6
    M6 --> Rules
    Rules --> Routing
    Routing --> A1
    Routing --> A2
    Routing --> A3
```

### Available Middleware

| Common Middleware | Advanced Middleware |
|-------------------|---------------------|
| LoggingMiddleware | DistributedTracingMiddleware |
| TimingMiddleware | SemanticRoutingMiddleware |
| ValidationMiddleware | MessageTransformationMiddleware |
| RateLimitMiddleware | MessageQueueMiddleware |
| CachingMiddleware | ABTestingMiddleware |
| RetryMiddleware | FeatureFlagsMiddleware |
| CircuitBreakerMiddleware | AgentHealthCheckMiddleware |
| MetricsMiddleware | WorkflowOrchestrationMiddleware |
| AuthenticationMiddleware | |
| PriorityBoostMiddleware | |
| EnrichmentMiddleware | |
| AnalyticsMiddleware | |

### MafiaDemo Agent Hierarchy

```mermaid
flowchart TB
    subgraph Hierarchy["Mafia Family Hierarchy"]
        direction TB

        GF["Godfather<br/>(Don Vito Corleone)<br/>Final decisions, strategy"]

        subgraph SecondLevel["Second Level"]
            UB["Underboss<br/>(Peter Clemenza)<br/>Daily operations"]
            CON["Consigliere<br/>(Tom Hagen)<br/>Legal advisor"]
        end

        subgraph Capos["Capos"]
            C1["Capo<br/>(Sonny Corleone)<br/>Territory manager"]
        end

        subgraph Soldiers["Soldiers"]
            S1["Soldier<br/>(Luca Brasi)<br/>Enforcement"]
            S2["Soldier<br/>(Paulie Gatto)<br/>Collections"]
        end

        subgraph Player["Player System"]
            PA["PlayerAgent<br/>Interactive gameplay"]
            MS["MissionSystem<br/>Missions & rewards"]
        end
    end

    GF --> UB
    GF --> CON
    UB --> C1
    C1 --> S1
    C1 --> S2

    PA --> MS
    PA -.->|"interacts with"| GF
    PA -.->|"interacts with"| UB
    PA -.->|"interacts with"| C1

    subgraph Inheritance["Class Inheritance"]
        AB["AgentBase"]
        AA["AutonomousAgent"]
        AA --> AB
    end

    GF -.-> AA
    UB -.-> AA
    CON -.-> AA
    C1 -.-> AA
    S1 -.-> AA
```

## Quick Start

### Prerequisites

- .NET 8.0 SDK

### Build

```bash
# Restore (offline - no NuGet access needed)
dotnet restore AgentRouting/AgentRouting.sln --source /nonexistent

# Build
dotnet build AgentRouting/AgentRouting.sln --no-restore
```

### Run Tests

```bash
# Build test projects
dotnet build Tests/RulesEngine.Tests/ --no-restore
dotnet build Tests/AgentRouting.Tests/ --no-restore
dotnet build Tests/MafiaDemo.Tests/ --no-restore
dotnet build Tests/TestRunner/ --no-restore

# Run all tests
dotnet run --project Tests/TestRunner/ --no-build
```

### Run MafiaDemo

```bash
dotnet run --project AgentRouting/AgentRouting.MafiaDemo/
```

## Project Structure

```
MafiaAgentSystem/
├── RulesEngine/
│   ├── RulesEngine/              # Core rules engine library
│   │   ├── Core/                 # IRule, Rule, RulesEngineCore, etc.
│   │   ├── Enhanced/             # RuleValidation
│   │   └── Examples/             # Example rules
│   ├── RulesEngine.Demo/         # Basic demo
│   └── RulesEngine.AgentDemo/    # Agent integration demo
│
├── AgentRouting/
│   ├── AgentRouting/             # Core routing library
│   │   ├── Core/                 # IAgent, AgentRouter, AgentMessage
│   │   ├── Middleware/           # 20+ middleware implementations
│   │   ├── Infrastructure/       # StateStore, SystemClock
│   │   ├── Configuration/        # Defaults
│   │   └── DependencyInjection/  # ServiceContainer
│   ├── AgentRouting.MafiaDemo/   # Mafia game demo
│   ├── AgentRouting.MiddlewareDemo/
│   └── AgentRouting.AdvancedMiddlewareDemo/
│
└── Tests/
    ├── TestRunner.Framework/     # Custom test framework (zero dependencies)
    ├── TestRunner/               # Test runner
    ├── RulesEngine.Tests/
    ├── AgentRouting.Tests/
    └── MafiaDemo.Tests/
```

## Key Features

### RulesEngine

- **Expression Trees**: Type-safe, compiled rule conditions
- **Fluent Builder**: Readable rule construction API
- **Composite Rules**: AND/OR/NOT combinations
- **Async Support**: Rules with I/O operations
- **Performance Tracking**: Execution metrics per rule
- **Thread-Safe Variant**: `ImmutableRulesEngine<T>`

### AgentRouting

- **Message Routing**: Rules-based agent selection
- **Middleware Pipeline**: ASP.NET Core-style request pipeline
- **20+ Built-in Middleware**: Logging, caching, rate limiting, circuit breaker, etc.
- **Agent Capabilities**: Skill-based routing
- **Broadcast Support**: Send to multiple agents

### MafiaDemo

- **Hierarchical Agents**: Godfather → Underboss → Capos → Soldiers
- **Autonomous Decisions**: Personality-driven agent behavior
- **Player Mode**: Interactive gameplay with missions
- **Rules Integration**: Game logic via RulesEngine

## Origins

> The story of how a deep dive into expression trees led to a production-ready rules engine and agent communication platform.

This project began as an exploration of **C# expression trees** - a powerful but often overlooked feature of the .NET framework. What started as a learning exercise evolved into a comprehensive agent-to-agent communication system.

### The Journey

**Expression Trees → Rules Engine → Agent Routing → MafiaDemo**

1. **Expression Trees**: Unlike compiled delegates (opaque black boxes), expression trees represent **code as data** - enabling inspection, modification, and composition of logic at runtime.

2. **The "Aha" Moment**: Expression trees are perfect for building a **rules engine** where business rules become first-class data that can be inspected, prioritized, and tested in isolation.

3. **Agent Communication**: Rules engines make decisions. Agents make decisions. This led to **AgentRouting** - a message routing system where agents use rules to decide how to handle, forward, or escalate messages.

4. **MafiaDemo**: To stress-test both systems, we built a mafia family simulation where hierarchy = routing, decisions = rules, and communication = message passing.

### Key Insight

> **Expression trees unlock a fundamental shift: rules become first-class data.**

```csharp
// Regular delegate - opaque, can only execute
Func<Order, bool> isLarge = order => order.Total > 1000;

// Expression tree - inspectable AND executable
Expression<Func<Order, bool>> isLarge = order => order.Total > 1000;
// Can examine: body.NodeType == GreaterThan, body.Left == order.Total, body.Right == 1000
```

This separation of rule definitions from execution logic enables dynamic business rules, configurable workflows, intelligent agent routing, and auditable decision-making.

For the full story, see [ORIGINS.md](ORIGINS.md).

## Documentation

| Document | Description |
|----------|-------------|
| [ORIGINS.md](ORIGINS.md) | How expression trees led to this architecture |
| [CLAUDE.md](CLAUDE.md) | Development guide and commands |
| [RulesEngine/README.md](RulesEngine/README.md) | RulesEngine documentation |
| [AgentRouting/README.md](AgentRouting/README.md) | AgentRouting documentation |
| [AgentRouting/MIDDLEWARE_EXPLAINED.md](AgentRouting/MIDDLEWARE_EXPLAINED.md) | Middleware tutorial |
| [MafiaDemo/ARCHITECTURE.md](AgentRouting/AgentRouting.MafiaDemo/ARCHITECTURE.md) | Game architecture |

## License

Educational project - use freely for learning and building.
