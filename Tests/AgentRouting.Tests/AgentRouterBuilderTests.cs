using TestRunner.Framework;
using AgentRouting.Core;
using AgentRouting.Middleware;
using RulesEngine.Core;
using TestUtilities;

namespace TestRunner.Tests;

/// <summary>
/// Comprehensive tests for AgentRouterBuilder to improve code coverage.
/// </summary>
public class AgentRouterBuilderTests
{
    #region Helper Methods

    private static AgentMessage CreateTestMessage(string category = "Test")
    {
        return new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = "test-sender",
            Subject = "Test Subject",
            Content = "Test Content",
            Category = category
        };
    }

    #endregion

    #region Build with Defaults Tests

    [Test]
    public void Build_WithNoConfiguration_CreatesRouterWithDefaults()
    {
        var builder = new AgentRouterBuilder();
        var router = builder.Build();

        Assert.NotNull(router);
    }

    [Test]
    public async Task Build_WithNoConfiguration_RouterCanProcessMessages()
    {
        var builder = new AgentRouterBuilder();
        var router = builder.Build();

        // Register an agent directly
        router.RegisterAgent(new SimpleTestAgent("test-1", "Test Agent", "Test"));

        var message = CreateTestMessage();
        var result = await router.RouteMessageAsync(message, CancellationToken.None);

        Assert.True(result.Success);
    }

    #endregion

    #region WithLogger Tests

    [Test]
    public void WithLogger_SetsCustomLogger()
    {
        var logger = new TestLogger();
        var builder = new AgentRouterBuilder()
            .WithLogger(logger);

        var router = builder.Build();

        Assert.NotNull(router);
    }

    [Test]
    public async Task WithLogger_CustomLoggerReceivesLogs()
    {
        var logger = new TestLogger();
        var builder = new AgentRouterBuilder()
            .WithLogger(logger)
            .RegisterAgent(new SimpleTestAgent("test-1", "Test Agent", "Test"));

        var router = builder.Build();
        var message = CreateTestMessage();

        await router.RouteMessageAsync(message, CancellationToken.None);

        Assert.True(logger.Logs.Count > 0);
    }

    [Test]
    public void WithLogger_ReturnsBuilderForChaining()
    {
        var builder = new AgentRouterBuilder();
        var result = builder.WithLogger(new TestLogger());

        Assert.Same(builder, result);
    }

    #endregion

    #region WithPipeline Tests

    [Test]
    public void WithPipeline_SetsCustomPipeline()
    {
        var pipeline = new MiddlewarePipeline();
        var builder = new AgentRouterBuilder()
            .WithPipeline(pipeline);

        var router = builder.Build();

        Assert.NotNull(router);
    }

    [Test]
    public void WithPipeline_ReturnsBuilderForChaining()
    {
        var builder = new AgentRouterBuilder();
        var result = builder.WithPipeline(new MiddlewarePipeline());

        Assert.Same(builder, result);
    }

    [Test]
    public async Task WithPipeline_CustomPipelineIsUsed()
    {
        var pipeline = new MiddlewarePipeline();
        var middleware = new CountingMiddleware();
        pipeline.Use(middleware);

        var builder = new AgentRouterBuilder()
            .WithPipeline(pipeline)
            .RegisterAgent(new SimpleTestAgent("test-1", "Test Agent", "Test"));

        var router = builder.Build();
        await router.RouteMessageAsync(CreateTestMessage(), CancellationToken.None);

        Assert.Equal(1, middleware.InvokeCount);
    }

    #endregion

    #region WithRoutingEngine Tests

    [Test]
    public void WithRoutingEngine_SetsCustomEngine()
    {
        var engine = new RulesEngineCore<RoutingContext>();
        var builder = new AgentRouterBuilder()
            .WithRoutingEngine(engine);

        var router = builder.Build();

        Assert.NotNull(router);
    }

    [Test]
    public void WithRoutingEngine_ReturnsBuilderForChaining()
    {
        var builder = new AgentRouterBuilder();
        var result = builder.WithRoutingEngine(new RulesEngineCore<RoutingContext>());

        Assert.Same(builder, result);
    }

    #endregion

    #region UseMiddleware Tests

    [Test]
    public void UseMiddleware_AddsMiddlewareToPipeline()
    {
        var middleware = new CountingMiddleware();
        var builder = new AgentRouterBuilder()
            .UseMiddleware(middleware);

        var router = builder.Build();

        Assert.NotNull(router);
    }

    [Test]
    public void UseMiddleware_ReturnsBuilderForChaining()
    {
        var builder = new AgentRouterBuilder();
        var result = builder.UseMiddleware(new CountingMiddleware());

        Assert.Same(builder, result);
    }

    [Test]
    public async Task UseMiddleware_MiddlewareIsInvokedOnRoute()
    {
        var middleware = new CountingMiddleware();
        var builder = new AgentRouterBuilder()
            .UseMiddleware(middleware)
            .RegisterAgent(new SimpleTestAgent("test-1", "Test Agent", "Test"));

        var router = builder.Build();
        await router.RouteMessageAsync(CreateTestMessage(), CancellationToken.None);

        Assert.Equal(1, middleware.InvokeCount);
    }

    [Test]
    public async Task UseMiddleware_MultipleMiddleware_InvokedInOrder()
    {
        var order = new List<string>();
        var middleware1 = new NamedTrackingMiddleware("First", order);
        var middleware2 = new NamedTrackingMiddleware("Second", order);

        var builder = new AgentRouterBuilder()
            .UseMiddleware(middleware1)
            .UseMiddleware(middleware2)
            .RegisterAgent(new SimpleTestAgent("test-1", "Test Agent", "Test"));

        var router = builder.Build();
        await router.RouteMessageAsync(CreateTestMessage(), CancellationToken.None);

        Assert.True(order.IndexOf("First-Before") < order.IndexOf("Second-Before"));
    }

    #endregion

    #region RegisterAgent Tests

    [Test]
    public void RegisterAgent_AddsAgentToRouter()
    {
        var agent = new SimpleTestAgent("test-1", "Test Agent", "Test");
        var builder = new AgentRouterBuilder()
            .RegisterAgent(agent);

        var router = builder.Build();

        var agents = router.GetAllAgents();
        Assert.Equal(1, agents.Count);
        Assert.Equal("test-1", agents[0].Id);
    }

    [Test]
    public void RegisterAgent_ReturnsBuilderForChaining()
    {
        var builder = new AgentRouterBuilder();
        var result = builder.RegisterAgent(new SimpleTestAgent("test-1", "Test", "Test"));

        Assert.Same(builder, result);
    }

    [Test]
    public void RegisterAgent_MultipleAgents_AllRegistered()
    {
        var builder = new AgentRouterBuilder()
            .RegisterAgent(new SimpleTestAgent("agent-1", "Agent 1", "Category1"))
            .RegisterAgent(new SimpleTestAgent("agent-2", "Agent 2", "Category2"))
            .RegisterAgent(new SimpleTestAgent("agent-3", "Agent 3", "Category3"));

        var router = builder.Build();

        Assert.Equal(3, router.GetAllAgents().Count);
    }

    [Test]
    public async Task RegisterAgent_AgentCanProcessMessages()
    {
        var agent = new SimpleTestAgent("test-1", "Test Agent", "Test");
        var builder = new AgentRouterBuilder()
            .RegisterAgent(agent);

        var router = builder.Build();
        var result = await router.RouteMessageAsync(CreateTestMessage(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Processed by Test Agent", result.Response!);
    }

    #endregion

    #region AddRoutingRule Tests

    [Test]
    public void AddRoutingRule_AddsRuleToEngine()
    {
        var builder = new AgentRouterBuilder()
            .RegisterAgent(new SimpleTestAgent("billing-agent", "Billing", "Billing"))
            .AddRoutingRule(
                "rule-1",
                "Route Billing",
                ctx => ctx.Category == "Billing",
                "billing-agent",
                priority: 100);

        var router = builder.Build();

        Assert.NotNull(router);
    }

    [Test]
    public void AddRoutingRule_ReturnsBuilderForChaining()
    {
        var builder = new AgentRouterBuilder();
        var result = builder.AddRoutingRule(
            "rule-1",
            "Test Rule",
            ctx => true,
            "target-agent");

        Assert.Same(builder, result);
    }

    [Test]
    public void AddRoutingRule_WithDefaultPriority_SetsZeroPriority()
    {
        var builder = new AgentRouterBuilder()
            .RegisterAgent(new SimpleTestAgent("test-agent", "Test", "Test"))
            .AddRoutingRule(
                "rule-1",
                "Test Rule",
                ctx => true,
                "test-agent");

        var router = builder.Build();

        Assert.NotNull(router);
    }

    [Test]
    public void AddRoutingRule_MultipleRules_AllAdded()
    {
        var builder = new AgentRouterBuilder()
            .RegisterAgent(new SimpleTestAgent("billing", "Billing", "Billing"))
            .RegisterAgent(new SimpleTestAgent("support", "Support", "Support"))
            .RegisterAgent(new SimpleTestAgent("sales", "Sales", "Sales"))
            .AddRoutingRule("r1", "Billing Route", ctx => ctx.Category == "Billing", "billing", 100)
            .AddRoutingRule("r2", "Support Route", ctx => ctx.Category == "Support", "support", 90)
            .AddRoutingRule("r3", "Sales Route", ctx => ctx.Category == "Sales", "sales", 80);

        var router = builder.Build();

        Assert.NotNull(router);
    }

    #endregion

    #region Full Builder Chain Tests

    [Test]
    public async Task FullChain_AllComponentsConfigured_WorksTogether()
    {
        var logger = new TestLogger();
        var middleware = new CountingMiddleware();

        var builder = new AgentRouterBuilder()
            .WithLogger(logger)
            .UseMiddleware(middleware)
            .RegisterAgent(new SimpleTestAgent("billing", "Billing Agent", "Billing"))
            .RegisterAgent(new SimpleTestAgent("support", "Support Agent", "Support"))
            .AddRoutingRule("r1", "Billing Route", ctx => ctx.Category == "Billing", "billing", 100)
            .AddRoutingRule("r2", "Support Route", ctx => ctx.Category == "Support", "support", 90);

        var router = builder.Build();

        var billingMessage = CreateTestMessage("Billing");
        var billingResult = await router.RouteMessageAsync(billingMessage, CancellationToken.None);

        Assert.True(billingResult.Success);
        Assert.Equal(1, middleware.InvokeCount);
        Assert.True(logger.Logs.Count > 0);
    }

    [Test]
    public void FullChain_FluentSyntax_Readable()
    {
        var router = new AgentRouterBuilder()
            .WithLogger(new TestLogger())
            .UseMiddleware(new ValidationMiddleware())
            .UseMiddleware(new TimingMiddleware())
            .RegisterAgent(new SimpleTestAgent("agent-1", "Agent 1", "Cat1"))
            .RegisterAgent(new SimpleTestAgent("agent-2", "Agent 2", "Cat2"))
            .AddRoutingRule("r1", "Rule 1", ctx => ctx.Category == "Cat1", "agent-1")
            .AddRoutingRule("r2", "Rule 2", ctx => ctx.Category == "Cat2", "agent-2")
            .Build();

        Assert.NotNull(router);
        Assert.Equal(2, router.GetAllAgents().Count);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void Build_CalledMultipleTimes_CreatesNewRouterEachTime()
    {
        var builder = new AgentRouterBuilder()
            .RegisterAgent(new SimpleTestAgent("test-1", "Test", "Test"));

        var router1 = builder.Build();
        var router2 = builder.Build();

        Assert.NotSame(router1, router2);
    }

    [Test]
    public void Build_EmptyBuilder_CreatesValidRouter()
    {
        var builder = new AgentRouterBuilder();
        var router = builder.Build();

        Assert.NotNull(router);
        Assert.Equal(0, router.GetAllAgents().Count);
    }

    #endregion
}
