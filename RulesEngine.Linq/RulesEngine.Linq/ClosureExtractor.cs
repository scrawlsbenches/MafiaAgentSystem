namespace RulesEngine.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Represents a closure capture extracted from an expression.
    /// Contains the captured variable's name, value, type, and serializability.
    /// </summary>
    public class ExtractedClosure
    {
        public string Name { get; init; } = string.Empty;
        public object? Value { get; init; }
        public Type Type { get; init; } = typeof(object);
        public bool IsSerializable { get; init; }

        // TODO: Consider adding source location info for better error messages
        // TODO: Consider adding the original MemberExpression for reconstruction
    }

    /// <summary>
    /// Result of validating closures in an expression.
    /// </summary>
    public class ClosureValidationResult
    {
        public bool IsValid { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

        public static ClosureValidationResult Success() => new() { IsValid = true };

        public static ClosureValidationResult Failure(IReadOnlyList<string> errors) =>
            new() { IsValid = false, Errors = errors };
    }

    /// <summary>
    /// Extracts closure captures from expression trees for serialization.
    /// Used to identify captured variables that need to be serialized when
    /// sending expressions to a remote server.
    /// </summary>
    public class ClosureExtractor : ExpressionVisitor
    {
        private readonly List<ExtractedClosure> _closures = new();
        private readonly HashSet<ParameterExpression> _lambdaParameters = new();

        #region Serializable Type Whitelist

        /// <summary>
        /// Types that can be safely serialized for remote execution.
        /// Mirrors what EF Core can parameterize in SQL queries.
        /// </summary>
        private static readonly HashSet<Type> SerializableTypes = new()
        {
            // Primitive types
            typeof(bool),
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(char),

            // Common value types
            typeof(decimal),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(Guid),

            // String (reference type but serializable)
            typeof(string),
        };

        /// <summary>
        /// Checks if a type can be serialized for remote execution.
        /// </summary>
        public static bool IsSerializableType(Type type)
        {
            // Direct match
            if (SerializableTypes.Contains(type))
                return true;

            // Enums are serializable (will be sent as name or underlying value)
            if (type.IsEnum)
                return true;

            // Nullable<T> where T is serializable
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = Nullable.GetUnderlyingType(type);
                return underlyingType != null && IsSerializableType(underlyingType);
            }

            // Array support for IN clauses: .Where(x => myValues.Contains(x.Id))
            if (type.IsArray)
            {
                var elementType = type.GetElementType();

                // string[] is supported for IN clause queries
                if (elementType == typeof(string))
                    return true;

                // Future array types that may be supported:
                // - int[] for numeric IN clauses
                // - Guid[] for identifier IN clauses
                // - Other primitive arrays as needed
                throw new NotImplementedException(
                    $"Array type '{elementType?.Name}[]' is not yet supported for serialization. " +
                    $"Currently only string[] is supported for IN clause queries.");
            }

            // Generic collection support for IN clauses: .Where(x => myList.Contains(x.Id))
            // Supports List<T>, IList<T>, IReadOnlyList<T>, ICollection<T>, IEnumerable<T>
            // Excludes IQueryable<T> - those are subqueries, not IN clause collections
            if (type.IsGenericType)
            {
                // IQueryable<T> is a subquery reference, not a serializable collection
                if (typeof(IQueryable).IsAssignableFrom(type))
                    return false;

                var elementType = GetEnumerableElementType(type);
                if (elementType != null)
                {
                    // List<string>, IEnumerable<string>, etc. are supported for IN clause queries
                    if (elementType == typeof(string))
                        return true;

                    // Collections of complex types (Order, Customer, etc.) are not IN clause candidates
                    // They're likely navigation properties or subquery-like patterns - return false
                    if (!elementType.IsPrimitive && !SerializableTypes.Contains(elementType) && !elementType.IsEnum)
                        return false;

                    // Future collection element types that may be supported:
                    // - List<int>, IEnumerable<int> for numeric IN clauses
                    // - List<Guid>, IEnumerable<Guid> for identifier IN clauses
                    // - Other primitive element types as needed
                    throw new NotImplementedException(
                        $"Collection type '{GetFriendlyTypeName(type)}' is not yet supported for serialization. " +
                        $"Currently only string collections (List<string>, IEnumerable<string>, etc.) are supported for IN clause queries.");
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the element type if the type implements IEnumerable&lt;T&gt;.
        /// Returns null if not a generic enumerable.
        /// </summary>
        private static Type? GetEnumerableElementType(Type type)
        {
            // Check if it's IEnumerable<T> directly
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];

            // Check implemented interfaces for IEnumerable<T>
            var enumerableInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerableInterface?.GetGenericArguments()[0];
        }

        #endregion

        #region Closure Extraction

        /// <summary>
        /// Extracts all closure captures from an expression.
        /// Returns information about each captured variable including its current value.
        /// </summary>
        public IReadOnlyList<ExtractedClosure> ExtractClosures(Expression expression)
        {
            _closures.Clear();
            _lambdaParameters.Clear();
            Visit(expression);
            return _closures.ToList();
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            // Track lambda parameters so we don't confuse them with closures
            foreach (var param in node.Parameters)
                _lambdaParameters.Add(param);

            var result = base.VisitLambda(node);

            foreach (var param in node.Parameters)
                _lambdaParameters.Remove(param);

            return result;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // Closure pattern: MemberAccess on a ConstantExpression (the closure class instance)
            if (node.Expression is ConstantExpression ce && IsClosureClass(ce.Type))
            {
                var closure = ExtractClosureValue(node, ce);
                if (closure != null)
                {
                    _closures.Add(closure);
                }
            }

            return base.VisitMember(node);
        }

        /// <summary>
        /// Determines if a type is a compiler-generated closure class.
        /// </summary>
        private static bool IsClosureClass(Type type)
        {
            // Primitive types and common value types are not closure classes
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
                || type == typeof(DateTime) || type == typeof(Guid))
                return false;

            // Compiler-generated closure classes have specific naming patterns
            // but we can't rely solely on that. Instead, we check if it's a
            // non-primitive class that's used as the target of a member access.
            return true;
        }

        /// <summary>
        /// Extracts the value of a closure capture by evaluating the member access.
        /// </summary>
        private static ExtractedClosure? ExtractClosureValue(MemberExpression memberExpr, ConstantExpression closureExpr)
        {
            try
            {
                var closureInstance = closureExpr.Value;
                if (closureInstance == null)
                    return null;

                object? value = null;
                Type memberType;

                if (memberExpr.Member is FieldInfo field)
                {
                    value = field.GetValue(closureInstance);
                    memberType = field.FieldType;
                }
                else if (memberExpr.Member is PropertyInfo property)
                {
                    value = property.GetValue(closureInstance);
                    memberType = property.PropertyType;
                }
                else
                {
                    // Unknown member type
                    return null;
                }

                return new ExtractedClosure
                {
                    Name = memberExpr.Member.Name,
                    Value = value,
                    Type = memberType,
                    IsSerializable = IsSerializableType(memberType)
                };
            }
            catch
            {
                // TODO: Consider logging extraction failures
                // For now, return null to indicate extraction failed
                return null;
            }
        }

        #endregion

        #region Closure Validation

        /// <summary>
        /// Validates that all closures in an expression can be serialized.
        /// </summary>
        public ClosureValidationResult ValidateClosures(Expression expression)
        {
            var closures = ExtractClosures(expression);
            var errors = new List<string>();

            foreach (var closure in closures)
            {
                if (!closure.IsSerializable)
                {
                    var typeName = closure.Type.Name;
                    if (closure.Type.IsGenericType)
                    {
                        typeName = GetFriendlyTypeName(closure.Type);
                    }

                    errors.Add($"Captured variable '{closure.Name}' of type '{typeName}' cannot be serialized");
                }
            }

            return errors.Count == 0
                ? ClosureValidationResult.Success()
                : ClosureValidationResult.Failure(errors);
        }

        /// <summary>
        /// Gets a human-readable name for a type, including generic arguments.
        /// </summary>
        private static string GetFriendlyTypeName(Type type)
        {
            if (!type.IsGenericType)
                return type.Name;

            var genericTypeName = type.Name;
            var backtickIndex = genericTypeName.IndexOf('`');
            if (backtickIndex > 0)
                genericTypeName = genericTypeName.Substring(0, backtickIndex);

            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{genericTypeName}<{genericArgs}>";
        }

        #endregion
    }
}
