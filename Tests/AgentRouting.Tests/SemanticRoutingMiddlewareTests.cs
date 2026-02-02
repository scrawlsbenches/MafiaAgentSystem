using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;

namespace AgentRouting.Tests;

/// <summary>
/// Comprehensive tests for SemanticRoutingMiddleware
/// Tests intent detection, priority boosting, category assignment, and metadata enrichment
/// </summary>
public class SemanticRoutingMiddlewareTests
{
    #region Intent Detection Tests

    [Test]
    public async Task SemanticRouting_DetectsComplaintIntent()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("I am frustrated with the service");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("complaint", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_DetectsQuestionIntent()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("What is the status of my order?");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("question", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_DetectsUrgentIntent()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("This is an emergency, please help immediately");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("urgent", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_DetectsPraiseIntent()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("Thank you for the excellent service, it was wonderful!");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("praise", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_DetectsTechnicalIntent()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("There is a bug in the application, it's not working");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("technical", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_NoMatchingKeywords_NoIntentsDetected()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("Hello, I would like to place an order please");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.False(message.Metadata.ContainsKey("DetectedIntents"));
    }

    #endregion

    #region Multiple Intents Tests

    [Test]
    public async Task SemanticRouting_DetectsMultipleIntents()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("I am angry because the bug is causing a critical error");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        var intents = message.Metadata["DetectedIntents"].ToString()!;

        Assert.Contains("complaint", intents);
        Assert.Contains("urgent", intents);
        Assert.Contains("technical", intents);
    }

    [Test]
    public async Task SemanticRouting_MultipleIntents_StoredAsCommaSeparated()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("Why is this awful error happening immediately?");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var intents = message.Metadata["DetectedIntents"].ToString()!;
        // Should contain commas if multiple intents detected
        Assert.True(intents.Contains(","), $"Expected comma-separated intents but got: {intents}");
    }

    [Test]
    public async Task SemanticRouting_ComplaintAndUrgent_BothDetected()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("I am furious, this is urgent and needs immediate attention");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var intents = message.Metadata["DetectedIntents"].ToString()!;
        Assert.Contains("complaint", intents);
        Assert.Contains("urgent", intents);
    }

    #endregion

    #region Priority Boosting Tests

    [Test]
    public async Task SemanticRouting_ComplaintIntent_BoostsPriorityToHigh()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Complaint",
            Content = "I am disappointed with the terrible service",
            Priority = MessagePriority.Normal
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task SemanticRouting_UrgentIntent_BoostsPriorityToHigh()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Help",
            Content = "Please respond asap, this is critical",
            Priority = MessagePriority.Low
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task SemanticRouting_AlreadyHighPriority_NoChange()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Alert",
            Content = "Emergency situation happening now",
            Priority = MessagePriority.High
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task SemanticRouting_UrgentPriority_NotDowngraded()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "VIP Message",
            Content = "Emergency help needed",
            Priority = MessagePriority.Urgent
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(MessagePriority.Urgent, message.Priority);
    }

