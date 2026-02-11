namespace SPTQuestingBots.BotLogic.ECS.UtilityAI;

/// <summary>
/// Tracks which utility task is currently assigned to an entity, along with
/// its ordinal index into the task array. Mirrors Phobos TaskAssignment.
/// </summary>
public readonly struct UtilityTaskAssignment
{
    /// <summary>The currently active task, or null if none.</summary>
    public readonly UtilityTask Task;

    /// <summary>Index of the task in the task manager's array.</summary>
    public readonly int Ordinal;

    public UtilityTaskAssignment(UtilityTask task, int ordinal)
    {
        Task = task;
        Ordinal = ordinal;
    }
}
