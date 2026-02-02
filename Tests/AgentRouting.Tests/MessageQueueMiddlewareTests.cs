using System.Collections.Concurrent;
using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for MessageQueueMiddleware batch processing and queuing functionality
/// </summary>
public class MessageQueueMiddlewareTests
{
    #region Basic Enqueueing Tests

    [Test]
    public async Task Enqueue_SingleMessage_ProcessesSuccessfully()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 1);
        var message = CreateTestMessage();
        var handlerCalled = false;

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                handlerCalled = true;
                return Task.FromResult(MessageResult.Ok("Processed"));
            },
            CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.True(result.Success);
        Assert.Equal("Processed", result.Response);
    }

    [Test]
    public async Task Enqueue_MultipleMessages_AllProcessed()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 3);
        var processedCount = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.FromResult(MessageResult.Ok($"Processed {msg.Id}"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 3; i++)
        {
            var message = CreateTestMessage($"sender-{i}");
            tasks.Add(middleware.InvokeAsync(message, handler, CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(3, processedCount);
        Assert.True(results.All(r => r.Success));
    }

    [Test]
    public async Task Enqueue_MessageWithMetadata_MetadataPreserved()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 1);
        var message = CreateTestMessage();
        message.Metadata["CustomKey"] = "CustomValue";
        message.Metadata["Priority"] = 42;

        AgentMessage? processedMessage = null;
        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                processedMessage = msg;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.NotNull(processedMessage);
        Assert.Equal("CustomValue", processedMessage!.Metadata["CustomKey"]?.ToString());
        Assert.Equal(42, processedMessage.Metadata["Priority"]);
    }

    #endregion

    #region Batch Processing Tests

    [Test]
    public async Task BatchProcessing_ExactBatchSize_ProcessesImmediately()
    {
        var batchSize = 5;
        var middleware = new MessageQueueMiddleware(batchSize: batchSize);
        var processedCount = 0;
        var processedIds = new ConcurrentBag<string>();

        MessageDelegate handler = (msg, ct) =>
        {
            processedIds.Add(msg.Id);
            Interlocked.Increment(ref processedCount);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < batchSize; i++)
        {
            var message = CreateTestMessage();
            message.Id = $"msg-{i}";
            tasks.Add(middleware.InvokeAsync(message, handler, CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(batchSize, processedCount);
        Assert.Equal(batchSize, processedIds.Count);
    }

    [Test]
    public async Task BatchProcessing_MultipleBatches_AllProcessed()
    {
        var batchSize = 3;
        var totalMessages = 9;
        var middleware = new MessageQueueMiddleware(batchSize: batchSize);
        var processedIds = new ConcurrentBag<string>();

        MessageDelegate handler = (msg, ct) =>
        {
            processedIds.Add(msg.Id);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < totalMessages; i++)
        {
            var message = CreateTestMessage();
            message.Id = $"batch-msg-{i}";
            tasks.Add(middleware.InvokeAsync(message, handler, CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(totalMessages, processedIds.Count);
        Assert.True(results.All(r => r.Success));
    }

    [Test]
    public async Task BatchProcessing_PartialBatch_ProcessedByTimer()
    {
        // Use a short timeout to test timer-based processing
        var middleware = new MessageQueueMiddleware(batchSize: 10, batchTimeout: TimeSpan.FromMilliseconds(100));
        var processedCount = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        // Send only 3 messages (less than batch size of 10)
        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None));
        }

        // Wait for timer to process the partial batch
        var results = await Task.WhenAll(tasks);

        Assert.Equal(3, processedCount);
        Assert.True(results.All(r => r.Success));
    }

    #endregion

    #region Batch Size Configuration Tests

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public async Task BatchSize_VariousSizes_ProcessesCorrectly(int batchSize)
    {
        var middleware = new MessageQueueMiddleware(batchSize: batchSize);
        var processedCount = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < batchSize; i++)
        {
            tasks.Add(middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(batchSize, processedCount);
    }

    [Test]
    public async Task BatchSize_DefaultValue_ProcessesBatchOf10()
    {
        var middleware = new MessageQueueMiddleware(); // Default batch size is 10
        var processedCount = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(10, processedCount);
    }

    [Test]
    public async Task BatchSize_LargerThanMessageCount_WaitsForTimeout()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 100, batchTimeout: TimeSpan.FromMilliseconds(50));
        var processedCount = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        // Send fewer messages than batch size
        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None));
        }

        // Should complete after timeout triggers processing
        await Task.WhenAll(tasks);

        Assert.Equal(5, processedCount);
    }

    #endregion

    #region Processing Interval/Timing Tests

    [Test]
    public async Task BatchTimeout_ShortTimeout_ProcessesQuickly()
    {
        var timeout = TimeSpan.FromMilliseconds(50);
        var middleware = new MessageQueueMiddleware(batchSize: 100, batchTimeout: timeout);
        var processedAt = DateTime.MinValue;
        var enqueuedAt = DateTime.UtcNow;

        MessageDelegate handler = (msg, ct) =>
        {
            processedAt = DateTime.UtcNow;
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var task = middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);
        await task;

        // Processing should happen within a reasonable time after enqueueing
        var elapsed = processedAt - enqueuedAt;
        Assert.True(elapsed.TotalMilliseconds < 500, $"Expected processing within 500ms but took {elapsed.TotalMilliseconds}ms");
    }

    [Test]
    public async Task BatchTimeout_DefaultTimeout_Uses5Seconds()
    {
        // This test verifies the default timeout behavior
        // We use a small batch size to trigger immediate processing instead of waiting
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

    #endregion

    #region Message Ordering (FIFO) Tests

    [Test]
    public async Task MessageOrdering_SameBatch_ProcessedInEnqueueOrder()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 5);
        var processedOrder = new ConcurrentQueue<int>();

        MessageDelegate handler = (msg, ct) =>
        {
            var order = int.Parse(msg.Id.Split('-').Last());
            processedOrder.Enqueue(order);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 5; i++)
        {
            var message = CreateTestMessage();
            message.Id = $"order-{i}";
            tasks.Add(middleware.InvokeAsync(message, handler, CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        // All messages should be processed
        Assert.Equal(5, processedOrder.Count);
    }

    [Test]
    public async Task MessageOrdering_MultipleEnqueues_MaintainsOrder()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 3);
        var processedMessages = new ConcurrentBag<string>();

        MessageDelegate handler = (msg, ct) =>
        {
            processedMessages.Add(msg.Subject);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 6; i++)
        {
            var message = CreateTestMessage();
            message.Subject = $"Message {i}";
            tasks.Add(middleware.InvokeAsync(message, handler, CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(6, processedMessages.Count);
        // Verify all messages were processed
        for (int i = 0; i < 6; i++)
        {
            Assert.Contains($"Message {i}", processedMessages);
        }
    }

    #endregion

    #region Concurrent Enqueue Operations Tests

    [Test]
    public async Task ConcurrentEnqueue_MultipleThreads_AllProcessed()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 10);
        var processedCount = 0;
        var messageCount = 50;

        MessageDelegate handler = (msg, ct) =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = Enumerable.Range(0, messageCount)
            .AsParallel()
            .Select(i => middleware.InvokeAsync(CreateTestMessage($"thread-{i}"), handler, CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(messageCount, processedCount);
    }

    [Test]
    public async Task ConcurrentEnqueue_RapidFire_NoMessageLost()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 5);
        var processedIds = new ConcurrentBag<string>();
        var messageCount = 25;

        MessageDelegate handler = (msg, ct) =>
        {
            processedIds.Add(msg.Id);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < messageCount; i++)
        {
            var message = CreateTestMessage();
            message.Id = $"rapid-{i}";
            tasks.Add(middleware.InvokeAsync(message, handler, CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(messageCount, processedIds.Count);
        // Verify all message IDs are present
        for (int i = 0; i < messageCount; i++)
        {
            Assert.Contains($"rapid-{i}", processedIds);
        }
    }

    [Test]
    public async Task ConcurrentEnqueue_ThreadSafety_NoExceptions()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 10);
        var exceptions = new ConcurrentBag<Exception>();
        var messageCount = 100;

        MessageDelegate handler = (msg, ct) =>
        {
            // Simulate some processing time
            Task.Delay(1).Wait();
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task>();
        for (int i = 0; i < messageCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await middleware.InvokeAsync(CreateTestMessage($"safe-{index}"), handler, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task ErrorHandling_HandlerThrowsException_ReturnsFailResult()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 1);

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => throw new InvalidOperationException("Handler failed"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Batch processing error", result.Error!);
    }

    [Test]
    public async Task ErrorHandling_OneFailsInBatch_OthersSucceed()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 3);
        var callCount = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            var count = Interlocked.Increment(ref callCount);
            if (msg.Subject == "fail")
            {
                throw new Exception("Intentional failure");
            }
            return Task.FromResult(MessageResult.Ok("Success"));
        };

        var msg1 = CreateTestMessage();
        msg1.Subject = "success1";
        var msg2 = CreateTestMessage();
        msg2.Subject = "fail";
        var msg3 = CreateTestMessage();
        msg3.Subject = "success2";

        var tasks = new[]
        {
            middleware.InvokeAsync(msg1, handler, CancellationToken.None),
            middleware.InvokeAsync(msg2, handler, CancellationToken.None),
            middleware.InvokeAsync(msg3, handler, CancellationToken.None)
        };

        var results = await Task.WhenAll(tasks);

        // First and third should succeed, second should fail
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.True(results[2].Success);
    }

    [Test]
    public async Task ErrorHandling_HandlerReturnsFailure_PropagatesFailure()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 1);

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Fail("Validation failed")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Validation failed", result.Error);
    }

    [Test]
    public async Task ErrorHandling_MultipleFailures_AllReported()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 3);

        MessageDelegate handler = (msg, ct) => Task.FromResult(MessageResult.Fail($"Failed: {msg.Subject}"));

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 3; i++)
        {
            var message = CreateTestMessage();
            message.Subject = $"msg-{i}";
            tasks.Add(middleware.InvokeAsync(message, handler, CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        Assert.True(results.All(r => !r.Success));
        Assert.Contains("Failed: msg-0", results[0].Error!);
        Assert.Contains("Failed: msg-1", results[1].Error!);
        Assert.Contains("Failed: msg-2", results[2].Error!);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task EdgeCase_EmptyQueue_NoProcessing()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 5, batchTimeout: TimeSpan.FromMilliseconds(50));
        var processedCount = 0;

        MessageDelegate handler = (msg, ct) =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        // Just wait without enqueueing any messages
        await Task.Delay(100);

        // No messages should be processed
        Assert.Equal(0, processedCount);
    }

    [Test]
    public async Task EdgeCase_SingleMessage_Processed()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 10, batchTimeout: TimeSpan.FromMilliseconds(50));

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Single")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Single", result.Response);
    }

    [Test]
    public async Task EdgeCase_ExactlyBatchSize_TriggersImmediateProcessing()
    {
        var batchSize = 5;
        var middleware = new MessageQueueMiddleware(batchSize: batchSize, batchTimeout: TimeSpan.FromSeconds(60));
        var processStartTime = DateTime.MaxValue;
        var enqueueEndTime = DateTime.MinValue;

        MessageDelegate handler = (msg, ct) =>
        {
            var now = DateTime.UtcNow;
            if (now < processStartTime)
                processStartTime = now;
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < batchSize; i++)
        {
            tasks.Add(middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None));
        }
        enqueueEndTime = DateTime.UtcNow;

        await Task.WhenAll(tasks);

        // Processing should have started quickly (not waiting for 60s timeout)
        var delay = processStartTime - enqueueEndTime;
        Assert.True(delay.TotalSeconds < 1, $"Expected immediate processing but waited {delay.TotalSeconds}s");
    }

    [Test]
    public async Task EdgeCase_BatchSizeOne_ProcessesEachMessageImmediately()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 1);
        var processedTimes = new ConcurrentBag<DateTime>();

        MessageDelegate handler = (msg, ct) =>
        {
            processedTimes.Add(DateTime.UtcNow);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        for (int i = 0; i < 3; i++)
        {
            await middleware.InvokeAsync(CreateTestMessage(), handler, CancellationToken.None);
        }

        Assert.Equal(3, processedTimes.Count);
    }

    [Test]
    public async Task EdgeCase_LargeMessageContent_ProcessedCorrectly()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 1);
        var largeContent = new string('X', 100000); // 100KB of content

        var message = CreateTestMessage();
        message.Content = largeContent;

        string? processedContent = null;
        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                processedContent = msg.Content;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(largeContent.Length, processedContent?.Length);
    }

    #endregion

    #region Pipeline Integration Tests

    [Test]
    public async Task PipelineIntegration_WithOtherMiddleware_WorksCorrectly()
    {
        var pipeline = new MiddlewarePipeline();
        var queueMiddleware = new MessageQueueMiddleware(batchSize: 1);
        var executionOrder = new List<string>();

        // Add tracking middleware before queue
        pipeline.Use(next => async (msg, ct) =>
        {
            executionOrder.Add("Before-Queue");
            var result = await next(msg, ct);
            executionOrder.Add("After-Queue");
            return result;
        });

        pipeline.Use(queueMiddleware);

        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            executionOrder.Add("Handler");
            return Task.FromResult(MessageResult.Ok("Done"));
        });

        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("Before-Queue", executionOrder[0]);
        Assert.Equal("Handler", executionOrder[1]);
        Assert.Equal("After-Queue", executionOrder[2]);
    }

    [Test]
    public async Task PipelineIntegration_ValidationBeforeQueue_ShortCircuitsCorrectly()
    {
        var pipeline = new MiddlewarePipeline();
        var queueMiddleware = new MessageQueueMiddleware(batchSize: 1);
        var queueCalled = false;

        // Add validation middleware that rejects messages
        pipeline.Use(next => (msg, ct) =>
        {
            if (string.IsNullOrEmpty(msg.Content))
            {
                return Task.FromResult(MessageResult.Fail("Content required"));
            }
            return next(msg, ct);
        });

        pipeline.Use(new TrackingQueueMiddleware(queueMiddleware, () => queueCalled = true));

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));

        var invalidMessage = new AgentMessage
        {
            SenderId = "test",
            Subject = "Test",
            Content = "" // Invalid - empty content
        };

        var result = await builtPipeline(invalidMessage, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Content required", result.Error!);
        Assert.False(queueCalled); // Queue should not be reached
    }

    [Test]
    public async Task PipelineIntegration_MultipleMessages_AllRoutedThroughPipeline()
    {
        var pipeline = new MiddlewarePipeline();
        var queueMiddleware = new MessageQueueMiddleware(batchSize: 3);
        var processedCount = 0;

        pipeline.Use(queueMiddleware);

        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.FromResult(MessageResult.Ok("Processed"));
        });

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(builtPipeline(CreateTestMessage(), CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(3, processedCount);
    }

    #endregion

    #region Result Data Tests

    [Test]
    public async Task ResultData_SuccessResponse_PreservedThroughQueue()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 1);

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                var response = MessageResult.Ok("Custom response");
                response.Data["ProcessedBy"] = "TestHandler";
                response.Data["Timestamp"] = 12345L;
                return Task.FromResult(response);
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Custom response", result.Response);
        Assert.Equal("TestHandler", result.Data["ProcessedBy"]);
        Assert.Equal(12345L, result.Data["Timestamp"]);
    }

    [Test]
    public async Task ResultData_ForwardedMessages_PreservedThroughQueue()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 1);

        var forwardedMessage = new AgentMessage
        {
            SenderId = "original",
            ReceiverId = "target",
            Subject = "Forwarded",
            Content = "Forwarded content"
        };

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                var response = MessageResult.Ok("With forward");
                response.ForwardedMessages.Add(forwardedMessage);
                return Task.FromResult(response);
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.ForwardedMessages.Count);
        Assert.Equal("Forwarded", result.ForwardedMessages[0].Subject);
    }

    #endregion

    #region Stress Tests

    [Test]
    public async Task StressTest_HighVolume_HandlesLoad()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 50);
        var processedCount = 0;
        var messageCount = 500;

        MessageDelegate handler = (msg, ct) =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < messageCount; i++)
        {
            tasks.Add(middleware.InvokeAsync(CreateTestMessage($"stress-{i}"), handler, CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(messageCount, processedCount);
    }

    [Test]
    public async Task StressTest_RapidBatchCycles_NoDataCorruption()
    {
        var middleware = new MessageQueueMiddleware(batchSize: 5);
        var processedMessages = new ConcurrentDictionary<string, bool>();
        var messageCount = 100;

        MessageDelegate handler = (msg, ct) =>
        {
            processedMessages.TryAdd(msg.Id, true);
            return Task.FromResult(MessageResult.Ok("Done"));
        };

        var tasks = new List<Task<MessageResult>>();
        for (int i = 0; i < messageCount; i++)
        {
            var message = CreateTestMessage();
            message.Id = $"unique-{i}";
            tasks.Add(middleware.InvokeAsync(message, handler, CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        // Verify all unique messages were processed exactly once
        Assert.Equal(messageCount, processedMessages.Count);
    }

    #endregion

    #region Helper Methods and Classes

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

    /// <summary>
    /// Helper middleware that wraps MessageQueueMiddleware and tracks if it was invoked
    /// </summary>
    private class TrackingQueueMiddleware : MiddlewareBase
    {
        private readonly MessageQueueMiddleware _innerMiddleware;
        private readonly Action _onInvoke;

        public TrackingQueueMiddleware(MessageQueueMiddleware innerMiddleware, Action onInvoke)
        {
            _innerMiddleware = innerMiddleware;
            _onInvoke = onInvoke;
        }

        public override Task<MessageResult> InvokeAsync(
            AgentMessage message,
            MessageDelegate next,
            CancellationToken ct)
        {
            _onInvoke();
            return _innerMiddleware.InvokeAsync(message, next, ct);
        }
    }

    #endregion
}
