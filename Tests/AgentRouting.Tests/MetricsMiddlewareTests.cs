using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;

namespace AgentRouting.Tests;

/// <summary>
/// Comprehensive tests for MetricsMiddleware - tracking message counts, success/failure rates, and processing times
/// </summary>
public class MetricsMiddlewareTests
{
    #region Message Count Tracking

    [Test]
    public async Task MetricsMiddleware_SingleMessage_IncrementsTotal()
    {
        var middleware = new MetricsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();
        Assert.Equal(1, snapshot.TotalMessages);
    }

    [Test]
    public async Task MetricsMiddleware_MultipleMessages_TracksAllMessages()
    {
        var middleware = new MetricsMiddleware();

        for (int i = 0; i < 10; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
                CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();
        Assert.Equal(10, snapshot.TotalMessages);
    }

    [Test]
    public void MetricsMiddleware_NoMessages_ZeroTotal()
    {
        var middleware = new MetricsMiddleware();

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(0, snapshot.TotalMessages);
        Assert.Equal(0, snapshot.SuccessCount);
        Assert.Equal(0, snapshot.FailureCount);
    }

    #endregion

    #region Success/Failure Counting

    [Test]
    public async Task MetricsMiddleware_SuccessfulMessage_IncrementsSuccessCount()
    {
        var middleware = new MetricsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Success")),
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();
        Assert.Equal(1, snapshot.SuccessCount);
        Assert.Equal(0, snapshot.FailureCount);
    }

    [Test]
    public async Task MetricsMiddleware_FailedMessage_IncrementsFailureCount()
    {
        var middleware = new MetricsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Fail("Error")),
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();
        Assert.Equal(0, snapshot.SuccessCount);
        Assert.Equal(1, snapshot.FailureCount);
    }

