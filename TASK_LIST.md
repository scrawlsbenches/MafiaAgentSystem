# MafiaAgentSystem Task List

> **Generated**: 2026-01-31
> **Based on**: Comprehensive code review of RulesEngine, AgentRouting, and MafiaDemo
> **Constraint**: All tasks are 2-4 hours, none exceeding 1 day

---

## Overview

| Priority | Category | Task Count | Total Estimated Hours |
|----------|----------|------------|----------------------|
| P0 | Critical Fixes | 4 tasks | 10-14 hours |
| P1 | Core Library Improvements | 8 tasks | 24-32 hours |
| P2 | MafiaDemo Completion | 10 tasks | 30-40 hours |
| P3 | Testing & Quality | 8 tasks | 24-32 hours |
| P4 | Documentation & Polish | 6 tasks | 16-22 hours |

**Total**: 36 tasks, ~104-140 hours (~3-4 weeks at full pace)

---

## P0: Critical Fixes (Must Do First)

These tasks block other work and must be completed before proceeding.

### Task P0-1: Fix GameEngine.cs Compilation Errors
**Estimated Time**: 3-4 hours
**Dependencies**: None
**Files**: `AgentRouting/AgentRouting.MafiaDemo/Game/GameEngine.cs`

**Problem**: `MafiaGameEngine` attempts to instantiate abstract `AutonomousAgent` class with non-existent properties (`AgentId`, `Personality`, `ActionCooldown`).

**Subtasks**:
- [ ] Analyze the mismatch between `AutonomousAgent` abstract class and usage in `MafiaGameEngine`
- [ ] Create a simple `GameAgent` data class for game state tracking (separate from IAgent)
- [ ] Update `MafiaGameEngine.InitializeGame()` to use the new data class
- [ ] Update `ProcessAutonomousActions()` and `DecideAction()` methods
- [ ] Verify compilation with `dotnet build`

**Acceptance Criteria**:
- `dotnet build` completes without errors
- Game engine initializes without runtime exceptions

---

### Task P0-2: Run Full Build and Identify All Compilation Issues
**Estimated Time**: 2-3 hours
**Dependencies**: None
**Files**: All `.csproj` files

**Subtasks**:
- [ ] Run `dotnet build` from repository root
- [ ] Document all compilation errors and warnings
- [ ] Categorize errors by project (RulesEngine, AgentRouting, MafiaDemo, Tests)
- [ ] Create fix plan for each error
- [ ] Fix simple issues (missing usings, typos, etc.)

**Acceptance Criteria**:
- Complete list of all build issues documented
- All trivial fixes applied
- Remaining issues logged as separate tasks

---

### Task P0-3: Run Test Suite and Establish Baseline
**Estimated Time**: 2-3 hours
**Dependencies**: P0-1, P0-2
**Files**: `Tests/TestRunner/`

**Subtasks**:
- [ ] Run `dotnet run --project Tests/TestRunner/`
- [ ] Document all passing and failing tests
- [ ] Identify tests that fail due to code issues vs test issues
- [ ] Create baseline test report
- [ ] Fix any test infrastructure issues

**Acceptance Criteria**:
- Test runner executes successfully
- Baseline pass/fail counts documented
- No test infrastructure failures

---

### Task P0-4: Fix Null Reference in AgentRouter
**Estimated Time**: 2 hours
**Dependencies**: P0-2
**Files**: `AgentRouting/AgentRouting/Core/AgentRouter.cs`

**Problem**: Line 109 passes `null!` to logger, hiding a design issue.

**Subtasks**:
- [ ] Review `LogMessageRouted` signature and usages
- [ ] Make `fromAgent` parameter nullable in `IAgentLogger.LogMessageRouted`
- [ ] Update `ConsoleAgentLogger` to handle null `fromAgent`
- [ ] Remove `null!` suppression
- [ ] Add unit test for routing without source agent

**Acceptance Criteria**:
- No `null!` suppressions in AgentRouter.cs
- Logger gracefully handles null source agent
- Existing tests still pass

---

## P1: Core Library Improvements

Improvements to the RulesEngine and AgentRouting core libraries.

