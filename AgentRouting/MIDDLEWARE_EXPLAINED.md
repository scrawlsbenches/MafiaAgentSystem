# AgentRouting Middleware System

## 🎯 What is Middleware?

**Middleware** is a pattern that allows you to insert processing logic **before** and **after** messages are handled by agents, creating a processing pipeline.

```
Message → [Middleware 1] → [Middleware 2] → [Middleware 3] → Agent → Response
            ↓ logging        ↓ auth         ↓ rate limit
            ↑ timing         ↑ validation   ↑ caching
```

Think of it like **filters** or **interceptors** that wrap around agent message processing.

## 🚀 Why Middleware?

### Without Middleware (Before)
```csharp
public async Task<MessageResult> ProcessMessage(AgentMessage message)
{
    // Log the message
    _logger.LogReceived(message);
    
    // Check authentication
    if (!IsAuthenticated(message))
        return MessageResult.Fail("Unauthorized");
    
    // Check rate limit
    if (IsRateLimited(message))
        return MessageResult.Fail("Too many requests");
    
    // Validate message
    if (!IsValid(message))
        return MessageResult.Fail("Invalid message");
    
    // FINALLY process the message
    var result = await ActuallyProcessMessage(message);
    
    // Log the result
    _logger.LogProcessed(message, result);
    
    // Track metrics
    _metrics.Record(message, result);
    
    return result;
}
```

**Problems:**
- ❌ Every agent repeats this code
- ❌ Hard to add new cross-cutting concerns
- ❌ Difficult to test in isolation
- ❌ Can't reorder or disable features easily

### With Middleware (After)
```csharp
public async Task<MessageResult> ProcessMessage(AgentMessage message)
{
    // All cross-cutting concerns handled by middleware!
    return await ActuallyProcessMessage(message);
}

// Middleware configured once:
router.UseMiddleware(new LoggingMiddleware());
router.UseMiddleware(new AuthenticationMiddleware());
router.UseMiddleware(new RateLimitMiddleware());
router.UseMiddleware(new ValidationMiddleware());
router.UseMiddleware(new MetricsMiddleware());
```

