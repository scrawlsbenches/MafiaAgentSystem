using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using RulesEngine.Core;

namespace RulesEngine.Enhanced;

/// <summary>
/// Validates rules for common errors before execution
/// </summary>
public static class RuleValidator
{
    public static ValidationResult Validate<T>(IRule<T> rule)
    {
        var result = new ValidationResult();
        
        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            result.AddError("Rule ID cannot be empty");
        }
        
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            result.AddError("Rule name cannot be empty");
        }
        
        if (rule is Rule<T> concreteRule)
        {
            ValidateExpression(concreteRule.Condition, result);
        }
        
        return result;
    }
    
    private static void ValidateExpression<T>(
        Expression<Func<T, bool>> expression,
        ValidationResult result)
    {
        try
        {
            // Try to compile - this catches many errors
            var compiled = expression.Compile();
            
            // Deep validation
            var visitor = new ValidationVisitor(typeof(T));
            visitor.Visit(expression);
            
            if (visitor.Errors.Any())
            {
                result.AddErrors(visitor.Errors);
            }
            
            if (visitor.Warnings.Any())
            {
                result.AddWarnings(visitor.Warnings);
            }
        }
        catch (Exception ex)
        {
            result.AddError($"Expression compilation failed: {ex.Message}");
        }
    }
    
    private class ValidationVisitor : ExpressionVisitor
    {
        private readonly Type _factType;
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        
        public ValidationVisitor(Type factType)
        {
            _factType = factType;
        }
        
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member is PropertyInfo prop)
            {
                // Check if property exists on the fact type
                if (node.Expression?.Type == _factType)
                {
                    var actualProp = _factType.GetProperty(prop.Name);
                    if (actualProp == null)
                    {
                        Errors.Add($"Property '{prop.Name}' does not exist on type '{_factType.Name}'");
                    }
                    else if (!actualProp.CanRead)
                    {
                        Errors.Add($"Property '{prop.Name}' is not readable");
                    }
                }
                
                // Check for potential null reference
                if (node.Expression != null && !IsNullSafe(node.Expression))
                {
                    Warnings.Add($"Potential null reference accessing '{prop.Name}'");
                }
            }
            
            return base.VisitMember(node);
        }
        
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Warn about potentially expensive operations
            if (node.Method.Name == "Contains" && node.Method.DeclaringType == typeof(Enumerable))
            {
                Warnings.Add("Using LINQ Contains on collections can be slow - consider HashSet");
            }
            
            // Warn about database queries in memory
            if (node.Method.DeclaringType?.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true)
            {
                Errors.Add("Cannot use EF Core queries directly in rule expressions - materialize data first");
            }
            
            return base.VisitMethodCall(node);
        }
        
        protected override Expression VisitBinary(BinaryExpression node)
        {
            // Check for potential division by zero
            if (node.NodeType == ExpressionType.Divide)
            {
                if (node.Right is ConstantExpression constant &&
                    (Equals(constant.Value, 0) || Equals(constant.Value, 0.0)))
                {
                    Errors.Add("Division by zero detected");
                }
            }
            
            return base.VisitBinary(node);
        }
        
        private bool IsNullSafe(Expression expression)
        {
            // Simple null-safety check - could be more sophisticated
            return expression is ParameterExpression ||
                   expression is ConstantExpression { Value: not null };
        }
    }
}

public class ValidationResult
{
    public bool IsValid => !Errors.Any();
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    
    public void AddError(string error) => Errors.Add(error);
    public void AddErrors(IEnumerable<string> errors) => Errors.AddRange(errors);
    public void AddWarning(string warning) => Warnings.Add(warning);
    public void AddWarnings(IEnumerable<string> warnings) => Warnings.AddRange(warnings);
    
