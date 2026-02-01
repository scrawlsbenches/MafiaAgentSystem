using TestRunner.Framework;
using AgentRouting.Core;
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
        var middleware = new RateLimitMiddleware(maxRequests: 10, window: TimeSpan.FromMinutes(1));

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
        var middleware = new RateLimitMiddleware(maxRequests: 3, window: TimeSpan.FromMinutes(1));

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
        var middleware = new RateLimitMiddleware(maxRequests: 2, window: TimeSpan.FromMinutes(1));

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
        var middleware = new RateLimitMiddleware(maxRequests: 100, window: TimeSpan.FromMinutes(1));
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
        var middleware = new RateLimitMiddleware(maxRequests: 0, window: TimeSpan.FromMinutes(1));

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.False(result.Success);
    }

    [Test]
    public async Task LargeLimit_AllowsManyRequests()
    {
        var middleware = new RateLimitMiddleware(maxRequests: 10000, window: TimeSpan.FromMinutes(1));

        for (int i = 0; i < 1000; i++)
        {
            var result = await middleware.InvokeAsync(
                CreateTestMessage(),
                CreateSuccessHandler(),
                CancellationToken.None);

            Assert.True(result.Success);
        }
    }
}
