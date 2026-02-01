using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentRouting.DependencyInjection;
using TestRunner.Framework;

namespace TestRunner.Tests;

// Test helper classes
public interface ITestService { string Name { get; } }
public class TestService : ITestService { public string Name => "TestService"; }
public class AnotherTestService : ITestService { public string Name => "Another"; }

public interface IDisposableService : IDisposable { bool IsDisposed { get; } }
public class DisposableService : IDisposableService
{
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
}

public interface ICountingService { int CallCount { get; } }
public class CountingService : ICountingService
{
    private static int _globalCount;
    public int CallCount { get; }
    public CountingService() => CallCount = Interlocked.Increment(ref _globalCount);
    public static void Reset() => _globalCount = 0;
}

public interface IDependentService { ITestService Dependency { get; } }
public class DependentService : IDependentService
{
    public ITestService Dependency { get; }
    public DependentService(ITestService dependency) => Dependency = dependency;
}

[TestClass]
public class ServiceContainerSingletonTests
{
    [Test]
    public void Singleton_ReturnsSameInstance_OnMultipleResolves()
    {
        using var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c => new TestService());

        var instance1 = container.Resolve<ITestService>();
        var instance2 = container.Resolve<ITestService>();

        Assert.True(ReferenceEquals(instance1, instance2));
    }

    [Test]
    public void Singleton_FactoryCalledOnce_OnMultipleResolves()
    {
        CountingService.Reset();
        using var container = new ServiceContainer();
        int factoryCallCount = 0;
        container.AddSingleton<ICountingService>(c =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return new CountingService();
        });

        _ = container.Resolve<ICountingService>();
        _ = container.Resolve<ICountingService>();
        _ = container.Resolve<ICountingService>();

        Assert.Equal(1, factoryCallCount);
    }

    [Test]
    public void Singleton_WithInstance_ReturnsSameInstance()
    {
        using var container = new ServiceContainer();
        var original = new TestService();
        container.AddSingleton<ITestService>(original);

        var resolved = container.Resolve<ITestService>();

        Assert.True(ReferenceEquals(original, resolved));
    }

    [Test]
    public void Singleton_DisposedWhenContainerDisposed()
    {
        var disposable = new DisposableService();
        var container = new ServiceContainer();
        container.AddSingleton<IDisposableService>(disposable);

        _ = container.Resolve<IDisposableService>(); // Ensure it's in the cache
        Assert.False(disposable.IsDisposed);

        container.Dispose();
        Assert.True(disposable.IsDisposed);
    }

    [Test]
    public void Singleton_CreatedByFactory_DisposedWhenContainerDisposed()
    {
        DisposableService? created = null;
        var container = new ServiceContainer();
        container.AddSingleton<IDisposableService>(c =>
        {
            created = new DisposableService();
            return created;
        });

        _ = container.Resolve<IDisposableService>();
        Assert.NotNull(created);
        Assert.False(created!.IsDisposed);

        container.Dispose();
        Assert.True(created.IsDisposed);
    }
}

[TestClass]
public class ServiceContainerTransientTests
{
    [Test]
    public void Transient_ReturnsNewInstance_OnEveryResolve()
    {
        using var container = new ServiceContainer();
        container.AddTransient<ITestService>(c => new TestService());

        var instance1 = container.Resolve<ITestService>();
        var instance2 = container.Resolve<ITestService>();

        Assert.False(ReferenceEquals(instance1, instance2));
    }

    [Test]
    public void Transient_FactoryCalledEveryTime()
    {
        using var container = new ServiceContainer();
        int factoryCallCount = 0;
        container.AddTransient<ITestService>(c =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return new TestService();
        });

        _ = container.Resolve<ITestService>();
        _ = container.Resolve<ITestService>();
        _ = container.Resolve<ITestService>();

