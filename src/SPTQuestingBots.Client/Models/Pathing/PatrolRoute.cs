namespace SPTQuestingBots.Models.Pathing;

/// <summary>Type of patrol route, affecting loop behavior and personality fit.</summary>
public enum PatrolRouteType : byte
{
    Perimeter = 0,
    Interior = 1,
    Overwatch = 2,
}

/// <summary>
/// A single waypoint along a patrol route. Value type for dense storage.
/// </summary>
public struct PatrolWaypoint
{
    /// <summary>World X position.</summary>
    public float X;

    /// <summary>World Y position (height).</summary>
    public float Y;

    /// <summary>World Z position.</summary>
    public float Z;

    /// <summary>Minimum seconds to pause at this waypoint (default 2).</summary>
    public float PauseDurationMin;

    /// <summary>Maximum seconds to pause at this waypoint (default 5).</summary>
    public float PauseDurationMax;

    /// <summary>Whether the bot should sit/crouch at this waypoint (from BSG PatrolPoint.ShallSit).</summary>
    public bool ShallSit;

    /// <summary>
    /// Patrol point type: 0 = checkPoint (brief stop), 1 = stayPoint (longer hold).
    /// From BSG <see cref="PatrolPointType"/>.
    /// </summary>
    public byte PointType;

    /// <summary>Whether this waypoint has a designated look direction.</summary>
    public bool HasLookDirection;

    /// <summary>Look direction X component (normalized, from PointWithLookSides).</summary>
    public float LookDirX;

    /// <summary>Look direction Y component (normalized, from PointWithLookSides).</summary>
    public float LookDirY;

    /// <summary>Look direction Z component (normalized, from PointWithLookSides).</summary>
    public float LookDirZ;

    /// <summary>Number of sub-points under this patrol point (0 if none).</summary>
    public byte SubPointCount;

    public PatrolWaypoint(float x, float y, float z, float pauseMin = 2f, float pauseMax = 5f)
    {
        X = x;
        Y = y;
        Z = z;
        PauseDurationMin = pauseMin;
        PauseDurationMax = pauseMax;
        ShallSit = false;
        PointType = 0;
        HasLookDirection = false;
        LookDirX = 0f;
        LookDirY = 0f;
        LookDirZ = 0f;
        SubPointCount = 0;
    }
}

/// <summary>
/// Constants for <see cref="PatrolWaypoint.PointType"/>.
/// Mirrors BSG's <c>PatrolPointType</c> enum.
/// </summary>
public static class PatrolPointTypeId
{
    public const byte CheckPoint = 0;
    public const byte StayPoint = 1;
}

/// <summary>
/// A named patrol route with a sequence of waypoints and filters.
/// Pure C# — no Unity or EFT dependencies.
/// </summary>
public class PatrolRoute
{
    /// <summary>Human-readable name for logging.</summary>
    public string Name;

    /// <summary>Route classification.</summary>
    public PatrolRouteType Type;

    /// <summary>Ordered waypoints defining the route path.</summary>
    public PatrolWaypoint[] Waypoints;

    /// <summary>Minimum aggression to use this route (0.0-1.0, default 0).</summary>
    public float MinAggression;

    /// <summary>Maximum aggression to use this route (0.0-1.0, default 1).</summary>
    public float MaxAggression;

    /// <summary>Minimum normalized raid time for this route (0.0-1.0, default 0).</summary>
    public float MinRaidTime;

    /// <summary>Maximum normalized raid time for this route (0.0-1.0, default 1).</summary>
    public float MaxRaidTime;

    /// <summary>Whether to loop back to the first waypoint after the last one.</summary>
    public bool IsLoop;

    public PatrolRoute(
        string name,
        PatrolRouteType type,
        PatrolWaypoint[] waypoints,
        float minAggression = 0f,
        float maxAggression = 1f,
        float minRaidTime = 0f,
        float maxRaidTime = 1f,
        bool? isLoop = null
    )
    {
        Name = name;
        Type = type;
        Waypoints = waypoints;
        MinAggression = minAggression;
        MaxAggression = maxAggression;
        MinRaidTime = minRaidTime;
        MaxRaidTime = maxRaidTime;
        // Default loop behavior: Perimeter and Interior loop, Overwatch does not
        IsLoop = isLoop ?? (type != PatrolRouteType.Overwatch);
    }
}
