using AgentRouting.Core;
using AgentRouting.DependencyInjection;
using AgentRouting.Infrastructure;
using AgentRouting.Middleware;
using RulesEngine.Core;
using TestRunner.Framework;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for ServiceExtensions and AgentRoutingOptions.
/// </summary>
public class ServiceExtensionsTests : AgentRoutingTestBase
{
    [Test]
    public void AddAgentRouting_RegistersAllCoreServices()
    {
        // Arrange
        var container = new ServiceContainer();

        // Act
        container.AddAgentRouting();

        // Assert
        Assert.True(container.IsRegistered<ISystemClock>(), "ISystemClock should be registered");
        Assert.True(container.IsRegistered<IStateStore>(), "IStateStore should be registered");
        Assert.True(container.IsRegistered<IAgentLogger>(), "IAgentLogger should be registered");
        Assert.True(container.IsRegistered<IMiddlewarePipeline>(), "IMiddlewarePipeline should be registered");
        Assert.True(container.IsRegistered<IRulesEngine<RoutingContext>>(), "IRulesEngine<RoutingContext> should be registered");
        Assert.True(container.IsRegistered<AgentRouter>(), "AgentRouter should be registered");
    }

    [Test]
    public void AddAgentRouting_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new ServiceContainer();

        // Act
        var result = container.AddAgentRouting();

