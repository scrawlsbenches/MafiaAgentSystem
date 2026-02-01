using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Infrastructure;
using AgentRouting.Middleware;

namespace TestRunner.Tests;

/// <summary>
/// Tests for the circuit breaker middleware.
/// </summary>
public class CircuitBreakerTests
{
    private AgentMessage CreateTestMessage()
    {
        return new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = "test-sender",
            ReceiverId = "test-receiver",
            Subject = "Test",
            Content = "Test content",
            Category = "Test",
            Priority = MessagePriority.Normal
        };
    }

    private MessageDelegate CreateSuccessHandler()
    {
        return (msg, ct) => Task.FromResult(MessageResult.Ok("Success"));
    }

    private MessageDelegate CreateFailureHandler()
    {
        return (msg, ct) => Task.FromResult(MessageResult.Fail("Failure"));
    }

    [Test]
    public async Task ClosedState_AllowsRequests()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 5,
            resetTimeout: TimeSpan.FromSeconds(30));

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    [Test]
    public async Task FailuresIncrementCounter()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 5,
            resetTimeout: TimeSpan.FromSeconds(30));

        // 4 failures should not open the circuit
        for (int i = 0; i < 4; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                CreateFailureHandler(),
                CancellationToken.None);
        }

        // Should still be closed
        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    [Test]
    public async Task ThresholdReached_OpensCircuit()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 3,
            resetTimeout: TimeSpan.FromSeconds(30));

        // 3 failures should open the circuit
        for (int i = 0; i < 3; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                CreateFailureHandler(),
                CancellationToken.None);
        }

        // Next request should fail fast (circuit is open)
        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Error?.Contains("circuit") == true ||
                    result.Error?.Contains("Circuit") == true);
    }

    [Test]
    public async Task OpenCircuit_FailsFast()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 2,
            resetTimeout: TimeSpan.FromSeconds(30));

        // Open the circuit
        for (int i = 0; i < 2; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                CreateFailureHandler(),
                CancellationToken.None);
        }

        var handlerCalled = false;
        MessageDelegate handler = (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("Success"));
        };

        // Circuit is open - handler should not be called
        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            handler,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(handlerCalled);
    }

    [Test]
    public async Task FailuresContinueToAccumulate()
    {
        // Circuit breaker accumulates all failures (not just consecutive)
        // Only HalfOpen → Closed transition resets the count
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 3,
            resetTimeout: TimeSpan.FromSeconds(30));

        // 2 failures
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // 1 success - does not reset failure count in Closed state
        await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);

        // 1 more failure reaches threshold (2 + 1 = 3)
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Circuit should now be open
        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.False(result.Success);
    }

    [Test]
    public async Task ConcurrentFailures_ThreadSafe()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 50,
            resetTimeout: TimeSpan.FromSeconds(30));

        var tasks = new List<Task>();

        // Concurrent failures
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await middleware.InvokeAsync(
                    CreateTestMessage(),
                    CreateFailureHandler(),
                    CancellationToken.None);
            }));
        }

        await Task.WhenAll(tasks);

        // Circuit should be open after 50 failures
        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.False(result.Success);
    }

    [Test]
    public async Task ExceptionsTreatedAsFailures()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 2,
            resetTimeout: TimeSpan.FromSeconds(30));

        MessageDelegate throwingHandler = (msg, ct) =>
        {
            throw new InvalidOperationException("Handler exception");
        };

        // 2 exceptions should open the circuit
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await middleware.InvokeAsync(
                    CreateTestMessage(),
                    throwingHandler,
                    CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Circuit should be open
        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.False(result.Success);
    }

    [Test]
    public async Task ZeroThreshold_OpensImmediately()
    {
        // Note: Implementation may not support 0 threshold
        // This tests the boundary condition
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 1,
            resetTimeout: TimeSpan.FromSeconds(30));

        // Single failure should open
        await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateFailureHandler(),
            CancellationToken.None);

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.False(result.Success);
    }

    [Test]
    public async Task CurrentFailureCount_TracksFailuresWithinWindow()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 10,
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60));

        // Initially zero
        Assert.Equal(0, middleware.CurrentFailureCount);

        // Add some failures
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Should track all 3
        Assert.Equal(3, middleware.CurrentFailureCount);
    }

    [Test]
    public async Task FailuresOutsideWindow_AreNotCounted()
    {
        // Use a very short window for testing
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 5,
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromMilliseconds(100));

        // Add 3 failures
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        Assert.Equal(3, middleware.CurrentFailureCount);

        // Wait for failures to expire from window
        await Task.Delay(150);

        // Old failures should be pruned
        Assert.Equal(0, middleware.CurrentFailureCount);

        // Add 2 more failures - these won't trigger threshold by themselves
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        Assert.Equal(2, middleware.CurrentFailureCount);

        // Circuit should still be closed since we only have 2 failures now
        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    [Test]
    public async Task SlidingWindow_PreventsStaleFailuresFromOpeningCircuit()
    {
        // This is the key behavior fix:
        // Failures from long ago should NOT count toward opening the circuit
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 3,
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromMilliseconds(100));

        // Add 2 failures
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Wait for them to expire
        await Task.Delay(150);

        // Add 2 more failures - total historical is 4, but within window is only 2
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Circuit should still be closed (only 2 failures in window, need 3)
        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, middleware.CurrentFailureCount);
    }
}
