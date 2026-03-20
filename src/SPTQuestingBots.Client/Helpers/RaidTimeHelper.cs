using Comfort.Common;
using EFT;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Provides direct access to the raid timer via the AbstractGame.GameTimer public property.
    /// Complements <see cref="RaidHelpers"/> (which delegates to SPT's RaidTimeUtil) by
    /// exposing the raw <see cref="GameTimerClass"/> for advanced timer queries.
    /// </summary>
    public static class RaidTimeHelper
    {
        /// <summary>
        /// Returns the <see cref="GameTimerClass"/> from the current game instance, or <c>null</c>
        /// if the game is not running.
        /// </summary>
        public static GameTimerClass GetGameTimer()
        {
            if (!Singleton<AbstractGame>.Instantiated)
            {
                return null;
            }

            return Singleton<AbstractGame>.Instance.GameTimer;
        }

        /// <summary>
        /// Returns the fraction of raid time remaining (0.0 = raid over, 1.0 = full time),
        /// or <c>null</c> if the timer is unavailable.
        /// Delegates to <see cref="RaidHelpers.GetRaidTimeRemainingFraction"/> when available.
        /// </summary>
        public static float? GetRemainingRaidFraction()
        {
            if (!RaidHelpers.HasRaidStarted())
            {
                return null;
            }

            return RaidHelpers.GetRaidTimeRemainingFraction();
        }

        /// <summary>
        /// Returns the number of seconds remaining in the raid,
        /// or <c>null</c> if the timer is unavailable.
        /// Delegates to <see cref="RaidHelpers.GetRemainingRaidTimeSeconds"/> when available.
        /// </summary>
        public static float? GetRemainingSeconds()
        {
            if (!RaidHelpers.HasRaidStarted())
            {
                return null;
            }

            return RaidHelpers.GetRemainingRaidTimeSeconds();
        }
    }
}
