using AgentRouting.Core;
using AgentRouting.Agents;
using AgentRouting.Middleware;

namespace AgentRouting;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║      Agent-to-Agent Communication & Routing System         ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        await Demo1_BasicRouting();
        await Demo2_PriorityBasedRouting();
        await Demo3_TriageAndForwarding();
        await Demo4_BroadcastMessages();
        await Demo5_MultiAgentCollaboration();
        await Demo6_Analytics();

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("All demos complete!");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static async Task Demo1_BasicRouting()
    {
        Console.WriteLine("═══ Demo 1: Basic Message Routing ═══");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        // Create agents
        var customerService = new CustomerServiceAgent("cs-001", "Customer Service", logger);
        var techSupport = new TechnicalSupportAgent("tech-001", "Tech Support", logger);
        var billing = new BillingAgent("billing-001", "Billing", logger);

        router.RegisterAgent(customerService);
        router.RegisterAgent(techSupport);
        router.RegisterAgent(billing);

        // Set up routing rules
        router.AddRoutingRule(
            "ROUTE_TECH",
            "Route Technical Issues",
            ctx => ctx.Category == "TechnicalSupport" || ctx.SubjectContains("bug"),
            "tech-001",
            priority: 100
        );

        router.AddRoutingRule(
            "ROUTE_BILLING",
            "Route Billing Issues",
            ctx => ctx.Category == "Billing" || ctx.SubjectContains("payment"),
            "billing-001",
            priority: 100
        );

        router.AddRoutingRule(
            "ROUTE_DEFAULT",
            "Route to Customer Service",
            ctx => true,  // Default route
            "cs-001",
            priority: 1
        );

        // Send various messages
        var messages = new[]
        {
            new AgentMessage
            {
                SenderId = "customer-1",
                Subject = "Login issue",
                Content = "I can't log in to my account",
                Category = "TechnicalSupport"
            },
            new AgentMessage
            {
                SenderId = "customer-2",
                Subject = "Refund request",
                Content = "I need a refund for my recent purchase",
                Category = "Billing"
            },
            new AgentMessage
            {
                SenderId = "customer-3",
                Subject = "General inquiry",
                Content = "What are your business hours?",
                Category = "CustomerService"
            }
        };

        foreach (var message in messages)
        {
            await router.RouteMessageAsync(message);
            await Task.Delay(500);
        }

        Console.WriteLine();
    }

    static async Task Demo2_PriorityBasedRouting()
    {
        Console.WriteLine("═══ Demo 2: Priority-Based Routing ═══");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var customerService = new CustomerServiceAgent("cs-001", "Customer Service", logger);
        var supervisor = new SupervisorAgent("supervisor-001", "Supervisor", logger);

        router.RegisterAgent(customerService);
        router.RegisterAgent(supervisor);

        // High-priority/urgent messages go to supervisor
        router.AddRoutingRule(
            "ROUTE_URGENT",
            "Route Urgent to Supervisor",
            ctx => ctx.IsUrgent,
            "supervisor-001",
            priority: 200
        );

        router.AddRoutingRule(
            "ROUTE_COMPLAINT",
            "Route Complaints to Supervisor",
            ctx => ctx.SubjectContains("complaint") || ctx.ContentContains("complaint"),
            "supervisor-001",
            priority: 150
        );

        router.AddRoutingRule(
            "ROUTE_NORMAL",
            "Route Normal to CS",
            ctx => true,
            "cs-001",
            priority: 1
        );

        var messages = new[]
        {
            new AgentMessage
            {
                SenderId = "customer-1",
                Subject = "URGENT: System down",
                Content = "Our entire system is down!",
                Priority = MessagePriority.Urgent
            },
            new AgentMessage
            {
                SenderId = "customer-2",
                Subject = "Complaint about service",
                Content = "I am very dissatisfied with the service I received",
                Priority = MessagePriority.High
            },
            new AgentMessage
            {
                SenderId = "customer-3",
                Subject = "Question about product",
                Content = "What colors does this product come in?",
                Priority = MessagePriority.Normal
            }
        };

        foreach (var message in messages)
        {
            await router.RouteMessageAsync(message);
            await Task.Delay(500);
        }

        Console.WriteLine();
    }

    static async Task Demo3_TriageAndForwarding()
    {
        Console.WriteLine("═══ Demo 3: Triage Agent & Message Forwarding ═══");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        // Create all agents
        var triage = new TriageAgent("triage-001", "Triage", logger, router);
        var customerService = new CustomerServiceAgent("cs-001", "Customer Service", logger);
        var techSupport = new TechnicalSupportAgent("tech-001", "Tech Support", logger);
        var billing = new BillingAgent("billing-001", "Billing", logger);

        router.RegisterAgent(triage);
        router.RegisterAgent(customerService);
        router.RegisterAgent(techSupport);
        router.RegisterAgent(billing);

        // All messages first go to triage
        router.AddRoutingRule(
            "ROUTE_TO_TRIAGE",
            "Route Everything to Triage",
            ctx => string.IsNullOrEmpty(ctx.Category),  // Unclassified messages
            "triage-001",
            priority: 100
        );

        // Triage will then forward to appropriate agents
        var messages = new[]
        {
            new AgentMessage
            {
                SenderId = "customer-1",
                Subject = "Need help",
                Content = "My app keeps crashing when I try to upload files"
            },
            new AgentMessage
            {
                SenderId = "customer-2",
                Subject = "Question",
                Content = "I was charged twice for my last order"
            },
            new AgentMessage
            {
                SenderId = "customer-3",
                Subject = "Hello",
                Content = "Do you ship internationally?"
            }
        };

        foreach (var message in messages)
        {
            await router.RouteMessageAsync(message);
            await Task.Delay(800);
        }

        Console.WriteLine();
    }

    static async Task Demo4_BroadcastMessages()
    {
        Console.WriteLine("═══ Demo 4: Broadcasting Messages ═══");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var agent1 = new CustomerServiceAgent("cs-001", "CS Team 1", logger);
        var agent2 = new CustomerServiceAgent("cs-002", "CS Team 2", logger);
        var agent3 = new CustomerServiceAgent("cs-003", "CS Team 3", logger);

        router.RegisterAgent(agent1);
        router.RegisterAgent(agent2);
        router.RegisterAgent(agent3);

        // Broadcast to all customer service agents
        var announcement = new AgentMessage
        {
            SenderId = "management",
            Subject = "System Maintenance Notice",
            Content = "System maintenance scheduled for tonight at 10 PM. Please inform customers of possible downtime.",
            Priority = MessagePriority.High
        };

        Console.WriteLine("Broadcasting to all Customer Service agents...");
        await router.BroadcastMessageAsync(
            announcement,
            agent => agent.Capabilities.SupportsCategory("CustomerService")
        );

        await Task.Delay(500);
        Console.WriteLine();
    }

    static async Task Demo5_MultiAgentCollaboration()
    {
        Console.WriteLine("═══ Demo 5: Multi-Agent Collaboration ═══");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        var customerService = new CustomerServiceAgent("cs-001", "Customer Service", logger);
        var techSupport = new TechnicalSupportAgent("tech-001", "Tech Support", logger);
        var supervisor = new SupervisorAgent("supervisor-001", "Supervisor", logger);

        router.RegisterAgent(customerService);
        router.RegisterAgent(techSupport);
        router.RegisterAgent(supervisor);

        // Complex issue that requires multiple agents
        var complexIssue = new AgentMessage
        {
            SenderId = "customer-vip",
            Subject = "Critical billing and technical issue",
            Content = "I was charged for a premium feature but the feature isn't working. This is urgent as I have a presentation tomorrow.",
            Priority = MessagePriority.Urgent,
            Category = "CustomerService"
        };

        Console.WriteLine("Routing complex issue...");
        
        // First, route to customer service
        router.AddRoutingRule("ROUTE_CS", "Route to CS", ctx => true, "cs-001", 1);
        await router.RouteMessageAsync(complexIssue);
        
        await Task.Delay(500);
        
        // Customer service recognizes this needs escalation
        var escalated = new AgentMessage
        {
            SenderId = "cs-001",
            ReceiverId = "supervisor-001",
            Subject = "Escalated: " + complexIssue.Subject,
            Content = "VIP customer with both billing and technical issues. Needs immediate attention.",
            Priority = MessagePriority.Urgent,
            ConversationId = complexIssue.Id
        };

        await router.GetAgent("supervisor-001")!.ProcessMessageAsync(escalated);

        Console.WriteLine();
    }

    static async Task Demo6_Analytics()
    {
        Console.WriteLine("═══ Demo 6: Analytics & Reporting ═══");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        // Add analytics middleware - automatically tracks all messages
        var analytics = new AnalyticsMiddleware();
        router.UseMiddleware(analytics);

        var customerService = new CustomerServiceAgent("cs-001", "Customer Service", logger);
        var techSupport = new TechnicalSupportAgent("tech-001", "Tech Support", logger);
        var billing = new BillingAgent("billing-001", "Billing", logger);

        router.RegisterAgent(customerService);
        router.RegisterAgent(techSupport);
        router.RegisterAgent(billing);

        // Route messages - analytics middleware captures automatically
        router.AddRoutingRule("TECH", "Tech", ctx => ctx.Category == "TechnicalSupport", "tech-001", 100);
        router.AddRoutingRule("BILLING", "Billing", ctx => ctx.Category == "Billing", "billing-001", 100);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001", 1);

        // Simulate a day's worth of messages
        var random = new Random();
        var categories = new[] { "TechnicalSupport", "Billing", "CustomerService" };

        Console.WriteLine("Processing 20 messages...");

        for (int i = 0; i < 20; i++)
        {
            var message = new AgentMessage
            {
                SenderId = $"customer-{i}",
                Subject = $"Request {i + 1}",
                Content = "Sample message content",
                Category = categories[random.Next(categories.Length)]
            };

            await router.RouteMessageAsync(message);
            // No manual analytics call needed - middleware handles it!
        }

        await Task.Delay(500);

        Console.WriteLine();
        Console.WriteLine(analytics.GenerateReport());
        Console.WriteLine();

        // Show routing performance
        Console.WriteLine("Routing Rule Performance:");
        var metrics = router.GetRoutingMetrics();
        foreach (var (ruleId, metric) in metrics.OrderByDescending(m => m.Value.ExecutionCount))
        {
            Console.WriteLine($"  {ruleId}: {metric.ExecutionCount} executions, " +
                            $"avg {metric.AverageExecutionTime.TotalMicroseconds:F2}μs");
        }

        Console.WriteLine();
    }
}
