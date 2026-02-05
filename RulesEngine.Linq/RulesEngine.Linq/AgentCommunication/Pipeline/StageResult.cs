using System;
using System.Collections.Generic;

namespace RulesEngine.Linq.AgentCommunication;

public class StageResult
{
    public bool Continue { get; init; } = true;
    public bool Blocked { get; init; }
    public string? Error { get; init; }
    public IReadOnlyDictionary<string, object> Outputs { get; init; }
        = new Dictionary<string, object>();

    public static StageResult Ok() => new();
    public static StageResult Stop() => new() { Continue = false };
    public static StageResult Block(string reason) => new() { Blocked = true, Error = reason };
    public static StageResult Fail(string error) => new() { Continue = false, Error = error };
}

public class PipelineResult<T> where T : class
{
    public T Fact { get; init; } = default!;
    public bool Completed { get; init; }
    public bool Blocked { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> StagesExecuted { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, object> Outputs { get; init; }
        = new Dictionary<string, object>();
}
