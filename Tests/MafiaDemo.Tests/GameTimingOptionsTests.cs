using TestRunner.Framework;
using AgentRouting.MafiaDemo;

namespace TestRunner.Tests;

/// <summary>
/// Unit tests for GameTimingOptions class.
/// Tests default values, presets, GetDelay method, and DelayAsync behavior.
/// </summary>
public class GameTimingOptionsTests
{
    #region Default Values Tests

    [Test]
    public void GameTimingOptions_DefaultDelayMultiplier_IsZero()
    {
        var options = new GameTimingOptions();

        Assert.Equal(0.0, options.DelayMultiplier);
    }

    [Test]
    public void GameTimingOptions_DefaultAgentThinkingDelays_AreZero()
    {
        var options = new GameTimingOptions();

        Assert.Equal(0, options.GodfatherThinkingMs);
        Assert.Equal(0, options.UnderbossThinkingMs);
        Assert.Equal(0, options.ConsigliereThinkingMs);
        Assert.Equal(0, options.CapoThinkingMs);
        Assert.Equal(0, options.SoldierThinkingMs);
        Assert.Equal(0, options.AssociateThinkingMs);
    }

    [Test]
    public void GameTimingOptions_DefaultGameLoopDelays_AreZero()
    {
        var options = new GameTimingOptions();

        Assert.Equal(0, options.TurnDelayMs);
        Assert.Equal(0, options.GameStartDelayMs);
    }

    [Test]
    public void GameTimingOptions_DefaultUIDelays_AreZero()
    {
        var options = new GameTimingOptions();

        Assert.Equal(0, options.SceneTransitionMs);
        Assert.Equal(0, options.DialoguePauseMs);
        Assert.Equal(0, options.DramaticPauseMs);
        Assert.Equal(0, options.TypewriterDelayMs);
    }

    #endregion

    #region GetDelay Method Tests

    [Test]
    public void GetDelay_WhenMultiplierIsZero_ReturnsZero()
    {
        var options = new GameTimingOptions { DelayMultiplier = 0 };

        var result = options.GetDelay(1000);

        Assert.Equal(0, result);
    }

    [Test]
    public void GetDelay_WhenMultiplierIsNegative_ReturnsZero()
    {
        var options = new GameTimingOptions { DelayMultiplier = -1 };

        var result = options.GetDelay(1000);

        Assert.Equal(0, result);
    }

    [Test]
    public void GetDelay_WhenMultiplierIsOne_ReturnsBaseDelay()
    {
        var options = new GameTimingOptions { DelayMultiplier = 1 };

        var result = options.GetDelay(1000);

        Assert.Equal(1000, result);
    }

    [Test]
    public void GetDelay_WhenMultiplierIsTwo_ReturnsDoubleBaseDelay()
    {
        var options = new GameTimingOptions { DelayMultiplier = 2 };

        var result = options.GetDelay(500);

        Assert.Equal(1000, result);
    }

    [Test]
    public void GetDelay_WhenMultiplierIsFractional_ReturnsScaledDelay()
    {
        var options = new GameTimingOptions { DelayMultiplier = 0.25 };

        var result = options.GetDelay(1000);

        Assert.Equal(250, result);
    }

    [Test]
    public void GetDelay_WhenBaseDelayIsZero_ReturnsZero()
    {
        var options = new GameTimingOptions { DelayMultiplier = 1 };

        var result = options.GetDelay(0);

        Assert.Equal(0, result);
    }

    [Test]
    public void GetDelay_TruncatesToInteger()
    {
        var options = new GameTimingOptions { DelayMultiplier = 0.5 };

        // 111 * 0.5 = 55.5, should truncate to 55
        var result = options.GetDelay(111);

        Assert.Equal(55, result);
    }

    #endregion

    #region Instant Preset Tests

    [Test]
    public void Instant_DelayMultiplier_IsZero()
    {
        var instant = GameTimingOptions.Instant;

        Assert.Equal(0.0, instant.DelayMultiplier);
    }

    [Test]
    public void Instant_AgentThinkingDelays_AreZero()
    {
        var instant = GameTimingOptions.Instant;

        Assert.Equal(0, instant.GodfatherThinkingMs);
        Assert.Equal(0, instant.UnderbossThinkingMs);
        Assert.Equal(0, instant.ConsigliereThinkingMs);
        Assert.Equal(0, instant.CapoThinkingMs);
        Assert.Equal(0, instant.SoldierThinkingMs);
        Assert.Equal(0, instant.AssociateThinkingMs);
    }