### Task P1-1: Add Thread Safety to RulesEngineCore
**Estimated Time**: 3-4 hours
**Dependencies**: P0-2
**Files**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`

**Problem**: `_rules` list is not thread-safe for concurrent read/write.

**Subtasks**:
- [ ] Add `ReaderWriterLockSlim` field to `RulesEngineCore`
- [ ] Wrap `RegisterRule()` and `RegisterRules()` with write lock
- [ ] Wrap `RemoveRule()` with write lock
- [ ] Wrap `Execute()` rule enumeration with read lock
- [ ] Wrap `GetRules()` and `GetMatchingRules()` with read lock
- [ ] Implement `IDisposable` to dispose the lock
- [ ] Add concurrent access unit tests

**Acceptance Criteria**:
- No race conditions under concurrent access
- All existing tests pass
- New concurrency tests pass

---

### Task P1-2: Add ExecuteAsync with Cancellation Support
**Estimated Time**: 3-4 hours
**Dependencies**: P1-1
**Files**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`

**Subtasks**:
- [ ] Add `ExecuteAsync(T fact, CancellationToken ct = default)` method
- [ ] Add cancellation check before each rule evaluation
- [ ] Add cancellation check in parallel execution path
- [ ] Update `ThreadSafeRulesEngine` with async variant
- [ ] Add unit tests for cancellation behavior
- [ ] Add timeout test scenario

**Acceptance Criteria**:
- Async execution completes normally
- Cancellation stops execution promptly
- No resource leaks on cancellation

---

