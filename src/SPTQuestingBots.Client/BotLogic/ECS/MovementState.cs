namespace SPTQuestingBots.BotLogic.ECS;

/// <summary>
/// Status of path following for a bot entity.
/// </summary>
public enum PathFollowStatus : byte
{
    /// <summary>No path active.</summary>
    None = 0,

    /// <summary>Actively following a path.</summary>
    Following = 1,

    /// <summary>Destination reached.</summary>
    Reached = 2,

    /// <summary>Path following failed (invalid path, exhausted retries).</summary>
    Failed = 3,
}

/// <summary>
/// Stuck detection phase for movement.
/// </summary>
public enum StuckPhase : byte
{
    /// <summary>Not stuck.</summary>
    None = 0,

    /// <summary>Soft stuck detected (EWMA speed drop).</summary>
    SoftStuck = 1,

    /// <summary>Hard stuck detected (position history stall).</summary>
    HardStuck = 2,

    /// <summary>Stuck recovery failed.</summary>
    Failed = 3,
}

/// <summary>
/// Per-bot movement state stored inline on <see cref="BotEntity"/>.
/// Tracks path-following progress, sprint state, and stuck detection for
/// the custom movement system. Pure value type — no Unity dependencies.
/// </summary>
public struct MovementState
{
    /// <summary>Current path-following status.</summary>
    public PathFollowStatus Status;

    /// <summary>Whether the bot is currently sprinting.</summary>
    public bool IsSprinting;

    /// <summary>Current pose level (0 = crouch, 1 = standing).</summary>
    public float CurrentPose;

    /// <summary>Current stuck detection phase.</summary>
    public StuckPhase StuckStatus;

    /// <summary>
    /// Computed angle jitter (degrees) from upcoming path corners.
    /// Used for sprint gating decisions.
    /// </summary>
    public float SprintAngleJitter;

    /// <summary>Time of last path recalculation (game time seconds).</summary>
    public float LastPathUpdateTime;

    /// <summary>Index of the current corner being navigated toward.</summary>
    public int CurrentCornerIndex;

    /// <summary>Total number of corners in the current path.</summary>
    public int TotalCorners;

    /// <summary>Number of path retry attempts for the current destination.</summary>
    public int RetryCount;

    /// <summary>
    /// Whether the custom mover controller is actively driving this bot's movement
    /// (as opposed to BSG's BotMover).
    /// </summary>
    public bool IsCustomMoverActive;

    /// <summary>
    /// Resets all fields to their defaults (no path, not moving, not stuck).
    /// </summary>
    public void Reset()
    {
        Status = PathFollowStatus.None;
        IsSprinting = false;
        CurrentPose = 1f;
        StuckStatus = StuckPhase.None;
        SprintAngleJitter = 0f;
        LastPathUpdateTime = 0f;
        CurrentCornerIndex = 0;
        TotalCorners = 0;
        RetryCount = 0;
        IsCustomMoverActive = false;
    }
}
