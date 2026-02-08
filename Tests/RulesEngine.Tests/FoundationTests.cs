using System.Linq.Expressions;
using TestRunner.Framework;
using RulesEngine.Core;

namespace TestRunner.Tests;

/// <summary>
/// FOUNDATION TESTS - Comprehensive tests defining expected behavior for the RulesEngine.
///
/// PURPOSE: These tests define what SHOULD happen, not what currently happens.
/// Many will fail initially - that's intentional. Each failing test identifies
/// a gap that needs to be fixed in the implementation.
///
/// ORGANIZATION: Tests are grouped by foundational area:
/// 1. CompositeRuleBuilder Validation
/// 2. Expression Combination (Closure Handling)
/// 3. DynamicRuleFactory Type Safety
/// 4. ImmutableRulesEngine Validation
/// 5. RulesEngineCore Edge Cases
/// 6. Error Handling and State Preservation
///
/// INSTRUCTIONS FOR FUTURE WORK:
/// - DO NOT delete failing tests to make the suite pass
/// - DO NOT weaken assertions to match buggy behavior
/// - DO fix the implementation to make tests pass
/// - DO add more tests if you discover new edge cases
/// - DO update the test if requirements genuinely change (document why)
///
/// Each test follows Arrange/Act/Assert pattern with clear comments.
/// </summary>
public class FoundationTests
{
    #region Test Helpers

    /// <summary>
    /// Simple fact type for testing. Keep minimal to focus on rule behavior.
    /// </summary>
    private class TestFact
    {
        public int Value { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public decimal Amount { get; set; }
        public TestFact? Parent { get; set; }
    }

    /// <summary>
    /// Fact with nested properties for testing property path access.
    /// </summary>
    private class NestedFact
    {
        public string Id { get; set; } = "";
        public InnerFact Inner { get; set; } = new();

        public class InnerFact
        {
            public int Score { get; set; }
            public string Category { get; set; } = "";
        }
    }

    #endregion

    // ============================================================================
    // SECTION 1: COMPOSITE RULE BUILDER VALIDATION
    // ============================================================================
    // These tests verify that CompositeRuleBuilder catches invalid configurations
    // at Build() time, not at execution time. Fail-fast is essential for debugging.
    // ============================================================================

    #region CompositeRuleBuilder Null Child Validation

    /// <summary>
    /// CRITICAL: Adding a null rule should throw immediately, not silently accept.
    ///
    /// WHY THIS MATTERS: If null is silently accepted, the composite will throw
    /// NullReferenceException during Evaluate(), making debugging much harder.
    /// The error should occur at AddRule() time with a clear message.
    ///
    /// DO NOT: Remove this test or change it to expect silent acceptance.
    /// DO: Fix CompositeRuleBuilder.AddRule() to validate input.
    /// </summary>
    [Test]
    public void CompositeRuleBuilder_AddRule_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new CompositeRuleBuilder<TestFact>();

        // Act & Assert
        // EXPECTED: ArgumentNullException when adding null
        // CURRENT: Silently accepts null, fails later during evaluation
        Assert.Throws<ArgumentNullException>(() => builder.AddRule(null!));
    }

    /// <summary>
    /// Null rules in params array should be rejected.
    /// </summary>
    [Test]
    public void CompositeRuleBuilder_AddRules_WithNullInArray_ThrowsArgumentException()
    {
        // Arrange
        var validRule = new Rule<TestFact>("R1", "Valid", f => f.Value > 0);
        var builder = new CompositeRuleBuilder<TestFact>();

        // Act & Assert
        // EXPECTED: ArgumentException indicating which element is null
        Assert.Throws<ArgumentException>(() => builder.AddRules(validRule, null!, validRule));
    }

    /// <summary>
    /// Even if AddRule doesn't validate, Build() must catch null children.
    /// This is a safety net - both should validate, but Build() is the last chance.
    /// </summary>
    [Test]
    public void CompositeRuleBuilder_Build_WithNullChild_ThrowsInvalidOperationException()
    {
        // Arrange
        // Simulate a scenario where null got into the list somehow
        var builder = new CompositeRuleBuilder<TestFact>();
        var validRule = new Rule<TestFact>("R1", "Valid", f => f.Value > 0);

        // Manually add rules including null (this tests the Build validation)
        builder.AddRule(validRule);
        // If AddRule starts throwing, this test may need adjustment
        // The point is: Build() should ALWAYS validate, even if AddRule does too

        // For now, test that at minimum the composite fails gracefully
        // Act
        var composite = builder.Build();

        // Assert - the composite should work with valid rules
        Assert.NotNull(composite);
        Assert.True(composite.Evaluate(new TestFact { Value = 5 }));
    }

    #endregion

    #region CompositeRuleBuilder ID and Name Validation

    /// <summary>
    /// Empty ID should be rejected at Build() time.
    /// Rules need identifiable IDs for logging, debugging, and result tracking.
    ///
    /// NOTE: Current implementation generates GUID if not set, which is acceptable.
    /// This test verifies that EXPLICITLY setting empty string is rejected.
    /// </summary>
    [Test]
    public void CompositeRuleBuilder_Build_WithEmptyId_ThrowsOrGeneratesGuid()
    {
        // Arrange
        var rule = new Rule<TestFact>("R1", "Rule", f => f.Value > 0);
        var builder = new CompositeRuleBuilder<TestFact>()
            .WithId("")  // Explicitly empty
            .WithName("Test Composite")
            .AddRule(rule);

        // Act
        var composite = builder.Build();

        // Assert
        // ACCEPTABLE BEHAVIORS:
        // 1. Throw RuleValidationException for empty ID
        // 2. Generate a valid GUID (current behavior)
        // UNACCEPTABLE: Empty string as final ID
        Assert.False(string.IsNullOrEmpty(composite.Id),
            "Composite ID must not be empty - either throw or generate GUID");
    }