    [Test]
    public void Instant_GetDelay_AlwaysReturnsZero()
    {
        var instant = GameTimingOptions.Instant;

        Assert.Equal(0, instant.GetDelay(1000));
        Assert.Equal(0, instant.GetDelay(5000));
        Assert.Equal(0, instant.GetDelay(int.MaxValue));
    }

    [Test]
    public void Instant_CreatesNewInstanceEachTime()
    {
        var instant1 = GameTimingOptions.Instant;
        var instant2 = GameTimingOptions.Instant;

        Assert.NotSame(instant1, instant2);
    }

    #endregion

    #region Normal Preset Tests

    [Test]
    public void Normal_DelayMultiplier_IsOne()
    {
        var normal = GameTimingOptions.Normal;

        Assert.Equal(1.0, normal.DelayMultiplier);
    }

    [Test]
    public void Normal_AgentThinkingDelays_HaveExpectedValues()
    {
        var normal = GameTimingOptions.Normal;

        Assert.Equal(500, normal.GodfatherThinkingMs);
        Assert.Equal(200, normal.UnderbossThinkingMs);
        Assert.Equal(300, normal.ConsigliereThinkingMs);
        Assert.Equal(150, normal.CapoThinkingMs);
        Assert.Equal(100, normal.SoldierThinkingMs);
        Assert.Equal(50, normal.AssociateThinkingMs);
    }

    [Test]
    public void Normal_GameLoopDelays_HaveExpectedValues()
    {
        var normal = GameTimingOptions.Normal;

        Assert.Equal(2000, normal.TurnDelayMs);
        Assert.Equal(1500, normal.GameStartDelayMs);
    }

    [Test]
    public void Normal_UIDelays_HaveExpectedValues()
    {
        var normal = GameTimingOptions.Normal;

        Assert.Equal(1000, normal.SceneTransitionMs);
        Assert.Equal(500, normal.DialoguePauseMs);
        Assert.Equal(800, normal.DramaticPauseMs);
        Assert.Equal(300, normal.TypewriterDelayMs);
    }

    [Test]
    public void Normal_GetDelay_ReturnsFullBaseDelay()
    {
        var normal = GameTimingOptions.Normal;

        Assert.Equal(1000, normal.GetDelay(1000));
        Assert.Equal(500, normal.GetDelay(500));
    }

    [Test]
    public void Normal_CreatesNewInstanceEachTime()
    {
        var normal1 = GameTimingOptions.Normal;
        var normal2 = GameTimingOptions.Normal;

        Assert.NotSame(normal1, normal2);
    }

    #endregion

    #region Fast Preset Tests

    [Test]
    public void Fast_DelayMultiplier_IsQuarter()
    {
        var fast = GameTimingOptions.Fast;

        Assert.Equal(0.25, fast.DelayMultiplier);
    }

    [Test]
    public void Fast_AgentThinkingDelays_HaveExpectedValues()
    {
        var fast = GameTimingOptions.Fast;

        Assert.Equal(500, fast.GodfatherThinkingMs);
        Assert.Equal(200, fast.UnderbossThinkingMs);
        Assert.Equal(300, fast.ConsigliereThinkingMs);
        Assert.Equal(150, fast.CapoThinkingMs);
        Assert.Equal(100, fast.SoldierThinkingMs);
        Assert.Equal(50, fast.AssociateThinkingMs);
    }

    [Test]
    public void Fast_GameLoopDelays_HaveExpectedValues()
    {
        var fast = GameTimingOptions.Fast;

        Assert.Equal(2000, fast.TurnDelayMs);
        Assert.Equal(1500, fast.GameStartDelayMs);
    }

    [Test]
    public void Fast_UIDelays_HaveExpectedValues()
    {
        var fast = GameTimingOptions.Fast;

        Assert.Equal(1000, fast.SceneTransitionMs);
        Assert.Equal(500, fast.DialoguePauseMs);
        Assert.Equal(800, fast.DramaticPauseMs);
        Assert.Equal(300, fast.TypewriterDelayMs);
    }

    [Test]
    public void Fast_GetDelay_ReturnsQuarterBaseDelay()
    {
        var fast = GameTimingOptions.Fast;

        Assert.Equal(250, fast.GetDelay(1000));
        Assert.Equal(125, fast.GetDelay(500));
    }

    [Test]
    public void Fast_CreatesNewInstanceEachTime()
    {
        var fast1 = GameTimingOptions.Fast;
        var fast2 = GameTimingOptions.Fast;

        Assert.NotSame(fast1, fast2);
    }

