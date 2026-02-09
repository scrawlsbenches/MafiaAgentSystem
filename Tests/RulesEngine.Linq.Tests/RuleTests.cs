namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Linq;
    using TestRunner.Framework;
    using RulesEngine.Linq;

    public class RuleTests
    {
        private class Order
        {
            public string Id { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public bool IsActive { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        #region Rule Creation

        [Test]
        public void Rule_CanBeCreated()
        {
            var rule = new Rule<Order>("R1", "Test Rule", o => o.Total > 100);

            Assert.Equal("R1", rule.Id);
            Assert.Equal("Test Rule", rule.Name);
            Assert.NotNull(rule.Condition);
        }

        [Test]
        public void Rule_NullId_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new Rule<Order>(null!, "Test", o => o.Total > 100));
        }

        [Test]
        public void Rule_EmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new Rule<Order>("", "Test", o => o.Total > 100));
        }

        [Test]
        public void Rule_NullName_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new Rule<Order>("R1", null!, o => o.Total > 100));
        }

        [Test]
        public void Rule_NullCondition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new Rule<Order>("R1", "Test", null!));
        }

        #endregion

        #region Rule Evaluation

        [Test]
        public void Rule_Evaluate_ReturnsTrueWhenMatches()
        {
            var rule = new Rule<Order>("R1", "High Value", o => o.Total > 100);
            var order = new Order { Total = 150 };

            Assert.True(rule.Evaluate(order));
        }

        [Test]
        public void Rule_Evaluate_ReturnsFalseWhenNotMatches()
        {
            var rule = new Rule<Order>("R1", "High Value", o => o.Total > 100);
            var order = new Order { Total = 50 };

            Assert.False(rule.Evaluate(order));
        }

        [Test]
        public void Rule_Evaluate_ReturnsFalseForNullFact()
        {
            var rule = new Rule<Order>("R1", "High Value", o => o.Total > 100);

            Assert.False(rule.Evaluate(null!));
        }

        [Test]
        public void Rule_Evaluate_PropagatesExceptions()
        {
            // Exceptions from condition evaluation now propagate to the caller
            // so the session error handler can capture them in IEvaluationResult.Errors.
            var rule = new Rule<Order>("R1", "Check Status", o => o.Status.Length > 5);
            var order = new Order { Status = null! };

            Assert.Throws<NullReferenceException>(() => rule.Evaluate(order));
        }

        #endregion

        #region Rule Execution

        [Test]
        public void Rule_Execute_ReturnsSuccessWhenMatches()
        {
            var rule = new Rule<Order>("R1", "High Value", o => o.Total > 100);
            var order = new Order { Total = 150 };

            var result = rule.Execute(order);

            Assert.True(result.Matched);
            Assert.True(result.ActionExecuted);
            Assert.Null(result.ErrorMessage);
        }

        [Test]
        public void Rule_Execute_ReturnsNoMatchWhenNotMatches()
        {
            var rule = new Rule<Order>("R1", "High Value", o => o.Total > 100);
            var order = new Order { Total = 50 };

            var result = rule.Execute(order);

            Assert.False(result.Matched);
            Assert.False(result.ActionExecuted);
        }

        [Test]
        public void Rule_Execute_RunsActions()
        {
            var rule = new Rule<Order>("R1", "High Value", o => o.Total > 100)
                .WithAction(o => o.Status = "Processed");

            var order = new Order { Total = 150, Status = "New" };
            rule.Execute(order);

            Assert.Equal("Processed", order.Status);
        }

        [Test]
        public void Rule_Execute_RunsMultipleActions()
        {
            var rule = new Rule<Order>("R1", "High Value", o => o.Total > 100)
                .WithAction(o => o.Status = "Processed")
                .WithAction(o => o.IsActive = true);

            var order = new Order { Total = 150, Status = "New", IsActive = false };
            rule.Execute(order);

            Assert.Equal("Processed", order.Status);
            Assert.True(order.IsActive);
        }

        [Test]
        public void Rule_Execute_CapturesActionError()
        {
            var rule = new Rule<Order>("R1", "High Value", o => o.Total > 100)
                .WithAction(o => throw new InvalidOperationException("Action failed"));

            var order = new Order { Total = 150 };
            var result = rule.Execute(order);

            Assert.True(result.Matched);
            Assert.False(result.ActionExecuted);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("Action failed", result.ErrorMessage);
        }

        #endregion

        #region RuleBuilder

        [Test]
        public void RuleBuilder_CreatesRule()
        {
            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test Rule")
                .When(o => o.Total > 100)
                .Build();

            Assert.Equal("R1", rule.Id);
            Assert.Equal("Test Rule", rule.Name);
        }

        [Test]
        public void RuleBuilder_GeneratesIdIfNotSet()
        {
            var rule = new RuleBuilder<Order>()
                .WithName("Test Rule")
                .When(o => o.Total > 100)
                .Build();

            Assert.False(string.IsNullOrEmpty(rule.Id));
        }

        [Test]
        public void RuleBuilder_UsesIdAsNameIfNotSet()
        {
            var rule = new RuleBuilder<Order>()
                .WithId("MY_RULE")
                .When(o => o.Total > 100)
                .Build();

            Assert.Equal("MY_RULE", rule.Name);
        }

        [Test]
        public void RuleBuilder_WithoutCondition_Throws()
        {
            var builder = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test");

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Test]
        public void RuleBuilder_And_CombinesConditions()
        {
            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test")
                .When(o => o.Total > 100)
                .And(o => o.IsActive)
                .Build();

            Assert.True(rule.Evaluate(new Order { Total = 150, IsActive = true }));
            Assert.False(rule.Evaluate(new Order { Total = 150, IsActive = false }));
            Assert.False(rule.Evaluate(new Order { Total = 50, IsActive = true }));
        }

        [Test]
        public void RuleBuilder_Or_CombinesConditions()
        {
            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test")
                .When(o => o.Total > 1000)
                .Or(o => o.IsActive)
                .Build();

            Assert.True(rule.Evaluate(new Order { Total = 1500, IsActive = false }));
            Assert.True(rule.Evaluate(new Order { Total = 50, IsActive = true }));
            Assert.False(rule.Evaluate(new Order { Total = 50, IsActive = false }));
        }

        [Test]
        public void RuleBuilder_Not_NegatesCondition()
        {
            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test")
                .When(o => o.Total > 100)
                .Not()
                .Build();

            Assert.True(rule.Evaluate(new Order { Total = 50 }));
            Assert.False(rule.Evaluate(new Order { Total = 150 }));
        }

        [Test]
        public void RuleBuilder_And_WithoutWhen_Throws()
        {
            var builder = new RuleBuilder<Order>()
                .WithId("R1");

            Assert.Throws<InvalidOperationException>(() =>
                builder.And(o => o.IsActive));
        }

        [Test]
        public void RuleBuilder_Or_WithoutWhen_Throws()
        {
            var builder = new RuleBuilder<Order>()
                .WithId("R1");

            Assert.Throws<InvalidOperationException>(() =>
                builder.Or(o => o.IsActive));
        }

        [Test]
        public void RuleBuilder_Not_WithoutWhen_Throws()
        {
            var builder = new RuleBuilder<Order>()
                .WithId("R1");

            Assert.Throws<InvalidOperationException>(() => builder.Not());
        }

        [Test]
        public void RuleBuilder_WithClosure_Works()
        {
            decimal threshold = 100m;

            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test")
                .When(o => o.Total > threshold)
                .Build();

            Assert.True(rule.Evaluate(new Order { Total = 150 }));
            Assert.False(rule.Evaluate(new Order { Total = 50 }));

            // Closure captures variable - changes affect evaluation
            threshold = 200m;
            Assert.False(rule.Evaluate(new Order { Total = 150 }));
        }

        [Test]
        public void RuleBuilder_ComplexCondition()
        {
            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Complex Rule")
                .When(o => o.Total > 100)
                .And(o => o.Total < 1000)
                .And(o => o.IsActive)
                .Or(o => o.Status == "VIP")
                .Build();

            Assert.True(rule.Evaluate(new Order { Total = 500, IsActive = true }));
            Assert.True(rule.Evaluate(new Order { Total = 50, IsActive = false, Status = "VIP" }));
            Assert.False(rule.Evaluate(new Order { Total = 50, IsActive = false, Status = "Normal" }));
        }

        [Test]
        public void RuleBuilder_WithPriority()
        {
            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test")
                .WithPriority(100)
                .When(o => o.Total > 100)
                .Build();

            Assert.Equal(100, rule.Priority);
        }

        [Test]
        public void RuleBuilder_Then_AddsAction()
        {
            string captured = "";

            var rule = new RuleBuilder<Order>()
                .WithId("R1")
                .WithName("Test")
                .When(o => o.Total > 100)
                .Then(o => captured = o.Id)
                .Build();

            rule.Execute(new Order { Id = "ORDER1", Total = 150 });

            Assert.Equal("ORDER1", captured);
        }

        #endregion
    }
}
