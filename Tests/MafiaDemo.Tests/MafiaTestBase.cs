using TestRunner.Framework;
using AgentRouting.Infrastructure;
using AgentRouting.MafiaDemo;

namespace MafiaDemo.Tests;

/// <summary>
/// Base class for MafiaDemo tests that need global state isolation.
/// Automatically resets SystemClock.Instance and GameTimingOptions.Current after each test.
/// </summary>
public abstract class MafiaTestBase : TestBase
{
    private ISystemClock? _originalSystemClock;
    private GameTimingOptions? _originalGameTimingOptions;

    public override void SetUp()
    {
        base.SetUp();
        _originalSystemClock = SystemClock.Instance;
        _originalGameTimingOptions = GameTimingOptions.Current;
    }

    public override void TearDown()
    {
        if (_originalSystemClock != null)
        {
            SystemClock.Instance = _originalSystemClock;
        }
        if (_originalGameTimingOptions != null)
        {
            GameTimingOptions.Current = _originalGameTimingOptions;
        }
        base.TearDown();
    }
}