    #endregion

    #region Current Property Tests

    [Test]
    public void Current_DefaultValue_IsNewInstance()
    {
        // Reset to default
        GameTimingOptions.Current = new GameTimingOptions();

        Assert.NotNull(GameTimingOptions.Current);
        Assert.Equal(0.0, GameTimingOptions.Current.DelayMultiplier);
    }

    [Test]
    public void Current_CanBeSetToInstant()
    {
        var original = GameTimingOptions.Current;
        try
        {
            GameTimingOptions.Current = GameTimingOptions.Instant;

            Assert.Equal(0.0, GameTimingOptions.Current.DelayMultiplier);
        }
        finally
        {
            GameTimingOptions.Current = original;
        }
    }

    [Test]
    public void Current_CanBeSetToNormal()
    {
        var original = GameTimingOptions.Current;
        try
        {
            GameTimingOptions.Current = GameTimingOptions.Normal;

            Assert.Equal(1.0, GameTimingOptions.Current.DelayMultiplier);
            Assert.Equal(500, GameTimingOptions.Current.GodfatherThinkingMs);
        }
        finally
        {
            GameTimingOptions.Current = original;
        }
    }

    [Test]
    public void Current_CanBeSetToFast()
    {
        var original = GameTimingOptions.Current;
        try
        {
            GameTimingOptions.Current = GameTimingOptions.Fast;

            Assert.Equal(0.25, GameTimingOptions.Current.DelayMultiplier);
        }
        finally
        {
            GameTimingOptions.Current = original;
        }
    }

    [Test]
    public void Current_CanBeSetToCustomOptions()
    {
        var original = GameTimingOptions.Current;
        try
        {
            var custom = new GameTimingOptions
            {
                DelayMultiplier = 3,
                GodfatherThinkingMs = 1000,
                TurnDelayMs = 5000
            };
            GameTimingOptions.Current = custom;

            Assert.Equal(3.0, GameTimingOptions.Current.DelayMultiplier);
            Assert.Equal(1000, GameTimingOptions.Current.GodfatherThinkingMs);
            Assert.Equal(5000, GameTimingOptions.Current.TurnDelayMs);
        }
        finally
        {
            GameTimingOptions.Current = original;
        }
    }

    #endregion

    #region DelayAsync Tests

    [Test]
    public async Task DelayAsync_WhenDelayIsZero_ReturnsImmediately()
    {
        var original = GameTimingOptions.Current;
        try
        {
            GameTimingOptions.Current = GameTimingOptions.Instant;

            var startTime = DateTime.UtcNow;
            await GameTimingOptions.DelayAsync(1000);
            var elapsed = DateTime.UtcNow - startTime;

            // Should complete nearly instantly (allow small margin for test execution)
            Assert.True(elapsed.TotalMilliseconds < 100);
        }
        finally
        {
            GameTimingOptions.Current = original;
        }
    }

    [Test]
    public async Task DelayAsync_WhenMultiplierIsZero_ReturnsImmediately()
    {
        var original = GameTimingOptions.Current;
        try
        {
            GameTimingOptions.Current = new GameTimingOptions { DelayMultiplier = 0 };

            var startTime = DateTime.UtcNow;
            await GameTimingOptions.DelayAsync(5000);
            var elapsed = DateTime.UtcNow - startTime;

            // Should complete nearly instantly
            Assert.True(elapsed.TotalMilliseconds < 100);
        }
        finally
        {
            GameTimingOptions.Current = original;
        }
    }

    [Test]
    public async Task DelayAsync_WithPositiveDelay_WaitsApproximateTime()
    {
        var original = GameTimingOptions.Current;
        try
        {
            GameTimingOptions.Current = new GameTimingOptions { DelayMultiplier = 1 };

            var startTime = DateTime.UtcNow;
            await GameTimingOptions.DelayAsync(50);
            var elapsed = DateTime.UtcNow - startTime;

            // Should wait approximately 50ms (allow 20-150ms for test timing variance)
            Assert.True(elapsed.TotalMilliseconds >= 20, $"Expected >= 20ms but was {elapsed.TotalMilliseconds}ms");
            Assert.True(elapsed.TotalMilliseconds < 150, $"Expected < 150ms but was {elapsed.TotalMilliseconds}ms");
        }
        finally
        {
            GameTimingOptions.Current = original;
        }
    }