### Task P1-3: Cache Sorted Rules for Performance
**Estimated Time**: 2-3 hours
**Dependencies**: P1-1
**Files**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`

**Problem**: Rules are sorted on every `Execute()` call.

**Subtasks**:
- [ ] Add `_sortedRulesCache` field (nullable `IReadOnlyList<IRule<T>>`)
- [ ] Invalidate cache in `RegisterRule()`, `RegisterRules()`, `RemoveRule()`, `ClearRules()`
- [ ] Lazy-populate cache on first `Execute()` after modification
- [ ] Add benchmark comparing before/after performance
- [ ] Document caching behavior

**Acceptance Criteria**:
- Rules only sorted when rule set changes
- Measurable performance improvement for repeated executions
- All existing tests pass

---

### Task P1-4: Add Cache Eviction to CachingMiddleware
**Estimated Time**: 3-4 hours
**Dependencies**: P0-2
**Files**: `AgentRouting/AgentRouting/Middleware/CommonMiddleware.cs`

**Problem**: No cache size limit or eviction policy.

**Subtasks**:
- [ ] Add `maxEntries` constructor parameter with default (1000)
- [ ] Implement LRU tracking (last access time on cache entries)
- [ ] Add eviction logic when cache exceeds max size
- [ ] Add periodic cleanup of expired entries
- [ ] Add `Clear()` method for manual cache invalidation
- [ ] Add unit tests for eviction behavior
- [ ] Add memory pressure test

**Acceptance Criteria**:
- Cache never exceeds configured max entries
- Expired entries are cleaned up
- Tests verify eviction behavior

---

### Task P1-5: Extract Configuration Constants
**Estimated Time**: 2-3 hours
**Dependencies**: P0-2
**Files**: Multiple middleware and agent files

**Subtasks**:
- [ ] Create `AgentRoutingDefaults` static class with constants
- [ ] Move hardcoded values: MaxConcurrentMessages, retry counts, delays, etc.
- [ ] Create `MiddlewareDefaults` static class
- [ ] Update middleware constructors to use defaults
- [ ] Document all configuration options
- [ ] Update existing tests to use constants

**Acceptance Criteria**:
- No magic numbers in middleware code
- All defaults documented
- Easy to find and modify configuration

---

### Task P1-6: Add Rule Validation on Registration
**Estimated Time**: 2-3 hours
**Dependencies**: P0-2
**Files**: `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`, `RulesEngine/RulesEngine/Enhanced/RuleValidation.cs`

**Subtasks**:
- [ ] Add `ValidateOnRegister` option to `RulesEngineOptions`
- [ ] Create `RuleValidationException` class
- [ ] Validate rule ID is not null/empty
- [ ] Validate rule ID is unique (optional, configurable)
- [ ] Validate rule name is not null/empty
- [ ] Add option to validate rule expression compiles
- [ ] Add unit tests for validation

**Acceptance Criteria**:
- Invalid rules rejected with clear error messages
- Duplicate ID detection works (when enabled)
- Validation can be disabled for performance

---

### Task P1-7: Add Async Rule Support
**Estimated Time**: 3-4 hours
**Dependencies**: P1-2
**Files**: `RulesEngine/RulesEngine/Core/Rule.cs`, `RulesEngine/RulesEngine/Core/RulesEngineCore.cs`

**Subtasks**:
- [ ] Create `IAsyncRule<T>` interface with `EvaluateAsync` and `ExecuteAsync`
- [ ] Create `AsyncRule<T>` implementation
- [ ] Update `RulesEngineCore.ExecuteAsync` to detect and handle async rules
- [ ] Create `AsyncRuleBuilder<T>` for building async rules
- [ ] Add integration test with simulated I/O rule
- [ ] Document async rule usage patterns

**Acceptance Criteria**:
- Async rules execute without blocking
- Mixed sync/async rule sets work correctly
- Clear documentation on when to use async rules

---

### Task P1-8: Standardize DateTime Usage
**Estimated Time**: 2 hours
**Dependencies**: P0-2
**Files**: All files using `DateTime`

**Subtasks**:
- [ ] Search for all `DateTime.Now` usages
- [ ] Replace with `DateTime.UtcNow`
- [ ] Create `ISystemClock` interface for testability
- [ ] Add `SystemClock` default implementation
- [ ] Update middleware that uses DateTime to accept optional clock
- [ ] Add tests that use fake clock

**Acceptance Criteria**:
- No `DateTime.Now` in codebase
- Time-dependent tests are deterministic
- Clock injection available for testing

---

## P2: MafiaDemo Completion

Complete the MafiaDemo game to serve as a proper test bed for the libraries.

### Task P2-1: Design MafiaDemo Agent Architecture
**Estimated Time**: 2-3 hours
**Dependencies**: P0-1
**Files**: Design document (new)

**Subtasks**:
- [ ] Define agent hierarchy (Godfather → Underboss → Capo → Soldier → Associate)
- [ ] Define message types between agents (Order, Report, Request, Response)
- [ ] Define decision rules for each agent type
- [ ] Map to RulesEngine and AgentRouting APIs
- [ ] Create architecture diagram
- [ ] Document in `AgentRouting/AgentRouting.MafiaDemo/ARCHITECTURE.md`

**Acceptance Criteria**:
- Clear agent hierarchy documented
- Message flow documented
- RulesEngine integration points identified

---

### Task P2-2: Implement Godfather Agent
**Estimated Time**: 3-4 hours
**Dependencies**: P2-1
**Files**: `AgentRouting/AgentRouting.MafiaDemo/Agents/GodfatherAgent.cs` (new)

**Subtasks**:
- [ ] Create `GodfatherAgent` class extending `AgentBase`
- [ ] Implement `CanHandle()` for godfather-specific messages
- [ ] Implement `HandleMessageAsync()` for decision making
- [ ] Create decision rules using RulesEngine
- [ ] Add personality traits affecting decisions
- [ ] Integrate with `MafiaGameEngine`
- [ ] Add unit tests

**Acceptance Criteria**:
- Godfather processes orders and makes strategic decisions
- Rules engine drives decision logic
- Unit tests cover main scenarios

---

### Task P2-3: Implement Underboss Agent
**Estimated Time**: 3-4 hours
**Dependencies**: P2-2
**Files**: `AgentRouting/AgentRouting.MafiaDemo/Agents/UnderbossAgent.cs` (new)

**Subtasks**:
- [ ] Create `UnderbossAgent` class extending `AgentBase`
- [ ] Implement message routing to Capos
- [ ] Implement decision rules for operation management
- [ ] Add loyalty and ambition personality effects
- [ ] Integrate with `MafiaGameEngine`
- [ ] Add unit tests

**Acceptance Criteria**:
- Underboss routes Godfather orders to appropriate Capos
- Personality affects routing decisions
- Unit tests cover main scenarios

---

### Task P2-4: Implement Capo Agent
**Estimated Time**: 3-4 hours
**Dependencies**: P2-3
**Files**: `AgentRouting/AgentRouting.MafiaDemo/Agents/CapoAgent.cs` (new)

**Subtasks**:
- [ ] Create `CapoAgent` class extending `AgentBase`
- [ ] Implement territory management logic
- [ ] Implement soldier assignment and task delegation
- [ ] Create collection and expansion rules
- [ ] Add aggression and greed personality effects
- [ ] Integrate with `MafiaGameEngine`
- [ ] Add unit tests

**Acceptance Criteria**:
- Capo manages territory and soldiers
- Rules drive operational decisions
- Unit tests cover main scenarios

---

### Task P2-5: Implement Soldier Agent
**Estimated Time**: 2-3 hours
**Dependencies**: P2-4
**Files**: `AgentRouting/AgentRouting.MafiaDemo/Agents/SoldierAgent.cs` (new)

**Subtasks**:
- [ ] Create `SoldierAgent` class extending `AgentBase`
- [ ] Implement task execution (collection, intimidation, etc.)
- [ ] Implement success/failure probability based on skills
- [ ] Add loyalty effects on task execution
- [ ] Integrate with `MafiaGameEngine`
- [ ] Add unit tests

**Acceptance Criteria**:
- Soldiers execute tasks from Capos
- Success probability affected by personality/skills
- Unit tests cover main scenarios

---

### Task P2-6: Implement Game Rules Engine Integration
**Estimated Time**: 4 hours
**Dependencies**: P2-1
**Files**: `AgentRouting/AgentRouting.MafiaDemo/Rules/GameRules.cs` (new or update existing)

**Subtasks**:
- [ ] Create `GameRulesEngine` class wrapping `RulesEngineCore<GameContext>`
- [ ] Implement heat generation rules
- [ ] Implement revenue calculation rules
- [ ] Implement rival family response rules
- [ ] Implement win/lose condition rules
- [ ] Integrate with `MafiaGameEngine.ExecuteTurnAsync()`
- [ ] Add unit tests for each rule category

**Acceptance Criteria**:
- All game logic driven by rules engine
- Rules are inspectable and modifiable
- Unit tests verify rule behavior

---

### Task P2-7: Implement Agent Message Routing
**Estimated Time**: 3-4 hours
**Dependencies**: P2-2, P2-3, P2-4, P2-5
**Files**: `AgentRouting/AgentRouting.MafiaDemo/Routing/FamilyRouter.cs` (new)

**Subtasks**:
- [ ] Create `FamilyRouter` extending `MiddlewareAgentRouter`
- [ ] Configure routing rules for hierarchy (orders flow down, reports flow up)
- [ ] Add logging middleware for message tracing
- [ ] Add timing middleware for performance tracking
- [ ] Implement message forwarding for chain of command
- [ ] Add integration test showing full message flow

**Acceptance Criteria**:
- Messages route correctly through hierarchy
- Middleware pipeline processes all messages
- Integration test demonstrates full flow

---

### Task P2-8: Implement Interactive Game Loop
**Estimated Time**: 3-4 hours
**Dependencies**: P2-6, P2-7
**Files**: `AgentRouting/AgentRouting.MafiaDemo/Program.cs`

**Subtasks**:
- [ ] Refactor `Program.cs` to use new agent architecture
- [ ] Implement interactive command loop
- [ ] Add commands: status, order, bribe, expand, hit, peace, next, quit
- [ ] Display agent activity and message flow
- [ ] Add turn summary with metrics
- [ ] Handle game over conditions gracefully

**Acceptance Criteria**:
- Game playable from command line
- All commands functional
- Clear feedback on agent actions

---

### Task P2-9: Implement AI Autopilot Mode
**Estimated Time**: 3-4 hours
**Dependencies**: P2-8
**Files**: `AgentRouting/AgentRouting.MafiaDemo/AI/AutopilotController.cs` (new)

**Subtasks**:
- [ ] Create `AutopilotController` class
- [ ] Implement AI strategy rules using RulesEngine
- [ ] Add configurable AI personalities (aggressive, cautious, balanced)
- [ ] Implement turn-by-turn autonomous execution
- [ ] Add speed controls (fast, normal, step-by-step)
- [ ] Add game recording/replay capability

**Acceptance Criteria**:
- AI plays complete games autonomously
- Different personalities produce different outcomes
- Games can be watched or fast-forwarded

---

### Task P2-10: Add MafiaDemo Integration Tests
**Estimated Time**: 3-4 hours
**Dependencies**: P2-8, P2-9
**Files**: `Tests/TestRunner/Tests/MafiaDemoTests.cs` (new)

**Subtasks**:
- [ ] Create test class for MafiaDemo scenarios
- [ ] Test: Full game simulation (10 turns)
- [ ] Test: Agent hierarchy message flow
- [ ] Test: Rules engine integration
- [ ] Test: Win condition achievement
- [ ] Test: Lose condition triggers
- [ ] Test: Concurrent agent actions

**Acceptance Criteria**:
- All MafiaDemo scenarios tested
- Tests run in custom test runner
- No external dependencies

---

## P3: Testing & Quality

Improve test coverage and code quality.

### Task P3-1: Add Concurrency Tests for RulesEngine
**Estimated Time**: 3-4 hours
**Dependencies**: P1-1
**Files**: `Tests/TestRunner/Tests/ConcurrencyTests.cs` (new)

**Subtasks**:
- [ ] Create concurrency test class
- [ ] Test: Concurrent rule registration
- [ ] Test: Concurrent rule execution
- [ ] Test: Concurrent registration during execution
- [ ] Test: Stress test with many threads
- [ ] Use `Task.WhenAll` for parallel test execution
- [ ] Verify no exceptions or data corruption

**Acceptance Criteria**:
- All concurrency tests pass
- No race conditions detected
- Tests complete in reasonable time

---

### Task P3-2: Add Edge Case Tests for Rules
**Estimated Time**: 3-4 hours
**Dependencies**: P0-3
**Files**: `Tests/TestRunner/Tests/RuleEdgeCaseTests.cs` (new)

**Subtasks**:
- [ ] Test: Null fact handling
- [ ] Test: Empty rule set execution
- [ ] Test: Rule that throws exception
- [ ] Test: Rule with null condition result
- [ ] Test: Very long rule chains (100+ rules)
- [ ] Test: Circular rule dependencies (if applicable)
- [ ] Test: Unicode in rule names/IDs

**Acceptance Criteria**:
- All edge cases handled gracefully
- No unhandled exceptions
- Clear error messages for invalid inputs

---

### Task P3-3: Add Middleware Pipeline Tests
**Estimated Time**: 3-4 hours
**Dependencies**: P0-3
**Files**: `Tests/TestRunner/Tests/MiddlewarePipelineTests.cs` (new)

**Subtasks**:
- [ ] Test: Empty pipeline execution
- [ ] Test: Single middleware execution
- [ ] Test: Middleware ordering (first added = first executed)
- [ ] Test: Short-circuit behavior
- [ ] Test: Exception in middleware
- [ ] Test: Async middleware with delays
- [ ] Test: Middleware context sharing

**Acceptance Criteria**:
- Pipeline behavior fully tested
- Ordering guarantees verified
- Error handling verified

---

### Task P3-4: Add Rate Limiter Tests
**Estimated Time**: 2-3 hours
**Dependencies**: P0-3
**Files**: `Tests/TestRunner/Tests/RateLimitTests.cs` (new)

**Subtasks**:
- [ ] Test: Under limit allows requests
- [ ] Test: At limit blocks requests
- [ ] Test: Window expiration allows new requests
- [ ] Test: Different senders have separate limits
- [ ] Test: Concurrent requests near limit
- [ ] Test: Reset behavior

**Acceptance Criteria**:
- Rate limiting works as documented
- Window sliding works correctly
- Thread-safe under concurrent access

---

### Task P3-5: Add Circuit Breaker Tests
**Estimated Time**: 2-3 hours
**Dependencies**: P0-3
**Files**: `Tests/TestRunner/Tests/CircuitBreakerTests.cs` (new)

**Subtasks**:
- [ ] Test: Closed state allows requests
- [ ] Test: Failures increment counter
- [ ] Test: Threshold reached opens circuit
- [ ] Test: Open circuit fails fast
- [ ] Test: Timeout transitions to half-open
- [ ] Test: Success in half-open closes circuit
- [ ] Test: Failure in half-open reopens circuit

**Acceptance Criteria**:
- All circuit breaker states tested
- State transitions verified
- Timeout behavior correct

---

### Task P3-6: Add Performance Benchmarks
**Estimated Time**: 3-4 hours
**Dependencies**: P1-3
**Files**: `Benchmarks/` (new directory)

**Subtasks**:
- [ ] Create benchmark project (no external deps, simple timing)
- [ ] Benchmark: Rule evaluation (1, 10, 100, 1000 rules)
- [ ] Benchmark: With/without rule caching
- [ ] Benchmark: Sequential vs parallel execution
- [ ] Benchmark: Middleware pipeline overhead
- [ ] Generate baseline performance report
- [ ] Document performance characteristics

**Acceptance Criteria**:
- Benchmarks runnable without external dependencies
- Performance baseline documented
- Clear performance characteristics identified

---

### Task P3-7: Add Integration Tests for Agent Routing
**Estimated Time**: 3-4 hours
**Dependencies**: P0-3
**Files**: `Tests/TestRunner/Tests/AgentRoutingIntegrationTests.cs` (new)

**Subtasks**:
- [ ] Test: Message routes to correct agent
- [ ] Test: Routing rules with priorities
- [ ] Test: Default agent fallback
- [ ] Test: Broadcast to multiple agents
- [ ] Test: Agent capability matching
- [ ] Test: Agent status affects routing
- [ ] Test: Full pipeline with middleware

**Acceptance Criteria**:
- Routing behavior fully tested
- Integration with middleware verified
- All agent selection scenarios covered

---

### Task P3-8: Add Test Coverage Analysis
**Estimated Time**: 2-3 hours
**Dependencies**: P3-1 through P3-7
**Files**: Test output analysis

**Subtasks**:
- [ ] Run all tests and collect results
- [ ] Manually identify untested public methods
- [ ] Create coverage report by class
- [ ] Identify high-risk untested areas
- [ ] Prioritize additional test needs
- [ ] Document coverage status

**Acceptance Criteria**:
- Coverage report generated
- Critical gaps identified
- Prioritized list of needed tests

---

## P4: Documentation & Polish

Final polish and documentation improvements.

### Task P4-1: Update CLAUDE.md with New Patterns
**Estimated Time**: 2-3 hours
**Dependencies**: P1 tasks
**Files**: `CLAUDE.md`

**Subtasks**:
- [ ] Add async rule usage patterns
- [ ] Document thread-safe engine options
- [ ] Add cache configuration guidance
- [ ] Document middleware ordering best practices
- [ ] Add troubleshooting section
- [ ] Update code examples to match current API

**Acceptance Criteria**:
- CLAUDE.md reflects current codebase
- All new features documented
- Examples compile and run

---

### Task P4-2: Add XML Documentation to Public APIs
**Estimated Time**: 3-4 hours
**Dependencies**: P0 tasks
**Files**: All public classes and methods

**Subtasks**:
- [ ] Add `<summary>` to all public classes
- [ ] Add `<param>` to all public method parameters
- [ ] Add `<returns>` to all non-void methods
- [ ] Add `<exception>` where exceptions are thrown
- [ ] Add `<example>` for complex methods
- [ ] Enable XML documentation generation in .csproj

**Acceptance Criteria**:
- All public APIs documented
- No documentation warnings on build
- IntelliSense shows helpful information

---

### Task P4-3: Create API Reference Documentation
**Estimated Time**: 3-4 hours
**Dependencies**: P4-2
**Files**: `docs/API.md` (new)

**Subtasks**:
- [ ] Document RulesEngine public API
- [ ] Document AgentRouting public API
- [ ] Document Middleware public API
- [ ] Add quick reference tables
- [ ] Add common usage patterns
- [ ] Cross-reference with CLAUDE.md

**Acceptance Criteria**:
- Complete API reference
- Easy to navigate
- Matches actual code

---

### Task P4-4: Create MafiaDemo Player Guide
**Estimated Time**: 2-3 hours
**Dependencies**: P2-8
**Files**: `AgentRouting/AgentRouting.MafiaDemo/PLAYER_GUIDE.md` (new)

**Subtasks**:
- [ ] Write game overview and objectives
- [ ] Document all commands with examples
- [ ] Explain game mechanics (heat, reputation, wealth)
- [ ] Describe agent hierarchy and behaviors
- [ ] Add strategy tips
- [ ] Include sample game session transcript

**Acceptance Criteria**:
- New players can understand and play the game
- All mechanics explained
- Engaging and readable

---

### Task P4-5: Clean Up Code Style and Warnings
**Estimated Time**: 2-3 hours
**Dependencies**: All P0 tasks
**Files**: All `.cs` files

**Subtasks**:
- [ ] Fix all compiler warnings
- [ ] Remove unused usings
- [ ] Ensure consistent naming conventions
- [ ] Remove dead code and commented-out sections
- [ ] Ensure consistent brace style
- [ ] Add `#nullable enable` where missing

