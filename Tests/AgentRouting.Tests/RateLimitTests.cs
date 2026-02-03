using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Infrastructure;
using AgentRouting.Middleware;

namespace TestRunner.Tests;

/// <summary>
/// Tests for the rate limiting middleware.
/// </summary>
public class RateLimitTests
{
    private AgentMessage CreateTestMessage(string senderId = "test-sender")
    {
        return new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = senderId,
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

    [Test]
    public async Task UnderLimit_AllowsRequests()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 10, window: TimeSpan.FromMinutes(1), SystemClock.Instance);

        for (int i = 0; i < 5; i++)
        {
            var result = await middleware.InvokeAsync(
                CreateTestMessage(),
                CreateSuccessHandler(),
                CancellationToken.None);

            Assert.True(result.Success, $"Request {i + 1} should succeed");
        }
    }

    [Test]
    public async Task AtLimit_BlocksRequests()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 3, window: TimeSpan.FromMinutes(1), SystemClock.Instance);

        // First 3 requests should succeed
        for (int i = 0; i < 3; i++)
        {
            var result = await middleware.InvokeAsync(
                CreateTestMessage(),
                CreateSuccessHandler(),
                CancellationToken.None);

            Assert.True(result.Success, $"Request {i + 1} should succeed");
        }

        // 4th request should be blocked
        var blocked = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.False(blocked.Success);
        Assert.True(blocked.Error?.Contains("rate limit") == true ||
                    blocked.Error?.Contains("Rate limit") == true);
    }

    [Test]
    public async Task DifferentSenders_HaveSeparateLimits()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 2, window: TimeSpan.FromMinutes(1), SystemClock.Instance);

        // Sender A uses both requests
        for (int i = 0; i < 2; i++)
        {
            var result = await middleware.InvokeAsync(
                CreateTestMessage("sender-A"),
                CreateSuccessHandler(),
                CancellationToken.None);

            Assert.True(result.Success);
        }

        // Sender A is now blocked
        var blockedA = await middleware.InvokeAsync(
            CreateTestMessage("sender-A"),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.False(blockedA.Success);

        // But Sender B can still make requests
        var allowedB = await middleware.InvokeAsync(
            CreateTestMessage("sender-B"),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.True(allowedB.Success);
    }

    [Test]
    public async Task ConcurrentRequests_ThreadSafe()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 100, window: TimeSpan.FromMinutes(1), SystemClock.Instance);
        var successCount = 0;
        var failCount = 0;

        var tasks = new List<Task>();

        for (int i = 0; i < 150; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var result = await middleware.InvokeAsync(
                    CreateTestMessage(),
                    CreateSuccessHandler(),
                    CancellationToken.None);

                if (result.Success)
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failCount);
            }));
        }

        await Task.WhenAll(tasks);

        // Should have exactly 100 successes and 50 failures
        Assert.Equal(100, successCount);
        Assert.Equal(50, failCount);
    }

    [Test]
    public async Task ZeroLimit_BlocksAllRequests()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 0, window: TimeSpan.FromMinutes(1), SystemClock.Instance);

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.False(result.Success);
    }

    [Test]
    public async Task LargeLimit_AllowsManyRequests()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 10000, window: TimeSpan.FromMinutes(1), SystemClock.Instance);

        for (int i = 0; i < 1000; i++)
        {
            var result = await middleware.InvokeAsync(
                CreateTestMessage(),
                CreateSuccessHandler(),
                CancellationToken.None);

            Assert.True(result.Success);
        }
    }

    // ==================== Additional Rate Limiter Tests ====================

    [Test]
    public async Task VeryShortWindow_ResetsQuickly()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 2, window: TimeSpan.FromMilliseconds(100), SystemClock.Instance);

        // Use up the limit
        await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);

        // Should be blocked
        var blocked = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.False(blocked.Success);

        // Wait for window to expire
        await Task.Delay(150);

        // Should be allowed again
        var allowed = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        Assert.True(allowed.Success);
    }

    [Test]
    public async Task EmptySenderId_UsesEmptyAsKey()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 2, window: TimeSpan.FromMinutes(1), SystemClock.Instance);

        // Create messages with empty sender
        var emptyMessage = new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = "",
            ReceiverId = "test-receiver",
            Subject = "Test",
            Content = "Test content"
        };

        var result1 = await middleware.InvokeAsync(emptyMessage, CreateSuccessHandler(), CancellationToken.None);
        var result2 = await middleware.InvokeAsync(emptyMessage, CreateSuccessHandler(), CancellationToken.None);
        var result3 = await middleware.InvokeAsync(emptyMessage, CreateSuccessHandler(), CancellationToken.None);

        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.False(result3.Success); // Blocked - empty sender counts as one sender
    }

    [Test]
    public async Task OneRequestLimit_BlocksSecondRequest()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 1, window: TimeSpan.FromMinutes(1), SystemClock.Instance);

        var first = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        var second = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);

        Assert.True(first.Success);
        Assert.False(second.Success);
    }

    [Test]
    public async Task MultipleWindows_IndependentTracking()
    {
        // Test with different senders, each with their own window
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 2, window: TimeSpan.FromMinutes(1), SystemClock.Instance);

        // Sender A uses up limit
        await middleware.InvokeAsync(CreateTestMessage("A"), CreateSuccessHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage("A"), CreateSuccessHandler(), CancellationToken.None);

        // Sender B uses up limit
        await middleware.InvokeAsync(CreateTestMessage("B"), CreateSuccessHandler(), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage("B"), CreateSuccessHandler(), CancellationToken.None);

        // Both should be blocked
        var blockedA = await middleware.InvokeAsync(CreateTestMessage("A"), CreateSuccessHandler(), CancellationToken.None);
        var blockedB = await middleware.InvokeAsync(CreateTestMessage("B"), CreateSuccessHandler(), CancellationToken.None);

        // Sender C should still work
        var allowedC = await middleware.InvokeAsync(CreateTestMessage("C"), CreateSuccessHandler(), CancellationToken.None);

        Assert.False(blockedA.Success);
        Assert.False(blockedB.Success);
        Assert.True(allowedC.Success);
    }

    [Test]
    public async Task HandlerFailure_StillCountsAsRequest()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 2, window: TimeSpan.FromMinutes(1), SystemClock.Instance);

        MessageDelegate failingHandler = (msg, ct) => Task.FromResult(MessageResult.Fail("Handler failed"));

        // Two failing requests should still count against the limit
        await middleware.InvokeAsync(CreateTestMessage(), failingHandler, CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), failingHandler, CancellationToken.None);

        // Third request should be rate limited
        var result = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Error?.Contains("rate limit") == true || result.Error?.Contains("Rate limit") == true);
    }

    [Test]
    public async Task ExtremelyHighConcurrency_MaintainsCorrectLimits()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 50, window: TimeSpan.FromMinutes(1), SystemClock.Instance);
        var successCount = 0;
        var failCount = 0;

        var tasks = new List<Task>();

        // Launch 200 concurrent requests
        for (int i = 0; i < 200; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var result = await middleware.InvokeAsync(
                    CreateTestMessage(),
                    CreateSuccessHandler(),
                    CancellationToken.None);

                if (result.Success)
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failCount);
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(50, successCount);
        Assert.Equal(150, failCount);
    }

    [Test]
    public async Task RateLimitError_ContainsHelpfulMessage()
    {
        var middleware = new RateLimitMiddleware(new InMemoryStateStore(), maxRequests: 1, window: TimeSpan.FromMinutes(1), SystemClock.Instance);

        await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);
        var blocked = await middleware.InvokeAsync(CreateTestMessage(), CreateSuccessHandler(), CancellationToken.None);

        Assert.False(blocked.Success);
        Assert.NotNull(blocked.Error);
        Assert.True(blocked.Error!.Length > 0);
    }
}
