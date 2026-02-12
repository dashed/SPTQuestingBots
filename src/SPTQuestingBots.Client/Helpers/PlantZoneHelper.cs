using System.Reflection;
using EFT;
using EFT.Interactive;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Reads the Player.PlaceItemZone backing field to check if a bot
    /// is inside a quest item plant zone.
    /// </summary>
    public static class PlantZoneHelper
    {
        private static readonly FieldInfo _placeItemZoneField = ReflectionHelper.RequireField(
            typeof(Player),
            "<PlaceItemZone>k__BackingField",
            "PlantZoneHelper — quest item plant zone trigger"
        );

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
        /// or <c>null</c> if not in a plant zone or if the field is unavailable.
        /// </summary>
        public static PlaceItemTrigger GetPlantZone(BotOwner bot)
        {
            if (bot?.GetPlayer == null || _placeItemZoneField == null)
            {
                return null;
            }

            return _placeItemZoneField.GetValue(bot.GetPlayer) as PlaceItemTrigger;
        }
    }
}