    public override string ToString()
    {
        var sb = new StringBuilder();
        
        if (Errors.Any())
        {
            sb.AppendLine("Errors:");
            foreach (var error in Errors)
            {
                sb.AppendLine($"  - {error}");
            }
        }
        
        if (Warnings.Any())
        {
            sb.AppendLine("Warnings:");
            foreach (var warning in Warnings)
            {
                sb.AppendLine($"  - {warning}");
            }
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Rule with enhanced debugging capabilities
/// Tracks WHY rules matched or didn't match
/// </summary>
public class DebuggableRule<T> : Rule<T>
{
    private readonly ThreadLocal<List<string>> _evaluationTrace = new(() => new List<string>());
    
    public IReadOnlyList<string> LastEvaluationTrace => _evaluationTrace.Value?.AsReadOnly() 
        ?? new List<string>().AsReadOnly();
    
    public DebuggableRule(
        string id,
        string name,
        Expression<Func<T, bool>> condition,
        string description = "",
        int priority = 0)
        : base(id, name, condition, description, priority)
    {
    }
    
    public new bool Evaluate(T fact)
    {
        _evaluationTrace.Value?.Clear();
        
        try
        {
            var result = base.Evaluate(fact);
            
            // Decompose and evaluate each part
            var parts = DecomposeExpression(Condition.Body);
            
            _evaluationTrace.Value?.Add($"Overall result: {result}");
            _evaluationTrace.Value?.Add("Breakdown:");
            
            foreach (var (expr, description) in parts)
            {
                try
                {
                    var partLambda = Expression.Lambda<Func<T, bool>>(expr, Condition.Parameters);
                    var partResult = partLambda.Compile()(fact);
                    _evaluationTrace.Value?.Add($"  {description} = {partResult}");
                }
                catch (Exception ex)
                {
                    _evaluationTrace.Value?.Add($"  {description} = ERROR: {ex.Message}");
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _evaluationTrace.Value?.Add($"EXCEPTION: {ex.Message}");
            _evaluationTrace.Value?.Add($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }
    
    private List<(Expression expr, string description)> DecomposeExpression(Expression expr)
    {
        var parts = new List<(Expression, string)>();
        DecomposeRecursive(expr, parts);
        return parts;
    }
    
    private void DecomposeRecursive(Expression expr, List<(Expression, string)> parts)
    {
        switch (expr)
        {
            case BinaryExpression binary:
                parts.Add((binary, FormatExpression(binary)));
                DecomposeRecursive(binary.Left, parts);
                DecomposeRecursive(binary.Right, parts);
                break;
                
            case MethodCallExpression method:
                parts.Add((method, FormatExpression(method)));
                break;
                
            case MemberExpression member:
                parts.Add((member, FormatExpression(member)));
                break;
        }
    }
    
    private string FormatExpression(Expression expr)
    {
        return expr.ToString()
            .Replace("Param_0", "fact")
            .Replace("Param_1", "fact");
    }
}

/// <summary>
/// Analyzes rule execution patterns and provides insights
/// </summary>
public class RuleAnalyzer<T>
{
    private readonly RulesEngineCore<T> _engine;
    private readonly List<T> _testCases;
    
    public RuleAnalyzer(RulesEngineCore<T> engine, IEnumerable<T> testCases)
    {
        _engine = engine;
        _testCases = testCases.ToList();
    }
    
    public AnalysisReport Analyze()
    {
        var report = new AnalysisReport();
        var rules = _engine.GetRules();
        
        foreach (var rule in rules)
        {
            var ruleAnalysis = AnalyzeRule(rule);
            report.RuleAnalyses.Add(ruleAnalysis);
        }
        
        DetectOverlaps(rules, report);
        DetectDeadRules(rules, report);
        
        return report;
    }
    
    private RuleAnalysis AnalyzeRule(IRule<T> rule)
    {
        var analysis = new RuleAnalysis
        {
            RuleId = rule.Id,
            RuleName = rule.Name
        };

        var matchedCases = new List<T>();

        foreach (var testCase in _testCases)
        {
            if (rule.Evaluate(testCase))
            {
                matchedCases.Add(testCase);
            }
        }

        analysis.MatchRate = _testCases.Count > 0
            ? (double)matchedCases.Count / _testCases.Count
            : 0.0;
        analysis.MatchedCount = matchedCases.Count;
        
        if (analysis.MatchRate == 0)
        {
            analysis.Issues.Add("Rule never matches any test cases - may be dead code");
        }
        else if (analysis.MatchRate == 1.0)
        {
            analysis.Issues.Add("Rule matches ALL test cases - may be too broad");
        }
        
        return analysis;
    }
    
    private void DetectOverlaps(IEnumerable<IRule<T>> rules, AnalysisReport report)
    {
        if (_testCases.Count == 0)
        {
            return; // Cannot detect overlaps without test cases
        }

        var ruleList = rules.ToList();

        for (int i = 0; i < ruleList.Count; i++)
        {
            for (int j = i + 1; j < ruleList.Count; j++)
            {
                var overlapCount = _testCases.Count(tc =>
                    ruleList[i].Evaluate(tc) && ruleList[j].Evaluate(tc));

                if (overlapCount > 0)
                {
                    var overlapRate = (double)overlapCount / _testCases.Count;
                    if (overlapRate > 0.5)
                    {
                        report.Overlaps.Add(new RuleOverlap
                        {
                            Rule1 = ruleList[i].Name,
                            Rule2 = ruleList[j].Name,
                            OverlapRate = overlapRate
                        });
                    }
                }
            }
        }
    }
    
    private void DetectDeadRules(IEnumerable<IRule<T>> rules, AnalysisReport report)
    {
        if (_testCases.Count == 0)
        {
            return; // Cannot detect dead rules without test cases
        }

        foreach (var rule in rules)
        {
            if (!_testCases.Any(tc => rule.Evaluate(tc)))
            {
                report.DeadRules.Add(rule.Name);
            }
        }
    }
}

public class AnalysisReport
{
    public List<RuleAnalysis> RuleAnalyses { get; } = new();
    public List<RuleOverlap> Overlaps { get; } = new();
    public List<string> DeadRules { get; } = new();
    
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Rule Analysis Report ===");
        sb.AppendLine();
        
        sb.AppendLine("Rule Statistics:");
        foreach (var analysis in RuleAnalyses)
        {
            sb.AppendLine($"  {analysis.RuleName}:");
            sb.AppendLine($"    Match Rate: {analysis.MatchRate:P}");
            sb.AppendLine($"    Matched: {analysis.MatchedCount} cases");
            
            if (analysis.Issues.Any())
            {
                sb.AppendLine($"    Issues:");
                foreach (var issue in analysis.Issues)
                {
                    sb.AppendLine($"      - {issue}");
                }
            }
        }
        
        if (Overlaps.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Significant Overlaps (>50%):");
            foreach (var overlap in Overlaps)
            {
                sb.AppendLine($"  {overlap.Rule1} ↔ {overlap.Rule2}: {overlap.OverlapRate:P}");
            }
        }
        
        if (DeadRules.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Dead Rules (never match):");
            foreach (var deadRule in DeadRules)
            {
                sb.AppendLine($"  - {deadRule}");
            }
        }
        
        return sb.ToString();
    }
}

public class RuleAnalysis
{
    public string RuleId { get; set; } = "";
    public string RuleName { get; set; } = "";
    public double MatchRate { get; set; }
    public int MatchedCount { get; set; }
    public List<string> Issues { get; } = new();
}

public class RuleOverlap
{
    public string Rule1 { get; set; } = "";
    public string Rule2 { get; set; } = "";
    public double OverlapRate { get; set; }
}
