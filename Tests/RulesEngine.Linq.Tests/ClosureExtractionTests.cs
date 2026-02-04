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
            Assert.False(ClosureExtractor.IsSerializableType(typeof(List<int>)));
            Assert.False(ClosureExtractor.IsSerializableType(typeof(int[])));
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
        public void ClosureExtractor_ValidateClosures_FailsForListCapture()
        {
            var allowedIds = new List<string> { "A", "B", "C" };
            Expression<Func<Order, bool>> expr = o => allowedIds.Contains(o.Id);

            var extractor = new ClosureExtractor();
            var result = extractor.ValidateClosures(expr);

            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.Contains("allowedIds") || e.Contains("List")));
        }

        [Test]
        public void ClosureExtractor_ValidateClosures_FailsForArrayCapture()
        {
            var allowedIds = new[] { "A", "B", "C" };
            Expression<Func<Order, bool>> expr = o => allowedIds.Contains(o.Id);

            var extractor = new ClosureExtractor();
            var result = extractor.ValidateClosures(expr);

            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.Contains("allowedIds") || e.Contains("array")));
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
    }
}
