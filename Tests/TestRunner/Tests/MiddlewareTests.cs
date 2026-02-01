using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;

namespace TestRunner.Tests;

/// <summary>
/// Tests for middleware pipeline and core middleware components
/// </summary>
public class MiddlewareTests
{
    #region MiddlewarePipeline Tests

    [Test]
    public async Task Pipeline_EmptyPipeline_CallsFinalHandler()
    {
        var pipeline = new MiddlewarePipeline();
        var handlerCalled = false;

        var builtPipeline = pipeline.Build(async (msg, ct) =>
        {
            handlerCalled = true;
            return MessageResult.Ok("Final handler");
        });

        var message = CreateTestMessage();
        var result = await builtPipeline(message, CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task Pipeline_SingleMiddleware_ExecutesInOrder()
    {
        var pipeline = new MiddlewarePipeline();
        var executionOrder = new List<string>();

        pipeline.Use(new TrackingMiddleware("First", executionOrder));

        var builtPipeline = pipeline.Build(async (msg, ct) =>
        {
            executionOrder.Add("Handler");
            return MessageResult.Ok("Done");
        });

        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("First-Before", executionOrder[0]);
        Assert.Equal("Handler", executionOrder[1]);
        Assert.Equal("First-After", executionOrder[2]);
    }

    [Test]
    public async Task Pipeline_MultipleMiddleware_ExecutesInCorrectOrder()
    {
        var pipeline = new MiddlewarePipeline();
        var executionOrder = new List<string>();

        pipeline.Use(new TrackingMiddleware("First", executionOrder));
        pipeline.Use(new TrackingMiddleware("Second", executionOrder));
        pipeline.Use(new TrackingMiddleware("Third", executionOrder));

        var builtPipeline = pipeline.Build(async (msg, ct) =>
        {
            executionOrder.Add("Handler");
            return MessageResult.Ok("Done");
        });

        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        // First added = first executed (wraps outer)
        Assert.Equal("First-Before", executionOrder[0]);
        Assert.Equal("Second-Before", executionOrder[1]);
        Assert.Equal("Third-Before", executionOrder[2]);
        Assert.Equal("Handler", executionOrder[3]);
        Assert.Equal("Third-After", executionOrder[4]);
        Assert.Equal("Second-After", executionOrder[5]);
        Assert.Equal("First-After", executionOrder[6]);
    }

    [Test]
    public async Task Pipeline_MiddlewareCanShortCircuit()
    {
        var pipeline = new MiddlewarePipeline();
        var handlerCalled = false;

        pipeline.Use(new ShortCircuitMiddleware("Blocked"));

        var builtPipeline = pipeline.Build(async (msg, ct) =>
        {
            handlerCalled = true;
            return MessageResult.Ok("Should not reach");
        });

        var result = await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.False(handlerCalled);
        Assert.False(result.Success);
        Assert.Contains("Blocked", result.Error!);
    }

    [Test]
    public async Task Pipeline_DelegateMiddleware_Works()
    {
        var pipeline = new MiddlewarePipeline();
        var middlewareCalled = false;

        pipeline.Use(next => async (msg, ct) =>
        {
            middlewareCalled = true;
            return await next(msg, ct);
        });

        var builtPipeline = pipeline.Build(async (msg, ct) => MessageResult.Ok("Done"));
        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.True(middlewareCalled);
    }

    #endregion

    #region ValidationMiddleware Tests

    [Test]
    public async Task ValidationMiddleware_ValidMessage_PassesThrough()
    {
        var middleware = new ValidationMiddleware();
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) =>
            {
                nextCalled = true;
                return MessageResult.Ok("Passed");
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task ValidationMiddleware_MissingSenderId_ShortCircuits()
    {
        var middleware = new ValidationMiddleware();
        var message = new AgentMessage
        {
            SenderId = "",
            Subject = "Test",
            Content = "Content"
        };

        var result = await middleware.InvokeAsync(
            message,
            async (msg, ct) => MessageResult.Ok("Should not reach"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("SenderId", result.Error!);
    }

    [Test]
    public async Task ValidationMiddleware_MissingSubject_ShortCircuits()
    {
        var middleware = new ValidationMiddleware();
        var message = new AgentMessage
        {
            SenderId = "sender-1",
            Subject = "",
            Content = "Content"
        };

        var result = await middleware.InvokeAsync(
            message,
            async (msg, ct) => MessageResult.Ok("Should not reach"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Subject", result.Error!);
    }

    [Test]
    public async Task ValidationMiddleware_MissingContent_ShortCircuits()
    {
        var middleware = new ValidationMiddleware();
        var message = new AgentMessage
        {
            SenderId = "sender-1",
            Subject = "Test",
            Content = ""
        };

        var result = await middleware.InvokeAsync(
            message,
            async (msg, ct) => MessageResult.Ok("Should not reach"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Content", result.Error!);
    }

    #endregion

    #region RateLimitMiddleware Tests

    [Test]
    public async Task RateLimitMiddleware_UnderLimit_AllowsRequests()
    {
        var middleware = new RateLimitMiddleware(5, TimeSpan.FromMinutes(1));
        var message = CreateTestMessage();

        for (int i = 0; i < 5; i++)
        {
            var result = await middleware.InvokeAsync(
                message,
                async (msg, ct) => MessageResult.Ok("Allowed"),
                CancellationToken.None);

            Assert.True(result.Success, $"Request {i + 1} should be allowed");
        }
    }

    [Test]
    public async Task RateLimitMiddleware_OverLimit_BlocksRequests()
    {
        var middleware = new RateLimitMiddleware(3, TimeSpan.FromMinutes(1));
        var message = CreateTestMessage();

        // Use up the limit
        for (int i = 0; i < 3; i++)
        {
            await middleware.InvokeAsync(
                message,
                async (msg, ct) => MessageResult.Ok("Allowed"),
                CancellationToken.None);
        }

        // This should be blocked
        var result = await middleware.InvokeAsync(
            message,
            async (msg, ct) => MessageResult.Ok("Should not reach"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Rate limit exceeded", result.Error!);
    }

    [Test]
    public async Task RateLimitMiddleware_DifferentSenders_IndependentLimits()
    {
        var middleware = new RateLimitMiddleware(2, TimeSpan.FromMinutes(1));

        var message1 = CreateTestMessage("sender-1");
        var message2 = CreateTestMessage("sender-2");

        // Exhaust limit for sender-1
        await middleware.InvokeAsync(message1, async (msg, ct) => MessageResult.Ok("OK"), CancellationToken.None);
        await middleware.InvokeAsync(message1, async (msg, ct) => MessageResult.Ok("OK"), CancellationToken.None);

        // sender-2 should still be allowed
        var result = await middleware.InvokeAsync(
            message2,
            async (msg, ct) => MessageResult.Ok("Allowed"),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    #endregion

    #region CachingMiddleware Tests

    [Test]
    public async Task CachingMiddleware_FirstRequest_CallsHandler()
    {
        var middleware = new CachingMiddleware(TimeSpan.FromMinutes(5));
        var handlerCallCount = 0;

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) =>
            {
                handlerCallCount++;
                return MessageResult.Ok("Fresh");
            },
            CancellationToken.None);

        Assert.Equal(1, handlerCallCount);
        Assert.Equal("Fresh", result.Response);
    }

    [Test]
    public async Task CachingMiddleware_SecondRequest_ReturnsCached()
    {
        var middleware = new CachingMiddleware(TimeSpan.FromMinutes(5));
        var handlerCallCount = 0;
        var message = CreateTestMessage();

        // First call
        await middleware.InvokeAsync(
            message,
            async (msg, ct) =>
            {
                handlerCallCount++;
                return MessageResult.Ok("Fresh");
            },
            CancellationToken.None);

        // Second call with same message
        var result = await middleware.InvokeAsync(
            CreateTestMessage(), // Same content
            async (msg, ct) =>
            {
                handlerCallCount++;
                return MessageResult.Ok("Should not reach");
            },
            CancellationToken.None);

        Assert.Equal(1, handlerCallCount); // Handler only called once
        Assert.Equal("Fresh", result.Response); // Cached result returned
    }

    [Test]
    public async Task CachingMiddleware_FailedResult_NotCached()
    {
        var middleware = new CachingMiddleware(TimeSpan.FromMinutes(5));
        var handlerCallCount = 0;
        var message = CreateTestMessage();

        // First call fails
        await middleware.InvokeAsync(
            message,
            async (msg, ct) =>
            {
                handlerCallCount++;
                return MessageResult.Fail("Error");
            },
            CancellationToken.None);

        // Second call should hit handler again
        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) =>
            {
                handlerCallCount++;
                return MessageResult.Ok("Success");
            },
            CancellationToken.None);

        Assert.Equal(2, handlerCallCount); // Both calls hit handler
    }

    [Test]
    public void CachingMiddleware_Clear_RemovesAllEntries()
    {
        var middleware = new CachingMiddleware(TimeSpan.FromMinutes(5));

        // Add some entries
        middleware.InvokeAsync(
            CreateTestMessage("sender-1"),
            async (msg, ct) => MessageResult.Ok("OK"),
            CancellationToken.None).Wait();

        middleware.InvokeAsync(
            CreateTestMessage("sender-2"),
            async (msg, ct) => MessageResult.Ok("OK"),
            CancellationToken.None).Wait();

        Assert.True(middleware.Count > 0);

        middleware.Clear();

        Assert.Equal(0, middleware.Count);
    }

    #endregion

    #region CircuitBreakerMiddleware Tests

    [Test]
    public async Task CircuitBreaker_InitiallyClosed_AllowsRequests()
    {
        var middleware = new CircuitBreakerMiddleware(3, TimeSpan.FromSeconds(30));

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) => MessageResult.Ok("Success"),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    [Test]
    public async Task CircuitBreaker_FailuresBelowThreshold_StaysClosed()
    {
        var middleware = new CircuitBreakerMiddleware(3, TimeSpan.FromSeconds(30));

        // 2 failures (below threshold of 3)
        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) => MessageResult.Fail("Error"),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) => MessageResult.Fail("Error"),
            CancellationToken.None);

        // Should still allow requests
        var nextCalled = false;
        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) =>
            {
                nextCalled = true;
                return MessageResult.Ok("Success");
            },
            CancellationToken.None);

        Assert.True(nextCalled);
    }

    [Test]
    public async Task CircuitBreaker_FailuresReachThreshold_Opens()
    {
        var middleware = new CircuitBreakerMiddleware(3, TimeSpan.FromSeconds(30));

        // 3 failures (reaches threshold)
        for (int i = 0; i < 3; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                async (msg, ct) => MessageResult.Fail("Error"),
                CancellationToken.None);
        }

        // Next request should be blocked
        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) => MessageResult.Ok("Should not reach"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Circuit breaker is OPEN", result.Error!);
    }

    [Test]
    public async Task CircuitBreaker_ExceptionCountsAsFailure()
    {
        var middleware = new CircuitBreakerMiddleware(2, TimeSpan.FromSeconds(30));

        // 2 exceptions (reaches threshold)
        for (int i = 0; i < 2; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => throw new InvalidOperationException("Test exception"),
                CancellationToken.None);
        }

        // Circuit should be open
        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) => MessageResult.Ok("Should not reach"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Circuit breaker is OPEN", result.Error!);
    }

    #endregion

    #region TimingMiddleware Tests

    [Test]
    public async Task TimingMiddleware_AddsProcessingTime()
    {
        var middleware = new TimingMiddleware();

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) =>
            {
                await Task.Delay(10); // Small delay
                return MessageResult.Ok("Done");
            },
            CancellationToken.None);

        Assert.True(result.Data.ContainsKey("ProcessingTimeMs"));
        var time = (long)result.Data["ProcessingTimeMs"];
        Assert.True(time >= 0);
    }

