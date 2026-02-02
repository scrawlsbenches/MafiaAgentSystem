using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;

namespace TestRunner.Tests;

/// <summary>
/// Tests for WorkflowOrchestrationMiddleware - multi-stage workflow orchestration.
/// </summary>
public class WorkflowOrchestrationMiddlewareTests
{
    #region Helper Methods

    private static AgentMessage CreateTestMessage(string senderId = "test-sender", string receiverId = "test-receiver")
    {
        return new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = senderId,
            ReceiverId = receiverId,
            Subject = "Test Subject",
            Content = "Test Content",
            Category = "Test",
            Priority = MessagePriority.Normal,
            ConversationId = Guid.NewGuid().ToString()
        };
    }

    private static AgentMessage CreateWorkflowMessage(string workflowId, int stageIndex = 0)
    {
        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = workflowId;
        message.Metadata["StageIndex"] = stageIndex;
        return message;
    }

    private static MessageDelegate CreateSuccessHandler(string response = "Success")
    {
        return (msg, ct) => Task.FromResult(MessageResult.Ok(response));
    }

    private static MessageDelegate CreateFailureHandler(string error = "Failed")
    {
        return (msg, ct) => Task.FromResult(MessageResult.Fail(error));
    }

    private static MessageDelegate CreateTrackingHandler(List<string> tracker)
    {
        return (msg, ct) =>
        {
            tracker.Add($"Handler called for: {msg.Subject}");
            return Task.FromResult(MessageResult.Ok("Tracked"));
        };
    }

    #endregion

    #region Workflow Registration Tests

    [Test]
    public async Task RegisterWorkflow_SingleStage_CanBeExecuted()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("simple-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" });

        var message = CreateWorkflowMessage("simple-workflow");
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Test]
    public async Task RegisterWorkflow_MultipleStages_AllRegistered()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("multi-stage-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" },
            new WorkflowStage { Name = "Stage3", AgentId = "agent-3" });

        var message = CreateWorkflowMessage("multi-stage-workflow");
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        Assert.True(result.Success);
        // Should have forwarded message for stage 2
        Assert.Equal(1, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task RegisterWorkflow_MultipleWorkflows_IndependentlyTracked()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("workflow-a",
            new WorkflowStage { Name = "A1", AgentId = "agent-a1" },
            new WorkflowStage { Name = "A2", AgentId = "agent-a2" });
        middleware.RegisterWorkflow("workflow-b",
            new WorkflowStage { Name = "B1", AgentId = "agent-b1" });

        var messageA = CreateWorkflowMessage("workflow-a");
        var messageB = CreateWorkflowMessage("workflow-b");

        var resultA = await middleware.InvokeAsync(messageA, CreateSuccessHandler(), CancellationToken.None);
        var resultB = await middleware.InvokeAsync(messageB, CreateSuccessHandler(), CancellationToken.None);

        // Workflow A should have forwarded message (has 2 stages)
        Assert.Equal(1, resultA.ForwardedMessages.Count);
        // Workflow B should have no forwarded message (only 1 stage)
        Assert.Equal(0, resultB.ForwardedMessages.Count);
    }

    #endregion

    #region Non-Workflow Message Tests

    [Test]
    public async Task InvokeAsync_MessageWithoutWorkflowId_PassesThrough()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" });

        var message = CreateTestMessage(); // No WorkflowId
        var handlerCalled = false;

        var result = await middleware.InvokeAsync(message, (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("Passed through"));
        }, CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.True(result.Success);
        Assert.Equal("Passed through", result.Response);
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task InvokeAsync_UnknownWorkflowId_PassesThrough()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("known-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" });

        var message = CreateWorkflowMessage("unknown-workflow");
        var handlerCalled = false;

        var result = await middleware.InvokeAsync(message, (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("Passed through"));
        }, CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.True(result.Success);
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task InvokeAsync_EmptyWorkflowId_PassesThrough()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" });

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "";

        var handlerCalled = false;
        var result = await middleware.InvokeAsync(message, (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("OK"));
        }, CancellationToken.None);

        Assert.True(handlerCalled);
    }

    [Test]
    public async Task InvokeAsync_NonStringWorkflowId_PassesThrough()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" });

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = 12345; // Not a string

        var handlerCalled = false;
        var result = await middleware.InvokeAsync(message, (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("OK"));
        }, CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    #endregion

    #region Stage Progression Tests

    [Test]
    public async Task InvokeAsync_FirstStage_ProcessesCorrectly()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("test-workflow", stageIndex: 0);
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler("Stage 1 complete"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task InvokeAsync_MiddleStage_ContinuesToNextStage()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" },
            new WorkflowStage { Name = "Stage3", AgentId = "agent-3" });

        var message = CreateWorkflowMessage("test-workflow", stageIndex: 1);
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.ForwardedMessages.Count);

        var forwardedMessage = result.ForwardedMessages[0];
        Assert.Equal(2, forwardedMessage.Metadata["StageIndex"]);
    }

    [Test]
    public async Task InvokeAsync_FinalStage_NoForwardedMessage()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        // Stage index 1 is the last stage (0-indexed)
        var message = CreateWorkflowMessage("test-workflow", stageIndex: 1);
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler("Workflow complete"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task InvokeAsync_SingleStageWorkflow_CompletesWithoutForwarding()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("single-stage",
            new WorkflowStage { Name = "OnlyStage", AgentId = "agent-1" });

        var message = CreateWorkflowMessage("single-stage", stageIndex: 0);
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task InvokeAsync_StageIndexExceedsCount_PassesThrough()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" });

        // Stage index 5 is way beyond the single stage
        var message = CreateWorkflowMessage("test-workflow", stageIndex: 5);
        var handlerCalled = false;

        var result = await middleware.InvokeAsync(message, (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("OK"));
        }, CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task InvokeAsync_MissingStageIndex_DefaultsToZero()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "test-workflow";
        // No StageIndex set

        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.ForwardedMessages.Count);

        // Next stage should be 1
        var forwardedMessage = result.ForwardedMessages[0];
        Assert.Equal(1, forwardedMessage.Metadata["StageIndex"]);
    }

    #endregion

    #region Forwarded Message Content Tests

    [Test]
    public async Task ForwardedMessage_HasCorrectWorkflowId()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("my-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("my-workflow");
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        var forwardedMessage = result.ForwardedMessages[0];
        Assert.Equal("my-workflow", forwardedMessage.Metadata["WorkflowId"]);
    }

    [Test]
    public async Task ForwardedMessage_HasIncrementedStageIndex()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" },
            new WorkflowStage { Name = "Stage3", AgentId = "agent-3" });

        var message = CreateWorkflowMessage("test-workflow", stageIndex: 0);
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        Assert.Equal(1, result.ForwardedMessages[0].Metadata["StageIndex"]);
    }

    [Test]
    public async Task ForwardedMessage_HasCorrectReceiverId()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "special-agent" });

        var message = CreateWorkflowMessage("test-workflow");
        message.ReceiverId = "agent-1";

        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        var forwardedMessage = result.ForwardedMessages[0];
        Assert.Equal("special-agent", forwardedMessage.ReceiverId);
    }

    [Test]
    public async Task ForwardedMessage_SenderIdIsOriginalReceiverId()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("test-workflow");
        message.ReceiverId = "original-receiver";

        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        var forwardedMessage = result.ForwardedMessages[0];
        Assert.Equal("original-receiver", forwardedMessage.SenderId);
    }

    [Test]
    public async Task ForwardedMessage_PreservesConversationId()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("test-workflow");
        message.ConversationId = "conversation-123";

        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        var forwardedMessage = result.ForwardedMessages[0];
        Assert.Equal("conversation-123", forwardedMessage.ConversationId);
    }

    [Test]
    public async Task ForwardedMessage_HasCorrectSubject()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("my-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("my-workflow", stageIndex: 0);
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        var forwardedMessage = result.ForwardedMessages[0];
        Assert.Contains("Workflow my-workflow", forwardedMessage.Subject);
        Assert.Contains("Stage 2", forwardedMessage.Subject);
    }

    [Test]
    public async Task ForwardedMessage_ContentIsResultResponse()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("test-workflow");
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler("Stage 1 output data"), CancellationToken.None);

        var forwardedMessage = result.ForwardedMessages[0];
        Assert.Equal("Stage 1 output data", forwardedMessage.Content);
    }

    [Test]
    public async Task ForwardedMessage_ContentFallsBackToOriginal_WhenResponseNull()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("test-workflow");
        message.Content = "Original content";

        // Handler returns null response
        var result = await middleware.InvokeAsync(message, (msg, ct) =>
            Task.FromResult(MessageResult.Ok(null)), CancellationToken.None);

        var forwardedMessage = result.ForwardedMessages[0];
        Assert.Equal("Original content", forwardedMessage.Content);
    }

    [Test]
    public async Task ForwardedMessage_PreservesOtherMetadata()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("test-workflow");
        message.Metadata["CustomKey"] = "CustomValue";
        message.Metadata["AnotherKey"] = 42;

        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        var forwardedMessage = result.ForwardedMessages[0];
        Assert.Equal("CustomValue", forwardedMessage.Metadata["CustomKey"]);
        Assert.Equal(42, forwardedMessage.Metadata["AnotherKey"]);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task InvokeAsync_StageFailure_NoForwardedMessage()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("test-workflow");
        var result = await middleware.InvokeAsync(message, CreateFailureHandler("Stage failed"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task InvokeAsync_StageFailure_PreservesErrorMessage()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("test-workflow");
        var result = await middleware.InvokeAsync(message, CreateFailureHandler("Specific error message"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Specific error message", result.Error);
    }

    [Test]
    public async Task InvokeAsync_ExceptionInHandler_Propagates()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("test-workflow");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await middleware.InvokeAsync(message, (msg, ct) =>
            {
                throw new InvalidOperationException("Handler exception");
            }, CancellationToken.None);
        });
    }

    [Test]
    public async Task InvokeAsync_IntermediateStageFailure_StopsProgression()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" },
            new WorkflowStage { Name = "Stage3", AgentId = "agent-3" });

        // Start at stage 1 (middle)
        var message = CreateWorkflowMessage("test-workflow", stageIndex: 1);
        var result = await middleware.InvokeAsync(message, CreateFailureHandler("Middle stage failed"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    #endregion

    #region Multiple Concurrent Workflows Tests

    [Test]
    public async Task InvokeAsync_ConcurrentWorkflowInstances_IndependentProgress()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" },
            new WorkflowStage { Name = "Stage3", AgentId = "agent-3" });

        // Instance 1 at stage 0
        var message1 = CreateWorkflowMessage("test-workflow", stageIndex: 0);
        message1.ConversationId = "instance-1";

        // Instance 2 at stage 1
        var message2 = CreateWorkflowMessage("test-workflow", stageIndex: 1);
        message2.ConversationId = "instance-2";

        var result1 = await middleware.InvokeAsync(message1, CreateSuccessHandler(), CancellationToken.None);
        var result2 = await middleware.InvokeAsync(message2, CreateSuccessHandler(), CancellationToken.None);

        // Both should progress independently
        Assert.Equal(1, result1.ForwardedMessages.Count);
        Assert.Equal(1, result2.ForwardedMessages.Count);

        // Instance 1 should go to stage 1
        Assert.Equal(1, result1.ForwardedMessages[0].Metadata["StageIndex"]);
        // Instance 2 should go to stage 2
        Assert.Equal(2, result2.ForwardedMessages[0].Metadata["StageIndex"]);
    }

    [Test]
    public async Task InvokeAsync_DifferentWorkflowsConcurrently_ProcessIndependently()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("workflow-a",
            new WorkflowStage { Name = "A1", AgentId = "agent-a1" },
            new WorkflowStage { Name = "A2", AgentId = "agent-a2" });
        middleware.RegisterWorkflow("workflow-b",
            new WorkflowStage { Name = "B1", AgentId = "agent-b1" },
            new WorkflowStage { Name = "B2", AgentId = "agent-b2" },
            new WorkflowStage { Name = "B3", AgentId = "agent-b3" });

        var messageA = CreateWorkflowMessage("workflow-a", stageIndex: 0);
        var messageB = CreateWorkflowMessage("workflow-b", stageIndex: 1);

        var resultA = await middleware.InvokeAsync(messageA, CreateSuccessHandler(), CancellationToken.None);
        var resultB = await middleware.InvokeAsync(messageB, CreateSuccessHandler(), CancellationToken.None);

        // Workflow A progresses to A2
        Assert.Equal(1, resultA.ForwardedMessages.Count);
        Assert.Equal("agent-a2", resultA.ForwardedMessages[0].ReceiverId);

        // Workflow B progresses to B3
        Assert.Equal(1, resultB.ForwardedMessages.Count);
        Assert.Equal("agent-b3", resultB.ForwardedMessages[0].ReceiverId);
    }

    [Test]
    public async Task InvokeAsync_ParallelExecution_ThreadSafe()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("parallel-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 10; i++)
        {
            var message = CreateWorkflowMessage("parallel-workflow", stageIndex: 0);
            message.ConversationId = $"parallel-instance-{i}";
            tasks.Add(middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        // All should succeed
        Assert.True(results.All(r => r.Success));
        // All should have forwarded messages
        Assert.True(results.All(r => r.ForwardedMessages.Count == 1));
    }

    #endregion

    #region Workflow State Tests

    [Test]
    public async Task InvokeAsync_StateInMetadata_ProgressesThroughAllStages()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("full-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" },
            new WorkflowStage { Name = "Stage3", AgentId = "agent-3" });

        // Simulate full workflow execution
        var message = CreateWorkflowMessage("full-workflow", stageIndex: 0);
        var result1 = await middleware.InvokeAsync(message, CreateSuccessHandler("Output 1"), CancellationToken.None);

        Assert.True(result1.Success);
        Assert.Equal(1, result1.ForwardedMessages.Count);

        // Continue with forwarded message
        var message2 = result1.ForwardedMessages[0];
        var result2 = await middleware.InvokeAsync(message2, CreateSuccessHandler("Output 2"), CancellationToken.None);

        Assert.True(result2.Success);
        Assert.Equal(1, result2.ForwardedMessages.Count);

        // Continue with final forwarded message
        var message3 = result2.ForwardedMessages[0];
        var result3 = await middleware.InvokeAsync(message3, CreateSuccessHandler("Final output"), CancellationToken.None);

        Assert.True(result3.Success);
        Assert.Equal(0, result3.ForwardedMessages.Count); // No more stages
    }

    [Test]
    public async Task InvokeAsync_DataFlowsThroughStages()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("data-flow-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" },
            new WorkflowStage { Name = "Stage3", AgentId = "agent-3" });

        var message = CreateWorkflowMessage("data-flow-workflow");
        message.Content = "Initial data";

        // Stage 1: Transform data
        var result1 = await middleware.InvokeAsync(message, (msg, ct) =>
            Task.FromResult(MessageResult.Ok($"Processed: {msg.Content}")), CancellationToken.None);

        Assert.Equal("Processed: Initial data", result1.ForwardedMessages[0].Content);

        // Stage 2: Further transform
        var result2 = await middleware.InvokeAsync(result1.ForwardedMessages[0], (msg, ct) =>
            Task.FromResult(MessageResult.Ok($"Enhanced: {msg.Content}")), CancellationToken.None);

        Assert.Equal("Enhanced: Processed: Initial data", result2.ForwardedMessages[0].Content);
    }

    #endregion

    #region Edge Cases Tests

    [Test]
    public async Task InvokeAsync_NoWorkflowsRegistered_PassesThrough()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        // No workflows registered

        var message = CreateWorkflowMessage("any-workflow");
        var handlerCalled = false;

        var result = await middleware.InvokeAsync(message, (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("OK"));
        }, CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task InvokeAsync_NegativeStageIndex_PassesThrough()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" });

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "test-workflow";
        message.Metadata["StageIndex"] = -1;

        var handlerCalled = false;
        var result = await middleware.InvokeAsync(message, (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("OK"));
        }, CancellationToken.None);

        Assert.True(handlerCalled);
    }

    [Test]
    public async Task InvokeAsync_LargeStageIndex_PassesThrough()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" });

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "test-workflow";
        message.Metadata["StageIndex"] = int.MaxValue;

        var handlerCalled = false;
        var result = await middleware.InvokeAsync(message, (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("OK"));
        }, CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task InvokeAsync_WorkflowIdCaseSensitive()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("TestWorkflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        // Different case - should not match
        var message = CreateWorkflowMessage("testworkflow"); // lowercase
        var handlerCalled = false;

        var result = await middleware.InvokeAsync(message, (msg, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(MessageResult.Ok("OK"));
        }, CancellationToken.None);

        Assert.True(handlerCalled);
        // Should pass through without workflow processing (no forwarded messages for unknown workflow)
        Assert.Equal(0, result.ForwardedMessages.Count);
    }

    [Test]
    public async Task RegisterWorkflow_OverwritesExistingWorkflow()
    {
        var middleware = new WorkflowOrchestrationMiddleware();

        // Register initial workflow with 2 stages
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        // Overwrite with 3 stages
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "New1", AgentId = "new-agent-1" },
            new WorkflowStage { Name = "New2", AgentId = "new-agent-2" },
            new WorkflowStage { Name = "New3", AgentId = "new-agent-3" });

        var message = CreateWorkflowMessage("test-workflow", stageIndex: 0);
        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.ForwardedMessages.Count);
        // Should use the new workflow's agent
        Assert.Equal("new-agent-2", result.ForwardedMessages[0].ReceiverId);
    }

    [Test]
    public async Task InvokeAsync_StageIndexAsDouble_Converts()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" },
            new WorkflowStage { Name = "Stage3", AgentId = "agent-3" });

        var message = CreateTestMessage();
        message.Metadata["WorkflowId"] = "test-workflow";
        message.Metadata["StageIndex"] = 1.0; // Double instead of int

        var result = await middleware.InvokeAsync(message, CreateSuccessHandler(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.ForwardedMessages.Count);
        // Should convert and progress to stage 2
        Assert.Equal(2, result.ForwardedMessages[0].Metadata["StageIndex"]);
    }

    #endregion

    #region Cancellation Tests

    [Test]
    public async Task InvokeAsync_CancellationRequested_PropagatedToHandler()
    {
        var middleware = new WorkflowOrchestrationMiddleware();
        middleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });

        var message = CreateWorkflowMessage("test-workflow");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await middleware.InvokeAsync(message, (msg, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(MessageResult.Ok("OK"));
            }, cts.Token);
        });
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task Pipeline_WithWorkflowMiddleware_ProcessesCorrectly()
    {
        var pipeline = new MiddlewarePipeline();
        var workflowMiddleware = new WorkflowOrchestrationMiddleware();
        workflowMiddleware.RegisterWorkflow("approval-workflow",
            new WorkflowStage { Name = "Review", AgentId = "reviewer" },
            new WorkflowStage { Name = "Approve", AgentId = "approver" },
            new WorkflowStage { Name = "Notify", AgentId = "notifier" });

        pipeline.Use(workflowMiddleware);

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Processed")));

        var message = CreateWorkflowMessage("approval-workflow");
        var result = await builtPipeline(message, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.ForwardedMessages.Count);
        Assert.Equal("approver", result.ForwardedMessages[0].ReceiverId);
    }

    [Test]
    public async Task Pipeline_MultipleMiddlewareWithWorkflow_ExecutesInOrder()
    {
        var executionOrder = new List<string>();
        var pipeline = new MiddlewarePipeline();

        // Add tracking middleware before
        pipeline.Use(next => async (msg, ct) =>
        {
            executionOrder.Add("Before-Workflow");
            var result = await next(msg, ct);
            executionOrder.Add("After-Workflow");
            return result;
        });

        var workflowMiddleware = new WorkflowOrchestrationMiddleware();
        workflowMiddleware.RegisterWorkflow("test-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });
        pipeline.Use(workflowMiddleware);

        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            executionOrder.Add("Handler");
            return Task.FromResult(MessageResult.Ok("Done"));
        });

        var message = CreateWorkflowMessage("test-workflow");
        await builtPipeline(message, CancellationToken.None);

        Assert.Equal("Before-Workflow", executionOrder[0]);
        Assert.Equal("Handler", executionOrder[1]);
        Assert.Equal("After-Workflow", executionOrder[2]);
    }

    [Test]
    public async Task Pipeline_WorkflowWithValidation_BothExecute()
    {
        var pipeline = new MiddlewarePipeline();

        // Validation middleware
        pipeline.Use(new ValidationMiddleware());

        // Workflow middleware
        var workflowMiddleware = new WorkflowOrchestrationMiddleware();
        workflowMiddleware.RegisterWorkflow("validated-workflow",
            new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
            new WorkflowStage { Name = "Stage2", AgentId = "agent-2" });
        pipeline.Use(workflowMiddleware);

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Success")));

        var message = CreateWorkflowMessage("validated-workflow");
        message.SenderId = "sender";
        message.Subject = "Subject";
        message.Content = "Content";

        var result = await builtPipeline(message, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.ForwardedMessages.Count);
    }

    #endregion

    #region WorkflowStage Tests

    [Test]
    public void WorkflowStage_DefaultValues_AreEmpty()
    {
        var stage = new WorkflowStage();

        Assert.Equal("", stage.Name);
        Assert.Equal("", stage.AgentId);
        Assert.Null(stage.Condition);
    }

    [Test]
    public void WorkflowStage_CanSetProperties()
    {
        var condition = new Func<AgentMessage, bool>(msg => msg.Priority == MessagePriority.High);
        var stage = new WorkflowStage
        {
            Name = "Approval Stage",
            AgentId = "approval-agent",
            Condition = condition
        };

        Assert.Equal("Approval Stage", stage.Name);
        Assert.Equal("approval-agent", stage.AgentId);
        Assert.NotNull(stage.Condition);
    }

    #endregion

    #region WorkflowDefinition Tests

    [Test]
    public void WorkflowDefinition_DefaultValues_AreEmpty()
    {
        var definition = new WorkflowDefinition();

        Assert.Equal("", definition.WorkflowId);
        Assert.NotNull(definition.Stages);
        Assert.Equal(0, definition.Stages.Count);
    }

    [Test]
    public void WorkflowDefinition_CanAddStages()
    {
        var definition = new WorkflowDefinition
        {
            WorkflowId = "my-workflow",
            Stages = new List<WorkflowStage>
            {
                new WorkflowStage { Name = "Stage1", AgentId = "agent-1" },
                new WorkflowStage { Name = "Stage2", AgentId = "agent-2" }
            }
        };

        Assert.Equal("my-workflow", definition.WorkflowId);
        Assert.Equal(2, definition.Stages.Count);
        Assert.Equal("Stage1", definition.Stages[0].Name);
        Assert.Equal("Stage2", definition.Stages[1].Name);
    }

    #endregion
}
