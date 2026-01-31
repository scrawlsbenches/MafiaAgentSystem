# Agent-to-Agent Communication & Routing System

A production-ready multi-agent communication framework built on expression trees and the rules engine. Demonstrates intelligent message routing, agent collaboration, and distributed task processing.

## 🎯 Overview

This system enables:
- **Intelligent Message Routing** - Rules-based routing to appropriate agents
- **Multi-Agent Collaboration** - Agents can forward messages and work together
- **Priority-Based Processing** - Urgent messages get routed to specialized agents
- **Dynamic Agent Discovery** - Agents advertise capabilities; router finds best match
- **Analytics & Monitoring** - Track message patterns and agent workload
- **Scalable Architecture** - Add new agents and routing rules at runtime

## 🚀 Quick Start

### Run the Demo

```bash
cd AgentRouting
dotnet run
```

This runs 6 interactive demos showing:
1. Basic message routing
2. Priority-based routing
3. Triage and forwarding
4. Broadcasting messages
5. Multi-agent collaboration
6. Analytics and reporting

### Run Tests

```bash
dotnet test
```

## 📐 Architecture

### Core Components

```
┌─────────────────────────────────────────────────────┐
│                   Agent Router                       │
│  (Rules Engine - Expression Trees)                   │
│  - Evaluates routing rules                          │
│  - Selects appropriate agent                        │
│  - Tracks performance                               │
└──────────────┬──────────────────────┬────────────────┘
               │                      │
    ┌──────────▼──────────┐  ┌───────▼─────────┐
    │   Agent A           │  │   Agent B        │
    │  - Capabilities     │  │  - Capabilities  │
    │  - Message Handler  │  │  - Message Handler│
    │  - Can Forward      │  │  - Can Forward   │
    └─────────────────────┘  └──────────────────┘
```

### Message Flow

```
1. Message arrives at Router
2. Router evaluates routing rules (Expression Trees)
3. Rule matches → Agent selected
4. Agent processes message
5. Agent may forward to another agent
6. Results returned
```

## 🤖 Built-In Agents

### CustomerServiceAgent
- Handles general inquiries
- Answers FAQs (hours, shipping, returns, payment)
- Escalates urgent issues to supervisor

### TechnicalSupportAgent
- Handles technical issues
- Troubleshoots common problems (login, crashes, performance)
- Creates support tickets

### BillingAgent
- Handles billing inquiries
- Processes refund requests
- Manages invoice questions

### SupervisorAgent
- Handles escalated issues
- Lower concurrent capacity (focuses on complex issues)
- Tracks escalation cases

### TriageAgent
- Classifies incoming messages
- Assigns categories and priorities
- Routes to appropriate specialist agents

### AnalyticsAgent
- Tracks message patterns
- Monitors agent workload
- Generates reports

## 💡 Usage Examples

### Example 1: Basic Routing

```csharp
var router = new AgentRouter(logger);

// Register agents
router.RegisterAgent(new CustomerServiceAgent("cs-001", "CS", logger));
router.RegisterAgent(new TechnicalSupportAgent("tech-001", "Tech", logger));

// Add routing rules
router.AddRoutingRule(
    "ROUTE_TECH",
    "Route Technical Issues",
    ctx => ctx.Category == "TechnicalSupport",
    "tech-001",
    priority: 100
);

// Route a message
var message = new AgentMessage
{
    Subject = "Login problem",
    Content = "Can't log in",
    Category = "TechnicalSupport"
};

var result = await router.RouteMessageAsync(message);
```

### Example 2: Priority-Based Routing

```csharp
// High-priority messages go to supervisor
router.AddRoutingRule(
    "URGENT_TO_SUPERVISOR",
    "Route Urgent Messages",
    ctx => ctx.IsUrgent || ctx.SubjectContains("complaint"),
    "supervisor-001",
    priority: 200  // Higher priority = evaluated first
);

var urgentMessage = new AgentMessage
{
    Subject = "URGENT: Major issue",
    Priority = MessagePriority.Urgent
};

// Routes to supervisor automatically
await router.RouteMessageAsync(urgentMessage);
```

