# C# File Merge Opportunities Report

> **NOTE**: This report is a point-in-time analysis and will become stale as the codebase evolves.
> Generated: 2026-02-02
> Last reviewed: 2026-02-02
> Based on file metadata and content analysis.

---

## Completed Consolidations

The following merge opportunities from the original report have been **completed**:

| Area | Original Files | Result | Commit |
|------|---------------|--------|--------|
| **MafiaDemo Agents** | `MafiaAgents.cs` + `AutonomousAgents.cs` | Consolidated into `MafiaAgents.cs` | `22d1577` |
| **RulesEngine Core** | `RulesEngineCore.cs` + `ThreadSafeRulesEngine.cs` | Consolidated into `RulesEngineCore.cs` | `3888f81` |
| **MafiaDemo Rules** | `RulesBasedEngine.cs` + `AdvancedRulesEngine.cs` | Combined into `GameRulesEngine.cs` | `c643d07` |

---

## Remaining Opportunities

### **LOW PRIORITY** (Previously marked as Critical)

| Area | Issue | Files | Notes |
|------|-------|-------|-------|
| **RulesEngine Builders** | Similar builder patterns | `RuleBuilder.cs` + `AsyncRule.cs` | These serve different purposes (sync vs async rules with different type signatures). Merging may reduce clarity. |

---

### **MEDIUM PRIORITY**

| Area | Issue | Files | Recommendation |
|------|-------|-------|----------------|
| **Middleware** | Observability duplication | `LoggingMiddleware`, `TimingMiddleware`, `MetricsMiddleware`, `DistributedTracingMiddleware` | Consider unified `ObservabilityMiddleware.cs` |
| **Middleware** | State management boilerplate | `RateLimitMiddleware`, `CachingMiddleware`, `CircuitBreakerMiddleware` | Consider extracting `StatefulMiddlewareBase` |
| **Infrastructure** | Thin utility files | `SystemClock.cs` + `StateStore.cs` | Could merge into `InfrastructureServices.cs` |

---

### **LOW PRIORITY**

| Area | Issue | Files | Recommendation |
|------|-------|-------|----------------|
| **Agent Core** | Multiple classes in one file | `Agent.cs` | Could extract `AgentLogger.cs`, `AgentMessage.cs` if file grows |
| **RulesEngine** | Multiple concerns | `RuleValidation.cs` | Could split into focused files if complexity increases |
| **Middleware** | Small middleware classes | `CommonMiddleware.cs` | Keep as-is unless maintenance burden increases |
| **MafiaDemo** | Separate config file | `GameTimingOptions.cs` | Keep separate for single-responsibility |

---

### **Updated Impact Assessment**

| Metric | Original Estimate | Current Status |
|--------|-------------------|----------------|
| Critical items completed | 0/3 | **3/3** ✅ |
| High priority items completed | 0/3 | **1/3** (GameRulesEngine) |
| Estimated lines saved | ~1,200 | ~550-650 already saved |
| Remaining opportunities | All | Mostly optional/low priority |
