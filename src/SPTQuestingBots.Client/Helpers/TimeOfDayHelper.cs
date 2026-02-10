using Comfort.Common;
using EFT;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Provides time-of-day detection range modifiers for the vulture system.
    /// Returns a multiplier that reduces detection range at night and during
    /// dawn/dusk transitions, simulating reduced visibility.
    /// </summary>
    public static class TimeOfDayHelper
    {
        /// <summary>
        /// Computes a detection range multiplier based on the current in-game time of day.
        /// Returns 1.0 during daytime (07:00-19:00), the full <paramref name="nightMultiplier"/>
        /// at night (22:00-05:00), and interpolated values during dawn (05:00-07:00) and
        /// dusk (19:00-22:00).
        /// </summary>
        /// <param name="nightMultiplier">The multiplier to apply during nighttime (e.g. 0.65 for 65% range).</param>
        /// <returns>A value between <paramref name="nightMultiplier"/> and 1.0, or 1.0 if game time is unavailable.</returns>
        public static float GetDetectionRangeModifier(float nightMultiplier)
        {
            var gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld?.GameDateTime == null)
                return 1f;

            var dateTime = gameWorld.GameDateTime.Calculate();
            float hour = dateTime.Hour + dateTime.Minute / 60f;

            // Night: 22:00-05:00
            if (hour >= 22f || hour < 5f)
                return nightMultiplier;

            // Dawn: 05:00-07:00
            if (hour >= 5f && hour < 7f)
                return Mathf.Lerp(nightMultiplier, 1f, 0.5f);

            // Dusk: 19:00-22:00
            if (hour >= 19f && hour < 22f)
                return Mathf.Lerp(1f, nightMultiplier, (hour - 19f) / 3f);

            // Day: 07:00-19:00
            return 1f;
        }
    }
}
