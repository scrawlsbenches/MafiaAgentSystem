using AgentRouting.Core;
using AgentRouting.Agents;
using AgentRouting.Middleware;

namespace AgentRouting.MiddlewareDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Clear();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         AgentRouting - Middleware System Demo               ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Demonstrating middleware patterns for agent message processing");
        Console.WriteLine();

        await Demo1_BasicMiddleware();
        await Demo2_RateLimiting();
        await Demo3_Caching();
        await Demo4_Retry();
        await Demo5_CircuitBreaker();
        await Demo6_CompleteMiddlewarePipeline();
        await Demo7_ConditionalMiddleware();
        await Demo8_Metrics();

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("✓ All middleware demos complete!");
        Console.WriteLine();
        Console.WriteLine("Middleware transforms simple routing into production-ready systems!");
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static async Task Demo1_BasicMiddleware()
    {
        Console.WriteLine("═══ Demo 1: Basic Middleware (Logging & Timing) ═══");
        Console.WriteLine();
        Console.WriteLine("Shows middleware wrapping agent processing.");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterWithMiddleware(logger);

        // Add middleware
        router.UseMiddleware(new LoggingMiddleware(logger));
        router.UseMiddleware(new TimingMiddleware());

        // Register agent
        var csAgent = new CustomerServiceAgent("cs-001", "Customer Service", logger);
        router.RegisterAgent(csAgent);

        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001");

        var message = new AgentMessage
        {
            SenderId = "customer-1",
            Subject = "Business Hours",
            Content = "What are your hours?",
            Category = "CustomerService"
        };

        var result = await router.RouteMessageAsync(message);

        if (result.Data.ContainsKey("ProcessingTimeMs"))
        {
            Console.WriteLine($"\n⏱️  Processing time: {result.Data["ProcessingTimeMs"]}ms");
        }

        Console.WriteLine();
    }

    static async Task Demo2_RateLimiting()
    {
        Console.WriteLine("═══ Demo 2: Rate Limiting Middleware ═══");
        Console.WriteLine();
        Console.WriteLine("Prevents abuse by limiting requests per sender.");
        Console.WriteLine("Limit: 3 requests per 5 seconds");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterWithMiddleware(logger);

        // Rate limit: 3 requests per 5 seconds
        router.UseMiddleware(new RateLimitMiddleware(3, TimeSpan.FromSeconds(5)));

        var csAgent = new CustomerServiceAgent("cs-001", "CS", logger);
        router.RegisterAgent(csAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001");

        var sender = "aggressive-user";

        // Try to send 5 messages rapidly
        for (int i = 1; i <= 5; i++)
        {
            var message = new AgentMessage
            {
                SenderId = sender,
                Subject = $"Request {i}",
                Content = "Quick question",
                Category = "CustomerService"
            };

            var result = await router.RouteMessageAsync(message);

            if (result.Success)
            {
                Console.WriteLine($"  ✓ Message {i}: Processed");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ Message {i}: {result.Error}");
                Console.ResetColor();
            }

            await Task.Delay(100);
        }

        Console.WriteLine();
    }

    static async Task Demo3_Caching()
    {
        Console.WriteLine("═══ Demo 3: Caching Middleware ═══");
        Console.WriteLine();
        Console.WriteLine("Caches responses to avoid duplicate processing.");
        Console.WriteLine("Cache TTL: 5 seconds");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterWithMiddleware(logger);

        // Cache for 5 seconds
        router.UseMiddleware(new CachingMiddleware(TimeSpan.FromSeconds(5)));

        var techAgent = new TechnicalSupportAgent("tech-001", "Tech", logger);
        router.RegisterAgent(techAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "tech-001");

        var message = new AgentMessage
        {
            SenderId = "customer-1",
            Subject = "Login Issue",
            Content = "Can't log in",
            Category = "TechnicalSupport"
        };

        Console.WriteLine("First request (cache miss):");
        var result1 = await router.RouteMessageAsync(message);

        await Task.Delay(300);

        Console.WriteLine("\nSecond request (cache hit):");
        var result2 = await router.RouteMessageAsync(message);

        await Task.Delay(300);

        Console.WriteLine("\nThird request (cache hit):");
        var result3 = await router.RouteMessageAsync(message);

        Console.WriteLine();
    }

    static async Task Demo4_Retry()
    {
        Console.WriteLine("═══ Demo 4: Retry Middleware ═══");
        Console.WriteLine();
        Console.WriteLine("Automatically retries failed operations.");
        Console.WriteLine("Max attempts: 3, with exponential backoff");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterWithMiddleware(logger);

        // Retry up to 3 times
        router.UseMiddleware(new RetryMiddleware(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(50)));

        // Create a flaky agent
        var flakyAgent = new FlakyAgent("flaky-001", "Flaky Agent", logger);
        router.RegisterAgent(flakyAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "flaky-001");

        var message = new AgentMessage
        {
            SenderId = "customer-1",
            Subject = "Test Retry",
            Content = "This should retry",
            Category = "Test"
        };

        var result = await router.RouteMessageAsync(message);

        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ Eventually succeeded: {result.Response}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    static async Task Demo5_CircuitBreaker()
    {
        Console.WriteLine("═══ Demo 5: Circuit Breaker Middleware ═══");
        Console.WriteLine();
        Console.WriteLine("Prevents cascading failures by stopping requests to failing services.");
        Console.WriteLine("Threshold: 3 failures, Reset: after 5 seconds");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterWithMiddleware(logger);

        // Circuit breaker: open after 3 failures
        router.UseMiddleware(new CircuitBreakerMiddleware(failureThreshold: 3, resetTimeout: TimeSpan.FromSeconds(5)));

        var failingAgent = new FailingAgent("failing-001", "Failing Agent", logger);
        router.RegisterAgent(failingAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "failing-001");

        Console.WriteLine("Sending messages to failing agent:");

        // Send messages until circuit opens
        for (int i = 1; i <= 6; i++)
        {
            var message = new AgentMessage
            {
                SenderId = "customer-1",
                Subject = $"Message {i}",
                Content = "Test",
                Category = "Test"
            };

            var result = await router.RouteMessageAsync(message);

            if (result.Success)
            {
                Console.WriteLine($"  ✓ Message {i}: Success");
            }
            else
            {
                if (result.Error?.Contains("Circuit breaker is OPEN") == true)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ⊗ Message {i}: Circuit OPEN - fast fail");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ Message {i}: Failed");
                    Console.ResetColor();
                }
            }

            await Task.Delay(200);
        }

        Console.WriteLine();
    }

    static async Task Demo6_CompleteMiddlewarePipeline()
    {
        Console.WriteLine("═══ Demo 6: Complete Middleware Pipeline ═══");
        Console.WriteLine();
        Console.WriteLine("Combines multiple middleware for production-ready system:");
        Console.WriteLine("  1. Validation");
        Console.WriteLine("  2. Authentication");
        Console.WriteLine("  3. Priority Boost (VIP)");
        Console.WriteLine("  4. Rate Limiting");
        Console.WriteLine("  5. Logging");
        Console.WriteLine("  6. Timing");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterWithMiddleware(logger);

        // Build complete middleware pipeline
        router.UseMiddleware(new ValidationMiddleware());
        router.UseMiddleware(new AuthenticationMiddleware("customer-1", "customer-2", "vip-customer"));
        router.UseMiddleware(new PriorityBoostMiddleware("vip-customer"));
        router.UseMiddleware(new RateLimitMiddleware(5, TimeSpan.FromSeconds(10)));
        router.UseMiddleware(new LoggingMiddleware(logger));
        router.UseMiddleware(new TimingMiddleware());

        var csAgent = new CustomerServiceAgent("cs-001", "CS", logger);
        router.RegisterAgent(csAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001");

        // Test different scenarios
        var scenarios = new[]
        {
            new AgentMessage
            {
                SenderId = "vip-customer",
                Subject = "VIP Question",
                Content = "I have a question",
                Category = "CustomerService",
                Priority = MessagePriority.Normal
            },
            new AgentMessage
            {
                SenderId = "invalid-sender",
                Subject = "Should Fail Auth",
                Content = "Test",
                Category = "CustomerService"
            },
            new AgentMessage
            {
                SenderId = "customer-1",
                Subject = "", // Invalid - empty subject
                Content = "Test",
                Category = "CustomerService"
            }
        };

        foreach (var message in scenarios)
        {
            Console.WriteLine($"\nSending: {message.Subject} from {message.SenderId}");
            var result = await router.RouteMessageAsync(message);

            if (!result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Blocked: {result.Error}");
                Console.ResetColor();
            }

            await Task.Delay(300);
        }

        Console.WriteLine();
    }

    static async Task Demo7_ConditionalMiddleware()
    {
        Console.WriteLine("═══ Demo 7: Conditional Middleware ═══");
        Console.WriteLine();
        Console.WriteLine("Apply middleware only when conditions are met.");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var pipeline = new MiddlewarePipeline();

        // Only log urgent messages
        pipeline.UseWhen(
            msg => msg.Priority == MessagePriority.Urgent,
            new CallbackMiddleware(
                before: msg => Console.WriteLine($"  [Urgent Handler] Processing: {msg.Subject}"),
                after: (msg, result) => Console.WriteLine($"  [Urgent Handler] Complete: {result.Success}")
            )
        );

        // Simple terminal handler
        MessageDelegate terminal = (msg, ct) =>
        {
            Console.WriteLine($"  [Agent] Processing: {msg.Subject}");
            return Task.FromResult(MessageResult.Ok("Processed"));
        };

        var messages = new[]
        {
            new AgentMessage { Subject = "Normal Message", Priority = MessagePriority.Normal },
            new AgentMessage { Subject = "Urgent Message", Priority = MessagePriority.Urgent },
            new AgentMessage { Subject = "Another Normal", Priority = MessagePriority.Normal }
        };

        foreach (var message in messages)
        {
            await pipeline.ExecuteAsync(message, terminal);
            await Task.Delay(300);
        }

        Console.WriteLine();
    }

    static async Task Demo8_Metrics()
    {
        Console.WriteLine("═══ Demo 8: Metrics Middleware ═══");
        Console.WriteLine();
        Console.WriteLine("Track performance metrics across all messages.");
        Console.WriteLine();

        var logger = new ConsoleAgentLogger();
        var router = new AgentRouterWithMiddleware(logger);

        var metricsMiddleware = new MetricsMiddleware();
        router.UseMiddleware(metricsMiddleware);

        var csAgent = new CustomerServiceAgent("cs-001", "CS", logger);
        router.RegisterAgent(csAgent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001");

        Console.WriteLine("Processing 10 messages...\n");

        var random = new Random();
        for (int i = 1; i <= 10; i++)
        {
            var message = new AgentMessage
            {
                SenderId = $"customer-{i}",
                Subject = $"Message {i}",
                Content = random.Next(2) == 0 ? "hours" : "just asking", // Some will match knowledge base
                Category = "CustomerService"
            };

            await router.RouteMessageAsync(message);
            await Task.Delay(50);
        }

        Console.WriteLine();
        Console.WriteLine("📊 Metrics Report:");
        Console.WriteLine("───────────────────");

        var snapshot = metricsMiddleware.GetSnapshot();
        Console.WriteLine($"Total Messages:    {snapshot.TotalMessages}");
        Console.WriteLine($"Successful:        {snapshot.SuccessCount}");
        Console.WriteLine($"Failed:            {snapshot.FailureCount}");
        Console.WriteLine($"Success Rate:      {snapshot.SuccessRate:P1}");
        Console.WriteLine($"Avg Time:          {snapshot.AverageProcessingTimeMs:F2}ms");
        Console.WriteLine($"Min Time:          {snapshot.MinProcessingTimeMs}ms");
        Console.WriteLine($"Max Time:          {snapshot.MaxProcessingTimeMs}ms");

        Console.WriteLine();
    }
}

// Helper agents for demos
class FlakyAgent : AgentBase
{
    private int _attempts = 0;

    public FlakyAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
    }

    protected override async Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
    {
        await Task.Delay(50, ct);

        _attempts++;

        // Fail first 2 attempts, succeed on 3rd
        if (_attempts < 3)
        {
            return MessageResult.Fail($"Simulated failure (attempt {_attempts})");
        }

        _attempts = 0;
        return MessageResult.Ok("Success after retries");
    }
}

class FailingAgent : AgentBase
{
    public FailingAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
    }

    protected override Task<MessageResult> HandleMessageAsync(AgentMessage message, CancellationToken ct)
    {
        // Always fails
        return Task.FromResult(MessageResult.Fail("Agent is down"));
    }
}
