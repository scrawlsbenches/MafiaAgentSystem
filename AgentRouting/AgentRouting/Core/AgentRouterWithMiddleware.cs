using AgentRouting.Core;
using AgentRouting.Middleware;

namespace AgentRouting.Core;

/// <summary>
/// Extension to AgentRouter to support middleware
/// </summary>
public class AgentRouterWithMiddleware : AgentRouter
{
    private readonly MiddlewarePipeline _pipeline;
    
    public AgentRouterWithMiddleware(IAgentLogger logger) : base(logger)
    {
        _pipeline = new MiddlewarePipeline();
    }
    
    /// <summary>
    /// Add middleware to the processing pipeline
    /// </summary>
    public void UseMiddleware(IMessageMiddleware middleware)
    {
        _pipeline.Use(middleware);
    }
    
    /// <summary>
    /// Route message through middleware pipeline
    /// </summary>
    public new async Task<MessageResult> RouteMessageAsync(
        AgentMessage message,
        CancellationToken ct = default)
    {
        // Execute through middleware pipeline
        return await _pipeline.ExecuteAsync(
            message,
            (msg, cancellationToken) => base.RouteMessageAsync(msg, cancellationToken),
            ct
        );
    }
    
    /// <summary>
    /// Get configured middleware
    /// </summary>
    public IReadOnlyList<IMessageMiddleware> GetMiddleware()
    {
        return _pipeline.GetMiddleware();
    }
}

/// <summary>
/// Fluent builder for router with middleware
/// </summary>
public class AgentRouterBuilder
{
    private readonly List<IMessageMiddleware> _middleware = new();
    private readonly List<IAgent> _agents = new();
    private readonly List<(string id, string name, System.Linq.Expressions.Expression<Func<RoutingContext, bool>> condition, string targetAgent, int priority)> _rules = new();
    private IAgentLogger? _logger;
    
    public AgentRouterBuilder WithLogger(IAgentLogger logger)
    {
        _logger = logger;
        return this;
    }
    
    public AgentRouterBuilder UseMiddleware(IMessageMiddleware middleware)
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
