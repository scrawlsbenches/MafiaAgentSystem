using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for AnalyticsMiddleware tracking business analytics
/// </summary>
public class AnalyticsMiddlewareTests
{
    #region TotalMessages Tracking Tests

    [Test]
    public async Task Analytics_SingleMessage_CountsOne()
    {
        var middleware = new AnalyticsMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(1, report.TotalMessages);
    }

    [Test]
    public async Task Analytics_MultipleMessages_CountsAll()
    {
        var middleware = new AnalyticsMiddleware();

        for (int i = 0; i < 10; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var report = middleware.GetReport();
        Assert.Equal(10, report.TotalMessages);
    }

    [Test]
    public async Task Analytics_FailedMessages_StillCounted()
    {
        var middleware = new AnalyticsMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Fail("Error")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(1, report.TotalMessages);
    }

    #endregion

    #region Category Tracking Tests

    [Test]
    public async Task Analytics_SingleCategory_TracksCorrectly()
    {
        var middleware = new AnalyticsMiddleware();
        var message = CreateTestMessage(category: "Orders");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.True(report.CategoryCounts.ContainsKey("Orders"));
        Assert.Equal(1, report.CategoryCounts["Orders"]);
    }

    [Test]
    public async Task Analytics_MultipleMessagesWithSameCategory_Aggregates()
    {
        var middleware = new AnalyticsMiddleware();

        for (int i = 0; i < 5; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(category: "Payments"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var report = middleware.GetReport();
        Assert.Equal(5, report.CategoryCounts["Payments"]);
    }

    [Test]
    public async Task Analytics_DifferentCategories_TrackedSeparately()
    {
        var middleware = new AnalyticsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(category: "Orders"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(category: "Payments"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(category: "Shipping"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(3, report.CategoryCounts.Count);
        Assert.Equal(1, report.CategoryCounts["Orders"]);
        Assert.Equal(1, report.CategoryCounts["Payments"]);
        Assert.Equal(1, report.CategoryCounts["Shipping"]);
    }

    [Test]
    public async Task Analytics_EmptyCategory_NotTracked()
    {
        var middleware = new AnalyticsMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            ReceiverId = "test-receiver",
            Subject = "Test",
            Content = "Content",
            Category = ""
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(1, report.TotalMessages);
        Assert.Equal(0, report.CategoryCounts.Count);
    }

    [Test]
    public async Task Analytics_NullCategory_NotTracked()
    {
        var middleware = new AnalyticsMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            ReceiverId = "test-receiver",
            Subject = "Test",
            Content = "Content",
            Category = null!
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(1, report.TotalMessages);
        Assert.Equal(0, report.CategoryCounts.Count);
    }

    [Test]
    public async Task Analytics_MixedValidAndInvalidCategories_OnlyTracksValid()
    {
        var middleware = new AnalyticsMiddleware();

        // Valid category
        await middleware.InvokeAsync(
            CreateTestMessage(category: "Valid"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Empty category
        await middleware.InvokeAsync(
            new AgentMessage { SenderId = "s", Subject = "T", Content = "C", Category = "" },
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Another valid category
        await middleware.InvokeAsync(
            CreateTestMessage(category: "Valid"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(3, report.TotalMessages);
        Assert.Equal(1, report.CategoryCounts.Count);
        Assert.Equal(2, report.CategoryCounts["Valid"]);
    }

    #endregion

    #region Agent Workload Tracking Tests

    [Test]
    public async Task Analytics_SingleReceiver_TracksWorkload()
    {
        var middleware = new AnalyticsMiddleware();
        var message = CreateTestMessage(receiverId: "agent-1");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.True(report.AgentWorkload.ContainsKey("agent-1"));
        Assert.Equal(1, report.AgentWorkload["agent-1"]);
    }

    [Test]
    public async Task Analytics_MultipleMessagesToSameReceiver_Aggregates()
    {
        var middleware = new AnalyticsMiddleware();

        for (int i = 0; i < 7; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(receiverId: "busy-agent"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var report = middleware.GetReport();
        Assert.Equal(7, report.AgentWorkload["busy-agent"]);
    }

    [Test]
    public async Task Analytics_DifferentReceivers_TrackedSeparately()
    {
        var middleware = new AnalyticsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(receiverId: "agent-a"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(receiverId: "agent-b"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(receiverId: "agent-a"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(2, report.AgentWorkload.Count);
        Assert.Equal(2, report.AgentWorkload["agent-a"]);
        Assert.Equal(1, report.AgentWorkload["agent-b"]);
    }

    [Test]
    public async Task Analytics_EmptyReceiverId_NotTracked()
    {
        var middleware = new AnalyticsMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            ReceiverId = "",
            Subject = "Test",
            Content = "Content",
            Category = "Test"
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(1, report.TotalMessages);
        Assert.Equal(0, report.AgentWorkload.Count);
    }

    [Test]
    public async Task Analytics_NullReceiverId_NotTracked()
    {
        var middleware = new AnalyticsMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            ReceiverId = null!,
            Subject = "Test",
            Content = "Content",
            Category = "Test"
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(1, report.TotalMessages);
        Assert.Equal(0, report.AgentWorkload.Count);
    }

    [Test]
    public async Task Analytics_WorkloadDistribution_Accurate()
    {
        var middleware = new AnalyticsMiddleware();

        // Agent A: 5 messages, Agent B: 3 messages, Agent C: 2 messages
        for (int i = 0; i < 5; i++)
            await middleware.InvokeAsync(
                CreateTestMessage(receiverId: "agent-a"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

        for (int i = 0; i < 3; i++)
            await middleware.InvokeAsync(
                CreateTestMessage(receiverId: "agent-b"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

        for (int i = 0; i < 2; i++)
            await middleware.InvokeAsync(
                CreateTestMessage(receiverId: "agent-c"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(10, report.TotalMessages);
        Assert.Equal(3, report.AgentWorkload.Count);
        Assert.Equal(5, report.AgentWorkload["agent-a"]);
        Assert.Equal(3, report.AgentWorkload["agent-b"]);
        Assert.Equal(2, report.AgentWorkload["agent-c"]);
    }

    #endregion

    #region GetReport Tests

    [Test]
    public async Task GetReport_ReturnsCorrectData()
    {
        var middleware = new AnalyticsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(category: "Cat1", receiverId: "agent-1"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(category: "Cat2", receiverId: "agent-1"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(category: "Cat1", receiverId: "agent-2"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();

        Assert.Equal(3, report.TotalMessages);
        Assert.Equal(2, report.CategoryCounts.Count);
        Assert.Equal(2, report.CategoryCounts["Cat1"]);
        Assert.Equal(1, report.CategoryCounts["Cat2"]);
        Assert.Equal(2, report.AgentWorkload.Count);
        Assert.Equal(2, report.AgentWorkload["agent-1"]);
        Assert.Equal(1, report.AgentWorkload["agent-2"]);
    }

    [Test]
    public void GetReport_NoMessages_ReturnsEmptyReport()
    {
        var middleware = new AnalyticsMiddleware();

        var report = middleware.GetReport();

        Assert.Equal(0, report.TotalMessages);
        Assert.NotNull(report.CategoryCounts);
        Assert.Equal(0, report.CategoryCounts.Count);
        Assert.NotNull(report.AgentWorkload);
        Assert.Equal(0, report.AgentWorkload.Count);
    }

    [Test]
    public async Task GetReport_ReturnsNewDictionaryInstance()
    {
        var middleware = new AnalyticsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(category: "Test"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report1 = middleware.GetReport();
        var report2 = middleware.GetReport();

        // Should be different dictionary instances
        Assert.NotSame(report1.CategoryCounts, report2.CategoryCounts);
        Assert.NotSame(report1.AgentWorkload, report2.AgentWorkload);

        // But with same data
        Assert.Equal(report1.CategoryCounts["Test"], report2.CategoryCounts["Test"]);
    }

    #endregion

    #region GenerateReport Tests

    [Test]
    public async Task GenerateReport_ContainsHeader()
    {
        var middleware = new AnalyticsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GenerateReport();

        Assert.Contains("=== Analytics Report ===", report);
    }

    [Test]
    public async Task GenerateReport_ContainsTotalMessages()
    {
        var middleware = new AnalyticsMiddleware();

        for (int i = 0; i < 15; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var report = middleware.GenerateReport();

        Assert.Contains("Total Messages Processed: 15", report);
    }

    [Test]
    public async Task GenerateReport_ContainsCategoryBreakdown()
    {
        var middleware = new AnalyticsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(category: "Orders"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GenerateReport();

        Assert.Contains("Messages by Category:", report);
        Assert.Contains("Orders:", report);
    }

    [Test]
    public async Task GenerateReport_ContainsAgentWorkload()
    {
        var middleware = new AnalyticsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(receiverId: "order-agent"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GenerateReport();

        Assert.Contains("Agent Workload:", report);
        Assert.Contains("order-agent:", report);
    }

    [Test]
    public async Task GenerateReport_ContainsPercentages()
    {
        var middleware = new AnalyticsMiddleware();

        // 2 of 4 messages = 50%
        for (int i = 0; i < 2; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(category: "Orders"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        for (int i = 0; i < 2; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(category: "Payments"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var report = middleware.GenerateReport();

        Assert.Contains("50.0%", report);
    }

    [Test]
    public async Task GenerateReport_CategoriesOrderedByCount()
    {
        var middleware = new AnalyticsMiddleware();

        // First category: 3 messages
        for (int i = 0; i < 3; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(category: "Most"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        // Second category: 1 message
        await middleware.InvokeAsync(
            CreateTestMessage(category: "Least"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Third category: 2 messages
        for (int i = 0; i < 2; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(category: "Middle"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var report = middleware.GenerateReport();

        // "Most" should appear before "Middle" which should appear before "Least"
        var mostIndex = report.IndexOf("Most:");
        var middleIndex = report.IndexOf("Middle:");
        var leastIndex = report.IndexOf("Least:");

        Assert.True(mostIndex < middleIndex, "Most should come before Middle");
        Assert.True(middleIndex < leastIndex, "Middle should come before Least");
    }

    #endregion

    #region Pipeline Integration Tests

    [Test]
    public async Task Analytics_PassesThroughToNextMiddleware()
    {
        var middleware = new AnalyticsMiddleware();
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task Analytics_ReturnsResultFromNextMiddleware()
    {
        var middleware = new AnalyticsMiddleware();

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Custom Response")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Custom Response", result.Response);
    }

    [Test]
    public async Task Analytics_PropagatesFailureFromNextMiddleware()
    {
        var middleware = new AnalyticsMiddleware();

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Fail("Downstream Error")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Downstream Error", result.Error!);
    }

    [Test]
    public async Task Analytics_WorksInPipeline()
    {
        var pipeline = new MiddlewarePipeline();
        var analytics = new AnalyticsMiddleware();

        pipeline.Use(analytics);

        var builtPipeline = pipeline.Build((msg, ct) =>
            Task.FromResult(MessageResult.Ok("Final Handler")));

        await builtPipeline(CreateTestMessage(category: "PipelineTest"), CancellationToken.None);

        var report = analytics.GetReport();
        Assert.Equal(1, report.TotalMessages);
        Assert.True(report.CategoryCounts.ContainsKey("PipelineTest"));
    }

    [Test]
    public async Task Analytics_WorksWithOtherMiddleware()
    {
        var pipeline = new MiddlewarePipeline();
        var analytics = new AnalyticsMiddleware();

        pipeline.Use(new ValidationMiddleware());
        pipeline.Use(analytics);
        pipeline.Use(new TimingMiddleware());

        var builtPipeline = pipeline.Build((msg, ct) =>
            Task.FromResult(MessageResult.Ok("Done")));

        var message = new AgentMessage
        {
            SenderId = "sender",
            Subject = "Subject",
            Content = "Content",
            Category = "IntegrationTest",
            ReceiverId = "receiver"
        };

        await builtPipeline(message, CancellationToken.None);

        var report = analytics.GetReport();
        Assert.Equal(1, report.TotalMessages);
        Assert.Equal(1, report.CategoryCounts["IntegrationTest"]);
        Assert.Equal(1, report.AgentWorkload["receiver"]);
    }

    #endregion

    #region Concurrent Updates Tests

    [Test]
    public async Task Analytics_ConcurrentUpdates_ThreadSafe()
    {
        var middleware = new AnalyticsMiddleware();
        var tasks = new List<Task>();
        var messageCount = 100;

        for (int i = 0; i < messageCount; i++)
        {
            var category = $"category-{i % 5}"; // 5 different categories
            var receiver = $"agent-{i % 3}"; // 3 different agents

            tasks.Add(middleware.InvokeAsync(
                CreateTestMessage(category: category, receiverId: receiver),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        var report = middleware.GetReport();
        Assert.Equal(messageCount, report.TotalMessages);
        Assert.Equal(5, report.CategoryCounts.Count);
        Assert.Equal(3, report.AgentWorkload.Count);

        // Verify totals match
        var categoryTotal = report.CategoryCounts.Values.Sum();
        var agentTotal = report.AgentWorkload.Values.Sum();
        Assert.Equal(messageCount, categoryTotal);
        Assert.Equal(messageCount, agentTotal);
    }

    [Test]
    public async Task Analytics_ParallelMessagesToSameCategory_Aggregates()
    {
        var middleware = new AnalyticsMiddleware();
        var tasks = new List<Task>();

        for (int i = 0; i < 50; i++)
        {
            tasks.Add(middleware.InvokeAsync(
                CreateTestMessage(category: "concurrent-category"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        var report = middleware.GetReport();
        Assert.Equal(50, report.CategoryCounts["concurrent-category"]);
    }

    [Test]
    public async Task Analytics_ParallelMessagesToSameAgent_Aggregates()
    {
        var middleware = new AnalyticsMiddleware();
        var tasks = new List<Task>();

        for (int i = 0; i < 50; i++)
        {
            tasks.Add(middleware.InvokeAsync(
                CreateTestMessage(receiverId: "busy-agent"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        var report = middleware.GetReport();
        Assert.Equal(50, report.AgentWorkload["busy-agent"]);
    }

    #endregion

    #region Edge Cases Tests

    [Test]
    public async Task Analytics_HighVolume_HandlesGracefully()
    {
        var middleware = new AnalyticsMiddleware();
        var messageCount = 1000;

        for (int i = 0; i < messageCount; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage(category: $"cat-{i % 100}"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);
        }

        var report = middleware.GetReport();
        Assert.Equal(messageCount, report.TotalMessages);
        Assert.Equal(100, report.CategoryCounts.Count);
        Assert.True(report.CategoryCounts.Values.All(c => c == 10));
    }

    [Test]
    public async Task Analytics_SpecialCharactersInCategory_Handled()
    {
        var middleware = new AnalyticsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(category: "category/with/slashes"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(category: "category with spaces"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(category: "category:with:colons"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(3, report.CategoryCounts.Count);
        Assert.True(report.CategoryCounts.ContainsKey("category/with/slashes"));
        Assert.True(report.CategoryCounts.ContainsKey("category with spaces"));
        Assert.True(report.CategoryCounts.ContainsKey("category:with:colons"));
    }

    [Test]
    public async Task Analytics_CaseSensitiveCategories()
    {
        var middleware = new AnalyticsMiddleware();

        await middleware.InvokeAsync(
            CreateTestMessage(category: "Orders"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(category: "orders"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            CreateTestMessage(category: "ORDERS"),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        // Categories are case-sensitive, so these should be tracked separately
        Assert.Equal(3, report.CategoryCounts.Count);
    }

    [Test]
    public async Task Analytics_BothCategoryAndReceiverEmpty_NoTracking()
    {
        var middleware = new AnalyticsMiddleware();
        var message = new AgentMessage
        {
            SenderId = "sender",
            Subject = "Test",
            Content = "Content",
            Category = "",
            ReceiverId = ""
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.Equal(1, report.TotalMessages);
        Assert.Equal(0, report.CategoryCounts.Count);
        Assert.Equal(0, report.AgentWorkload.Count);
    }

    [Test]
    public async Task Analytics_WhitespaceCategory_Tracked()
    {
        var middleware = new AnalyticsMiddleware();

        // Whitespace-only category is not empty, so it will be tracked
        await middleware.InvokeAsync(
            CreateTestMessage(category: "   "),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        // The implementation uses string.IsNullOrEmpty, not string.IsNullOrWhiteSpace
        // So whitespace-only strings will be tracked
        Assert.Equal(1, report.CategoryCounts.Count);
        Assert.True(report.CategoryCounts.ContainsKey("   "));
    }

    [Test]
    public async Task Analytics_LongCategoryName_Handled()
    {
        var middleware = new AnalyticsMiddleware();
        var longCategory = new string('a', 1000);

        await middleware.InvokeAsync(
            CreateTestMessage(category: longCategory),
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var report = middleware.GetReport();
        Assert.True(report.CategoryCounts.ContainsKey(longCategory));
        Assert.Equal(1, report.CategoryCounts[longCategory]);
    }

    #endregion

    #region AnalyticsReport Class Tests

    [Test]
    public void AnalyticsReport_DefaultValues()
    {
        var report = new AnalyticsReport();

        Assert.Equal(0, report.TotalMessages);
        Assert.NotNull(report.CategoryCounts);
        Assert.NotNull(report.AgentWorkload);
    }

    [Test]
    public void AnalyticsReport_DictionariesAreMutable()
    {
        var report = new AnalyticsReport();

        report.CategoryCounts["Test"] = 5;
        report.AgentWorkload["Agent1"] = 10;

        Assert.Equal(5, report.CategoryCounts["Test"]);
        Assert.Equal(10, report.AgentWorkload["Agent1"]);
    }

    #endregion

    #region Helper Methods

    private static AgentMessage CreateTestMessage(
        string senderId = "test-sender",
        string category = "TestCategory",
        string receiverId = "test-receiver")
    {
        return new AgentMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Subject = "Test Subject",
            Content = "Test Content",
            Category = category
        };
    }

    #endregion
}
