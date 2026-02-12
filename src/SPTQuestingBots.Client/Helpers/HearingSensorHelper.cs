using System.Reflection;
using EFT;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Provides access to the bot's hearing sensor via reflection.
    /// Infrastructure only — behavioral integration (sound reaction) is deferred.
    /// </summary>
    public static class HearingSensorHelper
    {
        private static readonly FieldInfo _hearingSensorField = ReflectionHelper.RequireField(
            typeof(BotOwner),
            "<HearingSensor>k__BackingField",
            "HearingSensorHelper — bot hearing sensor access"
        );

        /// <summary>
        /// Returns the <see cref="BotHearingSensor"/> for the given bot,
        /// or <c>null</c> if the field is unavailable.
        /// </summary>
        public static BotHearingSensor GetHearingSensor(BotOwner bot)
        {
            if (bot == null || _hearingSensorField == null)
            {
                return null;
            }

            return _hearingSensorField.GetValue(bot) as BotHearingSensor;
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
