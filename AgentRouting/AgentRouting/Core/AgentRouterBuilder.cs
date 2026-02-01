using AgentRouting.Core;
using AgentRouting.Middleware;

namespace AgentRouting.Core;

/// <summary>
/// Fluent builder for creating AgentRouter instances with middleware.
/// </summary>
public class AgentRouterBuilder
{
    private readonly List<IAgentMiddleware> _middleware = new();
    private readonly List<IAgent> _agents = new();
    private readonly List<(string id, string name, System.Linq.Expressions.Expression<Func<RoutingContext, bool>> condition, string targetAgent, int priority)> _rules = new();
    private IAgentLogger? _logger;

    public AgentRouterBuilder WithLogger(IAgentLogger logger)
    {
        _logger = logger;
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
        var logger = _logger ?? new ConsoleAgentLogger();
        var router = new AgentRouter(logger);

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
