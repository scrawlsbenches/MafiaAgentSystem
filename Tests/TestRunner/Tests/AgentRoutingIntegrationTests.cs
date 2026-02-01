using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;

namespace TestRunner.Tests;

/// <summary>
/// Integration tests for the agent routing system.
/// Tests message routing, agent selection, and middleware integration.
/// </summary>
public class AgentRoutingIntegrationTests
{
    // ==================== Test Agents ====================

    private class TestAgent : AgentBase
    {
        private readonly Func<AgentMessage, MessageResult>? _handler;
        public List<AgentMessage> ReceivedMessages { get; } = new();

        public TestAgent(string id, string name, Func<AgentMessage, MessageResult>? handler = null)
            : base(id, name, new ConsoleAgentLogger())
        {
            _handler = handler;
        }

        protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
        {
            ReceivedMessages.Add(message);
            return Task.FromResult(_handler?.Invoke(message) ?? MessageResult.Ok($"Handled by {Name}"));
        }
    }

    private class CategoryAgent : AgentBase
    {
        public CategoryAgent(string id, string name, params string[] categories)
            : base(id, name, new ConsoleAgentLogger())
        {
            foreach (var cat in categories)
            {
                Capabilities.SupportedCategories.Add(cat);
            }
        }

        protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
        {
            return Task.FromResult(MessageResult.Ok($"Handled {message.Category} by {Name}"));
        }
    }

