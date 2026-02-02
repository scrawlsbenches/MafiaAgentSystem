using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for ABTestingMiddleware A/B experiment variant assignment functionality
/// </summary>
public class ABTestingMiddlewareTests
{
    #region No Experiments Tests

    [Test]
    public async Task NoExperiments_MessagePassesThrough()
    {
        var middleware = new ABTestingMiddleware();
        var message = CreateTestMessage();
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
    public async Task NoExperiments_NoExperimentMetadataAdded()
    {
        var middleware = new ABTestingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // No experiment metadata should be added
        var experimentKeys = message.Metadata.Keys.Where(k => k.StartsWith("Experiment_")).ToList();
        Assert.Empty(experimentKeys);
    }

    #endregion

    #region Single Experiment Tests

    [Test]
    public async Task SingleExperiment_AssignsVariant()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("test-experiment", 0.5, "VariantA", "VariantB");

        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Should have experiment metadata
        Assert.True(message.Metadata.ContainsKey("Experiment_test-experiment"));
        var variant = message.Metadata["Experiment_test-experiment"]?.ToString();
        Assert.NotNull(variant);
        Assert.True(variant == "VariantA" || variant == "VariantB", $"Unexpected variant: {variant}");
    }

    [Test]
    public async Task SingleExperiment_ProbabilityZero_AlwaysAssignsVariantB()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("zero-prob", 0.0, "VariantA", "VariantB");

