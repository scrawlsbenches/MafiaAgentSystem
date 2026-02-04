using AgentRouting.Agents;
using AgentRouting.Core;
using AgentRouting.Middleware;
using RulesEngine.Core;
using TestRunner.Framework;
using TestUtilities;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for service agent implementations.
/// </summary>
public class ServiceAgentsTests : AgentRoutingTestBase
{
    private IAgentLogger _logger = null!;

    [SetUp]
    public void Setup()
    {
        _logger = SilentAgentLogger.Instance;
    }

    #region CustomerServiceAgent Tests

    [Test]
    public async Task CustomerServiceAgent_HandlesHoursInquiry()
    {
        // Arrange
        var agent = new CustomerServiceAgent("cs-1", "Customer Service", _logger);
        var message = new AgentMessage
        {
            Category = "CustomerService",
            Subject = "Business Hours",
            Content = "What are your business hours?"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("hours", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task CustomerServiceAgent_HandlesShippingInquiry()
    {
        // Arrange
        var agent = new CustomerServiceAgent("cs-1", "Customer Service", _logger);
        var message = new AgentMessage
        {
            Category = "CustomerService",
            Subject = "Shipping",
            Content = "How long does shipping take?"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("shipping", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task CustomerServiceAgent_HandlesReturnsInquiry()
    {
        // Arrange
        var agent = new CustomerServiceAgent("cs-1", "Customer Service", _logger);
        var message = new AgentMessage
        {
            Category = "CustomerService",
            Subject = "Returns",
            Content = "What is your returns policy?"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("returns", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task CustomerServiceAgent_HandlesPaymentInquiry()
    {
        // Arrange
        var agent = new CustomerServiceAgent("cs-1", "Customer Service", _logger);
        var message = new AgentMessage
        {
            Category = "CustomerService",
            Subject = "Payment",
            Content = "What payment methods do you accept?"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("accept", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task CustomerServiceAgent_HandlesTrackingInquiry()
    {
        // Arrange
        var agent = new CustomerServiceAgent("cs-1", "Customer Service", _logger);
        var message = new AgentMessage
        {
            Category = "CustomerService",
            Subject = "Tracking",
            Content = "Where is my tracking number?" // Must contain "tracking" keyword
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("track", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task CustomerServiceAgent_EscalatesUrgentMessages()
    {
        // Arrange
        var agent = new CustomerServiceAgent("cs-1", "Customer Service", _logger);
        var message = new AgentMessage
        {
            Category = "CustomerService",
            Subject = "Urgent Issue",
            Content = "I have an urgent problem",
            Priority = MessagePriority.Urgent
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ForwardedMessages.Count > 0, "Expected forwarded message");
        Assert.Contains("escalated", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task CustomerServiceAgent_EscalatesComplaints()
    {
        // Arrange
        var agent = new CustomerServiceAgent("cs-1", "Customer Service", _logger);
        var message = new AgentMessage
        {
            Category = "CustomerService",
            Subject = "Complaint",
            Content = "I want to file a complaint about my order"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ForwardedMessages.Count > 0, "Expected forwarded message");
    }

    [Test]
    public async Task CustomerServiceAgent_HandlesUnknownInquiry()
    {
        // Arrange
        var agent = new CustomerServiceAgent("cs-1", "Customer Service", _logger);
        var message = new AgentMessage
        {
            Category = "CustomerService",
            Subject = "Question",
            Content = "Random question that doesn't match any keyword"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("24 hours", result.Response);
    }

    [Test]
    public void CustomerServiceAgent_HasCorrectCapabilities()
    {
        // Arrange
        var agent = new CustomerServiceAgent("cs-1", "Customer Service", _logger);

        // Assert
        Assert.True(agent.Capabilities.SupportsCategory("CustomerService"));
        Assert.True(agent.Capabilities.SupportsCategory("GeneralInquiry"));
        Assert.True(agent.Capabilities.SupportsCategory("ProductInfo"));
        Assert.True(agent.Capabilities.HasSkill("CustomerSupport"));
        Assert.True(agent.Capabilities.HasSkill("ProductKnowledge"));
        Assert.True(agent.Capabilities.HasSkill("OrderTracking"));
    }

    #endregion

    #region TechnicalSupportAgent Tests

    [Test]
    public async Task TechnicalSupportAgent_HandlesLoginIssues()
    {
        // Arrange
        var agent = new TechnicalSupportAgent("tech-1", "Tech Support", _logger);
        var message = new AgentMessage
        {
            Category = "TechnicalSupport",
            Subject = "Login Issue",
            Content = "I can't login to my account"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("password", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task TechnicalSupportAgent_HandlesPasswordIssues()
    {
        // Arrange
        var agent = new TechnicalSupportAgent("tech-1", "Tech Support", _logger);
        var message = new AgentMessage
        {
            Category = "TechnicalSupport",
            Subject = "Password Reset",
            Content = "I forgot my password"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("reset", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task TechnicalSupportAgent_HandlesPerformanceIssues()
    {
        // Arrange
        var agent = new TechnicalSupportAgent("tech-1", "Tech Support", _logger);
        var message = new AgentMessage
        {
            Category = "TechnicalSupport",
            Subject = "Slow Performance",
            Content = "The application is running slow"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("cache", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task TechnicalSupportAgent_HandlesFreezeIssues()
    {
        // Arrange
        var agent = new TechnicalSupportAgent("tech-1", "Tech Support", _logger);
        var message = new AgentMessage
        {
            Category = "TechnicalSupport",
            Subject = "Application Freeze",
            Content = "The app keeps freeze issues" // Must contain exact "freeze" keyword
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("troubleshooting", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task TechnicalSupportAgent_HandlesGenericKnownIssues()
    {
        // Arrange
        var agent = new TechnicalSupportAgent("tech-1", "Tech Support", _logger);
        var message = new AgentMessage
        {
            Category = "TechnicalSupport",
            Subject = "Error",
            Content = "I'm getting an error message"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("logged", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task TechnicalSupportAgent_CreatesTicketForUnknownIssues()
    {
        // Arrange
        var agent = new TechnicalSupportAgent("tech-1", "Tech Support", _logger);
        var message = new AgentMessage
        {
            Category = "TechnicalSupport",
            Subject = "Unknown Issue",
            Content = "Something random happened"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("TECH-", result.Response);
        Assert.Contains("4 business hours", result.Response);
    }

    [Test]
    public void TechnicalSupportAgent_HasCorrectCapabilities()
    {
        // Arrange
        var agent = new TechnicalSupportAgent("tech-1", "Tech Support", _logger);

        // Assert
        Assert.True(agent.Capabilities.SupportsCategory("TechnicalSupport"));
        Assert.True(agent.Capabilities.SupportsCategory("Bug"));
        Assert.True(agent.Capabilities.SupportsCategory("Troubleshooting"));
        Assert.True(agent.Capabilities.HasSkill("Debugging"));
        Assert.True(agent.Capabilities.HasSkill("SystemDiagnostics"));
        Assert.True(agent.Capabilities.HasSkill("SoftwareSupport"));
    }

    #endregion

    #region BillingAgent Tests

    [Test]
    public async Task BillingAgent_HandlesRefundRequests()
    {
        // Arrange
        var agent = new BillingAgent("bill-1", "Billing", _logger);
        var message = new AgentMessage
        {
            Category = "Billing",
            Subject = "Refund Request",
            Content = "I want a refund for my order"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("5-7 business days", result.Response);
    }

    [Test]
    public async Task BillingAgent_HandlesInvoiceRequests()
    {
        // Arrange
        var agent = new BillingAgent("bill-1", "Billing", _logger);
        var message = new AgentMessage
        {
            Category = "Billing",
            Subject = "Invoice Request",
            Content = "I need a copy of my invoice"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("email", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task BillingAgent_HandlesReceiptRequests()
    {
        // Arrange
        var agent = new BillingAgent("bill-1", "Billing", _logger);
        var message = new AgentMessage
        {
            Category = "Billing",
            Subject = "Receipt",
            Content = "Where can I get my receipt?"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("dashboard", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task BillingAgent_HandlesChargeInquiries()
    {
        // Arrange
        var agent = new BillingAgent("bill-1", "Billing", _logger);
        var message = new AgentMessage
        {
            Category = "Billing",
            Subject = "Charge",
            Content = "Why was I charged this amount?"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("investigate", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task BillingAgent_HandlesPaymentInquiries()
    {
        // Arrange
        var agent = new BillingAgent("bill-1", "Billing", _logger);
        var message = new AgentMessage
        {
            Category = "Billing",
            Subject = "Payment",
            Content = "My payment didn't go through"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("24 hours", result.Response);
    }

    [Test]
    public async Task BillingAgent_HandlesGenericInquiries()
    {
        // Arrange
        var agent = new BillingAgent("bill-1", "Billing", _logger);
        var message = new AgentMessage
        {
            Category = "Billing",
            Subject = "Question",
            Content = "General billing question"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("billing@", result.Response);
    }

    [Test]
    public void BillingAgent_HasCorrectCapabilities()
    {
        // Arrange
        var agent = new BillingAgent("bill-1", "Billing", _logger);

        // Assert
        Assert.True(agent.Capabilities.SupportsCategory("Billing"));
        Assert.True(agent.Capabilities.SupportsCategory("Payment"));
        Assert.True(agent.Capabilities.SupportsCategory("Invoice"));
        Assert.True(agent.Capabilities.SupportsCategory("Refund"));
        Assert.True(agent.Capabilities.HasSkill("PaymentProcessing"));
        Assert.True(agent.Capabilities.HasSkill("Invoicing"));
        Assert.True(agent.Capabilities.HasSkill("RefundManagement"));
    }

    #endregion

    #region SupervisorAgent Tests

    [Test]
    public async Task SupervisorAgent_HandlesEscalatedIssues()
    {
        // Arrange
        var agent = new SupervisorAgent("sup-1", "Supervisor", _logger);
        var message = new AgentMessage
        {
            Category = "Escalation",
            Subject = "Escalated Issue",
            Content = "This issue was escalated"
        };

        // Act
        var result = await agent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Case #", result.Response);
        Assert.Contains("2 business hours", result.Response);
    }

    [Test]
    public async Task SupervisorAgent_TracksEscalatedIssues()
    {
        // Arrange
        var agent = new SupervisorAgent("sup-1", "Supervisor", _logger);
        var message1 = new AgentMessage { Category = "Escalation", Subject = "Issue 1", Content = "Content 1" };
        var message2 = new AgentMessage { Category = "Escalation", Subject = "Issue 2", Content = "Content 2" };

        // Act
        await agent.ProcessMessageAsync(message1, CancellationToken.None);
        await agent.ProcessMessageAsync(message2, CancellationToken.None);
        var issues = agent.GetEscalatedIssues();

        // Assert
        Assert.Equal(2, issues.Count);
    }

    [Test]
    public void SupervisorAgent_HasLimitedConcurrentMessages()
    {
        // Arrange
        var agent = new SupervisorAgent("sup-1", "Supervisor", _logger);

        // Assert
        Assert.Equal(5, agent.Capabilities.MaxConcurrentMessages);
    }

    [Test]
    public void SupervisorAgent_HasCorrectCapabilities()
    {
        // Arrange
        var agent = new SupervisorAgent("sup-1", "Supervisor", _logger);

        // Assert
        Assert.True(agent.Capabilities.SupportsCategory("Escalation"));
        Assert.True(agent.Capabilities.SupportsCategory("Complaint"));
        Assert.True(agent.Capabilities.SupportsCategory("Complex"));
        Assert.True(agent.Capabilities.HasSkill("ConflictResolution"));
        Assert.True(agent.Capabilities.HasSkill("ManagementDecisions"));
        Assert.True(agent.Capabilities.HasSkill("PolicyExceptions"));
    }

    #endregion

    #region TriageAgent Tests

    [Test]
    public async Task TriageAgent_ClassifiesBillingMessages()
    {
        // Arrange
        var router = CreateRouterWithAgents();
        var triageAgent = new TriageAgent("triage-1", "Triage", _logger, router);
        var message = new AgentMessage
        {
            Category = "Triage",
            Subject = "Help",
            Content = "I have a bill question"
        };

        // Act
        var result = await triageAgent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Billing", message.Category);
    }

    [Test]
    public async Task TriageAgent_ClassifiesTechnicalMessages()
    {
        // Arrange
        var router = CreateRouterWithAgents();
        var triageAgent = new TriageAgent("triage-1", "Triage", _logger, router);
        var message = new AgentMessage
        {
            Category = "Triage",
            Subject = "Help",
            Content = "I'm getting an error"
        };

        // Act
        var result = await triageAgent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("TechnicalSupport", message.Category);
    }

    [Test]
    public async Task TriageAgent_ClassifiesEscalations()
    {
        // Arrange
        var router = CreateRouterWithAgents();
        var triageAgent = new TriageAgent("triage-1", "Triage", _logger, router);
        var message = new AgentMessage
        {
            Category = "Triage",
            Subject = "Complaint",
            Content = "I want to file a complaint"
        };

        // Act
        var result = await triageAgent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Escalation", message.Category);
    }

    [Test]
    public async Task TriageAgent_ClassifiesCustomerService()
    {
        // Arrange
        var router = CreateRouterWithAgents();
        var triageAgent = new TriageAgent("triage-1", "Triage", _logger, router);
        var message = new AgentMessage
        {
            Category = "Triage",
            Subject = "Question",
            Content = "I have a general question"
        };

        // Act
        var result = await triageAgent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("CustomerService", message.Category);
    }

    [Test]
    public async Task TriageAgent_SetsUrgentPriority()
    {
        // Arrange
        var router = CreateRouterWithAgents();
        var triageAgent = new TriageAgent("triage-1", "Triage", _logger, router);
        var message = new AgentMessage
        {
            Category = "Triage",
            Subject = "Help",
            Content = "This is urgent!"
        };

        // Act
        await triageAgent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.Equal(MessagePriority.Urgent, message.Priority);
    }

    [Test]
    public async Task TriageAgent_SetsHighPriority()
    {
        // Arrange
        var router = CreateRouterWithAgents();
        var triageAgent = new TriageAgent("triage-1", "Triage", _logger, router);
        var message = new AgentMessage
        {
            Category = "Triage",
            Subject = "Help",
            Content = "This is important"
        };

        // Act
        await triageAgent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.Equal(MessagePriority.High, message.Priority);
    }

    [Test]
    public async Task TriageAgent_HandlesNoAvailableAgent()
    {
        // Arrange
        var router = new AgentRouter(
            _logger,
            new MiddlewarePipeline(),
            new RulesEngineCore<RoutingContext>());
        // No agents registered
        var triageAgent = new TriageAgent("triage-1", "Triage", _logger, router);
        var message = new AgentMessage
        {
            Category = "Triage",
            Subject = "Help",
            Content = "General question"
        };

        // Act
        var result = await triageAgent.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("high volume", result.Response);
    }

    [Test]
    public void TriageAgent_HasCorrectCapabilities()
    {
        // Arrange
        var router = CreateRouterWithAgents();
        var agent = new TriageAgent("triage-1", "Triage", _logger, router);

        // Assert
        Assert.True(agent.Capabilities.SupportsCategory("Triage"));
        Assert.True(agent.Capabilities.HasSkill("Classification"));
        Assert.True(agent.Capabilities.HasSkill("Routing"));
        Assert.True(agent.Capabilities.HasSkill("PriorityAssessment"));
    }

    #endregion

    private AgentRouter CreateRouterWithAgents()
    {
        var router = new AgentRouter(
            _logger,
            new MiddlewarePipeline(),
            new RulesEngineCore<RoutingContext>());

        router.RegisterAgent(new CustomerServiceAgent("cs-1", "Customer Service", _logger));
        router.RegisterAgent(new TechnicalSupportAgent("tech-1", "Tech Support", _logger));
        router.RegisterAgent(new BillingAgent("bill-1", "Billing", _logger));
        router.RegisterAgent(new SupervisorAgent("sup-1", "Supervisor", _logger));

        return router;
    }
}
