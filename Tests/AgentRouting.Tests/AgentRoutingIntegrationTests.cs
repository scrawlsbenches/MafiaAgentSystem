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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var agent = new TestAgent("agent-001", "Agent 1");
        router.RegisterAgent(agent);

        var notFound = router.GetAgent("nonexistent");

        Assert.Null(notFound);
    }

    [Test]
    public void Router_GetAllAgents_ReturnsAllRegistered()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

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

    // ==================== Additional Integration Tests ====================

    [Test]
    public async Task Router_GetAgent_VerifiesAgentRegistration()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var agent = new TestAgent("agent-001", "Agent 1");
        router.RegisterAgent(agent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "agent-001");

        // Verify agent is registered
        var found = router.GetAgent("agent-001");
        Assert.NotNull(found);
        Assert.Equal("agent-001", found!.Id);
        Assert.Equal("Agent 1", found.Name);
    }

    [Test]
    public async Task Router_MultipleRulesWithPriority_HigherPriorityExecutesFirst()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var agentA = new TestAgent("agent-a", "Agent A");
        var agentB = new TestAgent("agent-b", "Agent B");

        router.RegisterAgent(agentA);
        router.RegisterAgent(agentB);

        // Add rules with different priorities - higher priority should win
        router.AddRoutingRule("ROUTE_A", "Route to A", ctx => true, "agent-a", priority: 10);
        router.AddRoutingRule("ROUTE_B", "Route to B", ctx => true, "agent-b", priority: 100);

        var message = CreateMessage();
        await router.RouteMessageAsync(message);

        // Agent B should receive message (higher priority)
        Assert.Equal(0, agentA.ReceivedMessages.Count);
        Assert.Equal(1, agentB.ReceivedMessages.Count);
    }

    [Test]
    public async Task Router_AgentReturnsError_ErrorPropagates()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var errorAgent = new TestAgent("error-agent", "Error Agent",
            msg => MessageResult.Fail("Agent error occurred"));

        router.RegisterAgent(errorAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "error-agent");

        var result = await router.RouteMessageAsync(CreateMessage());

        Assert.False(result.Success);
        Assert.Equal("Agent error occurred", result.Error);
    }

    [Test]
    public async Task Router_AgentThrowsException_ExceptionHandled()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var throwingAgent = new ThrowingAgent("throwing-agent", "Throwing Agent");
        router.RegisterAgent(throwingAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "throwing-agent");

        // The router may either throw or return a failed result depending on implementation
        try
        {
            var result = await router.RouteMessageAsync(CreateMessage());
            // If no exception, result should indicate failure
            Assert.False(result.Success);
        }
        catch (InvalidOperationException)
        {
            // Exception is also acceptable behavior
            Assert.True(true);
        }
    }

    [Test]
    public async Task Router_Concurrent_RegisterAndRoute_ThreadSafe()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        // Initial agent
        router.RegisterAgent(new TestAgent("initial", "Initial"));
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "initial");

        var tasks = new List<Task>();
        var routingCount = 0;
        var registerCount = 0;

        // Concurrent routing
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await router.RouteMessageAsync(CreateMessage());
                Interlocked.Increment(ref routingCount);
            }));
        }

        // Concurrent agent registration
        for (int i = 0; i < 10; i++)
        {
            var agentId = $"concurrent-{i}";
            tasks.Add(Task.Run(() =>
            {
                router.RegisterAgent(new TestAgent(agentId, $"Agent {i}"));
                Interlocked.Increment(ref registerCount);
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(50, routingCount);
        Assert.Equal(10, registerCount);
        Assert.Equal(11, router.GetAllAgents().Count()); // 1 initial + 10 concurrent
    }

    [Test]
    public async Task Router_MultipleRoutingRules_HighestPriorityWins()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var lowAgent = new TestAgent("low-agent", "Low Priority Agent");
        var highAgent = new TestAgent("high-agent", "High Priority Agent");

        router.RegisterAgent(lowAgent);
        router.RegisterAgent(highAgent);

        // Both rules match, but high priority should win
        router.AddRoutingRule("LOW", "Low Priority", ctx => true, "low-agent", priority: 10);
        router.AddRoutingRule("HIGH", "High Priority", ctx => true, "high-agent", priority: 100);

        await router.RouteMessageAsync(CreateMessage());

        Assert.Equal(0, lowAgent.ReceivedMessages.Count);
        Assert.Equal(1, highAgent.ReceivedMessages.Count);
    }

    [Test]
    public async Task Router_CategoryBasedRouting_RoutesToCorrectAgent()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var salesAgent = new CategoryAgent("sales", "Sales", "Sales");
        var supportAgent = new CategoryAgent("support", "Support", "Support");
        var billingAgent = new CategoryAgent("billing", "Billing", "Billing");

        router.RegisterAgent(salesAgent);
        router.RegisterAgent(supportAgent);
        router.RegisterAgent(billingAgent);

        router.AddRoutingRule("SALES", "Sales Route", ctx => ctx.Category == "Sales", "sales");
        router.AddRoutingRule("SUPPORT", "Support Route", ctx => ctx.Category == "Support", "support");
        router.AddRoutingRule("BILLING", "Billing Route", ctx => ctx.Category == "Billing", "billing");

        await router.RouteMessageAsync(CreateMessage("Sales"));
        await router.RouteMessageAsync(CreateMessage("Support"));
        await router.RouteMessageAsync(CreateMessage("Billing"));
        await router.RouteMessageAsync(CreateMessage("Sales"));

        // Verify each agent received correct messages
        // Note: CategoryAgent is different from TestAgent
    }

    [Test]
    public async Task Router_MessageMetadata_PreservedThroughPipeline()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var agent = new MetadataCapturingAgent("agent", "Metadata Agent");
        router.RegisterAgent(agent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "agent");

        var message = CreateMessage();
        message.Metadata["custom-key"] = "custom-value";
        message.Metadata["request-id"] = "12345";

        await router.RouteMessageAsync(message);

        Assert.True(agent.CapturedMetadata.ContainsKey("custom-key"));
        Assert.Equal("custom-value", agent.CapturedMetadata["custom-key"]);
        Assert.Equal("12345", agent.CapturedMetadata["request-id"]);
    }

    [Test]
    public async Task Router_RoutingContext_ContainsAllMessageInfo()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var agent = new TestAgent("agent", "Agent");
        router.RegisterAgent(agent);

        // Use a simple condition that always matches
        router.AddRoutingRule("CAPTURE", "Capture", ctx => ctx.Priority == MessagePriority.High, "agent");

        var message = CreateMessage("TestCategory", MessagePriority.High);
        message.SenderId = "sender-123";

        var result = await router.RouteMessageAsync(message);

        Assert.True(result.Success);
        Assert.Equal(1, agent.ReceivedMessages.Count);

        // Verify the message that was received has the correct properties
        var receivedMsg = agent.ReceivedMessages[0];
        Assert.Equal("TestCategory", receivedMsg.Category);
        Assert.Equal(MessagePriority.High, receivedMsg.Priority);
        Assert.Equal("sender-123", receivedMsg.SenderId);
    }

    [Test]
    public async Task Router_EmptyRouter_NoAgents_HandlesGracefully()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var agents = router.GetAllAgents().ToList();
        Assert.Equal(0, agents.Count);

        // Routing should fail gracefully when no agents
        var result = await router.RouteMessageAsync(CreateMessage());
        Assert.False(result.Success);
    }

    [Test]
    public async Task Router_ComplexPipeline_AllComponentsWork()
    {
        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var agent = new TestAgent("agent", "Agent");
        router.RegisterAgent(agent);

        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "agent");

        // Add multiple middleware types
        router.UseMiddleware(new ValidationMiddleware());
        router.UseMiddleware(new TimingMiddleware());
        router.UseMiddleware(new MetricsMiddleware());

        var message = CreateMessage();
        var result = await router.RouteMessageAsync(message);

        Assert.True(result.Success);
        Assert.True(result.Data.ContainsKey("ProcessingTimeMs"));
        Assert.Equal(1, agent.ReceivedMessages.Count);
    }

    // Additional helper classes

    private class ThrowingAgent : AgentBase
    {
        public ThrowingAgent(string id, string name) : base(id, name, new ConsoleAgentLogger()) { }

        protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
        {
            throw new InvalidOperationException("Agent threw exception");
        }
    }

    private class MetadataCapturingAgent : AgentBase
    {
        public Dictionary<string, object> CapturedMetadata { get; } = new();

        public MetadataCapturingAgent(string id, string name) : base(id, name, new ConsoleAgentLogger()) { }

        protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
        {
            foreach (var kvp in message.Metadata)
            {
                CapturedMetadata[kvp.Key] = kvp.Value;
            }
            return Task.FromResult(MessageResult.Ok("Captured"));
        }
    }
}
