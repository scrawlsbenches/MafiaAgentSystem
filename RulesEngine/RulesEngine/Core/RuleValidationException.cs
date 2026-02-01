namespace RulesEngine.Core;

/// <summary>
/// Exception thrown when rule validation fails during registration
/// </summary>
public class RuleValidationException : Exception
{
    /// <summary>
    /// The ID of the rule that failed validation, if available
    /// </summary>
    public string? RuleId { get; }

    public RuleValidationException(string message, string? ruleId = null)
        : base(message)
    {
        RuleId = ruleId;
    }

    public RuleValidationException(string message, string? ruleId, Exception innerException)
        : base(message, innerException)
    {
        RuleId = ruleId;
    }
}
