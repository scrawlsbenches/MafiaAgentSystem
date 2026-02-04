namespace RulesEngine.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Validates expressions for LINQ provider compatibility.
    /// Rejects patterns that cannot be translated by providers like EF Core.
    /// </summary>
    public class ExpressionValidator : ExpressionVisitor
    {
        private List<string>? _errors;

        public static readonly IReadOnlySet<string> TranslatableMethods = new HashSet<string>
        {
            // Queryable/Enumerable
            "Where", "Select", "SelectMany", "OrderBy", "OrderByDescending",
            "ThenBy", "ThenByDescending", "Take", "Skip", "Distinct",
            "First", "FirstOrDefault", "Single", "SingleOrDefault",
            "Last", "LastOrDefault", "Count", "LongCount", "Any", "All",
            "Sum", "Average", "Min", "Max", "GroupBy", "Join", "GroupJoin",
            "Contains", "Concat", "Union", "Intersect", "Except",
            "Zip", "SequenceEqual", "DefaultIfEmpty", "Reverse",
            "Cast", "OfType", "ToList", "ToArray", "AsQueryable", "AsEnumerable",

            // String
            "Contains", "StartsWith", "EndsWith", "ToLower", "ToUpper",
            "ToLowerInvariant", "ToUpperInvariant",
            "Trim", "TrimStart", "TrimEnd", "Substring", "Replace",
            "IndexOf", "LastIndexOf", "PadLeft", "PadRight",
            "IsNullOrEmpty", "IsNullOrWhiteSpace",
            "Split", "Join", "Concat", "Format",
            "Equals", "CompareTo", "Compare",

            // Math
            "Abs", "Acos", "Asin", "Atan", "Atan2", "Ceiling", "Cos",
            "Exp", "Floor", "Log", "Log10", "Pow", "Round", "Sign",
            "Sin", "Sqrt", "Tan", "Truncate", "Min", "Max",

            // DateTime
            "AddDays", "AddMonths", "AddYears", "AddHours", "AddMinutes",
            "AddSeconds", "AddMilliseconds", "AddTicks",
            "Parse", "TryParse", "ToString",

            // Object
            "Equals", "ToString", "GetType", "CompareTo", "GetHashCode",

            // Nullable
            "GetValueOrDefault", "HasValue",

            // Convert
            "ToInt16", "ToInt32", "ToInt64", "ToDouble", "ToDecimal",
            "ToSingle", "ToByte", "ToSByte", "ToBoolean", "ToChar", "ToString",
            "ChangeType"
        };

        public void Validate(Expression expression)
        {
            _errors = new List<string>();
            Visit(expression);

            if (_errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Expression cannot be translated: {string.Join("; ", _errors)}");
            }
        }

        public IReadOnlyList<string> GetErrors(Expression expression)
        {
            _errors = new List<string>();
            Visit(expression);
            return _errors;
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            _errors!.Add("InvocationExpression not supported by LINQ providers");
            return base.VisitInvocation(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!IsTranslatable(node.Method))
            {
                _errors!.Add($"Method '{node.Method.DeclaringType?.Name}.{node.Method.Name}' not translatable");
            }
            return base.VisitMethodCall(node);
        }

        private static bool IsTranslatable(MethodInfo method)
        {
            var declaringType = method.DeclaringType;

            // Queryable and Enumerable are always allowed
            if (declaringType == typeof(Queryable)) return true;
            if (declaringType == typeof(Enumerable)) return true;

            // Check if method name is in whitelist and declaring type is supported
            if (!TranslatableMethods.Contains(method.Name))
                return false;

            // Supported declaring types
            if (declaringType == typeof(string)) return true;
            if (declaringType == typeof(Math)) return true;
            if (declaringType == typeof(DateTime)) return true;
            if (declaringType == typeof(DateTimeOffset)) return true;
            if (declaringType == typeof(TimeSpan)) return true;
            if (declaringType == typeof(object)) return true;
            if (declaringType == typeof(Convert)) return true;
            if (declaringType == typeof(Guid)) return true;

            // Nullable<T>
            if (declaringType?.IsGenericType == true &&
                declaringType.GetGenericTypeDefinition() == typeof(Nullable<>))
                return true;

            // Enum methods
            if (declaringType == typeof(Enum)) return true;

            return false;
        }
    }
}
