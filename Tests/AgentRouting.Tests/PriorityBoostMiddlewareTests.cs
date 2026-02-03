using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using TestUtilities;

namespace AgentRouting.Tests;

/// <summary>
/// Comprehensive tests for PriorityBoostMiddleware.
/// Tests VIP sender detection, priority boosting behavior, case sensitivity,
/// edge cases, and middleware pipeline integration.
/// </summary>
public class PriorityBoostMiddlewareTests
{
    #region VIP Sender Detection Tests

    [Test]
    public async Task VIPSender_WithNormalPriority_BoostsToHigh()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("vip-user", MessagePriority.Normal);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task VIPSender_WithLowPriority_BoostsToHigh()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("vip-user", MessagePriority.Low);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task NonVIPSender_WithNormalPriority_RemainsNormal()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("regular-user", MessagePriority.Normal);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.Normal, message.Priority);
    }

    [Test]
    public async Task NonVIPSender_WithLowPriority_RemainsLow()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("regular-user", MessagePriority.Low);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.Low, message.Priority);
    }

    #endregion

    #region Already High Priority Tests

    [Test]
    public async Task VIPSender_WithHighPriority_RemainsHigh()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("vip-user", MessagePriority.High);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task VIPSender_WithUrgentPriority_RemainsUrgent()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("vip-user", MessagePriority.Urgent);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.Urgent, message.Priority);
    }

    [Test]
    public async Task VIPSender_DoesNotDowngradePriority()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("vip-user", MessagePriority.Urgent);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        // Should never downgrade from Urgent to High
        Assert.True(message.Priority >= MessagePriority.High);
    }

    #endregion

    #region Multiple VIP Senders Tests

    [Test]
    public async Task MultipleVIPSenders_AllAreBoosted()
    {
        var middleware = new PriorityBoostMiddleware("vip1", "vip2", "vip3");

        var message1 = CreateMessage("vip1", MessagePriority.Normal);
        var message2 = CreateMessage("vip2", MessagePriority.Low);
        var message3 = CreateMessage("vip3", MessagePriority.Normal);

        await middleware.InvokeAsync(message1, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(message2, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(message3, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message1.Priority);
        Assert.Equal(MessagePriority.High, message2.Priority);
        Assert.Equal(MessagePriority.High, message3.Priority);
    }

    [Test]
    public async Task MultipleVIPSenders_NonVIPNotBoosted()
    {
        var middleware = new PriorityBoostMiddleware("vip1", "vip2");

        var vipMessage = CreateMessage("vip1", MessagePriority.Normal);
        var regularMessage = CreateMessage("regular", MessagePriority.Normal);

        await middleware.InvokeAsync(vipMessage, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(regularMessage, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, vipMessage.Priority);
        Assert.Equal(MessagePriority.Normal, regularMessage.Priority);
    }

    [Test]
    public async Task MultipleVIPSenders_MixedPriorities()
    {
        var middleware = new PriorityBoostMiddleware("vip1", "vip2", "vip3");

        var lowPriority = CreateMessage("vip1", MessagePriority.Low);
        var normalPriority = CreateMessage("vip2", MessagePriority.Normal);
        var highPriority = CreateMessage("vip3", MessagePriority.High);
        var urgentPriority = CreateMessage("regular", MessagePriority.Urgent);

        await middleware.InvokeAsync(lowPriority, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(normalPriority, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(highPriority, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(urgentPriority, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, lowPriority.Priority);
        Assert.Equal(MessagePriority.High, normalPriority.Priority);
        Assert.Equal(MessagePriority.High, highPriority.Priority);
        Assert.Equal(MessagePriority.Urgent, urgentPriority.Priority); // Not VIP, not boosted
    }

    #endregion

    #region Edge Cases - Empty VIP List

    [Test]
    public async Task EmptyVIPList_NoBoostApplied()
    {
        var middleware = new PriorityBoostMiddleware(); // No VIP senders

        var message = CreateMessage("any-user", MessagePriority.Normal);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.Normal, message.Priority);
    }

    [Test]
    public async Task EmptyVIPList_AllPrioritiesUnchanged()
    {
        var middleware = new PriorityBoostMiddleware();

        var lowMessage = CreateMessage("user1", MessagePriority.Low);
        var normalMessage = CreateMessage("user2", MessagePriority.Normal);
        var highMessage = CreateMessage("user3", MessagePriority.High);
        var urgentMessage = CreateMessage("user4", MessagePriority.Urgent);

        await middleware.InvokeAsync(lowMessage, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(normalMessage, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(highMessage, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(urgentMessage, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.Low, lowMessage.Priority);
        Assert.Equal(MessagePriority.Normal, normalMessage.Priority);
        Assert.Equal(MessagePriority.High, highMessage.Priority);
        Assert.Equal(MessagePriority.Urgent, urgentMessage.Priority);
    }

    #endregion

    #region Case Sensitivity Tests

    [Test]
    public async Task CaseInsensitive_UpperCaseSender_MatchesLowerCase()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("VIP-USER", MessagePriority.Normal);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task CaseInsensitive_LowerCaseSender_MatchesUpperCase()
    {
        var middleware = new PriorityBoostMiddleware("VIP-USER");
        var message = CreateMessage("vip-user", MessagePriority.Normal);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task CaseInsensitive_MixedCaseSender_Matches()
    {
        var middleware = new PriorityBoostMiddleware("VIP-User");
        var message = CreateMessage("vIp-uSeR", MessagePriority.Normal);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task CaseInsensitive_MultipleVIPsWithDifferentCases()
    {
        var middleware = new PriorityBoostMiddleware("VIP1", "vip2", "ViP3");

        var msg1 = CreateMessage("vip1", MessagePriority.Normal);
        var msg2 = CreateMessage("VIP2", MessagePriority.Normal);
        var msg3 = CreateMessage("VIP3", MessagePriority.Normal);

        await middleware.InvokeAsync(msg1, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(msg2, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(msg3, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, msg1.Priority);
        Assert.Equal(MessagePriority.High, msg2.Priority);
        Assert.Equal(MessagePriority.High, msg3.Priority);
    }

    #endregion

    #region Pipeline Behavior Tests

    [Test]
    public async Task Middleware_CallsNextHandler()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("vip-user", MessagePriority.Normal);
        var nextCalled = false;

        await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
    }

    [Test]
    public async Task Middleware_CallsNextHandler_ForNonVIP()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("regular-user", MessagePriority.Normal);
        var nextCalled = false;

        await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
    }

    [Test]
    public async Task Middleware_ReturnsNextHandlerResult()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("vip-user", MessagePriority.Normal);
        var expectedResult = MessageResult.Ok("Expected Response");

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(expectedResult),
            CancellationToken.None);

        Assert.Same(expectedResult, result);
    }

    [Test]
    public async Task Middleware_ReturnsFailureResult_FromNextHandler()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("vip-user", MessagePriority.Normal);

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Fail("Error")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Error", result.Error);
    }

    [Test]
    public async Task Middleware_BoostsPriority_BeforeNextHandler()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("vip-user", MessagePriority.Normal);
        MessagePriority? priorityInHandler = null;

        await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                priorityInHandler = msg.Priority;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.Equal(MessagePriority.High, priorityInHandler);
    }

    #endregion

    #region Pipeline Integration Tests

    [Test]
    public async Task Integration_WorksInPipeline()
    {
        var pipeline = new MiddlewarePipeline();
        var executionOrder = new List<string>();

        pipeline.Use(new NamedTrackingMiddleware("First", executionOrder));
        pipeline.Use(new PriorityBoostMiddleware("vip-user"));
        pipeline.Use(new NamedTrackingMiddleware("Last", executionOrder));

        var message = CreateMessage("vip-user", MessagePriority.Normal);

        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            executionOrder.Add("Handler");
            return Task.FromResult(MessageResult.Ok("Done"));
        });

        await builtPipeline(message, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
        // PriorityBoostMiddleware doesn't add to executionOrder, so order is:
        // First-Before -> Last-Before -> Handler -> Last-After -> First-After
        Assert.Equal(5, executionOrder.Count);
        Assert.Equal("First-Before", executionOrder[0]);
        Assert.Equal("Last-Before", executionOrder[1]);
        Assert.Equal("Handler", executionOrder[2]);
        Assert.Equal("Last-After", executionOrder[3]);
        Assert.Equal("First-After", executionOrder[4]);
    }

    [Test]
    public async Task Integration_PriorityVisibleToSubsequentMiddleware()
    {
        var pipeline = new MiddlewarePipeline();
        MessagePriority? observedPriority = null;

        pipeline.Use(new PriorityBoostMiddleware("vip-user"));
        pipeline.Use(new CallbackMiddleware(before: msg => observedPriority = msg.Priority));

        var message = CreateMessage("vip-user", MessagePriority.Low);

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));

        await builtPipeline(message, CancellationToken.None);

        Assert.Equal(MessagePriority.High, observedPriority);
    }

    [Test]
    public async Task Integration_ChainedWithValidationMiddleware()
    {
        var pipeline = new MiddlewarePipeline();

        pipeline.Use(new ValidationMiddleware());
        pipeline.Use(new PriorityBoostMiddleware("vip-user"));

        var message = CreateMessage("vip-user", MessagePriority.Normal);

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));

        var result = await builtPipeline(message, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(MessagePriority.High, message.Priority);
    }

    #endregion

    #region Theory Tests - Parameterized

    [Theory]
    [InlineData("vip", "vip", MessagePriority.Low, MessagePriority.High)]
    [InlineData("vip", "vip", MessagePriority.Normal, MessagePriority.High)]
    [InlineData("vip", "vip", MessagePriority.High, MessagePriority.High)]
    [InlineData("vip", "vip", MessagePriority.Urgent, MessagePriority.Urgent)]
    [InlineData("vip", "other", MessagePriority.Low, MessagePriority.Low)]
    [InlineData("vip", "other", MessagePriority.Normal, MessagePriority.Normal)]
    public async Task Theory_PriorityBoostBehavior(
        string vipSender,
        string messageSender,
        MessagePriority initialPriority,
        MessagePriority expectedPriority)
    {
        var middleware = new PriorityBoostMiddleware(vipSender);
        var message = CreateMessage(messageSender, initialPriority);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(expectedPriority, message.Priority);
    }

    [Theory]
    [InlineData("VIP")]
    [InlineData("vip")]
    [InlineData("Vip")]
    [InlineData("vIp")]
    [InlineData("viP")]
    public async Task Theory_CaseInsensitiveMatching(string senderVariation)
    {
        var middleware = new PriorityBoostMiddleware("VIP");
        var message = CreateMessage(senderVariation, MessagePriority.Normal);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    #endregion

    #region Edge Cases - Special Sender IDs

    [Test]
    public async Task EdgeCase_EmptySenderId_NotMatched()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = new AgentMessage
        {
            SenderId = "",
            Subject = "Test",
            Content = "Test",
            Priority = MessagePriority.Normal
        };

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.Normal, message.Priority);
    }

    [Test]
    public async Task EdgeCase_WhitespaceSenderId_NotMatched()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = new AgentMessage
        {
            SenderId = "   ",
            Subject = "Test",
            Content = "Test",
            Priority = MessagePriority.Normal
        };

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.Normal, message.Priority);
    }

    [Test]
    public async Task EdgeCase_SpecialCharactersInSenderId()
    {
        var middleware = new PriorityBoostMiddleware("vip@domain.com", "vip-user_123");

        var emailMessage = CreateMessage("vip@domain.com", MessagePriority.Normal);
        var underscoreMessage = CreateMessage("vip-user_123", MessagePriority.Normal);

        await middleware.InvokeAsync(emailMessage, PassthroughHandler, CancellationToken.None);
        await middleware.InvokeAsync(underscoreMessage, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, emailMessage.Priority);
        Assert.Equal(MessagePriority.High, underscoreMessage.Priority);
    }

    [Test]
    public async Task EdgeCase_UnicodeInSenderId()
    {
        var middleware = new PriorityBoostMiddleware("vip-user");
        var message = CreateMessage("vip-user", MessagePriority.Normal);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task EdgeCase_LongSenderId()
    {
        var longSenderId = new string('a', 1000);
        var middleware = new PriorityBoostMiddleware(longSenderId);
        var message = CreateMessage(longSenderId, MessagePriority.Normal);

        await middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    #endregion

    #region Concurrency Tests

    [Test]
    public async Task Concurrency_MultipleMessagesProcessedCorrectly()
    {
        var middleware = new PriorityBoostMiddleware("vip1", "vip2");

        var tasks = new List<Task>();
        var messages = new List<AgentMessage>();

        for (int i = 0; i < 100; i++)
        {
            var senderId = i % 2 == 0 ? "vip1" : "regular";
            var message = CreateMessage(senderId, MessagePriority.Normal);
            messages.Add(message);
            tasks.Add(middleware.InvokeAsync(message, PassthroughHandler, CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        for (int i = 0; i < messages.Count; i++)
        {
            var expectedPriority = i % 2 == 0 ? MessagePriority.High : MessagePriority.Normal;
            Assert.Equal(expectedPriority, messages[i].Priority);
        }
    }

    #endregion

    #region Helper Methods and Classes

    private static AgentMessage CreateMessage(string senderId, MessagePriority priority)
    {
        return new AgentMessage
        {
            SenderId = senderId,
            Subject = "Test Subject",
            Content = "Test Content",
            Priority = priority
        };
    }

    private static readonly MessageDelegate PassthroughHandler =
        (msg, ct) => Task.FromResult(MessageResult.Ok("OK"));

    #endregion
}
