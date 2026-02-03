using AgentRouting.Core;
using AgentRouting.Infrastructure;
using AgentRouting.Middleware;
using RulesEngine.Core;

namespace AgentRouting.DependencyInjection;

/// <summary>
/// Extension methods for registering AgentRouting services with the IoC container.
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Registers core AgentRouting services with the container.
    /// Includes: IAgentLogger, ISystemClock, IStateStore, IMiddlewarePipeline, IRulesEngine, AgentRouter
    /// </summary>
    /// <param name="container">The service container</param>
    /// <param name="configure">Optional configuration action for the router</param>
    /// <returns>The container for chaining</returns>
    public static IServiceContainer AddAgentRouting(
        this IServiceContainer container,
        Action<AgentRoutingOptions>? configure = null)
    {
        var options = new AgentRoutingOptions();
        configure?.Invoke(options);

        // Infrastructure services (singletons by default)
        if (!container.IsRegistered<ISystemClock>())
        {
            container.AddSingleton<ISystemClock>(SystemClock.Instance);
        }

        if (!container.IsRegistered<IStateStore>())
        {
            container.AddSingleton<IStateStore>(_ => new InMemoryStateStore());
        }

        // Logging
        if (!container.IsRegistered<IAgentLogger>())
        {
            container.AddSingleton<IAgentLogger>(_ =>
                options.Logger ?? new ConsoleAgentLogger());
        }

        // Middleware pipeline (transient - each router gets its own)
        container.AddTransient<IMiddlewarePipeline>(_ => new MiddlewarePipeline());

        // Rules engine for routing (transient - each router gets its own)
        container.AddTransient<IRulesEngine<RoutingContext>>(_ =>
            new RulesEngineCore<RoutingContext>(options.RulesEngineOptions));

        // AgentRouter (transient - typical usage creates one per scope/lifetime)
        container.AddTransient<AgentRouter>(c => new AgentRouter(
            c.Resolve<IAgentLogger>(),
            c.Resolve<IMiddlewarePipeline>(),
            c.Resolve<IRulesEngine<RoutingContext>>()
        ));

        return container;
    }

    /// <summary>
    /// Registers a middleware type with the container.
    /// Middleware is registered as transient by default.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type</typeparam>
    /// <param name="container">The service container</param>
    /// <param name="factory">Factory function to create the middleware</param>
    /// <returns>The container for chaining</returns>
    public static IServiceContainer AddMiddleware<TMiddleware>(
        this IServiceContainer container,
        Func<IServiceContainer, TMiddleware> factory)
        where TMiddleware : class, IAgentMiddleware
    {
        container.AddTransient(factory);
        return container;
    }

    /// <summary>
    /// Registers a middleware type with the container as a singleton.
    /// Use for stateful middleware that should be shared across the application.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type</typeparam>
    /// <param name="container">The service container</param>
    /// <param name="factory">Factory function to create the middleware</param>
    /// <returns>The container for chaining</returns>
    public static IServiceContainer AddMiddlewareSingleton<TMiddleware>(
        this IServiceContainer container,
        Func<IServiceContainer, TMiddleware> factory)
        where TMiddleware : class, IAgentMiddleware
    {
        container.AddSingleton(factory);
        return container;
    }

    /// <summary>
    /// Registers an agent type with the container.
    /// Agents are registered as transient by default.
    /// </summary>
    /// <typeparam name="TAgent">The agent type</typeparam>
    /// <param name="container">The service container</param>
    /// <param name="factory">Factory function to create the agent</param>
    /// <returns>The container for chaining</returns>
    public static IServiceContainer AddAgent<TAgent>(
        this IServiceContainer container,
        Func<IServiceContainer, TAgent> factory)
        where TAgent : class, IAgent
    {
        container.AddTransient(factory);
        return container;
    }

    /// <summary>
    /// Registers an agent type with the container as a singleton.
    /// Use for agents that maintain state or should be shared.
    /// </summary>
    /// <typeparam name="TAgent">The agent type</typeparam>
    /// <param name="container">The service container</param>
    /// <param name="factory">Factory function to create the agent</param>
    /// <returns>The container for chaining</returns>
    public static IServiceContainer AddAgentSingleton<TAgent>(
        this IServiceContainer container,
        Func<IServiceContainer, TAgent> factory)
        where TAgent : class, IAgent
    {
        container.AddSingleton(factory);
        return container;
    }

    /// <summary>
    /// Registers an existing agent instance with the container.
    /// </summary>
    /// <typeparam name="TAgent">The agent type</typeparam>
    /// <param name="container">The service container</param>
    /// <param name="instance">The agent instance</param>
    /// <returns>The container for chaining</returns>
    public static IServiceContainer AddAgentInstance<TAgent>(
        this IServiceContainer container,
        TAgent instance)
        where TAgent : class, IAgent
    {
        container.AddSingleton(instance);
        return container;
    }

    /// <summary>
    /// Registers a RulesEngine for a specific context type.
    /// </summary>
    /// <typeparam name="TContext">The context type for the rules engine</typeparam>
    /// <param name="container">The service container</param>
    /// <param name="options">Optional rules engine options</param>
    /// <returns>The container for chaining</returns>
    public static IServiceContainer AddRulesEngine<TContext>(
        this IServiceContainer container,
        RulesEngineOptions? options = null)
    {
        container.AddTransient<IRulesEngine<TContext>>(_ =>
            new RulesEngineCore<TContext>(options ?? new RulesEngineOptions()));
        return container;
    }

    /// <summary>
    /// Registers a singleton RulesEngine for a specific context type.
    /// Use when rules should be shared across the application.
    /// </summary>
    /// <typeparam name="TContext">The context type for the rules engine</typeparam>
    /// <param name="container">The service container</param>
    /// <param name="options">Optional rules engine options</param>
    /// <returns>The container for chaining</returns>
    public static IServiceContainer AddRulesEngineSingleton<TContext>(
        this IServiceContainer container,
        RulesEngineOptions? options = null)
    {
        container.AddSingleton<IRulesEngine<TContext>>(_ =>
            new RulesEngineCore<TContext>(options ?? new RulesEngineOptions()));
        return container;
    }
}

/// <summary>
/// Configuration options for AgentRouting services.
/// </summary>
public class AgentRoutingOptions
{
    /// <summary>
    /// Custom logger to use. If null, ConsoleAgentLogger is used.
    /// </summary>
    public IAgentLogger? Logger { get; set; }

    /// <summary>
    /// Options for the routing rules engine.
    /// </summary>
    public RulesEngineOptions RulesEngineOptions { get; set; } = new();
}
