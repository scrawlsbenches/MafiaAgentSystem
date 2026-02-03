using AgentRouting.Core;
using AgentRouting.Middleware;

namespace TestUtilities;

/// <summary>
/// Factory for creating test messages with common defaults.
/// </summary>
public static class TestMessageFactory
{
    /// <summary>
    /// Creates a test message with sensible defaults.
    /// </summary>
    public static AgentMessage Create(
        string? senderId = null,
        string? receiverId = null,
        string? subject = null,
        string? content = null,
        string? category = null,
        MessagePriority priority = MessagePriority.Normal)
    {
        return new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = senderId ?? "test-sender",
            ReceiverId = receiverId ?? "test-receiver",
            Subject = subject ?? "Test Subject",
            Content = content ?? "Test Content",
            Category = category ?? "Test",
            Priority = priority
        };
    }

    /// <summary>
    /// Creates a high priority test message.
    /// </summary>
    public static AgentMessage CreateHighPriority(string? subject = null, string? category = null)
        => Create(subject: subject, category: category, priority: MessagePriority.High);

    /// <summary>
    /// Creates an urgent test message.
    /// </summary>
    public static AgentMessage CreateUrgent(string? subject = null, string? category = null)
        => Create(subject: subject, category: category, priority: MessagePriority.Urgent);

    /// <summary>
    /// Creates a message handler that always succeeds.
    /// </summary>
    public static MessageDelegate CreateSuccessHandler(string response = "Success")
        => (msg, ct) => Task.FromResult(MessageResult.Ok(response));

    /// <summary>
    /// Creates a message handler that always fails.
    /// </summary>
    public static MessageDelegate CreateFailureHandler(string error = "Failed")
        => (msg, ct) => Task.FromResult(MessageResult.Fail(error));

    /// <summary>
    /// Creates a message handler that throws an exception.
    /// </summary>
    public static MessageDelegate CreateThrowingHandler(Exception? ex = null)
        => (msg, ct) => throw (ex ?? new InvalidOperationException("Handler exception"));
}
