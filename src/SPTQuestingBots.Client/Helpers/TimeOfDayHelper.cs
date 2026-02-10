using Comfort.Common;
using EFT;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    public static class TimeOfDayHelper
    {
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
