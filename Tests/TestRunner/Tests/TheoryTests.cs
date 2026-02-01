using TestRunner.Framework;

namespace TestRunner.Tests;

/// <summary>
/// Tests to verify [Theory], [InlineData], and [MemberData] support.
/// </summary>
public class TheoryTests
{
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 1, 0)]
    [InlineData(100, 200, 300)]
    public void Addition_ReturnsCorrectSum(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("hello", 5)]
    [InlineData("", 0)]
    [InlineData("test", 4)]
    public void StringLength_ReturnsCorrectValue(string input, int expectedLength)
    {
        Assert.Equal(expectedLength, input.Length);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(5, false)]
    [InlineData(15, true)]
    [InlineData(0, false)]
    public void IsGreaterThanSeven_ReturnsExpected(int value, bool expected)
    {
        var result = value > 7;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(2.5, 3.5, 6.0)]
    [InlineData(0.1, 0.2, 0.3)]
    public void DoubleAddition_ReturnsCorrectSum(double a, double b, double expected)
    {
        var result = a + b;
        Assert.True(Math.Abs(result - expected) < 0.0001, $"Expected {expected} but got {result}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not null")]
    public void NullableString_HandlesCorrectly(string? value)
    {
        if (value == null)
        {
            Assert.Null(value);
        }
        else
        {
            Assert.NotNull(value);
        }
    }

    // MemberData tests
    [Theory]
    [MemberData(nameof(MultiplicationData))]
    public void Multiplication_FromMemberData(int a, int b, int expected)
    {
        Assert.Equal(expected, a * b);
    }

    public static IEnumerable<object?[]> MultiplicationData()
    {
        yield return new object?[] { 2, 3, 6 };
        yield return new object?[] { 0, 100, 0 };
        yield return new object?[] { -1, 5, -5 };
        yield return new object?[] { 10, 10, 100 };
    }

    [Theory]
    [MemberData(nameof(DivisionTestCases))]
    public void Division_FromStaticProperty(int dividend, int divisor, int expected)
    {
        Assert.Equal(expected, dividend / divisor);
    }

    public static IEnumerable<object?[]> DivisionTestCases => new List<object?[]>
    {
        new object?[] { 10, 2, 5 },
        new object?[] { 100, 10, 10 },
        new object?[] { 9, 3, 3 },
    };

    [Theory(Skip = "Demonstrating skip functionality")]
    [InlineData(1, 2)]
    public void SkippedTheory_ShouldNotRun(int a, int b)
    {
        Assert.True(false, "This should not run");
    }

    // ===== EDGE CASE TESTS =====

    // Async Theory support
    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public async Task AsyncTheory_CompletesSuccessfully(int delayMs)
    {
        await Task.Delay(1); // Minimal delay for test speed
        Assert.True(delayMs > 0);
    }

    // Empty InlineData (parameterless theory case)
    [Theory]
    [InlineData()]
    public void EmptyInlineData_RunsOnce()
    {
        Assert.True(true);
    }

    // Multiple null arguments
    [Theory]
    [InlineData(null, null)]
    [InlineData("a", null)]
    [InlineData(null, "b")]
    public void MultipleNulls_HandledCorrectly(string? a, string? b)
    {
        // Just verify we can receive multiple nulls
        var result = (a ?? "") + (b ?? "");
        Assert.NotNull(result);
    }

    // Type conversions: int literals to long parameters
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(int.MaxValue, 1, (long)int.MaxValue + 1)]
    public void IntToLong_ConversionWorks(long a, long b, long expected)
    {
        Assert.Equal(expected, a + b);
    }

    // Type conversions: int literals to double parameters
    [Theory]
    [InlineData(1, 2, 3.0)]
    [InlineData(5, 3, 8.0)]
    public void IntToDouble_ConversionWorks(double a, double b, double expected)
    {
        Assert.Equal(expected, a + b);
    }

    // Combined InlineData and MemberData on same method
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 4)]
    [MemberData(nameof(SquareData))]
    public void CombinedDataSources_AllExecute(int input, int expectedSquare)
    {
        Assert.Equal(expectedSquare, input * input);
    }

    public static IEnumerable<object?[]> SquareData()
    {
        yield return new object?[] { 3, 9 };
        yield return new object?[] { 4, 16 };
    }

    // MemberData from static field
    [Theory]
    [MemberData(nameof(StaticFieldData))]
    public void MemberData_FromStaticField_Works(string input, int length)
    {
        Assert.Equal(length, input.Length);
    }

    public static readonly List<object?[]> StaticFieldData = new()
    {
        new object?[] { "abc", 3 },
        new object?[] { "hello", 5 },
    };

    // MemberData from different class
    [Theory]
    [MemberData(nameof(ExternalTestData.Numbers), MemberType = typeof(ExternalTestData))]
    public void MemberData_FromDifferentClass_Works(int a, int b, int sum)
    {
        Assert.Equal(sum, a + b);
    }

    // Char parameter
    [Theory]
    [InlineData('a', 'b', false)]
    [InlineData('x', 'x', true)]
    public void CharParameters_Work(char a, char b, bool shouldBeEqual)
    {
        Assert.Equal(shouldBeEqual, a == b);
    }

    // Enum parameter
    [Theory]
    [InlineData(DayOfWeek.Monday, false)]
    [InlineData(DayOfWeek.Saturday, true)]
    [InlineData(DayOfWeek.Sunday, true)]
    public void EnumParameters_Work(DayOfWeek day, bool isWeekend)
    {
        var actual = day == DayOfWeek.Saturday || day == DayOfWeek.Sunday;
        Assert.Equal(isWeekend, actual);
    }

    // Large number of test cases (performance check)
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void ManyInlineData_AllExecute(int value)
    {
        Assert.True(value >= 0 && value < 10);
    }

    // Nullable value types
    [Theory]
    [InlineData(null, 0)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    public void NullableInt_HandledCorrectly(int? value, int expected)
    {
        Assert.Equal(expected, value ?? 0);
    }

    // Long string display (verify it doesn't crash)
    [Theory]
    [InlineData("This is a very long string that should still display correctly in the test output without crashing or causing issues")]
    public void LongString_DisplaysCorrectly(string value)
    {
        Assert.True(value.Length > 50);
    }

    // Floating point edge cases
    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(double.Epsilon)]
    [InlineData(0.0)]
    public void DoubleEdgeCases_Work(double value)
    {
        Assert.True(double.IsFinite(value));
    }

    // Negative numbers display correctly
    [Theory]
    [InlineData(-1, -2, -3)]
    [InlineData(-100, 50, -50)]
    public void NegativeNumbers_DisplayCorrectly(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }
}

/// <summary>
/// External class for testing MemberData with MemberType property.
/// </summary>
public static class ExternalTestData
{
    public static IEnumerable<object?[]> Numbers()
    {
        yield return new object?[] { 10, 20, 30 };
        yield return new object?[] { 5, 5, 10 };
    }
}
