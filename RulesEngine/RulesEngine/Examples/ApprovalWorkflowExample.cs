namespace RulesEngine.Examples;

/// <summary>
/// Purchase request model
/// </summary>
public class PurchaseRequest
{
    public string RequestId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Department { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsUrgent { get; set; }
    
    // Approval workflow fields
    public string ApprovalLevel { get; set; } = ""; // Manager, Director, CFO, CEO
    public bool RequiresAdditionalReview { get; set; }
    public string ApprovalReason { get; set; } = "";
    public List<string> RequiredApprovers { get; set; } = new();
}

/// <summary>
/// Example: Approval workflow rules engine
/// </summary>
public static class ApprovalWorkflowExample
{
    public static Core.RulesEngineCore<PurchaseRequest> CreateApprovalEngine()
    {
        var engine = new Core.RulesEngineCore<PurchaseRequest>(new Core.RulesEngineOptions
        {
            StopOnFirstMatch = true, // Stop after determining approval level
            TrackPerformance = true
        });
        
        // Rule 1: CEO approval required for large purchases
        var ceoApprovalRule = new Core.RuleBuilder<PurchaseRequest>()
            .WithId("APPROVAL_CEO")
            .WithName("CEO Approval Required")
            .WithDescription("Purchases over $100,000 require CEO approval")
            .WithPriority(100)
            .When(request => request.Amount > 100000)
            .Then(request =>
            {
                request.ApprovalLevel = "CEO";
                request.RequiredApprovers.Add("CEO");
                request.ApprovalReason = "Amount exceeds $100,000";
            })
            .Build();
        
        // Rule 2: CFO approval for financial department or large amounts
        var cfoApprovalRule = new Core.RuleBuilder<PurchaseRequest>()
            .WithId("APPROVAL_CFO")
            .WithName("CFO Approval Required")
            .WithDescription("Finance purchases or amounts over $50,000 require CFO")
            .WithPriority(90)
            .When(request => request.Amount > 50000)
            .Or(request => request.Department == "Finance")
            .Then(request =>
            {
                request.ApprovalLevel = "CFO";
                request.RequiredApprovers.Add("CFO");
                request.ApprovalReason = request.Amount > 50000
                    ? "Amount exceeds $50,000"
                    : "Finance department purchase";
            })
            .Build();
        
        // Rule 3: Director approval for medium purchases
        var directorApprovalRule = new Core.RuleBuilder<PurchaseRequest>()
            .WithId("APPROVAL_DIRECTOR")
            .WithName("Director Approval Required")
            .WithDescription("Purchases over $10,000 require Director approval")
            .WithPriority(80)
            .When(request => request.Amount > 10000)
            .Then(request =>
            {
                request.ApprovalLevel = "Director";
                request.RequiredApprovers.Add("Director");
                request.ApprovalReason = "Amount exceeds $10,000";
            })
            .Build();
        
        // Rule 4: Manager approval for standard purchases
        var managerApprovalRule = new Core.RuleBuilder<PurchaseRequest>()
            .WithId("APPROVAL_MANAGER")
            .WithName("Manager Approval Required")
            .WithDescription("All purchases require at least Manager approval")
            .WithPriority(70)
            .When(request => request.Amount > 0)
            .Then(request =>
            {
                request.ApprovalLevel = "Manager";
                request.RequiredApprovers.Add("Manager");
                request.ApprovalReason = "Standard approval process";
            })
            .Build();
        
        // Rule 5: Additional review for IT equipment over $25,000
        var itReviewRule = new Core.RuleBuilder<PurchaseRequest>()
            .WithId("REVIEW_IT_EQUIPMENT")
            .WithName("IT Equipment Review")
            .WithDescription("Large IT purchases need technical review")
            .WithPriority(95)
            .When(request => request.Category == "IT Equipment")
            .And(request => request.Amount > 25000)
            .Then(request =>
            {
                request.RequiresAdditionalReview = true;
                request.RequiredApprovers.Add("IT Director");
            })
            .Build();
        
        // Rule 6: Urgent request escalation
        var urgentEscalationRule = new Core.RuleBuilder<PurchaseRequest>()
            .WithId("URGENT_ESCALATION")
            .WithName("Urgent Request Escalation")
            .WithDescription("Urgent requests over $5,000 get escalated")
            .WithPriority(85)
            .When(request => request.IsUrgent && request.Amount > 5000)
            .Then(request =>
            {
                if (!request.RequiredApprovers.Contains("Director"))
                {
                    request.RequiredApprovers.Add("Director");
                }
                request.ApprovalReason += " [URGENT - Escalated]";
            })
            .Build();
        
        // Register evaluation rules (stop on first match for approval level)
        engine.RegisterRules(
            ceoApprovalRule,
            cfoApprovalRule,
            directorApprovalRule,
            managerApprovalRule
        );
        
        // Note: For additional rules like IT review and urgent escalation,
        // you'd run a second pass or use a different engine configuration
        
        return engine;
    }
    
    public static Core.RulesEngineCore<PurchaseRequest> CreateAdditionalReviewEngine()
    {
        var engine = new Core.RulesEngineCore<PurchaseRequest>(new Core.RulesEngineOptions
        {
            StopOnFirstMatch = false, // Apply all review rules
            TrackPerformance = true
        });
        
        // Rule: IT equipment review
        var itReviewRule = new Core.RuleBuilder<PurchaseRequest>()
            .WithId("REVIEW_IT_EQUIPMENT")
            .WithName("IT Equipment Review")
            .When(request => request.Category == "IT Equipment" && request.Amount > 25000)
            .Then(request =>
            {
                request.RequiresAdditionalReview = true;
                request.RequiredApprovers.Add("IT Director");
            })
            .Build();
        
        // Rule: Urgent escalation
        var urgentRule = new Core.RuleBuilder<PurchaseRequest>()
            .WithId("URGENT_ESCALATION")
            .WithName("Urgent Escalation")
            .When(request => request.IsUrgent && request.Amount > 5000)
            .Then(request =>
            {
                if (request.ApprovalLevel != "Director" && 
                    request.ApprovalLevel != "CFO" && 
                    request.ApprovalLevel != "CEO")
                {
                    request.RequiredApprovers.Add("Director");
                }
            })
            .Build();
        
        engine.RegisterRules(itReviewRule, urgentRule);
        return engine;
    }
}
