using Xunit;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.Agents;

namespace AgentRouting.Tests;

public class MiddlewareTests
{
    private class TestLogger : IAgentLogger
    {
        public void LogMessageReceived(IAgent agent, AgentMessage message) { }
        public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result) { }
        public void LogMessageRouted(AgentMessage message, IAgent fromAgent, IAgent toAgent) { }
        public void LogError(IAgent agent, AgentMessage message, Exception ex) { }
    }

    [Fact]
    public async Task Pipeline_ExecutesMiddlewareInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var pipeline = new MiddlewarePipeline();

        pipeline.UseCallback(before: _ => executionOrder.Add("M1-Before"));
        pipeline.UseCallback(before: _ => executionOrder.Add("M2-Before"));
        pipeline.UseCallback(before: _ => executionOrder.Add("M3-Before"));

        MessageDelegate terminal = (msg, ct) =>
        {
            executionOrder.Add("Terminal");
            return Task.FromResult(MessageResult.Ok());
        };

        var message = new AgentMessage();

        // Act
        await pipeline.ExecuteAsync(message, terminal);

        // Assert
        Assert.Equal(new[] { "M1-Before", "M2-Before", "M3-Before", "Terminal" }, executionOrder);
    }

    [Fact]
    public async Task ValidationMiddleware_BlocksInvalidMessages()
    {
        // Arrange
        var middleware = new ValidationMiddleware();
        var message = new AgentMessage
        {
            SenderId = "", // Invalid - empty
            Subject = "Test",
            Content = "Test"
        };

        var nextCalled = false;
        MessageDelegate next = (msg, ct) =>
        {
            nextCalled = true;
            return Task.FromResult(MessageResult.Ok());
        };

        // Act
        var result = await middleware.InvokeAsync(message, next);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("SenderId is required", result.Error);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task RateLimitMiddleware_BlocksExcessiveRequests()
    {
        // Arrange
        var middleware = new RateLimitMiddleware(maxRequests: 2, window: TimeSpan.FromSeconds(1));
        var message = new AgentMessage { SenderId = "user1" };

        MessageDelegate next = (msg, ct) => Task.FromResult(MessageResult.Ok());

        // Act - first two should succeed
        var result1 = await middleware.InvokeAsync(message, next);
        var result2 = await middleware.InvokeAsync(message, next);

        // Third should be blocked
        var result3 = await middleware.InvokeAsync(message, next);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.False(result3.Success);
        Assert.Contains("Rate limit exceeded", result3.Error);
    }

    [Fact]
    public async Task CachingMiddleware_ReturnsCachedResults()
    {
        // Arrange
        var middleware = new CachingMiddleware(TimeSpan.FromMinutes(1));
        var message = new AgentMessage
        {
            SenderId = "user1",
            Subject = "Test",
            Content = "Content",
            Category = "Test"
        };

        var callCount = 0;
        MessageDelegate next = (msg, ct) =>
        {
            callCount++;
            return Task.FromResult(MessageResult.Ok($"Response {callCount}"));
        };

        // Act
        var result1 = await middleware.InvokeAsync(message, next);
        var result2 = await middleware.InvokeAsync(message, next);
        var result3 = await middleware.InvokeAsync(message, next);

        // Assert
        Assert.Equal(1, callCount); // Next should only be called once
        Assert.Equal("Response 1", result1.Response);
        Assert.Equal("Response 1", result2.Response); // Cached
        Assert.Equal("Response 1", result3.Response); // Cached
    }

    [Fact]
    public async Task RetryMiddleware_RetriesOnFailure()
    {
        // Arrange
        var middleware = new RetryMiddleware(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(10));
        var message = new AgentMessage();

        var attempts = 0;
        MessageDelegate next = (msg, ct) =>
        {
            attempts++;
            if (attempts < 3)
                return Task.FromResult(MessageResult.Fail("Failed"));
            return Task.FromResult(MessageResult.Ok("Success"));
        };

        // Act
        var result = await middleware.InvokeAsync(message, next);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task CircuitBreakerMiddleware_OpensAfterFailures()
    {
        // Arrange
        var middleware = new CircuitBreakerMiddleware(
            failureThreshold: 3,
            resetTimeout: TimeSpan.FromSeconds(1)
        );
        var message = new AgentMessage();

        MessageDelegate failingNext = (msg, ct) => Task.FromResult(MessageResult.Fail("Failed"));

        // Act - cause 3 failures to open circuit
        await middleware.InvokeAsync(message, failingNext);
        await middleware.InvokeAsync(message, failingNext);
        await middleware.InvokeAsync(message, failingNext);

        // Next call should be blocked by open circuit
        var result = await middleware.InvokeAsync(message, failingNext);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Circuit breaker is OPEN", result.Error);
    }

    [Fact]
    public async Task TimingMiddleware_AddsProcessingTime()
    {
        // Arrange
        var middleware = new TimingMiddleware();
        var message = new AgentMessage();

        MessageDelegate next = async (msg, ct) =>
        {
            await Task.Delay(50, ct);
            return MessageResult.Ok();
        };

        // Act
        var result = await middleware.InvokeAsync(message, next);

        // Assert
        Assert.True(result.Data.ContainsKey("ProcessingTimeMs"));
        var time = (long)result.Data["ProcessingTimeMs"];
        Assert.True(time >= 50);
    }

    [Fact]
    public async Task AuthenticationMiddleware_BlocksUnauthenticatedSenders()
    {
        // Arrange
        var middleware = new AuthenticationMiddleware("user1", "user2");

        var authenticatedMessage = new AgentMessage { SenderId = "user1" };
        var unauthenticatedMessage = new AgentMessage { SenderId = "hacker" };

        MessageDelegate next = (msg, ct) => Task.FromResult(MessageResult.Ok());

        // Act
        var result1 = await middleware.InvokeAsync(authenticatedMessage, next);
        var result2 = await middleware.InvokeAsync(unauthenticatedMessage, next);

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.Contains("not authenticated", result2.Error);
    }

    [Fact]
    public async Task PriorityBoostMiddleware_BoostsVIPPriority()
    {
        // Arrange
        var middleware = new PriorityBoostMiddleware("vip-user");

        var vipMessage = new AgentMessage
        {
            SenderId = "vip-user",
            Priority = MessagePriority.Normal
        };

        var regularMessage = new AgentMessage
        {
            SenderId = "regular-user",
            Priority = MessagePriority.Normal
        };

        MessageDelegate next = (msg, ct) => Task.FromResult(MessageResult.Ok());

        // Act
        await middleware.InvokeAsync(vipMessage, next);
        await middleware.InvokeAsync(regularMessage, next);

        // Assert
        Assert.Equal(MessagePriority.High, vipMessage.Priority); // Boosted
        Assert.Equal(MessagePriority.Normal, regularMessage.Priority); // Unchanged
    }

    [Fact]
    public async Task MetricsMiddleware_TracksStatistics()
    {
        // Arrange
        var middleware = new MetricsMiddleware();
        var message = new AgentMessage();

        var callCount = 0;
        MessageDelegate next = (msg, ct) =>
        {
            callCount++;
            return Task.FromResult(callCount <= 7
                ? MessageResult.Ok()
                : MessageResult.Fail("Error"));
        };

        // Act - process 10 messages (7 success, 3 failures)
        for (int i = 0; i < 10; i++)
        {
            await middleware.InvokeAsync(message, next);
        }

        var snapshot = middleware.GetSnapshot();

        // Assert
        Assert.Equal(10, snapshot.TotalMessages);
        Assert.Equal(7, snapshot.SuccessCount);
        Assert.Equal(3, snapshot.FailureCount);
        Assert.Equal(0.7, snapshot.SuccessRate, precision: 1);
    }

    [Fact]
    public async Task ConditionalMiddleware_OnlyExecutesWhenConditionMet()
    {
        // Arrange
        var executeCount = 0;
        var innerMiddleware = new CallbackMiddleware(before: _ => executeCount++);

        var conditionalMiddleware = new ConditionalMiddleware(
            msg => msg.Priority == MessagePriority.Urgent,
            innerMiddleware
        );

        MessageDelegate next = (msg, ct) => Task.FromResult(MessageResult.Ok());

        var urgentMessage = new AgentMessage { Priority = MessagePriority.Urgent };
        var normalMessage = new AgentMessage { Priority = MessagePriority.Normal };

        // Act
        await conditionalMiddleware.InvokeAsync(urgentMessage, next);
        await conditionalMiddleware.InvokeAsync(normalMessage, next);
        await conditionalMiddleware.InvokeAsync(urgentMessage, next);

        // Assert
        Assert.Equal(2, executeCount); // Only executed for urgent messages
    }

    [Fact]
    public async Task AgentRouterWithMiddleware_IntegratesMiddleware()
    {
        // Arrange
        var logger = new TestLogger();
        var router = new AgentRouter(logger);

        var validationCalled = false;
        router.UseMiddleware(new CallbackMiddleware(before: _ => validationCalled = true));

        var agent = new CustomerServiceAgent("cs-001", "CS", logger);
        router.RegisterAgent(agent);
        router.AddRoutingRule("DEFAULT", "Default", ctx => true, "cs-001");

        var message = new AgentMessage
        {
            SenderId = "user1",
            Subject = "Test",
            Content = "Test",
            Category = "CustomerService"
        };

        // Act
        await router.RouteMessageAsync(message);

        // Assert
        Assert.True(validationCalled);
    }

    [Fact]
    public async Task MiddlewarePipeline_ShortCircuits()
    {
        // Arrange
        var pipeline = new MiddlewarePipeline();

        var step1Executed = false;
        var step2Executed = false;
        var terminalExecuted = false;

        pipeline.UseCallback(before: _ => step1Executed = true);

        // This middleware short-circuits
        pipeline.Use(new CallbackMiddleware(
            before: _ =>
            {
                step2Executed = true;
                // Short circuit by not calling next
            }
        ));

        MessageDelegate terminal = (msg, ct) =>
        {
            terminalExecuted = true;
            return Task.FromResult(MessageResult.Ok());
        };

        // Actually, the CallbackMiddleware always calls next. Let me use a proper short-circuit middleware
        pipeline = new MiddlewarePipeline();
        pipeline.UseCallback(before: _ => step1Executed = true);

        pipeline.Use(new TestShortCircuitMiddleware(() =>
        {
            step2Executed = true;
        }));

        var message = new AgentMessage();

        // Act
        await pipeline.ExecuteAsync(message, terminal);

        // Assert
        Assert.True(step1Executed);
        Assert.True(step2Executed);
        Assert.False(terminalExecuted); // Short-circuited
    }

    private class TestShortCircuitMiddleware : MiddlewareBase
    {
        private readonly Action _action;

        public TestShortCircuitMiddleware(Action action)
        {
            _action = action;
        }

        public override Task<MessageResult> InvokeAsync(
            AgentMessage message,
            MessageDelegate next,
            CancellationToken ct)
        {
            _action();
            // Don't call next - short circuit
            return Task.FromResult(MessageResult.Fail("Short circuited"));
        }
    }
}
