using AgentRouting.Configuration;
using TestRunner.Framework;

namespace AgentRouting.Tests;

/// <summary>
/// Tests for AgentRoutingDefaults configuration class.
/// Ensures default values are appropriate and documented.
/// </summary>
public class AgentRoutingDefaultsTests : AgentRoutingTestBase
{
    [Test]
    public void MaxConcurrentMessages_HasReasonableDefault()
    {
        // Assert
        Assert.Equal(100, AgentRoutingDefaults.MaxConcurrentMessages);
    }

    [Test]
    public void DefaultTimeout_Is30Seconds()
    {
        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), AgentRoutingDefaults.DefaultTimeout);
    }

    [Test]
    public void MaxRetries_Is3()
    {
        // Assert
        Assert.Equal(3, AgentRoutingDefaults.MaxRetries);
    }

    [Test]
    public void DefaultRetryDelay_Is100Milliseconds()
    {
        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(100), AgentRoutingDefaults.DefaultRetryDelay);
    }

    [Test]
    public void StopOnFirstMatch_IsTrue()
    {
        // Assert
        Assert.True(AgentRoutingDefaults.StopOnFirstMatch);
    }

    [Test]
    public void TrackPerformance_IsTrue()
    {
        // Assert
        Assert.True(AgentRoutingDefaults.TrackPerformance);
    }

    [Test]
    public void DefaultPriority_IsZero()
    {
        // Assert
        Assert.Equal(0, AgentRoutingDefaults.DefaultPriority);
    }

    [Test]
    public void MaxQueueSize_Is10000()
    {
        // Assert
        Assert.Equal(10000, AgentRoutingDefaults.MaxQueueSize);
    }

    [Test]
    public void DefaultTimeout_IsPositive()
    {
        // Assert
        Assert.True(AgentRoutingDefaults.DefaultTimeout > TimeSpan.Zero);
    }

    [Test]
    public void DefaultRetryDelay_IsPositive()
    {
        // Assert
        Assert.True(AgentRoutingDefaults.DefaultRetryDelay > TimeSpan.Zero);
    }

    [Test]
    public void MaxConcurrentMessages_IsPositive()
    {
        // Assert
        Assert.True(AgentRoutingDefaults.MaxConcurrentMessages > 0);
    }

    [Test]
    public void MaxQueueSize_IsPositive()
    {
        // Assert
        Assert.True(AgentRoutingDefaults.MaxQueueSize > 0);
    }

    [Test]
    public void MaxRetries_IsPositive()
    {
        // Assert
        Assert.True(AgentRoutingDefaults.MaxRetries > 0);
    }
}