**Acceptance Criteria**:
- Zero compiler warnings
- Consistent code style
- No dead code

---

### Task P4-6: Create Release Checklist
**Estimated Time**: 2 hours
**Dependencies**: All tasks
**Files**: `RELEASE_CHECKLIST.md` (new)

**Subtasks**:
- [ ] Document build verification steps
- [ ] Document test verification steps
- [ ] List all deliverable files
- [ ] Create version numbering scheme
- [ ] Document changelog format
- [ ] Add pre-release review checklist

**Acceptance Criteria**:
- Clear release process documented
- Reproducible release steps
- Quality gates defined

---

## Task Dependency Graph

```
P0-1 ─┬─→ P0-3 ─→ P3-1 through P3-8
P0-2 ─┤
      ├─→ P1-1 ─→ P1-2 ─→ P1-7
      │         ↓
      │       P1-3
      │
      ├─→ P1-4, P1-5, P1-6, P1-8
      │
P0-4 ─┘

P0-1 ─→ P2-1 ─→ P2-2 ─→ P2-3 ─→ P2-4 ─→ P2-5
                  ↓       ↓       ↓       ↓
                P2-6 ←───────────────────┘
                  ↓
                P2-7 ─→ P2-8 ─→ P2-9 ─→ P2-10

P1-* ─→ P4-1, P4-2 ─→ P4-3
P2-8 ─→ P4-4
All ──→ P4-5, P4-6
```

