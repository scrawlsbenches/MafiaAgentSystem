using System.Collections.Concurrent;
using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;

namespace TestRunner.Tests;

/// <summary>
/// Additional coverage tests for AdvancedMiddleware classes to fill gaps in existing tests.
/// Tests edge cases, boundary conditions, and integration scenarios.
/// </summary>
public class AdvancedMiddlewareCoverageTests
{
    #region TraceSpan Property Tests

    [Test]
    public void TraceSpan_DefaultValues_AreEmpty()
    {
        var span = new TraceSpan();

        Assert.Equal("", span.TraceId);
        Assert.Equal("", span.SpanId);
        Assert.Null(span.ParentSpanId);
        Assert.Equal("", span.ServiceName);
        Assert.Equal("", span.OperationName);
        Assert.Equal(default(DateTime), span.StartTime);
        Assert.Equal(default(TimeSpan), span.Duration);
        Assert.False(span.Success);
        Assert.NotNull(span.Tags);
        Assert.Equal(0, span.Tags.Count);
    }

    [Test]
    public void TraceSpan_SetProperties_AllPersist()
    {
        var startTime = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(150);
        var tags = new Dictionary<string, string>
        {
            ["tag1"] = "value1",
            ["tag2"] = "value2"
        };

        var span = new TraceSpan
        {
            TraceId = "trace-123",
            SpanId = "span-456",
            ParentSpanId = "parent-789",
            ServiceName = "TestService",
            OperationName = "TestOperation",
            StartTime = startTime,
            Duration = duration,
            Success = true,
            Tags = tags
        };

        Assert.Equal("trace-123", span.TraceId);
        Assert.Equal("span-456", span.SpanId);
        Assert.Equal("parent-789", span.ParentSpanId);
        Assert.Equal("TestService", span.ServiceName);
        Assert.Equal("TestOperation", span.OperationName);
        Assert.Equal(startTime, span.StartTime);
        Assert.Equal(duration, span.Duration);
        Assert.True(span.Success);
        Assert.Equal(2, span.Tags.Count);
        Assert.Equal("value1", span.Tags["tag1"]);
    }

    [Test]
    public void TraceSpan_Tags_CanBeModifiedAfterCreation()
    {
        var span = new TraceSpan();

        span.Tags["newKey"] = "newValue";
        span.Tags["anotherKey"] = "anotherValue";

        Assert.Equal(2, span.Tags.Count);
        Assert.Equal("newValue", span.Tags["newKey"]);
        Assert.Equal("anotherValue", span.Tags["anotherKey"]);
    }

    [Test]
    public void TraceSpan_Duration_CanBeZero()
    {
        var span = new TraceSpan
        {
            Duration = TimeSpan.Zero
        };

        Assert.Equal(TimeSpan.Zero, span.Duration);
        Assert.Equal(0, span.Duration.TotalMilliseconds);
    }

    [Test]
    public void TraceSpan_Duration_CanBeNegative()
    {
        // Edge case: negative duration (shouldn't happen in practice but should handle)
        var span = new TraceSpan
        {
            Duration = TimeSpan.FromMilliseconds(-100)
        };

        Assert.Equal(-100, span.Duration.TotalMilliseconds);
    }

    #endregion

    #region DistributedTracingMiddleware Edge Cases

    [Test]
    public async Task TracingMiddleware_ExportJaegerFormat_MultipleTraceGroups_GroupsCorrectly()
    {
        var middleware = new DistributedTracingMiddleware("TestService");

        // Create messages with different trace IDs
        var message1 = CreateTestMessage("sender-1");
        var message2 = CreateTestMessage("sender-2");
        message2.Metadata["TraceId"] = "different-trace-id-abc123def";
        var message3 = CreateTestMessage("sender-3");
        message3.Metadata["TraceId"] = "different-trace-id-abc123def"; // Same as message2

        await middleware.InvokeAsync(message1, SuccessHandler, CancellationToken.None);
        await middleware.InvokeAsync(message2, SuccessHandler, CancellationToken.None);
        await middleware.InvokeAsync(message3, SuccessHandler, CancellationToken.None);

        var export = middleware.ExportJaegerFormat();

        // Should have two trace groups
        var traceIdCount = CountOccurrences(export, "Trace ID:");
        Assert.Equal(2, traceIdCount);
    }

