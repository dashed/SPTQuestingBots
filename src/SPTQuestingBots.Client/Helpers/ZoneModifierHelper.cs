using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Reads <see cref="BotLocationModifier"/> from a <see cref="BotZone"/> to provide
    /// per-zone tuning parameters for utility scoring and behavior.
    /// Each zone has 24 parameters controlling accuracy, visibility, sleep distance, weather, etc.
    /// </summary>
    public static class ZoneModifierHelper
    {
        /// <summary>
        /// Read the <see cref="BotLocationModifier"/> for the given bot's current zone.
        /// Returns a default struct with IsValid=false if the data is unavailable.
        /// </summary>
        public static ZoneModifierData GetModifierForBot(BotOwner botOwner)
        {
            var result = new ZoneModifierData();

            try
            {
                if (botOwner == null)
                    return result;

                var zone = botOwner.BotsGroup?.BotZone;
                if (zone == null)
                    return result;

                return GetModifierForZone(zone);
            }
            catch (System.Exception ex)
            {
                LoggingController.LogDebug("[ZoneModifierHelper] Failed to read modifier for bot: " + ex.Message);
                return result;
            }
        }

        /// <summary>
        /// Read the <see cref="BotLocationModifier"/> for a specific zone.
        /// </summary>
        public static ZoneModifierData GetModifierForZone(BotZone zone)
        {
            var result = new ZoneModifierData();

            try
            {
                if (zone == null)
                    return result;

                var modifier = zone.Modifier;
                if (modifier == null)
                    return result;

                result.VisibleDistance = modifier.VisibleDistance;
                result.DistToSleep = modifier.DistToSleep;
                result.DistToActivate = modifier.DistToActivate;
                result.AccuracySpeed = modifier.AccuracySpeed;
                result.GainSight = modifier.GainSight;
                result.Scattering = modifier.Scattering;
                result.FogVisibilityDistanceCoef = modifier.FogVisibilityDistanceCoef;
                result.RainVisibilityDistanceCoef = modifier.RainVisibilityDistanceCoef;
                result.IsValid = true;

                return result;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogDebug("[ZoneModifierHelper] Failed to read zone modifier: " + ex.Message);
                return result;
            }
        }

        /// <summary>
        /// Read the global <see cref="BotLocationModifier"/> from the <see cref="BotsController"/>.
        /// This is the map-wide default modifier, not zone-specific.
        /// Returns a default struct with IsValid=false if unavailable.
        /// </summary>
        public static ZoneModifierData GetGlobalModifier()
        {
            var result = new ZoneModifierData();

            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                if (botGame == null)
                    return result;

                var modifier = botGame.BotsController?.BotLocationModifier;
                if (modifier == null)
                    return result;

                result.VisibleDistance = modifier.VisibleDistance;
                result.DistToSleep = modifier.DistToSleep;
                result.DistToActivate = modifier.DistToActivate;
                result.AccuracySpeed = modifier.AccuracySpeed;
                result.GainSight = modifier.GainSight;
                result.Scattering = modifier.Scattering;
                result.FogVisibilityDistanceCoef = modifier.FogVisibilityDistanceCoef;
                result.RainVisibilityDistanceCoef = modifier.RainVisibilityDistanceCoef;
                result.IsValid = true;

                return result;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogWarning("[ZoneModifierHelper] Failed to read global modifier: " + ex.Message);
                return result;
            }
        }
    }
}
