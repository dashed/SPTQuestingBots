using System.Reflection;
using EFT;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Reads BotOwner extraction fields to provide exfiltration state information.
    /// Informational only — behavioral extraction integration is deferred.
    /// </summary>
    public static class ExtractionHelper
    {
        private static readonly FieldInfo _exfiltrationField = ReflectionHelper.RequireField(
            typeof(BotOwner),
            "<Exfiltration>k__BackingField",
            "ExtractionHelper — bot exfiltration data"
        );

        private static readonly FieldInfo _leaveDataField = ReflectionHelper.RequireField(
            typeof(BotOwner),
            "<LeaveData>k__BackingField",
            "ExtractionHelper — bot leave/extract decision data"
        );

        /// <summary>
        /// Returns <c>true</c> if the bot has exfiltration data assigned.
        /// Returns <c>false</c> if unavailable or not assigned.
        /// </summary>
        public static bool HasExfiltrationAssigned(BotOwner bot)
        {
            if (bot == null || _exfiltrationField == null)
            {
                return false;
            }

            return _exfiltrationField.GetValue(bot) != null;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot's <see cref="BotLeaveData"/> indicates it wants to leave the raid.
        /// Checks the <c>WannaLeave</c> property. Returns <c>false</c> if unavailable.
        /// </summary>
        public static bool IsLeaving(BotOwner bot)
        {
            if (bot == null || _leaveDataField == null)
            {
                return false;
            }

            object leaveData = _leaveDataField.GetValue(bot);
            if (leaveData == null)
            {
                return false;
            }

            var botLeaveData = leaveData as BotLeaveData;
            if (botLeaveData == null)
            {
                return false;
            }

            return botLeaveData.WannaLeave;
        }
    }
}
