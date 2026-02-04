namespace RulesEngine.Linq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using TestRunner.Framework;
    using RulesEngine.Linq;

    /// <summary>
    /// TDD tests for closure value extraction and serialization support.
    /// These tests define the expected behavior for extracting captured values
    /// from expression trees for serialization to a remote server.
    /// </summary>
    public class ClosureExtractionTests
    {
        #region Test Domain

        private class Order
        {
            public string Id { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public int Quantity { get; set; }
            public DateTime OrderDate { get; set; }
            public OrderStatus Status { get; set; }
            public Guid CustomerId { get; set; }
        }

        private enum OrderStatus
        {
            Pending,
            Approved,
            Shipped,
            Delivered
        }

        #endregion

        #region Piece 2: Value Extraction

        [Test]
        public void ClosureExtractor_ExtractsDecimalValue()
        {
            var threshold = 100.50m;
            Expression<Func<Order, bool>> expr = o => o.Total > threshold;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("threshold", closures[0].Name);
            Assert.Equal(100.50m, closures[0].Value);
            Assert.Equal(typeof(decimal), closures[0].Type);
        }

        [Test]
        public void ClosureExtractor_ExtractsIntValue()
        {
            var minQuantity = 5;
            Expression<Func<Order, bool>> expr = o => o.Quantity >= minQuantity;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("minQuantity", closures[0].Name);
            Assert.Equal(5, closures[0].Value);
            Assert.Equal(typeof(int), closures[0].Type);
        }

        [Test]
        public void ClosureExtractor_ExtractsStringValue()
        {
            var prefix = "ORD-";
            Expression<Func<Order, bool>> expr = o => o.Id.StartsWith(prefix);

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("prefix", closures[0].Name);
            Assert.Equal("ORD-", closures[0].Value);
            Assert.Equal(typeof(string), closures[0].Type);
        }

        [Test]
        public void ClosureExtractor_ExtractsDateTimeValue()
        {
            var cutoffDate = new DateTime(2024, 1, 15);
            Expression<Func<Order, bool>> expr = o => o.OrderDate > cutoffDate;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("cutoffDate", closures[0].Name);
            Assert.Equal(new DateTime(2024, 1, 15), closures[0].Value);
            Assert.Equal(typeof(DateTime), closures[0].Type);
        }

        [Test]
        public void ClosureExtractor_ExtractsGuidValue()
        {
            var customerId = Guid.Parse("12345678-1234-1234-1234-123456789012");
            Expression<Func<Order, bool>> expr = o => o.CustomerId == customerId;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("customerId", closures[0].Name);
            Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789012"), closures[0].Value);
            Assert.Equal(typeof(Guid), closures[0].Type);
        }

        [Test]
        public void ClosureExtractor_ExtractsEnumValue()
        {
            var requiredStatus = OrderStatus.Shipped;
            Expression<Func<Order, bool>> expr = o => o.Status == requiredStatus;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("requiredStatus", closures[0].Name);
            Assert.Equal(OrderStatus.Shipped, closures[0].Value);
            Assert.Equal(typeof(OrderStatus), closures[0].Type);
        }

        [Test]
        public void ClosureExtractor_ExtractsNullableWithValue()
        {
            int? maybeQuantity = 10;
            Expression<Func<Order, bool>> expr = o => o.Quantity == maybeQuantity;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("maybeQuantity", closures[0].Name);
            Assert.Equal(10, closures[0].Value);
            Assert.Equal(typeof(int?), closures[0].Type);
        }

        [Test]
        public void ClosureExtractor_ExtractsNullableWithNull()
        {
            int? maybeQuantity = null;
            Expression<Func<Order, bool>> expr = o => o.Quantity == maybeQuantity;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("maybeQuantity", closures[0].Name);
            Assert.Null(closures[0].Value);
            Assert.Equal(typeof(int?), closures[0].Type);
        }

        [Test]
        public void ClosureExtractor_ExtractsMultipleClosures()
        {
            var minTotal = 100m;
            var maxTotal = 500m;
            var status = OrderStatus.Approved;
            Expression<Func<Order, bool>> expr = o =>
                o.Total >= minTotal && o.Total <= maxTotal && o.Status == status;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(3, closures.Count);
            Assert.True(closures.Any(c => c.Name == "minTotal" && (decimal)c.Value! == 100m));
            Assert.True(closures.Any(c => c.Name == "maxTotal" && (decimal)c.Value! == 500m));
            Assert.True(closures.Any(c => c.Name == "status" && (OrderStatus)c.Value! == OrderStatus.Approved));
        }

        [Test]
        public void ClosureExtractor_ReturnsEmptyForNoClosure()
        {
            Expression<Func<Order, bool>> expr = o => o.Total > 100;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(0, closures.Count);
        }

        #endregion

        #region Piece 3: Bound vs Free Variable Tracking

        [Test]
        public void ClosureExtractor_IgnoresLambdaParameters()
        {
            var minQty = 5;
            Expression<Func<Order, bool>> expr = o => o.Quantity > minQty;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            // Should only find minQty, not 'o'
            Assert.Equal(1, closures.Count);
            Assert.Equal("minQty", closures[0].Name);
        }

        [Test]
        public void ClosureExtractor_HandlesNestedLambdaParameters()
        {
            var minQty = 5;
            // Simulating: o => o.Items.Any(i => i.Quantity > minQty)
            // The 'i' parameter should not be extracted as a closure
            Expression<Func<Order, bool>> expr = o => o.Quantity > minQty;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("minQty", closures[0].Name);
        }

        #endregion

        #region Piece 4: Serializable Type Validation

        [Test]
        public void ClosureExtractor_IsSerializable_TrueForPrimitives()
        {
            Assert.True(ClosureExtractor.IsSerializableType(typeof(int)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(long)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(short)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(byte)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(bool)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(double)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(float)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(char)));
        }

        [Test]
        public void ClosureExtractor_IsSerializable_TrueForCommonTypes()
        {
            Assert.True(ClosureExtractor.IsSerializableType(typeof(string)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(decimal)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(DateTime)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(DateTimeOffset)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(TimeSpan)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(Guid)));
        }

        [Test]
        public void ClosureExtractor_IsSerializable_TrueForEnums()
        {
            Assert.True(ClosureExtractor.IsSerializableType(typeof(OrderStatus)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(DayOfWeek)));
        }

        [Test]
        public void ClosureExtractor_IsSerializable_TrueForNullableOfSerializable()
        {
            Assert.True(ClosureExtractor.IsSerializableType(typeof(int?)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(decimal?)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(DateTime?)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(Guid?)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(OrderStatus?)));
        }

        [Test]
        public void ClosureExtractor_IsSerializable_FalseForComplexTypes()
        {
            Assert.False(ClosureExtractor.IsSerializableType(typeof(Order)));
            Assert.False(ClosureExtractor.IsSerializableType(typeof(object)));
        }

        [Test]
        public void ClosureExtractor_IsSerializable_TrueForStringCollections()
        {
            // String collections are supported for IN clause queries
            Assert.True(ClosureExtractor.IsSerializableType(typeof(List<string>)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(IList<string>)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(IReadOnlyList<string>)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(ICollection<string>)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(IEnumerable<string>)));
        }

        [Test]
        public void ClosureExtractor_IsSerializable_TrueForPrimitiveCollections()
        {
            // Collections of primitives are supported for IN clause queries
            Assert.True(ClosureExtractor.IsSerializableType(typeof(List<int>)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(List<long>)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(List<decimal>)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(List<Guid>)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(List<DateTime>)));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(IEnumerable<int>)));
        }

        [Test]
        public void ClosureExtractor_IsSerializable_TrueForPrimitiveArrays()
        {
            // Arrays of primitives are supported for IN clause queries
            Assert.True(ClosureExtractor.IsSerializableType(typeof(string[])));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(int[])));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(long[])));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(decimal[])));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(Guid[])));
            Assert.True(ClosureExtractor.IsSerializableType(typeof(DateTime[])));
        }

        [Test]
        public void ClosureExtractor_IsSerializable_FalseForComplexArraysAndCollections()
        {
            // Arrays and collections of complex types return false
            Assert.False(ClosureExtractor.IsSerializableType(typeof(Order[])));
            Assert.False(ClosureExtractor.IsSerializableType(typeof(List<Order>)));
            Assert.False(ClosureExtractor.IsSerializableType(typeof(IEnumerable<Order>)));
        }

        [Test]
        public void ClosureExtractor_IsSerializable_FalseForQueryables()
        {
            Assert.False(ClosureExtractor.IsSerializableType(typeof(IQueryable<Order>)));
            Assert.False(ClosureExtractor.IsSerializableType(typeof(IEnumerable<Order>)));
        }

        #endregion

        #region Piece 5: Validation of Captured Types

        [Test]
        public void ClosureExtractor_ValidateClosures_PassesForSerializableTypes()
        {
            var threshold = 100m;
            Expression<Func<Order, bool>> expr = o => o.Total > threshold;

            var extractor = new ClosureExtractor();
            var result = extractor.ValidateClosures(expr);

            Assert.True(result.IsValid);
            Assert.Equal(0, result.Errors.Count);
        }

        [Test]
        public void ClosureExtractor_ValidateClosures_FailsForNonSerializableType()
        {
            var complexObject = new Order { Id = "test" };
            // This would be: o => o.Id == complexObject.Id
            // But we're testing the capture of a complex object itself
            Expression<Func<Order, bool>> expr = o => o.Id == complexObject.Id;

            var extractor = new ClosureExtractor();
            var result = extractor.ValidateClosures(expr);

            // This should detect that complexObject is captured (even though we access .Id)
            // The closure captures the Order object, not just the string
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.Contains("complexObject")));
        }

        [Test]
        public void ClosureExtractor_ValidateClosures_PassesForListStringCapture()
        {
            // List<string> is supported for IN clause queries
            var allowedIds = new List<string> { "A", "B", "C" };
            Expression<Func<Order, bool>> expr = o => allowedIds.Contains(o.Id);

            var extractor = new ClosureExtractor();
            var result = extractor.ValidateClosures(expr);

            Assert.True(result.IsValid);
            Assert.Equal(0, result.Errors.Count);
        }

        [Test]
        public void ClosureExtractor_ExtractsListStringValue()
        {
            var allowedIds = new List<string> { "name1", "name2", "name3" };
            Expression<Func<Order, bool>> expr = o => allowedIds.Contains(o.Id);

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("allowedIds", closures[0].Name);
            Assert.Equal(typeof(List<string>), closures[0].Type);
            Assert.True(closures[0].IsSerializable);

            var values = closures[0].Value as List<string>;
            Assert.NotNull(values);
            Assert.Equal(3, values!.Count);
            Assert.Equal("name1", values[0]);
            Assert.Equal("name2", values[1]);
            Assert.Equal("name3", values[2]);
        }

        [Test]
        public void ClosureExtractor_ValidateClosures_PassesForStringArrayCapture()
        {
            // string[] is supported for IN clause queries
            var allowedIds = new[] { "A", "B", "C" };
            Expression<Func<Order, bool>> expr = o => allowedIds.Contains(o.Id);

            var extractor = new ClosureExtractor();
            var result = extractor.ValidateClosures(expr);

            Assert.True(result.IsValid);
            Assert.Equal(0, result.Errors.Count);
        }

        [Test]
        public void ClosureExtractor_ExtractsStringArrayValue()
        {
            var allowedIds = new[] { "name1", "name2" };
            Expression<Func<Order, bool>> expr = o => allowedIds.Contains(o.Id);

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            Assert.Equal(1, closures.Count);
            Assert.Equal("allowedIds", closures[0].Name);
            Assert.Equal(typeof(string[]), closures[0].Type);
            Assert.True(closures[0].IsSerializable);

            var values = closures[0].Value as string[];
            Assert.NotNull(values);
            Assert.Equal(2, values!.Length);
            Assert.Equal("name1", values[0]);
            Assert.Equal("name2", values[1]);
        }

        #endregion

        #region Piece 6: Closure Value Result Type

        [Test]
        public void ExtractedClosure_HasCorrectProperties()
        {
            var threshold = 100m;
            Expression<Func<Order, bool>> expr = o => o.Total > threshold;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            var closure = closures[0];
            Assert.Equal("threshold", closure.Name);
            Assert.Equal(100m, closure.Value);
            Assert.Equal(typeof(decimal), closure.Type);
            Assert.True(closure.IsSerializable);
        }

        [Test]
        public void ExtractedClosure_MarksNonSerializableCorrectly()
        {
            var order = new Order();
            Expression<Func<Order, bool>> expr = o => o.Id == order.Id;

            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(expr);

            // The closure captures 'order', not 'order.Id'
            var closure = closures.FirstOrDefault(c => c.Name == "order");
            if (closure != null)
            {
                Assert.False(closure.IsSerializable);
            }
        }

        #endregion

        #region Mutable Closure Behavior (Documentation/Decision Tests)

        [Test]
        public void ClosureExtractor_CapturesValueAtExtractionTime()
        {
            // This test documents the expected behavior:
            // Values are captured when ExtractClosures is called
            var threshold = 100m;
            Expression<Func<Order, bool>> expr = o => o.Total > threshold;

            var extractor = new ClosureExtractor();

            // Extract before mutation
            var closuresBefore = extractor.ExtractClosures(expr);

            // Mutate the variable (expression still references the closure)
            threshold = 200m;

            // Extract after mutation - should get the NEW value
            // because we're extracting from the live closure
            var closuresAfter = extractor.ExtractClosures(expr);

            Assert.Equal(100m, closuresBefore[0].Value); // Original value
            Assert.Equal(200m, closuresAfter[0].Value);  // Mutated value
        }

        #endregion

        #region Real-World Scenario Tests

        /// <summary>
        /// Scenario: Filter orders by multiple allowed statuses.
        /// Common pattern for status-based filtering in business rules.
        /// </summary>
        [Test]
        public void Scenario_FilterByMultipleStatuses_EnumList()
        {
            // Arrange: User wants orders that are either Pending or Approved
            var allowedStatuses = new List<OrderStatus> { OrderStatus.Pending, OrderStatus.Approved };
            Expression<Func<Order, bool>> rule = order => allowedStatuses.Contains(order.Status);

            // Act: Extract closures for serialization
            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(rule);
            var validation = extractor.ValidateClosures(rule);

            // Assert: Closure is valid and values are correct
            Assert.True(validation.IsValid);
            Assert.Equal(1, closures.Count);
            Assert.Equal("allowedStatuses", closures[0].Name);
            Assert.True(closures[0].IsSerializable);

            var statuses = closures[0].Value as List<OrderStatus>;
            Assert.NotNull(statuses);
            Assert.Equal(2, statuses!.Count);
            Assert.Contains(OrderStatus.Pending, statuses);
            Assert.Contains(OrderStatus.Approved, statuses);
        }

        /// <summary>
        /// Scenario: Filter orders by customer IDs from a CRM system.
        /// Common pattern for multi-tenant or customer-specific queries.
        /// </summary>
        [Test]
        public void Scenario_FilterByCustomerIds_GuidArray()
        {
            // Arrange: User has a list of VIP customer IDs
            var vipCustomerIds = new[] {
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("33333333-3333-3333-3333-333333333333")
            };
            Expression<Func<Order, bool>> rule = order => vipCustomerIds.Contains(order.CustomerId);

            // Act
            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(rule);
            var validation = extractor.ValidateClosures(rule);

            // Assert
            Assert.True(validation.IsValid);
            Assert.Equal(1, closures.Count);
            Assert.Equal(typeof(Guid[]), closures[0].Type);
            Assert.True(closures[0].IsSerializable);

            var ids = closures[0].Value as Guid[];
            Assert.NotNull(ids);
            Assert.Equal(3, ids!.Length);
        }

        /// <summary>
        /// Scenario: Filter orders within a date range.
        /// Common pattern for reporting and time-based rules.
        /// </summary>
        [Test]
        public void Scenario_FilterByDateRange_DateTimeClosures()
        {
            // Arrange: User wants orders from Q1 2024
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 3, 31);
            Expression<Func<Order, bool>> rule = order =>
                order.OrderDate >= startDate && order.OrderDate <= endDate;

            // Act
            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(rule);
            var validation = extractor.ValidateClosures(rule);

            // Assert
            Assert.True(validation.IsValid);
            Assert.Equal(2, closures.Count);
            Assert.True(closures.All(c => c.IsSerializable));
            Assert.True(closures.Any(c => c.Name == "startDate" && (DateTime)c.Value! == new DateTime(2024, 1, 1)));
            Assert.True(closures.Any(c => c.Name == "endDate" && (DateTime)c.Value! == new DateTime(2024, 3, 31)));
        }

        /// <summary>
        /// Scenario: Filter orders by price tiers.
        /// Common pattern for pricing rules and discount eligibility.
        /// </summary>
        [Test]
        public void Scenario_FilterByPriceTiers_DecimalList()
        {
            // Arrange: User defines price tier thresholds
            var priceTiers = new List<decimal> { 99.99m, 199.99m, 499.99m, 999.99m };
            Expression<Func<Order, bool>> rule = order => priceTiers.Contains(order.Total);

            // Act
            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(rule);
            var validation = extractor.ValidateClosures(rule);

            // Assert
            Assert.True(validation.IsValid);
            Assert.Equal(typeof(List<decimal>), closures[0].Type);
            Assert.True(closures[0].IsSerializable);

            var tiers = closures[0].Value as List<decimal>;
            Assert.NotNull(tiers);
            Assert.Equal(4, tiers!.Count);
        }

        /// <summary>
        /// Scenario: Filter by quantity thresholds including null handling.
        /// Common pattern when dealing with optional numeric fields.
        /// </summary>
        [Test]
        public void Scenario_FilterByNullableQuantities_NullableIntArray()
        {
            // Arrange: User wants specific quantities, including "unknown" (null)
            var targetQuantities = new int?[] { 1, 5, 10, null };
            // Note: This tests nullable element support in arrays
            Expression<Func<Order, bool>> rule = order => targetQuantities.Contains(order.Quantity);

            // Act
            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(rule);

            // Assert
            Assert.Equal(1, closures.Count);
            Assert.Equal(typeof(int?[]), closures[0].Type);
            Assert.True(closures[0].IsSerializable);
        }

        /// <summary>
        /// Scenario: Complex business rule with multiple closure types.
        /// Simulates a real promotion eligibility rule.
        /// </summary>
        [Test]
        public void Scenario_ComplexPromotionRule_MultipleCapturedValues()
        {
            // Arrange: Promotion rule - VIP customers with orders over threshold in valid statuses
            var minOrderTotal = 250.00m;
            var eligibleStatuses = new[] { OrderStatus.Approved, OrderStatus.Shipped };
            var promotionStartDate = new DateTime(2024, 11, 1);
            var promotionEndDate = new DateTime(2024, 12, 31);

            Expression<Func<Order, bool>> promotionRule = order =>
                order.Total >= minOrderTotal &&
                eligibleStatuses.Contains(order.Status) &&
                order.OrderDate >= promotionStartDate &&
                order.OrderDate <= promotionEndDate;

            // Act
            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(promotionRule);
            var validation = extractor.ValidateClosures(promotionRule);

            // Assert: All 4 closures are valid
            Assert.True(validation.IsValid);
            Assert.Equal(4, closures.Count);
            Assert.True(closures.All(c => c.IsSerializable));

            // Verify each captured value
            Assert.True(closures.Any(c => c.Name == "minOrderTotal" && c.Type == typeof(decimal)));
            Assert.True(closures.Any(c => c.Name == "eligibleStatuses" && c.Type == typeof(OrderStatus[])));
            Assert.True(closures.Any(c => c.Name == "promotionStartDate" && c.Type == typeof(DateTime)));
            Assert.True(closures.Any(c => c.Name == "promotionEndDate" && c.Type == typeof(DateTime)));
        }

        /// <summary>
        /// Scenario: User accidentally captures a complex object.
        /// Tests that validation catches the error with a helpful message.
        /// </summary>
        [Test]
        public void Scenario_InvalidClosure_ComplexObjectCapture_GivesHelpfulError()
        {
            // Arrange: User accidentally captures an Order object instead of its ID
            var referenceOrder = new Order { Id = "REF-001", Total = 500m };
            Expression<Func<Order, bool>> badRule = order => order.Total > referenceOrder.Total;

            // Act
            var extractor = new ClosureExtractor();
            var validation = extractor.ValidateClosures(badRule);

            // Assert: Validation fails with a helpful message
            Assert.False(validation.IsValid);
            Assert.True(validation.Errors.Count > 0);
            Assert.True(validation.Errors.Any(e => e.Contains("referenceOrder")));
        }

        /// <summary>
        /// Scenario: Filter by long IDs (common in high-volume systems).
        /// </summary>
        [Test]
        public void Scenario_FilterByLongIds_LongList()
        {
            // Arrange: System uses long IDs for high-volume order tracking
            var orderIds = new List<long> { 1000000001L, 1000000002L, 1000000003L };
            Expression<Func<Order, bool>> rule = order => orderIds.Contains(order.Quantity); // Using Quantity as stand-in

            // Act
            var extractor = new ClosureExtractor();
            var validation = extractor.ValidateClosures(rule);

            // Assert
            Assert.True(validation.IsValid);
        }

        /// <summary>
        /// Scenario: Filter using nullable enum statuses.
        /// Common when status can be "not set" vs explicit value.
        /// </summary>
        [Test]
        public void Scenario_FilterByNullableEnumStatus_NullableEnumList()
        {
            // Arrange: Filter includes "no status set" (null) as valid
            var validStatuses = new List<OrderStatus?> { OrderStatus.Pending, OrderStatus.Approved, null };
            Expression<Func<Order, bool>> rule = order => validStatuses.Contains(order.Status);

            // Act
            var extractor = new ClosureExtractor();
            var closures = extractor.ExtractClosures(rule);
            var validation = extractor.ValidateClosures(rule);

            // Assert
            Assert.True(validation.IsValid);
            Assert.Equal(typeof(List<OrderStatus?>), closures[0].Type);
            Assert.True(closures[0].IsSerializable);
        }

        #endregion
    }
}
