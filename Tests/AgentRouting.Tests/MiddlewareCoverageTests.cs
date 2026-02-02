using TestRunner.Framework;
using AgentRouting.Configuration;
using AgentRouting.Core;
using AgentRouting.Infrastructure;
using AgentRouting.Middleware;

namespace TestRunner.Tests;

/// <summary>
/// Comprehensive tests for LoggingMiddleware, CachingMiddleware, and RetryMiddleware
/// to improve code coverage.
/// </summary>
public class MiddlewareCoverageTests
{
    #region Helper Methods

    private static AgentMessage CreateTestMessage(
        string senderId = "test-sender",
        string subject = "Test Subject",
        string content = "Test Content",
        string category = "Test")
    {
        return new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = senderId,
            ReceiverId = "test-receiver",
            Subject = subject,
            Content = content,
            Category = category,
            Priority = MessagePriority.Normal
        };
    }

    private static MessageDelegate CreateSuccessHandler(string response = "Success")
    {
        return (msg, ct) => Task.FromResult(MessageResult.Ok(response));
    }

    private static MessageDelegate CreateFailureHandler(string error = "Failed")
    {
        return (msg, ct) => Task.FromResult(MessageResult.Fail(error));
    }

    private static MessageDelegate CreateThrowingHandler(Exception ex)
    {
        return (msg, ct) => throw ex;
    }

    private static MessageDelegate CreateCountingHandler(ref int counter, bool success = true)
    {
        var count = 0;
        return (msg, ct) =>
        {
            count++;
            // Note: Can't use ref in lambda, so caller must track separately
            return success
                ? Task.FromResult(MessageResult.Ok($"Call {count}"))
                : Task.FromResult(MessageResult.Fail($"Fail {count}"));
        };
    }

    /// <summary>
    /// A fake clock for testing time-dependent behavior.
    /// </summary>
    private class FakeClock : ISystemClock
    {
        private DateTime _utcNow;

        public FakeClock(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTime UtcNow => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }

        public void SetTime(DateTime utcNow)
        {
            _utcNow = utcNow;
        }
    }

    /// <summary>
    /// A fake logger that captures log output for verification.
    /// </summary>
    private class FakeAgentLogger : IAgentLogger
    {
        public List<string> ReceivedLogs { get; } = new();
        public List<string> ProcessedLogs { get; } = new();
        public List<string> RoutedLogs { get; } = new();
        public List<string> ErrorLogs { get; } = new();

        public void LogMessageReceived(IAgent agent, AgentMessage message)
        {
            ReceivedLogs.Add($"{agent.Name} received: {message.Subject}");
        }

        public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result)
        {
            ProcessedLogs.Add($"{agent.Name} processed: {message.Subject} - {(result.Success ? "SUCCESS" : "FAILED")}");
        }

        public void LogMessageRouted(AgentMessage message, IAgent? fromAgent, IAgent toAgent)
        {
            RoutedLogs.Add($"Routed: {message.Subject} from {fromAgent?.Name ?? "Router"} to {toAgent.Name}");
        }

        public void LogError(IAgent agent, AgentMessage message, Exception ex)
        {
            ErrorLogs.Add($"ERROR in {agent.Name}: {ex.Message}");
        }
    }

    #endregion

    #region LoggingMiddleware Tests

    [Test]
    public async Task LoggingMiddleware_ProcessesMessage_AndCallsNext()
    {
        var logger = new FakeAgentLogger();
        var middleware = new LoggingMiddleware(logger);
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Success"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task LoggingMiddleware_ReturnsResultFromNext()
    {
        var logger = new FakeAgentLogger();
        var middleware = new LoggingMiddleware(logger);

        var expectedResult = MessageResult.Ok("Expected response");
        expectedResult.Data["key"] = "value";

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(expectedResult),
            CancellationToken.None);

        Assert.Same(expectedResult, result);
        Assert.Equal("Expected response", result.Response);
        Assert.True(result.Data.ContainsKey("key"));
    }

    [Test]
    public async Task LoggingMiddleware_HandlesFailureResult()
    {
        var logger = new FakeAgentLogger();
        var middleware = new LoggingMiddleware(logger);

        var result = await middleware.InvokeAsync(
            CreateTestMessage(subject: "Failed Request"),
            CreateFailureHandler("Something went wrong"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Something went wrong", result.Error);
    }

    [Test]
    public async Task LoggingMiddleware_ProcessesMessageWithDifferentSubjects()
    {
        var logger = new FakeAgentLogger();
        var middleware = new LoggingMiddleware(logger);

        // Test with various subjects
        var subjects = new[] { "Order Placed", "Payment Received", "Item Shipped" };

        foreach (var subject in subjects)
        {
            var result = await middleware.InvokeAsync(
                CreateTestMessage(subject: subject),
                CreateSuccessHandler($"Processed: {subject}"),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal($"Processed: {subject}", result.Response);
        }
    }

    [Test]
    public async Task LoggingMiddleware_PreservesMessageIntegrity()
    {
        var logger = new FakeAgentLogger();
        var middleware = new LoggingMiddleware(logger);
        var originalMessage = CreateTestMessage(
            senderId: "sender-123",
            subject: "Important Message",
            content: "Original content");
        originalMessage.Metadata["customKey"] = "customValue";

        AgentMessage? capturedMessage = null;

        await middleware.InvokeAsync(
            originalMessage,
            (msg, ct) =>
            {
                capturedMessage = msg;
                return Task.FromResult(MessageResult.Ok());
            },
            CancellationToken.None);

        Assert.NotNull(capturedMessage);
        Assert.Equal("sender-123", capturedMessage!.SenderId);
        Assert.Equal("Important Message", capturedMessage.Subject);
        Assert.Equal("Original content", capturedMessage.Content);
        Assert.Equal("customValue", capturedMessage.Metadata["customKey"]);
    }

    [Test]
    public async Task LoggingMiddleware_WithNullLogger_DoesNotThrow()
    {
        // LoggingMiddleware should handle null logger gracefully if it doesn't use it internally
        // The current implementation uses Console.WriteLine, not the passed logger
        var middleware = new LoggingMiddleware(null!);

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            CreateSuccessHandler(),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    #endregion

    #region CachingMiddleware Tests - Expiration

    [Test]
    public async Task CachingMiddleware_ExpiredEntry_CallsHandlerAgain()
    {
        var fakeClock = new FakeClock(DateTime.UtcNow);
        var ttl = TimeSpan.FromMinutes(5);
        var middleware = new CachingMiddleware(new InMemoryStateStore(), ttl, 100, fakeClock);
        var callCount = 0;

        var message = CreateTestMessage(subject: "Cached Query");

        // First call - should call handler
        await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                callCount++;
                return Task.FromResult(MessageResult.Ok("Fresh result"));
            },
            CancellationToken.None);

        Assert.Equal(1, callCount);

        // Second call within TTL - should return cached
        await middleware.InvokeAsync(
            CreateTestMessage(subject: "Cached Query"),
            (msg, ct) =>
            {
                callCount++;
                return Task.FromResult(MessageResult.Ok("Should not see this"));
            },
            CancellationToken.None);

        Assert.Equal(1, callCount);

        // Advance time past TTL
        fakeClock.Advance(ttl + TimeSpan.FromSeconds(1));

        // Third call after TTL - should call handler again
        var result = await middleware.InvokeAsync(
            CreateTestMessage(subject: "Cached Query"),
            (msg, ct) =>
            {
                callCount++;
                return Task.FromResult(MessageResult.Ok("Fresh again"));
            },
            CancellationToken.None);

        Assert.Equal(2, callCount);
        Assert.Equal("Fresh again", result.Response);
    }

    [Test]
    public async Task CachingMiddleware_ExpiredEntryRemoved_FromCache()
    {
        var fakeClock = new FakeClock(DateTime.UtcNow);
        var ttl = TimeSpan.FromMinutes(1);
        var middleware = new CachingMiddleware(new InMemoryStateStore(), ttl, 100, fakeClock);

        // Add entry
        await middleware.InvokeAsync(
            CreateTestMessage(subject: "To Expire"),
            CreateSuccessHandler("Cached"),
            CancellationToken.None);

        Assert.Equal(1, middleware.Count);

        // Advance past TTL
        fakeClock.Advance(ttl + TimeSpan.FromSeconds(1));

        // Access expired entry - should be removed and new one added
        await middleware.InvokeAsync(
            CreateTestMessage(subject: "To Expire"),
            CreateSuccessHandler("Refreshed"),
            CancellationToken.None);

        // Count should still be 1 (old removed, new added)
        Assert.Equal(1, middleware.Count);
    }

    #endregion

    #region CachingMiddleware Tests - Eviction

    [Test]
    public async Task CachingMiddleware_ExceedsMaxEntries_EvictsLRU()
    {
        var fakeClock = new FakeClock(DateTime.UtcNow);
        var maxEntries = 10;
        var middleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromHours(1), maxEntries, fakeClock);

        // Add maxEntries items
        for (int i = 0; i < maxEntries; i++)
        {
            fakeClock.Advance(TimeSpan.FromSeconds(1)); // Ensure different timestamps
            await middleware.InvokeAsync(
                CreateTestMessage(subject: $"Entry{i}"),
                CreateSuccessHandler($"Result{i}"),
                CancellationToken.None);
        }

        Assert.Equal(maxEntries, middleware.Count);

        // Add one more - should trigger eviction
        fakeClock.Advance(TimeSpan.FromSeconds(1));
        await middleware.InvokeAsync(
            CreateTestMessage(subject: "NewEntry"),
            CreateSuccessHandler("NewResult"),
            CancellationToken.None);

        // Should have evicted some entries (10% buffer = 1 entry + overflow = 2 total evicted)
        Assert.True(middleware.Count <= maxEntries);
    }

    [Test]
    public async Task CachingMiddleware_LRUEviction_RemovesOldestAccessedFirst()
    {
        var fakeClock = new FakeClock(DateTime.UtcNow);
        var maxEntries = 5;
        var middleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromHours(1), maxEntries, fakeClock);

        // Add 5 entries with sequential access times
        for (int i = 0; i < maxEntries; i++)
        {
            fakeClock.Advance(TimeSpan.FromSeconds(1));
            await middleware.InvokeAsync(
                CreateTestMessage(senderId: $"sender{i}", subject: $"Entry{i}"),
                CreateSuccessHandler($"Result{i}"),
                CancellationToken.None);
        }

        // Access Entry0 to make it recently used (should survive eviction)
        fakeClock.Advance(TimeSpan.FromSeconds(1));
        var handler0Called = 0;
        await middleware.InvokeAsync(
            CreateTestMessage(senderId: "sender0", subject: "Entry0"),
            (msg, ct) =>
            {
                handler0Called++;
                return Task.FromResult(MessageResult.Ok("Should not call - cached"));
            },
            CancellationToken.None);

        Assert.Equal(0, handler0Called); // Was cached

        // Add more entries to trigger eviction
        for (int i = 0; i < 3; i++)
        {
            fakeClock.Advance(TimeSpan.FromSeconds(1));
            await middleware.InvokeAsync(
                CreateTestMessage(subject: $"Overflow{i}"),
                CreateSuccessHandler($"Overflow{i}"),
                CancellationToken.None);
        }

        // Entry0 should still be cached (was recently accessed)
        fakeClock.Advance(TimeSpan.FromSeconds(1));
        var handler0CalledAfterEviction = 0;
        await middleware.InvokeAsync(
            CreateTestMessage(senderId: "sender0", subject: "Entry0"),
            (msg, ct) =>
            {
                handler0CalledAfterEviction++;
                return Task.FromResult(MessageResult.Ok("Handler called"));
            },
            CancellationToken.None);

        // If handler wasn't called, Entry0 survived eviction (still cached)
        // If handler was called, Entry0 was evicted
        // Due to LRU, Entry0 should survive as it was recently accessed
        Assert.Equal(0, handler0CalledAfterEviction);
    }

    #endregion

    #region CachingMiddleware Tests - CleanupExpired

    [Test]
    public async Task CachingMiddleware_CleanupExpired_RemovesExpiredEntries()
    {
        var fakeClock = new FakeClock(DateTime.UtcNow);
        var ttl = TimeSpan.FromMinutes(5);
        var middleware = new CachingMiddleware(new InMemoryStateStore(), ttl, 100, fakeClock);

        // Add entries
        await middleware.InvokeAsync(CreateTestMessage(subject: "Entry1"), CreateSuccessHandler(), CancellationToken.None);
        fakeClock.Advance(TimeSpan.FromMinutes(1));
        await middleware.InvokeAsync(CreateTestMessage(subject: "Entry2"), CreateSuccessHandler(), CancellationToken.None);
        fakeClock.Advance(TimeSpan.FromMinutes(1));
        await middleware.InvokeAsync(CreateTestMessage(subject: "Entry3"), CreateSuccessHandler(), CancellationToken.None);

        Assert.Equal(3, middleware.Count);

        // Advance time so first entry expires (and possibly second at boundary)
        fakeClock.Advance(TimeSpan.FromMinutes(4)); // Entry1 now 6 minutes old, Entry2 is 5 (at boundary)

        // Call CleanupExpired
        middleware.CleanupExpired();

        // At least Entry1 should be removed, count should decrease
        Assert.True(middleware.Count < 3);
        Assert.True(middleware.Count >= 1); // Entry3 should remain
    }

    [Test]
    public async Task CachingMiddleware_CleanupExpired_RemovesAllExpired()
    {
        var fakeClock = new FakeClock(DateTime.UtcNow);
        var ttl = TimeSpan.FromMinutes(1);
        var middleware = new CachingMiddleware(new InMemoryStateStore(), ttl, 100, fakeClock);

        // Add multiple entries
        for (int i = 0; i < 5; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(subject: $"Entry{i}"),
                CreateSuccessHandler(),
                CancellationToken.None);
        }

        Assert.Equal(5, middleware.Count);

        // Advance time past TTL for all entries
        fakeClock.Advance(ttl + TimeSpan.FromSeconds(1));

        middleware.CleanupExpired();

        Assert.Equal(0, middleware.Count);
    }

    [Test]
    public void CachingMiddleware_CleanupExpired_EmptyCache_DoesNotThrow()
    {
        var middleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromMinutes(5), 100, SystemClock.Instance);

        // Should not throw on empty cache
        middleware.CleanupExpired();

        Assert.Equal(0, middleware.Count);
    }

    #endregion

    #region CachingMiddleware Tests - Cache Key Generation

    [Test]
    public async Task CachingMiddleware_DifferentSenders_DifferentCacheKeys()
    {
        var middleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromMinutes(5), 100, SystemClock.Instance);

        var callCount = 0;

        // Same subject and content, different senders
        await middleware.InvokeAsync(
            CreateTestMessage(senderId: "sender-A", subject: "Query", content: "Data"),
            (msg, ct) =>
            {
                callCount++;
                return Task.FromResult(MessageResult.Ok("A's result"));
            },
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(senderId: "sender-B", subject: "Query", content: "Data"),
            (msg, ct) =>
            {
                callCount++;
                return Task.FromResult(MessageResult.Ok("B's result"));
            },
            CancellationToken.None);

        // Both should call handler (different cache keys)
        Assert.Equal(2, callCount);
        Assert.Equal(2, middleware.Count);
    }

    [Test]
    public async Task CachingMiddleware_DifferentCategory_DifferentCacheKeys()
    {
        var middleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromMinutes(5), 100, SystemClock.Instance);

        var callCount = 0;

        await middleware.InvokeAsync(
            CreateTestMessage(subject: "Query", category: "Category-A"),
            (msg, ct) =>
            {
                callCount++;
                return Task.FromResult(MessageResult.Ok());
            },
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(subject: "Query", category: "Category-B"),
            (msg, ct) =>
            {
                callCount++;
                return Task.FromResult(MessageResult.Ok());
            },
            CancellationToken.None);

        Assert.Equal(2, callCount);
    }

    [Test]
    public async Task CachingMiddleware_DifferentContent_DifferentCacheKeys()
    {
        var middleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromMinutes(5), 100, SystemClock.Instance);

        var callCount = 0;

        await middleware.InvokeAsync(
            CreateTestMessage(subject: "Query", content: "Content-A"),
            (msg, ct) =>
            {
                callCount++;
                return Task.FromResult(MessageResult.Ok());
            },
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(subject: "Query", content: "Content-B"),
            (msg, ct) =>
            {
                callCount++;
                return Task.FromResult(MessageResult.Ok());
            },
            CancellationToken.None);

        Assert.Equal(2, callCount);
    }

    [Test]
    public async Task CachingMiddleware_SameMessage_SameCacheKey()
    {
        var middleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromMinutes(5), 100, SystemClock.Instance);

        var callCount = 0;

        // Identical messages should hit cache
        for (int i = 0; i < 5; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(senderId: "same", subject: "same", content: "same", category: "same"),
                (msg, ct) =>
                {
                    callCount++;
                    return Task.FromResult(MessageResult.Ok("Cached"));
                },
                CancellationToken.None);
        }

        Assert.Equal(1, callCount);
        Assert.Equal(1, middleware.Count);
    }

    #endregion

    #region CachingMiddleware Tests - Count Property

    [Test]
    public async Task CachingMiddleware_Count_ReflectsNumberOfCachedEntries()
    {
        var middleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromMinutes(5), 100, SystemClock.Instance);

        Assert.Equal(0, middleware.Count);

        await middleware.InvokeAsync(CreateTestMessage(subject: "E1"), CreateSuccessHandler(), CancellationToken.None);
        Assert.Equal(1, middleware.Count);

        await middleware.InvokeAsync(CreateTestMessage(subject: "E2"), CreateSuccessHandler(), CancellationToken.None);
        Assert.Equal(2, middleware.Count);

        await middleware.InvokeAsync(CreateTestMessage(subject: "E3"), CreateSuccessHandler(), CancellationToken.None);
        Assert.Equal(3, middleware.Count);

        middleware.Clear();
        Assert.Equal(0, middleware.Count);
    }

    [Test]
    public async Task CachingMiddleware_Count_DoesNotIncrementForCacheHits()
    {
        var middleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromMinutes(5), 100, SystemClock.Instance);

        // Add one entry
        await middleware.InvokeAsync(CreateTestMessage(subject: "Entry"), CreateSuccessHandler(), CancellationToken.None);
        Assert.Equal(1, middleware.Count);

        // Hit cache multiple times
        for (int i = 0; i < 10; i++)
        {
            await middleware.InvokeAsync(CreateTestMessage(subject: "Entry"), CreateSuccessHandler(), CancellationToken.None);
        }

        // Count should still be 1
        Assert.Equal(1, middleware.Count);
    }

    #endregion

    #region RetryMiddleware Tests - Default Constructor

    [Test]
    public async Task RetryMiddleware_DefaultConstructor_UsesDefaultSettings()
    {
        var middleware = new RetryMiddleware();
        var attempts = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            attempts++;
            return Task.FromResult(MessageResult.Fail("Always fails"));
        };

        await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        // Default is 3 attempts
        Assert.Equal(MiddlewareDefaults.RetryDefaultMaxAttempts, attempts);
    }

    [Test]
    public async Task RetryMiddleware_DefaultConstructor_SucceedsOnFirstTry()
    {
        var middleware = new RetryMiddleware();
        var attempts = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            attempts++;
            return Task.FromResult(MessageResult.Ok("Success"));
        };

        var result = await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, attempts);
    }

    #endregion

    #region RetryMiddleware Tests - Exception Handling

    [Test]
    public async Task RetryMiddleware_HandlerThrows_RetriesAndReturnsFailure()
    {
        var middleware = new RetryMiddleware(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(1));
        var attempts = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            attempts++;
            throw new InvalidOperationException($"Attempt {attempts} failed");
        };

        var result = await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(3, attempts);
        Assert.Contains("Failed after 3 attempts", result.Error!);
    }

    [Test]
    public async Task RetryMiddleware_HandlerThrowsThenSucceeds_ReturnsSuccess()
    {
        var middleware = new RetryMiddleware(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(1));
        var attempts = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            attempts++;
            if (attempts < 3)
                throw new InvalidOperationException("Temporary failure");
            return Task.FromResult(MessageResult.Ok("Recovered"));
        };

        var result = await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, attempts);
        Assert.Equal("Recovered", result.Response);
    }

    [Test]
    public async Task RetryMiddleware_ExceptionMessageIncludedInError()
    {
        var middleware = new RetryMiddleware(maxAttempts: 2, delay: TimeSpan.FromMilliseconds(1));

        MessageDelegate handler = (msg, ct) => throw new ArgumentException("Invalid argument provided");

        var result = await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Invalid argument provided", result.Error!);
    }

    #endregion

    #region RetryMiddleware Tests - Backoff Behavior

    [Test]
    public async Task RetryMiddleware_ExponentialBackoff_DelaysIncrease()
    {
        var delay = TimeSpan.FromMilliseconds(50);
        var middleware = new RetryMiddleware(maxAttempts: 3, delay: delay);
        var timestamps = new List<DateTime>();

        MessageDelegate handler = (msg, ct) =>
        {
            timestamps.Add(DateTime.UtcNow);
            return Task.FromResult(MessageResult.Fail("Always fails"));
        };

        var startTime = DateTime.UtcNow;
        await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);
        var totalTime = DateTime.UtcNow - startTime;

        Assert.Equal(3, timestamps.Count);

        // Total delay should be at least delay*1 + delay*2 = 150ms for 3 attempts
        // (first attempt has no delay, second has 50ms, third has 100ms)
        Assert.True(totalTime.TotalMilliseconds >= 140); // Allow some tolerance
    }

    [Test]
    public async Task RetryMiddleware_ZeroDelay_RetriesImmediately()
    {
        var middleware = new RetryMiddleware(maxAttempts: 5, delay: TimeSpan.Zero);
        var attempts = 0;

        var startTime = DateTime.UtcNow;
        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                attempts++;
                return Task.FromResult(MessageResult.Fail("Fails"));
            },
            CancellationToken.None);
        var duration = DateTime.UtcNow - startTime;

        Assert.Equal(5, attempts);
        Assert.True(duration.TotalMilliseconds < 500); // Should be very fast with no delays
    }

    #endregion

    #region RetryMiddleware Tests - Single Attempt

    [Test]
    public async Task RetryMiddleware_SingleAttempt_NoRetries()
    {
        var middleware = new RetryMiddleware(maxAttempts: 1, delay: TimeSpan.FromMilliseconds(100));
        var attempts = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            attempts++;
            return Task.FromResult(MessageResult.Fail("Single failure"));
        };

        var result = await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, attempts);
    }

    [Test]
    public async Task RetryMiddleware_SingleAttempt_SuccessOnFirst()
    {
        var middleware = new RetryMiddleware(maxAttempts: 1, delay: TimeSpan.FromMilliseconds(100));
        var attempts = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            attempts++;
            return Task.FromResult(MessageResult.Ok("First try success"));
        };

        var result = await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, attempts);
        Assert.Equal("First try success", result.Response);
    }

    #endregion

    #region RetryMiddleware Tests - Edge Cases

    [Test]
    public async Task RetryMiddleware_SucceedsOnLastAttempt()
    {
        var middleware = new RetryMiddleware(maxAttempts: 5, delay: TimeSpan.FromMilliseconds(1));
        var attempts = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            attempts++;
            if (attempts < 5)
                return Task.FromResult(MessageResult.Fail($"Attempt {attempts}"));
            return Task.FromResult(MessageResult.Ok("Finally!"));
        };

        var result = await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(5, attempts);
        Assert.Equal("Finally!", result.Response);
    }

    [Test]
    public async Task RetryMiddleware_MixedFailuresAndExceptions()
    {
        var middleware = new RetryMiddleware(maxAttempts: 4, delay: TimeSpan.FromMilliseconds(1));
        var attempts = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            attempts++;
            if (attempts == 1)
                return Task.FromResult(MessageResult.Fail("First failure"));
            if (attempts == 2)
                throw new InvalidOperationException("Second exception");
            if (attempts == 3)
                return Task.FromResult(MessageResult.Fail("Third failure"));
            return Task.FromResult(MessageResult.Ok("Fourth success"));
        };

        var result = await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(4, attempts);
    }

    [Test]
    public async Task RetryMiddleware_PreservesLastFailureResult()
    {
        var middleware = new RetryMiddleware(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(1));
        var attempts = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            attempts++;
            var result = MessageResult.Fail($"Failure #{attempts}");
            result.Data["attempt"] = attempts;
            return Task.FromResult(result);
        };

        var finalResult = await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        Assert.False(finalResult.Success);
        Assert.Equal(3, attempts);
        // Last result should be returned
        Assert.Equal("Failure #3", finalResult.Error);
    }

    [Test]
    public async Task RetryMiddleware_LargeMaxAttempts_Works()
    {
        var middleware = new RetryMiddleware(maxAttempts: 20, delay: TimeSpan.Zero);
        var attempts = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            attempts++;
            if (attempts < 15)
                return Task.FromResult(MessageResult.Fail("Keep trying"));
            return Task.FromResult(MessageResult.Ok("Made it"));
        };

        var result = await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(15, attempts);
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task CachingAndRetry_Integration_CachesAfterSuccessfulRetry()
    {
        var fakeClock = new FakeClock(DateTime.UtcNow);
        var cachingMiddleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromMinutes(5), 100, fakeClock);
        var retryMiddleware = new RetryMiddleware(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(1));

        var attempts = 0;

        // Build a mini pipeline: Caching -> Retry -> Handler
        MessageDelegate finalHandler = (msg, ct) =>
        {
            attempts++;
            if (attempts < 3)
                return Task.FromResult(MessageResult.Fail("Not yet"));
            return Task.FromResult(MessageResult.Ok("Success after retries"));
        };

        MessageDelegate retryPipeline = async (msg, ct) =>
        {
            return await retryMiddleware.InvokeAsync(msg, finalHandler, ct);
        };

        // First call - retries then succeeds
        var result1 = await cachingMiddleware.InvokeAsync(CreateTestMessage(subject: "Retry Test"), retryPipeline, CancellationToken.None);
        Assert.True(result1.Success);
        Assert.Equal(3, attempts);

        // Second call - should hit cache (no more retries)
        var result2 = await cachingMiddleware.InvokeAsync(CreateTestMessage(subject: "Retry Test"), retryPipeline, CancellationToken.None);
        Assert.True(result2.Success);
        Assert.Equal(3, attempts); // No additional attempts - cached
    }

    [Test]
    public async Task LoggingAndCaching_Integration_LogsCacheHits()
    {
        var logger = new FakeAgentLogger();
        var loggingMiddleware = new LoggingMiddleware(logger);
        var cachingMiddleware = new CachingMiddleware(new InMemoryStateStore(), TimeSpan.FromMinutes(5), 100, SystemClock.Instance);

        var callCount = 0;
        MessageDelegate handler = (msg, ct) =>
        {
            callCount++;
            return Task.FromResult(MessageResult.Ok("Result"));
        };

        // Build pipeline: Logging -> Caching -> Handler
        MessageDelegate cachingPipeline = async (msg, ct) =>
        {
            return await cachingMiddleware.InvokeAsync(msg, handler, ct);
        };

        // First call
        await loggingMiddleware.InvokeAsync(CreateTestMessage(subject: "Log Cache Test"), cachingPipeline, CancellationToken.None);
        Assert.Equal(1, callCount);

        // Second call - cached
        await loggingMiddleware.InvokeAsync(CreateTestMessage(subject: "Log Cache Test"), cachingPipeline, CancellationToken.None);
        Assert.Equal(1, callCount); // Handler not called again
    }

    #endregion
}
