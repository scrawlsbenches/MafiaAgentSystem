using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Agents;
using AgentRouting.Middleware;

namespace TestRunner.Tests;

public class AgentRoutingTests
{
    private class TestLogger : IAgentLogger
    {
        public List<string> Logs { get; } = new();

        public void LogMessageReceived(IAgent agent, AgentMessage message)
        {
            Logs.Add($"Received: {agent.Name} - {message.Subject}");
        }

        public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result)
        {
            Logs.Add($"Processed: {agent.Name} - {message.Subject} - {result.Success}");
        }

        public void LogMessageRouted(AgentMessage message, IAgent? fromAgent, IAgent toAgent)
        {
            Logs.Add($"Routed: {message.Subject} to {toAgent.Name}");
        }

        public void LogError(IAgent agent, AgentMessage message, Exception ex)
        {
            Logs.Add($"Error: {agent.Name} - {ex.Message}");
        }
    }

    [Test]
    public async Task AgentRouter_RoutesToCorrectAgent_BasedOnCategory()
    {
        var logger = new TestLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var techAgent = new TechnicalSupportAgent("tech-001", "Tech Support", logger);
        var csAgent = new CustomerServiceAgent("cs-001", "Customer Service", logger);

        router.RegisterAgent(techAgent);
        router.RegisterAgent(csAgent);

        router.AddRoutingRule(
            "ROUTE_TECH",
            "Route Tech",
            ctx => ctx.Category == "TechnicalSupport",
            "tech-001",
            100
        );

        router.AddRoutingRule(
            "ROUTE_CS",
            "Route CS",
            ctx => true,
            "cs-001",
            1
        );

        var message = new AgentMessage
        {
            SenderId = "user-1",
            Subject = "Bug report",
            Content = "Found a bug",
            Category = "TechnicalSupport"
        };

        var result = await router.RouteMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains(logger.Logs, l => l.Contains("Tech Support"));
    }

    [Test]
    public async Task AgentRouter_RoutesByPriority_HighestFirst()
    {
        var logger = new TestLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var agent1 = new CustomerServiceAgent("cs-001", "CS 1", logger);
        var agent2 = new CustomerServiceAgent("cs-002", "CS 2", logger);

        router.RegisterAgent(agent1);
        router.RegisterAgent(agent2);

        router.AddRoutingRule(
            "ROUTE_1",
            "Route 1",
            ctx => true,
            "cs-001",
            priority: 10
        );

        router.AddRoutingRule(
            "ROUTE_2",
            "Route 2",
            ctx => ctx.Priority == MessagePriority.High,
            "cs-002",
            priority: 100
        );

        var message = new AgentMessage
        {
            SenderId = "user-1",
            Subject = "High priority",
            Content = "Important",
            Priority = MessagePriority.High
        };

        var result = await router.RouteMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains(logger.Logs, l => l.Contains("CS 2"));
    }

    [Test]
    public async Task Agent_ProcessesMessage_Successfully()
    {
        var logger = new TestLogger();
        var agent = new CustomerServiceAgent("cs-001", "Customer Service", logger);

        var message = new AgentMessage
        {
            SenderId = "customer-1",
            Subject = "Business hours",
            Content = "What are your hours?",
            Category = "CustomerService"
        };

        var result = await agent.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("hours", result.Response!, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task TechnicalSupportAgent_HandlesLoginIssues()
    {
        var logger = new TestLogger();
        var agent = new TechnicalSupportAgent("tech-001", "Tech Support", logger);

        var message = new AgentMessage
        {
            SenderId = "customer-1",
            Subject = "Can't login",
            Content = "I forgot my password",
            Category = "TechnicalSupport"
        };

        var result = await agent.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("password", result.Response!, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task BillingAgent_HandlesRefundRequests()
    {
        var logger = new TestLogger();
        var agent = new BillingAgent("billing-001", "Billing", logger);

        var message = new AgentMessage
        {
            SenderId = "customer-1",
            Subject = "Refund needed",
            Content = "I need a refund for my purchase",
            Category = "Billing"
        };

        var result = await agent.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("refund", result.Response!, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task AgentRouter_BroadcastsToMultipleAgents()
    {
        var logger = new TestLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var agent1 = new CustomerServiceAgent("cs-001", "CS 1", logger);
        var agent2 = new CustomerServiceAgent("cs-002", "CS 2", logger);
        var agent3 = new TechnicalSupportAgent("tech-001", "Tech", logger);

        router.RegisterAgent(agent1);
        router.RegisterAgent(agent2);
        router.RegisterAgent(agent3);

        var message = new AgentMessage
        {
            SenderId = "admin",
            Subject = "Announcement",
            Content = "System maintenance tonight"
        };

        var results = await router.BroadcastMessageAsync(
            message,
            agent => agent.Capabilities.SupportsCategory("CustomerService")
        );

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Test]
    public async Task Agent_RejectsMessage_WhenAtCapacity()
    {
        var logger = new TestLogger();
        var agent = new CustomerServiceAgent("cs-001", "CS", logger);
        agent.Capabilities.MaxConcurrentMessages = 2;

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 2; i++)
        {
            var msg = new AgentMessage
            {
                SenderId = $"customer-{i}",
                Subject = $"Message {i}",
                Content = "Test",
                Category = "CustomerService"
            };
            tasks.Add(agent.ProcessMessageAsync(msg));
        }

        var extraMessage = new AgentMessage
        {
            SenderId = "customer-extra",
            Subject = "Extra",
            Content = "Test",
            Category = "CustomerService"
        };

        var canHandle = agent.CanHandle(extraMessage);

        Assert.False(canHandle);

        await Task.WhenAll(tasks);
    }

    [Test]
    public async Task TriageAgent_ClassifiesAndForwards()
    {
        var logger = new TestLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var techAgent = new TechnicalSupportAgent("tech-001", "Tech", logger);
        var triageAgent = new TriageAgent("triage-001", "Triage", logger, router);

        router.RegisterAgent(techAgent);
        router.RegisterAgent(triageAgent);

        var message = new AgentMessage
        {
            SenderId = "customer-1",
            Subject = "Problem",
            Content = "I'm getting an error message when I try to login",
            Category = ""
        };

        var result = await triageAgent.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(result.ForwardedMessages);

        var forwarded = result.ForwardedMessages[0];
        Assert.Equal("TechnicalSupport", forwarded.Category);
    }

    [Test]
    public async Task SupervisorAgent_HandlesEscalations()
    {
        var logger = new TestLogger();
        var supervisor = new SupervisorAgent("supervisor-001", "Supervisor", logger);

        var message = new AgentMessage
        {
            SenderId = "cs-agent",
            Subject = "Escalated issue",
            Content = "Customer is very upset and demanding to speak with management",
            Category = "Escalation",
            Priority = MessagePriority.Urgent
        };

        var result = await supervisor.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("supervisor", result.Response!, StringComparison.OrdinalIgnoreCase);
        Assert.Single(supervisor.GetEscalatedIssues());
    }

    [Test]
    public async Task AnalyticsMiddleware_TracksMessageMetrics()
    {
        var analytics = new AnalyticsMiddleware();

        // Create a simple next delegate that just returns success
        MessageDelegate next = (msg, ct) => Task.FromResult(MessageResult.Ok("processed"));

        var messages = new[]
        {
            new AgentMessage { Category = "Billing", ReceiverId = "billing-001" },
            new AgentMessage { Category = "Billing", ReceiverId = "billing-001" },
            new AgentMessage { Category = "TechnicalSupport", ReceiverId = "tech-001" },
        };

        foreach (var msg in messages)
        {
            await analytics.InvokeAsync(msg, next, CancellationToken.None);
        }

        var report = analytics.GenerateReport();

        Assert.Contains("Total Messages Processed: 3", report);
        Assert.Contains("Billing: 2", report);
        Assert.Contains("TechnicalSupport: 1", report);
    }

    [Test]
    public void AgentCapabilities_ChecksSkills_Correctly()
    {
        var capabilities = new AgentCapabilities();
        capabilities.Skills.Add("Programming");
        capabilities.Skills.Add("Debugging");

        Assert.True(capabilities.HasSkill("Programming"));
        Assert.True(capabilities.HasSkill("programming"));
        Assert.False(capabilities.HasSkill("Design"));
    }

    [Test]
    public void RoutingContext_HelperMethods_WorkCorrectly()
    {
        var message = new AgentMessage
        {
            Subject = "Important Question",
            Content = "This message contains the word 'urgent'",
            Category = "Support",
            Priority = MessagePriority.High
        };

        var context = new RoutingContext { Message = message };

        Assert.True(context.IsHighPriority);
        Assert.False(context.IsUrgent);
        Assert.True(context.CategoryIs("support"));
        Assert.True(context.SubjectContains("question"));
        Assert.True(context.ContentContains("urgent"));
    }
}