    [Test]
    public async Task DelayAsync_WithCancellation_ThrowsWhenCanceled()
    {
        var original = GameTimingOptions.Current;
        try
        {
            GameTimingOptions.Current = new GameTimingOptions { DelayMultiplier = 1 };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var exceptionThrown = false;
            try
            {
                await GameTimingOptions.DelayAsync(1000, cts.Token);
            }
            catch (TaskCanceledException)
            {
                exceptionThrown = true;
            }
            catch (OperationCanceledException)
            {
                exceptionThrown = true;
            }

            Assert.True(exceptionThrown);
        }
        finally
        {
            GameTimingOptions.Current = original;
        }
    }

    [Test]
    public async Task DelayAsync_DefaultCancellationToken_DoesNotCancel()
    {
        var original = GameTimingOptions.Current;
        try
        {
            GameTimingOptions.Current = GameTimingOptions.Instant;

            // Should complete without throwing
            await GameTimingOptions.DelayAsync(0);

            Assert.True(true); // If we get here, it succeeded
        }
        finally
        {
            GameTimingOptions.Current = original;
        }
    }

    [Test]
    public async Task DelayAsync_UsesCurrentOptions()
    {
        var original = GameTimingOptions.Current;
        try
        {
            // Set to fast mode (0.25x)
            GameTimingOptions.Current = GameTimingOptions.Fast;

            var startTime = DateTime.UtcNow;
            await GameTimingOptions.DelayAsync(200); // Should become 50ms (200 * 0.25)
            var elapsed = DateTime.UtcNow - startTime;

            // Expected ~50ms delay with fast preset
            Assert.True(elapsed.TotalMilliseconds >= 20, $"Expected >= 20ms but was {elapsed.TotalMilliseconds}ms");
            Assert.True(elapsed.TotalMilliseconds < 150, $"Expected < 150ms but was {elapsed.TotalMilliseconds}ms");
        }
        finally
        {
            GameTimingOptions.Current = original;
        }
    }

    #endregion

    #region Property Setter Tests

    [Test]
    public void GameTimingOptions_AllPropertiesAreSettable()
    {
        var options = new GameTimingOptions
        {
            DelayMultiplier = 1.5,
            GodfatherThinkingMs = 100,
            UnderbossThinkingMs = 200,
            ConsigliereThinkingMs = 300,
            CapoThinkingMs = 400,
            SoldierThinkingMs = 500,
            AssociateThinkingMs = 600,
            TurnDelayMs = 700,
            GameStartDelayMs = 800,
            SceneTransitionMs = 900,
            DialoguePauseMs = 1000,
            DramaticPauseMs = 1100,
            TypewriterDelayMs = 1200
        };

        Assert.Equal(1.5, options.DelayMultiplier);
        Assert.Equal(100, options.GodfatherThinkingMs);
        Assert.Equal(200, options.UnderbossThinkingMs);
        Assert.Equal(300, options.ConsigliereThinkingMs);
        Assert.Equal(400, options.CapoThinkingMs);
        Assert.Equal(500, options.SoldierThinkingMs);
        Assert.Equal(600, options.AssociateThinkingMs);
        Assert.Equal(700, options.TurnDelayMs);
        Assert.Equal(800, options.GameStartDelayMs);
        Assert.Equal(900, options.SceneTransitionMs);
        Assert.Equal(1000, options.DialoguePauseMs);
        Assert.Equal(1100, options.DramaticPauseMs);
        Assert.Equal(1200, options.TypewriterDelayMs);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void GetDelay_VerySmallMultiplier_ReturnsZeroForSmallDelays()
    {
        var options = new GameTimingOptions { DelayMultiplier = 0.001 };

        // 10 * 0.001 = 0.01, truncates to 0
        var result = options.GetDelay(10);

        Assert.Equal(0, result);
    }

    [Test]
    public void GetDelay_VeryLargeMultiplier_HandlesLargeValues()
    {
        var options = new GameTimingOptions { DelayMultiplier = 10 };

        var result = options.GetDelay(1000);

        Assert.Equal(10000, result);
    }

    [Test]
    public void Instant_GetDelay_WithVeryLargeBaseDelay_ReturnsZero()
    {
        var instant = GameTimingOptions.Instant;

        var result = instant.GetDelay(int.MaxValue);

        Assert.Equal(0, result);
    }

    [Test]
    public void GetDelay_MultiplierJustAboveZero_ReturnsScaledValue()
    {
        var options = new GameTimingOptions { DelayMultiplier = 0.0001 };

        // 1000000 * 0.0001 = 100
        var result = options.GetDelay(1000000);

        Assert.Equal(100, result);
    }

    #endregion
}
