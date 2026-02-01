using TestRunner.Framework;
using RulesEngine.Core;

namespace TestRunner.Tests;

public class RuleTests
{
    private class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string City { get; set; } = "";
        public decimal Salary { get; set; }
    }

    [Test]
    public void Rule_SimpleCondition_EvaluatesCorrectly()
    {
        var rule = new Rule<Person>(
            "AGE_CHECK",
            "Age Verification",
            person => person.Age >= 18
        );

        var adult = new Person { Age = 25 };
        var minor = new Person { Age = 16 };

        Assert.True(rule.Evaluate(adult));
        Assert.False(rule.Evaluate(minor));
    }

    [Test]
    public void Rule_WithAction_ExecutesWhenMatched()
    {
        var actionExecuted = false;
        var rule = new Rule<Person>(
            "TEST_RULE",
            "Test Rule",
            person => person.Age > 18
        ).WithAction(person => actionExecuted = true);

        var person = new Person { Age = 25 };

        var result = rule.Execute(person);

        Assert.True(result.Matched);
        Assert.True(result.ActionExecuted);
        Assert.True(actionExecuted);
    }

    [Test]
    public void Rule_NotMatched_DoesNotExecuteAction()
    {
        var actionExecuted = false;
        var rule = new Rule<Person>(
            "TEST_RULE",
            "Test Rule",
            person => person.Age > 18
        ).WithAction(person => actionExecuted = true);

        var person = new Person { Age = 16 };

        var result = rule.Execute(person);

        Assert.False(result.Matched);
        Assert.False(result.ActionExecuted);
        Assert.False(actionExecuted);
    }

    [Test]
    public void Rule_MultipleActions_AllExecute()
    {
        var counter = 0;
        var rule = new Rule<Person>(
            "MULTI_ACTION",
            "Multi Action Rule",
            person => person.Age > 18
        )
        .WithAction(person => counter++)
        .WithAction(person => counter++)
        .WithAction(person => counter++);

        var person = new Person { Age = 25 };

        rule.Execute(person);

        Assert.Equal(3, counter);
    }

    [Test]
    public void Rule_ComplexCondition_EvaluatesCorrectly()
    {
        var rule = new Rule<Person>(
            "ELIGIBILITY",
            "Eligibility Check",
            person => person.Age >= 18 && person.Salary > 30000 && person.City == "Boston"
        );

        var eligible = new Person { Age = 25, Salary = 50000, City = "Boston" };
        var tooYoung = new Person { Age = 17, Salary = 50000, City = "Boston" };
        var lowSalary = new Person { Age = 25, Salary = 20000, City = "Boston" };
        var wrongCity = new Person { Age = 25, Salary = 50000, City = "New York" };

        Assert.True(rule.Evaluate(eligible));
        Assert.False(rule.Evaluate(tooYoung));
        Assert.False(rule.Evaluate(lowSalary));
        Assert.False(rule.Evaluate(wrongCity));
    }

    [Test]
    public void Rule_ToString_ReturnsReadableFormat()
    {
        var rule = new Rule<Person>(
            "TEST",
            "Test Rule",
            person => person.Age > 18
        );

        var description = rule.ToString();

        Assert.Contains("Test Rule", description);
        Assert.Contains("Age", description);
    }
}

public class RuleBuilderTests
{
    private class TestFact
    {
        public int Value { get; set; }
        public string Status { get; set; } = "";
        public bool Flag { get; set; }
    }

    [Test]
    public void RuleBuilder_SimpleRule_Builds()
    {
        var rule = new RuleBuilder<TestFact>()
            .WithId("TEST_1")
            .WithName("Test Rule")
            .WithDescription("A test rule")
            .WithPriority(10)
            .When(fact => fact.Value > 5)
            .Build();

        Assert.Equal("TEST_1", rule.Id);
        Assert.Equal("Test Rule", rule.Name);
        Assert.Equal("A test rule", rule.Description);
        Assert.Equal(10, rule.Priority);
    }

    [Test]
    public void RuleBuilder_AndConditions_CombinesWithAnd()
    {
        var rule = new RuleBuilder<TestFact>()
            .WithName("Combined Rule")
            .When(fact => fact.Value > 5)
            .And(fact => fact.Value < 15)
            .Build();

        var matching = new TestFact { Value = 10 };
        var tooLow = new TestFact { Value = 3 };
        var tooHigh = new TestFact { Value = 20 };

        Assert.True(rule.Evaluate(matching));
        Assert.False(rule.Evaluate(tooLow));
        Assert.False(rule.Evaluate(tooHigh));
    }

