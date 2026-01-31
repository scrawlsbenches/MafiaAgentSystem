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
