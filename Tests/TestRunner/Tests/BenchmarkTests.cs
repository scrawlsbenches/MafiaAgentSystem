using TestRunner.Framework;
using RulesEngine.Core;
using AgentRouting.Core;
using AgentRouting.Middleware;
using System.Diagnostics;

namespace TestRunner.Tests;

/// <summary>
/// Performance benchmarks for the rules engine and agent routing.
/// These tests establish baseline performance metrics.
/// </summary>
public class BenchmarkTests
{
    private class BenchmarkFact
    {
        public int Value { get; set; }
        public string Category { get; set; } = "";
        public decimal Amount { get; set; }
    }

    // ==================== Rules Engine Benchmarks ====================

    [Test]
    public void Benchmark_RuleEvaluation_SingleRule()
    {
        var engine = new RulesEngineCore<BenchmarkFact>();
        engine.AddRule("R1", "Simple Rule", f => f.Value > 50, f => { });

        var fact = new BenchmarkFact { Value = 100 };

        // Warmup
        for (int i = 0; i < 100; i++)
            engine.Execute(fact);

        // Benchmark
        var iterations = 10000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            engine.Execute(fact);
        }

        sw.Stop();
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;

        Console.WriteLine($"  Single rule evaluation: {avgMicroseconds:F2}µs per execution");
        Console.WriteLine($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} ops/sec");

