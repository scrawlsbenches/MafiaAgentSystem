using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;

namespace TestRunner.Tests;

/// <summary>
/// Tests for MessageTransformationMiddleware which normalizes, sanitizes, and enriches messages
/// </summary>
public class MessageTransformationMiddlewareTests
{
    #region Text Normalization Tests

    [Test]
    public async Task NormalizeText_TrimsLeadingAndTrailingWhitespace_Subject()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(subject: "   Test Subject   ");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("Test Subject", message.Subject);
    }

    [Test]
    public async Task NormalizeText_TrimsLeadingAndTrailingWhitespace_Content()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "   Test Content   ");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("Test Content", message.Content);
    }

    [Test]
    public async Task NormalizeText_CollapsesMultipleSpaces()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Hello    world   with    spaces");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("Hello world with spaces", message.Content);
    }

    [Test]
    public async Task NormalizeText_CollapsesTabsAndNewlines()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Hello\t\tworld\n\nwith\r\nwhitespace");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("Hello world with whitespace", message.Content);
    }

    [Test]
    public async Task NormalizeText_EmptyString_ReturnsEmpty()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("", message.Content);
    }

    [Test]
    public async Task NormalizeText_OnlyWhitespace_ReturnsEmpty()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "     ");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("", message.Content);
    }

    [Test]
    public async Task NormalizeText_BothSubjectAndContent_Normalized()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(
            subject: "  Multiple   spaces  ",
            content: "  Also   has   spaces  ");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("Multiple spaces", message.Subject);
        Assert.Equal("Also has spaces", message.Content);
    }

    #endregion

    #region Content Sanitization Tests

    [Test]
    public async Task SanitizeInput_RemovesScriptOpenTag()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Hello <script>alert('xss')</script> World");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.False(message.Content.Contains("<script>"));
    }

    [Test]
    public async Task SanitizeInput_RemovesScriptCloseTag()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Test </script> content");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.False(message.Content.Contains("</script>"));
    }

    [Test]
    public async Task SanitizeInput_RemovesJavascriptProtocol()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Click here javascript:void(0)");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.False(message.Content.Contains("javascript:"));
    }

    [Test]
    public async Task SanitizeInput_RemovesOnerrorAttribute()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "<img src='x' onerror=alert('xss')>");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.False(message.Content.Contains("onerror="));
    }

    [Test]
    public async Task SanitizeInput_MultipleInjectionPatterns_AllRemoved()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(
            content: "<script>bad</script> javascript:evil onerror=hack");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.False(message.Content.Contains("<script>"));
        Assert.False(message.Content.Contains("</script>"));
        Assert.False(message.Content.Contains("javascript:"));
        Assert.False(message.Content.Contains("onerror="));
    }

    [Test]
    public async Task SanitizeInput_CleanContent_Unchanged()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "This is clean content without any attacks");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("This is clean content without any attacks", message.Content);
    }

    [Test]
    public async Task SanitizeInput_EmptyContent_ReturnsEmpty()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("", message.Content);
    }

    #endregion

    #region Email Extraction Tests

    [Test]
    public async Task ExtractEmails_SingleEmail_SetsMetadata()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Contact me at test@example.com");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsEmail"]);
        Assert.Equal(1, (int)message.Metadata["EmailCount"]);
    }

    [Test]
    public async Task ExtractEmails_MultipleEmails_CountsCorrectly()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(
            content: "Contact first@example.com or second@test.org or third@domain.net");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsEmail"]);
        Assert.Equal(3, (int)message.Metadata["EmailCount"]);
    }

    [Test]
    public async Task ExtractEmails_NoEmails_NoMetadataSet()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "No email addresses here");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.False(message.Metadata.ContainsKey("ContainsEmail"));
        Assert.False(message.Metadata.ContainsKey("EmailCount"));
    }

    [Test]
    public async Task ExtractEmails_ComplexEmailFormat_Extracted()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Email: user.name+tag@sub.example.co.uk");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsEmail"]);
        Assert.Equal(1, (int)message.Metadata["EmailCount"]);
    }

    [Test]
    public async Task ExtractEmails_InvalidEmail_NotExtracted()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Invalid: user@, @domain.com, user@domain");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.False(message.Metadata.ContainsKey("ContainsEmail"));
    }

    [Test]
    public async Task ExtractEmails_EmailWithNumbers_Extracted()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Contact: user123@test456.com");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsEmail"]);
        Assert.Equal(1, (int)message.Metadata["EmailCount"]);
    }

    #endregion

    #region Phone Number Extraction Tests

    [Test]
    public async Task ExtractPhoneNumbers_StandardFormat_Extracted()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Call me at 555-123-4567");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsPhone"]);
        Assert.Equal(1, (int)message.Metadata["PhoneCount"]);
    }

    [Test]
    public async Task ExtractPhoneNumbers_DotSeparated_Extracted()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Phone: 555.123.4567");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsPhone"]);
        Assert.Equal(1, (int)message.Metadata["PhoneCount"]);
    }

    [Test]
    public async Task ExtractPhoneNumbers_NoSeparators_Extracted()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Phone: 5551234567");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsPhone"]);
        Assert.Equal(1, (int)message.Metadata["PhoneCount"]);
    }

    [Test]
    public async Task ExtractPhoneNumbers_MultiplePhones_CountsCorrectly()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(
            content: "Office: 555-111-2222 Mobile: 555-333-4444 Fax: 555.555.6666");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsPhone"]);
        Assert.Equal(3, (int)message.Metadata["PhoneCount"]);
    }

    [Test]
    public async Task ExtractPhoneNumbers_NoPhones_NoMetadataSet()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "No phone numbers here");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.False(message.Metadata.ContainsKey("ContainsPhone"));
        Assert.False(message.Metadata.ContainsKey("PhoneCount"));
    }

    [Test]
    public async Task ExtractPhoneNumbers_TooShort_NotExtracted()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Short number: 555-1234");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.False(message.Metadata.ContainsKey("ContainsPhone"));
    }

    #endregion

    #region Language Detection Tests

    [Test]
    public async Task DetectLanguage_EnglishContent_DetectsEnglish()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "This is a test message in English");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("English", message.Metadata["DetectedLanguage"]);
    }

    [Test]
    public async Task DetectLanguage_SpanishContent_DetectsSpanish()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(
            content: "Este es un mensaje de prueba en el idioma que es muy bonito");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("Spanish", message.Metadata["DetectedLanguage"]);
    }

    [Test]
    public async Task DetectLanguage_FrenchContent_DetectsFrench()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(
            content: "C'est un message dans le style que est dans une langue");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("French", message.Metadata["DetectedLanguage"]);
    }

    [Test]
    public async Task DetectLanguage_MixedContent_DefaultsToEnglish()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Hello world testing message");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("English", message.Metadata["DetectedLanguage"]);
    }

    [Test]
    public async Task DetectLanguage_EmptyContent_DefaultsToEnglish()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("English", message.Metadata["DetectedLanguage"]);
    }

    #endregion

    #region Metadata Enrichment Tests

    [Test]
    public async Task Enrichment_AddsProcessingTimestamp()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("ProcessingTimestamp"));
        var timestamp = (string)message.Metadata["ProcessingTimestamp"];
        Assert.NotNull(timestamp);
        Assert.True(timestamp.Length > 0);
    }

    [Test]
    public async Task Enrichment_AlwaysSetsDetectedLanguage()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage();

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True(message.Metadata.ContainsKey("DetectedLanguage"));
    }

    [Test]
    public async Task Enrichment_CombinedEmailAndPhone()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(
            content: "Contact: test@example.com or call 555-123-4567");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsEmail"]);
        Assert.Equal(1, (int)message.Metadata["EmailCount"]);
        Assert.True((bool)message.Metadata["ContainsPhone"]);
        Assert.Equal(1, (int)message.Metadata["PhoneCount"]);
        Assert.True(message.Metadata.ContainsKey("DetectedLanguage"));
        Assert.True(message.Metadata.ContainsKey("ProcessingTimestamp"));
    }

    [Test]
    public async Task Enrichment_PreservesExistingMetadata()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage();
        message.Metadata["ExistingKey"] = "ExistingValue";

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal("ExistingValue", message.Metadata["ExistingKey"]);
    }

    #endregion

    #region Pipeline Integration Tests

    [Test]
    public async Task Pipeline_CallsNextDelegate()
    {
        var middleware = new MessageTransformationMiddleware();
        var nextCalled = false;

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("OK"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
    }

    [Test]
    public async Task Pipeline_ReturnsNextDelegateResult()
    {
        var middleware = new MessageTransformationMiddleware();

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Expected Response")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Expected Response", result.Response);
    }

    [Test]
    public async Task Pipeline_PropagatesFailure()
    {
        var middleware = new MessageTransformationMiddleware();

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Fail("Error occurred")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Error occurred", result.Error);
    }

    [Test]
    public async Task Pipeline_TransformsBeforeCallingNext()
    {
        var middleware = new MessageTransformationMiddleware();
        string? contentDuringNext = null;

        var message = CreateTestMessage(content: "  <script>bad</script>  test@example.com  ");

        await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                contentDuringNext = msg.Content;
                return Task.FromResult(MessageResult.Ok("OK"));
            },
            CancellationToken.None);

        // Content should be normalized and sanitized before next is called
        Assert.NotNull(contentDuringNext);
        Assert.False(contentDuringNext.Contains("<script>"));
        Assert.False(contentDuringNext.StartsWith(" "));
        Assert.False(contentDuringNext.EndsWith(" "));
    }

    [Test]
    public async Task Pipeline_MetadataAvailableInNext()
    {
        var middleware = new MessageTransformationMiddleware();
        bool emailMetadataPresent = false;

        var message = CreateTestMessage(content: "Email: test@example.com");

        await middleware.InvokeAsync(
            message,
            (msg, ct) =>
            {
                emailMetadataPresent = msg.Metadata.ContainsKey("ContainsEmail");
                return Task.FromResult(MessageResult.Ok("OK"));
            },
            CancellationToken.None);

        Assert.True(emailMetadataPresent);
    }

    [Test]
    public async Task Pipeline_RespectsCancellationToken()
    {
        var middleware = new MessageTransformationMiddleware();
        var cts = new CancellationTokenSource();
        var tokenReceivedInNext = CancellationToken.None;

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                tokenReceivedInNext = ct;
                return Task.FromResult(MessageResult.Ok("OK"));
            },
            cts.Token);

        Assert.Equal(cts.Token, tokenReceivedInNext);
    }

    #endregion

    #region Edge Cases Tests

    [Test]
    public async Task EdgeCase_NullSubject_HandledGracefully()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = null!,
            Content = "Valid content"
        };

        // Should not throw
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);
    }

    [Test]
    public async Task EdgeCase_NullContent_HandledGracefully()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Valid subject",
            Content = null!
        };

        // Should not throw
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);
    }

    [Test]
    public async Task EdgeCase_VeryLongContent_ProcessedSuccessfully()
    {
        var middleware = new MessageTransformationMiddleware();
        var longContent = string.Join(" ", Enumerable.Repeat("word", 10000));
        var message = CreateTestMessage(content: longContent);

        var result = await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True(result.Success);
    }

    [Test]
    public async Task EdgeCase_SpecialCharacters_Preserved()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Special: @#$%^&*()_+-=[]{}|;':\",./<>?");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Special characters should be preserved after normalization
        Assert.Contains("@#$%^&*()_+-=[]{}|;':\",./<>?", message.Content);
    }

    [Test]
    public async Task EdgeCase_UnicodeContent_Preserved()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "Unicode: \u4e2d\u6587 \u0420\u0443\u0441\u0441\u043a\u0438\u0439 \u65e5\u672c\u8a9e");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Contains("\u4e2d\u6587", message.Content);
        Assert.Contains("\u0420\u0443\u0441\u0441\u043a\u0438\u0439", message.Content);
    }

    [Test]
    public async Task EdgeCase_OnlyInjectionPatterns_ResultsInMinimalContent()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: "<script></script>javascript:onerror=");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.False(message.Content.Contains("<script>"));
        Assert.False(message.Content.Contains("javascript:"));
        Assert.False(message.Content.Contains("onerror="));
    }

    [Test]
    public async Task EdgeCase_CaseSensitiveInjectionPatterns_ExactMatchOnly()
    {
        var middleware = new MessageTransformationMiddleware();
        // These variations have different casing so won't be removed by exact match
        var message = CreateTestMessage(content: "<SCRIPT>test</SCRIPT> JAVASCRIPT: ONERROR=");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Upper case versions are preserved (the middleware does exact match only)
        Assert.Contains("<SCRIPT>", message.Content);
    }

    [Test]
    public async Task EdgeCase_EmailLikePatterns_OnlyValidExtracted()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(
            content: "Invalid: @@ at@at valid@example.com name@domain");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // Only valid@example.com should be extracted
        Assert.True((bool)message.Metadata["ContainsEmail"]);
        Assert.Equal(1, (int)message.Metadata["EmailCount"]);
    }

    [Test]
    public async Task EdgeCase_MultipleDuplicateEmails_AllCounted()
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(
            content: "Email: test@example.com and again test@example.com");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.True((bool)message.Metadata["ContainsEmail"]);
        Assert.Equal(2, (int)message.Metadata["EmailCount"]);
    }

    [Test]
    public async Task EdgeCase_PhoneNumbersWithParentheses_NotMatchedBySimpleRegex()
    {
        var middleware = new MessageTransformationMiddleware();
        // The middleware uses a simple pattern that doesn't support parentheses
        var message = CreateTestMessage(content: "Phone: (555) 123-4567");

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        // This format is not matched by the simple regex
        Assert.False(message.Metadata.ContainsKey("ContainsPhone"));
    }

    #endregion

    #region Theory Tests

    [Theory]
    [InlineData("hello", "English")]
    [InlineData("", "English")]
    [InlineData("test message", "English")]
    public async Task DetectLanguage_Theory_VariousEnglishContent(string content, string expectedLanguage)
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: content);

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal(expectedLanguage, message.Metadata["DetectedLanguage"]);
    }

    [Theory]
    [InlineData("test@example.com", true, 1)]
    [InlineData("no email here", false, 0)]
    [InlineData("a@b.co b@c.io", true, 2)]
    public async Task ExtractEmails_Theory_VariousPatterns(string content, bool containsEmail, int count)
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: content);

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        if (containsEmail)
        {
            Assert.True((bool)message.Metadata["ContainsEmail"]);
            Assert.Equal(count, (int)message.Metadata["EmailCount"]);
        }
        else
        {
            Assert.False(message.Metadata.ContainsKey("ContainsEmail"));
        }
    }

    [Theory]
    [InlineData("5551234567", true, 1)]
    [InlineData("555-123-4567", true, 1)]
    [InlineData("555.123.4567", true, 1)]
    [InlineData("no phone", false, 0)]
    public async Task ExtractPhones_Theory_VariousFormats(string content, bool containsPhone, int count)
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: content);

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        if (containsPhone)
        {
            Assert.True((bool)message.Metadata["ContainsPhone"]);
            Assert.Equal(count, (int)message.Metadata["PhoneCount"]);
        }
        else
        {
            Assert.False(message.Metadata.ContainsKey("ContainsPhone"));
        }
    }

    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("a  b  c", "a b c")]
    [InlineData("", "")]
    [InlineData("single", "single")]
    public async Task NormalizeText_Theory_WhitespaceHandling(string input, string expected)
    {
        var middleware = new MessageTransformationMiddleware();
        var message = CreateTestMessage(content: input);

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("OK")),
            CancellationToken.None);

        Assert.Equal(expected, message.Content);
    }

    #endregion

    #region Helper Methods

    private static AgentMessage CreateTestMessage(
        string senderId = "test-sender",
        string subject = "Test Subject",
        string content = "Test Content")
    {
        return new AgentMessage
        {
            SenderId = senderId,
            Subject = subject,
            Content = content,
            Category = "Test"
        };
    }

    #endregion
}
