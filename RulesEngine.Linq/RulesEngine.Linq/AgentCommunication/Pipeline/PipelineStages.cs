using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Mafia.Domain;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Base class for pipeline stages.
/// </summary>
public abstract class PipelineStage<T> where T : class
{
    public abstract string Name { get; }
    public abstract StageResult Execute(T fact, IAgentRulesContext context);
}

// Concrete stage implementations

internal class ValidationStage<T> : PipelineStage<T> where T : class
{
    private readonly Func<T, bool> _condition;
    private readonly string _errorMessage;

    public ValidationStage(Expression<Func<T, bool>> condition, string errorMessage)
    {
        _condition = condition.Compile();
        _errorMessage = errorMessage;
    }

    public override string Name => "Validate";

    public override StageResult Execute(T fact, IAgentRulesContext context)
        => _condition(fact) ? StageResult.Ok() : StageResult.Block(_errorMessage);
}

internal class TransformStage<T> : PipelineStage<T> where T : class
{
    private readonly Action<T> _transform;

    public TransformStage(Action<T> transform) => _transform = transform;
    public override string Name => "Transform";

    public override StageResult Execute(T fact, IAgentRulesContext context)
    {
        _transform(fact);
        return StageResult.Ok();
    }
}

internal class RouteStage<T> : PipelineStage<T> where T : class
{
    private readonly Func<T, IAgentRulesContext, Agent?> _resolver;

    public RouteStage(Func<T, IAgentRulesContext, Agent?> resolver)
        => _resolver = resolver;

    public override string Name => "Route";

    public override StageResult Execute(T fact, IAgentRulesContext context)
    {
        var target = _resolver(fact, context);
        return new StageResult
        {
            Continue = true,
            Outputs = new Dictionary<string, object>
            {
                ["RoutedTo"] = target!
            }
        };
    }
}

internal class LogStage<T> : PipelineStage<T> where T : class
{
    private readonly Func<T, string> _formatter;

    public LogStage(Func<T, string> formatter) => _formatter = formatter;
    public override string Name => "Log";

    public override StageResult Execute(T fact, IAgentRulesContext context)
    {
        var message = _formatter(fact);
        // Actual logging would go here
        return new StageResult
        {
            Continue = true,
            Outputs = new Dictionary<string, object> { ["LogMessage"] = message }
        };
    }
}
