using System;

namespace RulesEngine.Linq.AgentCommunication;

/// <summary>
/// Builder for creating processing pipelines.
/// Unlike rules (declarative matching), pipelines are sequential stages.
/// </summary>
public interface IPipelineBuilder<T> where T : class
{
    IPipelineBuilder<T> Add(PipelineStage<T> stage);
    IPipelineBuilder<T> AddRule(IAgentRule<T> rule);

    /// <summary>
    /// Invoke rule evaluation as a pipeline stage.
    /// </summary>
    IPipelineBuilder<T> EvaluateRules();

    IMessagePipeline<T> Build();
}

/// <summary>
/// Compiled pipeline ready for execution.
/// </summary>
public interface IMessagePipeline<T> where T : class
{
    PipelineResult<T> Process(T fact, IAgentRulesContext context);
}
