using EFT;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Reads BotOwner public properties to provide exfiltration state information.
    /// Informational only — behavioral extraction integration is deferred.
    /// </summary>
    public static class ExtractionHelper
    {
        /// <summary>
        /// Returns <c>true</c> if the bot has exfiltration data assigned.
        /// Returns <c>false</c> if unavailable or not assigned.
        /// </summary>
        public static bool HasExfiltrationAssigned(BotOwner bot)
        {
            return bot?.Exfiltration != null;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot's <see cref="BotLeaveData"/> indicates it wants to leave the raid.
        /// Checks the <c>WannaLeave</c> property. Returns <c>false</c> if unavailable.
        /// </summary>
        public static bool IsLeaving(BotOwner bot)
        {
            return bot?.LeaveData?.WannaLeave == true;
        }
    }
}
