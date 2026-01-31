using RulesEngine.Core;

namespace RulesEngine.AgentDemo;

/// <summary>
/// Simplified Agent-to-Agent Communication Demo
/// Shows how to use the Rules Engine for intelligent agent routing
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.Clear();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Agent-to-Agent Communication with Rules Engine            ║");
        Console.WriteLine("║   Simplified Demo - Rules Engine Core Features              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This demo shows how to build agent systems using just the Rules Engine.");
        Console.WriteLine("For a full-featured agent system, see the AgentRouting project!");
        Console.WriteLine();

        await Scenario1_BasicAgentRouting();
        await Scenario2_PriorityBasedRouting();
        await Scenario3_SkillBasedRouting();
        await Scenario4_LoadBalancing();
        await Scenario5_DynamicAgentSelection();
        await Scenario6_ConversationRouting();

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("✓ All scenarios complete!");
        Console.WriteLine();
        Console.WriteLine("Key Takeaways:");
        Console.WriteLine("  • Rules Engine enables intelligent agent routing");
        Console.WriteLine("  • Expression trees provide type-safe routing logic");
        Console.WriteLine("  • Dynamic rules allow runtime agent management");
        Console.WriteLine("  • Performance tracking built-in");
        Console.WriteLine();
        Console.WriteLine("For a complete agent framework, check out AgentRouting project!");
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static async Task Scenario1_BasicAgentRouting()
    {
        Console.WriteLine("═══ Scenario 1: Basic Agent Routing ═══");
        Console.WriteLine();
        Console.WriteLine("Three agents: Alice (General), Bob (Technical), Carol (Billing)");
        Console.WriteLine("Messages are routed based on category using rules.");
        Console.WriteLine();

        // Create routing context and engine
        var engine = new RulesEngineCore<MessageContext>(new RulesEngineOptions
        {
            StopOnFirstMatch = true,
            TrackPerformance = true
        });

        // Define routing rules
        var techRule = new RuleBuilder<MessageContext>()
            .WithId("ROUTE_TECH")
            .WithName("Route Technical Messages")
            .WithPriority(100)
            .When(ctx => ctx.Category == "Technical")
            .Then(ctx => ctx.AssignedAgent = "Bob (Technical)")
            .Build();

        var billingRule = new RuleBuilder<MessageContext>()
            .WithId("ROUTE_BILLING")
            .WithName("Route Billing Messages")
            .WithPriority(100)
            .When(ctx => ctx.Category == "Billing")
            .Then(ctx => ctx.AssignedAgent = "Carol (Billing)")
            .Build();

        var defaultRule = new RuleBuilder<MessageContext>()
            .WithId("ROUTE_DEFAULT")
            .WithName("Route to General Agent")
            .WithPriority(1)
            .When(ctx => true)
            .Then(ctx => ctx.AssignedAgent = "Alice (General)")
            .Build();

        engine.RegisterRules(techRule, billingRule, defaultRule);

        // Route some messages
        var messages = new[]
        {
            new MessageContext { From = "Customer1", Subject = "Password Reset", Category = "Technical" },
            new MessageContext { From = "Customer2", Subject = "Invoice Question", Category = "Billing" },
            new MessageContext { From = "Customer3", Subject = "General Inquiry", Category = "General" }
        };

        foreach (var message in messages)
        {
            engine.Execute(message);
            Console.WriteLine($"  [{message.From}] '{message.Subject}'");
            Console.WriteLine($"    → Routed to: {message.AssignedAgent}");
            await Task.Delay(300);
        }

        Console.WriteLine();
    }

    static async Task Scenario2_PriorityBasedRouting()
    {
        Console.WriteLine("═══ Scenario 2: Priority-Based Routing ═══");
        Console.WriteLine();
        Console.WriteLine("High-priority messages go to senior agents.");
        Console.WriteLine("Normal messages go to junior agents.");
        Console.WriteLine();

        var engine = new RulesEngineCore<MessageContext>();

        // High priority → Senior agent
        engine.RegisterRule(new RuleBuilder<MessageContext>()
            .WithPriority(200)
            .When(ctx => ctx.Priority == "High" || ctx.Priority == "Urgent")
            .Then(ctx => ctx.AssignedAgent = "Senior Agent (Dave)")
            .Build());

        // Normal priority → Junior agent
        engine.RegisterRule(new RuleBuilder<MessageContext>()
            .WithPriority(100)
            .When(ctx => ctx.Priority == "Normal")
            .Then(ctx => ctx.AssignedAgent = "Junior Agent (Eve)")
            .Build());

        var messages = new[]
        {
            new MessageContext { From = "VIP1", Subject = "System Down!", Priority = "Urgent" },
            new MessageContext { From = "User1", Subject = "Question", Priority = "Normal" },
            new MessageContext { From = "Manager", Subject = "Important", Priority = "High" }
        };

        foreach (var message in messages)
        {
            engine.Execute(message);
            Console.WriteLine($"  [{message.Priority}] {message.Subject}");
            Console.WriteLine($"    → Assigned to: {message.AssignedAgent}");
            await Task.Delay(300);
        }

        Console.WriteLine();
    }

    static async Task Scenario3_SkillBasedRouting()
    {
        Console.WriteLine("═══ Scenario 3: Skill-Based Routing ═══");
        Console.WriteLine();
        Console.WriteLine("Messages routed based on required skills.");
        Console.WriteLine("Using agent skill matrix.");
        Console.WriteLine();

        var engine = new RulesEngineCore<MessageContext>();

        // Define agent skills
        var agentSkills = new Dictionary<string, List<string>>
        {
            ["Frank"] = new() { "Python", "JavaScript", "Docker" },
            ["Grace"] = new() { "C#", ".NET", "SQL" },
            ["Henry"] = new() { "AWS", "Azure", "Kubernetes" }
        };

        // Route based on content keywords
        engine.RegisterRule(new RuleBuilder<MessageContext>()
            .WithName("Python/JavaScript Developer")
            .WithPriority(100)
            .When(ctx => ctx.Content.Contains("Python") || ctx.Content.Contains("JavaScript"))
            .Then(ctx =>
            {
                ctx.AssignedAgent = "Frank";
                ctx.MatchedSkills = "Python, JavaScript, Docker";
            })
            .Build());

        engine.RegisterRule(new RuleBuilder<MessageContext>()
            .WithName("C#/.NET Developer")
            .WithPriority(100)
            .When(ctx => ctx.Content.Contains("C#") || ctx.Content.Contains(".NET"))
            .Then(ctx =>
            {
                ctx.AssignedAgent = "Grace";
                ctx.MatchedSkills = "C#, .NET, SQL";
            })
            .Build());

        engine.RegisterRule(new RuleBuilder<MessageContext>()
            .WithName("Cloud Infrastructure")
            .WithPriority(100)
            .When(ctx => ctx.Content.Contains("AWS") || ctx.Content.Contains("Kubernetes"))
            .Then(ctx =>
            {
                ctx.AssignedAgent = "Henry";
                ctx.MatchedSkills = "AWS, Azure, Kubernetes";
            })
            .Build());

        var tasks = new[]
        {
            new MessageContext { Subject = "Python Migration", Content = "Migrate Python 2 to 3" },
            new MessageContext { Subject = "C# Bug Fix", Content = "Fix C# memory leak" },
            new MessageContext { Subject = "AWS Deployment", Content = "Deploy to AWS ECS" }
        };

        foreach (var task in tasks)
        {
            engine.Execute(task);
            Console.WriteLine($"  Task: {task.Subject}");
            Console.WriteLine($"    → Agent: {task.AssignedAgent}");
            Console.WriteLine($"    → Skills: {task.MatchedSkills}");
            await Task.Delay(300);
        }

        Console.WriteLine();
    }

    static async Task Scenario4_LoadBalancing()
    {
        Console.WriteLine("═══ Scenario 4: Load Balancing ═══");
        Console.WriteLine();
        Console.WriteLine("Distribute messages across available agents.");
        Console.WriteLine("Simulating agent workload tracking.");
        Console.WriteLine();

        var agentWorkload = new Dictionary<string, int>
        {
            ["Agent1"] = 0,
            ["Agent2"] = 0,
            ["Agent3"] = 0
        };

        var engine = new RulesEngineCore<MessageContext>();

        // Simple round-robin using workload
        engine.RegisterRule(new RuleBuilder<MessageContext>()
            .WithName("Load Balance")
            .When(ctx => true)
            .Then(ctx =>
            {
                // Find agent with lowest workload
                var leastBusy = agentWorkload.OrderBy(kv => kv.Value).First();
                ctx.AssignedAgent = leastBusy.Key;
                agentWorkload[leastBusy.Key]++;
            })
            .Build());

        Console.WriteLine("Processing 9 messages with 3 agents...");
        Console.WriteLine();

        for (int i = 1; i <= 9; i++)
        {
            var message = new MessageContext
            {
                From = $"User{i}",
                Subject = $"Request {i}"
            };

            engine.Execute(message);
            Console.Write($"  Message {i} → {message.AssignedAgent}  ");

            if (i % 3 == 0)
                Console.WriteLine();

            await Task.Delay(150);
        }

        Console.WriteLine();
        Console.WriteLine("Final workload distribution:");
        foreach (var (agent, count) in agentWorkload.OrderBy(kv => kv.Key))
        {
            Console.WriteLine($"  {agent}: {count} messages");
        }

        Console.WriteLine();
    }

    static async Task Scenario5_DynamicAgentSelection()
    {
        Console.WriteLine("═══ Scenario 5: Dynamic Agent Selection ═══");
        Console.WriteLine();
        Console.WriteLine("Agents can go online/offline dynamically.");
        Console.WriteLine("Rules adapt to available agents.");
        Console.WriteLine();

        var availableAgents = new HashSet<string> { "Agent1", "Agent2", "Agent3" };

        var engine = new RulesEngineCore<MessageContext>();

        engine.RegisterRule(new RuleBuilder<MessageContext>()
            .WithName("Select Available Agent")
            .When(ctx => true)
            .Then(ctx =>
            {
                if (availableAgents.Count > 0)
                {
                    ctx.AssignedAgent = availableAgents.First();
                }
                else
                {
                    ctx.AssignedAgent = "No agents available";
                }
            })
            .Build());

        // Simulate agents going offline
        var messages = new[]
        {
            ("Message 1", (Action?)null),
            ("Message 2", () => { availableAgents.Remove("Agent1"); Console.WriteLine("    [Agent1 went offline]"); }),
            ("Message 3", (Action?)null),
            ("Message 4", () => { availableAgents.Remove("Agent2"); Console.WriteLine("    [Agent2 went offline]"); }),
            ("Message 5", (Action?)null),
            ("Message 6", () => { availableAgents.Remove("Agent3"); Console.WriteLine("    [Agent3 went offline]"); }),
            ("Message 7", (Action?)null)
        };

        foreach (var (subject, action) in messages)
        {
            action?.Invoke();

            var message = new MessageContext { Subject = subject };
            engine.Execute(message);

            Console.WriteLine($"  {subject} → {message.AssignedAgent}");
            Console.WriteLine($"    Available: [{string.Join(", ", availableAgents)}]");

            await Task.Delay(400);
        }

        Console.WriteLine();
    }

    static async Task Scenario6_ConversationRouting()
    {
        Console.WriteLine("═══ Scenario 6: Conversation Routing ═══");
        Console.WriteLine();
        Console.WriteLine("Route follow-up messages to the same agent (conversation affinity).");
        Console.WriteLine();

        var conversationHistory = new Dictionary<string, string>();
        var engine = new RulesEngineCore<MessageContext>();

        // If conversation exists, route to same agent
        engine.RegisterRule(new RuleBuilder<MessageContext>()
            .WithName("Conversation Affinity")
            .WithPriority(200)
            .When(ctx => !string.IsNullOrEmpty(ctx.ConversationId) &&
                         conversationHistory.ContainsKey(ctx.ConversationId))
            .Then(ctx => ctx.AssignedAgent = conversationHistory[ctx.ConversationId])
            .Build());

        // New conversation - route to Alice
        engine.RegisterRule(new RuleBuilder<MessageContext>()
            .WithName("New Conversation")
            .WithPriority(100)
            .When(ctx => string.IsNullOrEmpty(ctx.ConversationId) ||
                         !conversationHistory.ContainsKey(ctx.ConversationId))
            .Then(ctx =>
            {
                ctx.AssignedAgent = "Alice";
                if (!string.IsNullOrEmpty(ctx.ConversationId))
                {
                    conversationHistory[ctx.ConversationId] = "Alice";
                }
            })
            .Build());

        var messages = new[]
        {
            new MessageContext { Subject = "Initial Question", ConversationId = "CONV-001" },
            new MessageContext { Subject = "Follow-up #1", ConversationId = "CONV-001" },
            new MessageContext { Subject = "Follow-up #2", ConversationId = "CONV-001" },
            new MessageContext { Subject = "New Question", ConversationId = "CONV-002" },
            new MessageContext { Subject = "Follow-up", ConversationId = "CONV-002" }
        };

        foreach (var message in messages)
        {
            engine.Execute(message);
            Console.WriteLine($"  [{message.ConversationId}] {message.Subject}");
            Console.WriteLine($"    → Agent: {message.AssignedAgent}");
            await Task.Delay(300);
        }

        Console.WriteLine();
    }
}

/// <summary>
/// Context for message routing decisions
/// </summary>
public class MessageContext
{
    public string From { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Content { get; set; } = "";
    public string Category { get; set; } = "";
    public string Priority { get; set; } = "Normal";
    public string? ConversationId { get; set; }

    // Routing decisions (set by rules)
    public string AssignedAgent { get; set; } = "";
    public string MatchedSkills { get; set; } = "";
}
