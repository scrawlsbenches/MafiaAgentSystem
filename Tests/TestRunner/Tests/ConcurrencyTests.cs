using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RulesEngine.Core;
using TestRunner.Framework;

namespace TestRunner.Tests;

[TestClass]
public class ConcurrencyTests
{
    [Test]
    public async Task ConcurrentRuleRegistration_NoExceptions()
    {
        // Test: Multiple threads registering rules simultaneously
        using var engine = new RulesEngineCore<int>();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            int ruleNum = i;
            tasks.Add(Task.Run(() =>
            {
                engine.AddRule($"rule-{ruleNum}", $"Rule {ruleNum}",
                    x => x > ruleNum, x => { }, ruleNum);
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Equal(100, engine.GetRules().Count());
    }

    [Test]
    public async Task ConcurrentRuleExecution_NoExceptions()
    {
        // Test: Multiple threads executing rules on same engine
        using var engine = new RulesEngineCore<int>();
        engine.AddRule("test", "Test Rule", x => x > 5, x => { });

        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            int value = i;
            tasks.Add(Task.Run(() => engine.Execute(value)));
        }

        await Task.WhenAll(tasks);
        // If we get here without exceptions, test passes
    }

    [Test]
    public async Task ConcurrentRegistrationDuringExecution_NoExceptions()
    {
        // Test: Registering rules while other threads are executing
        using var engine = new RulesEngineCore<int>(new RulesEngineOptions { AllowDuplicateRuleIds = true });
        engine.AddRule("initial", "Initial Rule", x => x > 0, x => { });

        var cts = new CancellationTokenSource();
        var executionTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                engine.Execute(42);
                await Task.Yield();
            }
        });

        // Register rules while execution is happening
        for (int i = 0; i < 50; i++)
        {
            engine.AddRule($"rule-{i}", $"Rule {i}", x => x > i, x => { });
            await Task.Delay(1); // Small delay to interleave
        }

        cts.Cancel();
        await executionTask;
    }

    [Test]
    public async Task StressTest_ManyThreads_NoDataCorruption()
    {
        // Stress test with many concurrent operations
        using var engine = new RulesEngineCore<int>(new RulesEngineOptions { AllowDuplicateRuleIds = true });
        var counter = 0;
        var lockObj = new object();

        engine.AddRule("counter", "Counter Rule",
            x => x > 0,
            x => { lock(lockObj) { counter++; } });

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    engine.Execute(10);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        // Should have executed 50 * 100 = 5000 times
        Assert.Equal(5000, counter);
    }
}
