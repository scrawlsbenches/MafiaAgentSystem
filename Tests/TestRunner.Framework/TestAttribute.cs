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

/// <summary>
/// Marks a method to be called before each test method in the class.
/// The method can be synchronous (void) or asynchronous (Task).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SetUpAttribute : Attribute
{
}

/// <summary>
/// Marks a method to be called after each test method in the class.
/// The method will be called even if the test fails (in a finally block).
/// The method can be synchronous (void) or asynchronous (Task).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TearDownAttribute : Attribute
{
}

/// <summary>
/// Marks a method to be called once before any test in the class runs.
/// The method can be synchronous (void) or asynchronous (Task).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class OneTimeSetUpAttribute : Attribute
{
}

/// <summary>
/// Marks a method to be called once after all tests in the class have run.
/// The method will be called even if tests fail (in a finally block).
/// The method can be synchronous (void) or asynchronous (Task).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class OneTimeTearDownAttribute : Attribute
{
}
