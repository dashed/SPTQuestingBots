using EFT;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Reads BotsGroup and BotOwner public properties to provide
    /// combat state information for questing decisions.
    /// </summary>
    public static class CombatStateHelper
    {
        /// <summary>
        /// Returns the number of seconds since the bot's group last saw an enemy (real time),
        /// or <c>null</c> if unavailable.
        /// </summary>
        public static float? GetTimeSinceLastCombat(BotOwner bot)
        {
            if (bot?.BotsGroup == null)
            {
                return null;
            }

            float lastSeenTime = bot.BotsGroup.EnemyLastSeenTimeReal;

            // A value of 0 means no combat has occurred yet
            if (lastSeenTime <= 0f)
            {
                return null;
            }

            return Time.time - lastSeenTime;
        }

        /// <summary>
        /// Returns the last known enemy position from the bot's group, or <c>null</c> if unavailable.
        /// </summary>
        public static Vector3? GetLastEnemyPosition(BotOwner bot)
        {
            if (bot?.BotsGroup == null)
            {
                return null;
            }

            Vector3 position = bot.BotsGroup.EnemyLastSeenPositionReal;

            // Vector3.zero means no position has been recorded
            if (position == Vector3.zero)
            {
                return null;
            }

            return position;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot's group ended combat within the specified cooldown period.
        /// Returns <c>false</c> if no combat has occurred or if fields are unavailable.
        /// </summary>
        public static bool IsPostCombat(BotOwner bot, float cooldownSeconds)
        {
            float? timeSince = GetTimeSinceLastCombat(bot);
            if (timeSince == null)
            {
                return false;
            }

            return timeSince.Value >= 0f && timeSince.Value <= cooldownSeconds;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot's danger area tracker has any blocked cover points,
        /// indicating the bot is aware of nearby danger. Returns <c>false</c> if unavailable.
        /// </summary>
        public static bool IsInDangerZone(BotOwner bot)
        {
            if (bot == null)
            {
                return false;
            }

            BotDangerArea dangerArea = bot.DangerArea;
            if (dangerArea?.BlockedCovers == null)
            {
                return false;
            }

            return dangerArea.BlockedCovers.Count > 0;
        }
    }
}
