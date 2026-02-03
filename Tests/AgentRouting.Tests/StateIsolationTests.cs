using TestRunner.Framework;
using AgentRouting.Infrastructure;

namespace AgentRouting.Tests;

/// <summary>
/// Tests to verify that AgentRoutingTestBase properly isolates SystemClock state between tests.
/// </summary>
public class SystemClockIsolationTests : AgentRoutingTestBase
{
    /// <summary>
    /// Custom clock for testing that returns a fixed time.
    /// </summary>
    private class FixedClock : ISystemClock
    {
        private readonly DateTime _fixedTime;

        public FixedClock(DateTime fixedTime)
        {
            _fixedTime = fixedTime;
        }

        public DateTime UtcNow => _fixedTime;
    }

    [Test]
    public void Test1_ModifySystemClock()
    {
        // Modify SystemClock.Instance to a fixed time
        var fixedTime = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        SystemClock.Instance = new FixedClock(fixedTime);

        Assert.Equal(fixedTime, SystemClock.Instance.UtcNow);
    }

    [Test]
    public void Test2_SystemClockShouldBeReset()
    {
        // After Test1, SystemClock should be reset to default behavior
        // Default SystemClock returns current time, not the fixed time
        var now = SystemClock.Instance.UtcNow;

        // The time should be recent (within last minute), not 2020
        Assert.True(now.Year >= 2026, $"SystemClock should be reset to real time, but got {now}");
    }

    [Test]
    public void Test3_EachTestGetsCleanState()
    {
        // Verify we can set clock again without interference
        var anotherFixedTime = new DateTime(2019, 6, 15, 8, 30, 0, DateTimeKind.Utc);
        SystemClock.Instance = new FixedClock(anotherFixedTime);

        Assert.Equal(anotherFixedTime, SystemClock.Instance.UtcNow);
    }
}

/// <summary>
/// Tests for manual state reset without base class inheritance.
/// </summary>
public class ManualClockResetTests
{
    private ISystemClock? _originalClock;

    /// <summary>
    /// Custom clock for testing that returns a fixed time.
    /// </summary>
    private class FixedClock : ISystemClock
    {
        private readonly DateTime _fixedTime;

        public FixedClock(DateTime fixedTime)
        {
            _fixedTime = fixedTime;
        }

        public DateTime UtcNow => _fixedTime;
    }

    [SetUp]
    public void SetUp()
    {
        // Manually capture state
        _originalClock = SystemClock.Instance;
    }

    [TearDown]
    public void TearDown()
    {
        // Manually restore state
        if (_originalClock != null)
        {
            SystemClock.Instance = _originalClock;
        }
    }

    [Test]
    public void ManualStateCapture_Works()
    {
        var fixedTime = new DateTime(2018, 3, 20, 10, 0, 0, DateTimeKind.Utc);
        SystemClock.Instance = new FixedClock(fixedTime);

        Assert.Equal(fixedTime, SystemClock.Instance.UtcNow);
    }

    [Test]
    public void ManualStateRestore_Works()
    {
        // After previous test, clock should be restored
        var now = SystemClock.Instance.UtcNow;
        Assert.True(now.Year >= 2026, $"Clock should be restored, got {now}");
    }
}
