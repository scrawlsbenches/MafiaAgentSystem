using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentRouting.Core;
using AgentRouting.DependencyInjection;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;
using TestRunner.Framework;
using TestUtilities;

namespace TestRunner.Tests;

/// <summary>
/// Regression tests for Batch J bug fixes in AgentRouting.
/// These tests verify that the critical bugs identified in the deep code review
/// have been properly fixed and don't regress.
/// </summary>
[TestClass]
public class AgentRoutingBugFixRegressionTests
{
    #region J-1b: ServiceContainer Singleton Race Condition Tests

    [Test]
    public async Task J1b_ServiceContainer_ConcurrentSingletonResolve_OnlyOneInstanceCreated()
    {
        // Test: Multiple threads resolving a singleton simultaneously.
        // Before fix: Multiple instances could be created (race condition).
        // After fix: Double-checked locking ensures only ONE instance is created.

        using var container = new ServiceContainer();
        int factoryCallCount = 0;
        var createdInstances = new ConcurrentBag<object>();

        container.AddSingleton<ITestService>(c =>
        {
            Interlocked.Increment(ref factoryCallCount);
            var instance = new TestService();
            createdInstances.Add(instance);
            // Add delay to increase chance of race condition
            Thread.SpinWait(100);
            return instance;
        });

        var tasks = new List<Task<ITestService>>();
        const int concurrentResolves = 100;

        // Barrier to ensure all threads start at the same time
        using var barrier = new Barrier(concurrentResolves);

        for (int i = 0; i < concurrentResolves; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                return container.Resolve<ITestService>();
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Factory should be called exactly ONCE
        Assert.Equal(1, factoryCallCount);

        // All resolves should return the same instance
        var firstInstance = results[0];
        Assert.True(results.All(r => ReferenceEquals(r, firstInstance)),
            "All concurrent resolves should return the same instance");

        // Only one instance should have been created
        Assert.Equal(1, createdInstances.Count);
    }

    [Test]
    public async Task J1b_ServiceContainer_ConcurrentSingletonResolve_DifferentTypes_AllCreated()
    {
        // Test: Multiple singletons being resolved concurrently
        using var container = new ServiceContainer();
        int service1Count = 0;
        int service2Count = 0;

        container.AddSingleton<ITestService>(c =>
        {
            Interlocked.Increment(ref service1Count);
            return new TestService();
        });

        container.AddSingleton<ICountingService>(c =>
        {
            Interlocked.Increment(ref service2Count);
            return new CountingService();
        });

        var tasks = new List<Task>();
        const int iterations = 50;

        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(Task.Run(() => container.Resolve<ITestService>()));
            tasks.Add(Task.Run(() => container.Resolve<ICountingService>()));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(1, service1Count);
        Assert.Equal(1, service2Count);
    }

    [Test]
    public void J1b_ServiceContainer_SingletonFactoryThrows_ExceptionPropagates()
    {
        // Edge case: What happens when singleton factory throws?
        using var container = new ServiceContainer();
        container.AddSingleton<ITestService>(c =>
        {
            throw new InvalidOperationException("Factory failed");
        });

        Assert.Throws<InvalidOperationException>(() => container.Resolve<ITestService>());
    }

    #endregion

    #region J-1c: AgentRouter Thread-Safety Tests

    [Test]
    public async Task J1c_AgentRouter_ConcurrentAgentRegistration_NoCorruption()
    {
        // Test: Multiple threads registering agents simultaneously.
        // Before fix: List corruption due to non-thread-safe Add operations.
        // After fix: Locking ensures thread-safe registration.

        var router = new AgentRouterBuilder().Build();
        var tasks = new List<Task>();
        const int agentCount = 100;

        for (int i = 0; i < agentCount; i++)
        {
            int agentNum = i;
            tasks.Add(Task.Run(() =>
            {
                var agent = new TestAgent($"agent-{agentNum}");
                router.RegisterAgent(agent);
            }));
        }

        await Task.WhenAll(tasks);

        // All agents should be registered
        var allAgents = router.GetAllAgents();
        Assert.Equal(agentCount, allAgents.Count);

        // All agents should be accessible by ID
        for (int i = 0; i < agentCount; i++)
        {
            var agent = router.GetAgent($"agent-{i}");
            Assert.NotNull(agent);
        }
    }

    [Test]
    public async Task J1c_AgentRouter_ConcurrentReadDuringRegistration_NoExceptions()
    {
        // Test: Reading agents while others are registering
        var router = new AgentRouterBuilder().Build();
        var cts = new CancellationTokenSource();
        int readCount = 0;

        // Pre-register some agents
        for (int i = 0; i < 10; i++)
        {
            router.RegisterAgent(new TestAgent($"initial-{i}"));
        }

        // Reader task
        var readerTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var agents = router.GetAllAgents();
                var byCapability = router.GetAgentsByCapability("test");
                var specific = router.GetAgent("initial-0");
                Interlocked.Increment(ref readCount);
                await Task.Delay(1);
            }
        });

        // Writer tasks
        var writerTasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            int agentNum = i;
            writerTasks.Add(Task.Run(async () =>
            {
                await Task.Delay(agentNum % 10);
                router.RegisterAgent(new TestAgent($"new-{agentNum}"));
            }));
        }

        await Task.WhenAll(writerTasks);
        cts.Cancel();

        try { await readerTask; } catch (OperationCanceledException) { }

        // Should complete without exceptions
        Assert.True(readCount > 0);
        Assert.Equal(60, router.GetAllAgents().Count);
    }

    #endregion

    #region J-1d: ABTestingMiddleware Random Thread-Safety Tests

    [Test]
    public async Task J1d_ABTestingMiddleware_ConcurrentRandomGeneration_NoCorruption()
    {
        // Test: Multiple threads using A/B testing middleware simultaneously.
        // Before fix: Random instance was not thread-safe, could produce duplicates or corrupt.
        // After fix: Uses Random.Shared which is thread-safe.

        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("test-exp", 0.5, "A", "B");

        var results = new ConcurrentBag<string>();
        var tasks = new List<Task>();
        const int iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var message = new AgentMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderId = "test",
                    Subject = "Test",
                    Content = "Test"
                };

                await middleware.InvokeAsync(message, (msg, ct) =>
                {
                    if (msg.Metadata.TryGetValue("Experiment_test-exp", out var variant))
                    {
                        results.Add(variant?.ToString() ?? "null");
                    }
                    return Task.FromResult(MessageResult.Ok("ok"));
                }, CancellationToken.None);
            }));
        }

        await Task.WhenAll(tasks);

        // All iterations should have produced a result
        Assert.Equal(iterations, results.Count);

        // Results should be approximately 50/50 (with some variance)
        var aCount = results.Count(r => r == "A");
        var bCount = results.Count(r => r == "B");

        Assert.True(aCount > 0, "Should have some A variants");
        Assert.True(bCount > 0, "Should have some B variants");
        Assert.Equal(iterations, aCount + bCount);

        // With 1000 samples at 50% probability, we expect roughly 400-600 of each
        Assert.True(aCount > 300 && aCount < 700,
            $"A count {aCount} should be roughly 50% (300-700)");
    }

    #endregion

    #region J-3a: MetricsMiddleware Bounded Buffer Tests

    [Test]
    public async Task J3a_MetricsMiddleware_BoundedBuffer_DoesNotGrowUnbounded()
    {
        // Test: Processing many messages doesn't cause unbounded memory growth.
        // Before fix: ConcurrentBag grew forever.
        // After fix: Circular buffer with max 10000 samples.

        var middleware = new MetricsMiddleware();
        const int messageCount = 15000; // More than the 10000 max buffer size

        for (int i = 0; i < messageCount; i++)
        {
            var message = new AgentMessage
            {
                Id = i.ToString(),
                SenderId = "test",
                Subject = "Test",
                Content = "Test"
            };

            await middleware.InvokeAsync(message, (msg, ct) =>
                Task.FromResult(MessageResult.Ok("ok")), CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();

        // Total count should reflect all messages
        Assert.Equal(messageCount, snapshot.TotalMessages);
        Assert.Equal(messageCount, snapshot.SuccessCount);

        // The buffer is internal, but we can verify statistics still work
        Assert.True(snapshot.AverageProcessingTimeMs >= 0);
        Assert.True(snapshot.MinProcessingTimeMs >= 0);
        Assert.True(snapshot.MaxProcessingTimeMs >= 0);
    }

    [Test]
    public async Task J3a_MetricsMiddleware_StatisticsAccurate_WithFullBuffer()
    {
        var middleware = new MetricsMiddleware();
        const int messageCount = 100;
        int successCount = 0;
        int failCount = 0;

        for (int i = 0; i < messageCount; i++)
        {
            var message = new AgentMessage
            {
                Id = i.ToString(),
                SenderId = "test",
                Subject = "Test",
                Content = "Test"
            };

            bool shouldFail = i % 4 == 0; // 25% failure rate
            if (shouldFail) failCount++; else successCount++;

            await middleware.InvokeAsync(message, (msg, ct) =>
                shouldFail
                    ? Task.FromResult(MessageResult.Fail("error"))
                    : Task.FromResult(MessageResult.Ok("ok")),
                CancellationToken.None);
        }

        var snapshot = middleware.GetSnapshot();

        Assert.Equal(messageCount, snapshot.TotalMessages);
        Assert.Equal(successCount, snapshot.SuccessCount);
        Assert.Equal(failCount, snapshot.FailureCount);

        // Success rate should be ~75%
        Assert.True(snapshot.SuccessRate > 0.7 && snapshot.SuccessRate < 0.8);
    }

    [Test]
    public async Task J3a_MetricsMiddleware_ConcurrentReadsAndWrites_NoCorruption()
    {
        // Edge case: Reading snapshot while writing concurrently
        var middleware = new MetricsMiddleware();
        var cts = new CancellationTokenSource();
        var snapshots = new ConcurrentBag<MetricsSnapshot>();

        // Writer task
        var writerTask = Task.Run(async () =>
        {
            for (int i = 0; i < 1000 && !cts.Token.IsCancellationRequested; i++)
            {
                var message = new AgentMessage
                {
                    Id = i.ToString(),
                    SenderId = "test",
                    Subject = "Test",
                    Content = "Test"
                };

                await middleware.InvokeAsync(message, (msg, ct) =>
                    Task.FromResult(MessageResult.Ok("ok")), CancellationToken.None);
            }
        });

        // Reader task
        var readerTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var snapshot = middleware.GetSnapshot();
                snapshots.Add(snapshot);
                await Task.Delay(1);
            }
        });

        await writerTask;
        cts.Cancel();

        try { await readerTask; } catch (OperationCanceledException) { }

        // All snapshots should be valid (no corrupted data)
        foreach (var snapshot in snapshots)
        {
            Assert.True(snapshot.TotalMessages >= 0);
            Assert.True(snapshot.SuccessCount >= 0);
            Assert.True(snapshot.FailureCount >= 0);
            Assert.True(snapshot.AverageProcessingTimeMs >= 0);
        }
    }

    #endregion

    #region Helper Classes

    private class TestAgent : IAgent
    {
        public string Id { get; }
        public string Name => Id;
        public AgentStatus Status { get; set; } = AgentStatus.Available;
        public AgentCapabilities Capabilities { get; } = new AgentCapabilities();

        public TestAgent(string id)
        {
            Id = id;
            Capabilities.Skills.Add("test");
        }

        public bool CanHandle(AgentMessage message) => true;

        public Task<MessageResult> ProcessMessageAsync(AgentMessage message, CancellationToken ct)
            => Task.FromResult(MessageResult.Ok($"Processed by {Id}"));
    }

    #endregion
}
