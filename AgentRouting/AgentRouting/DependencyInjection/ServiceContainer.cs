using System;
using System.Collections.Concurrent;
using System.Linq;

namespace AgentRouting.DependencyInjection;

/// <summary>
/// Service lifetime options.
/// </summary>
public enum ServiceLifetime
{
    Singleton,
    Transient,
    Scoped
}

/// <summary>
/// Lightweight IoC container implementation.
/// </summary>
public class ServiceContainer : IServiceContainer
{
    private readonly ConcurrentDictionary<Type, ServiceDescriptor> _descriptors = new();
    private readonly ConcurrentDictionary<Type, object> _singletons = new();
    private readonly object _singletonLock = new();
    private bool _disposed;

    public IServiceContainer AddSingleton<TService>(Func<IServiceContainer, TService> factory) where TService : class
    {
        ThrowIfDisposed();
        _descriptors[typeof(TService)] = new ServiceDescriptor(ServiceLifetime.Singleton, c => factory(c));
        return this;
    }

    public IServiceContainer AddSingleton<TService>(TService instance) where TService : class
    {
        ThrowIfDisposed();
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        _singletons[typeof(TService)] = instance;
        _descriptors[typeof(TService)] = new ServiceDescriptor(ServiceLifetime.Singleton, _ => instance);
        return this;
    }

    public IServiceContainer AddTransient<TService>(Func<IServiceContainer, TService> factory) where TService : class
    {
        ThrowIfDisposed();
        _descriptors[typeof(TService)] = new ServiceDescriptor(ServiceLifetime.Transient, c => factory(c));
        return this;
    }

    public IServiceContainer AddScoped<TService>(Func<IServiceContainer, TService> factory) where TService : class
    {
        ThrowIfDisposed();
        _descriptors[typeof(TService)] = new ServiceDescriptor(ServiceLifetime.Scoped, c => factory(c));
        return this;
    }

    public TService Resolve<TService>() where TService : class
    {
        ThrowIfDisposed();
        var type = typeof(TService);

        // Fast path: check singleton cache first
        if (_singletons.TryGetValue(type, out var cached))
            return (TService)cached;

        if (!_descriptors.TryGetValue(type, out var descriptor))
            throw new InvalidOperationException($"Service '{type.Name}' is not registered.");

        if (descriptor.Lifetime == ServiceLifetime.Scoped)
            throw new InvalidOperationException(
                $"Scoped service '{type.Name}' cannot be resolved from root container. Use CreateScope().");

        if (descriptor.Lifetime == ServiceLifetime.Singleton)
        {
            // Double-checked locking ensures only ONE instance is ever created.
            // Without the lock, multiple threads could all call the factory
            // before any of them cache the result.
            lock (_singletonLock)
            {
                // Check again inside lock - another thread may have created it
                if (_singletons.TryGetValue(type, out cached))
                    return (TService)cached;

                var instance = (TService)descriptor.Factory(this);
                _singletons[type] = instance;
                return instance;
            }
        }

        // Transient: always create new instance
        return (TService)descriptor.Factory(this);
    }

    public bool TryResolve<TService>(out TService? service) where TService : class
    {
        service = null;
        if (_disposed) return false;

        var type = typeof(TService);

        // Check singleton cache
        if (_singletons.TryGetValue(type, out var cached))
        {
            service = (TService)cached;
            return true;
        }

        if (!_descriptors.TryGetValue(type, out var descriptor))
            return false;

        // Can't resolve scoped from root
        if (descriptor.Lifetime == ServiceLifetime.Scoped)
            return false;

        try
        {
            service = Resolve<TService>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsRegistered<TService>() where TService : class
    {
        return _descriptors.ContainsKey(typeof(TService));
    }

    public IServiceScope CreateScope()
    {
        ThrowIfDisposed();
        return new ServiceScope(this, _descriptors);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose singletons, collecting any exceptions rather than swallowing them
        List<Exception>? exceptions = null;
        foreach (var singleton in _singletons.Values.OfType<IDisposable>())
        {
            try
            {
                singleton.Dispose();
            }
            catch (Exception ex)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(ex);
            }
        }

        _singletons.Clear();
        _descriptors.Clear();

        if (exceptions != null)
        {
            throw new AggregateException("One or more services threw during disposal", exceptions);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));
    }

    // Internal for ServiceScope access
    internal bool TryGetDescriptor(Type type, out ServiceDescriptor descriptor)
    {
        return _descriptors.TryGetValue(type, out descriptor!);
    }

    internal bool TryGetSingleton(Type type, out object instance)
    {
        return _singletons.TryGetValue(type, out instance!);
    }

    internal void CacheSingleton(Type type, object instance)
    {
        _singletons.TryAdd(type, instance);
    }
}

/// <summary>
/// Describes a registered service.
/// </summary>
internal record ServiceDescriptor(ServiceLifetime Lifetime, Func<IServiceContainer, object> Factory);

/// <summary>
/// A scope for resolving scoped services.
/// </summary>
internal class ServiceScope : IServiceScope
{
    private readonly ServiceContainer _root;
    private readonly ConcurrentDictionary<Type, ServiceDescriptor> _descriptors;
    private readonly ConcurrentDictionary<Type, object> _scopedInstances = new();
    private readonly object _scopedLock = new();
    private bool _disposed;

    public ServiceScope(ServiceContainer root, ConcurrentDictionary<Type, ServiceDescriptor> descriptors)
    {
        _root = root;
        _descriptors = descriptors;
    }

    public TService Resolve<TService>() where TService : class
    {
        ThrowIfDisposed();
        var type = typeof(TService);

        if (!_descriptors.TryGetValue(type, out var descriptor))
            throw new InvalidOperationException($"Service '{type.Name}' is not registered.");

        // Singletons: delegate to root container which handles thread-safety
        if (descriptor.Lifetime == ServiceLifetime.Singleton)
        {
            return _root.Resolve<TService>();
        }

        // Transients: always create new
        if (descriptor.Lifetime == ServiceLifetime.Transient)
            return (TService)descriptor.Factory(_root);

        // Scoped: use GetOrAdd which is thread-safe for the factory call
        // Note: The factory may be called multiple times but only one result is stored.
        // For scoped services this is acceptable as they're short-lived.
        return (TService)_scopedInstances.GetOrAdd(type, _ => descriptor.Factory(_root));
    }

    public bool TryResolve<TService>(out TService? service) where TService : class
    {
        service = null;
        if (_disposed) return false;

        var type = typeof(TService);
        if (!_descriptors.ContainsKey(type))
            return false;

        try
        {
            service = Resolve<TService>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose scoped instances, collecting any exceptions
        List<Exception>? exceptions = null;
        foreach (var scoped in _scopedInstances.Values.OfType<IDisposable>())
        {
            try
            {
                scoped.Dispose();
            }
            catch (Exception ex)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(ex);
            }
        }

        _scopedInstances.Clear();

        if (exceptions != null)
        {
            throw new AggregateException("One or more scoped services threw during disposal", exceptions);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServiceScope));
    }
}