    [Test]
    public void RuleBuilder_OrConditions_CombinesWithOr()
    {
        var rule = new RuleBuilder<TestFact>()
            .WithName("Or Rule")
            .When(fact => fact.Value < 5)
            .Or(fact => fact.Value > 15)
            .Build();

        var lowValue = new TestFact { Value = 3 };
        var midValue = new TestFact { Value = 10 };
        var highValue = new TestFact { Value = 20 };

        Assert.True(rule.Evaluate(lowValue));
        Assert.False(rule.Evaluate(midValue));
        Assert.True(rule.Evaluate(highValue));
    }

    [Test]
    public void RuleBuilder_WithAction_AddsAction()
    {
        var executed = false;

        var rule = new RuleBuilder<TestFact>()
            .WithName("Action Rule")
            .When(fact => fact.Value > 5)
            .Then(fact => executed = true)
            .Build();

        rule.Execute(new TestFact { Value = 10 });

        Assert.True(executed);
    }

    [Test]
    public void RuleBuilder_MultipleActions_AllAdded()
    {
        var counter = 0;

        var rule = new RuleBuilder<TestFact>()
            .WithName("Multi Action")
            .When(fact => fact.Value > 5)
            .Then(fact => counter++)
            .Then(fact => counter++)
            .Then(fact => counter++)
            .Build();

        rule.Execute(new TestFact { Value = 10 });

        Assert.Equal(3, counter);
    }

    [Test]
    public void RuleBuilder_NoCondition_ThrowsException()
    {
        var builder = new RuleBuilder<TestFact>()
            .WithName("Invalid Rule");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void RuleBuilder_ComplexConditions_WorksCorrectly()
    {
        var rule = new RuleBuilder<TestFact>()
            .WithName("Complex Rule")
            .When(fact => fact.Value > 10)
            .And(fact => fact.Status == "Active")
            .Or(fact => fact.Flag == true)
            .Build();

        var matching1 = new TestFact { Value = 15, Status = "Active", Flag = false };
        var matching2 = new TestFact { Value = 5, Status = "Inactive", Flag = true };
        var notMatching = new TestFact { Value = 5, Status = "Active", Flag = false };

        Assert.True(rule.Evaluate(matching1));
        Assert.True(rule.Evaluate(matching2));
        Assert.False(rule.Evaluate(notMatching));
    }
}

public class CompositeRuleTests
{
    private class TestData
    {
        public int Value { get; set; }
        public string Category { get; set; } = "";
    }

    [Test]
    public void CompositeRule_AndOperator_RequiresAllRules()
    {
        var rule1 = new Rule<TestData>("R1", "Rule 1", data => data.Value > 5);
        var rule2 = new Rule<TestData>("R2", "Rule 2", data => data.Value < 15);

        var composite = new CompositeRule<TestData>(
            "COMP",
            "Composite",
            CompositeOperator.And,
            new[] { rule1, rule2 }
        );

        Assert.True(composite.Evaluate(new TestData { Value = 10 }));
        Assert.False(composite.Evaluate(new TestData { Value = 3 }));
        Assert.False(composite.Evaluate(new TestData { Value = 20 }));
    }

    [Test]
    public void CompositeRule_OrOperator_RequiresAnyRule()
    {
        var rule1 = new Rule<TestData>("R1", "Rule 1", data => data.Value < 5);
        var rule2 = new Rule<TestData>("R2", "Rule 2", data => data.Value > 15);

        var composite = new CompositeRule<TestData>(
            "COMP",
            "Composite",
            CompositeOperator.Or,
            new[] { rule1, rule2 }
        );

        Assert.True(composite.Evaluate(new TestData { Value = 3 }));
        Assert.False(composite.Evaluate(new TestData { Value = 10 }));
        Assert.True(composite.Evaluate(new TestData { Value = 20 }));
    }

    [Test]
    public void CompositeRule_NotOperator_InvertsRule()
    {
        var rule = new Rule<TestData>("R1", "Rule 1", data => data.Value > 10);

        var composite = new CompositeRule<TestData>(
            "COMP",
            "Not Composite",
            CompositeOperator.Not,
            new[] { rule }
        );

        Assert.False(composite.Evaluate(new TestData { Value = 15 }));
        Assert.True(composite.Evaluate(new TestData { Value = 5 }));
    }

    [Test]
    public void CompositeRuleBuilder_BuildsCorrectly()
    {
        var rule1 = new Rule<TestData>("R1", "Rule 1", data => data.Value > 5);
        var rule2 = new Rule<TestData>("R2", "Rule 2", data => data.Category == "A");

        var composite = new CompositeRuleBuilder<TestData>()
            .WithId("COMP_1")
            .WithName("Test Composite")
            .WithOperator(CompositeOperator.And)
            .AddRule(rule1)
            .AddRule(rule2)
            .Build();

        Assert.Equal("COMP_1", composite.Id);
        Assert.Equal("Test Composite", composite.Name);
        Assert.Equal(2, composite.Rules.Count);
    }
}
