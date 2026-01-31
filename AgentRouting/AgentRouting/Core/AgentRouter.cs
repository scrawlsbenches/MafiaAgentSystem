using RulesEngine.Core;
using System.Linq.Expressions;

namespace AgentRouting.Core;

/// <summary>
/// Routes messages to appropriate agents based on rules
/// </summary>
public class AgentRouter
{
    private readonly List<IAgent> _agents = new();
    private readonly RulesEngineCore<RoutingContext> _routingEngine;
    private readonly IAgentLogger _logger;
    private readonly Dictionary<string, IAgent> _agentById = new();
    
    public AgentRouter(IAgentLogger logger)
    {
        _logger = logger;
        _routingEngine = new RulesEngineCore<RoutingContext>(new RulesEngineOptions
        {
            StopOnFirstMatch = true,  // First matching rule wins
            TrackPerformance = true
        });
    }
    
    /// <summary>
    /// Register an agent with the router
    /// </summary>
    public void RegisterAgent(IAgent agent)
    {
        _agents.Add(agent);
        _agentById[agent.Id] = agent;
    }
    
    /// <summary>
    /// Add a routing rule
    /// </summary>
    public void AddRoutingRule(
        string ruleId,
        string ruleName,
        Expression<Func<RoutingContext, bool>> condition,
        string targetAgentId,
        int priority = 0)
    {
        var rule = new RuleBuilder<RoutingContext>()
            .WithId(ruleId)
            .WithName(ruleName)
            .WithPriority(priority)
            .When(condition)
            .Then(ctx => ctx.TargetAgentId = targetAgentId)
            .Build();
        
        _routingEngine.RegisterRule(rule);
    }
    
    /// <summary>
    /// Add a routing rule with custom action
    /// </summary>
    public void AddRoutingRule(
        string ruleId,
        string ruleName,
        Expression<Func<RoutingContext, bool>> condition,
        Action<RoutingContext> action,
        int priority = 0)
    {
        var rule = new RuleBuilder<RoutingContext>()
            .WithId(ruleId)
            .WithName(ruleName)
            .WithPriority(priority)
            .When(condition)
            .Then(action)
            .Build();
        
        _routingEngine.RegisterRule(rule);
    }
    
    /// <summary>
    /// Route a message to the appropriate agent
    /// </summary>
    public async Task<MessageResult> RouteMessageAsync(
        AgentMessage message,
        CancellationToken ct = default)
    {
        var context = new RoutingContext
        {
            Message = message,
            AvailableAgents = _agents.Where(a => a.Status == AgentStatus.Available).ToList()
        };
        
        // Apply routing rules
        var result = _routingEngine.Execute(context);
        
        if (string.IsNullOrEmpty(context.TargetAgentId))
        {
            // No rule matched, try default routing
            context.TargetAgentId = FindDefaultAgent(message)?.Id;
        }
        
        if (string.IsNullOrEmpty(context.TargetAgentId))
        {
            return MessageResult.Fail("No agent available to handle this message");
        }
        
        if (!_agentById.TryGetValue(context.TargetAgentId, out var targetAgent))
        {
            return MessageResult.Fail($"Target agent {context.TargetAgentId} not found");
        }
        
        _logger.LogMessageRouted(message, null!, targetAgent);
        
        // Route to the selected agent
        message.ReceiverId = targetAgent.Id;
        return await targetAgent.ProcessMessageAsync(message, ct);
    }
    
    /// <summary>
    /// Broadcast a message to multiple agents
    /// </summary>
    public async Task<List<MessageResult>> BroadcastMessageAsync(
        AgentMessage message,
        Func<IAgent, bool>? agentFilter = null,
        CancellationToken ct = default)
    {
        var agents = agentFilter != null 
            ? _agents.Where(agentFilter).ToList()
            : _agents;
        
        var tasks = agents.Select(async agent =>
        {
            var copy = CloneMessage(message);
            copy.ReceiverId = agent.Id;
            return await agent.ProcessMessageAsync(copy, ct);
        });
        
        return (await Task.WhenAll(tasks)).ToList();
    }
    
