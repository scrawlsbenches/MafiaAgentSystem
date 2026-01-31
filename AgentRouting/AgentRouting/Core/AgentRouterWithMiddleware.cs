using AgentRouting.Core;
using AgentRouting.Middleware;

namespace AgentRouting.Core;

/// <summary>
/// Extension to AgentRouter to support middleware
/// </summary>
public class AgentRouterWithMiddleware : AgentRouter
{
    private readonly MiddlewarePipeline _pipeline;
    private readonly List<IAgentMiddleware> _middlewareList = new();
    private MessageDelegate? _builtPipeline;

    public AgentRouterWithMiddleware(IAgentLogger logger) : base(logger)
    {
        _pipeline = new MiddlewarePipeline();
    }

    /// <summary>
    /// Add middleware to the processing pipeline
    /// </summary>
    public void UseMiddleware(IAgentMiddleware middleware)
    {
        _pipeline.Use(middleware);
        _middlewareList.Add(middleware);
        _builtPipeline = null; // Invalidate cached pipeline
    }

    /// <summary>
    /// Route message through middleware pipeline
    /// </summary>
    public new async Task<MessageResult> RouteMessageAsync(
        AgentMessage message,
        CancellationToken ct = default)
    {
        // Build pipeline on first use
        if (_builtPipeline == null)
        {
            _builtPipeline = _pipeline.Build(
                async (msg, token) => await base.RouteMessageAsync(msg, token)
            );
        }

        return await _builtPipeline(message, ct);
    }

    /// <summary>
    /// Get configured middleware
    /// </summary>
    public IReadOnlyList<IAgentMiddleware> GetMiddleware()
    {
        return _middlewareList.AsReadOnly();
    }
}

/// <summary>
/// Fluent builder for router with middleware
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

    public AgentRouterWithMiddleware Build()
    {
        var logger = _logger ?? new ConsoleAgentLogger();
        var router = new AgentRouterWithMiddleware(logger);

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
