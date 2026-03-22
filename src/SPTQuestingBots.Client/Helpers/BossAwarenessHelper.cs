using System.Reflection;
using EFT;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Null-safe static methods for querying boss-specific BSG state.
    /// Centralizes boss awareness checks for use across HiveMind, patrol,
    /// cover point, and tactical positioning systems.
    /// </summary>
    public static class BossAwarenessHelper
    {
        // ── ECoverPointSpecial bitmask constants ──────────────────

        /// <summary>noSnipePatrol flag (bit 0).</summary>
        public const int CoverSpecialNoSnipePatrol = 1;

        /// <summary>forFollowers flag (bit 1) — cover reserved for boss followers.</summary>
        public const int CoverSpecialForFollowers = 2;

        /// <summary>forBoss flag (bit 2) — cover reserved for the boss.</summary>
        public const int CoverSpecialForBoss = 4;

        /// <summary>Combined boss + follower mask (bits 1-2).</summary>
        public const int CoverSpecialBossOrFollower = CoverSpecialForFollowers | CoverSpecialForBoss;

        // ── Boss Identification ──────────────────────────────────

        /// <summary>
        /// Whether this bot is a boss (IamBoss flag from BSG's BotBoss).
        /// </summary>
        public static bool IsBoss(BotOwner bot)
        {
            if (bot == null)
                return false;

            try
            {
                return bot.Boss?.IamBoss ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Whether this boss requires follower protection.
        /// Only Reshala, Killa, Tagilla, Sanitar, and Gluhar return true.
        /// </summary>
        public static bool GetNeedProtection(BotOwner bot)
        {
            if (bot == null)
                return false;

            try
            {
                return bot.Boss?.NeedProtection ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Whether this bot is a follower of a boss.
        /// </summary>
        public static bool IsFollower(BotOwner bot)
        {
            if (bot == null)
                return false;

            try
            {
                return bot.BotFollower?.HaveBoss ?? false;
            }
            catch
            {
                return false;
            }
        }

        // ── Boss Combat State ─────────────────────────────────────

        /// <summary>Cached reflection info for ABossLogic boolean properties.</summary>
        private static PropertyInfo _shallAttackProp;
        private static PropertyInfo _fightAtZoneProp;
        private static bool _bossLogicReflectionAttempted;

        /// <summary>Cached reflection info for BotGroupWarnData.WarnDistance.</summary>
        private static FieldInfo _warnDistanceField;
        private static bool _warnDataReflectionAttempted;

        /// <summary>
        /// Initialize reflection for ABossLogic properties.
        /// The actual property names may be obfuscated — we search for bool properties.
        /// </summary>
        private static void InitBossLogicReflection(object bossLogic)
        {
            if (_bossLogicReflectionAttempted)
                return;

            _bossLogicReflectionAttempted = true;

            try
            {
                var type = bossLogic.GetType();

                // Try known property names first, then search by name patterns
                _shallAttackProp = type.GetProperty("ShallAttack", BindingFlags.Public | BindingFlags.Instance);
                _fightAtZoneProp = type.GetProperty("FightAtZone", BindingFlags.Public | BindingFlags.Instance);

                if (_shallAttackProp != null || _fightAtZoneProp != null)
                {
                    LoggingController.LogDebug(
                        "[BossAwarenessHelper] Found boss logic properties: ShallAttack="
                            + (_shallAttackProp != null)
                            + " FightAtZone="
                            + (_fightAtZoneProp != null)
                    );
                }
            }
            catch
            {
                LoggingController.LogDebug("[BossAwarenessHelper] Failed to reflect ABossLogic properties");
            }
        }

        /// <summary>
        /// Initialize reflection for BotGroupWarnData fields.
        /// Uses AccessTools via the reflection helper to find fields across all binding flags.
        /// </summary>
        private static void InitWarnDataReflection(object warnData)
        {
            if (_warnDataReflectionAttempted)
                return;

            _warnDataReflectionAttempted = true;

            try
            {
                var type = warnData.GetType();
                if (_warnDistanceField == null)
                {
                    _warnDistanceField = ReflectionHelper.RequireField(type, "WarnDistance", "BossAwarenessHelper warning distance");
                }

                LoggingController.LogDebug("[BossAwarenessHelper] WarnDistance field found: " + (_warnDistanceField != null));
            }
            catch
            {
                LoggingController.LogDebug("[BossAwarenessHelper] Failed to reflect BotGroupWarnData");
            }
        }

        /// <summary>
        /// Whether the boss's BossLogic wants to attack (ShallAttack).
        /// Uses reflection since the property may be obfuscated.
        /// Returns false for non-bosses or if unavailable.
        /// </summary>
        public static bool GetShallAttack(BotOwner bot)
        {
            if (bot == null)
                return false;

            try
            {
                var bossLogic = bot.Boss?.BossLogic;
                if (bossLogic == null)
                    return false;

                InitBossLogicReflection(bossLogic);

                if (_shallAttackProp != null)
                    return (bool)_shallAttackProp.GetValue(bossLogic);

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Whether the boss should fight at its designated zone (FightAtZone).
        /// Uses reflection since the property may be obfuscated.
        /// Returns false for non-bosses or if unavailable.
        /// </summary>
        public static bool GetFightAtZone(BotOwner bot)
        {
            if (bot == null)
                return false;

            try
            {
                var bossLogic = bot.Boss?.BossLogic;
                if (bossLogic == null)
                    return false;

                InitBossLogicReflection(bossLogic);

                if (_fightAtZoneProp != null)
                    return (bool)_fightAtZoneProp.GetValue(bossLogic);

                return false;
            }
            catch
            {
                return false;
            }
        }

        // ── Warning System ────────────────────────────────────────

        /// <summary>
        /// Gets the warning distance from the bot's group warn data.
        /// Questing bots approaching boss zones should be aware of this threshold
        /// to avoid unnecessary aggro.
        /// Uses reflection since field names may be obfuscated.
        /// Returns 0 if unavailable.
        /// </summary>
        public static float GetWarningDistance(BotOwner bot)
        {
            if (bot == null)
                return 0f;

            try
            {
                var warnData = bot.BotsGroup?.BotGroupWarnData;
                if (warnData == null)
                    return 0f;

                InitWarnDataReflection(warnData);

                if (_warnDistanceField != null)
                {
                    var value = _warnDistanceField.GetValue(warnData);
                    if (value is float f)
                        return f;
                }

                return 0f;
            }
            catch
            {
                return 0f;
            }
        }

        // ── Cover Point Filtering ─────────────────────────────────

        /// <summary>
        /// Whether the given ECoverPointSpecial bitmask indicates a boss-reserved cover point.
        /// </summary>
        public static bool IsBossReservedCover(int specialFlags)
        {
            return (specialFlags & CoverSpecialForBoss) != 0;
        }

        /// <summary>
        /// Whether the given ECoverPointSpecial bitmask indicates a follower-reserved cover point.
        /// </summary>
        public static bool IsFollowerReservedCover(int specialFlags)
        {
            return (specialFlags & CoverSpecialForFollowers) != 0;
        }

        /// <summary>
        /// Whether the given ECoverPointSpecial bitmask indicates a cover point reserved
        /// for either bosses or followers. Regular questing bots should skip these.
        /// </summary>
        public static bool IsBossOrFollowerReservedCover(int specialFlags)
        {
            return (specialFlags & CoverSpecialBossOrFollower) != 0;
        }

        // ── Patrol Route Filtering ────────────────────────────────

        /// <summary>
        /// Whether the given PatrolType is a boss-specific patrol route.
        /// Non-boss bots should not use boss patrol routes.
        /// </summary>
        public static bool IsBossPatrolType(PatrolType type)
        {
            return type == PatrolType.boss;
        }

        /// <summary>
        /// Whether a bot should skip boss-type patrol routes.
        /// Returns true if the route is a boss route and the bot is not a boss.
        /// </summary>
        public static bool ShouldSkipBossRoute(BotOwner bot, PatrolType routeType)
        {
            if (routeType != PatrolType.boss)
                return false;

            return !IsBoss(bot);
        }

        // ── Gluhar Reinforcement ──────────────────────────────────

        /// <summary>
        /// Gets the current follower count for a boss.
        /// Used for Gluhar reinforcement awareness in bot cap calculations.
        /// Returns 0 for non-bosses or if unavailable.
        /// </summary>
        public static int GetFollowerCount(BotOwner bot)
        {
            if (bot == null)
                return 0;

            try
            {
                if (!IsBoss(bot))
                    return 0;

                var followers = bot.Boss?.Followers;
                if (followers == null)
                    return 0;

                return followers.Count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Checks whether a boss bot (typically Gluhar) might spawn reinforcements.
        /// Gluhar spawns replacements when follower count drops below threshold.
        /// Bot cap calculations should account for potential reinforcement spawns.
        /// </summary>
        /// <param name="bot">The boss bot to check.</param>
        /// <param name="minFollowers">Minimum follower threshold below which reinforcements spawn.</param>
        /// <returns>True if this boss may spawn reinforcements.</returns>
        public static bool MaySpawnReinforcements(BotOwner bot, int minFollowers = 2)
        {
            if (bot == null)
                return false;

            try
            {
                if (!IsBoss(bot))
                    return false;

                var role = bot.Profile?.Info?.Settings?.Role;
                if (role == null)
                    return false;

                // Only Gluhar (bossGluhar) spawns reinforcements
                if (role != WildSpawnType.bossGluhar)
                    return false;

                int currentFollowers = GetFollowerCount(bot);
                return currentFollowers < minFollowers;
            }
            catch
            {
                return false;
            }
        }
    }
}
