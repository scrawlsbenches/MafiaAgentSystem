namespace AgentRouting.MafiaDemo;

/// <summary>
/// Centralized timing configuration for the game.
/// All delays default to 0 (instant) for fast execution.
/// Set values > 0 for dramatic pacing in interactive mode.
/// </summary>
public class GameTimingOptions
{
    /// <summary>
    /// Global instance - configure once at startup
    /// </summary>
    public static GameTimingOptions Current { get; set; } = new();

    /// <summary>
    /// Multiplier applied to all delays (0 = instant, 1 = normal, 2 = slow)
    /// </summary>
    public double DelayMultiplier { get; set; } = 0;

    // Agent processing delays (by rank)
    public int GodfatherThinkingMs { get; set; } = 0;
    public int UnderbossThinkingMs { get; set; } = 0;
    public int ConsigliereThinkingMs { get; set; } = 0;
    public int CapoThinkingMs { get; set; } = 0;
    public int SoldierThinkingMs { get; set; } = 0;
    public int AssociateThinkingMs { get; set; } = 0;

    // Game loop delays
    public int TurnDelayMs { get; set; } = 0;
    public int GameStartDelayMs { get; set; } = 0;

    // UI/Demo delays
    public int SceneTransitionMs { get; set; } = 0;
    public int DialoguePauseMs { get; set; } = 0;
    public int DramaticPauseMs { get; set; } = 0;
    public int TypewriterDelayMs { get; set; } = 0;

    /// <summary>
    /// Get the actual delay to use (applies multiplier)
    /// </summary>
    public int GetDelay(int baseDelayMs)
    {
        if (DelayMultiplier <= 0) return 0;
        return (int)(baseDelayMs * DelayMultiplier);
    }

    /// <summary>
    /// Async delay helper - returns immediately if delay is 0
    /// </summary>
    public static async Task DelayAsync(int ms, CancellationToken ct = default)
    {
        var actualDelay = Current.GetDelay(ms);
        if (actualDelay > 0)
        {
            await Task.Delay(actualDelay, ct);
        }
    }

    /// <summary>
    /// Preset: Instant mode (all delays = 0)
    /// </summary>
    public static GameTimingOptions Instant => new()
    {
        DelayMultiplier = 0
    };

    /// <summary>
    /// Preset: Normal interactive mode
    /// </summary>
    public static GameTimingOptions Normal => new()
    {
        DelayMultiplier = 1,
        GodfatherThinkingMs = 500,
        UnderbossThinkingMs = 200,
        ConsigliereThinkingMs = 300,
        CapoThinkingMs = 150,
        SoldierThinkingMs = 100,
        AssociateThinkingMs = 50,
        TurnDelayMs = 2000,
        GameStartDelayMs = 1500,
        SceneTransitionMs = 1000,
        DialoguePauseMs = 500,
        DramaticPauseMs = 800,
        TypewriterDelayMs = 300
    };

    /// <summary>
    /// Preset: Fast mode (reduced delays for quick demos)
    /// </summary>
    public static GameTimingOptions Fast => new()
    {
        DelayMultiplier = 0.25,
        GodfatherThinkingMs = 500,
        UnderbossThinkingMs = 200,
        ConsigliereThinkingMs = 300,
        CapoThinkingMs = 150,
        SoldierThinkingMs = 100,
        AssociateThinkingMs = 50,
        TurnDelayMs = 2000,
        GameStartDelayMs = 1500,
        SceneTransitionMs = 1000,
        DialoguePauseMs = 500,
        DramaticPauseMs = 800,
        TypewriterDelayMs = 300
    };
}
