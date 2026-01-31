namespace RulesEngine.Examples;

/// <summary>
/// E-commerce order model
/// </summary>
public class Order
{
    public string OrderId { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string CustomerType { get; set; } = "Regular"; // Regular, Premium, VIP
    public int ItemCount { get; set; }
    public bool IsFirstOrder { get; set; }
    public string ProductCategory { get; set; } = "";
    public decimal DiscountAmount { get; set; }
    public string DiscountReason { get; set; } = "";
    public bool FreeShipping { get; set; }
}

/// <summary>
/// Example: E-commerce discount rules engine
/// </summary>
public static class DiscountRulesExample
{
    public static Core.RulesEngineCore<Order> CreateDiscountEngine()
    {
        var engine = new Core.RulesEngineCore<Order>(new Core.RulesEngineOptions
        {
            StopOnFirstMatch = false, // Apply all applicable discounts
            TrackPerformance = true
        });
        
        // Rule 1: VIP Customer - 20% discount
        var vipRule = new Core.RuleBuilder<Order>()
            .WithId("DISCOUNT_VIP")
            .WithName("VIP Customer Discount")
            .WithDescription("VIP customers get 20% off all orders")
            .WithPriority(100)
            .When(order => order.CustomerType == "VIP")
            .Then(order =>
            {
                var discount = order.TotalAmount * 0.20m;
                order.DiscountAmount += discount;
                order.DiscountReason += "VIP 20% discount; ";
            })
            .Build();
        
        // Rule 2: Large order discount - $50 off orders over $500
        var largeOrderRule = new Core.RuleBuilder<Order>()
            .WithId("DISCOUNT_LARGE_ORDER")
            .WithName("Large Order Discount")
            .WithDescription("Orders over $500 get $50 off")
            .WithPriority(90)
            .When(order => order.TotalAmount > 500)
            .Then(order =>
            {
                order.DiscountAmount += 50m;
                order.DiscountReason += "$50 large order discount; ";
            })
            .Build();
        
        // Rule 3: First order discount - 15% off
        var firstOrderRule = new Core.RuleBuilder<Order>()
            .WithId("DISCOUNT_FIRST_ORDER")
            .WithName("First Order Discount")
            .WithDescription("First-time customers get 15% off")
            .WithPriority(80)
            .When(order => order.IsFirstOrder)
            .Then(order =>
            {
                var discount = order.TotalAmount * 0.15m;
                order.DiscountAmount += discount;
                order.DiscountReason += "First order 15% discount; ";
            })
            .Build();
        
        // Rule 4: Electronics bundle - buy 3+ items, get 10% off
        var electronicsBundleRule = new Core.RuleBuilder<Order>()
            .WithId("DISCOUNT_ELECTRONICS_BUNDLE")
            .WithName("Electronics Bundle Discount")
            .WithDescription("3+ electronics items get 10% off")
            .WithPriority(70)
            .When(order => order.ProductCategory == "Electronics")
            .And(order => order.ItemCount >= 3)
            .Then(order =>
            {
                var discount = order.TotalAmount * 0.10m;
                order.DiscountAmount += discount;
                order.DiscountReason += "Electronics bundle 10% discount; ";
            })
            .Build();
        
        // Rule 5: Free shipping for premium customers with orders over $100
        var freeShippingRule = new Core.RuleBuilder<Order>()
            .WithId("FREE_SHIPPING_PREMIUM")
            .WithName("Premium Free Shipping")
            .WithDescription("Premium customers get free shipping on $100+ orders")
            .WithPriority(60)
            .When(order => order.CustomerType == "Premium" || order.CustomerType == "VIP")
            .And(order => order.TotalAmount >= 100)
            .Then(order =>
            {
                order.FreeShipping = true;
                order.DiscountReason += "Free shipping; ";
            })
            .Build();
        
        engine.RegisterRules(
            vipRule,
            largeOrderRule,
            firstOrderRule,
            electronicsBundleRule,
            freeShippingRule
        );
        
        return engine;
    }
}
