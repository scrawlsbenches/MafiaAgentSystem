using AgentRouting.Core;
using AgentRouting.Agents;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;

namespace AgentRouting.AdvancedMiddlewareDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║    AgentRouting - Advanced Middleware Capabilities Demo       ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Demonstrating enterprise-grade middleware patterns:");
        Console.WriteLine("  • Distributed Tracing");
        Console.WriteLine("  • Semantic Routing");
        Console.WriteLine("  • Message Transformation");
        Console.WriteLine("  • A/B Testing");
        Console.WriteLine("  • Feature Flags");
        Console.WriteLine("  • Agent Health Monitoring");
        Console.WriteLine("  • Workflow Orchestration");
        Console.WriteLine();

        await Demo1_DistributedTracing();
        await Demo2_SemanticRouting();
        await Demo3_MessageTransformation();
        await Demo4_ABTesting();
        await Demo5_FeatureFlags();
        await Demo6_AgentHealthChecking();
        await Demo7_WorkflowOrchestration();
        await Demo8_CompleteMiddlewareStack();

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("✓ All advanced middleware demos complete!");
        Console.WriteLine();
        Console.WriteLine("These middleware patterns enable:");
        Console.WriteLine("  ✓ Production observability (tracing, metrics)");
        Console.WriteLine("  ✓ Intelligent routing (semantic, health-based)");
        Console.WriteLine("  ✓ Experimentation (A/B testing, feature flags)");
        Console.WriteLine("  ✓ Complex workflows (multi-stage orchestration)");
        Console.WriteLine("  ✓ Enterprise resilience (health checks, failover)");
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static async Task Demo1_DistributedTracing()
    {
        Console.WriteLine("═══ Demo 1: Distributed Tracing ═══");
        Console.WriteLine();
        Console.WriteLine("Track messages across agent boundaries with OpenTelemetry-style tracing.");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        
        var tracingMiddleware = new DistributedTracingMiddleware("CustomerServiceRouter");
        router.UseMiddleware(tracingMiddleware);

        var csAgent = new CustomerServiceAgent("cs-001", "CS", logger);
        var techAgent = new TechnicalSupportAgent("tech-001", "Tech", logger);
        
        router.RegisterAgent(csAgent);
        router.RegisterAgent(techAgent);
        router.AddRoutingRule("TECH", "Tech", ctx => ctx.Category == "TechnicalSupport", "tech-001", 100);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001", 1);

        // Send messages that create a trace
        var messages = new[]
        {
            new AgentMessage
            {
                SenderId = "customer-1",
                Subject = "Need help with login",
                Content = "I can't log in to my account",
                Category = "TechnicalSupport"
            },
            new AgentMessage
            {
                SenderId = "customer-1",
                Subject = "Follow-up",
                Content = "Still having issues",
                Category = "TechnicalSupport",
                Metadata = new Dictionary<string, object>()
            }
        };

        foreach (var message in messages)
        {
            await router.RouteMessageAsync(message);
            await Task.Delay(300);
        }

        Console.WriteLine();
        Console.WriteLine(tracingMiddleware.ExportJaegerFormat());
        Console.WriteLine();
    }

    static async Task Demo2_SemanticRouting()
    {
        Console.WriteLine("═══ Demo 2: Semantic Routing ═══");
        Console.WriteLine();
        Console.WriteLine("Automatically detect intent and sentiment, adjust routing accordingly.");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        
        router.UseMiddleware(new SemanticRoutingMiddleware());

        var csAgent = new CustomerServiceAgent("cs-001", "CS", logger);
        var supervisor = new SupervisorAgent("supervisor-001", "Supervisor", logger);
        
        router.RegisterAgent(csAgent);
        router.RegisterAgent(supervisor);
        router.AddRoutingRule("HIGH_PRIORITY", "High Priority", ctx => ctx.IsHighPriority, "supervisor-001", 100);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001", 1);

        var messages = new[]
        {
            new AgentMessage
            {
                SenderId = "happy-customer",
                Subject = "Thanks!",
                Content = "Your service is wonderful, thank you so much!",
                Priority = MessagePriority.Normal
            },
            new AgentMessage
            {
                SenderId = "angry-customer",
                Subject = "Complaint",
                Content = "I am furious! This is the worst service I've ever experienced!",
                Priority = MessagePriority.Normal  // Will be boosted
            },
            new AgentMessage
            {
                SenderId = "tech-user",
                Subject = "Issue",
                Content = "My app keeps crashing with an error message",
                Category = ""  // Will be auto-categorized
            }
        };

        foreach (var message in messages)
        {
            Console.WriteLine($"\n📧 Processing: {message.Subject}");
            Console.WriteLine($"   Original Priority: {message.Priority}");
            
            var result = await router.RouteMessageAsync(message);
            
            if (message.Metadata.ContainsKey("DetectedIntents"))
            {
                Console.WriteLine($"   Detected Intents: {message.Metadata["DetectedIntents"]}");
            }
            Console.WriteLine($"   Final Priority: {message.Priority}");
            
            await Task.Delay(400);
        }

        Console.WriteLine();
    }

    static async Task Demo3_MessageTransformation()
    {
        Console.WriteLine("═══ Demo 3: Message Transformation ═══");
        Console.WriteLine();
        Console.WriteLine("Normalize, sanitize, and enrich messages with extracted data.");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        
        router.UseMiddleware(new MessageTransformationMiddleware());

        var csAgent = new CustomerServiceAgent("cs-001", "CS", logger);
        router.RegisterAgent(csAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001");

        var message = new AgentMessage
        {
            SenderId = "customer-1",
            Subject = "   Contact   Information   ",  // Extra whitespace
            Content = "Please contact me at john.doe@example.com or call me at 555-123-4567. ¡Hola! Mi número es importante.",
            Category = "CustomerService"
        };

        Console.WriteLine("Original message:");
        Console.WriteLine($"  Subject: '{message.Subject}'");
        Console.WriteLine($"  Content: '{message.Content}'");
        Console.WriteLine();

        await router.RouteMessageAsync(message);

        Console.WriteLine("\nTransformed message:");
        Console.WriteLine($"  Subject: '{message.Subject}' (whitespace normalized)");
        Console.WriteLine($"  Detected Language: {message.Metadata["DetectedLanguage"]}");
        Console.WriteLine($"  Contains Email: {message.Metadata.GetValueOrDefault("ContainsEmail", false)}");
        if (message.Metadata.ContainsKey("EmailCount"))
            Console.WriteLine($"  Email Count: {message.Metadata["EmailCount"]}");
        Console.WriteLine($"  Contains Phone: {message.Metadata.GetValueOrDefault("ContainsPhone", false)}");
        if (message.Metadata.ContainsKey("PhoneCount"))
            Console.WriteLine($"  Phone Count: {message.Metadata["PhoneCount"]}");

        Console.WriteLine();
    }

    static async Task Demo4_ABTesting()
    {
        Console.WriteLine("═══ Demo 4: A/B Testing ═══");
        Console.WriteLine();
        Console.WriteLine("Experiment with different routing strategies.");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        
        var abTestMiddleware = new ABTestingMiddleware();
        abTestMiddleware.RegisterExperiment("RoutingStrategy", 0.5, "VariantA_Fast", "VariantB_Thorough");
        abTestMiddleware.RegisterExperiment("ResponseStyle", 0.7, "VariantA_Formal", "VariantB_Casual");
        
        router.UseMiddleware(abTestMiddleware);

        var csAgent = new CustomerServiceAgent("cs-001", "CS", logger);
        router.RegisterAgent(csAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001");

        Console.WriteLine("Processing 5 messages to see variant distribution:");
        Console.WriteLine();

        for (int i = 1; i <= 5; i++)
        {
            var message = new AgentMessage
            {
                SenderId = $"customer-{i}",
                Subject = $"Message {i}",
                Content = "Test message",
                Category = "CustomerService"
            };

            await router.RouteMessageAsync(message);
            await Task.Delay(200);
        }

        Console.WriteLine();
    }

    static async Task Demo5_FeatureFlags()
    {
        Console.WriteLine("═══ Demo 5: Feature Flags ═══");
        Console.WriteLine();
        Console.WriteLine("Conditionally enable features based on criteria.");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        
        var featureFlagsMiddleware = new FeatureFlagsMiddleware();
        
        // Enable AI suggestions for all messages
        featureFlagsMiddleware.RegisterFlag("AI_Suggestions", enabled: true);
        
        // Enable premium features only for VIP customers
        featureFlagsMiddleware.RegisterFlag(
            "Premium_Features",
            enabled: true,
            condition: msg => msg.SenderId.Contains("vip")
        );
        
        // Enable beta features for specific users
        featureFlagsMiddleware.RegisterFlag(
            "Beta_Features",
            enabled: true,
            condition: msg => msg.SenderId.StartsWith("beta-")
        );
        
        router.UseMiddleware(featureFlagsMiddleware);

        var csAgent = new CustomerServiceAgent("cs-001", "CS", logger);
        router.RegisterAgent(csAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001");

        var messages = new[]
        {
            new AgentMessage { SenderId = "regular-user", Subject = "Regular user" },
            new AgentMessage { SenderId = "vip-customer", Subject = "VIP customer" },
            new AgentMessage { SenderId = "beta-tester-1", Subject = "Beta tester" }
        };

        foreach (var message in messages)
        {
            Console.WriteLine($"\n👤 {message.Subject}:");
            await router.RouteMessageAsync(message);
            await Task.Delay(200);
        }

        Console.WriteLine();
    }

    static async Task Demo6_AgentHealthChecking()
    {
        Console.WriteLine("═══ Demo 6: Agent Health Monitoring ═══");
        Console.WriteLine();
        Console.WriteLine("Detect unhealthy agents and reroute traffic automatically.");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        
        var healthCheckMiddleware = new AgentHealthCheckMiddleware(TimeSpan.FromSeconds(2));
        
        // Simulate health checks
        var agent1Healthy = true;
        var agent2Healthy = true;
        
        healthCheckMiddleware.RegisterAgent("cs-001", () => Task.FromResult(agent1Healthy));
        healthCheckMiddleware.RegisterAgent("cs-002", () => Task.FromResult(agent2Healthy));
        
        router.UseMiddleware(healthCheckMiddleware);

        var cs1 = new CustomerServiceAgent("cs-001", "CS-1", logger);
        var cs2 = new CustomerServiceAgent("cs-002", "CS-2", logger);
        
        router.RegisterAgent(cs1);
        router.RegisterAgent(cs2);

        Console.WriteLine("Scenario: cs-001 becomes unhealthy, traffic reroutes to cs-002");
        Console.WriteLine();

        // Send message to healthy agent
        var msg1 = new AgentMessage
        {
            SenderId = "customer-1",
            ReceiverId = "cs-001",
            Subject = "Message 1",
            Content = "Test",
            Category = "CustomerService"
        };
        
        Console.WriteLine("Message 1 → cs-001 (healthy)");
        await router.RouteMessageAsync(msg1);
        
        await Task.Delay(500);
        
        // Mark agent 1 as unhealthy
        agent1Healthy = false;
        await Task.Delay(100);
        
        // Send another message
        var msg2 = new AgentMessage
        {
            SenderId = "customer-2",
            ReceiverId = "cs-001",  // Targeting unhealthy agent
            Subject = "Message 2",
            Content = "Test",
            Category = "CustomerService"
        };
        
        Console.WriteLine("\nMessage 2 → cs-001 (now unhealthy)");
        await router.RouteMessageAsync(msg2);
        
        Console.WriteLine("\nCurrent Health Status:");
        var healthStatus = healthCheckMiddleware.GetHealthStatus();
        foreach (var (agentId, isHealthy) in healthStatus)
        {
            var status = isHealthy ? "✓ Healthy" : "✗ Unhealthy";
            Console.WriteLine($"  {agentId}: {status}");
        }

        Console.WriteLine();
    }

    static async Task Demo7_WorkflowOrchestration()
    {
        Console.WriteLine("═══ Demo 7: Multi-Stage Workflow ═══");
        Console.WriteLine();
        Console.WriteLine("Orchestrate complex multi-agent workflows.");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        
        var workflowMiddleware = new WorkflowOrchestrationMiddleware();
        
        // Define order processing workflow
        workflowMiddleware.RegisterWorkflow(
            "OrderProcessing",
            new WorkflowStage { Name = "Validation", AgentId = "validator" },
            new WorkflowStage { Name = "Inventory Check", AgentId = "inventory" },
            new WorkflowStage { Name = "Payment", AgentId = "payment" },
            new WorkflowStage { Name = "Fulfillment", AgentId = "fulfillment" }
        );
        
        router.UseMiddleware(workflowMiddleware);

        // Create simple agents for demo
        var validators = new SimpleAgent("validator", "Validator", logger);
        router.RegisterAgent(validators);
        router.AddRoutingRule("R1", "R1", ctx => true, "validator");

        var message = new AgentMessage
        {
            SenderId = "api",
            Subject = "New Order #12345",
            Content = "Order details...",
            Metadata = new Dictionary<string, object>
            {
                ["WorkflowId"] = "OrderProcessing",
                ["StageIndex"] = 0
            }
        };

        Console.WriteLine("Starting OrderProcessing workflow...");
        Console.WriteLine();

        await router.RouteMessageAsync(message);

        Console.WriteLine();
    }

    static async Task Demo8_CompleteMiddlewareStack()
    {
        Console.WriteLine("═══ Demo 8: Complete Middleware Stack ═══");
        Console.WriteLine();
        Console.WriteLine("Combining multiple middleware for production-grade system:");
        Console.WriteLine("  1. Distributed Tracing");
        Console.WriteLine("  2. Message Transformation");
        Console.WriteLine("  3. Semantic Routing");
        Console.WriteLine("  4. Feature Flags");
        Console.WriteLine("  5. A/B Testing");
        Console.WriteLine("  6. Rate Limiting (from basic middleware)");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        
        // Build complete stack
        router.UseMiddleware(new DistributedTracingMiddleware("ProductionRouter"));
        router.UseMiddleware(new MessageTransformationMiddleware());
        router.UseMiddleware(new SemanticRoutingMiddleware());
        
        var featureFlags = new FeatureFlagsMiddleware();
        featureFlags.RegisterFlag("Enhanced_Routing", enabled: true);
        router.UseMiddleware(featureFlags);
        
        var abTest = new ABTestingMiddleware();
        abTest.RegisterExperiment("Algorithm", 0.5, "V1", "V2");
        router.UseMiddleware(abTest);

        var csAgent = new CustomerServiceAgent("cs-001", "CS", logger);
        var supervisor = new SupervisorAgent("supervisor-001", "Supervisor", logger);
        
        router.RegisterAgent(csAgent);
        router.RegisterAgent(supervisor);
        router.AddRoutingRule("HIGH", "High", ctx => ctx.IsHighPriority, "supervisor-001", 100);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001", 1);

        var message = new AgentMessage
        {
            SenderId = "customer@example.com",
            Subject = "  URGENT - Account Issue  ",
            Content = "This is urgent! I can't access my account and I'm very frustrated. Please help ASAP!",
            Priority = MessagePriority.Normal
        };

        Console.WriteLine("Processing message through complete middleware stack:");
        Console.WriteLine();
        
        var result = await router.RouteMessageAsync(message);

        Console.WriteLine("\n✓ Message processed through all middleware layers!");
        Console.WriteLine($"  Final Priority: {message.Priority}");
        Console.WriteLine($"  Routed to: {message.ReceiverId}");

        Console.WriteLine();
    }
}

// Simple agent for workflow demo
class SimpleAgent : AgentBase
{
    public SimpleAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
    }

    protected override async Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return MessageResult.Ok($"Processed by {Name}");
    }
}