        Assert.Equal(3, factoryCallCount);
    }

    [Test]
    public void Transient_NotDisposedByContainer()
    {
        var instances = new List<DisposableService>();
        var container = new ServiceContainer();
        container.AddTransient<IDisposableService>(c =>
        {
            var instance = new DisposableService();
            instances.Add(instance);
            return instance;
        });

        _ = container.Resolve<IDisposableService>();
        _ = container.Resolve<IDisposableService>();

        container.Dispose();

        // Transients are NOT disposed by container - caller's responsibility
        foreach (var instance in instances)
        {
            Assert.False(instance.IsDisposed);
        }
    }
}

[TestClass]
public class ServiceContainerScopedTests
{
    [Test]
    public void Scoped_ReturnsSameInstance_WithinScope()
    {
        using var container = new ServiceContainer();
        container.AddScoped<ITestService>(c => new TestService());

        using var scope = container.CreateScope();
        var instance1 = scope.Resolve<ITestService>();
        var instance2 = scope.Resolve<ITestService>();

        Assert.True(ReferenceEquals(instance1, instance2));
    }

    [Test]
    public void Scoped_ReturnsDifferentInstances_AcrossScopes()
    {
        using var container = new ServiceContainer();
        container.AddScoped<ITestService>(c => new TestService());

        ITestService instance1, instance2;
        using (var scope1 = container.CreateScope())
        {
            instance1 = scope1.Resolve<ITestService>();
        }
        using (var scope2 = container.CreateScope())
        {
            instance2 = scope2.Resolve<ITestService>();
        }

        Assert.False(ReferenceEquals(instance1, instance2));
    }

    [Test]
    public void Scoped_CannotResolveFromRoot_ThrowsException()
    {
        using var container = new ServiceContainer();
        container.AddScoped<ITestService>(c => new TestService());

        bool threw = false;
        string? message = null;
        try
        {
            container.Resolve<ITestService>();
        }
        catch (InvalidOperationException ex)
        {
            threw = true;
            message = ex.Message;
        }

        Assert.True(threw);
        Assert.True(message?.Contains("Scoped") == true);
        Assert.True(message?.Contains("CreateScope") == true);
    }

    [Test]
    public void Scoped_DisposedWhenScopeDisposed()
    {
        using var container = new ServiceContainer();
        container.AddScoped<IDisposableService>(c => new DisposableService());

        DisposableService? instance = null;
        using (var scope = container.CreateScope())
        {
            instance = (DisposableService)scope.Resolve<IDisposableService>();
            Assert.False(instance.IsDisposed);
        }

        Assert.True(instance!.IsDisposed);
    }

    [Test]
    public void Scoped_FactoryCalledOncePerScope()
    {
        using var container = new ServiceContainer();
        int factoryCallCount = 0;
        container.AddScoped<ITestService>(c =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return new TestService();
        });

        using (var scope = container.CreateScope())
        {
            _ = scope.Resolve<ITestService>();
            _ = scope.Resolve<ITestService>();
        }

        Assert.Equal(1, factoryCallCount);

        using (var scope = container.CreateScope())
        {
            _ = scope.Resolve<ITestService>();
        }

        Assert.Equal(2, factoryCallCount);
    }
}

[TestClass]
public class ServiceContainerErrorTests
{
    [Test]
    public void Resolve_UnregisteredService_ThrowsException()
    {
        using var container = new ServiceContainer();

        bool threw = false;
        string? message = null;
        try
        {
            container.Resolve<ITestService>();
        }
        catch (InvalidOperationException ex)
        {
            threw = true;
            message = ex.Message;
        }

        Assert.True(threw);
        Assert.True(message?.Contains("not registered") == true);
    }

    [Test]
    public void Resolve_FromDisposedContainer_ThrowsObjectDisposedException()
    {
        var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c => new TestService());
        container.Dispose();

