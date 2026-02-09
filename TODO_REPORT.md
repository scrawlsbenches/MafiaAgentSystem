# TODO Comments Report

Generated: 2026-02-09

**Total: 21 TODO comments** across 5 files, all in the `RulesEngine.Linq` subsystem.

No `FIXME`, `HACK`, `XXX`, or `UNDONE` comments were found.

---

## DependencyAnalysis.cs (8)

File: `RulesEngine.Linq/RulesEngine.Linq/DependencyAnalysis.cs`

| Line | TODO |
|------|------|
| 387 | Detect other IFactContext methods like `FindByKey<T>()` |
| 826 | Add method to update DependencyGraph based on analysis result |
| 827 | Add method to validate all rules in a RuleSet have valid dependencies |
| 879 | Handle `FindByKey<T>()` projection when needed |
| 1130 | Static `Rule.When<T>()` factory |
| 1138 | Handle self-referential dependencies |
| 1144 | Performance optimization — parallel rule evaluation when dependencies allow |
| 1147 | ContextConditionProjector — `FindByKey<T>()` support |

## AgentCommunicationDesign.cs (5)

File: `RulesEngine.Linq/RulesEngine.Linq/AgentCommunicationDesign.cs`

| Line | TODO |
|------|------|
| 175 | Implement InMemoryWorldState |
| 180 | Implement InMemoryMessageSession |
| 185 | Implement InMemoryRuleRegistry |
| 189 | Implement `PipelineBuilder<T>` and `MessagePipeline<T>` |
| 193 | Integrate with DependencyExtractor from CrossFactRulesDesign |

## CrossFactRulesDesign.cs (7)

File: `RulesEngine.Linq/RulesEngine.Linq/CrossFactRulesDesign.cs`

| Line | TODO |
|------|------|
| 862 | Combine with existing condition (should chain with AND logic) |
| 1298 | Integrate CrossFactRuleSet with existing `IRuleSet<T>` |
| 1303 | Integrate FactSchema with existing RulesContext |
| 1308 | Integrate NavigationResolver with RuleSession |
| 1312 | Add support for proposed API patterns (`context.Rules<T>()`, etc.) |
| 1318 | Self-referential dependency handling |
| 1322 | Performance optimizations |

## AgentRule.cs (1)

File: `RulesEngine.Linq/RulesEngine.Linq/AgentCommunication/Rules/AgentRule.cs`

| Line | TODO |
|------|------|
| 57 | Use DependencyExtractor in `EnsureCompiled()` to detect additional dependencies |

## AgentCommunicationDesignTests.cs (1)

File: `Tests/RulesEngine.Linq.AgentCommunication.Tests/AgentCommunicationDesignTests.cs`

| Line | TODO |
|------|------|
| 10 | File has build errors due to API changes in `IAgentRule<T>` |
