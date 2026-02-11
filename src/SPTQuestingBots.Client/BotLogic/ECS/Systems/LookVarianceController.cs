namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Pure C# controller that decides IF and WHERE a bot should look during movement.
/// Implements three priorities: combat event glance, squad member glance, and flank check.
/// No Unity dependencies — fully testable.
/// </summary>
public static class LookVarianceController
{
    private static readonly System.Random Rng = new System.Random(42);

    // Default intervals matching LookVarianceConfig defaults
    private const float DefaultFlankMin = 5.0f;
    private const float DefaultFlankMax = 15.0f;
    private const float DefaultPoiMin = 8.0f;
    private const float DefaultPoiMax = 20.0f;
    private const float DefaultSquadRange = 15.0f;
    private const float DefaultSquadRangeSqr = DefaultSquadRange * DefaultSquadRange;

    /// <summary>
    /// Evaluates whether the bot should look somewhere other than its movement direction.
    /// Uses hardcoded defaults matching <c>LookVarianceConfig</c> defaults.
    /// Returns true and sets targetX/targetZ if a look target was chosen.
    /// </summary>
    public static bool TryGetLookTarget(BotEntity entity, float currentTime, out float targetX, out float targetZ)
    {
        return TryGetLookTarget(
            entity,
            currentTime,
            DefaultFlankMin,
            DefaultFlankMax,
            DefaultPoiMin,
            DefaultPoiMax,
            DefaultSquadRangeSqr,
            out targetX,
            out targetZ
        );
    }

    /// <summary>
    /// Evaluates with config-driven intervals.
    /// Used when config is available at runtime.
    /// </summary>
    public static bool TryGetLookTarget(
        BotEntity entity,
        float currentTime,
        float flankMin,
        float flankMax,
        float poiMin,
        float poiMax,
        float squadRangeSqr,
        out float targetX,
        out float targetZ
    )
    {
        targetX = 0f;
        targetZ = 0f;

        // Don't look around while in combat — BSG AI handles look direction
        if (entity.IsInCombat)
        {
            return false;
        }

        // Priority 1: combat event glance (if HasNearbyEvent and timer expired)
        if (entity.HasNearbyEvent && currentTime >= entity.NextPoiGlanceTime)
        {
            targetX = entity.NearbyEventX;
            targetZ = entity.NearbyEventZ;
            entity.NextPoiGlanceTime = currentTime + SampleInterval(poiMin, poiMax);
            return true;
        }

        // Priority 2: squad member glance (if has boss and close enough)
        if (entity.HasBoss && currentTime >= entity.NextPoiGlanceTime)
        {
            float dx = entity.CurrentPositionX - entity.Boss.CurrentPositionX;
            float dz = entity.CurrentPositionZ - entity.Boss.CurrentPositionZ;
            if (dx * dx + dz * dz < squadRangeSqr)
            {
                targetX = entity.Boss.CurrentPositionX;
                targetZ = entity.Boss.CurrentPositionZ;
                entity.NextPoiGlanceTime = currentTime + SampleInterval(poiMin, poiMax);
                return true;
            }
        }

        // Priority 3: flank check (periodic random head rotation +/-45 degrees)
        if (currentTime >= entity.NextFlankCheckTime)
        {
            float angle = RandomAngle(-45f, 45f);
            float rad = angle * (float)(System.Math.PI / 180.0);
            float cosA = (float)System.Math.Cos(rad);
            float sinA = (float)System.Math.Sin(rad);

            // Rotate current facing direction by the random angle
            float newX = entity.CurrentFacingX * cosA - entity.CurrentFacingZ * sinA;
            float newZ = entity.CurrentFacingX * sinA + entity.CurrentFacingZ * cosA;

            // Project 10 meters ahead in the rotated direction
            targetX = entity.CurrentPositionX + newX * 10f;
            targetZ = entity.CurrentPositionZ + newZ * 10f;

            entity.NextFlankCheckTime = currentTime + SampleInterval(flankMin, flankMax);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Sample a random float in [min, max].
    /// </summary>
    internal static float SampleInterval(float min, float max)
    {
        return min + (float)(Rng.NextDouble() * (max - min));
    }

    /// <summary>
    /// Generate a random angle in [minDegrees, maxDegrees].
    /// </summary>
    internal static float RandomAngle(float minDegrees, float maxDegrees)
    {
        return minDegrees + (float)(Rng.NextDouble() * (maxDegrees - minDegrees));
    }
}
