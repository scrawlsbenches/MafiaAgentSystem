---
paths:
  - "RulesEngine.Linq/**/*.cs"
  - "Tests/RulesEngine.Linq.Tests/**/*.cs"
---

# RulesEngine.Linq — SOPs and Reference

## Design Invariants (Never Violate)

1. **Serialization is sacred** — rules may be serialized for remote execution. No shortcuts that assume local-only.
2. **Expression trees stay expressions** — `Expression<Func<T, bool>>` is the currency, not `Func<T, bool>`. Inspectability is the point.
3. **Cross-fact queries are symbolic** — `FactQueryExpression` is a marker node containing only a Type. It carries no runtime data.

## Before Modifying Code

1. Identify which layer you're touching:
   - `Abstractions.cs` — interfaces (highest impact, all implementations depend on these)
   - `Implementation.cs` — `RulesContext`, `RuleSet<T>`, `RuleSession` (core behavior)
   - `Rule.cs` — `Rule<T>` with expression detection and rewriting
   - `Provider.cs` — `FactQueryExpression`, `FactQueryRewriter` (expression tree plumbing)
   - `DependencyAnalysis.cs` — `DependentRule<T>`, `DependencyGraph`, `ContextConditionProjector`
   - `Validation.cs`, `Constraints.cs`, `ClosureExtractor.cs` — supporting infrastructure
2. Run baseline: `dotnet run --project Tests/TestRunner/ --no-build -- Tests/RulesEngine.Linq.Tests/bin/Debug/net8.0/RulesEngine.Linq.Tests.dll`
3. Read `RulesEngine.Linq/AUDIT_2026-02-05.md` for known issues and deferred items

## Evaluation Dispatch — 4 Paths

When modifying `RuleSession.EvaluateFactSet<T>()`, understand all 4 dispatch paths:

| Path | Condition | Mechanism |
|------|-----------|-----------|
| 1. DependentRule | `rule is DependentRule<T>` | `EvaluateWithContext(fact, session)` directly |
| 2. Rule<T> + rewriter | `rule is Rule<T>` and `RequiresRewriting` | `Rule<T>.GetOrCompileWithRewriter(rewriter)` |
| 3. Generic rewriter | Any `IRule<T>` with `ContainsFactQuery(Condition)` | Session rewrites and caches |
| 4. Standard | No cross-fact references | `rule.Evaluate(fact)` directly |

If you change one path, verify the others still work. Path 3 is the generic fallback for custom `IRule<T>` implementations.

## Known Issue: Rule<T> Swallows Exceptions

`Rule<T>.Evaluate()` and `Execute()` have internal try-catch that returns `false`/`RuleResult.Error`, preventing the session's error handler from capturing failures in `IEvaluationResult.Errors`. `DependentRule<T>` does NOT have this problem — its exceptions propagate correctly. Be aware of this asymmetry when debugging missing errors.

## Cross-Fact Query Patterns

Two patterns produce identical expression trees:

**Pattern 1 (closure):** `var agents = context.Facts<Agent>();` then reference in lambda — `FactQueryable<T>` detected automatically

**Pattern 2 (explicit):** `DependentRule<T>` with `(t, ctx) =>` — `ContextConditionProjector` transforms `ctx.Facts<T>()` to `FactQueryExpression`

Both unify to `FactQueryExpression` marker nodes. If you change how either pattern works, verify the other still produces the same expression tree form.

## Session Lifecycle States

Sessions transition: `Created → Evaluating → Evaluated`. Verify:
- `InsertFact` only works in `Created` state
- `Evaluate` transitions to `Evaluating` then `Evaluated`
- Re-evaluation from `Evaluated` state clears caches properly (was a P0 bug, now fixed)

## Deferred Design Issues

These are known but intentionally unresolved:
- #7: `DependentRule.Execute()` skips condition check (session handles it; trap for direct callers)
- #8: `FindByKey()` ignores `HasKey()` schema config (needs schema access from session)
- #9: `RegisteredFactTypes` returns inserted types, not registered (same root cause as #8)
- #14: Some false-positive test naming

## After Changes

1. Run all 332 LINQ tests
2. Verify no regressions in cross-fact evaluation (tests in `CrossFactRulesTests.cs`, `DependencyAnalysisTests.cs`)
3. If you touched `Abstractions.cs`, verify `AgentCommunicationRulesTests.cs` still passes