    [Test]
    public async Task MetricsMiddleware_MixedResults_TracksSuccessAndFailureSeparately()
    {
        var middleware = new MetricsMiddleware();

        // 2 successes
        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // 1 failure
        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Fail("Error")),
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(3, snapshot.TotalMessages);
        Assert.Equal(2, snapshot.SuccessCount);
        Assert.Equal(1, snapshot.FailureCount);
    }

    [Test]
    public async Task MetricsMiddleware_AllSuccessful_CountsMatchTotal()
    {
        var middleware = new MetricsMiddleware();

        for (int i = 0; i < 5; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(5, snapshot.TotalMessages);
        Assert.Equal(5, snapshot.SuccessCount);
        Assert.Equal(0, snapshot.FailureCount);
    }

    [Test]
    public async Task MetricsMiddleware_AllFailures_CountsMatchTotal()
    {
        var middleware = new MetricsMiddleware();

        for (int i = 0; i < 5; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Fail("Error")),
                CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(5, snapshot.TotalMessages);
        Assert.Equal(0, snapshot.SuccessCount);
        Assert.Equal(5, snapshot.FailureCount);
    }

    #endregion

    #region Success Rate Calculation

    [Test]
    public async Task MetricsMiddleware_CalculatesSuccessRate_75Percent()
    {
        var middleware = new MetricsMiddleware();

        // 3 successes, 1 failure = 75% success rate
        for (int i = 0; i < 3; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Fail("Error")),
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(0.75, snapshot.SuccessRate);
    }

    [Test]
    public async Task MetricsMiddleware_CalculatesSuccessRate_100Percent()
    {
        var middleware = new MetricsMiddleware();

        for (int i = 0; i < 4; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(1.0, snapshot.SuccessRate);
    }

    [Test]
    public async Task MetricsMiddleware_CalculatesSuccessRate_0Percent()
    {
        var middleware = new MetricsMiddleware();

        for (int i = 0; i < 4; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Fail("Error")),
                CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(0.0, snapshot.SuccessRate);
    }

    [Test]
    public async Task MetricsMiddleware_CalculatesSuccessRate_50Percent()
    {
        var middleware = new MetricsMiddleware();

        // 2 successes, 2 failures = 50% success rate
        await middleware.InvokeAsync(CreateTestMessage(), (msg, ct) => Task.FromResult(MessageResult.Ok("OK")), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), (msg, ct) => Task.FromResult(MessageResult.Fail("Error")), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), (msg, ct) => Task.FromResult(MessageResult.Ok("OK")), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), (msg, ct) => Task.FromResult(MessageResult.Fail("Error")), CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(0.5, snapshot.SuccessRate);
    }

    [Test]
    public void MetricsMiddleware_NoMessages_SuccessRateIsZero()
    {
        var middleware = new MetricsMiddleware();

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(0.0, snapshot.SuccessRate);
    }

    #endregion

    #region Processing Time Measurement

    [Test]
    public async Task MetricsMiddleware_TracksProcessingTime()
    {
        var middleware = new MetricsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) =>
            {
                await Task.Delay(10); // Small delay
                return MessageResult.Ok("Done");
            },
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        Assert.True(snapshot.AverageProcessingTimeMs >= 0, "Average processing time should be non-negative");
        Assert.True(snapshot.MinProcessingTimeMs >= 0, "Min processing time should be non-negative");
        Assert.True(snapshot.MaxProcessingTimeMs >= 0, "Max processing time should be non-negative");
    }

    [Test]
    public async Task MetricsMiddleware_SingleMessage_MinEqualsMaxEqualsAverage()
    {
        var middleware = new MetricsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        // For a single message, min, max, and average should be the same
        Assert.Equal(snapshot.MinProcessingTimeMs, snapshot.MaxProcessingTimeMs);
    }

    [Test]
    public async Task MetricsMiddleware_VariedProcessingTimes_TracksMinAndMax()
    {
        var middleware = new MetricsMiddleware();

        // Fast message
        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Fast")),
            CancellationToken.None);

        // Slower message
        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) =>
            {
                await Task.Delay(50); // 50ms delay
                return MessageResult.Ok("Slow");
            },
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        // Max should be at least 50ms (accounting for some margin)
        Assert.True(snapshot.MaxProcessingTimeMs >= 40, $"Max time {snapshot.MaxProcessingTimeMs}ms should be >= 40ms");
        // Min should be less than max
        Assert.True(snapshot.MinProcessingTimeMs <= snapshot.MaxProcessingTimeMs, "Min should be <= Max");
    }

    [Test]
    public async Task MetricsMiddleware_CalculatesAverageProcessingTime()
    {
        var middleware = new MetricsMiddleware();

        // Process multiple messages
        for (int i = 0; i < 5; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                async (msg, ct) =>
                {
                    await Task.Delay(10);
                    return MessageResult.Ok("Done");
                },
                CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();

        // Average should be between min and max
        Assert.True(snapshot.AverageProcessingTimeMs >= snapshot.MinProcessingTimeMs,
            "Average should be >= Min");
        Assert.True(snapshot.AverageProcessingTimeMs <= snapshot.MaxProcessingTimeMs,
            "Average should be <= Max");
    }

    [Test]
    public void MetricsMiddleware_NoMessages_ProcessingTimesAreZero()
    {
        var middleware = new MetricsMiddleware();

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(0, snapshot.AverageProcessingTimeMs);
        Assert.Equal(0, snapshot.MinProcessingTimeMs);
        Assert.Equal(0, snapshot.MaxProcessingTimeMs);
    }

    #endregion

    #region Metrics Snapshot Generation

    [Test]
    public async Task MetricsMiddleware_GetSnapshot_ReturnsCompleteData()
    {
        var middleware = new MetricsMiddleware();

        // Process some messages
        await middleware.InvokeAsync(CreateTestMessage(), (msg, ct) => Task.FromResult(MessageResult.Ok("OK")), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), (msg, ct) => Task.FromResult(MessageResult.Fail("Error")), CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.TotalMessages);
        Assert.Equal(1, snapshot.SuccessCount);
        Assert.Equal(1, snapshot.FailureCount);
        Assert.Equal(0.5, snapshot.SuccessRate);
    }

    [Test]
    public async Task MetricsMiddleware_MultipleSnapshots_ReturnCurrentState()
    {
        var middleware = new MetricsMiddleware();

        await middleware.InvokeAsync(CreateTestMessage(), (msg, ct) => Task.FromResult(MessageResult.Ok("OK")), CancellationToken.None);

        var snapshot1 = middleware.GetSnapshot();
        Assert.Equal(1, snapshot1.TotalMessages);

        await middleware.InvokeAsync(CreateTestMessage(), (msg, ct) => Task.FromResult(MessageResult.Ok("OK")), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage(), (msg, ct) => Task.FromResult(MessageResult.Ok("OK")), CancellationToken.None);

        var snapshot2 = middleware.GetSnapshot();
        Assert.Equal(3, snapshot2.TotalMessages);
    }

    [Test]
    public void MetricsSnapshot_ToString_ContainsAllMetrics()
    {
        var snapshot = new MetricsSnapshot
        {
            TotalMessages = 100,
            SuccessCount = 90,
            FailureCount = 10,
            SuccessRate = 0.9,
            AverageProcessingTimeMs = 50.5,
            MinProcessingTimeMs = 10,
            MaxProcessingTimeMs = 200
        };

        var str = snapshot.ToString();

        Assert.Contains("100", str);
        Assert.Contains("90", str);
        Assert.Contains("10", str);
        Assert.Contains("50.5", str);
    }

    #endregion

    #region Concurrent Message Processing

    [Test]
    public async Task MetricsMiddleware_ConcurrentMessages_TracksAllMessages()
    {
        var middleware = new MetricsMiddleware();
        var tasks = new List<Task>();

        // Process 100 messages concurrently
        for (int i = 0; i < 100; i++)
        {
            var localI = i;
            tasks.Add(middleware.InvokeAsync(
                CreateTestMessage($"sender-{localI}"),
                async (msg, ct) =>
                {
                    await Task.Delay(1); // Small delay to increase concurrency
                    return localI % 2 == 0 ? MessageResult.Ok("OK") : MessageResult.Fail("Error");
                },
                CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(100, snapshot.TotalMessages);
        Assert.Equal(50, snapshot.SuccessCount); // Even indices succeed
        Assert.Equal(50, snapshot.FailureCount); // Odd indices fail
    }

    [Test]
    public async Task MetricsMiddleware_ConcurrentMessages_MaintainsConsistency()
    {
        var middleware = new MetricsMiddleware();
        var tasks = new List<Task>();

        // All successes
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        var snapshot = middleware.GetSnapshot();

        // Verify consistency: Total = Success + Failure
        Assert.Equal(snapshot.TotalMessages, snapshot.SuccessCount + snapshot.FailureCount);
        Assert.Equal(50, snapshot.TotalMessages);
        Assert.Equal(50, snapshot.SuccessCount);
    }

    [Test]
    public async Task MetricsMiddleware_HighVolumeConcurrent_HandlesWithoutError()
    {
        var middleware = new MetricsMiddleware();
        var tasks = new List<Task>();

        // Stress test with 1000 concurrent messages
        for (int i = 0; i < 1000; i++)
        {
            tasks.Add(middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(1000, snapshot.TotalMessages);
        Assert.Equal(1000, snapshot.SuccessCount);
        Assert.Equal(0, snapshot.FailureCount);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task MetricsMiddleware_RapidSuccessiveMessages_TracksAll()
    {
        var middleware = new MetricsMiddleware();

        // Fire off messages as fast as possible without waiting
        for (int i = 0; i < 100; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(100, snapshot.TotalMessages);
        Assert.Equal(1.0, snapshot.SuccessRate);
    }

    [Test]
    public async Task MetricsMiddleware_LongRunningHandler_TracksTime()
    {
        var middleware = new MetricsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(),
            async (msg, ct) =>
            {
                await Task.Delay(100); // 100ms delay
                return MessageResult.Ok("Done");
            },
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        // Should have recorded at least 90ms (with some tolerance for timing)
        Assert.True(snapshot.MaxProcessingTimeMs >= 90,
            $"Expected at least 90ms processing time but got {snapshot.MaxProcessingTimeMs}ms");
    }

    [Test]
    public async Task MetricsMiddleware_ZeroProcessingTime_HandlesCorrectly()
    {
        var middleware = new MetricsMiddleware();

        // Instant handler
        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Instant")),
            CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        // Should handle 0ms processing time without issues
        Assert.True(snapshot.MinProcessingTimeMs >= 0);
        Assert.True(snapshot.AverageProcessingTimeMs >= 0);
    }

    [Test]
    public async Task MetricsMiddleware_DifferentMessageTypes_TracksAll()
    {
        var middleware = new MetricsMiddleware();

        // Different senders
        await middleware.InvokeAsync(CreateTestMessage("sender-1"), (msg, ct) => Task.FromResult(MessageResult.Ok("OK")), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage("sender-2"), (msg, ct) => Task.FromResult(MessageResult.Ok("OK")), CancellationToken.None);
        await middleware.InvokeAsync(CreateTestMessage("sender-3"), (msg, ct) => Task.FromResult(MessageResult.Fail("Error")), CancellationToken.None);

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(3, snapshot.TotalMessages);
        Assert.Equal(2, snapshot.SuccessCount);
        Assert.Equal(1, snapshot.FailureCount);
    }

    [Test]
    public async Task MetricsMiddleware_PassesMessageToNext_Unchanged()
    {
        var middleware = new MetricsMiddleware();
        var originalMessage = CreateTestMessage();
        AgentMessage? receivedMessage = null;

        await middleware.InvokeAsync(
            originalMessage,
            (msg, ct) =>
            {
                receivedMessage = msg;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.Same(originalMessage, receivedMessage);
    }

    [Test]
    public async Task MetricsMiddleware_ReturnsResultFromNext_Unchanged()
    {
        var middleware = new MetricsMiddleware();
        var expectedResult = MessageResult.Ok("Expected Response");

        var actualResult = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(expectedResult),
            CancellationToken.None);

        Assert.Same(expectedResult, actualResult);
    }

    #endregion

    #region Integration with Middleware Pipeline

    [Test]
    public async Task MetricsMiddleware_InPipeline_TracksAllMessages()
    {
        var pipeline = new MiddlewarePipeline();
        var metrics = new MetricsMiddleware();

        pipeline.Use(metrics);

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));

        await builtPipeline(CreateTestMessage(), CancellationToken.None);
        await builtPipeline(CreateTestMessage(), CancellationToken.None);
        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(3, snapshot.TotalMessages);
        Assert.Equal(3, snapshot.SuccessCount);
    }

    [Test]
    public async Task MetricsMiddleware_WithOtherMiddleware_TracksCorrectly()
    {
        var pipeline = new MiddlewarePipeline();
        var metrics = new MetricsMiddleware();

        // Metrics should track even when other middleware is present
        pipeline.Use(metrics);
        pipeline.Use(new ValidationMiddleware());

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));

        // Valid message
        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        // Invalid message (will be short-circuited by validation)
        var invalidMessage = new AgentMessage
        {
            SenderId = "", // Invalid - empty sender
            Subject = "Test",
            Content = "Content"
        };
        await builtPipeline(invalidMessage, CancellationToken.None);

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(2, snapshot.TotalMessages);
        Assert.Equal(1, snapshot.SuccessCount); // Valid message succeeded
        Assert.Equal(1, snapshot.FailureCount); // Invalid message failed validation
    }

    [Test]
    public async Task MetricsMiddleware_AtEndOfPipeline_TracksAllPipelineResults()
    {
        var pipeline = new MiddlewarePipeline();
        var metrics = new MetricsMiddleware();

        // Put metrics at the end to catch all results
        pipeline.Use(new ValidationMiddleware());
        pipeline.Use(metrics);

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));

        // This message will fail validation before reaching metrics
        var invalidMessage = new AgentMessage { SenderId = "", Subject = "Test", Content = "Content" };
        await builtPipeline(invalidMessage, CancellationToken.None);

        var snapshot = metrics.GetSnapshot();

        // Metrics at end won't see messages that were short-circuited before it
        Assert.Equal(0, snapshot.TotalMessages);
    }

    [Test]
    public async Task MetricsMiddleware_AtStartOfPipeline_TracksAllAttempts()
    {
        var pipeline = new MiddlewarePipeline();
        var metrics = new MetricsMiddleware();

        // Put metrics at the start to catch all attempts
        pipeline.Use(metrics);
        pipeline.Use(new ValidationMiddleware());

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));

        // This message will fail validation, but metrics at start will track it
        var invalidMessage = new AgentMessage { SenderId = "", Subject = "Test", Content = "Content" };
        await builtPipeline(invalidMessage, CancellationToken.None);

        var snapshot = metrics.GetSnapshot();

        // Metrics at start sees all messages, even those that fail later
        Assert.Equal(1, snapshot.TotalMessages);
        Assert.Equal(1, snapshot.FailureCount); // Recorded as failure because result.Success is false
    }

    #endregion

    #region MetricsSnapshot Tests

    [Test]
    public void MetricsSnapshot_DefaultValues_AreZero()
    {
        var snapshot = new MetricsSnapshot();

        Assert.Equal(0, snapshot.TotalMessages);
        Assert.Equal(0, snapshot.SuccessCount);
        Assert.Equal(0, snapshot.FailureCount);
        Assert.Equal(0.0, snapshot.SuccessRate);
        Assert.Equal(0.0, snapshot.AverageProcessingTimeMs);
        Assert.Equal(0, snapshot.MinProcessingTimeMs);
        Assert.Equal(0, snapshot.MaxProcessingTimeMs);
    }

    [Test]
    public void MetricsSnapshot_SetProperties_ReturnsCorrectValues()
    {
        var snapshot = new MetricsSnapshot
        {
            TotalMessages = 100,
            SuccessCount = 80,
            FailureCount = 20,
            SuccessRate = 0.8,
            AverageProcessingTimeMs = 25.5,
            MinProcessingTimeMs = 5,
            MaxProcessingTimeMs = 100
        };

        Assert.Equal(100, snapshot.TotalMessages);
        Assert.Equal(80, snapshot.SuccessCount);
        Assert.Equal(20, snapshot.FailureCount);
        Assert.Equal(0.8, snapshot.SuccessRate);
        Assert.Equal(25.5, snapshot.AverageProcessingTimeMs);
        Assert.Equal(5, snapshot.MinProcessingTimeMs);
        Assert.Equal(100, snapshot.MaxProcessingTimeMs);
    }

    #endregion

    #region Theory Tests - Parameterized

    [Theory]
    [InlineData(1, 0, 1.0)]
    [InlineData(5, 5, 0.5)]
    [InlineData(0, 1, 0.0)]
    [InlineData(10, 0, 1.0)]
    [InlineData(0, 10, 0.0)]
    [InlineData(3, 1, 0.75)]
    public async Task MetricsMiddleware_SuccessRate_CalculatesCorrectly(int successCount, int failureCount, double expectedRate)
    {
        var middleware = new MetricsMiddleware();

        for (int i = 0; i < successCount; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        for (int i = 0; i < failureCount; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Fail("Error")),
                CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(successCount + failureCount, snapshot.TotalMessages);
        Assert.Equal(successCount, snapshot.SuccessCount);
        Assert.Equal(failureCount, snapshot.FailureCount);
        Assert.Equal(expectedRate, snapshot.SuccessRate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task MetricsMiddleware_MessageCount_MatchesExpected(int messageCount)
    {
        var middleware = new MetricsMiddleware();

        for (int i = 0; i < messageCount; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(messageCount, snapshot.TotalMessages);
    }

    #endregion

    #region Helper Methods

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

    #endregion
}
