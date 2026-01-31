namespace AgentRouting.Agents;

using AgentRouting.Core;

/// <summary>
/// Agent that handles customer service inquiries
/// </summary>
public class CustomerServiceAgent : AgentBase
{
    private readonly Dictionary<string, string> _knowledgeBase;
    
    public CustomerServiceAgent(string id, string name, IAgentLogger logger) 
        : base(id, name, logger)
    {
        Capabilities.SupportedCategories.AddRange(new[] 
        { 
            "CustomerService", 
            "GeneralInquiry", 
            "ProductInfo" 
        });
        
        Capabilities.Skills.AddRange(new[] 
        { 
            "CustomerSupport", 
            "ProductKnowledge", 
            "OrderTracking" 
        });
        
        _knowledgeBase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["hours"] = "Our business hours are Monday-Friday, 9 AM - 5 PM EST.",
            ["shipping"] = "Standard shipping takes 5-7 business days. Express shipping is available for an additional fee.",
            ["returns"] = "We accept returns within 30 days of purchase. Items must be in original condition.",
            ["payment"] = "We accept all major credit cards, PayPal, and Apple Pay.",
            ["tracking"] = "You can track your order using the tracking number sent to your email."
        };
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message, 
        CancellationToken ct)
    {
        await Task.Delay(100, ct); // Simulate processing time
        
        // Check if we have a canned response
        foreach (var (keyword, response) in _knowledgeBase)
        {
            if (message.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return MessageResult.Ok($"Thank you for contacting us! {response}");
            }
        }
        
        // Check if this needs escalation
        if (message.Priority == MessagePriority.Urgent || 
            message.Content.Contains("complaint", StringComparison.OrdinalIgnoreCase))
        {
            var escalated = ForwardMessage(message, "supervisor-agent", 
                "This inquiry requires supervisor attention.");
            return MessageResult.Forward(escalated, 
                "Your inquiry has been escalated to a supervisor who will contact you shortly.");
        }
        
        return MessageResult.Ok(
            "Thank you for your inquiry. A customer service representative will respond within 24 hours.");
    }
}

/// <summary>
/// Agent that handles technical support requests
/// </summary>
public class TechnicalSupportAgent : AgentBase
{
    private readonly HashSet<string> _knownIssues;
    
    public TechnicalSupportAgent(string id, string name, IAgentLogger logger) 
        : base(id, name, logger)
    {
        Capabilities.SupportedCategories.AddRange(new[] 
        { 
            "TechnicalSupport", 
            "Bug", 
            "Troubleshooting" 
        });
        
        Capabilities.Skills.AddRange(new[] 
        { 
            "Debugging", 
            "SystemDiagnostics", 
            "SoftwareSupport" 
        });
        
        _knownIssues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "login", "password", "connection", "error", "crash", "slow", "freeze"
        };
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message, 
        CancellationToken ct)
    {
        await Task.Delay(150, ct);
        
        var contentLower = message.Content.ToLowerInvariant();
        
        // Check for known issues
        if (_knownIssues.Any(issue => contentLower.Contains(issue)))
        {
            if (contentLower.Contains("login") || contentLower.Contains("password"))
            {
                return MessageResult.Ok(
                    "To reset your password:\n" +
                    "1. Click 'Forgot Password' on the login page\n" +
                    "2. Enter your email address\n" +
                    "3. Check your email for reset instructions");
            }
            
            if (contentLower.Contains("slow") || contentLower.Contains("freeze"))
            {
                return MessageResult.Ok(
                    "Performance troubleshooting steps:\n" +
                    "1. Clear your browser cache\n" +
                    "2. Disable browser extensions\n" +
                    "3. Check your internet connection\n" +
                    "4. Try using incognito mode");
            }
            
            return MessageResult.Ok(
                "Our technical team has logged your issue. " +
                "A support ticket has been created and you'll receive updates via email.");
        }
        
        // Unknown issue - create ticket
        var ticketId = $"TECH-{DateTime.UtcNow.Ticks}";
        return MessageResult.Ok(
            $"Support ticket {ticketId} has been created. " +
            $"Our team will investigate and contact you within 4 business hours.");
    }
}

/// <summary>
/// Agent that handles billing and payment inquiries
/// </summary>
public class BillingAgent : AgentBase
{
    public BillingAgent(string id, string name, IAgentLogger logger) 
        : base(id, name, logger)
    {
        Capabilities.SupportedCategories.AddRange(new[] 
        { 
            "Billing", 
            "Payment", 
            "Invoice",
            "Refund"
        });
        
        Capabilities.Skills.AddRange(new[] 
        { 
            "PaymentProcessing", 
            "Invoicing", 
            "RefundManagement" 
        });
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message, 
        CancellationToken ct)
    {
        await Task.Delay(100, ct);
        
        var contentLower = message.Content.ToLowerInvariant();
        
        if (contentLower.Contains("refund"))
        {
            return MessageResult.Ok(
                "Refund requests are processed within 5-7 business days. " +
                "Please provide your order number and we'll initiate the refund process.");
        }
        
        if (contentLower.Contains("invoice") || contentLower.Contains("receipt"))
        {
            return MessageResult.Ok(
                "Invoices are automatically sent to your registered email address. " +
                "You can also download them from your account dashboard.");
        }
        
        if (contentLower.Contains("charge") || contentLower.Contains("payment"))
        {
            return MessageResult.Ok(
                "Please provide your transaction details and we'll investigate the charge. " +
                "Our billing team will respond within 24 hours.");
        }
        
        return MessageResult.Ok(
            "For billing inquiries, please contact our billing department at billing@company.com " +
            "or call 1-800-BILLING during business hours.");
    }
}

