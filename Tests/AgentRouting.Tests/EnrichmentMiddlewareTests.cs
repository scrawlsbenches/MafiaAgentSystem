using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for EnrichmentMiddleware - validates metadata enrichment,
/// timestamp addition, correlation ID generation, and context data enrichment.
/// </summary>
public class EnrichmentMiddlewareTests
{
    #region Basic Enrichment Tests

    [Test]
    public async Task EnrichmentMiddleware_AddsReceivedAtTimestamp()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("ReceivedAt"));
        Assert.IsType<DateTime>(message.Metadata["ReceivedAt"]);
    }

    [Test]
    public async Task EnrichmentMiddleware_AddsProcessedByMachineName()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("ProcessedBy"));
        Assert.Equal(Environment.MachineName, message.Metadata["ProcessedBy"]);
    }

    [Test]
    public async Task EnrichmentMiddleware_GeneratesConversationId_WhenNull()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();
        message.ConversationId = null;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.NotNull(message.ConversationId);
        Assert.True(Guid.TryParse(message.ConversationId, out _), "ConversationId should be a valid GUID");
    }

    [Test]
    public async Task EnrichmentMiddleware_GeneratesConversationId_WhenEmpty()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();
        message.ConversationId = "";

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.NotNull(message.ConversationId);
        Assert.True(message.ConversationId.Length > 0, "ConversationId should not be empty");
        Assert.True(Guid.TryParse(message.ConversationId, out _), "ConversationId should be a valid GUID");
    }

    #endregion

    #region Timestamp Enrichment Tests

    [Test]
    public async Task EnrichmentMiddleware_DoesNotOverwriteExistingReceivedAt()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();
        var existingTimestamp = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        message.Metadata["ReceivedAt"] = existingTimestamp;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(existingTimestamp, message.Metadata["ReceivedAt"]);
    }

    [Test]
    public async Task EnrichmentMiddleware_ReceivedAtIsRecentTimestamp()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();
        var beforeCall = DateTime.UtcNow;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var afterCall = DateTime.UtcNow;
        var receivedAt = (DateTime)message.Metadata["ReceivedAt"];

        Assert.True(receivedAt >= beforeCall, "ReceivedAt should be >= time before call");
        Assert.True(receivedAt <= afterCall, "ReceivedAt should be <= time after call");
    }

    #endregion

    #region ProcessedBy Enrichment Tests

    [Test]
    public async Task EnrichmentMiddleware_OverwritesExistingProcessedBy()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();
        message.Metadata["ProcessedBy"] = "OldMachine";

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        // ProcessedBy should be overwritten with current machine name
        Assert.Equal(Environment.MachineName, message.Metadata["ProcessedBy"]);
    }

    [Test]
    public async Task EnrichmentMiddleware_ProcessedByIsNotEmpty()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var processedBy = message.Metadata["ProcessedBy"] as string;
        Assert.NotNull(processedBy);
        Assert.True(processedBy!.Length > 0, "ProcessedBy should not be empty");
    }

    #endregion

    #region Correlation ID Tests

    [Test]
    public async Task EnrichmentMiddleware_PreservesExistingConversationId()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();
        var existingId = "existing-conversation-123";
        message.ConversationId = existingId;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(existingId, message.ConversationId);
    }

    [Test]
    public async Task EnrichmentMiddleware_GeneratesUniqueConversationIds()
    {
        var middleware = new EnrichmentMiddleware();
        var message1 = CreateTestMessage();
        var message2 = CreateTestMessage();
        message1.ConversationId = null;
        message2.ConversationId = null;

        await middleware.InvokeAsync(
            message1,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            message2,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.NotEqual(message1.ConversationId, message2.ConversationId);
    }

    [Test]
    public async Task EnrichmentMiddleware_ConversationIdPreservedAcrossCalls()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();
        message.ConversationId = "keep-this-id";

        // Multiple calls should preserve the same ConversationId
        for (int i = 0; i < 3; i++)
        {
            await middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
                CancellationToken.None);

            Assert.Equal("keep-this-id", message.ConversationId);
        }
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task EnrichmentMiddleware_HandlesEmptyMetadata()
    {
        var middleware = new EnrichmentMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Test Subject",
            Content = "Test Content",
            Metadata = new Dictionary<string, object>()
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("ReceivedAt"));
        Assert.True(message.Metadata.ContainsKey("ProcessedBy"));
    }

    [Test]
    public async Task EnrichmentMiddleware_HandlesMetadataWithOtherKeys()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();
        message.Metadata["CustomKey1"] = "Value1";
        message.Metadata["CustomKey2"] = 42;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        // Original keys should be preserved
        Assert.Equal("Value1", message.Metadata["CustomKey1"]);
        Assert.Equal(42, message.Metadata["CustomKey2"]);
        // Enrichment keys should be added
        Assert.True(message.Metadata.ContainsKey("ReceivedAt"));
        Assert.True(message.Metadata.ContainsKey("ProcessedBy"));
    }

    [Test]
    public async Task EnrichmentMiddleware_WhitespaceOnlyConversationId_NotOverwritten()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();
        message.ConversationId = "   "; // Whitespace only

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        // The implementation checks for IsNullOrEmpty, so whitespace-only should be preserved
        Assert.Equal("   ", message.ConversationId);
    }

    #endregion

    #region Middleware Pipeline Behavior Tests

    [Test]
    public async Task EnrichmentMiddleware_CallsNextDelegate()
    {
        var middleware = new EnrichmentMiddleware();
        var nextCalled = false;

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
    }

    [Test]
    public async Task EnrichmentMiddleware_ReturnsResultFromNextDelegate()
    {
        var middleware = new EnrichmentMiddleware();

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Expected Response")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Expected Response", result.Response);
    }

    [Test]
    public async Task EnrichmentMiddleware_PropagatesFailureFromNextDelegate()
    {
        var middleware = new EnrichmentMiddleware();

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Fail("Expected Error")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Expected Error", result.Error!);
    }

    [Test]
    public async Task EnrichmentMiddleware_EnrichesBeforeCallingNext()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();
        message.ConversationId = null;
        var enrichmentVerified = false;

        await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                // Verify enrichment happened before next was called
                enrichmentVerified = msg.Metadata.ContainsKey("ReceivedAt") &&
                                     msg.Metadata.ContainsKey("ProcessedBy") &&
                                     !string.IsNullOrEmpty(msg.ConversationId);
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.True(enrichmentVerified, "Message should be enriched before calling next delegate");
    }

    #endregion

    #region Integration with Pipeline Tests

    [Test]
    public async Task EnrichmentMiddleware_WorksInPipeline()
    {
        var pipeline = new MiddlewarePipeline();
        pipeline.Use(new EnrichmentMiddleware());

        var message = CreateTestMessage();
        message.ConversationId = null;

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));
        var result = await builtPipeline(message, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(message.Metadata.ContainsKey("ReceivedAt"));
        Assert.True(message.Metadata.ContainsKey("ProcessedBy"));
        Assert.NotNull(message.ConversationId);
    }

    [Test]
    public async Task EnrichmentMiddleware_WorksWithOtherMiddleware()
    {
        var pipeline = new MiddlewarePipeline();
        var executionOrder = new List<string>();

        pipeline.Use(next => async (msg, ct) =>
        {
            executionOrder.Add("Before-Custom");
            var result = await next(msg, ct);
            executionOrder.Add("After-Custom");
            return result;
        });

        pipeline.Use(new EnrichmentMiddleware());

        var message = CreateTestMessage();
        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            executionOrder.Add("Handler");
            return Task.FromResult(MessageResult.Ok("Done"));
        });

        await builtPipeline(message, CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("Before-Custom", executionOrder[0]);
        Assert.Equal("Handler", executionOrder[1]);
        Assert.Equal("After-Custom", executionOrder[2]);
        Assert.True(message.Metadata.ContainsKey("ReceivedAt"));
    }

    [Test]
    public async Task EnrichmentMiddleware_MultiplePasses_ReceivedAtNotOverwritten()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();

        // First pass - adds ReceivedAt
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var firstReceivedAt = (DateTime)message.Metadata["ReceivedAt"];

        // Small delay to ensure different timestamp
        await Task.Delay(1);

        // Second pass - should not overwrite ReceivedAt
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var secondReceivedAt = (DateTime)message.Metadata["ReceivedAt"];

        Assert.Equal(firstReceivedAt, secondReceivedAt);
    }

    #endregion

    #region Metadata Type Tests

    [Test]
    public async Task EnrichmentMiddleware_ReceivedAtIsUtcDateTime()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var receivedAt = (DateTime)message.Metadata["ReceivedAt"];
        Assert.Equal(DateTimeKind.Utc, receivedAt.Kind);
    }

    [Test]
    public async Task EnrichmentMiddleware_ProcessedByIsString()
    {
        var middleware = new EnrichmentMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var processedBy = message.Metadata["ProcessedBy"];
        Assert.IsType<string>(processedBy);
    }

    #endregion

    #region Cancellation Token Tests

    [Test]
    public async Task EnrichmentMiddleware_PassesCancellationToken()
    {
        var middleware = new EnrichmentMiddleware();
        var cts = new CancellationTokenSource();
        var receivedToken = CancellationToken.None;

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                receivedToken = ct;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            cts.Token);

        Assert.Equal(cts.Token, receivedToken);
    }

    #endregion

    #region Multiple Enrichment Middleware Instances

    [Test]
    public async Task EnrichmentMiddleware_MultipleInstances_WorkCorrectly()
    {
        var pipeline = new MiddlewarePipeline();
        pipeline.Use(new EnrichmentMiddleware());
        pipeline.Use(new EnrichmentMiddleware());

        var message = CreateTestMessage();
        message.ConversationId = null;

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));
        await builtPipeline(message, CancellationToken.None);

        // Should have enrichment data (first middleware sets it, second preserves it)
        Assert.True(message.Metadata.ContainsKey("ReceivedAt"));
        Assert.True(message.Metadata.ContainsKey("ProcessedBy"));
        Assert.NotNull(message.ConversationId);
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