        // Run multiple times to verify consistent behavior
        for (int i = 0; i < 20; i++)
        {
            var message = CreateTestMessage($"sender-{i}");

            await middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

            var variant = message.Metadata["Experiment_zero-prob"]?.ToString();
            Assert.Equal("VariantB", variant, $"Iteration {i}: Expected VariantB with 0.0 probability");
        }
    }

    [Test]
    public async Task SingleExperiment_ProbabilityOne_AlwaysAssignsVariantA()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("full-prob", 1.0, "VariantA", "VariantB");

        // Run multiple times to verify consistent behavior
        for (int i = 0; i < 20; i++)
        {
            var message = CreateTestMessage($"sender-{i}");

            await middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

            var variant = message.Metadata["Experiment_full-prob"]?.ToString();
            Assert.Equal("VariantA", variant, $"Iteration {i}: Expected VariantA with 1.0 probability");
        }
    }

    [Test]
    public async Task SingleExperiment_CustomVariantNames()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("custom-names", 0.5, "control-group", "treatment-group");

        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var variant = message.Metadata["Experiment_custom-names"]?.ToString();
        Assert.True(variant == "control-group" || variant == "treatment-group",
            $"Expected custom variant names but got: {variant}");
    }

    #endregion

    #region Multiple Experiments Tests

    [Test]
    public async Task MultipleExperiments_AllAssignVariants()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("experiment-1", 0.5, "A1", "B1");
        middleware.RegisterExperiment("experiment-2", 0.5, "A2", "B2");
        middleware.RegisterExperiment("experiment-3", 0.5, "A3", "B3");

        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // All three experiments should have variants assigned
        Assert.True(message.Metadata.ContainsKey("Experiment_experiment-1"));
        Assert.True(message.Metadata.ContainsKey("Experiment_experiment-2"));
        Assert.True(message.Metadata.ContainsKey("Experiment_experiment-3"));

        var variant1 = message.Metadata["Experiment_experiment-1"]?.ToString();
        var variant2 = message.Metadata["Experiment_experiment-2"]?.ToString();
        var variant3 = message.Metadata["Experiment_experiment-3"]?.ToString();

        Assert.True(variant1 == "A1" || variant1 == "B1");
        Assert.True(variant2 == "A2" || variant2 == "B2");
        Assert.True(variant3 == "A3" || variant3 == "B3");
    }

    [Test]
    public async Task MultipleExperiments_DifferentProbabilities()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("always-a", 1.0, "A", "B");
        middleware.RegisterExperiment("always-b", 0.0, "A", "B");
        middleware.RegisterExperiment("half-half", 0.5, "A", "B");

        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("A", message.Metadata["Experiment_always-a"]?.ToString());
        Assert.Equal("B", message.Metadata["Experiment_always-b"]?.ToString());
        // half-half could be either
        var halfHalf = message.Metadata["Experiment_half-half"]?.ToString();
        Assert.True(halfHalf == "A" || halfHalf == "B");
    }

    [Test]
    public async Task MultipleExperiments_IndependentAssignment()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("exp-1", 0.5, "A1", "B1");
        middleware.RegisterExperiment("exp-2", 0.5, "A2", "B2");

        // Run many iterations and verify experiments are independently assigned
        var sameAssignmentCount = 0;
        var differentAssignmentCount = 0;

        for (int i = 0; i < 100; i++)
        {
            var message = CreateTestMessage($"sender-{i}");

            await middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

            var variant1 = message.Metadata["Experiment_exp-1"]?.ToString();
            var variant2 = message.Metadata["Experiment_exp-2"]?.ToString();

            // Check if both are A variants or both are B variants
            bool bothA = variant1 == "A1" && variant2 == "A2";
            bool bothB = variant1 == "B1" && variant2 == "B2";

            if (bothA || bothB)
                sameAssignmentCount++;
            else
                differentAssignmentCount++;
        }

        // With independent assignment, we should see a mix of same and different
        // This is a probabilistic test, but with 100 iterations it should very rarely fail
        Assert.True(sameAssignmentCount > 0, "Expected some messages with same variant across experiments");
        Assert.True(differentAssignmentCount > 0, "Expected some messages with different variants across experiments");
    }

    #endregion

    #region Statistical Distribution Tests

    [Test]
    public async Task Distribution_FiftyPercent_ReasonablyBalanced()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("balanced", 0.5, "A", "B");

        var variantACounts = 0;
        var variantBCounts = 0;
        const int iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            var message = CreateTestMessage($"sender-{i}");

            await middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

            var variant = message.Metadata["Experiment_balanced"]?.ToString();
            if (variant == "A") variantACounts++;
            else if (variant == "B") variantBCounts++;
        }

        // With 50% probability and 1000 iterations, we expect roughly 500 each
        // Allow for statistical variance (within 35% to 65% range should be very safe)
        var ratioA = (double)variantACounts / iterations;
        Assert.True(ratioA >= 0.35 && ratioA <= 0.65,
            $"Expected ~50% variant A, but got {ratioA:P1} ({variantACounts}/{iterations})");
    }

    [Test]
    public async Task Distribution_TenPercent_MostlyVariantB()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("low-prob", 0.1, "A", "B");

        var variantACounts = 0;
        const int iterations = 500;

        for (int i = 0; i < iterations; i++)
        {
            var message = CreateTestMessage($"sender-{i}");

            await middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

            var variant = message.Metadata["Experiment_low-prob"]?.ToString();
            if (variant == "A") variantACounts++;
        }

        // With 10% probability, should have fewer than 25% A variants
        var ratioA = (double)variantACounts / iterations;
        Assert.True(ratioA <= 0.25,
            $"Expected ~10% variant A, but got {ratioA:P1} ({variantACounts}/{iterations})");
    }

    [Test]
    public async Task Distribution_NinetyPercent_MostlyVariantA()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("high-prob", 0.9, "A", "B");

        var variantACounts = 0;
        const int iterations = 500;

        for (int i = 0; i < iterations; i++)
        {
            var message = CreateTestMessage($"sender-{i}");

            await middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

            var variant = message.Metadata["Experiment_high-prob"]?.ToString();
            if (variant == "A") variantACounts++;
        }

        // With 90% probability, should have more than 75% A variants
        var ratioA = (double)variantACounts / iterations;
        Assert.True(ratioA >= 0.75,
            $"Expected ~90% variant A, but got {ratioA:P1} ({variantACounts}/{iterations})");
    }

    #endregion

    #region Metadata Handling Tests

    [Test]
    public async Task MetadataKey_HasCorrectFormat()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("my-experiment", 0.5, "A", "B");

        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("Experiment_my-experiment"));
    }

    [Test]
    public async Task ExistingMetadata_Preserved()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("test", 0.5, "A", "B");

        var message = CreateTestMessage();
        message.Metadata["ExistingKey"] = "ExistingValue";
        message.Metadata["AnotherKey"] = 42;

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Existing metadata should be preserved
        Assert.Equal("ExistingValue", message.Metadata["ExistingKey"]?.ToString());
        Assert.Equal(42, message.Metadata["AnotherKey"]);
        // And experiment metadata should be added
        Assert.True(message.Metadata.ContainsKey("Experiment_test"));
    }

    [Test]
    public async Task SpecialCharactersInExperimentName_HandledCorrectly()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("feature-flag-v2.0", 0.5, "enabled", "disabled");

        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("Experiment_feature-flag-v2.0"));
        var variant = message.Metadata["Experiment_feature-flag-v2.0"]?.ToString();
        Assert.True(variant == "enabled" || variant == "disabled");
    }

    #endregion

    #region Next Delegate Tests

    [Test]
    public async Task NextDelegate_AlwaysCalled()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("test", 0.5, "A", "B");

        var nextCallCount = 0;

        for (int i = 0; i < 10; i++)
        {
            var message = CreateTestMessage($"sender-{i}");

            await middleware.InvokeAsync(
                message,
                (msg, ct) =>
                {
                    nextCallCount++;
                    return Task.FromResult(MessageResult.Ok("OK"));
                },
                CancellationToken.None);
        }

        Assert.Equal(10, nextCallCount);
    }

    [Test]
    public async Task NextDelegate_ReceivesEnrichedMessage()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("enrichment-test", 1.0, "EnrichedA", "EnrichedB");

        var capturedVariant = "";

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                capturedVariant = msg.Metadata["Experiment_enrichment-test"]?.ToString() ?? "";
                return Task.FromResult(MessageResult.Ok("OK"));
            },
            CancellationToken.None);

        // With 1.0 probability, should always be EnrichedA
        Assert.Equal("EnrichedA", capturedVariant);
    }

    [Test]
    public async Task NextDelegate_ResultPassedThrough()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("test", 0.5, "A", "B");

        var successResult = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Success response")),
            CancellationToken.None);

        var failResult = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Fail("Failure response")),
            CancellationToken.None);

        Assert.True(successResult.Success);
        Assert.Equal("Success response", successResult.Response);
        Assert.False(failResult.Success);
        Assert.Equal("Failure response", failResult.Error);
    }

    #endregion

    #region Edge Cases Tests

    [Test]
    public async Task RegisterExperiment_SameNameTwice_OverwritesPrevious()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("experiment", 0.0, "OldA", "OldB"); // Always B
        middleware.RegisterExperiment("experiment", 1.0, "NewA", "NewB"); // Always A (overwrites)

        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Should use the new configuration (always A)
        Assert.Equal("NewA", message.Metadata["Experiment_experiment"]?.ToString());
    }

    [Test]
    public async Task EmptyExperimentName_StillWorks()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("", 0.5, "A", "B");

        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("Experiment_"));
    }

    [Test]
    public async Task EmptyVariantNames_StillWorks()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("empty-variants", 0.5, "", "");

        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        var variant = message.Metadata["Experiment_empty-variants"]?.ToString();
        Assert.Equal("", variant);
    }

    [Test]
    public async Task BoundaryProbability_NearZero()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("near-zero", 0.001, "A", "B");

        var variantACounts = 0;
        const int iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            var message = CreateTestMessage($"sender-{i}");

            await middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

            var variant = message.Metadata["Experiment_near-zero"]?.ToString();
            if (variant == "A") variantACounts++;
        }

        // With 0.1% probability, very few should be A
        Assert.True(variantACounts <= 50,
            $"Expected very few variant A with 0.1% probability, but got {variantACounts}");
    }

    [Test]
    public async Task BoundaryProbability_NearOne()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("near-one", 0.999, "A", "B");

        var variantBCounts = 0;
        const int iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            var message = CreateTestMessage($"sender-{i}");

            await middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                CancellationToken.None);

            var variant = message.Metadata["Experiment_near-one"]?.ToString();
            if (variant == "B") variantBCounts++;
        }

        // With 99.9% probability for A, very few should be B
        Assert.True(variantBCounts <= 50,
            $"Expected very few variant B with 99.9% probability for A, but got {variantBCounts}");
    }

    [Test]
    public async Task MessagePriority_NotAffected()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("test", 0.5, "A", "B");

        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Test",
            Content = "Content",
            Priority = MessagePriority.Urgent
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Priority should remain unchanged
        Assert.Equal(MessagePriority.Urgent, message.Priority);
    }

    [Test]
    public async Task MessageCategory_NotAffected()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("test", 0.5, "A", "B");

        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Test",
            Content = "Content",
            Category = "OriginalCategory"
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Category should remain unchanged
        Assert.Equal("OriginalCategory", message.Category);
    }

    #endregion

    #region Pipeline Integration Tests

    [Test]
    public async Task PipelineIntegration_WorksWithOtherMiddleware()
    {
        var pipeline = new MiddlewarePipeline();
        var executionOrder = new List<string>();

        var abTesting = new ABTestingMiddleware();
        abTesting.RegisterExperiment("pipeline-test", 1.0, "TestVariant", "Other");

        // Add a tracking middleware before
        pipeline.Use(new CallbackMiddleware(
            before: msg => executionOrder.Add("Before-AB"),
            after: (msg, result) => executionOrder.Add("After-AB")
        ));

        pipeline.Use(abTesting);

        // Add a middleware after that checks the experiment metadata
        pipeline.Use(new CallbackMiddleware(
            before: msg =>
            {
                executionOrder.Add("CheckMetadata");
                var hasExperiment = msg.Metadata.ContainsKey("Experiment_pipeline-test");
                if (hasExperiment)
                    executionOrder.Add("HasExperiment");
            }
        ));

        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            executionOrder.Add("Handler");
            return Task.FromResult(MessageResult.Ok("Done"));
        });

        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.Contains("Before-AB", executionOrder);
        Assert.Contains("CheckMetadata", executionOrder);
        Assert.Contains("HasExperiment", executionOrder);
        Assert.Contains("Handler", executionOrder);
    }

    [Test]
    public async Task PipelineIntegration_ExperimentMetadataAvailableDownstream()
    {
        var pipeline = new MiddlewarePipeline();

        var abTesting = new ABTestingMiddleware();
        abTesting.RegisterExperiment("downstream-test", 1.0, "Expected", "NotExpected");

        pipeline.Use(abTesting);

        string? capturedVariant = null;

        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            capturedVariant = msg.Metadata["Experiment_downstream-test"]?.ToString();
            return Task.FromResult(MessageResult.Ok("Done"));
        });

        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.Equal("Expected", capturedVariant);
    }

    #endregion

    #region Concurrent Access Tests

    [Test]
    public async Task ConcurrentMessages_AllGetVariants()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("concurrent-test", 0.5, "A", "B");

        var tasks = new List<Task<AgentMessage>>();
        const int concurrentCount = 50;

        for (int i = 0; i < concurrentCount; i++)
        {
            var message = CreateTestMessage($"sender-{i}");
            var task = Task.Run(async () =>
            {
                await middleware.InvokeAsync(
                    message,
                    (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
                    CancellationToken.None);
                return message;
            });
            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);

        // All messages should have received a variant
        foreach (var msg in results)
        {
            Assert.True(msg.Metadata.ContainsKey("Experiment_concurrent-test"),
                $"Message from {msg.SenderId} missing experiment metadata");
            var variant = msg.Metadata["Experiment_concurrent-test"]?.ToString();
            Assert.True(variant == "A" || variant == "B",
                $"Invalid variant for message from {msg.SenderId}: {variant}");
        }
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
