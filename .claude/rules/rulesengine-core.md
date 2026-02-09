---
paths:
  - "RulesEngine/RulesEngine/**/*.cs"
  - "Tests/RulesEngine.Tests/**/*.cs"
---

# RulesEngine Core — SOPs and Reference

## Before Modifying Rules Engine Code

1. Understand which layer you're in:
   - `Core/` — engine, rules, builders, results (high impact, many consumers)
   - `Enhanced/` — validation, analysis, debugging (lower impact)
2. Check if MafiaDemo uses the API you're changing — it has 8 engine instances with ~98 rules
3. Run RulesEngine tests first to establish baseline:
   `dotnet run --project Tests/TestRunner/ --no-build -- Tests/RulesEngine.Tests/bin/Debug/net8.0/RulesEngine.Tests.dll`

## Thread Safety Invariants

`RulesEngineCore<T>` uses `ReaderWriterLockSlim`. When modifying:
- Rule execution acquires a read lock (concurrent reads OK)
- Rule registration/removal acquires a write lock (exclusive)
- The sorted rules cache invalidates on any mutation — do not bypass this
- `ImmutableRulesEngine<T>` is the lock-free alternative (returns new instances)

If you add a new public method, decide: does it read rules or mutate them? Acquire the correct lock.

## API Patterns (4 Ways to Create Rules)

| Pattern | When to use |
|---------|-------------|
| `IRule<T>` implementation | Complex rules with custom logic |
| `engine.AddRule(...)` | Quick inline definitions |
| `RuleBuilder<T>` | Readable fluent composition |
| `CompositeRuleBuilder<T>` | Combining existing rules with And/Or |

## Rule Validation Contract

Rules are validated on registration. These invariants must hold:
- `null` rule → `ArgumentNullException`
- Empty/null `Id` → `RuleValidationException`
- Empty/null `Name` → `RuleValidationException`
- Duplicate `Id` → `RuleValidationException` (unless `AllowDuplicateRuleIds = true`)

Do not weaken these validations without updating all tests that assert them.

## Execution Modes

- `Execute(fact)` — returns results, does not modify fact
- `EvaluateAll(fact)` — applies all matching rules, modifies fact in-place
- `ExecuteAsync(fact, ct)` — async execution with cancellation

`StopOnFirstMatch` affects all three modes differently. If changing stop behavior, verify all three paths.

## Key Files

- `Core/IRulesEngine.cs`, `Core/IRule.cs` — interfaces
- `Core/IResults.cs` — `IRuleResult`, `IRulesEngineResult`, `IRuleExecutionResult<T>`
- `Core/RulesEngineCore.cs` — main engine + `ImmutableRulesEngine<T>` at end of file
- `Core/RuleBuilder.cs` — `RuleBuilder<T>` and `CompositeRuleBuilder<T>`
- `Core/AsyncRule.cs` — `IAsyncRule<T>` and `AsyncRuleBuilder<T>`
- `Enhanced/RuleValidation.cs` — validator, analyzer, debuggable rules

## After Changes

1. Run RulesEngine tests
2. Run MafiaDemo tests (consumer): `dotnet run --project Tests/TestRunner/ --no-build -- Tests/MafiaDemo.Tests/bin/Debug/net8.0/MafiaDemo.Tests.dll`
3. If you changed `IRule<T>` or `IRulesEngine<T>`, also run RulesEngine.Linq tests — they share concepts
