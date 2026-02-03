using AgentRouting.Infrastructure;

namespace TestUtilities;

/// <summary>
/// A test clock that returns a fixed time.
/// Useful for tests that need deterministic time behavior.
/// </summary>
public class FixedClock : ISystemClock
{
    private readonly DateTime _fixedTime;

    public FixedClock(DateTime fixedTime)
    {
        _fixedTime = fixedTime;
    }

    public DateTime UtcNow => _fixedTime;

    /// <summary>
    /// Creates a fixed clock set to a specific year, month, day.
    /// </summary>
    public static FixedClock Create(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        => new(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));
}

/// <summary>
/// A test clock that can be advanced programmatically.
/// Useful for tests that need to simulate time passage.
/// </summary>
public class AdvanceableClock : ISystemClock
{
    private DateTime _utcNow;

    public AdvanceableClock() : this(DateTime.UtcNow) { }

    public AdvanceableClock(DateTime initialTime)
    {
        _utcNow = initialTime;
    }

    public DateTime UtcNow => _utcNow;

    /// <summary>
    /// Advances the clock by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
    }

    /// <summary>
    /// Sets the clock to a specific time.
    /// </summary>
    public void SetTime(DateTime utcNow)
    {
        _utcNow = utcNow;
    }

    /// <summary>
    /// Creates an advanceable clock set to a specific year, month, day.
    /// </summary>
    public static AdvanceableClock Create(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        => new(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));
}
