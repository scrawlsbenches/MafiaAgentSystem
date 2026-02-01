using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;

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

        pipeline.Use(new CallbackMiddleware(
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

        pipeline.Use(new CallbackMiddleware(before: () => reached.Add("M1")));
        pipeline.Use(new ShortCircuitMiddleware("Blocked"));
        pipeline.Use(new CallbackMiddleware(before: () => reached.Add("M3")));

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

    // Helper middleware classes for testing

    private class MessageCapturingMiddleware : MiddlewareBase
    {
        private readonly Action<AgentMessage> _capture;

        public MessageCapturingMiddleware(Action<AgentMessage> capture)
        {
            _capture = capture;
        }

        public override async Task<MessageResult> InvokeAsync(
            AgentMessage message, MessageDelegate next, CancellationToken ct)
        {
            _capture(message);
            return await next(message, ct);
        }
    }

    private class CallbackMiddleware : MiddlewareBase
    {
        private readonly Action? _before;
        private readonly Action? _after;

        public CallbackMiddleware(Action? before = null, Action? after = null)
        {
            _before = before;
            _after = after;
        }

        public override async Task<MessageResult> InvokeAsync(
            AgentMessage message, MessageDelegate next, CancellationToken ct)
        {
            _before?.Invoke();
            var result = await next(message, ct);
            _after?.Invoke();
            return result;
        }
    }

    private class OrderTrackingMiddleware : MiddlewareBase
    {
        private readonly int _id;
        private readonly List<int> _log;

        public OrderTrackingMiddleware(int id, List<int> log)
        {
            _id = id;
            _log = log;
        }

        public override async Task<MessageResult> InvokeAsync(
            AgentMessage message, MessageDelegate next, CancellationToken ct)
        {
            _log.Add(_id); // Before
            var result = await next(message, ct);
            _log.Add(-_id); // After (negative to distinguish)
            return result;
        }
    }

    private class ShortCircuitMiddleware : MiddlewareBase
    {
        private readonly string _error;

        public ShortCircuitMiddleware(string error)
        {
            _error = error;
        }

        public override Task<MessageResult> InvokeAsync(
            AgentMessage message, MessageDelegate next, CancellationToken ct)
        {
            return Task.FromResult(ShortCircuit(_error));
        }
    }

    private class ThrowingMiddleware : MiddlewareBase
    {
        public override Task<MessageResult> InvokeAsync(
            AgentMessage message, MessageDelegate next, CancellationToken ct)
        {
            throw new InvalidOperationException("Middleware exception");
        }
    }

    private class ResultModifyingMiddleware : MiddlewareBase
    {
        private readonly string _newResponse;

        public ResultModifyingMiddleware(string newResponse)
        {
            _newResponse = newResponse;
        }

        public override async Task<MessageResult> InvokeAsync(
            AgentMessage message, MessageDelegate next, CancellationToken ct)
        {
            var result = await next(message, ct);
            result.Response = _newResponse;
            return result;
        }
    }
}