    [Test]
    public async Task SemanticRouting_PraiseIntent_DoesNotBoostPriority()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Feedback",
            Content = "Thank you for the great and amazing help!",
            Priority = MessagePriority.Normal
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(MessagePriority.Normal, message.Priority);
    }

    [Test]
    public async Task SemanticRouting_QuestionIntent_DoesNotBoostPriority()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Inquiry",
            Content = "What is the delivery time?",
            Priority = MessagePriority.Normal
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(MessagePriority.Normal, message.Priority);
    }

    [Test]
    public async Task SemanticRouting_LowPriorityComplaint_BoostsToHigh()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Issue",
            Content = "The product was horrible and I am upset",
            Priority = MessagePriority.Low
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
    }

    #endregion

    #region Category Assignment Tests

    [Test]
    public async Task SemanticRouting_TechnicalIntent_AssignsTechnicalSupportCategory()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Help",
            Content = "The application has a bug and keeps crashing",
            Category = ""
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal("TechnicalSupport", message.Category);
    }

    [Test]
    public async Task SemanticRouting_TechnicalIntent_ExistingCategory_NoChange()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Bug Report",
            Content = "There is an error in the system",
            Category = "CustomerService"
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal("CustomerService", message.Category);
    }

    [Test]
    public async Task SemanticRouting_NonTechnicalIntent_NoCategory_StaysEmpty()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Question",
            Content = "What are your business hours?",
            Category = ""
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal("", message.Category);
    }

    [Test]
    public async Task SemanticRouting_TechnicalWithNullCategory_AssignsCategory()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Issue",
            Content = "The system is broken and not working"
        };
        // Category defaults to "" in AgentMessage, not null

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal("TechnicalSupport", message.Category);
    }

    #endregion

    #region Edge Cases Tests

    [Test]
    public async Task SemanticRouting_EmptyContent_NoError()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Test",
            Content = ""
        };

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(message.Metadata.ContainsKey("DetectedIntents"));
    }

    [Test]
    public async Task SemanticRouting_EmptySubjectAndContent_NoError()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "",
            Content = ""
        };

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    [Test]
    public async Task SemanticRouting_KeywordInSubjectOnly_Detected()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "URGENT: Please help",
            Content = "I need assistance with my account"
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("urgent", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_KeywordInContentOnly_Detected()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Hello",
            Content = "I need help immediately with this emergency"
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("urgent", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_CaseInsensitive_UpperCase()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("THIS IS URGENT AND CRITICAL!");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("urgent", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_CaseInsensitive_MixedCase()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("I am ANGRY and Frustrated");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("complaint", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_PartialKeywordMatch_Detected()
    {
        var middleware = new SemanticRoutingMiddleware();
        // "frustrated" contains "frustrated" so it should match
        var message = CreateTestMessage("I was frustrated yesterday");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("complaint", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_KeywordWithinWord_Detected()
    {
        var middleware = new SemanticRoutingMiddleware();
        // "horrible" is in the keywords and "horribleness" contains it
        var message = CreateTestMessage("The horribleness of the situation was apparent");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("complaint", message.Metadata["DetectedIntents"].ToString()!);
    }

    #endregion

    #region Specific Keyword Tests

    [Theory]
    [InlineData("I am angry about this")]
    [InlineData("This is the worst experience")]
    [InlineData("I am furious with the service")]
    [InlineData("The product was awful")]
    [InlineData("I am upset about the delay")]
    public async Task SemanticRouting_ComplaintKeywords_Detected(string content)
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage(content);

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("complaint", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Theory]
    [InlineData("When will my order arrive?")]
    [InlineData("How do I reset my password?")]
    [InlineData("Can you help me with this?")]
    [InlineData("Could you explain this feature?")]
    [InlineData("Who is in charge here?")]
    public async Task SemanticRouting_QuestionKeywords_Detected(string content)
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage(content);

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("question", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Theory]
    [InlineData("Please respond asap")]
    [InlineData("This is an emergency")]
    [InlineData("Critical issue needs resolution now")]
    [InlineData("I need a quick response")]
    [InlineData("Please help immediately")]
    public async Task SemanticRouting_UrgentKeywords_Detected(string content)
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage(content);

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("urgent", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Theory]
    [InlineData("Great job on this!")]
    [InlineData("You are fantastic")]
    [InlineData("I love the new features")]
    [InlineData("This is amazing work")]
    [InlineData("Thank you for your help")]
    public async Task SemanticRouting_PraiseKeywords_Detected(string content)
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage(content);

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("praise", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Theory]
    [InlineData("There is a bug in the system")]
    [InlineData("The application keeps crashing")]
    [InlineData("I found an error message")]
    [InlineData("The feature is broken")]
    [InlineData("I have a technical issue")]
    public async Task SemanticRouting_TechnicalKeywords_Detected(string content)
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage(content);

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("technical", message.Metadata["DetectedIntents"].ToString()!);
    }

    #endregion

    #region Pipeline Integration Tests

    [Test]
    public async Task SemanticRouting_CallsNextMiddleware()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("I am angry about this bug");
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
    public async Task SemanticRouting_ReturnsResultFromNext()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("Some content");
        var expectedResponse = "Expected Response";

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok(expectedResponse)),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedResponse, result.Response);
    }

    [Test]
    public async Task SemanticRouting_InPipeline_ExecutesCorrectly()
    {
        var pipeline = new MiddlewarePipeline();
        var executionOrder = new List<string>();

        pipeline.Use(new CallbackMiddleware(before: _ => executionOrder.Add("Before")));
        pipeline.Use(new SemanticRoutingMiddleware());
        pipeline.Use(new CallbackMiddleware(before: _ => executionOrder.Add("After")));

        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Emergency",
            Content = "This is an urgent matter",
            Priority = MessagePriority.Normal
        };

        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            executionOrder.Add("Handler");
            return Task.FromResult(MessageResult.Ok("Done"));
        });

        await builtPipeline(message, CancellationToken.None);

        Assert.Contains("Before", executionOrder);
        Assert.Contains("After", executionOrder);
        Assert.Contains("Handler", executionOrder);
        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task SemanticRouting_ModifiesMessageBeforeNext()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Issue",
            Content = "The system crashed with an error",
            Priority = MessagePriority.Normal,
            Category = ""
        };

        MessagePriority? capturedPriority = null;
        string? capturedCategory = null;

        await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                capturedPriority = msg.Priority;
                capturedCategory = msg.Category;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        // Verify the modifications were made before calling next
        Assert.Equal(MessagePriority.Normal, capturedPriority);
        Assert.Equal("TechnicalSupport", capturedCategory);
    }

    [Test]
    public async Task SemanticRouting_HandlesFailedNextResult()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("I am angry about this issue");

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Fail("Processing failed")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Processing failed", result.Error);
        // Metadata should still be set even if next fails
        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
    }

    [Test]
    public async Task SemanticRouting_RespectsCancellationToken()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("Some content");
        var cts = new CancellationTokenSource();
        CancellationToken receivedToken = default;

        await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                receivedToken = ct;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            cts.Token);

        Assert.Equal(cts.Token, receivedToken);
    }

    #endregion

    #region Complex Scenarios Tests

    [Test]
    public async Task SemanticRouting_AllIntentsInOneMessage()
    {
        var middleware = new SemanticRoutingMiddleware();
        // A message that could trigger all intents
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Urgent question about bug",
            Content = "Why is this terrible crash happening? Thank you for helping now!",
            Priority = MessagePriority.Low,
            Category = ""
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var intents = message.Metadata["DetectedIntents"].ToString()!;
        Assert.Contains("complaint", intents);  // "terrible"
        Assert.Contains("question", intents);   // "why"
        Assert.Contains("urgent", intents);     // "now"
        Assert.Contains("praise", intents);     // "thank"
        Assert.Contains("technical", intents);  // "crash"
    }

    [Test]
    public async Task SemanticRouting_PriorityAndCategoryBothApplied()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Bug Report",
            Content = "I am angry that this bug is not working properly",
            Priority = MessagePriority.Low,
            Category = ""
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.Equal(MessagePriority.High, message.Priority);
        Assert.Equal("TechnicalSupport", message.Category);

        var intents = message.Metadata["DetectedIntents"].ToString()!;
        Assert.Contains("complaint", intents);
        Assert.Contains("technical", intents);
    }

    [Test]
    public async Task SemanticRouting_MultipleMessages_IndependentProcessing()
    {
        var middleware = new SemanticRoutingMiddleware();

        var message1 = new AgentMessage
        {
            SenderId = "sender-1",
            Subject = "Test",
            Content = "I am angry",
            Priority = MessagePriority.Normal
        };

        var message2 = new AgentMessage
        {
            SenderId = "sender-2",
            Subject = "Test",
            Content = "Thank you for the help",
            Priority = MessagePriority.Normal
        };

        await middleware.InvokeAsync(
            message1,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            message2,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        // Message 1 should have complaint intent and boosted priority
        Assert.Equal(MessagePriority.High, message1.Priority);
        Assert.Contains("complaint", message1.Metadata["DetectedIntents"].ToString()!);

        // Message 2 should have praise intent and normal priority
        Assert.Equal(MessagePriority.Normal, message2.Priority);
        Assert.Contains("praise", message2.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_WhitespaceOnlyContent_NoError()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "   ",
            Content = "   \t\n  "
        };

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(message.Metadata.ContainsKey("DetectedIntents"));
    }

    [Test]
    public async Task SemanticRouting_SpecialCharactersInContent_NoError()
    {
        var middleware = new SemanticRoutingMiddleware();
        var message = CreateTestMessage("I'm <angry>!!! @#$%^&*() about the terrible [service]");

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("complaint", message.Metadata["DetectedIntents"].ToString()!);
    }

    [Test]
    public async Task SemanticRouting_VeryLongContent_NoError()
    {
        var middleware = new SemanticRoutingMiddleware();
        var longContent = string.Join(" ", Enumerable.Repeat("word", 1000)) + " angry " + string.Join(" ", Enumerable.Repeat("text", 1000));
        var message = CreateTestMessage(longContent);

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(message.Metadata.ContainsKey("DetectedIntents"));
        Assert.Contains("complaint", message.Metadata["DetectedIntents"].ToString()!);
    }

    #endregion

    #region Helper Methods

    private static AgentMessage CreateTestMessage(string content, string senderId = "test-sender")
    {
        return new AgentMessage
        {
            SenderId = senderId,
            Subject = "Test Subject",
            Content = content,
            Category = ""
        };
    }

    #endregion
}