    private AgentMessage CreateMessage(string category = "Test", MessagePriority priority = MessagePriority.Normal)
    {
        return new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = "test-sender",
            Subject = "Test Message",
            Content = "Test content",
            Category = category,
            Priority = priority
        };
    }

    // ==================== Basic Routing Tests ====================

    [Test]
    public async Task Router_RegistersAndRoutes_ToCorrectAgent()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouter(logger);

        var techAgent = new TestAgent("tech-001", "Tech Support");
        var billingAgent = new TestAgent("billing-001", "Billing");

        router.RegisterAgent(techAgent);
        router.RegisterAgent(billingAgent);

        router.AddRoutingRule(
            "ROUTE_TECH",
            "Route to Tech",
            ctx => ctx.Category == "Technical",
            "tech-001");

        router.AddRoutingRule(
            "ROUTE_BILLING",
            "Route to Billing",
            ctx => ctx.Category == "Billing",
            "billing-001");

        var techMessage = CreateMessage("Technical");
        var result = await router.RouteMessageAsync(techMessage);

        Assert.True(result.Success);
        Assert.Equal(1, techAgent.ReceivedMessages.Count);
        Assert.Equal(0, billingAgent.ReceivedMessages.Count);
    }

    [Test]
    public async Task Router_PriorityRouting_HigherPriorityRuleWins()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouter(logger);

        var vipAgent = new TestAgent("vip-001", "VIP Handler");
        var normalAgent = new TestAgent("normal-001", "Normal Handler");

        router.RegisterAgent(vipAgent);
        router.RegisterAgent(normalAgent);

        // Lower priority rule for all messages
        router.AddRoutingRule(
            "DEFAULT",
            "Default Route",
            ctx => true,
            "normal-001",
            priority: 10);

        // Higher priority rule for urgent messages
        router.AddRoutingRule(
            "VIP",
            "VIP Route",
            ctx => ctx.Priority == MessagePriority.Urgent,
            "vip-001",
            priority: 100);

        var urgentMessage = CreateMessage(priority: MessagePriority.Urgent);
        var result = await router.RouteMessageAsync(urgentMessage);

        Assert.True(result.Success);
        Assert.Equal(1, vipAgent.ReceivedMessages.Count);
        Assert.Equal(0, normalAgent.ReceivedMessages.Count);
    }

    [Test]
    public async Task Router_NoMatchingRule_UsesDefaultAgent()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouter(logger);

        var defaultAgent = new TestAgent("default-001", "Default Agent");
        var specificAgent = new TestAgent("specific-001", "Specific Agent");

        router.RegisterAgent(defaultAgent);
        router.RegisterAgent(specificAgent);

        // Only route "Special" category to specific agent
        router.AddRoutingRule(
            "SPECIAL",
            "Special Route",
            ctx => ctx.Category == "Special",
            "specific-001");

        // Send a message with different category (no matching rule)
        var message = CreateMessage("Other");

        // When no rule matches and no default is configured, routing should fail
        var result = await router.RouteMessageAsync(message);

        // No rule matched, no default - should not reach specific agent
        Assert.Equal(0, specificAgent.ReceivedMessages.Count);
    }

    [Test]
    public async Task Router_Broadcast_SendsToMultipleAgents()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouter(logger);

        var agent1 = new TestAgent("agent-001", "Agent 1");
        var agent2 = new TestAgent("agent-002", "Agent 2");
        var agent3 = new TestAgent("agent-003", "Agent 3");

        router.RegisterAgent(agent1);
        router.RegisterAgent(agent2);
        router.RegisterAgent(agent3);

        var message = CreateMessage();
        var results = await router.BroadcastMessageAsync(message);

        Assert.Equal(3, results.Count);
        Assert.Equal(1, agent1.ReceivedMessages.Count);
        Assert.Equal(1, agent2.ReceivedMessages.Count);
        Assert.Equal(1, agent3.ReceivedMessages.Count);
    }

    [Test]
    public async Task Router_Broadcast_WithFilter_SendsToMatchingAgents()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouter(logger);

        var techAgent = new CategoryAgent("tech-001", "Tech", "Technical");
        var billingAgent = new CategoryAgent("billing-001", "Billing", "Billing");
        var bothAgent = new CategoryAgent("both-001", "Both", "Technical", "Billing");

        router.RegisterAgent(techAgent);
        router.RegisterAgent(billingAgent);
        router.RegisterAgent(bothAgent);

        var message = CreateMessage("Technical");

        // Broadcast only to agents that support "Technical" category
        var results = await router.BroadcastMessageAsync(
            message,
            agent => agent.Capabilities.SupportedCategories.Contains("Technical"));

        Assert.Equal(2, results.Count); // techAgent and bothAgent
    }

    // ==================== Agent Selection Tests ====================

    [Test]
    public void Router_GetAgentById_ReturnsCorrectAgent()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouter(logger);

        var agent1 = new TestAgent("agent-001", "Agent 1");
        var agent2 = new TestAgent("agent-002", "Agent 2");

        router.RegisterAgent(agent1);
        router.RegisterAgent(agent2);

        var found = router.GetAgent("agent-001");

        Assert.NotNull(found);
        Assert.Equal("agent-001", found!.Id);
        Assert.Equal("Agent 1", found.Name);
    }

    [Test]
    public void Router_GetAgentById_NotFound_ReturnsNull()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouter(logger);

        var agent = new TestAgent("agent-001", "Agent 1");
        router.RegisterAgent(agent);

        var notFound = router.GetAgent("nonexistent");

        Assert.Null(notFound);
    }

    [Test]
    public void Router_GetAllAgents_ReturnsAllRegistered()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouter(logger);

        router.RegisterAgent(new TestAgent("agent-001", "Agent 1"));
        router.RegisterAgent(new TestAgent("agent-002", "Agent 2"));
        router.RegisterAgent(new TestAgent("agent-003", "Agent 3"));

        var agents = router.GetAllAgents().ToList();

        Assert.Equal(3, agents.Count);
    }

    // ==================== Middleware Integration Tests ====================

    [Test]
    public async Task RouterWithMiddleware_ExecutesMiddlewarePipeline()
    {
        var logger = new ConsoleAgentLogger();
        var router = new MiddlewareAgentRouter(logger);

        var agent = new TestAgent("agent-001", "Agent 1");
        router.RegisterAgent(agent);

        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "agent-001");

        // Add middleware
        var middlewareExecuted = false;
        router.UseMiddleware(new CallbackMiddleware(() => middlewareExecuted = true));

        var message = CreateMessage();
        var result = await router.RouteMessageAsync(message);

        Assert.True(result.Success);
        Assert.True(middlewareExecuted);
    }

    [Test]
    public async Task RouterWithMiddleware_ValidationMiddleware_RejectsInvalidMessage()
    {
        var logger = new ConsoleAgentLogger();
        var router = new MiddlewareAgentRouter(logger);

        var agent = new TestAgent("agent-001", "Agent 1");
        router.RegisterAgent(agent);

        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "agent-001");

        // Add validation middleware
        router.UseMiddleware(new ValidationMiddleware());

        // Create invalid message (missing sender)
        var message = new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = "", // Invalid - empty sender
            Subject = "Test",
            Content = "Content"
        };

        var result = await router.RouteMessageAsync(message);

        Assert.False(result.Success);
        Assert.Equal(0, agent.ReceivedMessages.Count);
    }

    [Test]
    public async Task RouterWithMiddleware_TimingMiddleware_AddsProcessingTime()
    {
        var logger = new ConsoleAgentLogger();
        var router = new MiddlewareAgentRouter(logger);

        var agent = new TestAgent("agent-001", "Agent 1");
        router.RegisterAgent(agent);

        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "agent-001");

        // Add timing middleware
        router.UseMiddleware(new TimingMiddleware());

        var message = CreateMessage();
        var result = await router.RouteMessageAsync(message);

        Assert.True(result.Success);
        Assert.True(result.Data.ContainsKey("ProcessingTimeMs"));
    }

    [Test]
    public async Task RouterWithMiddleware_MultipleMiddleware_ExecuteInOrder()
    {
        var logger = new ConsoleAgentLogger();
        var router = new MiddlewareAgentRouter(logger);

        var agent = new TestAgent("agent-001", "Agent 1");
        router.RegisterAgent(agent);

        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "agent-001");

        var executionOrder = new List<int>();

        router.UseMiddleware(new OrderTrackingMiddleware(1, executionOrder));
        router.UseMiddleware(new OrderTrackingMiddleware(2, executionOrder));
        router.UseMiddleware(new OrderTrackingMiddleware(3, executionOrder));

        var message = CreateMessage();
        await router.RouteMessageAsync(message);

        Assert.Equal(6, executionOrder.Count); // 3 before + 3 after
        Assert.Equal(1, executionOrder[0]);
        Assert.Equal(2, executionOrder[1]);
        Assert.Equal(3, executionOrder[2]);
    }

    // ==================== Helper Classes ====================

    private class CallbackMiddleware : MiddlewareBase
    {
        private readonly Action _callback;

        public CallbackMiddleware(Action callback)
        {
            _callback = callback;
        }

        public override async Task<MessageResult> InvokeAsync(
            AgentMessage message, MessageDelegate next, CancellationToken ct)
        {
            _callback();
            return await next(message, ct);
        }
    }

    private class OrderTrackingMiddleware : MiddlewareBase
    {
        private readonly int _id;
        private readonly List<int> _log;

        public OrderTrackingMiddleware(int id, List<int> log)
        {
            _id = id;
            _log = log;
        }

        public override async Task<MessageResult> InvokeAsync(
            AgentMessage message, MessageDelegate next, CancellationToken ct)
        {
            _log.Add(_id);
            var result = await next(message, ct);
            _log.Add(-_id);
            return result;
        }
    }
}
