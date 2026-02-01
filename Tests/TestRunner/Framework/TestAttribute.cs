namespace TestRunner.Framework;

/// <summary>
/// Marks a method as a test to be discovered and run by the test runner.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestAttribute : Attribute
{
    public string? Name { get; set; }
    public string? Skip { get; set; }
}

/// <summary>
/// Marks a class as containing tests.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class TestClassAttribute : Attribute
{
}

/// <summary>
/// Marks a method as a parameterized test (theory) that runs once per data set.
/// Use with [InlineData] or [MemberData] to provide test data.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TheoryAttribute : Attribute
{
    public string? Name { get; set; }
    public string? Skip { get; set; }
}

/// <summary>
/// Provides inline data for a [Theory] test method.
/// Can be applied multiple times to provide multiple test cases.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class InlineDataAttribute : Attribute
{
    public object?[] Data { get; }

    public InlineDataAttribute(params object?[]? data)
    {
        // Handle [InlineData(null)] which passes null as the params array
        Data = data ?? new object?[] { null };
    }
}

/// <summary>
/// Provides data for a [Theory] test method from a static member (method or property).
/// The member must return IEnumerable&lt;object?[]&gt;.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MemberDataAttribute : Attribute
{
    public string MemberName { get; }
    public Type? MemberType { get; set; }

    public MemberDataAttribute(string memberName)
    {
        MemberName = memberName;
    }
}