**Benefits:**
- ✅ DRY (Don't Repeat Yourself)
- ✅ Separation of concerns
- ✅ Easy to add/remove/reorder
- ✅ Testable in isolation
- ✅ Composable pipeline

## 🎨 Middleware Architecture

### Execution Flow

```
Request enters pipeline
    ↓
┌─────────────────────────┐
│  Logging Middleware     │ ← Logs incoming message
│  - Before: Log request  │
│  - After: Log response  │
└──────────┬──────────────┘
           ↓
┌─────────────────────────┐
│  Auth Middleware        │ ← Checks permissions
│  - Before: Validate     │
│  - After: (nothing)     │
└──────────┬──────────────┘
           ↓
┌─────────────────────────┐
│  Rate Limit Middleware  │ ← Prevents abuse
│  - Before: Check limit  │
│  - After: Track usage   │
└──────────┬──────────────┘
           ↓
┌─────────────────────────┐
│  Cache Middleware       │ ← Avoids duplicate work
│  - Before: Check cache  │
│  - After: Store result  │
└──────────┬──────────────┘
           ↓
┌─────────────────────────┐
│  Agent Handler          │ ← Actual business logic
│  - Process message      │
└──────────┬──────────────┘
           ↓
Response returns through pipeline (reverse order)
```

### The Middleware Interface

```csharp
public interface IMessageMiddleware
{
    Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct = default);
}

// Delegate that calls the next middleware in the pipeline
public delegate Task<MessageResult> MessageDelegate(
    AgentMessage message,
    CancellationToken ct);
```

## 💡 Common Middleware Use Cases

### 1. **Logging Middleware**
**Purpose:** Track all messages and responses
```csharp
Before: Log("Received message: {subject}")
After:  Log("Processed in {duration}ms with {status}")
```

### 2. **Authentication Middleware**
**Purpose:** Verify sender identity
```csharp
Before: Check if message.SenderId is authenticated
If not: Return MessageResult.Fail("Unauthorized")
```

### 3. **Authorization Middleware**
**Purpose:** Check permissions
```csharp
Before: Check if sender has permission for this operation
If not: Return MessageResult.Fail("Forbidden")
```

### 4. **Rate Limiting Middleware**
**Purpose:** Prevent abuse
```csharp
Before: Check if sender exceeded rate limit
If yes: Return MessageResult.Fail("Too many requests")
After: Increment request counter
```

### 5. **Caching Middleware**
**Purpose:** Avoid duplicate processing
```csharp
Before: Check if we've processed this exact message before
If yes: Return cached result
After: Cache the result for future use
```

### 6. **Validation Middleware**
**Purpose:** Ensure message integrity
```csharp
Before: Validate message structure, required fields, data types
If invalid: Return MessageResult.Fail("Invalid message")
```

### 7. **Retry Middleware**
**Purpose:** Handle transient failures
```csharp
Try processing message
If fails: Retry up to N times with exponential backoff
```

### 8. **Circuit Breaker Middleware**
**Purpose:** Prevent cascading failures
```csharp
If agent is failing too often: Stop sending messages temporarily
After cooldown: Resume sending messages
```

### 9. **Metrics/Monitoring Middleware**
**Purpose:** Track performance
```csharp
Before: Start timer
After:  Record duration, success rate, error types
```

### 10. **Transformation Middleware**
**Purpose:** Modify messages
```csharp
Before: Enrich message with additional context
After:  Transform response format
```

## 🔧 Implementation Patterns

### Pattern 1: Simple Middleware
```csharp
public class LoggingMiddleware : IMessageMiddleware
{
    public async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        Console.WriteLine($"Before: {message.Subject}");
        
        var result = await next(message, ct);
        
        Console.WriteLine($"After: {result.Success}");
        
        return result;
    }
}
```

### Pattern 2: Short-Circuit Middleware
```csharp
public class AuthMiddleware : IMessageMiddleware
{
    public async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        if (!IsAuthenticated(message))
        {
            // Don't call next - short circuit!
            return MessageResult.Fail("Unauthorized");
        }
        
        return await next(message, ct);
    }
}
```

### Pattern 3: Wrap and Modify
```csharp
public class TimingMiddleware : IMessageMiddleware
{
    public async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        
        var result = await next(message, ct);
        
        sw.Stop();
        result.Data["ProcessingTime"] = sw.ElapsedMilliseconds;
        
        return result;
    }
}
```

### Pattern 4: State Management
```csharp
public class RateLimitMiddleware : IMessageMiddleware
{
    private readonly Dictionary<string, RateLimitState> _state = new();
    
    public async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        if (IsRateLimited(message.SenderId))
        {
            return MessageResult.Fail("Rate limit exceeded");
        }
        
        var result = await next(message, ct);
        
        IncrementCounter(message.SenderId);
        
        return result;
    }
}
```

## 🎯 Real-World Examples

### Example 1: E-Commerce Order Processing

```csharp
// Order processing pipeline
router.UseMiddleware(new LoggingMiddleware());
router.UseMiddleware(new AuthenticationMiddleware());
router.UseMiddleware(new ValidationMiddleware(new OrderValidator()));
router.UseMiddleware(new InventoryCheckMiddleware());
router.UseMiddleware(new PaymentAuthorizationMiddleware());
router.UseMiddleware(new FraudDetectionMiddleware());
router.UseMiddleware(new MetricsMiddleware());

// Message flows through all these checks before reaching OrderAgent
```

### Example 2: Customer Service Escalation

```csharp
// Customer service pipeline
router.UseMiddleware(new SentimentAnalysisMiddleware()); // Detect angry customers
router.UseMiddleware(new PriorityBoostMiddleware());     // Boost priority if needed
router.UseMiddleware(new VIPDetectionMiddleware());      // Check VIP status
router.UseMiddleware(new LanguageDetectionMiddleware()); // Route by language
router.UseMiddleware(new BusinessHoursMiddleware());     // Queue if after hours
```

### Example 3: API Gateway Pattern

```csharp
// API-style middleware
router.UseMiddleware(new CorsMiddleware());
router.UseMiddleware(new ApiKeyAuthMiddleware());
router.UseMiddleware(new RateLimitMiddleware(100, TimeSpan.FromMinutes(1)));
router.UseMiddleware(new RequestCompressionMiddleware());
router.UseMiddleware(new ResponseCachingMiddleware());
router.UseMiddleware(new ErrorHandlingMiddleware());
```

## 📊 Performance Considerations

### Middleware Overhead

| Middleware Type | Overhead | When to Use |
|----------------|----------|-------------|
| Logging | ~0.1ms | Always |
| Authentication | ~1-5ms | User-facing APIs |
| Rate Limiting | ~0.5ms | Public endpoints |
| Caching | Variable | Expensive operations |
| Validation | ~0.5-2ms | Input validation |

### Optimization Tips

1. **Order matters!** Put fast, eliminating middleware first
   ```csharp
   // Good order
   router.UseMiddleware(new CacheMiddleware());     // Fast, often returns early
   router.UseMiddleware(new AuthMiddleware());      // Fast, eliminates bad requests
   router.UseMiddleware(new RateLimitMiddleware()); // Fast, protects downstream
   router.UseMiddleware(new ValidationMiddleware()); // Slower
   router.UseMiddleware(new LoggingMiddleware());   // Slowest (I/O)
   ```

2. **Use async properly** - Don't block!
   ```csharp
   // Bad
   var result = next(message, ct).Result; // Blocks!
   
   // Good
   var result = await next(message, ct); // Async all the way
   ```

3. **Cache middleware instances** - Don't recreate
   ```csharp
   // Bad
   foreach (var message in messages)
       router.UseMiddleware(new LoggingMiddleware()); // Creates new instance each time!
   
   // Good
   var logging = new LoggingMiddleware();
   router.UseMiddleware(logging); // Reuse instance
   ```

## 🔐 Security Middleware Examples

### Defense in Depth
```csharp
router.UseMiddleware(new IpWhitelistMiddleware());
router.UseMiddleware(new ApiKeyAuthMiddleware());
router.UseMiddleware(new JwtAuthMiddleware());
router.UseMiddleware(new PermissionCheckMiddleware());
router.UseMiddleware(new AuditLoggingMiddleware());
router.UseMiddleware(new EncryptionMiddleware());
```

Multiple layers of security!

## 🧪 Testing Middleware

### Test Individual Middleware
```csharp
[Fact]
public async Task RateLimitMiddleware_BlocksExcessiveRequests()
{
    var middleware = new RateLimitMiddleware(limit: 2, window: TimeSpan.FromSeconds(1));
    var message = new AgentMessage { SenderId = "user1" };
    
    // First two requests should succeed
    var result1 = await middleware.InvokeAsync(message, MockNext, default);
    var result2 = await middleware.InvokeAsync(message, MockNext, default);
    Assert.True(result1.Success);
    Assert.True(result2.Success);
    
    // Third should be blocked
    var result3 = await middleware.InvokeAsync(message, MockNext, default);
    Assert.False(result3.Success);
    Assert.Contains("rate limit", result3.Error);
}
```

### Test Middleware Pipeline
```csharp
[Fact]
public async Task Pipeline_ExecutesMiddlewareInOrder()
{
    var executionOrder = new List<string>();
    
    var middleware1 = new CallbackMiddleware(() => executionOrder.Add("M1"));
    var middleware2 = new CallbackMiddleware(() => executionOrder.Add("M2"));
    
    var pipeline = CreatePipeline(middleware1, middleware2);
    await pipeline.InvokeAsync(message);
    
    Assert.Equal(new[] { "M1", "M2" }, executionOrder);
}
```

## 🎓 Best Practices

### ✅ DO

1. **Keep middleware focused** - One responsibility per middleware
2. **Make middleware reusable** - Don't couple to specific agents
3. **Use dependency injection** - For logger, config, etc.
4. **Document behavior** - Especially short-circuit conditions
5. **Handle errors gracefully** - Don't let middleware crash the pipeline
6. **Use cancellation tokens** - Respect cancellation
7. **Order thoughtfully** - Fast and eliminating middleware first

### ❌ DON'T

1. **Don't modify the message object directly** - Create copies if needed
2. **Don't swallow exceptions** - Log and re-throw or return error result
3. **Don't block async code** - Use async/await throughout
4. **Don't create stateful middleware without thread safety** - Use concurrent collections
5. **Don't perform expensive operations in middleware** - Keep it fast
6. **Don't skip calling next()** - Unless intentionally short-circuiting

## 🚀 Advanced Patterns

### Conditional Middleware
```csharp
public class ConditionalMiddleware : IMessageMiddleware
{
    private readonly Func<AgentMessage, bool> _condition;
    private readonly IMessageMiddleware _middleware;
    
    public async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        if (_condition(message))
        {
            return await _middleware.InvokeAsync(message, next, ct);
        }
        
        return await next(message, ct);
    }
}

// Usage
router.UseMiddleware(new ConditionalMiddleware(
    msg => msg.Priority == MessagePriority.Urgent,
    new FastTrackMiddleware()
));
```

### Branching Middleware
```csharp
public class BranchingMiddleware : IMessageMiddleware
{
    public async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        if (message.Category == "Emergency")
        {
            // Take emergency path
            return await ProcessEmergency(message, ct);
        }
        
        // Take normal path
        return await next(message, ct);
    }
}
```

### Parallel Middleware
```csharp
public class ParallelMiddleware : IMessageMiddleware
{
    public async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        // Run multiple operations in parallel
        var tasks = new[]
        {
            AnalyzeSentiment(message),
            DetectLanguage(message),
            CheckSpam(message)
        };
        
        await Task.WhenAll(tasks);
        
        return await next(message, ct);
    }
}
```

## 🎯 Summary

Middleware provides:
- ✅ **Separation of concerns** - Agent logic separate from infrastructure
- ✅ **Reusability** - Write once, use everywhere
- ✅ **Composability** - Build complex pipelines from simple pieces
- ✅ **Testability** - Test middleware in isolation
- ✅ **Flexibility** - Add/remove/reorder at runtime
- ✅ **Maintainability** - Changes in one place

**Middleware transforms your agent system from a simple router into a powerful, production-ready platform!**

---

Next: See the complete implementation with 10+ middleware examples!
