using AgentRouting.Core;

namespace TestUtilities;

/// <summary>
/// A test logger that captures all log entries for verification in tests.
/// Stores logs in a simple string list for easy assertion.
/// </summary>
public class TestLogger : IAgentLogger
{
    public List<string> Logs { get; } = new();

    public void LogMessageReceived(IAgent agent, AgentMessage message)
        => Logs.Add($"Received: {agent.Name} - {message.Subject}");

    public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result)
        => Logs.Add($"Processed: {agent.Name} - {message.Subject} - {result.Success}");

    public void LogMessageRouted(AgentMessage message, IAgent? fromAgent, IAgent toAgent)
        => Logs.Add($"Routed: {message.Subject} from {fromAgent?.Name ?? "Router"} to {toAgent.Name}");

    public void LogError(IAgent agent, AgentMessage message, Exception ex)
        => Logs.Add($"Error: {agent.Name} - {ex.Message}");

    /// <summary>
    /// Clears all captured logs.
    /// </summary>
    public void Clear() => Logs.Clear();
}

/// <summary>
/// A more detailed test logger that captures logs in categorized lists.
/// Useful for tests that need to verify specific log types.
/// </summary>
public class CapturingAgentLogger : IAgentLogger
{
    public List<string> ReceivedLogs { get; } = new();
    public List<string> ProcessedLogs { get; } = new();
    public List<string> RoutedLogs { get; } = new();
    public List<string> ErrorLogs { get; } = new();

    public void LogMessageReceived(IAgent agent, AgentMessage message)
        => ReceivedLogs.Add($"{agent.Name} received: {message.Subject}");

    public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result)
        => ProcessedLogs.Add($"{agent.Name} processed: {message.Subject} - {(result.Success ? "SUCCESS" : "FAILED")}");

    public void LogMessageRouted(AgentMessage message, IAgent? fromAgent, IAgent toAgent)
        => RoutedLogs.Add($"Routed: {message.Subject} from {fromAgent?.Name ?? "Router"} to {toAgent.Name}");

    public void LogError(IAgent agent, AgentMessage message, Exception ex)
        => ErrorLogs.Add($"ERROR in {agent.Name}: {ex.Message}");

    /// <summary>
    /// Clears all captured logs across all categories.
    /// </summary>
    public void Clear()
    {
        ReceivedLogs.Clear();
        ProcessedLogs.Clear();
        RoutedLogs.Clear();
        ErrorLogs.Clear();
    }

    /// <summary>
    /// Gets the total count of all logs across all categories.
    /// </summary>
    public int TotalCount => ReceivedLogs.Count + ProcessedLogs.Count + RoutedLogs.Count + ErrorLogs.Count;
}

/// <summary>
/// A silent logger that suppresses all output.
/// Useful for tests where logging output is not needed.
/// </summary>
public class SilentAgentLogger : IAgentLogger
{
    /// <summary>
    /// Singleton instance for convenience.
    /// </summary>
    public static SilentAgentLogger Instance { get; } = new();

    public void LogMessageReceived(IAgent agent, AgentMessage message) { }
    public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result) { }
    public void LogMessageRouted(AgentMessage message, IAgent? fromAgent, IAgent toAgent) { }
    public void LogError(IAgent agent, AgentMessage message, Exception ex) { }
}
