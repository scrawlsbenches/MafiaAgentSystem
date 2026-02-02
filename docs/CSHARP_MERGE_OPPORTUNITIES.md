# C# File Merge Opportunities Report

> **NOTE**: This report is a point-in-time analysis and will become stale as the codebase evolves.
> Generated: 2026-02-02
> Based on file metadata and content analysis.

---

## Summary: C# File Merge Opportunities

---

### **CRITICAL PRIORITY**

| Area | Issue | Files | Estimated Savings |
|------|-------|-------|-------------------|
| **MafiaDemo Agents** | Duplicate agent hierarchies | `MafiaAgents.cs` + `AutonomousAgents.cs` | 400-500 lines |
| **RulesEngine Core** | Duplicate execution methods | `RulesEngineCore.cs` + `ThreadSafeRulesEngine.cs` | 150+ lines |
| **RulesEngine Builders** | Duplicate builder patterns | `RuleBuilder.cs` + `AsyncRule.cs` | 80+ lines |

---

### **HIGH PRIORITY**

| Area | Issue | Files | Recommendation |
|------|-------|-------|----------------|
| **MafiaDemo Rules** | Parallel rule systems | `RulesBasedEngine.cs` + `AdvancedRulesEngine.cs` | Combine into single `GameRulesEngine.cs` |
| **Middleware** | Observability duplication | `LoggingMiddleware`, `TimingMiddleware`, `MetricsMiddleware`, `DistributedTracingMiddleware` | Create unified `ObservabilityMiddleware.cs` |
| **Middleware** | State management boilerplate | `RateLimitMiddleware`, `CachingMiddleware`, `CircuitBreakerMiddleware` | Extract `StatefulMiddlewareBase` |

---

### **MEDIUM PRIORITY**

| Area | Issue | Files | Recommendation |
|------|-------|-------|----------------|
| **Agent Core** | Too many classes in one file | `Agent.cs` (307 lines, 7 classes) | Extract `AgentLogger.cs`, `AgentMessage.cs` |
| **Infrastructure** | Thin utility files | `SystemClock.cs` + `StateStore.cs` | Merge into `InfrastructureServices.cs` |
| **RulesEngine** | Overloaded file | `RuleValidation.cs` (4 concerns) | Split into 3 focused files |
| **Middleware** | Trivial middleware classes | 4 small middlewares in `CommonMiddleware.cs` | Consolidate into `SimpleMiddleware.cs` |
| **MafiaDemo** | Tiny config file | `GameTimingOptions.cs` (107 lines) | Merge with `GameEngine.cs` |

---

### **Estimated Total Impact**

| Metric | Before | After (Est.) |
|--------|--------|--------------|
| Duplicated code | ~1,200 lines | ~200 lines |
| File count reduction | N/A | 5-8 fewer files |
| Maintainability | Scattered | Consolidated |