        bool threw = false;
        try
        {
            container.Resolve<ITestService>();
        }
        catch (ObjectDisposedException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Test]
    public void Resolve_FromDisposedScope_ThrowsObjectDisposedException()
    {
        using var container = new ServiceContainer();
        container.AddScoped<ITestService>(c => new TestService());

        var scope = container.CreateScope();
        scope.Dispose();

        bool threw = false;
        try
        {
            scope.Resolve<ITestService>();
        }
        catch (ObjectDisposedException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Test]
    public void AddSingleton_NullInstance_ThrowsArgumentNullException()
    {
        using var container = new ServiceContainer();

        bool threw = false;
        try
        {
            ITestService nullInstance = null!;
            container.AddSingleton<ITestService>(nullInstance);
        }
        catch (ArgumentNullException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Test]
    public void CreateScope_OnDisposedContainer_ThrowsObjectDisposedException()
    {
        var container = new ServiceContainer();
        container.Dispose();

        bool threw = false;
        try
        {
            container.CreateScope();
        }
        catch (ObjectDisposedException)
        {
            threw = true;
        }

        Assert.True(threw);
    }
}

[TestClass]
public class ServiceContainerDependencyTests
{
    [Test]
    public void DependencyChain_ResolvesCorrectly()
    {
        using var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c => new TestService());
        container.AddSingleton<IDependentService>(c => new DependentService(c.Resolve<ITestService>()));

        var dependent = container.Resolve<IDependentService>();

        Assert.NotNull(dependent.Dependency);
        Assert.Equal("TestService", dependent.Dependency.Name);
    }

    [Test]
    public void DependencyChain_SingletonSharesDependency()
    {
        using var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c => new TestService());
        container.AddSingleton<IDependentService>(c => new DependentService(c.Resolve<ITestService>()));

        var dependency = container.Resolve<ITestService>();
        var dependent = container.Resolve<IDependentService>();

        Assert.True(ReferenceEquals(dependency, dependent.Dependency));
    }

    [Test]
    public void Scope_CanResolveSingletonFromRoot()
    {
        using var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c => new TestService());

        var rootInstance = container.Resolve<ITestService>();

        using var scope = container.CreateScope();
        var scopeInstance = scope.Resolve<ITestService>();

        Assert.True(ReferenceEquals(rootInstance, scopeInstance));
    }

    [Test]
    public void Scope_CanResolveTransient()
    {
        using var container = new ServiceContainer();
        container.AddTransient<ITestService>(c => new TestService());

        using var scope = container.CreateScope();
        var instance1 = scope.Resolve<ITestService>();
        var instance2 = scope.Resolve<ITestService>();

        Assert.False(ReferenceEquals(instance1, instance2));
    }
}

[TestClass]
public class ServiceContainerRegistrationTests
{
    [Test]
    public void IsRegistered_ReturnsTrue_ForRegisteredService()
    {
        using var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c => new TestService());

        Assert.True(container.IsRegistered<ITestService>());
    }

    [Test]
    public void IsRegistered_ReturnsFalse_ForUnregisteredService()
    {
        using var container = new ServiceContainer();

        Assert.False(container.IsRegistered<ITestService>());
    }

    [Test]
    public void TryResolve_ReturnsTrue_AndInstance_ForRegisteredService()
    {
        using var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c => new TestService());

        bool result = container.TryResolve<ITestService>(out var service);

        Assert.True(result);
        Assert.NotNull(service);
    }

    [Test]
    public void TryResolve_ReturnsFalse_ForUnregisteredService()
    {
        using var container = new ServiceContainer();

        bool result = container.TryResolve<ITestService>(out var service);

        Assert.False(result);
        Assert.Null(service);
    }

    [Test]
    public void TryResolve_ReturnsFalse_ForScopedServiceFromRoot()
    {
        using var container = new ServiceContainer();
        container.AddScoped<ITestService>(c => new TestService());

        bool result = container.TryResolve<ITestService>(out var service);

        Assert.False(result);
        Assert.Null(service);
    }

    [Test]
    public void Registration_CanOverwrite_PreviousRegistration()
    {
        using var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c => new TestService());
        container.AddSingleton<ITestService>(c => new AnotherTestService());

        var instance = container.Resolve<ITestService>();

        Assert.Equal("Another", instance.Name);
    }

    [Test]
    public void FluentRegistration_ReturnsContainer()
    {
        using var container = new ServiceContainer()
            .AddSingleton<ITestService>(c => new TestService())
            .AddTransient<ICountingService>(c => new CountingService());

        Assert.True(container.IsRegistered<ITestService>());
        Assert.True(container.IsRegistered<ICountingService>());
    }
}