    #endregion

    #region MetricsMiddleware Tests

    [Test]
    public async Task MetricsMiddleware_TracksSuccessAndFailure()
    {
        var middleware = new MetricsMiddleware();

        // 2 successes
        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) => MessageResult.Ok("OK"),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) => MessageResult.Ok("OK"),
            CancellationToken.None);

        // 1 failure
        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) => MessageResult.Fail("Error"),
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(3, snapshot.TotalMessages);
        Assert.Equal(2, snapshot.SuccessCount);
        Assert.Equal(1, snapshot.FailureCount);
    }

    [Test]
    public async Task MetricsMiddleware_CalculatesSuccessRate()
    {
        var middleware = new MetricsMiddleware();

        // 3 successes, 1 failure = 75% success rate
        for (int i = 0; i < 3; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                async (msg, ct) => MessageResult.Ok("OK"),
                CancellationToken.None);
        }

        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) => MessageResult.Fail("Error"),
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(0.75, snapshot.SuccessRate);
    }

    #endregion

    #region AuthenticationMiddleware Tests

    [Test]
    public async Task AuthenticationMiddleware_AuthenticatedSender_Allowed()
    {
        var middleware = new AuthenticationMiddleware("admin", "user1");

        var result = await middleware.InvokeAsync(
            CreateTestMessage("admin"),
            async (msg, ct) => MessageResult.Ok("Authenticated"),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    [Test]
    public async Task AuthenticationMiddleware_UnauthenticatedSender_Blocked()
    {
        var middleware = new AuthenticationMiddleware("admin", "user1");

        var result = await middleware.InvokeAsync(
            CreateTestMessage("hacker"),
            async (msg, ct) => MessageResult.Ok("Should not reach"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("not authenticated", result.Error!);
    }

    [Test]
    public async Task AuthenticationMiddleware_CaseInsensitive()
    {
        var middleware = new AuthenticationMiddleware("Admin");

        var result = await middleware.InvokeAsync(
            CreateTestMessage("ADMIN"),
            async (msg, ct) => MessageResult.Ok("Authenticated"),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    #endregion

    #region MiddlewareContext Tests

    [Test]
    public void MiddlewareContext_SetAndGet_Works()
    {
        var context = new MiddlewareContext();

        context.Set("key1", "value1");
        context.Set("key2", 42);

        Assert.Equal("value1", context.Get<string>("key1"));
        Assert.Equal(42, context.Get<int>("key2"));
    }

    [Test]
    public void MiddlewareContext_TryGet_ReturnsFalseForMissing()
    {
        var context = new MiddlewareContext();

        var found = context.TryGet<string>("missing", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Test]
    public void MessageContextExtensions_GetContext_CreatesSingleInstance()
    {
        var message = CreateTestMessage();

        var context1 = message.GetContext();
        var context2 = message.GetContext();

        Assert.Same(context1, context2);
    }

    #endregion

    #region Helper Methods and Classes

    private static AgentMessage CreateTestMessage(string senderId = "test-sender")
    {
        return new AgentMessage
        {
            SenderId = senderId,
            Subject = "Test Subject",
            Content = "Test Content",
            Category = "Test"
        };
    }

    private class TrackingMiddleware : MiddlewareBase
    {
        private readonly string _name;
        private readonly List<string> _executionOrder;

        public TrackingMiddleware(string name, List<string> executionOrder)
        {
            _name = name;
            _executionOrder = executionOrder;
        }

        public override async Task<MessageResult> InvokeAsync(
            AgentMessage message,
            MessageDelegate next,
            CancellationToken ct)
        {
            _executionOrder.Add($"{_name}-Before");
            var result = await next(message, ct);
            _executionOrder.Add($"{_name}-After");
            return result;
        }
    }

    private class ShortCircuitMiddleware : MiddlewareBase
    {
        private readonly string _reason;

        public ShortCircuitMiddleware(string reason)
        {
            _reason = reason;
        }

        public override Task<MessageResult> InvokeAsync(
            AgentMessage message,
            MessageDelegate next,
            CancellationToken ct)
        {
            return Task.FromResult(ShortCircuit(_reason));
        }
    }

    #endregion
}
