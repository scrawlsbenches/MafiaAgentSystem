using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for DistributedTracingMiddleware
/// </summary>
public class DistributedTracingMiddlewareTests
{
    #region Trace ID Generation Tests

    [Test]
    public async Task TracingMiddleware_GeneratesNewTraceId_WhenNotPresent()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("TraceId"));
        var traceId = message.Metadata["TraceId"].ToString();
        Assert.NotNull(traceId);
        Assert.Equal(32, traceId!.Length); // GUID "N" format is 32 chars
    }

    [Test]
    public async Task TracingMiddleware_PropagatesExistingTraceId()
    {
        var middleware = new DistributedTracingMiddleware();
        var existingTraceId = "abc123def456789012345678901234ab";
        var message = CreateTestMessage();
        message.Metadata["TraceId"] = existingTraceId;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(existingTraceId, message.Metadata["TraceId"].ToString());
    }

    [Test]
    public async Task TracingMiddleware_GeneratesUniqueTraceIds()
    {
        var middleware = new DistributedTracingMiddleware();
        var message1 = CreateTestMessage("sender-1");
        var message2 = CreateTestMessage("sender-2");

        await middleware.InvokeAsync(
            message1,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            message2,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traceId1 = message1.Metadata["TraceId"].ToString();
        var traceId2 = message2.Metadata["TraceId"].ToString();

        Assert.NotEqual(traceId1, traceId2);
    }

    #endregion

    #region Span ID Tests

    [Test]
    public async Task TracingMiddleware_GeneratesSpanId()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("SpanId"));
        var spanId = message.Metadata["SpanId"].ToString();
        Assert.NotNull(spanId);
        Assert.Equal(16, spanId!.Length); // First 16 chars of GUID "N" format
    }

    [Test]
    public async Task TracingMiddleware_GeneratesUniqueSpanIds()
    {
        var middleware = new DistributedTracingMiddleware();
        var message1 = CreateTestMessage("sender-1");
        var message2 = CreateTestMessage("sender-2");

        await middleware.InvokeAsync(
            message1,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            message2,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var spanId1 = message1.Metadata["SpanId"].ToString();
        var spanId2 = message2.Metadata["SpanId"].ToString();

        Assert.NotEqual(spanId1, spanId2);
    }

    #endregion

    #region Parent-Child Span Hierarchy Tests

    [Test]
    public async Task TracingMiddleware_SetsParentSpanId_WhenSpanIdExists()
    {
        var middleware = new DistributedTracingMiddleware();
        var existingTraceId = "abc123def456789012345678901234ab";
        var existingSpanId = "parentspan123456";
        var message = CreateTestMessage();
        message.Metadata["TraceId"] = existingTraceId;
        message.Metadata["SpanId"] = existingSpanId;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal(1, traces.Count);
        Assert.Equal(existingSpanId, traces[0].ParentSpanId);
    }

    [Test]
    public async Task TracingMiddleware_NoParentSpanId_WhenNoExistingSpan()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal(1, traces.Count);
        Assert.Null(traces[0].ParentSpanId);
    }

    [Test]
    public async Task TracingMiddleware_CreatesSpanHierarchy_WhenChained()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        // First invocation creates a root span
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        // The message now has TraceId and SpanId set
        // Second invocation should use the existing SpanId as ParentSpanId
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal(2, traces.Count);

        // First trace should have no parent
        var rootSpan = traces.First(t => t.ParentSpanId == null);
        Assert.NotNull(rootSpan);

        // Second trace should have the first span as parent
        var childSpan = traces.First(t => t.ParentSpanId != null);
        Assert.NotNull(childSpan);
        Assert.Equal(rootSpan.TraceId, childSpan.TraceId);
    }

    #endregion

    #region Service Name Tests

    [Test]
    public async Task TracingMiddleware_UsesDefaultServiceName()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal(1, traces.Count);
        Assert.Equal("AgentRouter", traces[0].ServiceName);
    }

    [Test]
    public async Task TracingMiddleware_UsesCustomServiceName()
    {
        var middleware = new DistributedTracingMiddleware("CustomService");
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal(1, traces.Count);
        Assert.Equal("CustomService", traces[0].ServiceName);
    }

    [Theory]
    [InlineData("MafiaGame")]
    [InlineData("OrderService")]
    [InlineData("")]
    public async Task TracingMiddleware_AcceptsVariousServiceNames(string serviceName)
    {
        var middleware = new DistributedTracingMiddleware(serviceName);
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal(serviceName, traces[0].ServiceName);
    }

    #endregion

    #region Span Operation Name Tests

    [Test]
    public async Task TracingMiddleware_SetsOperationName_FromSubject()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();
        message.Subject = "ProcessOrder";

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal(1, traces.Count);
        Assert.Equal("ProcessMessage: ProcessOrder", traces[0].OperationName);
    }

    [Test]
    public async Task TracingMiddleware_HandlesEmptySubject()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();
        message.Subject = "";

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal("ProcessMessage: ", traces[0].OperationName);
    }

    #endregion

    #region Span Timing Tests

    [Test]
    public async Task TracingMiddleware_SetsDuration()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            async (msg, ct) =>
            {
                await Task.Delay(10);
                return MessageResult.Ok("Done");
            },
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal(1, traces.Count);
        Assert.True(traces[0].Duration.TotalMilliseconds >= 0);
    }

    [Test]
    public async Task TracingMiddleware_SetsStartTime()
    {
        var beforeTest = DateTime.UtcNow;
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var afterTest = DateTime.UtcNow;
        var traces = middleware.GetTraces();

        Assert.True(traces[0].StartTime >= beforeTest);
        Assert.True(traces[0].StartTime <= afterTest);
    }

    [Test]
    public async Task TracingMiddleware_DurationReflectsProcessingTime()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            async (msg, ct) =>
            {
                await Task.Delay(50);
                return MessageResult.Ok("Done");
            },
            CancellationToken.None);

        var traces = middleware.GetTraces();
        // Should be at least 50ms, allowing some tolerance
        Assert.True(traces[0].Duration.TotalMilliseconds >= 40);
    }

    #endregion

    #region Success Result Tests

    [Test]
    public async Task TracingMiddleware_SetsSuccess_WhenResultSucceeds()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Success")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.True(traces[0].Success);
    }

    [Test]
    public async Task TracingMiddleware_AddsSuccessTag_WhenResultSucceeds()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Success")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.True(traces[0].Tags.ContainsKey("result.success"));
        Assert.Equal("True", traces[0].Tags["result.success"]);
    }

    [Test]
    public async Task TracingMiddleware_ReturnsOriginalResult_OnSuccess()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Expected Response")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Expected Response", result.Response);
    }

    #endregion

    #region Failure Result Tests

    [Test]
    public async Task TracingMiddleware_SetsSuccessFalse_WhenResultFails()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Fail("Error occurred")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.False(traces[0].Success);
    }

    [Test]
    public async Task TracingMiddleware_AddsErrorTag_WhenResultFails()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Fail("Error occurred")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.True(traces[0].Tags.ContainsKey("error.message"));
        Assert.Equal("Error occurred", traces[0].Tags["error.message"]);
    }

    [Test]
    public async Task TracingMiddleware_AddsSuccessFalseTag_WhenResultFails()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Fail("Error")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal("False", traces[0].Tags["result.success"]);
    }

    [Test]
    public async Task TracingMiddleware_ReturnsOriginalResult_OnFailure()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Fail("Expected Error")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Expected Error", result.Error);
    }

    [Test]
    public async Task TracingMiddleware_HandlesNullError_WhenResultFails()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        var failResult = new MessageResult { Success = false, Error = null };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(failResult),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.False(traces[0].Success);
        // Should not add error.message tag when Error is null
        Assert.False(traces[0].Tags.ContainsKey("error.message"));
    }

    #endregion

    #region Exception Handling Tests

    [Test]
    public async Task TracingMiddleware_RecordsException_AndRethrows()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await middleware.InvokeAsync(
                message,
                (msg, ct) => throw new InvalidOperationException("Test exception"),
                CancellationToken.None);
        });

        Assert.Equal("Test exception", thrownException.Message);
    }

    [Test]
    public async Task TracingMiddleware_SetsSuccessFalse_OnException()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        try
        {
            await middleware.InvokeAsync(
                message,
                (msg, ct) => throw new InvalidOperationException("Test exception"),
                CancellationToken.None);
        }
        catch { }

        var traces = middleware.GetTraces();
        Assert.Equal(1, traces.Count);
        Assert.False(traces[0].Success);
    }

    [Test]
    public async Task TracingMiddleware_AddsErrorTags_OnException()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        try
        {
            await middleware.InvokeAsync(
                message,
                (msg, ct) => throw new InvalidOperationException("Test exception"),
                CancellationToken.None);
        }
        catch { }

        var traces = middleware.GetTraces();
        Assert.True(traces[0].Tags.ContainsKey("error.type"));
        Assert.Equal("InvalidOperationException", traces[0].Tags["error.type"]);
        Assert.True(traces[0].Tags.ContainsKey("error.message"));
        Assert.Equal("Test exception", traces[0].Tags["error.message"]);
    }

    [Test]
    public async Task TracingMiddleware_RecordsDuration_EvenOnException()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        try
        {
            await middleware.InvokeAsync(
                message,
                async (msg, ct) =>
                {
                    await Task.Delay(10);
                    throw new Exception("Delayed exception");
                },
                CancellationToken.None);
        }
        catch { }

        var traces = middleware.GetTraces();
        Assert.True(traces[0].Duration.TotalMilliseconds >= 0);
    }

    [Theory]
    [InlineData("ArgumentException")]
    [InlineData("NullReferenceException")]
    [InlineData("TimeoutException")]
    public async Task TracingMiddleware_RecordsVariousExceptionTypes(string exceptionTypeName)
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        Exception exceptionToThrow = exceptionTypeName switch
        {
            "ArgumentException" => new ArgumentException("Test"),
            "NullReferenceException" => new NullReferenceException("Test"),
            "TimeoutException" => new TimeoutException("Test"),
            _ => new Exception("Test")
        };

        try
        {
            await middleware.InvokeAsync(
                message,
                (msg, ct) => throw exceptionToThrow,
                CancellationToken.None);
        }
        catch { }

        var traces = middleware.GetTraces();
        Assert.Equal(exceptionTypeName, traces[0].Tags["error.type"]);
    }

    #endregion

    #region Tags Tests

    [Test]
    public async Task TracingMiddleware_AddsMessageIdTag()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();
        var expectedId = message.Id;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.True(traces[0].Tags.ContainsKey("message.id"));
        Assert.Equal(expectedId, traces[0].Tags["message.id"]);
    }

    [Test]
    public async Task TracingMiddleware_AddsSenderTag()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage("test-sender");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.True(traces[0].Tags.ContainsKey("message.sender"));
        Assert.Equal("test-sender", traces[0].Tags["message.sender"]);
    }

    [Test]
    public async Task TracingMiddleware_AddsCategoryTag()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();
        message.Category = "OrderProcessing";

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.True(traces[0].Tags.ContainsKey("message.category"));
        Assert.Equal("OrderProcessing", traces[0].Tags["message.category"]);
    }

    [Test]
    public async Task TracingMiddleware_AddsPriorityTag()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();
        message.Priority = MessagePriority.Urgent;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.True(traces[0].Tags.ContainsKey("message.priority"));
        Assert.Equal("Urgent", traces[0].Tags["message.priority"]);
    }

    [Theory]
    [InlineData(MessagePriority.Low, "Low")]
    [InlineData(MessagePriority.Normal, "Normal")]
    [InlineData(MessagePriority.High, "High")]
    [InlineData(MessagePriority.Urgent, "Urgent")]
    public async Task TracingMiddleware_TagsAllPriorityLevels(MessagePriority priority, string expectedTag)
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();
        message.Priority = priority;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal(expectedTag, traces[0].Tags["message.priority"]);
    }

    [Test]
    public async Task TracingMiddleware_HandlesEmptyCategory()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();
        message.Category = "";

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal("", traces[0].Tags["message.category"]);
    }

    #endregion

    #region GetTraces Tests

    [Test]
    public void GetTraces_ReturnsEmptyList_WhenNoTraces()
    {
        var middleware = new DistributedTracingMiddleware();

        var traces = middleware.GetTraces();

        Assert.NotNull(traces);
        Assert.Empty(traces);
    }

    [Test]
    public async Task GetTraces_ReturnsAllTraces()
    {
        var middleware = new DistributedTracingMiddleware();

        for (int i = 0; i < 5; i++)
        {
            await middleware.InvokeAsync(
                CreateTestMessage($"sender-{i}"),
                (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
                CancellationToken.None);
        }

        var traces = middleware.GetTraces();
        Assert.Equal(5, traces.Count);
    }

    [Test]
    public async Task GetTraces_ReturnsNewListEachTime()
    {
        var middleware = new DistributedTracingMiddleware();
        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces1 = middleware.GetTraces();
        var traces2 = middleware.GetTraces();

        Assert.NotSame(traces1, traces2);
        Assert.Equal(traces1.Count, traces2.Count);
    }

    #endregion

    #region ExportJaegerFormat Tests

    [Test]
    public async Task ExportJaegerFormat_IncludesHeader()
    {
        var middleware = new DistributedTracingMiddleware();
        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var export = middleware.ExportJaegerFormat();

        Assert.Contains("Jaeger Trace Export:", export);
    }

    [Test]
    public async Task ExportJaegerFormat_IncludesTraceId()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traceId = message.Metadata["TraceId"].ToString();
        var export = middleware.ExportJaegerFormat();

        Assert.Contains($"Trace ID: {traceId}", export);
    }

    [Test]
    public async Task ExportJaegerFormat_IncludesSpanInfo()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();
        message.Subject = "TestOperation";

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var export = middleware.ExportJaegerFormat();

        Assert.Contains("Span: ProcessMessage: TestOperation", export);
        Assert.Contains("Duration:", export);
        Assert.Contains("Success:", export);
    }

    [Test]
    public async Task ExportJaegerFormat_ShowsChildSpanWithArrow()
    {
        var middleware = new DistributedTracingMiddleware();
        var existingTraceId = "abc123def456789012345678901234ab";
        var existingSpanId = "parentspan123456";
        var message = CreateTestMessage();
        message.Metadata["TraceId"] = existingTraceId;
        message.Metadata["SpanId"] = existingSpanId;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var export = middleware.ExportJaegerFormat();

        // Child spans are indented with arrow
        Assert.Contains("  \u2192 ", export); // Arrow character
    }

    [Test]
    public void ExportJaegerFormat_ReturnsEmptyExport_WhenNoTraces()
    {
        var middleware = new DistributedTracingMiddleware();

        var export = middleware.ExportJaegerFormat();

        Assert.Contains("Jaeger Trace Export:", export);
        // Should not contain any trace IDs
        Assert.False(export.Contains("Trace ID:"));
    }

    [Test]
    public async Task ExportJaegerFormat_GroupsByTraceId()
    {
        var middleware = new DistributedTracingMiddleware();

        // Create two messages with same trace ID
        var sharedTraceId = "abc123def456789012345678901234ab";
        var message1 = CreateTestMessage();
        message1.Metadata["TraceId"] = sharedTraceId;
        message1.Subject = "First";

        var message2 = CreateTestMessage();
        message2.Metadata["TraceId"] = sharedTraceId;
        message2.Subject = "Second";

        await middleware.InvokeAsync(
            message1,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            message2,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var export = middleware.ExportJaegerFormat();

        // Should only have one "Trace ID:" line for the shared trace
        var traceIdCount = CountOccurrences(export, $"Trace ID: {sharedTraceId}");
        Assert.Equal(1, traceIdCount);
    }

    #endregion

    #region Pipeline Integration Tests

    [Test]
    public async Task TracingMiddleware_WorksInPipeline()
    {
        var pipeline = new MiddlewarePipeline();
        var tracingMiddleware = new DistributedTracingMiddleware("TestService");

        pipeline.Use(tracingMiddleware);

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Final")));

        var result = await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, tracingMiddleware.GetTraces().Count);
    }

    [Test]
    public async Task TracingMiddleware_PropagatesTraceContext_ThroughPipeline()
    {
        var pipeline = new MiddlewarePipeline();
        var tracingMiddleware = new DistributedTracingMiddleware();

        string? capturedTraceId = null;
        string? capturedSpanId = null;

        pipeline.Use(tracingMiddleware);
        pipeline.Use(next => async (msg, ct) =>
        {
            capturedTraceId = msg.Metadata["TraceId"]?.ToString();
            capturedSpanId = msg.Metadata["SpanId"]?.ToString();
            return await next(msg, ct);
        });

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));
        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.NotNull(capturedTraceId);
        Assert.NotNull(capturedSpanId);
        Assert.Equal(32, capturedTraceId!.Length);
        Assert.Equal(16, capturedSpanId!.Length);
    }

    [Test]
    public async Task TracingMiddleware_CapturesDownstreamFailure()
    {
        var pipeline = new MiddlewarePipeline();
        var tracingMiddleware = new DistributedTracingMiddleware();

        pipeline.Use(tracingMiddleware);
        pipeline.Use(next => (msg, ct) => Task.FromResult(MessageResult.Fail("Downstream failure")));

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Should not reach")));
        var result = await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.False(result.Success);
        var traces = tracingMiddleware.GetTraces();
        Assert.False(traces[0].Success);
        Assert.Equal("Downstream failure", traces[0].Tags["error.message"]);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task TracingMiddleware_HandlesEmptySenderId()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "",
            Subject = "Test",
            Content = "Content"
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var traces = middleware.GetTraces();
        Assert.Equal("", traces[0].Tags["message.sender"]);
    }

    [Test]
    public async Task TracingMiddleware_PreservesExistingMetadata()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();
        message.Metadata["CustomKey"] = "CustomValue";

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("CustomKey"));
        Assert.Equal("CustomValue", message.Metadata["CustomKey"]);
    }

    [Test]
    public async Task TracingMiddleware_ThreadSafe_ConcurrentInvocations()
    {
        var middleware = new DistributedTracingMiddleware();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await middleware.InvokeAsync(
                    CreateTestMessage($"sender-{index}"),
                    (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
                    CancellationToken.None);
            }));
        }

        await Task.WhenAll(tasks);

        var traces = middleware.GetTraces();
        Assert.Equal(100, traces.Count);
    }

    [Test]
    public async Task TracingMiddleware_MultipleInstances_Independent()
    {
        var middleware1 = new DistributedTracingMiddleware("Service1");
        var middleware2 = new DistributedTracingMiddleware("Service2");

        await middleware1.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        await middleware2.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(1, middleware1.GetTraces().Count);
        Assert.Equal(1, middleware2.GetTraces().Count);
        Assert.Equal("Service1", middleware1.GetTraces()[0].ServiceName);
        Assert.Equal("Service2", middleware2.GetTraces()[0].ServiceName);
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

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion
}
