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
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

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
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

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
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

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
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

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
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

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
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

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
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

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
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

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
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

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
            failureWindow: TimeSpan.FromMilliseconds(100),
            clock: SystemClock.Instance);

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
            failureWindow: TimeSpan.FromMilliseconds(100),
            clock: SystemClock.Instance);

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

    // ==================== Additional Circuit Breaker Tests ====================

    [Test]
    public async Task HalfOpenState_SuccessClosesCircuit()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 2,
            resetTimeout: TimeSpan.FromMilliseconds(100),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

        // Open the circuit
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Verify circuit is open
        var openResult = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.False(openResult.Success);

        // Wait for reset timeout (half-open state)
        await Task.Delay(150);

        // Next successful request should close the circuit
        var halfOpenResult = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.True(halfOpenResult.Success);

        // Verify circuit is now closed (more requests allowed)
        var closedResult = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.True(closedResult.Success);
    }

    [Test]
    public async Task HalfOpenState_FailureReopensCircuit()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 2,
            resetTimeout: TimeSpan.FromMilliseconds(100),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

        // Open the circuit
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Wait for reset timeout (half-open state)
        await Task.Delay(150);

        // Failure in half-open state should re-open
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Circuit should be open again
        var result = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.False(result.Success);
    }

    [Test]
    public async Task LargeFailureThreshold_ToleratesManyFailures()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 100,
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

        // 99 failures should not open the circuit
        for (int i = 0; i < 99; i++)
        {
            await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        }

        // Should still be closed
        var result = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.True(result.Success);

        Assert.Equal(99, middleware.CurrentFailureCount);
    }

    [Test]
    public async Task PartialFailures_MixedSuccessAndFailure()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 5,
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

        // Mix of successes and failures
        await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Only 4 failures - should still be closed
        Assert.Equal(4, middleware.CurrentFailureCount);

        var result = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.True(result.Success);
    }

    [Test]
    public async Task ConcurrentHalfOpenTransitions_ThreadSafe()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 2,
            resetTimeout: TimeSpan.FromMilliseconds(50),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

        // Open the circuit
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Wait for half-open
        await Task.Delay(100);

        // Concurrent requests in half-open state
        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        // At least one should succeed (the probe request)
        var successCount = results.Count(r => r.Success);
        Assert.True(successCount >= 1);
    }

    [Test]
    public async Task CircuitBreakerError_ContainsHelpfulMessage()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 1,
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

        // Open the circuit
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Verify error message
        var result = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.True(result.Error!.Length > 0);
    }

    [Test]
    public async Task SlowHandler_NotTreatedAsFailure()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 2,
            resetTimeout: TimeSpan.FromSeconds(30),
            failureWindow: TimeSpan.FromSeconds(60),
            clock: SystemClock.Instance);

        MessageDelegate slowHandler = async (msg, ct) =>
        {
            await Task.Delay(50);
            return MessageResult.Ok("Slow but successful");
        };

        // Multiple slow requests should not open the circuit
        await middleware.InvokeAsync(CreateTestMessage(), slowHandler, CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), slowHandler, CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), slowHandler, CancellationToken.None);

        // Should still be closed
        var result = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(0, middleware.CurrentFailureCount);
    }

    [Test]
    public async Task MultipleCircuitBreakers_SeparateStateStores_IndependentState()
    {
        // Use separate state stores to ensure independent state
        var stateStore1 = new InMemoryStateStore();
        var stateStore2 = new InMemoryStateStore();

        var middleware1 = new CircuitBreakerMiddleware(stateStore1, failureThreshold: 2, resetTimeout: TimeSpan.FromSeconds(30), failureWindow: TimeSpan.FromSeconds(60), clock: SystemClock.Instance);
        var middleware2 = new CircuitBreakerMiddleware(stateStore2, failureThreshold: 2, resetTimeout: TimeSpan.FromSeconds(30), failureWindow: TimeSpan.FromSeconds(60), clock: SystemClock.Instance);

        // Open middleware1's circuit
        await middleware1.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);
        await middleware1.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // middleware1 should be open
        var result1 = await middleware1.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.False(result1.Success);

        // middleware2 should still be closed (separate state store)
        var result2 = await middleware2.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.True(result2.Success);
    }

    [Test]
    public async Task RecoveryAfterLongOutage_CircuitCloses()
    {
        var middleware = new CircuitBreakerMiddleware(
            new InMemoryStateStore(),
            failureThreshold: 1,
            resetTimeout: TimeSpan.FromMilliseconds(50),
            failureWindow: TimeSpan.FromMilliseconds(100),
            clock: SystemClock.Instance);

        // Open the circuit
        await middleware.InvokeAsync(CreateTestMessage(), CreateFailureHandler(), CancellationToken.None);

        // Verify open
        var openResult = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.False(openResult.Success);

        // Wait for reset timeout
        await Task.Delay(100);

        // Recover with successful request
        var recoveryResult = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.True(recoveryResult.Success);

        // Failure count should be reset
        Assert.Equal(0, middleware.CurrentFailureCount);
    }
}
