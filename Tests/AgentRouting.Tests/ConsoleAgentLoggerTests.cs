using AgentRouting.Core;
using TestRunner.Framework;
using TestUtilities;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for ConsoleAgentLogger.
/// Since ConsoleAgentLogger writes to Console, we capture/verify the behavior.
/// </summary>
public class ConsoleAgentLoggerTests : AgentRoutingTestBase
{
    private ConsoleAgentLogger _logger = null!;
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalOutput = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new ConsoleAgentLogger();
        _consoleOutput = new StringWriter();
        _originalOutput = Console.Out;
        Console.SetOut(_consoleOutput);
    }

    [TearDown]
    public void Cleanup()
    {
        Console.SetOut(_originalOutput);
        _consoleOutput.Dispose();
    }

    [Test]
    public void LogMessageReceived_WritesToConsole()
    {
        // Arrange
        var agent = new SimpleTestAgent("agent-1", "Test Agent");
        var message = new AgentMessage
        {
            Subject = "Test Subject",
            Content = "Test Content"
        };

        // Act
        _logger.LogMessageReceived(agent, message);

        // Assert
        var output = _consoleOutput.ToString();
        Assert.Contains("Test Agent", output);
        Assert.Contains("received", output);
        Assert.Contains("Test Subject", output);
    }

    [Test]
    public void LogMessageProcessed_WithSuccess_WritesGreenOutput()
    {
        // Arrange
        var agent = new SimpleTestAgent("agent-1", "Test Agent");
        var message = new AgentMessage { Subject = "Test Subject" };
        var result = MessageResult.Ok("Success response");

        // Act
        _logger.LogMessageProcessed(agent, message, result);

        // Assert
        var output = _consoleOutput.ToString();
        Assert.Contains("Test Agent", output);
        Assert.Contains("processed", output);
        Assert.Contains("SUCCESS", output);
        Assert.Contains("Success response", output);
    }

    [Test]
    public void LogMessageProcessed_WithFailure_WritesRedOutput()
    {
        // Arrange
        var agent = new SimpleTestAgent("agent-1", "Test Agent");
        var message = new AgentMessage { Subject = "Test Subject" };
        var result = MessageResult.Fail("Error occurred");

        // Act
        _logger.LogMessageProcessed(agent, message, result);

        // Assert
        var output = _consoleOutput.ToString();
        Assert.Contains("Test Agent", output);
        Assert.Contains("FAILED", output);
    }

    [Test]
    public void LogMessageProcessed_WithoutResponse_DoesNotWriteResponse()
    {
        // Arrange
        var agent = new SimpleTestAgent("agent-1", "Test Agent");
        var message = new AgentMessage { Subject = "Test Subject" };
        var result = MessageResult.Ok(); // No response

        // Act
        _logger.LogMessageProcessed(agent, message, result);

        // Assert
        var output = _consoleOutput.ToString();
        Assert.Contains("SUCCESS", output);
        Assert.False(output.Contains("Response:")); // No response line
    }

    [Test]
    public void LogMessageRouted_WithFromAgent_WritesRoutingInfo()
    {
        // Arrange
        var fromAgent = new SimpleTestAgent("from-1", "From Agent");
        var toAgent = new SimpleTestAgent("to-1", "To Agent");
        var message = new AgentMessage { Subject = "Routed Subject" };

        // Act
        _logger.LogMessageRouted(message, fromAgent, toAgent);

        // Assert
        var output = _consoleOutput.ToString();
        Assert.Contains("Routed", output);
        Assert.Contains("Routed Subject", output);
        Assert.Contains("From Agent", output);
        Assert.Contains("To Agent", output);
    }

    [Test]
    public void LogMessageRouted_WithoutFromAgent_WritesRouter()
    {
        // Arrange
        var toAgent = new SimpleTestAgent("to-1", "To Agent");
        var message = new AgentMessage { Subject = "Routed Subject" };

        // Act
        _logger.LogMessageRouted(message, null, toAgent);

        // Assert
        var output = _consoleOutput.ToString();
        Assert.Contains("Router", output);
        Assert.Contains("To Agent", output);
    }

    [Test]
    public void LogError_WritesErrorInfo()
    {
        // Arrange
        var agent = new SimpleTestAgent("agent-1", "Test Agent");
        var message = new AgentMessage { Subject = "Test Subject" };
        var exception = new InvalidOperationException("Test error message");

        // Act
        _logger.LogError(agent, message, exception);

        // Assert
        var output = _consoleOutput.ToString();
        Assert.Contains("ERROR", output);
        Assert.Contains("Test Agent", output);
        Assert.Contains("Test error message", output);
    }

    [Test]
    public void Logger_IsThreadSafe_MultipleThreadsCanLog()
    {
        // Arrange
        var agent = new SimpleTestAgent("agent-1", "Test Agent");
        var tasks = new List<Task>();

        // Act - Start multiple threads logging concurrently
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var message = new AgentMessage { Subject = $"Subject {index}" };
                _logger.LogMessageReceived(agent, message);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - All messages were logged (no crash)
        var output = _consoleOutput.ToString();
        Assert.Contains("Subject", output);
    }

    [Test]
    public void Logger_IncludesTimestamp()
    {
        // Arrange
        var agent = new SimpleTestAgent("agent-1", "Test Agent");
        var message = new AgentMessage { Subject = "Test Subject" };

        // Act
        _logger.LogMessageReceived(agent, message);

        // Assert - Should contain HH:mm:ss format timestamp
        var output = _consoleOutput.ToString();
        Assert.True(output.Contains(":") && output.Contains("[") && output.Contains("]"),
            "Output should contain timestamp in brackets");
    }
}
