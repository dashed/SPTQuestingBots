using System.Runtime.CompilerServices;

namespace SPTQuestingBots.Models.Pathing;

/// <summary>
/// Configuration for the custom path-following movement system.
/// All thresholds are tunable; defaults match Phobos values.
/// </summary>
public struct CustomMoverConfig
{
    /// <summary>
    /// Distance (meters) at which a corner is considered reached while walking.
    /// Phobos default: 0.35m.
    /// </summary>
    public float WalkCornerEpsilon;

    /// <summary>
    /// Distance (meters) at which a corner is considered reached while sprinting.
    /// Larger than walk to avoid overshoot. Phobos default: 0.6m.
    /// </summary>
    public float SprintCornerEpsilon;

    /// <summary>
    /// Distance (meters) from final destination to declare arrival.
    /// Phobos default: 1.5m.
    /// </summary>
    public float DestinationEpsilon;

    /// <summary>
    /// Max angle jitter (degrees) allowed for sprinting at High urgency.
    /// </summary>
    public float SprintJitterHigh;

    /// <summary>
    /// Max angle jitter (degrees) allowed for sprinting at Medium urgency.
    /// </summary>
    public float SprintJitterMedium;

    /// <summary>
    /// Max angle jitter (degrees) allowed for sprinting at Low urgency.
    /// </summary>
    public float SprintJitterLow;

    /// <summary>
    /// How far ahead (meters) along the path to scan for angle jitter.
    /// </summary>
    public float SprintLookaheadDistance;

    /// <summary>
    /// Chaikin smoothing iteration count (0 = no smoothing).
    /// </summary>
    public int SmoothingIterations;

    /// <summary>
    /// Maximum deviation (meters) that path smoothing may introduce.
    /// </summary>
    public float MaxSmoothDeviation;

    /// <summary>
    /// Minimum segment length (meters) before intermediate points are inserted.
    /// </summary>
    public float MinSegmentLength;

    /// <summary>
    /// Radius (meters) around doors where movement speed is reduced.
    /// </summary>
    public float DoorSlowdownRadius;

    /// <summary>
    /// Movement speed multiplier when near doors (0–1).
    /// </summary>
    public float DoorSlowdownSpeed;

    /// <summary>
    /// Interval (seconds) between BSG voxel position updates.
    /// </summary>
    public float VoxelUpdateInterval;

    /// <summary>
    /// Maximum number of path recalculation retries before declaring failure.
    /// </summary>
    public int MaxRetries;

    /// <summary>
    /// Default configuration matching Phobos values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CustomMoverConfig CreateDefault()
    {
        return new CustomMoverConfig
        {
            WalkCornerEpsilon = 0.35f,
            SprintCornerEpsilon = 0.60f,
            DestinationEpsilon = 1.5f,
            SprintJitterHigh = 45f,
            SprintJitterMedium = 30f,
            SprintJitterLow = 20f,
            SprintLookaheadDistance = 10f,
            SmoothingIterations = 2,
            MaxSmoothDeviation = 1.0f,
            MinSegmentLength = 3.0f,
            DoorSlowdownRadius = 3.0f,
            DoorSlowdownSpeed = 0.25f,
            VoxelUpdateInterval = 0.25f,
            MaxRetries = 10,
        };
    }
}