### Example 3: Agent Forwarding

```csharp
public class CustomerServiceAgent : AgentBase
{
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message, 
        CancellationToken ct)
    {
        if (message.Content.Contains("complaint"))
        {
            // Forward to supervisor
            var forwarded = ForwardMessage(message, "supervisor-001", 
                "Customer complaint - needs supervisor attention");
            
            return MessageResult.Forward(forwarded, 
                "Your message has been escalated to a supervisor");
        }
        
        // Handle normally
        return MessageResult.Ok("We'll respond within 24 hours");
    }
}
```

### Example 4: Broadcasting

```csharp
// Send announcement to all customer service agents
var announcement = new AgentMessage
{
    Subject = "System Maintenance",
    Content = "Scheduled downtime tonight at 10 PM"
};

var results = await router.BroadcastMessageAsync(
    announcement,
    agent => agent.Capabilities.SupportsCategory("CustomerService")
);
```

### Example 5: Agent Collaboration

```csharp
// Triage agent receives message
var triageAgent = new TriageAgent("triage", "Triage", logger, router);

// Classifies and forwards
var result = await triageAgent.ProcessMessageAsync(message);

// Result contains forwarded message to specialist
Assert.NotEmpty(result.ForwardedMessages);
var forwarded = result.ForwardedMessages[0];
// forwarded.ReceiverId = "tech-001" (for technical issues)
```

## 🎨 Creating Custom Agents

```csharp
public class MyCustomAgent : AgentBase
{
    public MyCustomAgent(string id, string name, IAgentLogger logger) 
        : base(id, name, logger)
    {
        // Define capabilities
        Capabilities.SupportedCategories.Add("MyCategory");
        Capabilities.Skills.Add("MySkill");
        Capabilities.MaxConcurrentMessages = 10;
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message, 
        CancellationToken ct)
    {
        // Your custom logic here
        await Task.Delay(100, ct); // Simulate work
        
        if (NeedToForward(message))
        {
            var forwarded = ForwardMessage(message, "other-agent-id");
            return MessageResult.Forward(forwarded, "Forwarded to specialist");
        }
        
        return MessageResult.Ok("Processed successfully");
    }
    
    private bool NeedToForward(AgentMessage message)
    {
        // Your decision logic
        return message.Priority == MessagePriority.Urgent;
    }
}
```

## 🔧 Advanced Features

### 1. Dynamic Rule Management

```csharp
// Add rules at runtime
router.AddRoutingRule(
    "NEW_RULE",
    "Route Premium Customers",
    ctx => ctx.Message.Metadata.ContainsKey("IsPremium"),
    "vip-agent-001",
    priority: 150
);

// Rules are evaluated in priority order
// First matching rule wins (StopOnFirstMatch = true)
```

### 2. Performance Metrics

```csharp
// Track routing performance
var metrics = router.GetRoutingMetrics();

foreach (var (ruleId, metric) in metrics)
{
    Console.WriteLine($"{ruleId}:");
    Console.WriteLine($"  Executions: {metric.ExecutionCount}");
    Console.WriteLine($"  Avg Time: {metric.AverageExecutionTime}");
}
```

### 3. Agent Status Management

```csharp
// Agents track their status
public enum AgentStatus
{
    Available,  // Ready for messages
    Busy,       // At capacity
    Offline,    // Not accepting messages
    Error       // In error state
}

// Router only routes to Available agents
// Automatically switches to Busy at capacity
```

### 4. Conversation Tracking

```csharp
// Messages in same conversation share ConversationId
var message = new AgentMessage
{
    ConversationId = "conv-123",
    ReplyToMessageId = originalMessage.Id
};

// Track entire conversation threads
```

### 5. Custom Logging

```csharp
public class MyLogger : IAgentLogger
{
    public void LogMessageReceived(IAgent agent, AgentMessage message)
    {
        // Send to Application Insights, Splunk, etc.
    }
    
    public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result)
    {
        // Track success rates, response times
    }
    
    // ... implement other methods
}
```

