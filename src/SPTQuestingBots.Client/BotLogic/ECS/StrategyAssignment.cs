namespace SPTQuestingBots.BotLogic.ECS;

/// <summary>
/// Tracks which squad strategy is currently assigned to a squad, along with
/// its ordinal index into the strategy array. Mirrors <see cref="UtilityAI.UtilityTaskAssignment"/>.
/// Uses <c>object</c> for the Strategy field since SquadStrategy (Phase 2) doesn't exist yet.
/// Phase 2 will update this to the correct type.
/// </summary>
public readonly struct StrategyAssignment
{
    /// <summary>The currently active strategy, or null if none. Will be cast to SquadStrategy at usage sites.</summary>
    public readonly object Strategy;

    /// <summary>Index of the strategy in the strategy manager's array.</summary>
    public readonly int Ordinal;

    public StrategyAssignment(object strategy, int ordinal)
    {
        Strategy = strategy;
        Ordinal = ordinal;
    }
}
