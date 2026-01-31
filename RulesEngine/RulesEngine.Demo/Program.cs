using RulesEngine.Core;
using RulesEngine.Examples;

namespace RulesEngine.Demo;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          Rules Engine - Interactive Demo                  ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Demo1_BasicRule();
        Demo2_MultipleRules();
        Demo3_DynamicRules();
        Demo4_DiscountEngine();
        Demo5_ApprovalWorkflow();
        Demo6_PerformanceTracking();

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("Demo complete! Check the tests for 30+ more examples.");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static void Demo1_BasicRule()
    {
        Console.WriteLine("═══ Demo 1: Basic Rule ═══");
        Console.WriteLine();

        // Create a simple rule
        var rule = new RuleBuilder<Order>()
            .WithName("Large Order Discount")
            .When(order => order.TotalAmount > 500)
            .Then(order =>
            {
                order.DiscountAmount = 50;
                order.DiscountReason = "$50 discount for orders over $500";
            })
            .Build();

        // Test with different orders
        var smallOrder = new Order { OrderId = "O1", TotalAmount = 300 };
        var largeOrder = new Order { OrderId = "O2", TotalAmount = 600 };

        rule.Execute(smallOrder);
        rule.Execute(largeOrder);

        Console.WriteLine($"Small order (${smallOrder.TotalAmount}): Discount = ${smallOrder.DiscountAmount}");
        Console.WriteLine($"Large order (${largeOrder.TotalAmount}): Discount = ${largeOrder.DiscountAmount}");
        Console.WriteLine($"Reason: {largeOrder.DiscountReason}");
        Console.WriteLine();
    }

    static void Demo2_MultipleRules()
    {
        Console.WriteLine("═══ Demo 2: Multiple Rules with Priority ═══");
        Console.WriteLine();

        var engine = new RulesEngineCore<Order>();

        // Add rules with different priorities
        engine.RegisterRule(new RuleBuilder<Order>()
            .WithName("VIP Discount")
            .WithPriority(100) // Highest priority
            .When(order => order.CustomerType == "VIP")
            .Then(order => order.DiscountAmount += order.TotalAmount * 0.20m)
            .Build());

        engine.RegisterRule(new RuleBuilder<Order>()
            .WithName("Large Order Discount")
            .WithPriority(50)
            .When(order => order.TotalAmount > 500)
            .Then(order => order.DiscountAmount += 50)
            .Build());

        var order = new Order
        {
            OrderId = "O3",
            TotalAmount = 1000,
            CustomerType = "VIP"
        };

        var result = engine.Execute(order);

        Console.WriteLine($"Order Total: ${order.TotalAmount}");
        Console.WriteLine($"Rules Evaluated: {result.TotalRulesEvaluated}");
        Console.WriteLine($"Rules Matched: {result.MatchedRules}");
        Console.WriteLine($"Total Discount: ${order.DiscountAmount}");
        Console.WriteLine($"Final Amount: ${order.TotalAmount - order.DiscountAmount}");
        Console.WriteLine();
    }

    static void Demo3_DynamicRules()
    {
        Console.WriteLine("═══ Demo 3: Dynamic Rule Creation ═══");
        Console.WriteLine();

        // Create rules from runtime data (simulating JSON/config)
        var definitions = new List<RuleDefinition>
        {
            new RuleDefinition
            {
                Id = "PREMIUM_CUSTOMER",
                Name = "Premium Customer Benefits",
                Priority = 90,
                Conditions = new List<ConditionDefinition>
                {
                    new ConditionDefinition
                    {
                        PropertyName = "CustomerType",
                        Operator = "==",
                        Value = "Premium",
                        LogicalOperator = "AND"
                    },
                    new ConditionDefinition
                    {
                        PropertyName = "TotalAmount",
                        Operator = ">",
                        Value = 100m,
                        LogicalOperator = "AND"
                    }
                }
            }
        };

        var rules = DynamicRuleFactory.CreateRulesFromDefinitions<Order>(definitions);
        var engine = new RulesEngineCore<Order>();
        
        foreach (var rule in rules)
        {
            engine.RegisterRule(rule.WithAction(order =>
            {
                order.FreeShipping = true;
                order.DiscountReason = "Premium customer benefits applied";
            }));
        }

        var order = new Order
        {
            OrderId = "O4",
            TotalAmount = 150,
            CustomerType = "Premium"
        };

        var result = engine.Execute(order);

        Console.WriteLine($"Customer Type: {order.CustomerType}");
        Console.WriteLine($"Order Amount: ${order.TotalAmount}");
        Console.WriteLine($"Free Shipping: {order.FreeShipping}");
        Console.WriteLine($"Reason: {order.DiscountReason}");
        Console.WriteLine();
    }

    static void Demo4_DiscountEngine()
    {
        Console.WriteLine("═══ Demo 4: E-Commerce Discount Engine ═══");
        Console.WriteLine();

        var engine = DiscountRulesExample.CreateDiscountEngine();

        // Test different scenarios
        var scenarios = new[]
        {
            new Order
            {
                OrderId = "S1",
                TotalAmount = 1500,
                CustomerType = "VIP",
                ItemCount = 1,
                ProductCategory = "General"
            },
            new Order
            {
                OrderId = "S2",
                TotalAmount = 400,
                CustomerType = "Regular",
                IsFirstOrder = true,
                ItemCount = 1,
                ProductCategory = "General"
            },
            new Order
            {
                OrderId = "S3",
                TotalAmount = 500,
                CustomerType = "Regular",
                ItemCount = 4,
                ProductCategory = "Electronics"
            }
        };

        foreach (var order in scenarios)
        {
            var originalTotal = order.TotalAmount;
            var result = engine.Execute(order);

            Console.WriteLine($"Order {order.OrderId}:");
            Console.WriteLine($"  Original: ${originalTotal:F2}");
            Console.WriteLine($"  Discount: ${order.DiscountAmount:F2}");
            Console.WriteLine($"  Final: ${originalTotal - order.DiscountAmount:F2}");
            Console.WriteLine($"  Rules Applied: {result.MatchedRules}");
            Console.WriteLine($"  Free Shipping: {order.FreeShipping}");
            if (!string.IsNullOrEmpty(order.DiscountReason))
            {
                Console.WriteLine($"  Reasons: {order.DiscountReason.TrimEnd(';', ' ')}");
            }
            Console.WriteLine();
        }
    }

    static void Demo5_ApprovalWorkflow()
    {
        Console.WriteLine("═══ Demo 5: Purchase Approval Workflow ═══");
        Console.WriteLine();

        var approvalEngine = ApprovalWorkflowExample.CreateApprovalEngine();
        var reviewEngine = ApprovalWorkflowExample.CreateAdditionalReviewEngine();

        var requests = new[]
        {
            new PurchaseRequest
            {
                RequestId = "PR1",
                Amount = 5000,
                Department = "Marketing",
                Category = "Advertising",
                IsUrgent = false
            },
            new PurchaseRequest
            {
                RequestId = "PR2",
                Amount = 75000,
                Department = "Operations",
                Category = "Equipment",
                IsUrgent = false
            },
            new PurchaseRequest
            {
                RequestId = "PR3",
                Amount = 30000,
                Department = "IT",
                Category = "IT Equipment",
                IsUrgent = true
            },
            new PurchaseRequest
            {
                RequestId = "PR4",
                Amount = 150000,
                Department = "Facilities",
                Category = "Renovation",
                IsUrgent = false
            }
        };

        foreach (var request in requests)
        {
            // First pass: determine approval level
            approvalEngine.Execute(request);
            
            // Second pass: check for additional reviews
            reviewEngine.Execute(request);

            Console.WriteLine($"Request {request.RequestId} (${request.Amount:N0}):");
            Console.WriteLine($"  Department: {request.Department}");
            Console.WriteLine($"  Approval Level: {request.ApprovalLevel}");
            Console.WriteLine($"  Required Approvers: {string.Join(", ", request.RequiredApprovers)}");
            if (request.RequiresAdditionalReview)
            {
                Console.WriteLine($"  Additional Review: Required");
            }
            if (request.IsUrgent)
            {
                Console.WriteLine($"  Status: URGENT");
            }
            Console.WriteLine();
        }
    }

    static void Demo6_PerformanceTracking()
    {
        Console.WriteLine("═══ Demo 6: Performance Tracking ═══");
        Console.WriteLine();

        var engine = new RulesEngineCore<Order>(new RulesEngineOptions
        {
            TrackPerformance = true
        });

        // Add some rules
        engine.RegisterRules(
            new RuleBuilder<Order>()
                .WithId("RULE1")
                .WithName("Rule 1")
                .When(order => order.TotalAmount > 100)
                .Build(),
            new RuleBuilder<Order>()
                .WithId("RULE2")
                .WithName("Rule 2")
                .When(order => order.CustomerType == "VIP")
                .Build(),
            new RuleBuilder<Order>()
                .WithId("RULE3")
                .WithName("Rule 3")
                .When(order => order.ItemCount > 5)
                .Build()
        );

        // Execute multiple times
        Console.WriteLine("Executing 1000 orders...");
        var totalTime = TimeSpan.Zero;
        
        for (int i = 0; i < 1000; i++)
        {
            var order = new Order
            {
                OrderId = $"O{i}",
                TotalAmount = Random.Shared.Next(50, 1000),
                CustomerType = i % 3 == 0 ? "VIP" : "Regular",
                ItemCount = Random.Shared.Next(1, 10)
            };

            var result = engine.Execute(order);
            totalTime += result.TotalExecutionTime;
        }

        Console.WriteLine($"Total execution time: {totalTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Average per order: {totalTime.TotalMilliseconds / 1000:F4}ms");
        Console.WriteLine();
        Console.WriteLine("Rule Performance Metrics:");
        
        var allMetrics = engine.GetAllMetrics();
        foreach (var (ruleId, metrics) in allMetrics)
        {
            Console.WriteLine($"  {ruleId}:");
            Console.WriteLine($"    Executions: {metrics.ExecutionCount}");
            Console.WriteLine($"    Average: {metrics.AverageExecutionTime.TotalMicroseconds:F2}μs");
            Console.WriteLine($"    Min: {metrics.MinExecutionTime.TotalMicroseconds:F2}μs");
            Console.WriteLine($"    Max: {metrics.MaxExecutionTime.TotalMicroseconds:F2}μs");
        }
        Console.WriteLine();
    }
}
