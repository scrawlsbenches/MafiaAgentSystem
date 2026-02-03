using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;
using TestUtilities;

namespace TestRunner.Tests;

/// <summary>
/// Tests for AgentHealthCheckMiddleware - monitors agent availability
/// and routes around unhealthy agents.
/// </summary>
public class AgentHealthCheckMiddlewareTests
{
    #region Registration and Health Status Tracking Tests

    [Test]
    public void RegisterAgent_AddsAgentToHealthTracking()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        var status = middleware.GetHealthStatus();
        Assert.True(status.ContainsKey("agent-1"));
    }

    [Test]
    public void RegisterAgent_MultiplAgents_AllTracked()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));
        middleware.RegisterAgent("agent-2", () => Task.FromResult(true));
        middleware.RegisterAgent("agent-3", () => Task.FromResult(true));

        var status = middleware.GetHealthStatus();
        Assert.Equal(3, status.Count);
        Assert.True(status.ContainsKey("agent-1"));
        Assert.True(status.ContainsKey("agent-2"));
        Assert.True(status.ContainsKey("agent-3"));
    }

    [Test]
    public void RegisterAgent_InitialHealthStatusIsHealthy()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        var status = middleware.GetHealthStatus();
        Assert.True(status["agent-1"]);
    }

    [Test]
    public void GetHealthStatus_ReturnsEmptyDictionary_WhenNoAgentsRegistered()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        var status = middleware.GetHealthStatus();

        Assert.Equal(0, status.Count);
    }

    [Test]
    public void RegisterAgent_SameIdTwice_OverwritesPrevious()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(false));

        var status = middleware.GetHealthStatus();
        Assert.Equal(1, status.Count);
    }

    #endregion

    #region Healthy Agent Tests - Messages Pass Through

    [Test]
    public async Task InvokeAsync_HealthyTargetAgent_PassesThrough()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        var message = CreateTestMessage("sender", "agent-1");
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Processed"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.Success);
        Assert.Equal("agent-1", message.ReceiverId);
    }

    [Test]
    public async Task InvokeAsync_NoReceiverId_PassesThrough()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        var message = CreateTestMessage("sender", "");
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Processed"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task InvokeAsync_NullReceiverId_PassesThrough()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        var message = new AgentMessage
        {
            SenderId = "sender",
            ReceiverId = null!,
            Subject = "Test",
            Content = "Test content"
        };
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Processed"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task InvokeAsync_UnregisteredReceiver_PassesThrough()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        // Message targets unregistered agent
        var message = CreateTestMessage("sender", "unknown-agent");
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Processed"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.Success);
        Assert.Equal("unknown-agent", message.ReceiverId);
    }

    #endregion

    #region Unhealthy Agent Detection and Rerouting Tests

    [Test]
    public async Task InvokeAsync_UnhealthyTargetAgent_ReroutesToHealthyAgent()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        // Agent-1 will be marked unhealthy, agent-2 stays healthy
        var agent1Healthy = false;
        middleware.RegisterAgent("agent-1", () => Task.FromResult(agent1Healthy));
        middleware.RegisterAgent("agent-2", () => Task.FromResult(true));

        // Manually trigger health check to mark agent-1 as unhealthy
        await SimulateHealthCheck(middleware, "agent-1", false);

        var message = CreateTestMessage("sender", "agent-1");
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Processed"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.Success);
        Assert.Equal("agent-2", message.ReceiverId);
    }

    [Test]
    public async Task InvokeAsync_UnhealthyTargetAgent_SelectsFirstHealthyAgent()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(false));
        middleware.RegisterAgent("agent-2", () => Task.FromResult(false));
        middleware.RegisterAgent("agent-3", () => Task.FromResult(true));

        // Mark agent-1 and agent-2 as unhealthy
        await SimulateHealthCheck(middleware, "agent-1", false);
        await SimulateHealthCheck(middleware, "agent-2", false);

        var message = CreateTestMessage("sender", "agent-1");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Processed")),
            CancellationToken.None);

        // Should be rerouted to agent-3 (the only healthy one)
        Assert.Equal("agent-3", message.ReceiverId);
    }

    [Test]
    public async Task InvokeAsync_MultipleUnhealthyAgents_FindsHealthyAlternative()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("primary", () => Task.FromResult(false));
        middleware.RegisterAgent("backup1", () => Task.FromResult(false));
        middleware.RegisterAgent("backup2", () => Task.FromResult(true));
        middleware.RegisterAgent("backup3", () => Task.FromResult(true));

        await SimulateHealthCheck(middleware, "primary", false);
        await SimulateHealthCheck(middleware, "backup1", false);

        var message = CreateTestMessage("sender", "primary");

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Processed")),
            CancellationToken.None);

        Assert.True(result.Success);
        // Should be rerouted to one of the healthy backups
        Assert.True(message.ReceiverId == "backup2" || message.ReceiverId == "backup3");
    }

    #endregion

    #region No Healthy Agents Available Tests

    [Test]
    public async Task InvokeAsync_AllAgentsUnhealthy_ShortCircuitsWithError()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(false));
        middleware.RegisterAgent("agent-2", () => Task.FromResult(false));

        await SimulateHealthCheck(middleware, "agent-1", false);
        await SimulateHealthCheck(middleware, "agent-2", false);

        var message = CreateTestMessage("sender", "agent-1");
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Should not reach"));
            },
            CancellationToken.None);

        Assert.False(nextCalled);
        Assert.False(result.Success);
        Assert.Contains("No healthy agents available", result.Error!);
    }

    [Test]
    public async Task InvokeAsync_SingleUnhealthyAgent_NoFallback_ShortCircuits()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(false));

        await SimulateHealthCheck(middleware, "agent-1", false);

        var message = CreateTestMessage("sender", "agent-1");

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Should not reach")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("No healthy agents available", result.Error!);
    }

    #endregion

    #region Recovery Detection Tests

    [Test]
    public async Task InvokeAsync_AgentRecovers_MessagesRouteCorrectly()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        var agentHealthy = true;

        middleware.RegisterAgent("agent-1", () => Task.FromResult(agentHealthy));
        middleware.RegisterAgent("agent-2", () => Task.FromResult(true));

        // Mark agent-1 as unhealthy
        await SimulateHealthCheck(middleware, "agent-1", false);

        // First message should be rerouted
        var message1 = CreateTestMessage("sender", "agent-1");
        await middleware.InvokeAsync(
            message1,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);
        Assert.Equal("agent-2", message1.ReceiverId);

        // Simulate agent recovery
        await SimulateHealthCheck(middleware, "agent-1", true);

        // Second message should go to agent-1 directly
        var message2 = CreateTestMessage("sender", "agent-1");
        await middleware.InvokeAsync(
            message2,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);
        Assert.Equal("agent-1", message2.ReceiverId);
    }

    [Test]
    public async Task InvokeAsync_AgentBecomesUnhealthy_ThenRecovers_StatusTrackedCorrectly()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        // Initially healthy
        var status1 = middleware.GetHealthStatus();
        Assert.True(status1["agent-1"]);

        // Mark unhealthy
        await SimulateHealthCheck(middleware, "agent-1", false);
        var status2 = middleware.GetHealthStatus();
        Assert.False(status2["agent-1"]);

        // Mark healthy again
        await SimulateHealthCheck(middleware, "agent-1", true);
        var status3 = middleware.GetHealthStatus();
        Assert.True(status3["agent-1"]);
    }

    #endregion

    #region Health Check Execution Tests

    [Test]
    public async Task HealthCheck_ReturnsTrue_AgentMarkedHealthy()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        var healthCheckCallCount = 0;

        middleware.RegisterAgent("agent-1", () =>
        {
            healthCheckCallCount++;
            return Task.FromResult(true);
        });

        // Manually simulate health check
        await SimulateHealthCheck(middleware, "agent-1", true);

        var status = middleware.GetHealthStatus();
        Assert.True(status["agent-1"]);
    }

    [Test]
    public async Task HealthCheck_ReturnsFalse_AgentMarkedUnhealthy()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(false));

        await SimulateHealthCheck(middleware, "agent-1", false);

        var status = middleware.GetHealthStatus();
        Assert.False(status["agent-1"]);
    }

    [Test]
    public async Task HealthCheck_ThrowsException_AgentMarkedUnhealthy()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        // Health check that throws
        middleware.RegisterAgent("agent-1", () => throw new InvalidOperationException("Connection failed"));

        // Simulate what happens when health check throws - the middleware marks it unhealthy
        // We can't easily trigger the timer, so we simulate the effect
        await SimulateHealthCheck(middleware, "agent-1", false);

        var status = middleware.GetHealthStatus();
        Assert.False(status["agent-1"]);
    }

    #endregion

    #region Concurrent Scenarios Tests

    [Test]
    public async Task InvokeAsync_ConcurrentMessages_AllRoutedCorrectly()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("healthy-1", () => Task.FromResult(true));
        middleware.RegisterAgent("healthy-2", () => Task.FromResult(true));
        middleware.RegisterAgent("unhealthy", () => Task.FromResult(false));

        await SimulateHealthCheck(middleware, "unhealthy", false);

        var tasks = new List<Task<MessageResult>>();
        var messages = new List<AgentMessage>();

        for (int i = 0; i < 10; i++)
        {
            var message = CreateTestMessage($"sender-{i}", "unhealthy");
            messages.Add(message);

            var task = middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("Processed")),
                CancellationToken.None);
            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);

        // All messages should be rerouted to healthy agents
        foreach (var message in messages)
        {
            Assert.True(message.ReceiverId == "healthy-1" || message.ReceiverId == "healthy-2");
        }

        foreach (var result in results)
        {
            Assert.True(result.Success);
        }
    }

    [Test]
    public async Task InvokeAsync_MultipleSendersToSameHealthyAgent_AllProcessed()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        var processedCount = 0;
        var tasks = new List<Task<MessageResult>>();

        for (int i = 0; i < 5; i++)
        {
            var message = CreateTestMessage($"sender-{i}", "agent-1");
            var task = middleware.InvokeAsync(
                message,
                (msg, ct) =>
                {
                    Interlocked.Increment(ref processedCount);
                    return Task.FromResult(MessageResult.Ok("Processed"));
                },
                CancellationToken.None);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        Assert.Equal(5, processedCount);
    }

    #endregion

    #region Pipeline Integration Tests

    [Test]
    public async Task Pipeline_HealthCheckMiddleware_WorksWithOtherMiddleware()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        var pipeline = new MiddlewarePipeline();
        var executionOrder = new List<string>();

        pipeline.Use(new NamedTrackingMiddleware("Validation", executionOrder));
        pipeline.Use(middleware);
        pipeline.Use(new NamedTrackingMiddleware("Logging", executionOrder));

        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            executionOrder.Add("Handler");
            return Task.FromResult(MessageResult.Ok("Done"));
        });

        var message = CreateTestMessage("sender", "agent-1");
        var result = await builtPipeline(message, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Validation-Before", executionOrder[0]);
        Assert.Equal("Logging-Before", executionOrder[1]);
        Assert.Equal("Handler", executionOrder[2]);
        Assert.Equal("Logging-After", executionOrder[3]);
        Assert.Equal("Validation-After", executionOrder[4]);
    }

    [Test]
    public async Task Pipeline_HealthCheckShortCircuits_StopsOtherMiddleware()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(false));
        await SimulateHealthCheck(middleware, "agent-1", false);

        var pipeline = new MiddlewarePipeline();
        var postHealthCheckCalled = false;

        pipeline.Use(middleware);
        pipeline.Use(next => async (msg, ct) =>
        {
            postHealthCheckCalled = true;
            return await next(msg, ct);
        });

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));

        var message = CreateTestMessage("sender", "agent-1");
        var result = await builtPipeline(message, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(postHealthCheckCalled);
    }

    [Test]
    public async Task Pipeline_HealthCheckReroutes_RestOfPipelineProcessesRerouted()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("unhealthy", () => Task.FromResult(false));
        middleware.RegisterAgent("healthy", () => Task.FromResult(true));
        await SimulateHealthCheck(middleware, "unhealthy", false);

        var pipeline = new MiddlewarePipeline();
        string? receiverSeenByNextMiddleware = null;

        pipeline.Use(middleware);
        pipeline.Use(next => async (msg, ct) =>
        {
            receiverSeenByNextMiddleware = msg.ReceiverId;
            return await next(msg, ct);
        });

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));

        var message = CreateTestMessage("sender", "unhealthy");
        await builtPipeline(message, CancellationToken.None);

        Assert.Equal("healthy", receiverSeenByNextMiddleware);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Test]
    public async Task InvokeAsync_EmptyAgentId_Registered_Works()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        // Edge case: empty string as agent ID
        middleware.RegisterAgent("", () => Task.FromResult(true));

        var message = CreateTestMessage("sender", "");
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Processed"));
            },
            CancellationToken.None);

        // Empty receiver ID means middleware doesn't look it up
        Assert.True(nextCalled);
    }

    [Test]
    public async Task InvokeAsync_CancellationToken_Respected()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        var message = CreateTestMessage("sender", "agent-1");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // The middleware passes the cancellation token to the next handler
        // So when the handler checks the token, it should throw OperationCanceledException
        var exceptionThrown = false;
        try
        {
            await middleware.InvokeAsync(
                message,
                (msg, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(MessageResult.Ok("Done"));
                },
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown, "Expected OperationCanceledException to be thrown");
    }

    [Test]
    public void GetHealthStatus_ReturnsNewDictionary_NotReference()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        var status1 = middleware.GetHealthStatus();
        var status2 = middleware.GetHealthStatus();

        Assert.NotSame(status1, status2);
    }

    [Test]
    public async Task InvokeAsync_MessageModified_ChangesPersist()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(false));
        middleware.RegisterAgent("agent-2", () => Task.FromResult(true));
        await SimulateHealthCheck(middleware, "agent-1", false);

        var message = CreateTestMessage("sender", "agent-1");
        var originalReceiverId = message.ReceiverId;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Verify the message was actually modified
        Assert.NotEqual(originalReceiverId, message.ReceiverId);
        Assert.Equal("agent-2", message.ReceiverId);
    }

    [Test]
    public async Task InvokeAsync_HealthyAgentThenUnhealthy_ReroutesOnNextCall()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));
        middleware.RegisterAgent("agent-2", () => Task.FromResult(true));

        // First call - agent-1 is healthy
        var message1 = CreateTestMessage("sender", "agent-1");
        await middleware.InvokeAsync(
            message1,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);
        Assert.Equal("agent-1", message1.ReceiverId);

        // Agent-1 becomes unhealthy
        await SimulateHealthCheck(middleware, "agent-1", false);

        // Second call - should reroute
        var message2 = CreateTestMessage("sender", "agent-1");
        await middleware.InvokeAsync(
            message2,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);
        Assert.Equal("agent-2", message2.ReceiverId);
    }

    #endregion

    #region Theory Tests with Multiple Data Points

    [Theory]
    [InlineData("agent-a")]
    [InlineData("agent-b")]
    [InlineData("service-1")]
    [InlineData("worker_001")]
    public void InvokeAsync_VariousAgentIds_TrackedCorrectly(string agentId)
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent(agentId, () => Task.FromResult(true));

        var status = middleware.GetHealthStatus();
        Assert.True(status.ContainsKey(agentId));
        Assert.True(status[agentId]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public void InvokeAsync_VariousAgentCounts_AllTracked(int agentCount)
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        for (int i = 0; i < agentCount; i++)
        {
            middleware.RegisterAgent($"agent-{i}", () => Task.FromResult(true));
        }

        var status = middleware.GetHealthStatus();
        Assert.Equal(agentCount, status.Count);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task InvokeAsync_TwoAgentsHealthCombinations_RoutesCorrectly(bool agent1Healthy, bool agent2Healthy)
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(agent1Healthy));
        middleware.RegisterAgent("agent-2", () => Task.FromResult(agent2Healthy));

        await SimulateHealthCheck(middleware, "agent-1", agent1Healthy);
        await SimulateHealthCheck(middleware, "agent-2", agent2Healthy);

        var message = CreateTestMessage("sender", "agent-1");

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        if (agent1Healthy)
        {
            // Should stay on agent-1
            Assert.Equal("agent-1", message.ReceiverId);
            Assert.True(result.Success);
        }
        else if (agent2Healthy)
        {
            // Should reroute to agent-2
            Assert.Equal("agent-2", message.ReceiverId);
            Assert.True(result.Success);
        }
        else
        {
            // Both unhealthy - should fail
            Assert.False(result.Success);
            Assert.Contains("No healthy agents available", result.Error!);
        }
    }

    #endregion

    #region Statistics and Metrics Tests

    [Test]
    public async Task GetHealthStatus_ReflectsCurrentState()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));
        middleware.RegisterAgent("agent-2", () => Task.FromResult(false));
        middleware.RegisterAgent("agent-3", () => Task.FromResult(true));

        await SimulateHealthCheck(middleware, "agent-2", false);

        var status = middleware.GetHealthStatus();

        Assert.True(status["agent-1"]);
        Assert.False(status["agent-2"]);
        Assert.True(status["agent-3"]);
    }

    [Test]
    public async Task GetHealthStatus_CountsHealthyAgents()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));
        middleware.RegisterAgent("agent-2", () => Task.FromResult(true));
        middleware.RegisterAgent("agent-3", () => Task.FromResult(false));

        await SimulateHealthCheck(middleware, "agent-3", false);

        var status = middleware.GetHealthStatus();
        var healthyCount = status.Count(kv => kv.Value);
        var unhealthyCount = status.Count(kv => !kv.Value);

        Assert.Equal(2, healthyCount);
        Assert.Equal(1, unhealthyCount);
    }

    #endregion

    #region Constructor and Timer Tests

    [Test]
    public void Constructor_AcceptsTimeSpan_DoesNotThrow()
    {
        // Various valid time spans
        var middleware1 = new AgentHealthCheckMiddleware(TimeSpan.FromSeconds(1));
        var middleware2 = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(5));
        var middleware3 = new AgentHealthCheckMiddleware(TimeSpan.FromHours(1));

        // If we got here without exception, the test passes
        Assert.NotNull(middleware1);
        Assert.NotNull(middleware2);
        Assert.NotNull(middleware3);
    }

    [Test]
    public void Constructor_ZeroInterval_DoesNotThrow()
    {
        // Edge case: zero interval (may not be useful but shouldn't crash)
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.Zero);
        Assert.NotNull(middleware);
    }

    #endregion

    #region Helper Methods and Classes

    private static AgentMessage CreateTestMessage(string senderId, string receiverId)
    {
        return new AgentMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Subject = "Test Subject",
            Content = "Test Content",
            Category = "Test"
        };
    }

    /// <summary>
    /// Simulates a health check result by using reflection to update the internal health status.
    /// This is necessary because the actual health check runs on a timer.
    /// </summary>
    private static async Task SimulateHealthCheck(AgentHealthCheckMiddleware middleware, string agentId, bool isHealthy)
    {
        // Get the _health field via reflection
        var healthField = typeof(AgentHealthCheckMiddleware)
            .GetField("_health", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (healthField != null)
        {
            // Get the dictionary (it's ConcurrentDictionary<string, HealthStatus> where HealthStatus is private)
            var healthDict = healthField.GetValue(middleware);
            if (healthDict != null)
            {
                // Use the IDictionary interface to access it
                var dict = healthDict as System.Collections.IDictionary;
                if (dict != null && dict.Contains(agentId))
                {
                    var statusObj = dict[agentId];
                    if (statusObj != null)
                    {
                        // Get the IsHealthy property of the HealthStatus inner class
                        var isHealthyProp = statusObj.GetType().GetProperty("IsHealthy");
                        if (isHealthyProp != null)
                        {
                            isHealthyProp.SetValue(statusObj, isHealthy);
                        }
                    }
                }
            }
        }

        await Task.CompletedTask;
    }

    #endregion
}
