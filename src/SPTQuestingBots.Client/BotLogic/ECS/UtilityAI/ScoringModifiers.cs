namespace SPTQuestingBots.BotLogic.ECS.UtilityAI;

/// <summary>
/// Pure C# static helper that computes personality and raid-time scoring multipliers
/// for utility AI tasks. Each task's ScoreEntity multiplies its base score by
/// <see cref="CombinedModifier"/> for a single cheap multiplication.
/// <para>
/// No Unity or EFT dependencies — fully testable in net9.0.
/// </para>
/// </summary>
public static class ScoringModifiers
{
    /// <summary>
    /// Personality-based scoring multiplier.
    /// Aggressive bots rush more, cautious bots camp/snipe more.
    /// </summary>
    public static float PersonalityModifier(float aggression, int actionTypeId)
    {
        float clampedAggression =
            aggression < 0f ? 0f
            : aggression > 1f ? 1f
            : aggression;
        switch (actionTypeId)
        {
            case BotActionTypeId.GoToObjective:
                return Lerp(0.85f, 1.15f, clampedAggression);
            case BotActionTypeId.Ambush:
                return Lerp(1.2f, 0.8f, clampedAggression);
            case BotActionTypeId.Snipe:
                return Lerp(1.2f, 0.8f, clampedAggression);
            case BotActionTypeId.Linger:
                return Lerp(1.3f, 0.7f, clampedAggression);
            case BotActionTypeId.Loot:
                return Lerp(1.1f, 0.9f, clampedAggression);
            case BotActionTypeId.Vulture:
                return Lerp(0.7f, 1.3f, clampedAggression);
            case BotActionTypeId.Investigate:
                return Lerp(0.8f, 1.2f, clampedAggression);
            case BotActionTypeId.Patrol:
                return Lerp(1.2f, 0.8f, clampedAggression);
            default:
                return 1f;
        }
    }

    /// <summary>
    /// Raid time progression multiplier.
    /// Early raid: rush objectives. Late raid: linger, loot, camp.
    /// <paramref name="raidTimeNormalized"/> is 0.0 at raid start, 1.0 at raid end.
    /// </summary>
    public static float RaidTimeModifier(float raidTimeNormalized, int actionTypeId)
    {
        float clampedTime =
            raidTimeNormalized < 0f ? 0f
            : raidTimeNormalized > 1f ? 1f
            : raidTimeNormalized;
        switch (actionTypeId)
        {
            case BotActionTypeId.GoToObjective:
                return Lerp(1.2f, 0.8f, clampedTime);
            case BotActionTypeId.Linger:
                return Lerp(0.7f, 1.3f, clampedTime);
            case BotActionTypeId.Loot:
                return Lerp(0.8f, 1.2f, clampedTime);
            case BotActionTypeId.Ambush:
                return Lerp(0.9f, 1.2f, clampedTime);
            case BotActionTypeId.Snipe:
                return Lerp(0.9f, 1.2f, clampedTime);
            case BotActionTypeId.Patrol:
                return Lerp(0.8f, 1.2f, clampedTime);
            default:
                return 1f;
        }
    }

    /// <summary>
    /// Combined personality + raid time modifier for a single task.
    /// </summary>
    public static float CombinedModifier(float aggression, float raidTimeNormalized, int actionTypeId)
    {
        float result = PersonalityModifier(aggression, actionTypeId) * RaidTimeModifier(raidTimeNormalized, actionTypeId);
        if (float.IsNaN(result) || result < 0f)
        {
            return 1.0f;
        }

        return result;
    }

    /// <summary>Simple linear interpolation: a + (b - a) * t, with t clamped to [0, 1].</summary>
    internal static float Lerp(float a, float b, float t)
    {
        float clampedT =
            t < 0f ? 0f
            : t > 1f ? 1f
            : t;
        return a + (b - a) * clampedT;
    }
}
