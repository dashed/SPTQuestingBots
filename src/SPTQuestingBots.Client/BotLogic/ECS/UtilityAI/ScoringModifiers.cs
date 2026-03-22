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
    /// Player proximity scoring multiplier.
    /// When a human player is nearby, bots become more tactical:
    /// boost ambush/snipe/investigate, reduce passive movement/looting.
    /// <paramref name="proximity"/> is 0.0 (no player nearby) to 1.0 (very close).
    /// </summary>
    public static float PlayerProximityModifier(float proximity, int actionTypeId)
    {
        if (proximity <= 0f)
            return 1f;

        float clampedProximity = proximity > 1f ? 1f : proximity;
        switch (actionTypeId)
        {
            case BotActionTypeId.GoToObjective:
                return Lerp(1f, 0.7f, clampedProximity);
            case BotActionTypeId.Ambush:
                return Lerp(1f, 1.4f, clampedProximity);
            case BotActionTypeId.Snipe:
                return Lerp(1f, 1.3f, clampedProximity);
            case BotActionTypeId.HoldPosition:
                return Lerp(1f, 1.3f, clampedProximity);
            case BotActionTypeId.Investigate:
                return Lerp(1f, 1.3f, clampedProximity);
            case BotActionTypeId.Loot:
                return Lerp(1f, 0.7f, clampedProximity);
            case BotActionTypeId.Vulture:
                return Lerp(1f, 1.2f, clampedProximity);
            case BotActionTypeId.Patrol:
                return Lerp(1f, 0.8f, clampedProximity);
            case BotActionTypeId.Linger:
                return Lerp(1f, 0.6f, clampedProximity);
            default:
                return 1f;
        }
    }

    /// <summary>
    /// Cover-aware scoring multiplier.
    /// Bots in cover prefer hold/ambush tasks. Bots exposed with an enemy prefer movement.
    /// <paramref name="coverInfluence"/> is 0.0 (exposed + has enemy), 0.5 (neutral), 1.0 (in cover).
    /// </summary>
    public static float CoverModifier(float coverInfluence, int actionTypeId)
    {
        // Neutral cover state (0.5) returns 1.0 for all tasks
        if (coverInfluence >= 0.45f && coverInfluence <= 0.55f)
            return 1f;

        float clamped =
            coverInfluence < 0f ? 0f
            : coverInfluence > 1f ? 1f
            : coverInfluence;
        switch (actionTypeId)
        {
            case BotActionTypeId.Ambush:
                // In cover: boost ambush. Exposed: reduce ambush.
                return Lerp(0.85f, 1.15f, clamped);
            case BotActionTypeId.Snipe:
                return Lerp(0.85f, 1.15f, clamped);
            case BotActionTypeId.HoldPosition:
                return Lerp(0.85f, 1.15f, clamped);
            case BotActionTypeId.GoToObjective:
                // In cover: reduce urgency to move. Exposed: boost movement to find cover.
                return Lerp(1.15f, 0.9f, clamped);
            case BotActionTypeId.Investigate:
                // Exposed bots are more motivated to investigate threats.
                return Lerp(1.1f, 0.95f, clamped);
            case BotActionTypeId.Loot:
                // Don't loot when exposed with enemy.
                return Lerp(0.8f, 1.05f, clamped);
            default:
                return 1f;
        }
    }

    /// <summary>
    /// DogFight scoring multiplier.
    /// Bots in a dogfight get boosted combat action scores; aggressive bots benefit more.
    /// <paramref name="isInDogFight"/> gates the modifier; <paramref name="aggression"/> scales it.
    /// </summary>
    public static float DogFightModifier(bool isInDogFight, float aggression, int actionTypeId)
    {
        if (!isInDogFight)
            return 1f;

        float aggressiveBoost = 1f + aggression * 0.1f; // 1.0 at 0 aggression, 1.1 at max
        switch (actionTypeId)
        {
            case BotActionTypeId.Investigate:
                return aggressiveBoost;
            case BotActionTypeId.Vulture:
                return aggressiveBoost;
            case BotActionTypeId.GoToObjective:
                // In dogfight, reduce non-combat movement
                return 0.9f;
            case BotActionTypeId.Loot:
                // Don't loot during dogfight
                return 0.7f;
            case BotActionTypeId.Linger:
                return 0.7f;
            case BotActionTypeId.Patrol:
                return 0.8f;
            default:
                return 1f;
        }
    }

    /// <summary>
    /// Compute cover influence from entity state.
    /// Returns 0.0 (exposed + has enemy), 0.5 (neutral), or 1.0 (in cover).
    /// </summary>
    public static float ComputeCoverInfluence(bool isInCover, bool hasEnemyInfo)
    {
        if (isInCover)
            return 1f;
        if (hasEnemyInfo)
            return 0f;
        return 0.5f;
    }

    /// <summary>Upper bound for CombinedModifier to prevent score overflow.</summary>
    public const float MaxCombinedModifier = 1.5f;

    /// <summary>
    /// Combined personality + raid time + player proximity modifier for a single task.
    /// </summary>
    public static float CombinedModifier(float aggression, float raidTimeNormalized, int actionTypeId)
    {
        return CombinedModifier(aggression, raidTimeNormalized, 0f, actionTypeId);
    }

    /// <summary>
    /// Combined personality + raid time + player proximity modifier for a single task.
    /// </summary>
    public static float CombinedModifier(float aggression, float raidTimeNormalized, float playerProximity, int actionTypeId)
    {
        return CombinedModifier(aggression, raidTimeNormalized, playerProximity, 0.5f, false, actionTypeId);
    }

    /// <summary>
    /// Full combined modifier with all factors: personality, raid time, player proximity,
    /// cover influence, and dogfight state.
    /// </summary>
    public static float CombinedModifier(
        float aggression,
        float raidTimeNormalized,
        float playerProximity,
        float coverInfluence,
        bool isInDogFight,
        int actionTypeId
    )
    {
        float result =
            PersonalityModifier(aggression, actionTypeId)
            * RaidTimeModifier(raidTimeNormalized, actionTypeId)
            * PlayerProximityModifier(playerProximity, actionTypeId)
            * CoverModifier(coverInfluence, actionTypeId)
            * DogFightModifier(isInDogFight, aggression, actionTypeId);
        if (float.IsNaN(result) || result < 0f)
        {
            return 1.0f;
        }

        if (result > MaxCombinedModifier)
        {
            return MaxCombinedModifier;
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
