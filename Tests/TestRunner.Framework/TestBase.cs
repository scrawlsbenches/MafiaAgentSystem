namespace TestRunner.Framework;

/// <summary>
/// Base class for tests that provides a template for state isolation.
/// Override SetUp and TearDown to add custom initialization and cleanup.
/// </summary>
/// <remarks>
/// This is a minimal base class that test projects can inherit from
/// and extend with application-specific state management.
///
/// Example usage in a test project:
/// <code>
/// public class MyTestBase : TestBase
/// {
///     private MyGlobalState _originalState;
///
///     public override void SetUp()
///     {
///         base.SetUp();
///         _originalState = MyGlobalState.Current;
///     }
///
///     public override void TearDown()
///     {
///         MyGlobalState.Current = _originalState;
///         base.TearDown();
///     }
/// }
/// </code>
/// </remarks>
public abstract class TestBase
{
    /// <summary>
    /// Called before each test. Override to add custom setup logic.
    /// </summary>
    [SetUp]
    public virtual void SetUp()
    {
        // Base implementation does nothing.
        // Derived classes should capture global state here.
    }

    /// <summary>
    /// Called after each test (even if test fails). Override to add custom cleanup.
    /// </summary>
    [TearDown]
    public virtual void TearDown()
    {
        // Base implementation does nothing.
        // Derived classes should restore global state here.
    }
}
