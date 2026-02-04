using AgentRouting.Core;
using AgentRouting.Middleware;
using TestRunner.Framework;
using TestUtilities;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for MiddlewareInfrastructure classes including MiddlewareBase,
/// MiddlewarePipelineExtensions, MiddlewareContext, and related classes.
/// </summary>
public class MiddlewareInfrastructureTests : AgentRoutingTestBase
{
    #region MiddlewareBase Tests

    [Test]
    public async Task MiddlewareBase_ContinueAsync_CallsNextMiddleware()
    {
        // Arrange
        var middleware = new TestContinueMiddleware();
        var nextCalled = false;
        MessageDelegate next = (msg, ct) =>
        {
            nextCalled = true;
            return Task.FromResult(MessageResult.Ok("next called"));
        };
        var message = new AgentMessage { Subject = "Test" };

        // Act
        var result = await middleware.InvokeAsync(message, next, CancellationToken.None);

        // Assert
        Assert.True(nextCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task MiddlewareBase_ShortCircuit_StopsExecution()
    {
        // Arrange
        var middleware = new TestShortCircuitMiddleware();
        var nextCalled = false;
        MessageDelegate next = (msg, ct) =>
        {
            nextCalled = true;
            return Task.FromResult(MessageResult.Ok());
        };
        var message = new AgentMessage { Subject = "Test" };

        // Act
        var result = await middleware.InvokeAsync(message, next, CancellationToken.None);

        // Assert
        Assert.False(nextCalled);
        Assert.False(result.Success);
        Assert.Equal("Short circuited", result.Error);
    }

    #endregion

    #region MiddlewareContext Tests

    [Test]
    public void MiddlewareContext_Get_ReturnsSetValue()
    {
        // Arrange
        var context = new MiddlewareContext();
        context.Set("key", "value");

        // Act
        var result = context.Get<string>("key");

        // Assert
        Assert.Equal("value", result);
    }

    [Test]
    public void MiddlewareContext_Get_ReturnsDefaultForMissingKey()
    {
        // Arrange
        var context = new MiddlewareContext();

        // Act
        var result = context.Get<string>("missing");

        // Assert
        Assert.Null(result);
    }

    [Test]
    public void MiddlewareContext_TryGet_ReturnsTrueForExistingKey()
    {
        // Arrange
        var context = new MiddlewareContext();
        context.Set("key", 42);

        // Act
        var found = context.TryGet<int>("key", out var value);

        // Assert
        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Test]
    public void MiddlewareContext_TryGet_ReturnsFalseForMissingKey()
    {
        // Arrange
        var context = new MiddlewareContext();

        // Act
        var found = context.TryGet<int>("missing", out var value);

        // Assert
        Assert.False(found);
        Assert.Equal(0, value);
    }

    [Test]
    public void MiddlewareContext_TryGet_ReturnsFalseForWrongType()
    {
        // Arrange
        var context = new MiddlewareContext();
        context.Set("key", "string value");

        // Act
        var found = context.TryGet<int>("key", out var value);

        // Assert
        Assert.False(found);
        Assert.Equal(0, value);
    }

    [Test]
    public void MiddlewareContext_Set_DoesNotStoreNull()
    {
        // Arrange
        var context = new MiddlewareContext();

        // Act
        context.Set<string?>("key", null);
        var result = context.Get<string>("key");

        // Assert
        Assert.Null(result);
    }

    [Test]
    public void MiddlewareContext_Set_OverwritesExistingValue()
    {
        // Arrange
        var context = new MiddlewareContext();
        context.Set("key", "first");

        // Act
        context.Set("key", "second");
        var result = context.Get<string>("key");

        // Assert
        Assert.Equal("second", result);
    }

    [Test]
    public void MiddlewareContext_IsThreadSafe()
    {
        // Arrange
        var context = new MiddlewareContext();
        var tasks = new List<Task>();

        // Act - Multiple threads set/get values concurrently
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                context.Set($"key{index}", index);
                var value = context.Get<int>($"key{index}");
            }));
        }

        // Should not throw
        Task.WaitAll(tasks.ToArray());
        Assert.True(true);
    }

    #endregion

    #region MessageContextExtensions Tests

    [Test]
    public void GetContext_CreatesNewContextIfNotExists()
    {
        // Arrange
        var message = new AgentMessage();

        // Act
        var context = message.GetContext();

        // Assert
        Assert.NotNull(context);
    }

    [Test]
    public void GetContext_ReturnsSameContextOnMultipleCalls()
    {
        // Arrange
        var message = new AgentMessage();

        // Act
        var context1 = message.GetContext();
        var context2 = message.GetContext();

        // Assert
        Assert.Same(context1, context2);
    }

    [Test]
    public void GetContext_PreservesSetValues()
    {
        // Arrange
        var message = new AgentMessage();
        var context = message.GetContext();
        context.Set("test", "value");

        // Act
        var laterContext = message.GetContext();
        var value = laterContext.Get<string>("test");

        // Assert
        Assert.Equal("value", value);
    }

    #endregion

    #region CallbackMiddleware Tests

    [Test]
    public async Task CallbackMiddleware_ExecutesBefore()
    {
        // Arrange
        var beforeCalled = false;
        var middleware = new CallbackMiddleware(
            before: msg => beforeCalled = true);
        var message = new AgentMessage();

        // Act
        await middleware.InvokeAsync(message,
            (msg, ct) => Task.FromResult(MessageResult.Ok()),
            CancellationToken.None);

        // Assert
        Assert.True(beforeCalled);
    }

    [Test]
    public async Task CallbackMiddleware_ExecutesAfter()
    {
        // Arrange
        MessageResult? capturedResult = null;
        var middleware = new CallbackMiddleware(
            after: (msg, result) => capturedResult = result);
        var message = new AgentMessage();

        // Act
        await middleware.InvokeAsync(message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("test")),
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedResult);
        Assert.True(capturedResult!.Success);
    }

    [Test]
    public async Task CallbackMiddleware_BeforeExecutesBeforeNext()
    {
        // Arrange
        var order = new List<string>();
        var middleware = new CallbackMiddleware(
            before: msg => order.Add("before"),
            after: (msg, result) => order.Add("after"));
        var message = new AgentMessage();

        // Act
        await middleware.InvokeAsync(message,
            (msg, ct) =>
            {
                order.Add("next");
                return Task.FromResult(MessageResult.Ok());
            },
            CancellationToken.None);

        // Assert
        Assert.Equal(3, order.Count);
        Assert.Equal("before", order[0]);
        Assert.Equal("next", order[1]);
        Assert.Equal("after", order[2]);
    }

    [Test]
    public async Task CallbackMiddleware_WorksWithoutCallbacks()
    {
        // Arrange
        var middleware = new CallbackMiddleware();
        var message = new AgentMessage();

        // Act
        var result = await middleware.InvokeAsync(message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("passed through")),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("passed through", result.Response);
    }

    #endregion

    #region ConditionalMiddleware Tests

    [Test]
    public async Task ConditionalMiddleware_ExecutesWhenConditionTrue()
    {
        // Arrange
        var innerCalled = false;
        var innerMiddleware = new TestCallbackMiddleware(() => innerCalled = true);
        var middleware = new ConditionalMiddleware(
            msg => true, // Always true
            innerMiddleware);
        var message = new AgentMessage();

        // Act
        await middleware.InvokeAsync(message,
            (msg, ct) => Task.FromResult(MessageResult.Ok()),
            CancellationToken.None);

        // Assert
        Assert.True(innerCalled);
    }

    [Test]
    public async Task ConditionalMiddleware_SkipsWhenConditionFalse()
    {
        // Arrange
        var innerCalled = false;
        var innerMiddleware = new TestCallbackMiddleware(() => innerCalled = true);
        var middleware = new ConditionalMiddleware(
            msg => false, // Always false
            innerMiddleware);
        var message = new AgentMessage();

        // Act
        await middleware.InvokeAsync(message,
            (msg, ct) => Task.FromResult(MessageResult.Ok()),
            CancellationToken.None);

        // Assert
        Assert.False(innerCalled);
    }

    [Test]
    public async Task ConditionalMiddleware_EvaluatesConditionWithMessage()
    {
        // Arrange
        var innerCalled = false;
        var innerMiddleware = new TestCallbackMiddleware(() => innerCalled = true);
        var middleware = new ConditionalMiddleware(
            msg => msg.Category == "Important",
            innerMiddleware);
        var message = new AgentMessage { Category = "Important" };

        // Act
        await middleware.InvokeAsync(message,
            (msg, ct) => Task.FromResult(MessageResult.Ok()),
            CancellationToken.None);

        // Assert
        Assert.True(innerCalled);
    }

    #endregion

    #region MiddlewarePipelineExtensions Tests

    [Test]
    public async Task UseWhen_AddsConditionalMiddleware()
    {
        // Arrange
        var pipeline = new MiddlewarePipeline();
        var called = false;
        var innerMiddleware = new TestCallbackMiddleware(() => called = true);
        pipeline.UseWhen(msg => msg.Category == "Test", innerMiddleware);
        var message = new AgentMessage { Category = "Test" };

        // Act
        var executor = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok()));
        await executor(message, CancellationToken.None);

        // Assert
        Assert.True(called);
    }

    [Test]
    public async Task UseCallback_AddsCallbackMiddleware()
    {
        // Arrange
        var pipeline = new MiddlewarePipeline();
        var beforeCalled = false;
        pipeline.UseCallback(before: msg => beforeCalled = true);
        var message = new AgentMessage();

        // Act
        var executor = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok()));
        await executor(message, CancellationToken.None);

        // Assert
        Assert.True(beforeCalled);
    }

    [Test]
    public async Task ExecuteAsync_BuildsAndExecutesPipeline()
    {
        // Arrange
        var pipeline = new MiddlewarePipeline();
        var middlewareCalled = false;
        pipeline.UseCallback(before: msg => middlewareCalled = true);
        var message = new AgentMessage();

        // Act
        var result = await pipeline.ExecuteAsync(message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("terminal")),
            CancellationToken.None);

        // Assert
        Assert.True(middlewareCalled);
        Assert.True(result.Success);
        Assert.Equal("terminal", result.Response);
    }

    #endregion

    #region DelegateMiddleware Tests (via pipeline.Use with delegate)

    [Test]
    public async Task DelegateMiddleware_ExecutesDelegate()
    {
        // Arrange
        var pipeline = new MiddlewarePipeline();
        var delegateCalled = false;
        pipeline.Use(next => async (message, ct) =>
        {
            delegateCalled = true;
            return await next(message, ct);
        });
        var message = new AgentMessage();

        // Act
        var executor = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok()));
        await executor(message, CancellationToken.None);

        // Assert
        Assert.True(delegateCalled);
    }

    [Test]
    public async Task DelegateMiddleware_CanModifyResult()
    {
        // Arrange
        var pipeline = new MiddlewarePipeline();
        pipeline.Use(next => async (message, ct) =>
        {
            var result = await next(message, ct);
            result.Data["modified"] = true;
            return result;
        });
        var message = new AgentMessage();

        // Act
        var executor = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok()));
        var result = await executor(message, CancellationToken.None);

        // Assert
        Assert.True(result.Data.ContainsKey("modified"));
        Assert.True((bool)result.Data["modified"]);
    }

    #endregion

    // Helper classes

    private class TestContinueMiddleware : MiddlewareBase
    {
        public override Task<MessageResult> InvokeAsync(
            AgentMessage message,
            MessageDelegate next,
            CancellationToken ct = default)
        {
            return ContinueAsync(message, next, ct);
        }
    }

    private class TestShortCircuitMiddleware : MiddlewareBase
    {
        public override Task<MessageResult> InvokeAsync(
            AgentMessage message,
            MessageDelegate next,
            CancellationToken ct = default)
        {
            return Task.FromResult(ShortCircuit("Short circuited"));
        }
    }

    private class TestCallbackMiddleware : MiddlewareBase
    {
        private readonly Action _callback;

        public TestCallbackMiddleware(Action callback)
        {
            _callback = callback;
        }

        public override async Task<MessageResult> InvokeAsync(
            AgentMessage message,
            MessageDelegate next,
            CancellationToken ct = default)
        {
            _callback();
            return await next(message, ct);
        }
    }
}
