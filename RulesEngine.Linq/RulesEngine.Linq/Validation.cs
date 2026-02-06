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
        private ExpressionCapabilities _capabilities = new();
        private readonly HashSet<ParameterExpression> _lambdaParameters = new();

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
            Validate(expression, new ExpressionCapabilities());
        }

        public void Validate(Expression expression, ExpressionCapabilities capabilities)
        {
            _errors = new List<string>();
            _capabilities = capabilities;
            _lambdaParameters.Clear();
            Visit(expression);

            if (_errors.Count > 0)
            {
                throw new NotImplementedException(
                    $"Expression cannot be translated: {string.Join("; ", _errors)}. " +
                    $"To add support for a new method, add its name to TranslatableMethods and its " +
                    $"declaring type to IsTranslatable() in {nameof(ExpressionValidator)} (Validation.cs).");
            }
        }

        public IReadOnlyList<string> GetErrors(Expression expression)
        {
            return GetErrors(expression, new ExpressionCapabilities());
        }

        public IReadOnlyList<string> GetErrors(Expression expression, ExpressionCapabilities capabilities)
        {
            _errors = new List<string>();
            _capabilities = capabilities;
            _lambdaParameters.Clear();
            Visit(expression);
            return _errors;
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            _errors!.Add("InvocationExpression not supported by LINQ providers");
            return base.VisitInvocation(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            foreach (var param in node.Parameters)
                _lambdaParameters.Add(param);

            var result = base.VisitLambda(node);

            foreach (var param in node.Parameters)
                _lambdaParameters.Remove(param);

            return result;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // Detect closure captures
            if (node.Expression is ConstantExpression ce && !IsPrimitiveType(ce.Type))
            {
                if (!_capabilities.SupportsClosures)
                {
                    _errors!.Add($"Closure capture of '{node.Member.Name}' not supported");
                }

                // Check if the captured member is a queryable (subquery via closure)
                if (!_capabilities.SupportsSubqueries && IsQueryableType(node.Type))
                {
                    _errors!.Add($"Subquery reference to '{node.Member.Name}' not supported");
                }
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Check method call capability
            if (!_capabilities.SupportsMethodCalls && !IsQueryableOrEnumerable(node.Method.DeclaringType))
            {
                _errors!.Add($"Method calls not supported: '{node.Method.DeclaringType?.Name}.{node.Method.Name}'");
                return base.VisitMethodCall(node);
            }

            if (!IsTranslatable(node.Method))
            {
                _errors!.Add($"Method '{node.Method.DeclaringType?.Name}.{node.Method.Name}' not translatable");
            }

            // Check for subqueries - method calls on IQueryable that aren't the root
            if (!_capabilities.SupportsSubqueries && IsSubqueryMethodCall(node))
            {
                _errors!.Add($"Subqueries not supported: '{node.Method.Name}' on nested queryable");
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            // Detect subquery sources (IQueryable references that aren't the root)
            if (!_capabilities.SupportsSubqueries && IsQueryableType(node.Type))
            {
                _errors!.Add($"Subquery reference to '{node.Type.Name}' not supported");
            }

            return base.VisitConstant(node);
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

            // Collection types — methods like Contains, Add, Remove on ICollection<T>, ISet<T>, etc.
            if (declaringType?.IsGenericType == true)
            {
                var genericDef = declaringType.GetGenericTypeDefinition();
                if (genericDef == typeof(ICollection<>) ||
                    genericDef == typeof(ISet<>) ||
                    genericDef == typeof(IList<>) ||
                    genericDef == typeof(IReadOnlyCollection<>) ||
                    genericDef == typeof(IReadOnlyList<>) ||
                    genericDef == typeof(HashSet<>) ||
                    genericDef == typeof(List<>))
                    return true;
            }

            // Concrete types implementing collection interfaces
            if (declaringType != null && declaringType.IsGenericType)
            {
                foreach (var iface in declaringType.GetInterfaces())
                {
                    if (iface.IsGenericType)
                    {
                        var ifaceDef = iface.GetGenericTypeDefinition();
                        if (ifaceDef == typeof(ICollection<>) ||
                            ifaceDef == typeof(ISet<>) ||
                            ifaceDef == typeof(IList<>))
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool IsPrimitiveType(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
                || type == typeof(DateTime) || type == typeof(Guid);
        }

        private static bool IsQueryableOrEnumerable(Type? type)
        {
            if (type == null) return false;
            return type == typeof(Queryable) || type == typeof(Enumerable);
        }

        private static bool IsQueryableType(Type type)
        {
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(IQueryable<>)) return true;
                if (genericDef == typeof(IOrderedQueryable<>)) return true;
            }

            // Check for IRuleSet<T> or IFactSet<T>
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType)
                {
                    var genericDef = iface.GetGenericTypeDefinition();
                    if (genericDef == typeof(IQueryable<>)) return true;
                }
            }

            return type == typeof(IQueryable);
        }

        private static bool IsSubqueryMethodCall(MethodCallExpression node)
        {
            // A subquery is when a LINQ method is called on a queryable that's not a collection property
            if (!IsQueryableOrEnumerable(node.Method.DeclaringType))
                return false;

            // Check if the source is a constant queryable (indicates a separate data source)
            var source = node.Arguments.FirstOrDefault();
            if (source is ConstantExpression ce && IsQueryableType(ce.Type))
                return true;

            // Check if source is a method call that returns a queryable (chained subquery)
            if (source is MethodCallExpression mce && IsQueryableType(mce.Type))
                return IsSubqueryMethodCall(mce);

            return false;
        }
    }
}
