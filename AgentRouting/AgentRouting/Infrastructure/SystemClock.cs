namespace AgentRouting.Infrastructure;

/// <summary>
/// Abstraction for system time to enable testability and consistent UTC usage.
/// </summary>
public interface ISystemClock
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTime UtcNow { get; }
}

/// <summary>
/// Default implementation of ISystemClock using system time.
/// Use SystemClock.Instance for the default clock, or replace it for testing.
/// </summary>
public class SystemClock : ISystemClock
{
    /// <summary>
    /// The default clock instance. Can be replaced for testing purposes.
    /// </summary>
    public static ISystemClock Instance { get; set; } = new SystemClock();

    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;
}
