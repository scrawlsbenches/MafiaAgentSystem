using System;

namespace AgentRouting.DependencyInjection;

/// <summary>
/// Lightweight IoC container with Singleton, Transient, and Scoped lifetime support.
/// Uses lambda-based registration for explicit construction (no reflection).
/// </summary>
public interface IServiceContainer : IDisposable
{
    /// <summary>
    /// Register a singleton service with a factory. Instance created on first resolve, then cached.
    /// </summary>
    IServiceContainer AddSingleton<TService>(Func<IServiceContainer, TService> factory) where TService : class;

    /// <summary>
    /// Register an existing instance as a singleton.
    /// </summary>
    IServiceContainer AddSingleton<TService>(TService instance) where TService : class;

    /// <summary>
    /// Register a transient service. New instance created on every resolve.
    /// </summary>
    IServiceContainer AddTransient<TService>(Func<IServiceContainer, TService> factory) where TService : class;

    /// <summary>
    /// Register a scoped service. One instance per scope, shared within that scope.
    /// Cannot be resolved from root container - must use CreateScope().
    /// </summary>
    IServiceContainer AddScoped<TService>(Func<IServiceContainer, TService> factory) where TService : class;

    /// <summary>
    /// Resolve a service. Throws if not registered or if scoped service resolved from root.
    /// </summary>
    TService Resolve<TService>() where TService : class;

    /// <summary>
    /// Try to resolve a service. Returns false if not registered.
    /// </summary>
    bool TryResolve<TService>(out TService? service) where TService : class;

    /// <summary>
    /// Check if a service is registered.
    /// </summary>
    bool IsRegistered<TService>() where TService : class;

    /// <summary>
    /// Create a new scope for scoped service resolution.
    /// </summary>
    IServiceScope CreateScope();
}

/// <summary>
/// A scope for resolving scoped services. Dispose to clean up scoped instances.
/// </summary>
public interface IServiceScope : IDisposable
{
    /// <summary>
    /// Resolve a service within this scope.
    /// </summary>
    TService Resolve<TService>() where TService : class;

    /// <summary>
    /// Try to resolve a service within this scope.
    /// </summary>
    bool TryResolve<TService>(out TService? service) where TService : class;
}