        // Assert reasonable performance (< 100µs per execution)
        Assert.True(avgMicroseconds < 100, $"Single rule too slow: {avgMicroseconds:F2}µs");
    }

    [Test]
    public void Benchmark_RuleEvaluation_TenRules()
    {
        var engine = new RulesEngineCore<BenchmarkFact>();

        for (int i = 0; i < 10; i++)
        {
            var threshold = i * 10;
            engine.AddRule($"R{i}", $"Rule {i}", f => f.Value > threshold, f => { });
        }

        var fact = new BenchmarkFact { Value = 100 };

        // Warmup
        for (int i = 0; i < 100; i++)
            engine.Execute(fact);

        // Benchmark
        var iterations = 10000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            engine.Execute(fact);
        }

        sw.Stop();
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;

        Console.WriteLine($"  10 rules evaluation: {avgMicroseconds:F2}µs per execution");
        Console.WriteLine($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} ops/sec");

        // Assert reasonable performance (< 500µs per execution for 10 rules)
        Assert.True(avgMicroseconds < 500, $"10 rules too slow: {avgMicroseconds:F2}µs");
    }

    [Test]
    public void Benchmark_RuleEvaluation_HundredRules()
    {
        var engine = new RulesEngineCore<BenchmarkFact>();

        for (int i = 0; i < 100; i++)
        {
            var threshold = i;
            engine.AddRule($"R{i}", $"Rule {i}", f => f.Value > threshold, f => { });
        }

        var fact = new BenchmarkFact { Value = 100 };

        // Warmup
        for (int i = 0; i < 50; i++)
            engine.Execute(fact);

        // Benchmark
        var iterations = 1000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            engine.Execute(fact);
        }

        sw.Stop();
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;

        Console.WriteLine($"  100 rules evaluation: {avgMicroseconds:F2}µs per execution");
        Console.WriteLine($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} ops/sec");

        // Assert reasonable performance (< 5ms per execution for 100 rules)
        Assert.True(avgMicroseconds < 5000, $"100 rules too slow: {avgMicroseconds:F2}µs");
    }

    [Test]
    public void Benchmark_RuleRegistration()
    {
        // Benchmark rule registration speed
        var iterations = 1000;

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var engine = new RulesEngineCore<BenchmarkFact>();
            for (int j = 0; j < 10; j++)
            {
                engine.AddRule($"R{j}", $"Rule {j}", f => f.Value > j, f => { });
            }
        }

        sw.Stop();
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;

        Console.WriteLine($"  Engine creation + 10 rules: {avgMicroseconds:F2}µs");
        Console.WriteLine($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} engines/sec");

        // Assert reasonable performance
        Assert.True(avgMicroseconds < 1000, $"Registration too slow: {avgMicroseconds:F2}µs");
    }

    // ==================== Middleware Pipeline Benchmarks ====================

    [Test]
    public async Task Benchmark_MiddlewarePipeline_Empty()
    {
        var pipeline = new MiddlewarePipeline();
        MessageDelegate handler = (msg, ct) => Task.FromResult(MessageResult.Ok("Done"));

        var executor = pipeline.Build(handler);
        var message = CreateTestMessage();

        // Warmup
        for (int i = 0; i < 100; i++)
            await executor(message, CancellationToken.None);

        // Benchmark
        var iterations = 10000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            await executor(message, CancellationToken.None);
        }

        sw.Stop();
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;

        Console.WriteLine($"  Empty pipeline: {avgMicroseconds:F2}µs per execution");
        Console.WriteLine($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} ops/sec");

        Assert.True(avgMicroseconds < 50, $"Empty pipeline too slow: {avgMicroseconds:F2}µs");
    }

    [Test]
    public async Task Benchmark_MiddlewarePipeline_WithMiddleware()
    {
        var pipeline = new MiddlewarePipeline();
        pipeline.Use(new ValidationMiddleware());
        pipeline.Use(new TimingMiddleware());
        pipeline.Use(new MetricsMiddleware());

        MessageDelegate handler = (msg, ct) => Task.FromResult(MessageResult.Ok("Done"));

        var executor = pipeline.Build(handler);
        var message = CreateTestMessage();

        // Warmup
        for (int i = 0; i < 100; i++)
            await executor(message, CancellationToken.None);

        // Benchmark
        var iterations = 5000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            await executor(message, CancellationToken.None);
        }

        sw.Stop();
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;

        Console.WriteLine($"  3-middleware pipeline: {avgMicroseconds:F2}µs per execution");
        Console.WriteLine($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} ops/sec");

        Assert.True(avgMicroseconds < 500, $"3-middleware pipeline too slow: {avgMicroseconds:F2}µs");
    }

    [Test]
    public async Task Benchmark_RateLimitMiddleware()
    {
        var middleware = new RateLimitMiddleware(maxRequests: 100000, window: TimeSpan.FromMinutes(1));
        MessageDelegate handler = (msg, ct) => Task.FromResult(MessageResult.Ok("Done"));

        var message = CreateTestMessage();

        // Warmup
        for (int i = 0; i < 100; i++)
            await middleware.InvokeAsync(message, handler, CancellationToken.None);

        // Benchmark
        var iterations = 10000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            await middleware.InvokeAsync(message, handler, CancellationToken.None);
        }

        sw.Stop();
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;

        Console.WriteLine($"  Rate limit check: {avgMicroseconds:F2}µs per execution");
        Console.WriteLine($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} ops/sec");

        Assert.True(avgMicroseconds < 100, $"Rate limit too slow: {avgMicroseconds:F2}µs");
    }

    // ==================== Memory Benchmarks ====================

    [Test]
    public void Benchmark_MemoryAllocation_RuleExecution()
    {
        var engine = new RulesEngineCore<BenchmarkFact>();
        engine.AddRule("R1", "Simple Rule", f => f.Value > 50, f => { });

        var fact = new BenchmarkFact { Value = 100 };

        // Force GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var beforeMemory = GC.GetTotalMemory(true);

        // Execute many times
        for (int i = 0; i < 10000; i++)
        {
            engine.Execute(fact);
        }

        var afterMemory = GC.GetTotalMemory(false);
        var memoryPerExecution = (afterMemory - beforeMemory) / 10000.0;

        Console.WriteLine($"  Memory per execution: ~{memoryPerExecution:F0} bytes");

        // Assert reasonable memory usage (< 1KB per execution average)
        Assert.True(memoryPerExecution < 1024, $"Memory usage too high: {memoryPerExecution:F0} bytes/exec");
    }

    // ==================== Concurrent Access Benchmarks ====================

    [Test]
    public async Task Benchmark_ConcurrentRuleExecution()
    {
        var engine = new RulesEngineCore<BenchmarkFact>();

        for (int i = 0; i < 10; i++)
        {
            var threshold = i * 10;
            engine.AddRule($"R{i}", $"Rule {i}", f => f.Value > threshold, f => { });
        }

        var tasks = new List<Task>();
        var iterations = 1000;
        var threadCount = 10;

        var sw = Stopwatch.StartNew();

        for (int t = 0; t < threadCount; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                var fact = new BenchmarkFact { Value = 100 };
                for (int i = 0; i < iterations; i++)
                {
                    engine.Execute(fact);
                }
            }));
        }

        await Task.WhenAll(tasks);

        sw.Stop();
        var totalOps = iterations * threadCount;
        var opsPerSecond = totalOps / sw.Elapsed.TotalSeconds;

        Console.WriteLine($"  Concurrent execution ({threadCount} threads): {opsPerSecond:F0} ops/sec");
        Console.WriteLine($"  Total: {totalOps} operations in {sw.Elapsed.TotalMilliseconds:F0}ms");

        // Assert concurrent throughput is reasonable
        Assert.True(opsPerSecond > 10000, $"Concurrent throughput too low: {opsPerSecond:F0} ops/sec");
    }

    private AgentMessage CreateTestMessage()
    {
        return new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = "benchmark-sender",
            ReceiverId = "benchmark-receiver",
            Subject = "Benchmark Test",
            Content = "This is a benchmark test message",
            Category = "Benchmark",
            Priority = MessagePriority.Normal
        };
    }
}
