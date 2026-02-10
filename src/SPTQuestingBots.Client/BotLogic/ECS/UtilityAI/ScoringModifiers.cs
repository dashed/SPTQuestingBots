namespace SPTQuestingBots.BotLogic.ECS.UtilityAI
{
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
            switch (actionTypeId)
            {
                case BotActionTypeId.GoToObjective:
                    return Lerp(0.85f, 1.15f, aggression);
                case BotActionTypeId.Ambush:
                    return Lerp(1.2f, 0.8f, aggression);
                case BotActionTypeId.Snipe:
                    return Lerp(1.2f, 0.8f, aggression);
                case BotActionTypeId.Linger:
                    return Lerp(1.3f, 0.7f, aggression);
                case BotActionTypeId.Loot:
                    return Lerp(1.1f, 0.9f, aggression);
                case BotActionTypeId.Vulture:
                    return Lerp(0.7f, 1.3f, aggression);
                case BotActionTypeId.Investigate:
                    return Lerp(0.8f, 1.2f, aggression);
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
            switch (actionTypeId)
            {
                case BotActionTypeId.GoToObjective:
                    return Lerp(1.2f, 0.8f, raidTimeNormalized);
                case BotActionTypeId.Linger:
                    return Lerp(0.7f, 1.3f, raidTimeNormalized);
                case BotActionTypeId.Loot:
                    return Lerp(0.8f, 1.2f, raidTimeNormalized);
                case BotActionTypeId.Ambush:
                    return Lerp(0.9f, 1.1f, raidTimeNormalized);
                case BotActionTypeId.Snipe:
                    return Lerp(0.9f, 1.1f, raidTimeNormalized);
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Combined personality + raid time modifier for a single task.
        /// </summary>
        public static float CombinedModifier(float aggression, float raidTimeNormalized, int actionTypeId)
        {
            return PersonalityModifier(aggression, actionTypeId) * RaidTimeModifier(raidTimeNormalized, actionTypeId);
        }

        /// <summary>Simple linear interpolation: a + (b - a) * t.</summary>
        internal static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
