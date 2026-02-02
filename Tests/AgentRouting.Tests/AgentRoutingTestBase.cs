using TestRunner.Framework;
using AgentRouting.Infrastructure;

namespace AgentRouting.Tests;

/// <summary>
/// Base class for AgentRouting tests that need global state isolation.
/// Automatically resets SystemClock.Instance after each test.
/// </summary>
public abstract class AgentRoutingTestBase : TestBase
{
    private ISystemClock? _originalSystemClock;

    public override void SetUp()
    {
        base.SetUp();
        _originalSystemClock = SystemClock.Instance;
    }

    public override void TearDown()
    {
        if (_originalSystemClock != null)
        {
            SystemClock.Instance = _originalSystemClock;
        }
        base.TearDown();
    }
}
