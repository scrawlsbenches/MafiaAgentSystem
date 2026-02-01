using AgentRouting.Core;
using AgentRouting.Middleware;
using RulesEngine.Core;

namespace AgentRouting.Core;

/// <summary>
/// Fluent builder for creating AgentRouter instances with middleware.
/// Creates default dependencies (logger, pipeline, rules engine) if not explicitly provided.
/// </summary>
public class AgentRouterBuilder
{
    private readonly List<IAgentMiddleware> _middleware = new();
    private readonly List<IAgent> _agents = new();
    private readonly List<(string id, string name, System.Linq.Expressions.Expression<Func<RoutingContext, bool>> condition, string targetAgent, int priority)> _rules = new();
    private IAgentLogger? _logger;
    private IMiddlewarePipeline? _pipeline;
    private IRulesEngine<RoutingContext>? _routingEngine;

    public AgentRouterBuilder WithLogger(IAgentLogger logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Use a custom middleware pipeline instead of the default.
    /// </summary>
    public AgentRouterBuilder WithPipeline(IMiddlewarePipeline pipeline)
    {
        _pipeline = pipeline;
        return this;
    }

    /// <summary>
    /// Use a custom routing engine instead of the default.
    /// </summary>
    public AgentRouterBuilder WithRoutingEngine(IRulesEngine<RoutingContext> routingEngine)
    {
        _routingEngine = routingEngine;
        return this;
    }

    public AgentRouterBuilder UseMiddleware(IAgentMiddleware middleware)
    {
        _middleware.Add(middleware);
        return this;
    }

    public AgentRouterBuilder RegisterAgent(IAgent agent)
    {
        _agents.Add(agent);
        return this;
    }

    public AgentRouterBuilder AddRoutingRule(
        string id,
        string name,
        System.Linq.Expressions.Expression<Func<RoutingContext, bool>> condition,
        string targetAgent,
        int priority = 0)
    {
        _rules.Add((id, name, condition, targetAgent, priority));
        return this;
    }

    public AgentRouter Build()
    {
        // Create defaults if not provided
        var logger = _logger ?? new ConsoleAgentLogger();
        var pipeline = _pipeline ?? new MiddlewarePipeline();
        var routingEngine = _routingEngine ?? new RulesEngineCore<RoutingContext>(new RulesEngineOptions
        {
            StopOnFirstMatch = true,  // First matching rule wins
            TrackPerformance = true
        });

        var router = new AgentRouter(logger, pipeline, routingEngine);

        // Add middleware
        foreach (var middleware in _middleware)
        {
            router.UseMiddleware(middleware);
        }

        // Register agents
        foreach (var agent in _agents)
        {
            router.RegisterAgent(agent);
        }

        // Add routing rules
        foreach (var (id, name, condition, targetAgent, priority) in _rules)
        {
            router.AddRoutingRule(id, name, condition, targetAgent, priority);
        }

        return router;
    }
}
