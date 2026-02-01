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
}