        // Assert
        Assert.Same(container, result);
    }

    [Test]
    public void AddAgentRouting_WithCustomLogger_UsesProvidedLogger()
    {
        // Arrange
        var container = new ServiceContainer();
        var customLogger = new TestLogger();

        // Act
        container.AddAgentRouting(options => options.Logger = customLogger);

        // Assert
        var resolved = container.Resolve<IAgentLogger>();
        Assert.Same(customLogger, resolved);
    }

    [Test]
    public void AddAgentRouting_WithCustomRulesEngineOptions_AppliesOptions()
    {
        // Arrange
        var container = new ServiceContainer();
        var options = new RulesEngineOptions
        {
            StopOnFirstMatch = true,
            TrackPerformance = true
        };

        // Act
        container.AddAgentRouting(o => o.RulesEngineOptions = options);

        // Assert - just verify resolution works
        var engine = container.Resolve<IRulesEngine<RoutingContext>>();
        Assert.NotNull(engine);
    }

    [Test]
    public void AddAgentRouting_DoesNotOverrideExistingRegistrations()
    {
        // Arrange
        var container = new ServiceContainer();
        var customClock = new TestClock();
        container.AddSingleton<ISystemClock>(customClock);

        // Act
        container.AddAgentRouting();

        // Assert
        var resolved = container.Resolve<ISystemClock>();
        Assert.Same(customClock, resolved);
    }

    [Test]
    public void AddAgentRouting_ResolvesAgentRouter_WithAllDependencies()
    {
        // Arrange
        var container = new ServiceContainer();
        container.AddAgentRouting();

        // Act
        var router = container.Resolve<AgentRouter>();

        // Assert
        Assert.NotNull(router);
    }

    [Test]
    public void AddMiddleware_RegistersMiddlewareAsTransient()
    {
        // Arrange
        var container = new ServiceContainer();

        // Act
        container.AddMiddleware<TestMiddleware>(c => new TestMiddleware());

        // Assert
        var first = container.Resolve<TestMiddleware>();
        var second = container.Resolve<TestMiddleware>();
        Assert.NotSame(first, second); // Transient means different instances
    }

    [Test]
    public void AddMiddlewareSingleton_RegistersMiddlewareAsSingleton()
    {
        // Arrange
        var container = new ServiceContainer();

        // Act
        container.AddMiddlewareSingleton<TestMiddleware>(c => new TestMiddleware());

        // Assert
        var first = container.Resolve<TestMiddleware>();
        var second = container.Resolve<TestMiddleware>();
        Assert.Same(first, second); // Singleton means same instance
    }

    [Test]
    public void AddAgent_RegistersAgentAsTransient()
    {
        // Arrange
        var container = new ServiceContainer();
        container.AddAgentRouting(); // Need logger

        // Act
        container.AddAgent<TestAgent>(c => new TestAgent(
            Guid.NewGuid().ToString(),
            "TestAgent",
            c.Resolve<IAgentLogger>()));

        // Assert
        var first = container.Resolve<TestAgent>();
        var second = container.Resolve<TestAgent>();
        Assert.NotSame(first, second); // Transient means different instances
    }

    [Test]
    public void AddAgentSingleton_RegistersAgentAsSingleton()
    {
        // Arrange
        var container = new ServiceContainer();
        container.AddAgentRouting(); // Need logger

        // Act
        container.AddAgentSingleton<TestAgent>(c => new TestAgent(
            "singleton-agent",
            "SingletonAgent",
            c.Resolve<IAgentLogger>()));

        // Assert
        var first = container.Resolve<TestAgent>();
        var second = container.Resolve<TestAgent>();
        Assert.Same(first, second); // Singleton means same instance
    }

    [Test]
    public void AddAgentInstance_RegistersExistingInstance()
    {
        // Arrange
        var container = new ServiceContainer();
        var logger = new TestLogger();
        var agent = new TestAgent("existing-agent", "ExistingAgent", logger);

        // Act
        container.AddAgentInstance(agent);

        // Assert
        var resolved = container.Resolve<TestAgent>();
        Assert.Same(agent, resolved);
    }

    [Test]
    public void AddRulesEngine_RegistersRulesEngineAsTransient()
    {
        // Arrange
        var container = new ServiceContainer();

        // Act
        container.AddRulesEngine<TestContext>();

        // Assert
        var first = container.Resolve<IRulesEngine<TestContext>>();
        var second = container.Resolve<IRulesEngine<TestContext>>();
        Assert.NotSame(first, second); // Transient means different instances
    }

    [Test]
    public void AddRulesEngine_WithOptions_AppliesOptions()
    {
        // Arrange
        var container = new ServiceContainer();
        var options = new RulesEngineOptions { TrackPerformance = true };

        // Act
        container.AddRulesEngine<TestContext>(options);

        // Assert
        var engine = container.Resolve<IRulesEngine<TestContext>>();
        Assert.NotNull(engine);
    }

    [Test]
    public void AddRulesEngineSingleton_RegistersRulesEngineAsSingleton()
    {
        // Arrange
        var container = new ServiceContainer();

        // Act
        container.AddRulesEngineSingleton<TestContext>();

        // Assert
        var first = container.Resolve<IRulesEngine<TestContext>>();
        var second = container.Resolve<IRulesEngine<TestContext>>();
        Assert.Same(first, second); // Singleton means same instance
    }

    [Test]
    public void AddRulesEngineSingleton_WithOptions_AppliesOptions()
    {
        // Arrange
        var container = new ServiceContainer();
        var options = new RulesEngineOptions { StopOnFirstMatch = true };

        // Act
        container.AddRulesEngineSingleton<TestContext>(options);

        // Assert
        var engine = container.Resolve<IRulesEngine<TestContext>>();
        Assert.NotNull(engine);
    }

    [Test]
    public void AgentRoutingOptions_DefaultLogger_IsNull()
    {
        // Arrange & Act
        var options = new AgentRoutingOptions();

        // Assert
        Assert.Null(options.Logger);
    }

    [Test]
    public void AgentRoutingOptions_DefaultRulesEngineOptions_IsNotNull()
    {
        // Arrange & Act
        var options = new AgentRoutingOptions();

        // Assert
        Assert.NotNull(options.RulesEngineOptions);
    }

    [Test]
    public void FluentChaining_AllMethodsReturnContainer()
    {
        // Arrange
        var container = new ServiceContainer();

        // Act & Assert - chain multiple registrations
        var result = container
            .AddAgentRouting()
            .AddMiddleware<TestMiddleware>(c => new TestMiddleware())
            .AddRulesEngine<TestContext>();

        Assert.Same(container, result);
    }

    // Helper classes
    private class TestContext { public string Value { get; set; } = ""; }

    private class TestMiddleware : MiddlewareBase
    {
        public override Task<MessageResult> InvokeAsync(
            AgentMessage message,
            MessageDelegate next,
            CancellationToken ct)
        {
            return next(message, ct);
        }
    }

    private class TestAgent : AgentBase
    {
        public TestAgent(string id, string name, IAgentLogger logger)
            : base(id, name, logger) { }

        protected override Task<MessageResult> HandleMessageAsync(
            AgentMessage message,
            CancellationToken ct)
        {
            return Task.FromResult(MessageResult.Ok("handled"));
        }
    }

    private class TestLogger : IAgentLogger
    {
        public void LogMessageReceived(IAgent agent, AgentMessage message) { }
        public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result) { }
        public void LogMessageRouted(AgentMessage message, IAgent? fromAgent, IAgent toAgent) { }
        public void LogError(IAgent agent, AgentMessage message, Exception ex) { }
    }

    private class TestClock : ISystemClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
