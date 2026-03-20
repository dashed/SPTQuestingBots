using EFT;
using EFT.Interactive;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Checks if a bot is inside a quest item plant zone via
    /// the Player.PlaceItemZone public property.
    /// </summary>
    public static class PlantZoneHelper
    {
        /// <summary>
        /// Returns <c>true</c> if the bot's player is currently inside a plant zone
        /// (i.e. has a non-null <see cref="PlaceItemTrigger"/>).
        /// </summary>
        public static bool IsInPlantZone(BotOwner bot)
        {
            return GetPlantZone(bot) != null;
        }

        /// <summary>
        /// Returns the <see cref="PlaceItemTrigger"/> the bot is currently inside,
        /// or <c>null</c> if not in a plant zone.
        /// </summary>
        public static PlaceItemTrigger GetPlantZone(BotOwner bot)
        {
            return bot?.GetPlayer?.PlaceItemZone;
        }
    }
}
