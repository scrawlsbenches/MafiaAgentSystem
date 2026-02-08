namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using TestRunner.Framework;
    using RulesEngine.Linq;
    using RulesEngine.Linq.Dependencies;
    using Mafia.Domain;

    /// <summary>
    /// Tests documenting known closure-related issues in the RulesEngine.Linq API.
    ///
    /// The architecture has two cross-fact patterns:
    ///   1. Closure capture:    Rule&lt;T&gt; + context.Facts&lt;T&gt;()
    ///   2. Context parameter:  DependentRule&lt;T&gt; + (fact, ctx) => ctx.Facts&lt;T&gt;()
    ///
    /// These take different code paths at evaluation time:
    ///   Closure path:  FactQueryRewriter substitutes FactQueryable with plain EnumerableQuery (no validation)
    ///   Context path:  Compiled lambda calls session.Facts&lt;T&gt;() → FactSet with FactSetQueryProvider (validates)
    ///
    /// The tests below document where these paths diverge in developer-visible ways.
    /// </summary>
    public class ClosureIssueTests
    {
        // =====================================================================
        // ISSUE 1: Facts<T>() at definition time vs evaluation time
        //
        // RulesContext.Facts<T>() returns FactQueryable<T> — a symbolic
        // placeholder that builds expression trees but CANNOT be enumerated.
        // RuleSession.Facts<T>() returns FactSet<T> — a live queryable backed
        // by actual data. Developers capture the first, expecting the second.
        // =====================================================================

        [Test]
        public void Issue1_ContextFacts_CannotBeEnumerated_AtDefinitionTime()
        {
            // A developer captures context.Facts<Agent>() to use in a rule.
            // It looks like a normal IQueryable, so they try to inspect it.
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            // This is a FactQueryable<Agent> — a dead queryable.
            // It throws if you try to enumerate it outside of session evaluation.
            var ex = Assert.Throws<InvalidOperationException>(() => agents.Count());
            Assert.Contains("cannot be executed directly", ex.Message);

            // ISSUE: The API returns IQueryable<T> from both context and session.
            // Nothing in the type system distinguishes "symbolic placeholder" from
            // "live data source". The developer discovers this at runtime.
        }

        [Test]
        public void Issue1_ContextFacts_CannotBeEnumerated_EvenWithWhere()
        {
            // LINQ chaining works (builds expression trees), but terminal
            // operations fail. This is extra confusing — .Where() succeeds,
            // .Any() throws.
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            // Building the expression tree succeeds (Where returns another FactQueryable)
            var filtered = agents.Where(a => a.Status == AgentStatus.Available);

            // But executing it fails
            var ex = Assert.Throws<InvalidOperationException>(() => filtered.Any());
            Assert.Contains("cannot be executed directly", ex.Message);
        }

        [Test]
        public void Issue1_SessionFacts_CAN_BeEnumerated_AtEvaluationTime()
        {
            // Inside a DependentRule action, ctx.Facts<T>() returns a live FactSet.
            // This works because the session provides real data.
            using var context = new RulesContext();
            context.GetRuleSet<Agent>().Add(new DependentRule<Agent>(
                "inspect", "Inspect facts",
                (a, ctx) => a.Status == AgentStatus.Available)
                .Then((a, ctx) =>
                {
                    // This WORKS — ctx is a RuleSession, Facts<Agent>() returns FactSet
                    var count = ctx.Facts<Agent>().Count();
                    a.Capabilities.Add($"saw-{count}-agents");
                }));

            using var session = context.CreateSession();
            session.Insert(new Agent { Id = "A1", Status = AgentStatus.Available });
            session.Insert(new Agent { Id = "A2", Status = AgentStatus.Available });
            var result = session.Evaluate();

            // The action ran and could enumerate facts
            var a1 = session.Facts<Agent>().First(a => a.Id == "A1");
            Assert.True(a1.Capabilities.Contains("saw-2-agents"));
        }

        // =====================================================================
        // ISSUE 2: Closure path skips validation that DependentRule enforces
        //
        // The ExpressionValidator checks that method calls are "translatable"
        // (safe for remote serialization). DependentRule goes through this
        // validator via FactSetQueryProvider. Rule<T> with closures does NOT —
        // FactQueryRewriter substitutes plain EnumerableQuery, bypassing validation.
        //
        // Result: the same predicate logic is accepted in Rule<T> but rejected
        // in DependentRule<T>.
        // =====================================================================

        [Test]
        public void Issue2_ClosurePath_AllowsUntranslatableMethod_ThatContextPathRejects()
        {
            // A custom static method in a cross-fact predicate.
            // Rule<T> (closure) accepts it; DependentRule<T> (context) rejects it.

            using var context = new RulesContext();
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<AgentMessage>(cfg => cfg.DependsOn<Agent>());
            });

            // --- Closure path: Rule<T> ---
            var agents = context.Facts<Agent>();
            context.GetRuleSet<AgentMessage>().Add(new Rule<AgentMessage>(
                "closure-custom", "Closure with custom method",
                m => m.Type == MessageType.Request
                     && agents.Any(a => IsHighValue(a)))  // custom method
                .Then(m => m.Flag("closure-matched")));

            using var session = context.CreateSession();
            session.Insert(new Agent { Id = "A1", ReputationScore = 9.0, Status = AgentStatus.Available });
            session.Insert(new AgentMessage { Id = "M1", Type = MessageType.Request });

            var result = session.Evaluate();

            // Closure path: custom method works fine in-memory, no validation error.
            Assert.False(result.HasErrors);
            var msg = session.Facts<AgentMessage>().First();
            Assert.True(msg.Flags.Contains("closure-matched"));
        }

        [Test]
        public void Issue2_ContextPath_RejectsUntranslatableMethod_ThatClosurePathAllows()
        {
            // Same predicate as above, but through DependentRule<T>.
            // The FactSetQueryProvider validates expression trees and rejects
            // the custom method as not translatable.

            using var context = new RulesContext();
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<AgentMessage>(cfg => cfg.DependsOn<Agent>());
            });

            // --- Context path: DependentRule<T> ---
            context.GetRuleSet<AgentMessage>().Add(new DependentRule<AgentMessage>(
                "context-custom", "Context with custom method",
                (m, ctx) => m.Type == MessageType.Request
                            && ctx.Facts<Agent>().Any(a => IsHighValue(a)))  // same custom method
                .DependsOn<Agent>());

            using var session = context.CreateSession();
            session.Insert(new Agent { Id = "A1", ReputationScore = 9.0 });
            session.Insert(new AgentMessage { Id = "M1", Type = MessageType.Request });

            var result = session.Evaluate();

            // Context path: custom method is rejected by the validator.
            // Error is captured in result.Errors, not thrown.
            Assert.True(result.HasErrors);
            Assert.True(result.Errors.Count > 0);
            var error = result.Errors[0];
            Assert.Contains("IsHighValue", error.Exception.Message);
            Assert.Contains("not translatable", error.Exception.Message);
        }

        [Test]
        public void Issue2_BothPaths_SameLogic_DifferentOutcome()
        {
            // Side-by-side: identical business logic, different results based
            // solely on which pattern the developer chose.

            using var context = new RulesContext();
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Agent>();
                schema.RegisterFactType<AgentMessage>(cfg => cfg.DependsOn<Agent>());
            });

            var agents = context.Facts<Agent>();

            // Closure: uses custom method
            context.GetRuleSet<AgentMessage>().Add(new Rule<AgentMessage>(
                "closure-version", "Closure version",
                m => agents.Any(a => IsHighValue(a)))
                .Then(m => m.Flag("closure")));

            // Context: uses SAME custom method
            context.GetRuleSet<AgentMessage>().Add(new DependentRule<AgentMessage>(
                "context-version", "Context version",
                (m, ctx) => ctx.Facts<Agent>().Any(a => IsHighValue(a)))
                .DependsOn<Agent>());

            using var session = context.CreateSession();
            session.Insert(new Agent { Id = "A1", ReputationScore = 9.0 });
            var msg = new AgentMessage { Id = "M1", Type = MessageType.Request };
            session.Insert(msg);

            var result = session.Evaluate();

            // Closure version matched (no validation)
            Assert.True(msg.Flags.Contains("closure"));

            // Context version errored (validator rejected IsHighValue)
            Assert.True(result.HasErrors);
            Assert.Equal("context-version", result.Errors[0].RuleId);
        }

        // =====================================================================
        // ISSUE 3: ClosureExtractor knows about non-serializable captures,
        //          but nobody asks it during rule creation or evaluation
        //
        // ClosureExtractor.ValidateClosures() can detect non-serializable
        // captured variables. ExpressionValidator checks method translatability.
        // Neither is called on the closure path during Rule<T> construction
        // or session evaluation.
        // =====================================================================

        [Test]
        public void Issue3_ClosureExtractor_DetectsNonSerializableCapture_ButIsNeverCalled()
        {
            // Developer captures a Dictionary in a closure.
            // ClosureExtractor WOULD flag it as non-serializable, but nobody calls it.
            var lookup = new Dictionary<string, double>
            {
                ["downtown"] = 1.5,
                ["docks"] = 1.0,
                ["suburb"] = 0.8
            };

            // Rule uses the dictionary in its condition
            var rule = new Rule<Territory>(
                "lookup-multiplier", "Apply territory multiplier from lookup",
                t => lookup.ContainsKey(t.Id) && t.Revenue * lookup[t.Id] > 10000)
                .Then(t => t.Status = "high-value");

            // The rule was accepted without complaint.
            Assert.NotNull(rule);
            Assert.NotNull(rule.Condition);

            // But ClosureExtractor knows this isn't serializable:
            var extractor = new ClosureExtractor();
            var validation = extractor.ValidateClosures(rule.Condition);
            Assert.False(validation.IsValid, "ClosureExtractor correctly detects non-serializable capture");
            Assert.True(validation.Errors.Count > 0);
            Assert.Contains("lookup", validation.Errors[0]);

            // ISSUE: The extractor has the knowledge, but Rule<T> never consults it.
            // The rule works in-memory, but will fail when serialization is attempted.
        }

        [Test]
        public void Issue3_ClosureExtractor_DoesNotFlag_SerializableCaptures()
        {
            // Primitive/string captures ARE serializable — ClosureExtractor approves.
            var threshold = 50;
            var prefix = "hot-";

            var rule = new Rule<Territory>(
                "simple-closure", "Simple closure with serializable captures",
                t => t.HeatLevel > threshold && t.Name.StartsWith(prefix));

            var extractor = new ClosureExtractor();
            var validation = extractor.ValidateClosures(rule.Condition);
            Assert.True(validation.IsValid, "Serializable captures pass validation");
        }

        [Test]
        public void Issue3_ClosureExtractor_FlagsObjectCapture_ButRuleWorksLocally()
        {
            // Capturing a domain object (Agent) in a closure for comparison.
            // Non-serializable, but works perfectly in-memory.
            var boss = new Agent { Id = "vito", Name = "Vito", Role = AgentRole.Godfather };

            var rule = new Rule<Agent>(
                "reports-to-boss", "Check if agent reports to boss",
                a => a.SuperiorId == boss.Id);

            // Works in local evaluation — no issues
            using var context = new RulesContext();
            context.GetRuleSet<Agent>().Add(rule);
            using var session = context.CreateSession();
            session.Insert(new Agent { Id = "sonny", SuperiorId = "vito" });
            var result = session.Evaluate();
            Assert.False(result.HasErrors);
            Assert.True(result.TotalMatches > 0);

            // But ClosureExtractor flags it as non-serializable
            var extractor = new ClosureExtractor();
            var validation = extractor.ValidateClosures(rule.Condition);
            Assert.False(validation.IsValid);
            Assert.Contains("boss", validation.Errors[0]);

            // ISSUE: Works locally, would fail during serialization for remote execution.
            // Nothing warns the developer until they try to serialize.
        }

        // =====================================================================
        // ISSUE 4: Nested cross-fact subqueries go unchecked
        //
        // A DependentRule can reference multiple fact types in a single
        // condition, including nesting them (subquery inside subquery).
        // The ExpressionValidator only checks subquery depth when
        // SupportsSubqueries is false (it defaults to true).
        // In-memory execution handles arbitrary nesting fine, but a remote
        // provider may not.
        // =====================================================================

        [Test]
        public void Issue4_ContextPath_NestedCrossFactQueries_FailDueToValidation()
        {
            // Developer writes a condition that queries Territory inside a query of Agent
            // — a subquery inside a subquery via DependentRule.
            //
            // This FAILS because when ctx.Facts<Agent>().Any(...) is evaluated at runtime,
            // the inner predicate (which calls ctx.Facts<Territory>()) goes through
            // FactSetQueryProvider validation. The validator sees the Territory subquery
            // as a nested queryable and rejects it.
            //
            // Meanwhile, the SAME logic via closure capture (Issue4_ClosurePath test below)
            // works perfectly — another asymmetry between the two paths.
            using var context = new RulesContext();
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Territory>();
                schema.RegisterFactType<Agent>(cfg => cfg.DependsOn<Territory>());
                schema.RegisterFactType<AgentMessage>(cfg =>
                {
                    cfg.DependsOn<Agent>();
                    cfg.DependsOn<Territory>();
                });
            });

            // Nested ctx.Facts<T>() calls — subquery inside subquery.
            context.GetRuleSet<AgentMessage>().Add(new DependentRule<AgentMessage>(
                "nested-subquery", "Route based on agent's territory value",
                (msg, ctx) => msg.Type == MessageType.Request
                              && ctx.Facts<Agent>().Any(a =>
                                    a.Id == msg.ToId
                                    && ctx.Facts<Territory>().Any(t =>
                                        t.Id == a.TerritoryId
                                        && t.Revenue > 10000)))
                .DependsOn<Agent>()
                .DependsOn<Territory>()
                .Then(msg => msg.Flag("high-value-territory")));

            using var session = context.CreateSession();
            session.Insert(new Territory { Id = "downtown", Revenue = 25000 });
            session.Insert(new Agent { Id = "paulie", TerritoryId = "downtown" });
            var msg = new AgentMessage { Id = "M1", Type = MessageType.Request, ToId = "paulie" };
            session.Insert(msg);

            var result = session.Evaluate();

            // ISSUE: Context path rejects nested subqueries that closure path allows.
            // The developer gets an error here but the same logic works via closures.
            Assert.True(result.HasErrors, "Nested context subqueries fail validation");
            Assert.True(result.Errors.Count > 0);
            Assert.False(msg.Flags.Contains("high-value-territory"),
                "Rule did not fire due to validation error");
        }

        [Test]
        public void Issue4_ClosurePath_NestedCrossFactQueries_AlsoWorkUnchecked()
        {
            // Same nesting pattern via closure capture — also no validation.
            using var context = new RulesContext();
            context.ConfigureSchema(schema =>
            {
                schema.RegisterFactType<Territory>();
                schema.RegisterFactType<Agent>(cfg => cfg.DependsOn<Territory>());
                schema.RegisterFactType<AgentMessage>(cfg =>
                {
                    cfg.DependsOn<Agent>();
                    cfg.DependsOn<Territory>();
                });
            });

            var agents = context.Facts<Agent>();
            var territories = context.Facts<Territory>();

            // Closure captures TWO FactQueryable<T> references and nests them
            context.GetRuleSet<AgentMessage>().Add(new Rule<AgentMessage>(
                "nested-closure", "Nested closure cross-fact",
                m => m.Type == MessageType.Request
                     && agents.Any(a =>
                        a.Id == m.ToId
                        && territories.Any(t =>
                            t.Id == a.TerritoryId
                            && t.Revenue > 10000)))
                .Then(m => m.Flag("nested-closure-matched")));

            using var session = context.CreateSession();
            session.Insert(new Territory { Id = "downtown", Revenue = 25000 });
            session.Insert(new Agent { Id = "paulie", TerritoryId = "downtown" });
            var msg = new AgentMessage { Id = "M1", Type = MessageType.Request, ToId = "paulie" };
            session.Insert(msg);

            var result = session.Evaluate();

            // Works in-memory, no validation, no depth check
            Assert.False(result.HasErrors);
            Assert.True(msg.Flags.Contains("nested-closure-matched"));
        }

        // =====================================================================
        // ISSUE 5: FactQueryRewriter.VisitMember silently falls through on
        //          evaluation errors, leaving FactQueryable in the compiled
        //          expression. The resulting error message points to the wrong
        //          problem.
        //
        // The rewriter evaluates closure fields via:
        //   Expression.Lambda(node).Compile().DynamicInvoke()
        // If this fails, the catch block at Provider.cs:617 swallows the
        // exception and falls through to base.VisitMember, leaving the
        // original FactQueryable<T> node in the tree. When that tree is
        // compiled and invoked, FactQueryable.GetEnumerator() throws
        // "cannot be enumerated directly" — a misleading error.
        // =====================================================================

        [Test]
        public void Issue5_RewriterSubstitution_WhenWorking_ProducesCorrectResults()
        {
            // Baseline: normal closure rewriting works correctly.
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            var rule = new Rule<AgentMessage>(
                "rewriter-works", "Normal rewriter path",
                m => agents.Any(a => a.Status == AgentStatus.Available))
                .Then(m => m.Flag("rewritten"));

            context.GetRuleSet<AgentMessage>().Add(rule);

            using var session = context.CreateSession();
            session.Insert(new Agent { Id = "A1", Status = AgentStatus.Available });
            session.Insert(new AgentMessage { Id = "M1", Type = MessageType.Request });

            var result = session.Evaluate();
            Assert.False(result.HasErrors);

            var msg = session.Facts<AgentMessage>().First();
            Assert.True(msg.Flags.Contains("rewritten"));
        }

        [Test]
        public void Issue5_FactQueryableDirectEnumeration_ErrorMessage_IsConfusing()
        {
            // If a FactQueryable somehow survives rewriting (e.g., rewriter
            // failed silently), the error message doesn't explain the real cause.
            using var context = new RulesContext();
            var agents = context.Facts<Agent>();

            // Directly trying to enumerate the FactQueryable gives this error:
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var a in agents) { }
            });

            // The error says "cannot be enumerated directly" — but doesn't explain
            // WHY it ended up here (e.g., "the FactQueryRewriter may have failed
            // to substitute this node — check that the closure field is accessible").
            Assert.Contains("cannot be enumerated directly", ex.Message);

            // ISSUE: If this error appears during session.Evaluate(), the developer
            // has no clue that the rewriter silently skipped their closure field.
            // The error points to FactQueryable, not to the rewriter failure.
        }

        [Test]
        public void Issue5_RewriterVisitMember_CompilesAndInvokesPerVisit()
        {
            // Demonstrate that the rewriter evaluates closure fields dynamically.
            // This is observable: the rewriter calls Expression.Lambda(node).Compile().DynamicInvoke()
            // for EVERY MemberExpression on a ConstantExpression, not just FactQueryable ones.
            //
            // We can observe this by checking that a rule with many closure captures
            // still works (it does), but each captured field is evaluated at rewrite time.

            using var context = new RulesContext();
            var agents = context.Facts<Agent>();
            var minReputation = 5.0;       // closure capture: double
            var requiredRole = AgentRole.Soldier;  // closure capture: enum
            var statusFilter = AgentStatus.Available;  // closure capture: enum

            // Rule with multiple closure captures + a FactQueryable
            var rule = new Rule<AgentMessage>(
                "multi-closure", "Multiple closures plus cross-fact",
                m => m.Type == MessageType.Request
                     && agents.Any(a => a.ReputationScore > minReputation
                                        && a.Role == requiredRole
                                        && a.Status == statusFilter))
                .Then(m => m.Flag("multi-closure-matched"));

            context.GetRuleSet<AgentMessage>().Add(rule);

            using var session = context.CreateSession();
            session.Insert(new Agent
            {
                Id = "rocco", Role = AgentRole.Soldier,
                Status = AgentStatus.Available, ReputationScore = 8.0
            });
            session.Insert(new AgentMessage { Id = "M1", Type = MessageType.Request });

            var result = session.Evaluate();

            // This works, but the rewriter had to DynamicInvoke for agents,
            // minReputation, requiredRole, and statusFilter closure fields.
            // Only the agents field is a FactQueryable that needed substitution.
            // The rest were evaluated and discarded — wasted work.
            Assert.False(result.HasErrors);
            var msg = session.Facts<AgentMessage>().First();
            Assert.True(msg.Flags.Contains("multi-closure-matched"));
        }

        // =====================================================================
        // Helper methods
        // =====================================================================

        /// <summary>
        /// Custom predicate — not in the ExpressionValidator's translatable whitelist.
        /// Works in LINQ-to-Objects but would fail in a remote provider.
        /// </summary>
        private static bool IsHighValue(Agent agent) => agent.ReputationScore > 7.0;

        // =====================================================================
        // FIXES: Silent catches must surface errors
        //
        // Three locations silently swallow exceptions:
        //   1. ClosureExtractor.ExtractClosureValue catch (ClosureExtractor.cs)
        //   2. FactQueryDetector.VisitMember catch (Provider.cs)
        //   3. FactQueryRewriter.VisitMember catch (Provider.cs)
        //
        // These tests use a property that throws on access to trigger each catch.
        // =====================================================================

        /// <summary>
        /// A type whose property throws — used to trigger silent catches.
        /// Simulates a closure field that can't be evaluated.
        /// </summary>
        public class ThrowingCapture
        {
            public IQueryable<Agent> Agents
            {
                get => throw new InvalidOperationException("Property access failed: connection lost");
            }
        }

        [Test]
        public void Fix5a_ClosureExtractor_SurfacesExtractionError_WhenPropertyThrows()
        {
            // ClosureExtractor.ExtractClosureValue currently swallows exceptions.
            // It should surface the error so ValidateClosures can report it.
            var capture = new ThrowingCapture();

            // Build expression: a => a.CurrentTaskCount > capture.Agents.Count()
            // The closure captures 'capture'. When the extractor tries to evaluate
            // capture.Agents, the property getter throws.
            // We use a simpler form: build expression that references capture.Agents.
            var param = Expression.Parameter(typeof(Agent), "a");
            var captureConst = Expression.Constant(capture);
            var agentsProp = Expression.Property(captureConst, nameof(ThrowingCapture.Agents));
            // a => capture.Agents != null (arbitrary condition that references the closure)
            var body = Expression.NotEqual(agentsProp, Expression.Constant(null, typeof(IQueryable<Agent>)));
            var lambda = Expression.Lambda<Func<Agent, bool>>(body, param);

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(lambda);

            // EXPECTED AFTER FIX: The extractor should report the closure with an error,
            // not silently omit it. ValidateClosures should flag it.
            Assert.True(closures.Count > 0, "Closure should be reported even when extraction fails");
            var errorClosure = closures.First(c => c.Name == "Agents");
            Assert.False(errorClosure.IsSerializable);
            Assert.NotNull(errorClosure.ExtractionError);
            Assert.Contains("Property access failed", errorClosure.ExtractionError!);
        }

        [Test]
        public void Fix5a_ClosureExtractor_ValidateClosures_IncludesExtractionErrors()
        {
            // ValidateClosures should report extraction errors as validation failures.
            var capture = new ThrowingCapture();

            var param = Expression.Parameter(typeof(Agent), "a");
            var captureConst = Expression.Constant(capture);
            var agentsProp = Expression.Property(captureConst, nameof(ThrowingCapture.Agents));
            var body = Expression.NotEqual(agentsProp, Expression.Constant(null, typeof(IQueryable<Agent>)));
            var lambda = Expression.Lambda<Func<Agent, bool>>(body, param);

            var extractor = new ClosureExtractor();
            var result = extractor.ValidateClosures(lambda);

            Assert.False(result.IsValid, "Extraction errors should cause validation failure");
            Assert.True(result.Errors.Count > 0);
            // Error message should mention the field and the cause
            Assert.Contains("Agents", result.Errors[0]);
        }

        [Test]
        public void Fix5b_FactQueryDetector_DetectsConservatively_WhenEvaluationFails()
        {
            // FactQueryDetector.VisitMember currently swallows exceptions.
            // When evaluation fails, it should conservatively assume the field
            // IS a FactQueryable (Found=true), since failing to detect means
            // the rule goes down the Standard path and fails with a confusing error.
            var capture = new ThrowingCapture();

            var param = Expression.Parameter(typeof(Agent), "a");
            var captureConst = Expression.Constant(capture);
            var agentsProp = Expression.Property(captureConst, nameof(ThrowingCapture.Agents));
            // Build: Enumerable.Any(capture.Agents, a => true)
            var anyMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Any" && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(Agent));
            var anyCall = Expression.Call(anyMethod, agentsProp);
            var lambda = Expression.Lambda<Func<Agent, bool>>(anyCall, param);

            // ContainsFactQuery should return true (conservative) rather than
            // silently returning false and sending the rule down the wrong path.
            bool containsFactQuery = FactQueryExpression.ContainsFactQuery(lambda);
            Assert.True(containsFactQuery,
                "Detector should conservatively report true when closure field evaluation fails");
        }

        [Test]
        public void Fix5c_FactQueryRewriter_ThrowsClearly_WhenClosureEvaluationFails()
        {
            // FactQueryRewriter.VisitMember currently swallows exceptions.
            // When it can't evaluate a closure field, it should throw with
            // a clear message explaining the failure, not silently leave the
            // FactQueryable in the tree (which later produces "cannot be enumerated").
            var capture = new ThrowingCapture();

            Func<Type, IQueryable> resolver = type => Array.Empty<Agent>().AsQueryable();
            var rewriter = new FactQueryRewriter(resolver);

            var param = Expression.Parameter(typeof(Agent), "a");
            var captureConst = Expression.Constant(capture);
            var agentsProp = Expression.Property(captureConst, nameof(ThrowingCapture.Agents));
            // Build: capture.Agents != null (references the throwing property)
            var body = Expression.NotEqual(agentsProp, Expression.Constant(null, typeof(IQueryable<Agent>)));
            var lambda = Expression.Lambda<Func<Agent, bool>>(body, param);

            // EXPECTED AFTER FIX: The rewriter should throw with context,
            // not silently fall through and leave the FactQueryable in the tree.
            var ex = Assert.Throws<InvalidOperationException>(() => rewriter.Rewrite(lambda));
            Assert.Contains("Agents", ex.Message);
            Assert.Contains("ThrowingCapture", ex.Message);
        }
    }
}
