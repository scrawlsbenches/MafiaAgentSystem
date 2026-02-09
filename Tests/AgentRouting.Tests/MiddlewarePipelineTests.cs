using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using TestUtilities;

namespace TestRunner.Tests;

/// <summary>
/// Tests for the middleware pipeline behavior.
/// </summary>
public class MiddlewarePipelineTests
{
    private AgentMessage CreateTestMessage(string subject = "Test", string content = "Test content")
    {
        return new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = "test-sender",
            ReceiverId = "test-receiver",
            Subject = subject,
            Content = content,
            Category = "Test",
            Priority = MessagePriority.Normal
        };
    }

    [Test]
    public async Task EmptyPipeline_ExecutesHandler()
    {
        var pipeline = new MiddlewarePipeline();
        var handlerCalled = false;

        MessageDelegate handler = (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("Handler executed"));
        };

        var executor = pipeline.Build(handler);
        var message = CreateTestMessage();

        var result = await executor(message, CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task SingleMiddleware_ExecutesBeforeAndAfter()
    {
        var pipeline = new MiddlewarePipeline();
        var executionLog = new List<string>();

        pipeline.Use(new SimpleCallbackMiddleware(
            before: () => executionLog.Add("Before"),
            after: () => executionLog.Add("After")
        ));

        MessageDelegate handler = (msg, ct) =>
        {
            executionLog.Add("Handler");
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var executor = pipeline.Build(handler);
        await executor(CreateTestMessage(), CancellationToken.None);

        Assert.Equal(3, executionLog.Count);
        Assert.Equal("Before", executionLog[0]);
        Assert.Equal("Handler", executionLog[1]);
        Assert.Equal("After", executionLog[2]);
    }

    [Test]
    public async Task MultipleMiddleware_ExecuteInOrder()
    {
        var pipeline = new MiddlewarePipeline();
        var executionOrder = new List<int>();

        pipeline.Use(new OrderTrackingMiddleware(1, executionOrder));
        pipeline.Use(new OrderTrackingMiddleware(2, executionOrder));
        pipeline.Use(new OrderTrackingMiddleware(3, executionOrder));

        MessageDelegate handler = (msg, ct) =>
        {
            executionOrder.Add(0); // Handler is "0"
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var executor = pipeline.Build(handler);
        await executor(CreateTestMessage(), CancellationToken.None);

        // Should be: 1-before, 2-before, 3-before, handler, 3-after, 2-after, 1-after
        Assert.Equal(7, executionOrder.Count);
        Assert.Equal(1, executionOrder[0]); // M1 before
        Assert.Equal(2, executionOrder[1]); // M2 before
        Assert.Equal(3, executionOrder[2]); // M3 before
        Assert.Equal(0, executionOrder[3]); // Handler
        Assert.Equal(-3, executionOrder[4]); // M3 after (negative = after)
        Assert.Equal(-2, executionOrder[5]); // M2 after
        Assert.Equal(-1, executionOrder[6]); // M1 after
    }

    [Test]
    public async Task ShortCircuit_StopsPipeline()
    {
        var pipeline = new MiddlewarePipeline();
        var reached = new List<string>();

        pipeline.Use(new SimpleCallbackMiddleware(before: () => reached.Add("M1")));
        pipeline.Use(new ShortCircuitMiddleware("Blocked"));
        pipeline.Use(new SimpleCallbackMiddleware(before: () => reached.Add("M3")));

        MessageDelegate handler = (msg, ct) =>
        {
            reached.Add("Handler");
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var executor = pipeline.Build(handler);
        var result = await executor(CreateTestMessage(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Blocked", result.Error);
        Assert.Equal(1, reached.Count); // Only M1 was reached
        Assert.Equal("M1", reached[0]);
    }

    [Test]
    public async Task MiddlewareException_PropagatesUp()
    {
        var pipeline = new MiddlewarePipeline();

        pipeline.Use(new ThrowingMiddleware());

        MessageDelegate handler = (msg, ct) => Task.FromResult(MessageResult.Ok("Done"));

        var executor = pipeline.Build(handler);

        var exceptionThrown = false;
        try
        {
            await executor(CreateTestMessage(), CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
    }

    [Test]
    public async Task Middleware_CanModifyResult()
    {
        var pipeline = new MiddlewarePipeline();

        pipeline.Use(new ResultModifyingMiddleware("Modified"));

        MessageDelegate handler = (msg, ct) =>
        {
            var result = MessageResult.Ok("Original");
            return Task.FromResult(result);
        };

        var executor = pipeline.Build(handler);
        var result = await executor(CreateTestMessage(), CancellationToken.None);

        Assert.Equal("Modified", result.Response);
    }

    [Test]
    public async Task Middleware_CanAccessMessageData()
    {
        var pipeline = new MiddlewarePipeline();
        string? capturedSubject = null;

        pipeline.Use(new MessageCapturingMiddleware(msg => capturedSubject = msg.Subject));

        MessageDelegate handler = (msg, ct) => Task.FromResult(MessageResult.Ok("Done"));

        var executor = pipeline.Build(handler);
        var message = CreateTestMessage("Important Subject");
        await executor(message, CancellationToken.None);

        Assert.Equal("Important Subject", capturedSubject);
    }

    [Test]
    public async Task CancellationToken_Respected_ThrowsOperationCanceled()
    {
        var pipeline = new MiddlewarePipeline();
        var reachedHandler = false;

        pipeline.Use(new CancellationCheckingMiddleware());

        MessageDelegate handler = (msg, ct) =>
        {
            reachedHandler = true;
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var executor = pipeline.Build(handler);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var exceptionThrown = false;
        try
        {
            await executor(CreateTestMessage(), cts.Token);
        }
        catch (OperationCanceledException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
        Assert.False(reachedHandler);
    }

    [Test]
    public async Task ConcurrentPipelineExecution_ThreadSafe()
    {
        var pipeline = new MiddlewarePipeline();
        var executionCount = 0;

        pipeline.Use(new SimpleCallbackMiddleware(before: () => Interlocked.Increment(ref executionCount)));

        MessageDelegate handler = (msg, ct) => Task.FromResult(MessageResult.Ok("Done"));

        var executor = pipeline.Build(handler);
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await executor(CreateTestMessage(), CancellationToken.None);
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(100, executionCount);
    }

    [Test]
    public async Task MiddlewareContext_SharedAcrossPipeline()
    {
        var pipeline = new MiddlewarePipeline();

        pipeline.Use(new ContextSettingMiddleware("key1", "value1"));
        pipeline.Use(new ContextReadingMiddleware("key1"));

        MessageDelegate handler = (msg, ct) =>
        {
            var result = MessageResult.Ok("Done");
            return Task.FromResult(result);
        };

        var executor = pipeline.Build(handler);
        var message = CreateTestMessage();
        var result = await executor(message, CancellationToken.None);

        // Value should be readable by subsequent middleware
        Assert.True(result.Success);
    }

    [Test]
    public async Task Pipeline_Reusable_MultipleCalls()
    {
        var pipeline = new MiddlewarePipeline();
        var callCount = 0;

        pipeline.Use(new SimpleCallbackMiddleware(before: () => callCount++));

        MessageDelegate handler = (msg, ct) => Task.FromResult(MessageResult.Ok("Done"));

        var executor = pipeline.Build(handler);

        for (int i = 0; i < 5; i++)
        {
            await executor(CreateTestMessage(), CancellationToken.None);
        }

        Assert.Equal(5, callCount);
    }

    [Test]
    public async Task DeepPipeline_ManyMiddleware_ExecutesAll()
    {
        var pipeline = new MiddlewarePipeline();
        var executionOrder = new List<int>();

        // Add 50 middleware components
        for (int i = 0; i < 50; i++)
        {
            pipeline.Use(new OrderTrackingMiddleware(i, executionOrder));
        }

        MessageDelegate handler = (msg, ct) =>
        {
            executionOrder.Add(-1); // Handler marker
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var executor = pipeline.Build(handler);
        await executor(CreateTestMessage(), CancellationToken.None);

        // 50 before + 1 handler + 50 after = 101
        Assert.Equal(101, executionOrder.Count);
        Assert.Equal(-1, executionOrder[50]); // Handler in the middle
    }

    [Test]
    public async Task Middleware_CanAccessAndModifyMessageMetadata()
    {
        var pipeline = new MiddlewarePipeline();

        pipeline.Use(new MetadataAddingMiddleware("trace-id", "12345"));

        MessageDelegate handler = (msg, ct) =>
        {
            var result = MessageResult.Ok("Done");
            if (msg.Metadata.TryGetValue("trace-id", out var traceId))
            {
                result.Data["trace-id"] = traceId;
            }
            return Task.FromResult(result);
        };

        var executor = pipeline.Build(handler);
        var result = await executor(CreateTestMessage(), CancellationToken.None);

        Assert.True(result.Data.ContainsKey("trace-id"));
        Assert.Equal("12345", result.Data["trace-id"]);
    }

    [Test]
    public async Task AfterMiddleware_ExceptionInAfterPhase_StillExecutes()
    {
        var pipeline = new MiddlewarePipeline();

        pipeline.Use(new SimpleCallbackMiddleware(after: () => { }));
        pipeline.Use(new AfterThrowingMiddleware());

        MessageDelegate handler = (msg, ct) => Task.FromResult(MessageResult.Ok("Done"));

        var executor = pipeline.Build(handler);

        var exceptionThrown = false;
        try
        {
            await executor(CreateTestMessage(), CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
        // Outer middleware's "after" might not execute if inner throws
        // This documents the behavior
    }

    [Test]
    public async Task Pipeline_UseConditional_OnlyExecutesWhenConditionMet()
    {
        var pipeline = new MiddlewarePipeline();
        var executed = false;

        // Add middleware that only executes for high priority
        pipeline.Use(new ConditionalActionMiddleware(
            msg => msg.Priority == MessagePriority.High,
            () => executed = true));

        MessageDelegate handler = (msg, ct) => Task.FromResult(MessageResult.Ok("Done"));
        var executor = pipeline.Build(handler);

        // Normal priority - should not execute conditional middleware action
        var normalMessage = CreateTestMessage();
        normalMessage.Priority = MessagePriority.Normal;
        await executor(normalMessage, CancellationToken.None);
        Assert.False(executed);

        // High priority - should execute
        var highMessage = CreateTestMessage();
        highMessage.Priority = MessagePriority.High;
        await executor(highMessage, CancellationToken.None);
        Assert.True(executed);
    }
}