---

## Suggested Sprint Plan

### Sprint 1 (Week 1): Foundation
- P0-1: Fix GameEngine.cs
- P0-2: Full build verification
- P0-3: Test baseline
- P0-4: Fix null reference
- P1-1: Thread safety

### Sprint 2 (Week 2): Core Improvements
- P1-2: Async execution
- P1-3: Rule caching
- P1-4: Cache eviction
- P1-5: Configuration extraction
- P1-6: Rule validation

### Sprint 3 (Week 3): MafiaDemo Part 1
- P2-1: Architecture design
- P2-2: Godfather agent
- P2-3: Underboss agent
- P2-4: Capo agent
- P2-5: Soldier agent

### Sprint 4 (Week 4): MafiaDemo Part 2
- P2-6: Game rules integration
- P2-7: Agent routing
- P2-8: Interactive loop
- P2-9: AI autopilot
- P2-10: Integration tests

### Sprint 5 (Week 5): Quality & Polish
- P3-1 through P3-8: Testing
- P4-1 through P4-6: Documentation

---

## Quick Reference: Task by File

| File | Related Tasks |
|------|---------------|
| `RulesEngineCore.cs` | P1-1, P1-2, P1-3, P1-6 |
| `AgentRouter.cs` | P0-4 |
| `CommonMiddleware.cs` | P1-4 |
| `GameEngine.cs` | P0-1, P2-6, P2-8 |
| `Tests/` | P0-3, P3-1 through P3-8 |
| `CLAUDE.md` | P4-1 |

---

## Notes

- Tasks are designed to be completable in a single focused session
- Dependencies should be respected to avoid rework
- P0 tasks are blockers and should be completed first
- P2 tasks can be parallelized across multiple developers if available
- P3 and P4 can be interleaved with P2 work

**Last Updated**: 2026-01-31
