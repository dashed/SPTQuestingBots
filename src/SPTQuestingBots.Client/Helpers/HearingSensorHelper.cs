using EFT;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Provides access to the bot's hearing sensor via direct property access.
    /// Infrastructure only — behavioral integration (sound reaction) is deferred.
    /// </summary>
    public static class HearingSensorHelper
    {
        /// <summary>
        /// Returns the <see cref="BotHearingSensor"/> for the given bot,
        /// or <c>null</c> if unavailable.
        /// </summary>
        public static BotHearingSensor GetHearingSensor(BotOwner bot)
        {
            return bot?.HearingSensor;
        }

        /// <summary>
        /// Returns whether the bot is currently suspicious (has recently heard an enemy sound).
        /// Delegates to the ECS sensor state populated by <see cref="BotLogic.BotMonitor.Monitors.BotHearingMonitor"/>.
        /// Returns <c>false</c> if the bot is not registered in the ECS.
        /// </summary>
        public static bool IsSuspicious(BotOwner bot)
        {
            return BotEntityBridge.GetSensorForBot(bot, BotSensor.IsSuspicious);
        }
    }
}
