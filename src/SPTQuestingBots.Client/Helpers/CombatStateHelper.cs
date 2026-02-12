using System.Reflection;
using EFT;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Reads BotsGroup and BotOwner fields via reflection to provide
    /// combat state information for questing decisions.
    /// </summary>
    public static class CombatStateHelper
    {
        private static readonly FieldInfo _enemyLastSeenTimeSenceField = ReflectionHelper.RequireField(
            typeof(BotsGroup),
            "<EnemyLastSeenTimeSence>k__BackingField",
            "CombatStateHelper — perceived time of last enemy sighting"
        );

        private static readonly FieldInfo _enemyLastSeenTimeRealField = ReflectionHelper.RequireField(
            typeof(BotsGroup),
            "<EnemyLastSeenTimeReal>k__BackingField",
            "CombatStateHelper — real time of last enemy sighting"
        );

        private static readonly FieldInfo _enemyLastSeenPositionRealField = ReflectionHelper.RequireField(
            typeof(BotsGroup),
            "<EnemyLastSeenPositionReal>k__BackingField",
            "CombatStateHelper — real position of last enemy sighting"
        );

        private static readonly FieldInfo _enemyLastSeenPositionSenceField = ReflectionHelper.RequireField(
            typeof(BotsGroup),
            "<EnemyLastSeenPositionSence>k__BackingField",
            "CombatStateHelper — perceived enemy position"
        );

        private static readonly FieldInfo _dangerAreaField = ReflectionHelper.RequireField(
            typeof(BotOwner),
            "<DangerArea>k__BackingField",
            "CombatStateHelper — danger area awareness"
        );

        private static readonly FieldInfo _botAvoidDangerPlacesField = ReflectionHelper.RequireField(
            typeof(BotOwner),
            "<BotAvoidDangerPlaces>k__BackingField",
            "CombatStateHelper — danger place avoidance"
        );

        /// <summary>
        /// Returns the number of seconds since the bot's group last saw an enemy (real time),
        /// or <c>null</c> if unavailable.
        /// </summary>
        public static float? GetTimeSinceLastCombat(BotOwner bot)
        {
            if (bot?.BotsGroup == null || _enemyLastSeenTimeRealField == null)
            {
                return null;
            }

            float lastSeenTime = (float)_enemyLastSeenTimeRealField.GetValue(bot.BotsGroup);

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
            if (bot?.BotsGroup == null || _enemyLastSeenPositionRealField == null)
            {
                return null;
            }

            Vector3 position = (Vector3)_enemyLastSeenPositionRealField.GetValue(bot.BotsGroup);

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
            if (bot == null || _dangerAreaField == null)
            {
                return false;
            }

            object dangerArea = _dangerAreaField.GetValue(bot);
            if (dangerArea == null)
            {
                return false;
            }

            // BotDangerArea.BlockedCovers is a public Dictionary field;
            // if it has entries, the bot is aware of danger in the area.
            var botDangerArea = dangerArea as BotDangerArea;
            if (botDangerArea?.BlockedCovers == null)
            {
                return false;
            }

            return botDangerArea.BlockedCovers.Count > 0;
        }
    }
}