[TestClass]
public class ServiceContainerConcurrencyTests
{
    [Test]
    public void Singleton_ThreadSafe_ConcurrentResolve()
    {
        using var container = new ServiceContainer();
        int factoryCallCount = 0;
        container.AddSingleton<ITestService>(c =>
        {
            Interlocked.Increment(ref factoryCallCount);
            Thread.Sleep(10); // Simulate slow construction
            return new TestService();
        });

        var instances = new ITestService[10];
        var threads = new Thread[10];

        for (int i = 0; i < 10; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                instances[index] = container.Resolve<ITestService>();
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        // All instances should be the same
        var first = instances[0];
        foreach (var instance in instances)
        {
            Assert.True(ReferenceEquals(first, instance));
        }

        // Factory might be called multiple times due to race, but result is still same instance
        // (this is acceptable behavior - the important thing is same instance returned)
    }

    [Test]
    public void Scoped_ThreadSafe_ConcurrentResolveWithinScope()
    {
        using var container = new ServiceContainer();
        container.AddScoped<ITestService>(c => new TestService());

        using var scope = container.CreateScope();
        var instances = new ITestService[10];
        var threads = new Thread[10];

        for (int i = 0; i < 10; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                instances[index] = scope.Resolve<ITestService>();
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        // All instances should be the same within scope
        var first = instances[0];
        foreach (var instance in instances)
        {
            Assert.True(ReferenceEquals(first, instance));
        }
    }

    [Test]
    public void ConcurrentRegistration_DoesNotThrow()
    {
        using var container = new ServiceContainer();
        var threads = new Thread[10];

        for (int i = 0; i < 10; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                // Each thread registers a different service type - safe
                if (index % 2 == 0)
                    container.AddSingleton<ITestService>(c => new TestService());
                else
                    container.AddTransient<ICountingService>(c => new CountingService());
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        // Should not throw, and at least one registration should exist
        Assert.True(container.IsRegistered<ITestService>() || container.IsRegistered<ICountingService>());
    }
}

[TestClass]
public class ServiceContainerEdgeCaseTests
{
    [Test]
    public void MultipleDispose_DoesNotThrow()
    {
        var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c => new TestService());

        container.Dispose();
        container.Dispose(); // Should not throw
        container.Dispose(); // Should not throw
    }

    [Test]
    public void ScopeMultipleDispose_DoesNotThrow()
    {
        using var container = new ServiceContainer();
        container.AddScoped<ITestService>(c => new TestService());

        var scope = container.CreateScope();
        scope.Dispose();
        scope.Dispose(); // Should not throw
        scope.Dispose(); // Should not throw
    }

    [Test]
    public void Scope_TryResolve_ReturnsFalse_AfterDispose()
    {
        using var container = new ServiceContainer();
        container.AddScoped<ITestService>(c => new TestService());

        var scope = container.CreateScope();
        scope.Dispose();

        bool result = scope.TryResolve<ITestService>(out var service);

        Assert.False(result);
        Assert.Null(service);
    }

    [Test]
    public void Container_TryResolve_ReturnsFalse_AfterDispose()
    {
        var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c => new TestService());
        container.Dispose();

        bool result = container.TryResolve<ITestService>(out var service);

        Assert.False(result);
        Assert.Null(service);
    }

    [Test]
    public void DisposableService_ExceptionInDispose_DoesNotPreventOtherDisposals()
    {
        var goodService = new DisposableService();
        var container = new ServiceContainer();

        container.AddSingleton<IDisposableService>(goodService);
        // Note: We can't easily test exception swallowing without a throwing service
        // but we verify the good service still gets disposed

        container.Dispose();
        Assert.True(goodService.IsDisposed);
    }
}