## 📊 Real-World Use Cases

### Customer Service Routing

```csharp
// Auto-classify and route customer inquiries
// - Technical issues → Tech Support
// - Billing questions → Billing Team
// - Complaints → Supervisor
// - General questions → Customer Service
```

### Task Distribution

```csharp
// Distribute tasks across worker agents
// - Route by task type, priority, agent availability
// - Load balancing across available agents
// - Automatic failover if agent goes offline
```

### Multi-Stage Workflows

```csharp
// Order processing workflow
// Order → Validation Agent → Payment Agent → Fulfillment Agent
// Each agent processes and forwards to next stage
```

### Help Desk Automation

```csharp
// Triage → Specialist → Escalation if needed
// Analytics tracks common issues for knowledge base improvement
```

## 🧪 Testing

The project includes comprehensive tests:

```bash
dotnet test --verbosity detailed
```

**Test Coverage:**
- ✅ Message routing to correct agents
- ✅ Priority-based routing
- ✅ Agent message processing
- ✅ Broadcasting to multiple agents
- ✅ Agent capacity management
- ✅ Triage and classification
- ✅ Analytics tracking
- ✅ Agent capabilities

## 🎯 Expression Trees in Action

This system demonstrates advanced expression tree usage:

### 1. Dynamic Rule Compilation
```csharp
// Rules are expression trees - compiled once, executed many times
Expression<Func<RoutingContext, bool>> rule = 
    ctx => ctx.IsUrgent && ctx.CategoryIs("Support");

// Compiled to native code for maximum performance
var compiled = rule.Compile();
```

### 2. Runtime Rule Building
```csharp
// Build routing rules from configuration
router.AddRoutingRule(
    ruleId,
    ruleName,
    ctx => ctx.Priority == MessagePriority.High,  // Expression tree
    targetAgentId,
    priority
);
```

### 3. Rule Composition
```csharp
// Combine multiple conditions
Expression<Func<RoutingContext, bool>> complex = 
    ctx => ctx.IsUrgent && 
           ctx.CategoryIs("Billing") && 
           ctx.HasAvailableAgentWithSkill("RefundProcessing");
```

## 🔗 Integration with Rules Engine

This system is built on top of the RulesEngine project:

- **Rules Engine** provides the routing logic evaluation
- **Expression Trees** enable type-safe, performant routing rules
- **Performance Tracking** built into the rules engine
- **Priority-Based Execution** from rules engine

## 📈 Performance

- **Routing Decisions**: < 1ms (compiled expression trees)
- **Message Processing**: Depends on agent logic
- **Throughput**: Limited by agent capacity, not routing
- **Scalability**: Add agents horizontally; routing is stateless

## 🚧 Production Considerations

### Thread Safety
All agents use thread-safe message processing. Multiple messages can be processed concurrently up to `MaxConcurrentMessages`.

### Error Handling
Agents catch exceptions and return `MessageResult.Fail()` with error details.

### Logging
Implement `IAgentLogger` for production logging (Application Insights, etc.)

### Monitoring
Use routing metrics and analytics agent for operational insights.

### Scaling
- Run multiple instances of each agent type
- Use message queues (RabbitMQ, Azure Service Bus) for persistence
- Add load balancing across agent instances

## 📝 Next Steps

1. **Run the demos** to see everything in action
2. **Create custom agents** for your domain
3. **Define routing rules** for your workflow
4. **Add analytics** to track patterns
5. **Integrate** with your existing systems

## 🎓 Key Concepts

- **Agent**: Processes messages, has capabilities, can forward
- **Router**: Evaluates rules, selects agents, tracks performance
- **Routing Rules**: Expression trees that determine message routing
- **Capabilities**: What an agent can do (categories, skills)
- **Message**: Unit of communication between agents
- **Triage**: Classification and initial routing
- **Escalation**: Forwarding to higher-level agent
- **Broadcasting**: Send to multiple agents simultaneously

---

**Built with Expression Trees + Rules Engine = Powerful Agent Communication!** 🚀
