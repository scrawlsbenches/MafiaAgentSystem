using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Agents;
using AgentRouting.DependencyInjection;
using TestUtilities;

namespace TestRunner.Tests;

/// <summary>
/// Regression tests for AgentRouting bug fixes identified in the 2026-02-08 code review.
/// </summary>
[TestClass]
public class AgentRoutingCodeReviewBugFixTests
{
    #region CR-04: OnUnroutableMessage event

    [Test]
    public async Task CR04_RouteMessage_NoAgentAvailable_FiresUnroutableEvent()
    {
        // Arrange: Router with no agents registered
        var logger = new TestLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        AgentMessage? capturedMessage = null;
        string? capturedReason = null;
        router.OnUnroutableMessage += (msg, reason) =>
        {
            capturedMessage = msg;
            capturedReason = reason;
        };

        var message = new AgentMessage
        {
            SenderId = "user-1",
            Subject = "Test message",
            Content = "No one can handle this",
            Category = "Unknown"
        };

        // Act
        var result = await router.RouteMessageAsync(message);

        // Assert
        Assert.False(result.Success, "Should fail when no agent available");
        Assert.NotNull(capturedMessage);
        Assert.Same(message, capturedMessage);
        Assert.NotNull(capturedReason);
        Assert.Contains("Test message", capturedReason!);
    }

    [Test]
    public async Task CR04_RouteMessage_AgentAvailable_DoesNotFireUnroutableEvent()
    {
        // Arrange: Router with a matching agent
        var logger = new TestLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();
        var agent = new CustomerServiceAgent("cs-001", "CS Agent", logger);
        router.RegisterAgent(agent);

        router.AddRoutingRule("ROUTE_CS", "Route CS", ctx => true, "cs-001", 1);

        bool eventFired = false;
        router.OnUnroutableMessage += (msg, reason) => eventFired = true;

        var message = new AgentMessage
        {
            SenderId = "user-1",
            Subject = "Help",
            Content = "Question",
            Category = "CustomerService"
        };

        // Act
        var result = await router.RouteMessageAsync(message);

        // Assert
        Assert.True(result.Success);
        Assert.False(eventFired, "OnUnroutableMessage should not fire when agent handles message");
    }

    [Test]
    public async Task CR04_RouteMessage_ThrowingEventHandler_StillReturnsFailResult()
    {
        // Arrange: Router with no agents, event handler that throws
        var logger = new TestLogger();
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        router.OnUnroutableMessage += (msg, reason) =>
        {
            throw new InvalidOperationException("Handler blew up!");
        };

        var message = new AgentMessage
        {
            SenderId = "user-1",
            Subject = "Test message",
            Content = "No one can handle this"
        };

        // Act: Should NOT throw — should return MessageResult.Fail
        var result = await router.RouteMessageAsync(message);

        // Assert: Routing returns a failure result despite the handler exception
        Assert.False(result.Success, "Should return failure, not throw");
        Assert.Contains("No agent available", result.Error!);
    }

    #endregion

    #region CR-15: ServiceContainer disposal exception aggregation

    [Test]
    public void CR15_ServiceContainer_ThrowingDisposable_AggregatesExceptions()
    {
        // Arrange: Register a service that throws on Dispose
        var container = new ServiceContainer();
        container.AddSingleton<IThrowingDisposable>(new ThrowingDisposableService("Test disposal error"));

        // Force the singleton to be resolved so it's in the cache
        _ = container.Resolve<IThrowingDisposable>();

        // Act & Assert: Dispose should throw AggregateException
        var ex = Assert.Throws<AggregateException>(() => container.Dispose());
        Assert.NotNull(ex);
        Assert.True(ex!.InnerExceptions.Count > 0, "Should have at least one inner exception");
    }

    [Test]
    public void CR15_ServiceContainer_MixedDisposables_AllGetDisposed()
    {
        // Arrange: One good service and one throwing service
        var goodService = new DisposableService();
        var throwingService = new ThrowingDisposableService("Boom");

        var container = new ServiceContainer();
        container.AddSingleton<IDisposableService>(goodService);
        container.AddSingleton<IThrowingDisposable>(throwingService);

        _ = container.Resolve<IDisposableService>();
        _ = container.Resolve<IThrowingDisposable>();

        // Act: Dispose should throw but still dispose all services
        try
        {
            container.Dispose();
        }
        catch (AggregateException)
        {
            // Expected
        }

        // Assert: Both services should have had Dispose called
        Assert.True(goodService.IsDisposed, "Good service should be disposed");
        Assert.True(throwingService.DisposeCalled, "Throwing service should have had Dispose called");
    }

    [Test]
    public void CR15_ServiceContainer_NoThrowingDisposables_DisposesCleanly()
    {
        // Arrange: Only well-behaved disposables
        var goodService = new DisposableService();
        var container = new ServiceContainer();
        container.AddSingleton<IDisposableService>(goodService);
        _ = container.Resolve<IDisposableService>();

        // Act & Assert: Should not throw
        container.Dispose();
        Assert.True(goodService.IsDisposed);
    }

    #endregion
}