/// <summary>
/// Supervisor agent that handles escalated issues
/// </summary>
public class SupervisorAgent : AgentBase
{
    private readonly List<AgentMessage> _escalatedIssues = new();
    
    public SupervisorAgent(string id, string name, IAgentLogger logger) 
        : base(id, name, logger)
    {
        Capabilities.SupportedCategories.AddRange(new[] 
        { 
            "Escalation", 
            "Complaint", 
            "Complex" 
        });
        
        Capabilities.Skills.AddRange(new[] 
        { 
            "ConflictResolution", 
            "ManagementDecisions", 
            "PolicyExceptions" 
        });
        
        Capabilities.MaxConcurrentMessages = 5; // Supervisors handle fewer messages
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message, 
        CancellationToken ct)
    {
        await Task.Delay(200, ct); // Supervisors take more time
        
        _escalatedIssues.Add(message);
        
        return MessageResult.Ok(
            $"Your issue has been received by a supervisor (Case #{_escalatedIssues.Count:D6}). " +
            "We take all escalated issues seriously and will personally review your case. " +
            "You can expect a response from our management team within 2 business hours.");
    }
    
    public IReadOnlyList<AgentMessage> GetEscalatedIssues() => _escalatedIssues.AsReadOnly();
}

/// <summary>
/// Triage agent that classifies and routes messages
/// </summary>
public class TriageAgent : AgentBase
{
    private readonly AgentRouter _router;
    
    public TriageAgent(string id, string name, IAgentLogger logger, AgentRouter router) 
        : base(id, name, logger)
    {
        _router = router;
        
        Capabilities.SupportedCategories.Add("Triage");
        Capabilities.Skills.AddRange(new[] 
        { 
            "Classification", 
            "Routing", 
            "PriorityAssessment" 
        });
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message, 
        CancellationToken ct)
    {
        await Task.Delay(50, ct); // Quick classification
        
        // Classify the message
        var category = ClassifyMessage(message);
        message.Category = category;
        
        // Assess priority
        message.Priority = AssessPriority(message);
        
        // Find the best agent
        var targetAgent = FindBestAgent(category);
        
        if (targetAgent == null)
        {
            return MessageResult.Ok(
                "Thank you for your message. We're experiencing high volume and will respond shortly.");
        }
        
        // Forward to the appropriate agent
        var forwarded = ForwardMessage(message, targetAgent.Id);
        return MessageResult.Forward(forwarded, 
            $"Your {category.ToLower()} request has been forwarded to our {targetAgent.Name} team.");
    }
    
    private string ClassifyMessage(AgentMessage message)
    {
        var content = message.Content.ToLowerInvariant();
        
        if (content.Contains("bill") || content.Contains("payment") || content.Contains("refund"))
            return "Billing";
        
        if (content.Contains("bug") || content.Contains("error") || content.Contains("crash") || content.Contains("not working"))
            return "TechnicalSupport";
        
        if (content.Contains("complaint") || content.Contains("urgent") || content.Contains("manager"))
            return "Escalation";
        
        return "CustomerService";
    }
    
    private MessagePriority AssessPriority(AgentMessage message)
    {
        var content = message.Content.ToLowerInvariant();
        
        if (content.Contains("urgent") || content.Contains("emergency") || content.Contains("critical"))
            return MessagePriority.Urgent;
        
        if (content.Contains("important") || content.Contains("asap"))
            return MessagePriority.High;
        
        return MessagePriority.Normal;
    }
    
    private IAgent? FindBestAgent(string category)
    {
        return _router.GetAllAgents()
            .Where(a => a.Capabilities.SupportsCategory(category))
            .OrderBy(a => a.Status == AgentStatus.Busy ? 1 : 0)
            .FirstOrDefault();
    }
}

/// <summary>
/// Analytics agent that collects and reports on message patterns
/// </summary>
public class AnalyticsAgent : AgentBase
{
    private readonly Dictionary<string, int> _categoryCount = new();
    private readonly Dictionary<string, int> _agentWorkload = new();
    private int _totalMessages = 0;
    
    public AnalyticsAgent(string id, string name, IAgentLogger logger) 
        : base(id, name, logger)
    {
        Capabilities.SupportedCategories.Add("Analytics");
        Capabilities.Skills.AddRange(new[] { "DataAnalysis", "Reporting" });
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message, 
        CancellationToken ct)
    {
        await Task.Delay(10, ct); // Very quick
        
        _totalMessages++;
        
        // Track categories
        if (!string.IsNullOrEmpty(message.Category))
        {
            _categoryCount[message.Category] = _categoryCount.GetValueOrDefault(message.Category, 0) + 1;
        }
        
        // Track agent workload
        if (!string.IsNullOrEmpty(message.ReceiverId))
        {
            _agentWorkload[message.ReceiverId] = _agentWorkload.GetValueOrDefault(message.ReceiverId, 0) + 1;
        }
        
        return MessageResult.Ok($"Logged message analytics (Total: {_totalMessages})");
    }
    
    public string GenerateReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Analytics Report ===");
        report.AppendLine($"Total Messages Processed: {_totalMessages}");
        report.AppendLine();
        
        report.AppendLine("Messages by Category:");
        foreach (var (category, count) in _categoryCount.OrderByDescending(x => x.Value))
        {
            report.AppendLine($"  {category}: {count} ({count * 100.0 / _totalMessages:F1}%)");
        }
        
        report.AppendLine();
        report.AppendLine("Agent Workload:");
        foreach (var (agentId, count) in _agentWorkload.OrderByDescending(x => x.Value))
        {
            report.AppendLine($"  {agentId}: {count} messages");
        }
        
        return report.ToString();
    }
}
