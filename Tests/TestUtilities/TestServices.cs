namespace TestUtilities;

/// <summary>
/// Simple test service interface for DI testing.
/// </summary>
public interface ITestService
{
    string Name { get; }
}

/// <summary>
/// Default implementation of ITestService.
/// </summary>
public class TestService : ITestService
{
    public string Name => "TestService";
}

/// <summary>
/// Alternative implementation of ITestService.
/// </summary>
public class AnotherTestService : ITestService
{
    public string Name => "Another";
}

/// <summary>
/// Interface for testing disposable service behavior.
/// </summary>
public interface IDisposableService : IDisposable
{
    bool IsDisposed { get; }
}

/// <summary>
/// Disposable service that tracks its disposal state.
/// </summary>
public class DisposableService : IDisposableService
{
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
}

/// <summary>
/// Interface for testing singleton instantiation counting.
/// </summary>
public interface ICountingService
{
    int CallCount { get; }
}

/// <summary>
/// Service that counts how many times it's been instantiated.
/// Uses static counter to track across instances.
/// </summary>
public class CountingService : ICountingService
{
    private static int _globalCount;

    public int CallCount { get; }

    public CountingService() => CallCount = Interlocked.Increment(ref _globalCount);

    /// <summary>
    /// Resets the global counter. Call this in test setup.
    /// </summary>
    public static void Reset() => _globalCount = 0;
}

/// <summary>
/// Interface for testing dependency injection chains.
/// </summary>
public interface IDependentService
{
    ITestService Dependency { get; }
}

/// <summary>
/// Service with a dependency on ITestService.
/// </summary>
public class DependentService : IDependentService
{
    public ITestService Dependency { get; }

    public DependentService(ITestService dependency) => Dependency = dependency;
}