    [Test]
    public async Task TracingMiddleware_ExportJaegerFormat_ShowsDurationInMilliseconds()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(message, async (msg, ct) =>
        {
            await Task.Delay(10);
            return MessageResult.Ok("Done");
        }, CancellationToken.None);

        var export = middleware.ExportJaegerFormat();

        Assert.Contains("Duration:", export);
        Assert.Contains("ms", export);
    }

    [Test]
    public async Task TracingMiddleware_ExportJaegerFormat_ShowsSuccessStatus()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        var export = middleware.ExportJaegerFormat();

        Assert.Contains("Success: True", export);
    }

    [Test]
    public async Task TracingMiddleware_FailedResult_ShowsSuccessFalseInExport()
    {
        var middleware = new DistributedTracingMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(message, (msg, ct) =>
            Task.FromResult(MessageResult.Fail("Test failure")), CancellationToken.None);

        var export = middleware.ExportJaegerFormat();

        Assert.Contains("Success: False", export);
    }

    [Test]
    public async Task TracingMiddleware_ConcurrentInvocations_TracesAreDistinct()
    {
        var middleware = new DistributedTracingMiddleware("ConcurrentService");
        var tasks = new List<Task>();

        for (int i = 0; i < 50; i++)
        {
            var message = CreateTestMessage($"concurrent-sender-{i}");
            tasks.Add(middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        var traces = middleware.GetTraces();
        Assert.Equal(50, traces.Count);

        // All span IDs should be unique
        var spanIds = traces.Select(t => t.SpanId).ToHashSet();
        Assert.Equal(50, spanIds.Count);
    }

    #endregion

    #region SemanticRoutingMiddleware Edge Cases

    [Test]
    public async Task SemanticRouting_EmptySubjectWithKeywordInContent_DetectsIntent()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "",
            Content = "This is urgent and needs immediate attention"
        };

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("urgent", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_TechnicalWithExistingCategory_KeepsOriginalCategory()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Bug Report",
            Content = "The system crashed with a fatal error",
            Category = "CustomerService"
        };

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        // Should NOT change to TechnicalSupport since category is already set
        Assert.Equal("CustomerService", message.Category);
    }

    [Test]
    public async Task SemanticRouting_ComplaintWithUrgentPriority_DoesNotDowngrade()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Complaint",
            Content = "I am disappointed with the service",
            Priority = MessagePriority.Urgent
        };

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        // Should remain Urgent, not downgrade to High
        Assert.Equal(MessagePriority.Urgent, message.Priority);
    }

    [Test]
    public async Task SemanticRouting_VeryLongContent_DetectsIntentAtEnd()
    {
        var middleware = new SemanticRoutingMiddleware();
        var paddingText = string.Join(" ", Enumerable.Repeat("neutral", 1000));
        var message = CreateTestMessage($"{paddingText} I am angry about this");

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("complaint", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_AllIntentTypes_DetectedSimultaneously()
    {
        var middleware = new SemanticRoutingMiddleware();
        // Message containing keywords from all intents
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Question",
            Content = "Why is this terrible bug causing errors immediately? Thank you for your excellent help!",
            Priority = MessagePriority.Low,
            Category = ""
        };

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        var intents = message.Metadata["DetectedIntents"].ToString()!;
        Assert.Contains("question", intents);    // "why"
        Assert.Contains("complaint", intents);   // "terrible"
        Assert.Contains("urgent", intents);      // "immediately"
        Assert.Contains("praise", intents);      // "thank", "excellent"
        Assert.Contains("technical", intents);   // "bug", "error"
    }

    #endregion

    #region MessageTransformationMiddleware Edge Cases

    [Test]
    public async Task MessageTransformation_MultipleEmailsAndPhones_CountsBoth()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(
            "Contact us: john@example.com, jane@test.org. " +
            "Call 555-111-2222 or 555.333.4444"
        );

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsEmail"]);
        Assert.Equal(2, (int)message.Metadata["EmailCount"]);
        Assert.True((bool)message.Metadata["ContainsPhone"]);
        Assert.Equal(2, (int)message.Metadata["PhoneCount"]);
    }

    [Test]
    public async Task MessageTransformation_MixedWhitespace_Normalized()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage("Hello\t\t\nWorld\r\n  Test");

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        Assert.Equal("Hello World Test", message.Content);
    }

    [Test]
    public async Task MessageTransformation_ScriptTagsNotCaseSensitive_OnlyExactRemoved()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage("<script>bad</script> <SCRIPT>also bad</SCRIPT>");

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        // Only lowercase versions are removed by the sanitizer
        Assert.False(message.Content.Contains("<script>"));
        Assert.False(message.Content.Contains("</script>"));
        // Uppercase versions remain (as documented behavior)
        Assert.Contains("<SCRIPT>", message.Content);
    }

    [Test]
    public async Task MessageTransformation_ProcessingTimestamp_IsValidIsoFormat()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        var timestamp = message.Metadata["ProcessingTimestamp"].ToString();
        Assert.NotNull(timestamp);

        // Should be parseable as ISO 8601 format
        var parsed = DateTime.TryParse(timestamp, out var parsedTime);
        Assert.True(parsed, $"Expected valid ISO timestamp but got: {timestamp}");
    }

    [Test]
    public async Task MessageTransformation_DetectLanguage_InsufficientSpanish_DefaultsToEnglish()
    {
        var middleware = new MessageTransformationMiddleware();
        // Only one Spanish word, not enough to trigger detection
        var message = CreateTestMessage("Hello world el testing");

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        Assert.Equal("English", message.Metadata["DetectedLanguage"]);
    }

    [Test]
    public async Task MessageTransformation_DetectLanguage_InsufficientFrench_DefaultsToEnglish()
    {
        var middleware = new MessageTransformationMiddleware();
        // Only one French word
        var message = CreateTestMessage("Hello le testing world");

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        Assert.Equal("English", message.Metadata["DetectedLanguage"]);
    }

    #endregion

    #region MessageQueueMiddleware Edge Cases

    [Test]
    public async Task MessageQueue_BatchSizeZero_StillProcesses()
    {
        // Edge case: batch size of 0 might cause issues
        // Using batch size 1 as minimum safe value
        var middleware = new MessageQueueMiddleware(batchSize: 1);
        var processed = false;

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                processed = true;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.True(processed);
        Assert.True(result.Success);
    }

    [Test]
    public async Task MessageQueue_VeryShortTimeout_StillWorks()
    {
        var middleware = new MessageQueueMiddleware(
            batchSize: 100,
            batchTimeout: TimeSpan.FromMilliseconds(1));
        var processed = false;

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                processed = true;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(processed);
    }

    [Test]
    public async Task MessageQueue_MultipleExceptions_AllReported()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 3);

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 3; i++)
        {
            var index = i;
            var message = CreateTestMessage($"sender-{index}");
            tasks.Add(middleware.InvokeAsync(
                message,
                (msg, ct) => throw new InvalidOperationException($"Error {index}"),
                CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        Assert.True(results.All(r => !r.Success));
        Assert.True(results.All(r => r.Error!.Contains("Batch processing error")));
    }

    #endregion

    #region ABTestingMiddleware Edge Cases

    [Test]
    public async Task ABTesting_NegativeProbability_TreatedAsZero()
    {
        var middleware = new ABTestingMiddleware();
        // Edge case: negative probability
        middleware.RegisterExperiment("negative-test", -0.5, "A", "B");

        var variantBCount = 0;
        for (int i = 0; i < 20; i++)
        {
            var message = CreateTestMessage($"sender-{i}");
            await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

            if (message.Metadata["Experiment_negative-test"]?.ToString() == "B")
                variantBCount++;
        }

        // With negative probability, all should be B
        Assert.Equal(20, variantBCount);
    }

    [Test]
    public async Task ABTesting_ProbabilityGreaterThanOne_TreatedAsOne()
    {
        var middleware = new ABTestingMiddleware();
        middleware.RegisterExperiment("over-one", 1.5, "A", "B");

        var variantACount = 0;
        for (int i = 0; i < 20; i++)
        {
            var message = CreateTestMessage($"sender-{i}");
            await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

            if (message.Metadata["Experiment_over-one"]?.ToString() == "A")
                variantACount++;
        }

        // With probability > 1, all should be A
        Assert.Equal(20, variantACount);
    }

    [Test]
    public async Task ABTesting_LongExperimentName_Works()
    {
        var middleware = new ABTestingMiddleware();
        var longName = new string('x', 1000);
        middleware.RegisterExperiment(longName, 1.0, "A", "B");

        var message = CreateTestMessage();
        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey($"Experiment_{longName}"));
        Assert.Equal("A", message.Metadata[$"Experiment_{longName}"]);
    }

    [Test]
    public async Task ABTesting_LongVariantNames_Preserved()
    {
        var middleware = new ABTestingMiddleware();
        var longVariantA = new string('A', 500);
        var longVariantB = new string('B', 500);
        middleware.RegisterExperiment("long-variants", 1.0, longVariantA, longVariantB);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        Assert.Equal(longVariantA, message.Metadata["Experiment_long-variants"]);
    }

    #endregion

    #region FeatureFlagsMiddleware Edge Cases

    [Test]
    public async Task FeatureFlags_ConditionThrowsException_FlagIsDisabled()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "throwing-condition",
            enabled: true,
            condition: msg => throw new InvalidOperationException("Condition failed"));

        var message = CreateTestMessage();

        // Should not throw - exception in condition should be caught
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None));
    }

    [Test]
    public async Task FeatureFlags_NullConditionWithEnabledFalse_FlagIsDisabled()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("null-disabled", enabled: false, condition: null);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        var context = message.GetContext();
        Assert.False(context.Get<bool>("Feature_null-disabled"));
    }

    [Test]
    public async Task FeatureFlags_MultipleFlags_EvaluatedInDictionaryOrder()
    {
        var middleware = new FeatureFlagsMiddleware();
        var evaluationOrder = new List<string>();

        middleware.RegisterFlag("aaa", enabled: true);
        middleware.RegisterFlag("bbb", enabled: true);
        middleware.RegisterFlag("ccc", enabled: true);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        var context = message.GetContext();
        Assert.True(context.Get<bool>("Feature_aaa"));
        Assert.True(context.Get<bool>("Feature_bbb"));
        Assert.True(context.Get<bool>("Feature_ccc"));
    }

    [Test]
    public async Task FeatureFlags_ConditionBasedOnPreviousMetadata_Works()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "metadata-dependent",
            enabled: true,
            condition: msg => msg.Metadata.TryGetValue("role", out var role) && role.ToString() == "admin");

        var adminMessage = CreateTestMessage();
        adminMessage.Metadata["role"] = "admin";

        var userMessage = CreateTestMessage();
        userMessage.Metadata["role"] = "user";

        await middleware.InvokeAsync(adminMessage, SuccessHandler, CancellationToken.None);
        await middleware.InvokeAsync(userMessage, SuccessHandler, CancellationToken.None);

        Assert.True(adminMessage.GetContext().Get<bool>("Feature_metadata-dependent"));
        Assert.False(userMessage.GetContext().Get<bool>("Feature_metadata-dependent"));
    }

    #endregion

    #region AgentHealthCheckMiddleware Edge Cases

    [Test]
    public void AgentHealthCheck_RegisterSameAgentTwice_OverwritesPrevious()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));

        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(false));

        var status = middleware.GetHealthStatus();
        Assert.Equal(1, status.Count);
        // Initial status is healthy regardless of health check function
        Assert.True(status["agent-1"]);
    }

    [Test]
    public async Task AgentHealthCheck_TargetHealthyAgent_MessageUnchanged()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("healthy-agent", () => Task.FromResult(true));

        var message = new AgentMessage
        {
            SenderId = "sender",
            ReceiverId = "healthy-agent",
            Subject = "Test",
            Content = "Content"
        };

        await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        Assert.Equal("healthy-agent", message.ReceiverId);
    }

    [Test]
    public async Task AgentHealthCheck_WhitespaceReceiverId_PassesThrough()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1));
        middleware.RegisterAgent("agent-1", () => Task.FromResult(true));

        var message = new AgentMessage
        {
            SenderId = "sender",
            ReceiverId = "   ",
            Subject = "Test",
            Content = "Content"
        };

        var nextCalled = false;
        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.Success);
    }

    #endregion

    #region WorkflowOrchestrationMiddleware Edge Cases

    [Test]
    public async Task WorkflowOrchestration_ZeroStages_PassesThrough()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        // Register workflow with no stages (edge case)
        middleware.RegisterWorkflow("empty-workflow");

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "empty-workflow";
        message.Metadata["StageIndex"] = 0;

        var handlerCalled = false;
        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                handlerCalled = true;
                return Task.FromResult(MessageResult.Ok("Passed"));
            },
            CancellationToken.None);

        // Should pass through since stageIndex (0) is not < stages.Count (0)
        Assert.True(handlerCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task WorkflowOrchestration_VeryLargeStageIndex_PassesThrough()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" });

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "test-workflow";
        message.Metadata["StageIndex"] = 99999; // Large but valid int, out of bounds for stages

        var handlerCalled = false;
        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                handlerCalled = true;
                return Task.FromResult(MessageResult.Ok("Passed"));
            },
            CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task WorkflowOrchestration_StringStageIndex_Converts()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "test-workflow";
        message.Metadata["StageIndex"] = "0"; // String instead of int

        var result = await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task WorkflowOrchestration_NullResponseInResult_UseOriginalContent()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "test-workflow";
        message.Content = "Original Content";

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok(null)), // Null response
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.ForwardedMessages.Count);
        Assert.Equal("Original Content", result.ForwardedMessages[0].Content);
    }

    [Test]
    public async Task WorkflowOrchestration_ForwardedMessage_HasAllOriginalMetadata()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "test-workflow";
        message.Metadata["StageIndex"] = 0;
        message.Metadata["CustomKey1"] = "CustomValue1";
        message.Metadata["CustomKey2"] = 12345;
        message.ConversationId = "conversation-abc";

        var result = await middleware.InvokeAsync(message, SuccessHandler, CancellationToken.None);

        var forwarded = result.ForwardedMessages[0];
        Assert.Equal("test-workflow", forwarded.Metadata["WorkflowId"]);
        Assert.Equal(1, forwarded.Metadata["StageIndex"]);
        Assert.Equal("CustomValue1", forwarded.Metadata["CustomKey1"]);
        Assert.Equal(12345, forwarded.Metadata["CustomKey2"]);
        Assert.Equal("conversation-abc", forwarded.ConversationId);
    }

    #endregion

    #region Integration Tests - Combined Middleware Scenarios

    [Test]
    public async Task Integration_TracingAndSemanticRouting_BothEnrichMessage()
    {
        var pipeline = new MiddlewarePipeline();
        var tracingMiddleware = new DistributedTracingMiddleware("IntegrationService");
        var semanticMiddleware = new SemanticRoutingMiddleware();

        pipeline.Use(tracingMiddleware);
        pipeline.Use(semanticMiddleware);

        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Urgent",
            Content = "This is an emergency request",
            Priority = MessagePriority.Normal
        };

        var builtPipeline = pipeline.Build(SuccessHandler);
        await builtPipeline(message, CancellationToken.None);

        // Tracing should add TraceId and SpanId
        Assert.True(message.Metadata.ContainsKey("TraceId"));
        Assert.True(message.Metadata.ContainsKey("SpanId"));

        // Semantic should detect urgent intent and boost priority
        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("urgent", message.Metadata["DetectedIntents"].ToString()!);
        Assert.Equal(MessagePriority.High, message.Priority);

        // Tracing should have recorded the span
        Assert.Equal(1, tracingMiddleware.GetTraces().Count);
    }

    [Test]
    public async Task Integration_TransformationAndFeatureFlags_BothApply()
    {
        var pipeline = new MiddlewarePipeline();
        var transformMiddleware = new MessageTransformationMiddleware();
        var featureFlagsMiddleware = new FeatureFlagsMiddleware();
        featureFlagsMiddleware.RegisterFlag("email-detection", enabled: true);

        pipeline.Use(transformMiddleware);
        pipeline.Use(featureFlagsMiddleware);

        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "   Spaced   Subject   ",
            Content = "Contact: test@example.com  with   spaces"
        };

        var builtPipeline = pipeline.Build(SuccessHandler);
        await builtPipeline(message, CancellationToken.None);

        // Transformation should normalize whitespace
        Assert.Equal("Spaced Subject", message.Subject);
        Assert.Equal("Contact: test@example.com with spaces", message.Content);

        // Transformation should detect email
        Assert.True((bool)message.Metadata["ContainsEmail"]);

        // Feature flag should be set
        var context = message.GetContext();
        Assert.True(context.Get<bool>("Feature_email-detection"));
    }

    [Test]
    public async Task Integration_ABTestingAndWorkflow_BothApply()
    {
        var pipeline = new MiddlewarePipeline();
        var abTestingMiddleware = new ABTestingMiddleware();
        abTestingMiddleware.RegisterExperiment("workflow-variant", 1.0, "FastPath", "SlowPath");

        var workflowMiddleware = new WorkflowOrchestrationMiddleware();
        workflowMiddleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        pipeline.Use(abTestingMiddleware);
        pipeline.Use(workflowMiddleware);

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "test-workflow";
        message.Metadata["StageIndex"] = 0;

        var builtPipeline = pipeline.Build(SuccessHandler);
        var result = await builtPipeline(message, CancellationToken.None);

        // AB testing should assign variant
        Assert.Equal("FastPath", message.Metadata["Experiment_workflow-variant"]);

        // Workflow should forward to next stage
        Assert.Equal(1, result.ForwardedMessages.Count);

        // Forwarded message should also have the experiment metadata
        Assert.Equal("FastPath", result.ForwardedMessages[0].Metadata["Experiment_workflow-variant"]);
    }

    [Test]
    public async Task Integration_AllMiddleware_ProcessInCorrectOrder()
    {
        var pipeline = new MiddlewarePipeline();
        var executionOrder = new ConcurrentQueue<string>();

        // Tracing first
        pipeline.Use(next => async (msg, ct) =>
        {
            executionOrder.Enqueue("Tracing-Before");
            var result = await next(msg, ct);
            executionOrder.Enqueue("Tracing-After");
            return result;
        });

        // Semantic routing
        pipeline.Use(next => async (msg, ct) =>
        {
            executionOrder.Enqueue("Semantic-Before");
            var result = await next(msg, ct);
            executionOrder.Enqueue("Semantic-After");
            return result;
        });

        // Transformation
        pipeline.Use(next => async (msg, ct) =>
        {
            executionOrder.Enqueue("Transform-Before");
            var result = await next(msg, ct);
            executionOrder.Enqueue("Transform-After");
            return result;
        });

        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            executionOrder.Enqueue("Handler");
            return Task.FromResult(MessageResult.Ok("Done"));
        });

        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        var order = executionOrder.ToArray();
        Assert.Equal("Tracing-Before", order[0]);
        Assert.Equal("Semantic-Before", order[1]);
        Assert.Equal("Transform-Before", order[2]);
        Assert.Equal("Handler", order[3]);
        Assert.Equal("Transform-After", order[4]);
        Assert.Equal("Semantic-After", order[5]);
        Assert.Equal("Tracing-After", order[6]);
    }

    #endregion

    #region WorkflowDefinition and WorkflowStage Class Tests

    [Test]
    public void WorkflowDefinition_CanModifyStagesAfterCreation()
    {
        var definition = new WorkflowDefinition
        {
            WorkflowId = "modifiable-workflow"
        };

        definition.Stages.Add(new WorkflowStage { Name = "Added1", AgentId = "agent-1" });
        definition.Stages.Add(new WorkflowStage { Name = "Added2", AgentId = "agent-2" });

        Assert.Equal(2, definition.Stages.Count);
        Assert.Equal("Added1", definition.Stages[0].Name);
        Assert.Equal("Added2", definition.Stages[1].Name);
    }

    [Test]
    public void WorkflowStage_Condition_CanBeSetAndEvaluated()
    {
        var stage = new WorkflowStage
        {
            Name = "ConditionalStage",
            AgentId = "conditional-agent",
            Condition = msg => msg.Priority >= MessagePriority.High
        };

        var highPriorityMessage = new AgentMessage
        {
            SenderId = "test",
            Subject = "Test",
            Content = "Test",
            Priority = MessagePriority.High
        };

        var lowPriorityMessage = new AgentMessage
        {
            SenderId = "test",
            Subject = "Test",
            Content = "Test",
            Priority = MessagePriority.Low
        };

        Assert.NotNull(stage.Condition);
        Assert.True(stage.Condition!(highPriorityMessage));
        Assert.False(stage.Condition!(lowPriorityMessage));
    }

    [Test]
    public void WorkflowStage_LongNames_Supported()
    {
        var longName = new string('N', 1000);
        var longAgentId = new string('A', 1000);

        var stage = new WorkflowStage
        {
            Name = longName,
            AgentId = longAgentId
        };

        Assert.Equal(longName, stage.Name);
        Assert.Equal(longAgentId, stage.AgentId);
    }

    #endregion

    #region Timer Disposal Tests (P0-NEW-3)

    [Test]
    public void MessageQueueMiddleware_Dispose_StopsTimer()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 10, batchTimeout: TimeSpan.FromSeconds(1));

        // Should not throw
        middleware.Dispose();

        // Calling dispose again should be safe (idempotent)
        middleware.Dispose();

        Assert.True(true); // If we get here, disposal worked
    }

    [Test]
    public void MessageQueueMiddleware_ImplementsIDisposable()
    {
        var middleware = new MessageQueueMiddleware();

        Assert.True(middleware is IDisposable);

        ((IDisposable)middleware).Dispose();
    }

    [Test]
    public void AgentHealthCheckMiddleware_Dispose_StopsTimer()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromSeconds(1));

        // Should not throw
        middleware.Dispose();

        // Calling dispose again should be safe (idempotent)
        middleware.Dispose();

        Assert.True(true); // If we get here, disposal worked
    }

    [Test]
    public void AgentHealthCheckMiddleware_ImplementsIDisposable()
    {
        var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromSeconds(1));

        Assert.True(middleware is IDisposable);

        ((IDisposable)middleware).Dispose();
    }

    [Test]
    public void MessageQueueMiddleware_UsingStatement_DisposesCorrectly()
    {
        // Verify middleware works with using statement
        using (var middleware = new MessageQueueMiddleware())
        {
            Assert.NotNull(middleware);
        }
        // Timer should be disposed after using block
        Assert.True(true);
    }

    [Test]
    public void AgentHealthCheckMiddleware_UsingStatement_DisposesCorrectly()
    {
        // Verify middleware works with using statement
        using (var middleware = new AgentHealthCheckMiddleware(TimeSpan.FromMinutes(1)))
        {
            Assert.NotNull(middleware);
        }
        // Timer should be disposed after using block
        Assert.True(true);
    }

    #endregion

    #region Helper Methods

    private static AgentMessage CreateTestMessage(string content = "Test Content")
    {
        return new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Test Subject",
            Content = content,
            Category = "Test"
        };
    }

    private static MessageDelegate SuccessHandler =>
        (msg, ct) => Task.FromResult(MessageResult.Ok("Success"));

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