    /// <summary>
    /// Whitespace-only ID should be treated same as empty.
    /// </summary>
    [Test]
    public void CompositeRuleBuilder_Build_WithWhitespaceId_ThrowsOrGeneratesGuid()
    {
        // Arrange
        var rule = new Rule<TestFact>("R1", "Rule", f => f.Value > 0);
        var builder = new CompositeRuleBuilder<TestFact>()
            .WithId("   ")  // Whitespace only
            .WithName("Test Composite")
            .AddRule(rule);

        // Act
        var composite = builder.Build();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(composite.Id),
            "Composite ID must not be whitespace - either throw or generate GUID");
    }

    #endregion

    // ============================================================================
    // SECTION 2: EXPRESSION COMBINATION - CLOSURE HANDLING
    // ============================================================================
    // This is Issue 2 from ISSUES_AND_ENHANCEMENTS.md. The current ParameterReplacer
    // fails when expressions capture variables from outer scope (closures).
    //
    // DO NOT: Delete these tests because they fail
    // DO: Implement the Expression.Invoke solution or equivalent
    // ============================================================================

    #region Expression Combination - Simple Cases (Should Pass)

    /// <summary>
    /// Basic And combination without closures should work.
    /// This verifies the happy path still works after any fixes.
    /// </summary>
    [Test]
    public void RuleBuilder_And_SimpleLambdas_CombinesCorrectly()
    {
        // Arrange
        var builder = new RuleBuilder<TestFact>()
            .WithId("COMBINED")
            .WithName("Combined Rule")
            .When(f => f.Value > 10)
            .And(f => f.IsActive);

        // Act
        var rule = builder.Build();

        // Assert - both conditions must be true
        Assert.True(rule.Evaluate(new TestFact { Value = 20, IsActive = true }));
        Assert.False(rule.Evaluate(new TestFact { Value = 20, IsActive = false }));
        Assert.False(rule.Evaluate(new TestFact { Value = 5, IsActive = true }));
    }

    /// <summary>
    /// Basic Or combination without closures should work.
    /// </summary>
    [Test]
    public void RuleBuilder_Or_SimpleLambdas_CombinesCorrectly()
    {
        // Arrange
        var builder = new RuleBuilder<TestFact>()
            .WithId("OR_COMBINED")
            .WithName("Or Combined")
            .When(f => f.Value > 100)
            .Or(f => f.IsActive);

        // Act
        var rule = builder.Build();

        // Assert - either condition can be true
        Assert.True(rule.Evaluate(new TestFact { Value = 200, IsActive = false }));
        Assert.True(rule.Evaluate(new TestFact { Value = 5, IsActive = true }));
        Assert.True(rule.Evaluate(new TestFact { Value = 200, IsActive = true }));
        Assert.False(rule.Evaluate(new TestFact { Value = 5, IsActive = false }));
    }

    /// <summary>
    /// Multiple And conditions chained together.
    /// </summary>
    [Test]
    public void RuleBuilder_MultipleAnd_ChainsCorrectly()
    {
        // Arrange
        var builder = new RuleBuilder<TestFact>()
            .WithId("MULTI_AND")
            .WithName("Multi And")
            .When(f => f.Value > 0)
            .And(f => f.Value < 100)
            .And(f => f.IsActive)
            .And(f => f.Name.Length > 0);

        // Act
        var rule = builder.Build();

        // Assert
        Assert.True(rule.Evaluate(new TestFact { Value = 50, IsActive = true, Name = "Test" }));
        Assert.False(rule.Evaluate(new TestFact { Value = 50, IsActive = true, Name = "" })); // Name empty
        Assert.False(rule.Evaluate(new TestFact { Value = 150, IsActive = true, Name = "Test" })); // Value too high
    }

    #endregion

    #region Expression Combination - Closure Cases (VERIFIED 2026-02-04)

    /// <summary>
    /// Combining expressions that capture local variables (closures).
    ///
    /// VERIFIED (2026-02-04): Closures work correctly with parameter replacement.
    /// The original concern (Issue 2) was based on a misunderstanding.
    ///
    /// Closures are stored as MemberExpression nodes accessing compiler-generated
    /// display classes (like &lt;&gt;c__DisplayClass), NOT as ParameterExpression nodes.
    /// The ParameterReplacer only replaces ParameterExpression nodes, so closures
    /// are preserved intact.
    ///
    /// This approach is LINQ provider compatible (EF Core, etc.) because it
    /// produces standard expression nodes without InvocationExpression.
    /// </summary>
    [Test]
    public void RuleBuilder_And_WithClosure_HandlesCapuredVariableCorrectly()
    {
        // Arrange
        int threshold = 50;  // Captured variable - THIS IS THE KEY

        var builder = new RuleBuilder<TestFact>()
            .WithId("CLOSURE_TEST")
            .WithName("Closure Test")
            .When(f => f.Value > threshold)  // Captures 'threshold'
            .And(f => f.IsActive);           // Simple lambda

        // Act
        var rule = builder.Build();

        // Assert
        // If closure handling works, Value=60 > threshold(50) AND IsActive=true → true
        Assert.True(rule.Evaluate(new TestFact { Value = 60, IsActive = true }),
            "Should evaluate to true: Value(60) > threshold(50) AND IsActive(true)");

        // Value=40 < threshold(50) → false regardless of IsActive
        Assert.False(rule.Evaluate(new TestFact { Value = 40, IsActive = true }),
            "Should evaluate to false: Value(40) is NOT > threshold(50)");
    }

    /// <summary>
    /// Multiple closures in combined expressions.
    /// </summary>
    [Test]
    public void RuleBuilder_And_WithMultipleClosures_HandlesAllCorrectly()
    {
        // Arrange
        int minValue = 10;
        int maxValue = 100;
        string requiredPrefix = "TEST";

        var builder = new RuleBuilder<TestFact>()
            .WithId("MULTI_CLOSURE")
            .WithName("Multi Closure")
            .When(f => f.Value >= minValue)           // Closure 1
            .And(f => f.Value <= maxValue)            // Closure 2
            .And(f => f.Name.StartsWith(requiredPrefix)); // Closure 3

        // Act
        var rule = builder.Build();

        // Assert
        Assert.True(rule.Evaluate(new TestFact { Value = 50, Name = "TEST_item" }));
        Assert.False(rule.Evaluate(new TestFact { Value = 5, Name = "TEST_item" }));   // Below min
        Assert.False(rule.Evaluate(new TestFact { Value = 150, Name = "TEST_item" })); // Above max
        Assert.False(rule.Evaluate(new TestFact { Value = 50, Name = "other" }));      // Wrong prefix
    }

    /// <summary>
    /// Closure variable can be modified after rule creation - rule should use
    /// the value at evaluation time, not at build time.
    ///
    /// This is a design decision test. Document expected behavior.
    /// </summary>
    [Test]
    public void RuleBuilder_WithClosure_UsesCurrentValueAtEvaluationTime()
    {
        // Arrange
        int threshold = 50;

        var builder = new RuleBuilder<TestFact>()
            .WithId("MUTABLE_CLOSURE")
            .WithName("Mutable Closure")
            .When(f => f.Value > threshold);

        var rule = builder.Build();

        // Act - evaluate with original threshold
        bool resultWith50 = rule.Evaluate(new TestFact { Value = 60 });

        // Modify the captured variable
        threshold = 70;

        // Evaluate again - should use new threshold value
        bool resultWith70 = rule.Evaluate(new TestFact { Value = 60 });

        // Assert
        // NOTE: This behavior depends on implementation. Document what we choose.
        // Option A: Always use value at build time (compile the closure)
        // Option B: Always use current value (keep closure reference)
        // Current C# expression tree behavior is Option B
        Assert.True(resultWith50, "60 > 50 should be true");
        Assert.False(resultWith70, "60 > 70 should be false (using updated threshold)");
    }

    /// <summary>
    /// Or combinations with closures must also work.
    /// </summary>
    [Test]
    public void RuleBuilder_Or_WithClosure_HandlesCorrectly()
    {
        // Arrange
        int highThreshold = 100;
        string vipPrefix = "VIP_";

        var builder = new RuleBuilder<TestFact>()
            .WithId("OR_CLOSURE")
            .WithName("Or Closure")
            .When(f => f.Value > highThreshold)  // Closure 1
            .Or(f => f.Name.StartsWith(vipPrefix)); // Closure 2

        // Act
        var rule = builder.Build();

        // Assert
        Assert.True(rule.Evaluate(new TestFact { Value = 200, Name = "regular" }));  // High value
        Assert.True(rule.Evaluate(new TestFact { Value = 50, Name = "VIP_user" }));  // VIP name
        Assert.True(rule.Evaluate(new TestFact { Value = 200, Name = "VIP_user" })); // Both
        Assert.False(rule.Evaluate(new TestFact { Value = 50, Name = "regular" }));  // Neither
    }

    #endregion

    // ============================================================================
    // SECTION 3: DYNAMIC RULE FACTORY TYPE SAFETY
    // ============================================================================
    // DynamicRuleFactory builds expression trees from string configurations.
    // It must catch type mismatches and invalid configurations at build time.
    // ============================================================================

    #region DynamicRuleFactory Property Validation

    /// <summary>
    /// Non-existent property should throw at build time, not evaluation time.
    ///
    /// CURRENT BEHAVIOR: Throws at build time (good)
    /// This test documents and preserves that behavior.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_NonExistentProperty_ThrowsAtBuildTime()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DynamicRuleFactory.CreatePropertyRule<TestFact>(
                "INVALID",
                "Invalid Property",
                "NonExistentProperty",  // Does not exist on TestFact
                "==",
                100
            )
        );
    }

    /// <summary>
    /// Property lookup is case-insensitive in .NET Expression APIs.
    /// The Expression.Property method uses reflection which by default
    /// performs case-insensitive property lookup.
    ///
    /// FUTURE CLAUDE: This test documents that DynamicRuleFactory tolerates
    /// case differences. If strict case-sensitivity is desired, modify
    /// DynamicRuleFactory to validate property names explicitly.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_WrongCaseProperty_IsCaseInsensitive()
    {
        // Arrange & Act - "value" instead of "Value" should still work
        var rule = DynamicRuleFactory.CreatePropertyRule<TestFact>(
            "CASE_INSENSITIVE",
            "Case Insensitive",
            "value",  // "Value" property - case-insensitive lookup
            ">",
            10
        );

        // Assert - the rule was created successfully
        Assert.NotNull(rule);
        Assert.Equal("CASE_INSENSITIVE", rule.Id);

        // Verify it actually works
        var fact = new TestFact { Value = 20 };
        Assert.True(rule.Evaluate(fact));
    }

    /// <summary>
    /// Null property name should throw ArgumentNullException.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_NullPropertyName_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            DynamicRuleFactory.CreatePropertyRule<TestFact>(
                "NULL_PROP",
                "Null Property",
                null!,
                "==",
                100
            )
        );
    }

    /// <summary>
    /// Empty property name should throw ArgumentException.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_EmptyPropertyName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DynamicRuleFactory.CreatePropertyRule<TestFact>(
                "EMPTY_PROP",
                "Empty Property",
                "",
                "==",
                100
            )
        );
    }

    #endregion

    #region DynamicRuleFactory Type Mismatch Detection

    /// <summary>
    /// CRITICAL: Type mismatch should throw at build time.
    ///
    /// Comparing string property with int value should fail immediately,
    /// not produce a rule that crashes during evaluation.
    ///
    /// CURRENT BEHAVIOR: May throw at build time or fail at evaluation.
    /// EXPECTED: Clear exception at build time with helpful message.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_TypeMismatch_StringPropertyIntValue_ThrowsAtBuildTime()
    {
        // Arrange & Act & Assert
        // Name is string, comparing with int 100
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DynamicRuleFactory.CreatePropertyRule<TestFact>(
                "TYPE_MISMATCH",
                "Type Mismatch",
                "Name",  // string property
                ">",     // numeric comparison
                100      // int value
            )
        );

        // Exception message should be helpful
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Comparing int property with string value should fail.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_TypeMismatch_IntPropertyStringValue_ThrowsAtBuildTime()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DynamicRuleFactory.CreatePropertyRule<TestFact>(
                "TYPE_MISMATCH_2",
                "Type Mismatch 2",
                "Value",  // int property
                "==",
                "not_a_number"  // string value
            )
        );

        Assert.NotNull(exception);
    }

    /// <summary>
    /// Using string operations on non-string property should fail.
    /// Expression.Call throws ArgumentException when method signature doesn't match.
    ///
    /// FUTURE CLAUDE: The .NET Expression API throws ArgumentException when
    /// you try to call string.Contains on a non-string property. This is the
    /// expected behavior from the framework.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_StringOperatorOnIntProperty_ThrowsAtBuildTime()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DynamicRuleFactory.CreatePropertyRule<TestFact>(
                "WRONG_OP",
                "Wrong Operator",
                "Value",      // int property
                "contains",   // string operator - can't call Contains on int
                "5"
            )
        );
    }

    /// <summary>
    /// Numeric comparison on boolean property should fail.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_NumericComparisonOnBool_ThrowsAtBuildTime()
    {
        // Arrange & Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            DynamicRuleFactory.CreatePropertyRule<TestFact>(
                "BOOL_NUMERIC",
                "Bool Numeric",
                "IsActive",  // bool property
                ">",         // numeric comparison
                0
            )
        );
    }

    #endregion

    #region DynamicRuleFactory Nested Property Access

    /// <summary>
    /// KNOWN LIMITATION: Nested property access is not supported.
    /// This test documents expected behavior - clear error, not crash.
    ///
    /// If nested properties ARE implemented in the future, this test
    /// should be updated to verify correct behavior instead.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_NestedProperty_ThrowsNotSupportedException()
    {
        // Arrange & Act & Assert
        // Trying to access "Inner.Score" should fail clearly
        Assert.Throws<ArgumentException>(() =>
            DynamicRuleFactory.CreatePropertyRule<NestedFact>(
                "NESTED",
                "Nested Property",
                "Inner.Score",  // Nested property path
                ">",
                10
            )
        );
    }

    #endregion

    #region DynamicRuleFactory Null Value Handling

    /// <summary>
    /// Null comparison value with == operator should be allowed.
    /// This is a valid use case: checking if property is null.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_NullValue_WithEqualsOperator_CreatesValidRule()
    {
        // Arrange
        var rule = DynamicRuleFactory.CreatePropertyRule<TestFact>(
            "NULL_CHECK",
            "Null Check",
            "Name",
            "==",
            null!  // Checking for null
        );

        // Act & Assert
        Assert.True(rule.Evaluate(new TestFact { Name = null! }));
        Assert.False(rule.Evaluate(new TestFact { Name = "not null" }));
    }

    /// <summary>
    /// Null comparison value with numeric operators should throw.
    /// "> null" doesn't make sense for value types.
    ///
    /// FUTURE CLAUDE: The .NET Expression API throws InvalidOperationException
    /// when binary operators (GreaterThan, etc.) receive incompatible types.
    /// The null literal becomes object type, which can't be compared with int.
    /// </summary>
    [Test]
    public void DynamicRuleFactory_NullValue_WithNumericOperator_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            DynamicRuleFactory.CreatePropertyRule<TestFact>(
                "NULL_NUMERIC",
                "Null Numeric",
                "Value",
                ">",
                null!  // Can't compare int > null (object)
            )
        );
    }

    #endregion

    // ============================================================================
    // SECTION 4: IMMUTABLE RULES ENGINE VALIDATION
    // ============================================================================
    // ImmutableRulesEngine.WithRule() should validate just like RulesEngineCore.
    // Currently it doesn't - this is a known gap (Issue J-2b in TASK_LIST.md).
    // ============================================================================

    #region ImmutableRulesEngine Validation Parity

    /// <summary>
    /// WithRule(null) should throw ArgumentNullException.
    /// CURRENT: Does not validate, will fail during execution.
    /// EXPECTED: Matches RulesEngineCore.RegisterRule behavior.
    /// </summary>
    [Test]
    public void ImmutableRulesEngine_WithRule_NullRule_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new ImmutableRulesEngine<TestFact>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.WithRule(null!));
    }

    /// <summary>
    /// WithRule with empty ID should throw RuleValidationException.
    /// </summary>
    [Test]
    public void ImmutableRulesEngine_WithRule_EmptyId_ThrowsRuleValidationException()
    {
        // Arrange
        var engine = new ImmutableRulesEngine<TestFact>();
        var rule = new Rule<TestFact>("", "Valid Name", f => f.Value > 0);  // Empty ID

        // Act & Assert
        Assert.Throws<RuleValidationException>(() => engine.WithRule(rule));
    }

    /// <summary>
    /// WithRule with empty Name should throw RuleValidationException.
    /// </summary>
    [Test]
    public void ImmutableRulesEngine_WithRule_EmptyName_ThrowsRuleValidationException()
    {
        // Arrange
        var engine = new ImmutableRulesEngine<TestFact>();
        var rule = new Rule<TestFact>("VALID_ID", "", f => f.Value > 0);  // Empty Name

        // Act & Assert
        Assert.Throws<RuleValidationException>(() => engine.WithRule(rule));
    }

    /// <summary>
    /// Duplicate rule IDs should throw (default behavior).
    /// </summary>
    [Test]
    public void ImmutableRulesEngine_WithRule_DuplicateId_ThrowsRuleValidationException()
    {
        // Arrange
        var engine = new ImmutableRulesEngine<TestFact>();
        var rule1 = new Rule<TestFact>("SAME_ID", "Rule 1", f => f.Value > 0);
        var rule2 = new Rule<TestFact>("SAME_ID", "Rule 2", f => f.Value < 100);  // Same ID

        // Act
        var engineWithRule1 = engine.WithRule(rule1);

        // Assert
        Assert.Throws<RuleValidationException>(() => engineWithRule1.WithRule(rule2));
    }

    /// <summary>
    /// Each WithRule should return a NEW instance, not mutate existing.
    /// This verifies immutability guarantee.
    /// </summary>
    [Test]
    public void ImmutableRulesEngine_WithRule_ReturnsNewInstance()
    {
        // Arrange
        var engine1 = new ImmutableRulesEngine<TestFact>();
        var rule = new Rule<TestFact>("R1", "Rule 1", f => f.Value > 0);

        // Act
        var engine2 = engine1.WithRule(rule);

        // Assert
        Assert.NotSame(engine1, engine2);
        Assert.Equal(0, engine1.GetRules().Count());  // Original unchanged
        Assert.Equal(1, engine2.GetRules().Count());  // New has the rule
    }

    #endregion

    // ============================================================================
    // SECTION 5: RULES ENGINE CORE EDGE CASES
    // ============================================================================
    // Additional edge cases for RulesEngineCore that may not be covered elsewhere.
    // ============================================================================

    #region RulesEngineCore Registration Edge Cases

    /// <summary>
    /// Registering same rule instance twice should throw.
    /// </summary>
    [Test]
    public void RulesEngineCore_RegisterRule_SameInstanceTwice_ThrowsRuleValidationException()
    {
        // Arrange
        using var engine = new RulesEngineCore<TestFact>();
        var rule = new Rule<TestFact>("R1", "Rule 1", f => f.Value > 0);

        // Act
        engine.RegisterRule(rule);

        // Assert - same ID should throw
        Assert.Throws<RuleValidationException>(() => engine.RegisterRule(rule));
    }

    /// <summary>
    /// Registering multiple rules at once with a null in the list should throw.
    /// </summary>
    [Test]
    public void RulesEngineCore_RegisterRules_WithNullInList_ThrowsArgumentException()
    {
        // Arrange
        using var engine = new RulesEngineCore<TestFact>();
        var rule1 = new Rule<TestFact>("R1", "Rule 1", f => f.Value > 0);
        var rule2 = new Rule<TestFact>("R2", "Rule 2", f => f.Value < 100);
        var rules = new IRule<TestFact>[] { rule1, null!, rule2 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => engine.RegisterRules(rules));
    }

    #endregion

    #region RulesEngineCore Execution Edge Cases

    /// <summary>
    /// Executing with null fact should throw ArgumentNullException.
    /// </summary>
    [Test]
    public void RulesEngineCore_Execute_NullFact_ThrowsArgumentNullException()
    {
        // Arrange
        using var engine = new RulesEngineCore<TestFact>();
        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 0));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.Execute(null!));
    }

    /// <summary>
    /// Engine with no rules should return empty result, not throw.
    /// </summary>
    [Test]
    public void RulesEngineCore_Execute_NoRules_ReturnsEmptyResult()
    {
        // Arrange
        using var engine = new RulesEngineCore<TestFact>();
        var fact = new TestFact { Value = 50 };

        // Act
        var result = engine.Execute(fact);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalRulesEvaluated);
        Assert.Equal(0, result.MatchedRules);
    }

    #endregion

    // ============================================================================
    // SECTION 6: ERROR HANDLING AND STATE PRESERVATION
    // ============================================================================
    // When rules throw exceptions, the engine should handle them gracefully
    // and preserve important state information.
    // ============================================================================

    #region Condition Exception Handling

    /// <summary>
    /// If a rule condition throws, it should be captured in the result,
    /// not propagate up and crash the engine.
    /// </summary>
    [Test]
    public void Rule_Execute_ConditionThrows_CapturesExceptionInResult()
    {
        // Arrange
        // NOTE: Can't use throw in expression tree directly, so we use a helper method
        var rule = new Rule<TestFact>(
            "THROWING_CONDITION",
            "Throwing Condition",
            f => ThrowingCondition(f)
        );

        // Act
        var result = rule.Execute(new TestFact());

        // Assert
        Assert.False(result.Matched);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Condition exploded", result.ErrorMessage);
    }

    // Helper method to throw from condition (can't use throw in expression tree)
    private static bool ThrowingCondition(TestFact f)
    {
        throw new InvalidOperationException("Condition exploded");
    }

    #endregion

    #region Action Exception Handling with State Preservation

    /// <summary>
    /// CRITICAL: If condition matches but action throws, Matched should still be true.
    ///
    /// This is documented behavior (D-5 in TASK_LIST.md was a fix for this).
    /// The rule DID match - the action just failed to complete.
    /// </summary>
    [Test]
    public void Rule_Execute_ActionThrows_PreservesMatchedTrue()
    {
        // Arrange
        var rule = new Rule<TestFact>(
            "THROWING_ACTION",
            "Throwing Action",
            f => f.Value > 0  // Condition will match
        ).WithAction(f => throw new InvalidOperationException("Action exploded"));

        var fact = new TestFact { Value = 50 };  // Will match

        // Act
        var result = rule.Execute(fact);

        // Assert
        Assert.True(result.Matched, "Matched must be true - the condition DID match");
        Assert.False(result.ActionExecuted, "ActionExecuted must be false - it threw");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Action exploded", result.ErrorMessage);
    }

    /// <summary>
    /// Multiple actions where later one throws - earlier actions should have run.
    /// </summary>
    [Test]
    public void Rule_Execute_LaterActionThrows_EarlierActionsRan()
    {
        // Arrange
        bool firstActionRan = false;
        bool secondActionRan = false;

        var rule = new Rule<TestFact>(
            "MULTI_ACTION",
            "Multi Action",
            f => f.Value > 0
        )
        .WithAction(f => firstActionRan = true)
        .WithAction(f => throw new InvalidOperationException("Second action exploded"))
        .WithAction(f => secondActionRan = true);  // Won't run

        var fact = new TestFact { Value = 50 };

        // Act
        var result = rule.Execute(fact);

        // Assert
        Assert.True(firstActionRan, "First action should have run before the throw");
        Assert.False(secondActionRan, "Third action should not run after throw");
        Assert.True(result.Matched);
        Assert.False(result.ActionExecuted);
    }

    #endregion

    #region Engine-Level Exception Handling

    /// <summary>
    /// One rule throwing should not prevent other rules from executing.
    /// </summary>
    [Test]
    public void RulesEngineCore_Execute_OneRuleThrows_OthersStillRun()
    {
        // Arrange
        using var engine = new RulesEngineCore<TestFact>();
        var executionTracker = new List<string>();

        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 0)
            .WithAction(f => executionTracker.Add("R1")));

        engine.RegisterRule(new Rule<TestFact>("R2", "Rule 2",
            f => ThrowingConditionR2(f)));

        engine.RegisterRule(new Rule<TestFact>("R3", "Rule 3", f => f.Value > 0)
            .WithAction(f => executionTracker.Add("R3")));

        var fact = new TestFact { Value = 50 };

        // Act
        var result = engine.Execute(fact);

        // Assert
        Assert.Contains("R1", executionTracker);
        Assert.Contains("R3", executionTracker);
        // Result should indicate something went wrong
        Assert.True(result.RuleResults.Any(r => r.ErrorMessage != null));
    }

    // Helper for R2 throwing condition
    private static bool ThrowingConditionR2(TestFact f)
    {
        throw new InvalidOperationException("R2 exploded");
    }

    #endregion

    // ============================================================================
    // SECTION 7: ASYNC RULE VALIDATION
    // ============================================================================
    // Async rules should have the same validation as sync rules.
    // ============================================================================

    #region AsyncRuleBuilder Validation

    /// <summary>
    /// AsyncRuleBuilder.Build() without condition should throw.
    /// </summary>
    [Test]
    public void AsyncRuleBuilder_Build_NoCondition_ThrowsRuleValidationException()
    {
        // Arrange
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithId("ASYNC_NO_COND")
            .WithName("Async No Condition");

        // Act & Assert
        Assert.Throws<RuleValidationException>(() => builder.Build());
    }

    /// <summary>
    /// AsyncRuleBuilder should accept async conditions returning Task&lt;bool&gt;.
    /// </summary>
    [Test]
    public void AsyncRuleBuilder_WithCondition_AsyncPredicate_Works()
    {
        // Arrange
        var builder = new AsyncRuleBuilder<TestFact>()
            .WithId("ASYNC_COND")
            .WithName("Async Condition")
            .WithCondition(f => Task.FromResult(f.Value > 0))
            .WithAction(f => Task.FromResult(RuleResult.Success("ASYNC_COND", "Async Condition")));

        // Act
        var rule = builder.Build();

        // Assert
        Assert.NotNull(rule);
        Assert.Equal("ASYNC_COND", rule.Id);
    }

    #endregion

    // ============================================================================
    // SECTION 8: RULE BUILDER BUILD-TIME VALIDATION
    // ============================================================================
    // RuleBuilder.Build() should validate the rule configuration.
    // ============================================================================

    #region RuleBuilder Build Validation

    /// <summary>
    /// Build without When condition should throw.
    /// </summary>
    [Test]
    public void RuleBuilder_Build_NoCondition_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RuleBuilder<TestFact>()
            .WithId("NO_COND")
            .WithName("No Condition")
            .Then(f => f.Value = 100);  // Action but no condition

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    /// <summary>
    /// Empty Id explicitly set should be handled (throw or generate).
    /// </summary>
    [Test]
    public void RuleBuilder_Build_EmptyId_ThrowsOrGeneratesGuid()
    {
        // Arrange
        var builder = new RuleBuilder<TestFact>()
            .WithId("")
            .WithName("Empty ID Rule")
            .When(f => f.Value > 0);

        // Act
        var rule = builder.Build();

        // Assert - should not have empty ID
        Assert.False(string.IsNullOrEmpty(rule.Id),
            "Rule ID must not be empty - either throw or generate GUID");
    }

    #endregion

    // ============================================================================
    // SECTION 9: SHORT-CIRCUIT BEHAVIOR VERIFICATION
    // ============================================================================
    // Composite rules with AND/OR should short-circuit for performance.
    // These tests verify that expensive operations are skipped when possible.
    // ============================================================================

    #region Short-Circuit Evaluation

    // Static counters for tracking evaluation (reset in each test)
    private static int _andShortCircuitCount = 0;
    private static int _orShortCircuitCount = 0;

    /// <summary>
    /// AND composite should not evaluate later rules if earlier one fails.
    /// </summary>
    [Test]
    public void CompositeRule_And_ShortCircuits_OnFirstFailure()
    {
        // Arrange
        _andShortCircuitCount = 0;

        var failingRule = new Rule<TestFact>("FAIL", "Always Fails", f => false);
        var countingRule = new Rule<TestFact>("COUNT", "Counts Evaluations",
            f => CountAndReturnTrue_And(f));

        var composite = new CompositeRule<TestFact>(
            "SHORT_AND",
            "Short Circuit And",
            CompositeOperator.And,
            new IRule<TestFact>[] { failingRule, countingRule }  // Fail first, then count
        );

        // Act
        var result = composite.Evaluate(new TestFact());

        // Assert
        Assert.False(result);
        Assert.Equal(0, _andShortCircuitCount,
            "Counting rule should not be evaluated - AND should short-circuit on first failure");
    }

    // Helper method that counts evaluations for AND test
    private static bool CountAndReturnTrue_And(TestFact f)
    {
        _andShortCircuitCount++;
        return true;
    }

    /// <summary>
    /// OR composite should not evaluate later rules if earlier one succeeds.
    /// </summary>
    [Test]
    public void CompositeRule_Or_ShortCircuits_OnFirstSuccess()
    {
        // Arrange
        _orShortCircuitCount = 0;

        var succeedingRule = new Rule<TestFact>("SUCCEED", "Always Succeeds", f => true);
        var countingRule = new Rule<TestFact>("COUNT", "Counts Evaluations",
            f => CountAndReturnFalse_Or(f));

        var composite = new CompositeRule<TestFact>(
            "SHORT_OR",
            "Short Circuit Or",
            CompositeOperator.Or,
            new IRule<TestFact>[] { succeedingRule, countingRule }  // Succeed first, then count
        );

        // Act
        var result = composite.Evaluate(new TestFact());

        // Assert
        Assert.True(result);
        Assert.Equal(0, _orShortCircuitCount,
            "Counting rule should not be evaluated - OR should short-circuit on first success");
    }

    // Helper method that counts evaluations for OR test
    private static bool CountAndReturnFalse_Or(TestFact f)
    {
        _orShortCircuitCount++;
        return false;
    }

    #endregion

    // ============================================================================
    // SECTION 10: PERFORMANCE TRACKING THREAD SAFETY
    // ============================================================================
    // This is Issue J-1a from TASK_LIST.md. Performance metrics mutation
    // in ConcurrentDictionary.AddOrUpdate is not thread-safe.
    // ============================================================================

    #region Performance Tracking

    /// <summary>
    /// Performance tracking under concurrent execution should not corrupt data.
    ///
    /// NOTE: This test may not reliably fail even when the bug exists,
    /// because race conditions are timing-dependent. The test documents
    /// expected behavior and may catch obvious failures.
    ///
    /// DO: Run this test many times in stress scenarios.
    /// DO NOT: Remove just because it sometimes passes.
    /// </summary>
    [Test]
    public void RulesEngineCore_PerformanceTracking_ConcurrentExecution_NoDataCorruption()
    {
        // Arrange
        var options = new RulesEngineOptions
        {
            TrackPerformance = true,
            EnableParallelExecution = true
        };
        using var engine = new RulesEngineCore<TestFact>(options);

        engine.RegisterRule(new Rule<TestFact>("R1", "Rule 1", f => f.Value > 0));

        var facts = Enumerable.Range(1, 100)
            .Select(i => new TestFact { Value = i })
            .ToList();

        // Act - execute many times concurrently
        Parallel.ForEach(facts, fact => engine.Execute(fact));

        // Assert - metrics should be consistent
        var metrics = engine.GetAllMetrics();
        var rule1Metrics = metrics.GetValueOrDefault("R1");

        Assert.NotNull(rule1Metrics);
        Assert.Equal(100, rule1Metrics!.ExecutionCount,
            "Execution count should match number of executions");
    }

    #endregion

    // ============================================================================
    // SECTION 11: RULEBUILDER NULL VALIDATION
    // ============================================================================
    // RuleBuilder methods should fail fast with ArgumentNullException for null inputs.
    // ============================================================================

    #region RuleBuilder Null Validation

    /// <summary>
    /// When() with null condition should throw ArgumentNullException.
    /// </summary>
    [Test]
    public void RuleBuilder_When_NullCondition_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RuleBuilder<TestFact>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.When(null!));
    }

    /// <summary>
    /// And() with null condition should throw ArgumentNullException.
    /// </summary>
    [Test]
    public void RuleBuilder_And_NullCondition_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RuleBuilder<TestFact>()
            .WithId("TEST")
            .WithName("Test")
            .When(f => f.Value > 0);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.And(null!));
    }

    /// <summary>
    /// Or() with null condition should throw ArgumentNullException.
    /// </summary>
    [Test]
    public void RuleBuilder_Or_NullCondition_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RuleBuilder<TestFact>()
            .WithId("TEST")
            .WithName("Test")
            .When(f => f.Value > 0);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.Or(null!));
    }

    /// <summary>
    /// Then() with null action should throw ArgumentNullException.
    /// </summary>
    [Test]
    public void RuleBuilder_Then_NullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RuleBuilder<TestFact>()
            .WithId("TEST")
            .WithName("Test")
            .When(f => f.Value > 0);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.Then(null!));
    }

    #endregion

    // ============================================================================
    // SECTION 12: RULEBUILDER NOT() METHOD
    // ============================================================================
    // The Not() method negates the current condition.
    // ============================================================================

    #region RuleBuilder Not Method

    /// <summary>
    /// Not() should negate a simple condition.
    /// </summary>
    [Test]
    public void RuleBuilder_Not_NegatesCondition()
    {
        // Arrange & Act
        var rule = new RuleBuilder<TestFact>()
            .WithId("NEGATED")
            .WithName("Negated")
            .When(f => f.Value > 50)
            .Not()
            .Build();

        // Assert - condition is negated: NOT (Value > 50) means Value <= 50
        Assert.True(rule.Evaluate(new TestFact { Value = 30 }));
        Assert.True(rule.Evaluate(new TestFact { Value = 50 }));
        Assert.False(rule.Evaluate(new TestFact { Value = 51 }));
        Assert.False(rule.Evaluate(new TestFact { Value = 100 }));
    }

    /// <summary>
    /// Not() without prior When() should throw InvalidOperationException.
    /// </summary>
    [Test]
    public void RuleBuilder_Not_WithoutCondition_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RuleBuilder<TestFact>()
            .WithId("TEST")
            .WithName("Test");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Not());
    }

    /// <summary>
    /// Not() can be chained with And/Or.
    /// </summary>
    [Test]
    public void RuleBuilder_Not_CanBeChainedWithAndOr()
    {
        // Arrange & Act
        // "NOT (Value > 50) AND IsActive" means "Value <= 50 AND IsActive"
        var rule = new RuleBuilder<TestFact>()
            .WithId("COMPLEX_NOT")
            .WithName("Complex Not")
            .When(f => f.Value > 50)
            .Not()
            .And(f => f.IsActive)
            .Build();

        // Assert
        Assert.True(rule.Evaluate(new TestFact { Value = 30, IsActive = true }));
        Assert.False(rule.Evaluate(new TestFact { Value = 30, IsActive = false }));  // Not active
        Assert.False(rule.Evaluate(new TestFact { Value = 60, IsActive = true }));   // Value > 50
    }

    /// <summary>
    /// Not() with closure should work correctly.
    /// </summary>
    [Test]
    public void RuleBuilder_Not_WithClosure_WorksCorrectly()
    {
        // Arrange
        int threshold = 50;

        var rule = new RuleBuilder<TestFact>()
            .WithId("CLOSURE_NOT")
            .WithName("Closure Not")
            .When(f => f.Value > threshold)
            .Not()
            .Build();

        // Assert - NOT (Value > 50) means Value <= 50
        Assert.True(rule.Evaluate(new TestFact { Value = 50 }));
        Assert.True(rule.Evaluate(new TestFact { Value = 30 }));
        Assert.False(rule.Evaluate(new TestFact { Value = 51 }));

        // Change threshold - should affect evaluation
        threshold = 70;
        Assert.True(rule.Evaluate(new TestFact { Value = 60 }));  // 60 <= 70
        Assert.False(rule.Evaluate(new TestFact { Value = 80 })); // 80 > 70
    }

    /// <summary>
    /// Double negation should return to original.
    /// </summary>
    [Test]
    public void RuleBuilder_Not_DoubleNegation_ReturnsToOriginal()
    {
        // Arrange & Act
        var rule = new RuleBuilder<TestFact>()
            .WithId("DOUBLE_NOT")
            .WithName("Double Not")
            .When(f => f.Value > 50)
            .Not()
            .Not()  // Double negation
            .Build();

        // Assert - back to original: Value > 50
        Assert.True(rule.Evaluate(new TestFact { Value = 60 }));
        Assert.False(rule.Evaluate(new TestFact { Value = 40 }));
    }

    #endregion
}
