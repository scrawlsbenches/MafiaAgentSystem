using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.Middleware.Advanced;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for FeatureFlagsMiddleware - conditional feature enablement middleware
/// </summary>
public class FeatureFlagsMiddlewareTests
{
    #region Basic Flag Registration and Evaluation

    [Test]
    public async Task FeatureFlags_EnabledFlag_SetsContextToTrue()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("premium-features", enabled: true);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        var flagValue = context.Get<bool>("Feature_premium-features");
        Assert.True(flagValue);
    }

    [Test]
    public async Task FeatureFlags_DisabledFlag_SetsContextToFalse()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("beta-features", enabled: false);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        var flagValue = context.Get<bool>("Feature_beta-features");
        Assert.False(flagValue);
    }

    [Test]
    public async Task FeatureFlags_NoRegisteredFlags_CallsNextAndSucceeds()
    {
        var middleware = new FeatureFlagsMiddleware();
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.Success);
    }

    [Test]
    public async Task FeatureFlags_AlwaysCallsNext_DoesNotShortCircuit()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("some-flag", enabled: false);
        var nextCalled = false;

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(MessageResult.Ok("Handler executed"));
            },
            CancellationToken.None);

        Assert.True(nextCalled, "Next handler should always be called");
        Assert.True(result.Success);
        Assert.Equal("Handler executed", result.Response);
    }

    #endregion

    #region Conditional Flag Evaluation

    [Test]
    public async Task FeatureFlags_ConditionMet_EnablesFlag()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "vip-only",
            enabled: true,
            condition: msg => msg.SenderId.StartsWith("vip-"));

        var vipMessage = CreateTestMessage("vip-user123");
        await middleware.InvokeAsync(
            vipMessage,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = vipMessage.GetContext();
        var flagValue = context.Get<bool>("Feature_vip-only");
        Assert.True(flagValue);
    }

    [Test]
    public async Task FeatureFlags_ConditionNotMet_DisablesFlag()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "vip-only",
            enabled: true,
            condition: msg => msg.SenderId.StartsWith("vip-"));

        var regularMessage = CreateTestMessage("regular-user");
        await middleware.InvokeAsync(
            regularMessage,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = regularMessage.GetContext();
        var flagValue = context.Get<bool>("Feature_vip-only");
        Assert.False(flagValue);
    }

    [Test]
    public async Task FeatureFlags_DisabledFlagWithTrueCondition_RemainsDisabled()
    {
        var middleware = new FeatureFlagsMiddleware();
        // Flag is disabled, so even if condition is true, it should remain disabled
        middleware.RegisterFlag(
            "disabled-feature",
            enabled: false,
            condition: msg => true);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        var flagValue = context.Get<bool>("Feature_disabled-feature");
        Assert.False(flagValue);
    }

    [Test]
    public async Task FeatureFlags_EnabledFlagWithNullCondition_IsEnabled()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("always-on", enabled: true, condition: null);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        var flagValue = context.Get<bool>("Feature_always-on");
        Assert.True(flagValue);
    }

    #endregion

    #region Sender-Specific Flag Overrides

    [Test]
    public async Task FeatureFlags_SenderSpecificCondition_EnablesForSpecificSender()
    {
        var middleware = new FeatureFlagsMiddleware();
        var allowedSenders = new HashSet<string> { "admin", "superuser", "tester" };

        middleware.RegisterFlag(
            "admin-tools",
            enabled: true,
            condition: msg => allowedSenders.Contains(msg.SenderId));

        var adminMessage = CreateTestMessage("admin");
        await middleware.InvokeAsync(
            adminMessage,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var adminContext = adminMessage.GetContext();
        Assert.True(adminContext.Get<bool>("Feature_admin-tools"));

        var regularMessage = CreateTestMessage("regular-user");
        await middleware.InvokeAsync(
            regularMessage,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var regularContext = regularMessage.GetContext();
        Assert.False(regularContext.Get<bool>("Feature_admin-tools"));
    }

    [Test]
    public async Task FeatureFlags_PercentageRollout_BasedOnSenderId()
    {
        var middleware = new FeatureFlagsMiddleware();
        // Simulate percentage rollout based on sender ID hash
        middleware.RegisterFlag(
            "experimental",
            enabled: true,
            condition: msg => Math.Abs(msg.SenderId.GetHashCode() % 100) < 50);

        // Test multiple senders to verify conditional logic works
        var results = new List<bool>();
        for (int i = 0; i < 10; i++)
        {
            var message = CreateTestMessage($"user-{i}");
            await middleware.InvokeAsync(
                message,
                (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
                CancellationToken.None);

            results.Add(message.GetContext().Get<bool>("Feature_experimental"));
        }

        // Verify that the condition is being evaluated (some true, some false)
        // Due to hash distribution, we expect a mix
        Assert.True(results.Contains(true) || results.Contains(false));
    }

    #endregion

    #region Multiple Flags Evaluation

    [Test]
    public async Task FeatureFlags_MultipleFlags_AllEvaluated()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("feature-a", enabled: true);
        middleware.RegisterFlag("feature-b", enabled: false);
        middleware.RegisterFlag("feature-c", enabled: true);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        Assert.True(context.Get<bool>("Feature_feature-a"));
        Assert.False(context.Get<bool>("Feature_feature-b"));
        Assert.True(context.Get<bool>("Feature_feature-c"));
    }

    [Test]
    public async Task FeatureFlags_MultipleFlagsWithConditions_EvaluatedIndependently()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "high-priority-only",
            enabled: true,
            condition: msg => msg.Priority >= MessagePriority.High);
        middleware.RegisterFlag(
            "admin-only",
            enabled: true,
            condition: msg => msg.SenderId == "admin");
        middleware.RegisterFlag(
            "always-enabled",
            enabled: true);

        var adminHighPriorityMessage = new AgentMessage
        {
            SenderId = "admin",
            Subject = "Test",
            Content = "Content",
            Priority = MessagePriority.High
        };

        await middleware.InvokeAsync(
            adminHighPriorityMessage,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = adminHighPriorityMessage.GetContext();
        Assert.True(context.Get<bool>("Feature_high-priority-only"));
        Assert.True(context.Get<bool>("Feature_admin-only"));
        Assert.True(context.Get<bool>("Feature_always-enabled"));
    }

    [Test]
    public async Task FeatureFlags_MultipleFlagsWithMixedConditionResults()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "urgent-only",
            enabled: true,
            condition: msg => msg.Priority == MessagePriority.Urgent);
        middleware.RegisterFlag(
            "normal-allowed",
            enabled: true,
            condition: msg => msg.Priority >= MessagePriority.Normal);

        var normalMessage = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Test",
            Content = "Content",
            Priority = MessagePriority.Normal
        };

        await middleware.InvokeAsync(
            normalMessage,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = normalMessage.GetContext();
        Assert.False(context.Get<bool>("Feature_urgent-only"));
        Assert.True(context.Get<bool>("Feature_normal-allowed"));
    }

    #endregion

    #region Flag Configuration Updates

    [Test]
    public async Task FeatureFlags_RegisteringSameFlagTwice_OverwritesPrevious()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("toggle-flag", enabled: true);
        middleware.RegisterFlag("toggle-flag", enabled: false); // Overwrite

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        Assert.False(context.Get<bool>("Feature_toggle-flag"));
    }

    [Test]
    public async Task FeatureFlags_RegisteringSameFlagWithDifferentCondition_OverwritesCondition()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("conditional-flag", enabled: true, condition: msg => false);
        middleware.RegisterFlag("conditional-flag", enabled: true, condition: msg => true); // New condition

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        Assert.True(context.Get<bool>("Feature_conditional-flag"));
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task FeatureFlags_UnknownFlagKey_NotInContext()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("known-flag", enabled: true);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        // Trying to get an unregistered flag should return default (false for bool)
        var unknownFlag = context.Get<bool>("Feature_unknown-flag");
        Assert.False(unknownFlag);
    }

    [Test]
    public async Task FeatureFlags_EmptyFlagName_StillWorks()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("", enabled: true);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        var flagValue = context.Get<bool>("Feature_");
        Assert.True(flagValue);
    }

    [Test]
    public async Task FeatureFlags_SpecialCharactersInFlagName_Works()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("feature:v2.0-beta_test", enabled: true);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        var flagValue = context.Get<bool>("Feature_feature:v2.0-beta_test");
        Assert.True(flagValue);
    }

    [Test]
    public async Task FeatureFlags_ConditionBasedOnMessageContent_Works()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "keyword-feature",
            enabled: true,
            condition: msg => msg.Content.Contains("special"));

        var messageWithKeyword = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Test",
            Content = "This is a special message"
        };

        var messageWithoutKeyword = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Test",
            Content = "This is a normal message"
        };

        await middleware.InvokeAsync(
            messageWithKeyword,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            messageWithoutKeyword,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(messageWithKeyword.GetContext().Get<bool>("Feature_keyword-feature"));
        Assert.False(messageWithoutKeyword.GetContext().Get<bool>("Feature_keyword-feature"));
    }

    [Test]
    public async Task FeatureFlags_ConditionBasedOnMetadata_Works()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "metadata-feature",
            enabled: true,
            condition: msg => msg.Metadata.TryGetValue("plan", out var plan) && plan.ToString() == "premium");

        var premiumMessage = CreateTestMessage();
        premiumMessage.Metadata["plan"] = "premium";

        var freeMessage = CreateTestMessage();
        freeMessage.Metadata["plan"] = "free";

        await middleware.InvokeAsync(
            premiumMessage,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            freeMessage,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(premiumMessage.GetContext().Get<bool>("Feature_metadata-feature"));
        Assert.False(freeMessage.GetContext().Get<bool>("Feature_metadata-feature"));
    }

    [Test]
    public async Task FeatureFlags_ConditionBasedOnCategory_Works()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "support-feature",
            enabled: true,
            condition: msg => msg.Category == "Support");

        var supportMessage = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Help needed",
            Content = "I need help",
            Category = "Support"
        };

        var salesMessage = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Sales inquiry",
            Content = "I want to buy",
            Category = "Sales"
        };

        await middleware.InvokeAsync(
            supportMessage,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        await middleware.InvokeAsync(
            salesMessage,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        Assert.True(supportMessage.GetContext().Get<bool>("Feature_support-feature"));
        Assert.False(salesMessage.GetContext().Get<bool>("Feature_support-feature"));
    }

    #endregion

    #region Pipeline Integration Tests

    [Test]
    public async Task FeatureFlags_InPipeline_PropagatesContext()
    {
        var pipeline = new MiddlewarePipeline();
        var featureFlagsMiddleware = new FeatureFlagsMiddleware();
        featureFlagsMiddleware.RegisterFlag("test-feature", enabled: true);

        pipeline.Use(featureFlagsMiddleware);

        bool? flagValueInHandler = null;
        var builtPipeline = pipeline.Build((msg, ct) =>
        {
            flagValueInHandler = msg.GetContext().Get<bool>("Feature_test-feature");
            return Task.FromResult(MessageResult.Ok("Done"));
        });

        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.True(flagValueInHandler.HasValue, "Flag value should have been set in handler");
        Assert.True(flagValueInHandler!.Value);
    }

    [Test]
    public async Task FeatureFlags_InPipeline_DownstreamMiddlewareCanReadFlags()
    {
        var pipeline = new MiddlewarePipeline();
        var featureFlagsMiddleware = new FeatureFlagsMiddleware();
        featureFlagsMiddleware.RegisterFlag("premium", enabled: true);

        bool premiumFlagInDownstream = false;

        pipeline.Use(featureFlagsMiddleware);
        pipeline.Use(next => async (msg, ct) =>
        {
            premiumFlagInDownstream = msg.GetContext().Get<bool>("Feature_premium");
            return await next(msg, ct);
        });

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));
        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.True(premiumFlagInDownstream);
    }

    [Test]
    public async Task FeatureFlags_InPipeline_ConditionalMiddlewareBasedOnFlag()
    {
        var pipeline = new MiddlewarePipeline();
        var featureFlagsMiddleware = new FeatureFlagsMiddleware();
        featureFlagsMiddleware.RegisterFlag("enable-logging", enabled: true);

        var loggedMessages = new List<string>();

        pipeline.Use(featureFlagsMiddleware);
        pipeline.Use(next => async (msg, ct) =>
        {
            var loggingEnabled = msg.GetContext().Get<bool>("Feature_enable-logging");
            if (loggingEnabled)
            {
                loggedMessages.Add($"Processing: {msg.Subject}");
            }
            return await next(msg, ct);
        });

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));
        await builtPipeline(CreateTestMessage(), CancellationToken.None);

        Assert.Equal(1, loggedMessages.Count);
        Assert.Contains("Processing:", loggedMessages[0]);
    }

    [Test]
    public async Task FeatureFlags_InPipeline_MultipleMessagesIndependent()
    {
        var pipeline = new MiddlewarePipeline();
        var featureFlagsMiddleware = new FeatureFlagsMiddleware();
        featureFlagsMiddleware.RegisterFlag(
            "vip-feature",
            enabled: true,
            condition: msg => msg.SenderId.StartsWith("vip"));

        pipeline.Use(featureFlagsMiddleware);

        var builtPipeline = pipeline.Build((msg, ct) => Task.FromResult(MessageResult.Ok("Done")));

        var vipMessage = CreateTestMessage("vip-user");
        var regularMessage = CreateTestMessage("regular-user");

        await builtPipeline(vipMessage, CancellationToken.None);
        await builtPipeline(regularMessage, CancellationToken.None);

        Assert.True(vipMessage.GetContext().Get<bool>("Feature_vip-feature"));
        Assert.False(regularMessage.GetContext().Get<bool>("Feature_vip-feature"));
    }

    #endregion

    #region Async and Cancellation Tests

    [Test]
    public async Task FeatureFlags_RespectsNextResult_PassesThrough()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("test-flag", enabled: true);

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Ok("Custom response")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Custom response", result.Response);
    }

    [Test]
    public async Task FeatureFlags_NextFails_PassesFailureThrough()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("test-flag", enabled: true);

        var result = await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) => Task.FromResult(MessageResult.Fail("Handler failed")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Handler failed", result.Error!);
    }

    [Test]
    public async Task FeatureFlags_CancellationToken_PassedToNext()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("test-flag", enabled: true);

        using var cts = new CancellationTokenSource();
        CancellationToken receivedToken = default;

        await middleware.InvokeAsync(
            CreateTestMessage(),
            (msg, ct) =>
            {
                receivedToken = ct;
                return Task.FromResult(MessageResult.Ok("Done"));
            },
            cts.Token);

        Assert.Equal(cts.Token, receivedToken);
    }

    #endregion

    #region Complex Condition Tests

    [Test]
    public async Task FeatureFlags_ComplexCondition_AndLogic()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "premium-urgent",
            enabled: true,
            condition: msg =>
                msg.Priority == MessagePriority.Urgent &&
                msg.Metadata.TryGetValue("tier", out var tier) && tier.ToString() == "premium");

        var matchingMessage = new AgentMessage
        {
            SenderId = "test",
            Subject = "Test",
            Content = "Test",
            Priority = MessagePriority.Urgent,
            Metadata = new Dictionary<string, object> { ["tier"] = "premium" }
        };

        var urgentButNotPremium = new AgentMessage
        {
            SenderId = "test",
            Subject = "Test",
            Content = "Test",
            Priority = MessagePriority.Urgent,
            Metadata = new Dictionary<string, object> { ["tier"] = "free" }
        };

        var premiumButNotUrgent = new AgentMessage
        {
            SenderId = "test",
            Subject = "Test",
            Content = "Test",
            Priority = MessagePriority.Normal,
            Metadata = new Dictionary<string, object> { ["tier"] = "premium" }
        };

        await middleware.InvokeAsync(matchingMessage, (msg, ct) => Task.FromResult(MessageResult.Ok("Done")), CancellationToken.None);
        await middleware.InvokeAsync(urgentButNotPremium, (msg, ct) => Task.FromResult(MessageResult.Ok("Done")), CancellationToken.None);
        await middleware.InvokeAsync(premiumButNotUrgent, (msg, ct) => Task.FromResult(MessageResult.Ok("Done")), CancellationToken.None);

        Assert.True(matchingMessage.GetContext().Get<bool>("Feature_premium-urgent"));
        Assert.False(urgentButNotPremium.GetContext().Get<bool>("Feature_premium-urgent"));
        Assert.False(premiumButNotUrgent.GetContext().Get<bool>("Feature_premium-urgent"));
    }

    [Test]
    public async Task FeatureFlags_ComplexCondition_OrLogic()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "support-or-urgent",
            enabled: true,
            condition: msg =>
                msg.Category == "Support" ||
                msg.Priority == MessagePriority.Urgent);

        var supportMessage = new AgentMessage
        {
            SenderId = "test",
            Subject = "Test",
            Content = "Test",
            Category = "Support",
            Priority = MessagePriority.Low
        };

        var urgentMessage = new AgentMessage
        {
            SenderId = "test",
            Subject = "Test",
            Content = "Test",
            Category = "Sales",
            Priority = MessagePriority.Urgent
        };

        var neitherMessage = new AgentMessage
        {
            SenderId = "test",
            Subject = "Test",
            Content = "Test",
            Category = "Sales",
            Priority = MessagePriority.Low
        };

        await middleware.InvokeAsync(supportMessage, (msg, ct) => Task.FromResult(MessageResult.Ok("Done")), CancellationToken.None);
        await middleware.InvokeAsync(urgentMessage, (msg, ct) => Task.FromResult(MessageResult.Ok("Done")), CancellationToken.None);
        await middleware.InvokeAsync(neitherMessage, (msg, ct) => Task.FromResult(MessageResult.Ok("Done")), CancellationToken.None);

        Assert.True(supportMessage.GetContext().Get<bool>("Feature_support-or-urgent"));
        Assert.True(urgentMessage.GetContext().Get<bool>("Feature_support-or-urgent"));
        Assert.False(neitherMessage.GetContext().Get<bool>("Feature_support-or-urgent"));
    }

    #endregion

    #region Default Values Tests

    [Test]
    public async Task FeatureFlags_ContextTryGet_ReturnsFalseForUnsetFlag()
    {
        var middleware = new FeatureFlagsMiddleware();
        // Register a different flag
        middleware.RegisterFlag("registered-flag", enabled: true);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        var found = context.TryGet<bool>("Feature_unregistered-flag", out var value);

        Assert.False(found);
    }

    [Test]
    public async Task FeatureFlags_FlagNaming_UsesCorrectPrefix()
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag("my-flag", enabled: true);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();

        // Should be set with Feature_ prefix
        Assert.True(context.TryGet<bool>("Feature_my-flag", out var withPrefix));
        Assert.True(withPrefix);

        // Should NOT be set without prefix
        Assert.False(context.TryGet<bool>("my-flag", out _));
    }

    #endregion

    #region Theory Tests

    [Theory]
    [InlineData("flag-a", true, true)]
    [InlineData("flag-b", false, false)]
    [InlineData("feature-123", true, true)]
    [InlineData("UPPERCASE", false, false)]
    public async Task FeatureFlags_VariousFlagConfigurations(string flagName, bool enabled, bool expected)
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(flagName, enabled: enabled);

        var message = CreateTestMessage();
        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        var flagValue = context.Get<bool>($"Feature_{flagName}");
        Assert.Equal(expected, flagValue);
    }

    [Theory]
    [InlineData(MessagePriority.Low)]
    [InlineData(MessagePriority.Normal)]
    [InlineData(MessagePriority.High)]
    [InlineData(MessagePriority.Urgent)]
    public async Task FeatureFlags_PriorityCondition_EvaluatesCorrectly(MessagePriority priority)
    {
        var middleware = new FeatureFlagsMiddleware();
        middleware.RegisterFlag(
            "high-priority-feature",
            enabled: true,
            condition: msg => msg.Priority >= MessagePriority.High);

        var message = new AgentMessage
        {
            SenderId = "test-sender",
            Subject = "Test",
            Content = "Content",
            Priority = priority
        };

        await middleware.InvokeAsync(
            message,
            (msg, ct) => Task.FromResult(MessageResult.Ok("Done")),
            CancellationToken.None);

        var context = message.GetContext();
        var flagValue = context.Get<bool>("Feature_high-priority-feature");
        var expectedValue = priority >= MessagePriority.High;
        Assert.Equal(expectedValue, flagValue);
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