    /// <summary>
    /// Get agent by ID
    /// </summary>
    public IAgent? GetAgent(string agentId)
    {
        return _agentById.TryGetValue(agentId, out var agent) ? agent : null;
    }
    
    /// <summary>
    /// Get all agents
    /// </summary>
    public IReadOnlyList<IAgent> GetAllAgents() => _agents.AsReadOnly();
    
    /// <summary>
    /// Get agents by capability
    /// </summary>
    public List<IAgent> GetAgentsByCapability(string skill)
    {
        return _agents
            .Where(a => a.Capabilities.HasSkill(skill))
            .ToList();
    }
    
    /// <summary>
    /// Get performance metrics for routing
    /// </summary>
    public Dictionary<string, RulePerformanceMetrics> GetRoutingMetrics()
    {
        return _routingEngine.GetAllMetrics();
    }
    
    private IAgent? FindDefaultAgent(AgentMessage message)
    {
        // Try to find an agent that can handle this message
        return _agents
            .Where(a => a.CanHandle(message))
            .OrderBy(a => a.Status == AgentStatus.Busy ? 1 : 0) // Prefer available
            .FirstOrDefault();
    }
    
    private AgentMessage CloneMessage(AgentMessage message)
    {
        return new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = message.SenderId,
            Subject = message.Subject,
            Content = message.Content,
            Category = message.Category,
            Priority = message.Priority,
            Metadata = new Dictionary<string, object>(message.Metadata),
            ConversationId = message.ConversationId
        };
    }
}

/// <summary>
/// Context passed to routing rules for evaluation
/// </summary>
public class RoutingContext
{
    public AgentMessage Message { get; set; } = null!;
    public List<IAgent> AvailableAgents { get; set; } = new();
    public string? TargetAgentId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    // Convenience properties for rules
    public string Subject => Message.Subject;
    public string Content => Message.Content;
    public string Category => Message.Category;
    public MessagePriority Priority => Message.Priority;
    public bool IsUrgent => Message.Priority == MessagePriority.Urgent;
    public bool IsHighPriority => Message.Priority >= MessagePriority.High;
    
    public int AvailableAgentCount => AvailableAgents.Count;
    
    public bool HasAvailableAgentWithSkill(string skill)
    {
        return AvailableAgents.Any(a => a.Capabilities.HasSkill(skill));
    }
    
    public bool CategoryIs(string category)
    {
        return Category.Equals(category, StringComparison.OrdinalIgnoreCase);
    }
    
    public bool SubjectContains(string text)
    {
        return Subject.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
    
    public bool ContentContains(string text)
    {
        return Content.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Fluent builder for routing rules
/// </summary>
public class RoutingRuleBuilder
{
    private readonly AgentRouter _router;
    private string _ruleId = Guid.NewGuid().ToString();
    private string _ruleName = "Unnamed Rule";
    private int _priority = 0;
    private Expression<Func<RoutingContext, bool>>? _condition;
    private string? _targetAgentId;
    
    public RoutingRuleBuilder(AgentRouter router)
    {
        _router = router;
    }
    
    public RoutingRuleBuilder WithId(string id)
    {
        _ruleId = id;
        return this;
    }
    
    public RoutingRuleBuilder WithName(string name)
    {
        _ruleName = name;
        return this;
    }
    
    public RoutingRuleBuilder WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }
    
    public RoutingRuleBuilder When(Expression<Func<RoutingContext, bool>> condition)
    {
        _condition = condition;
        return this;
    }
    
    public RoutingRuleBuilder RouteToAgent(string agentId)
    {
        _targetAgentId = agentId;
        return this;
    }
    
    public void Build()
    {
        if (_condition == null)
            throw new InvalidOperationException("Condition is required");
        
        if (string.IsNullOrEmpty(_targetAgentId))
            throw new InvalidOperationException("Target agent ID is required");
        
        _router.AddRoutingRule(_ruleId, _ruleName, _condition, _targetAgentId, _priority);
    }
}
