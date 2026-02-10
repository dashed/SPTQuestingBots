using EFT;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Thin Unity wrapper for look direction commands.
    /// This is the ONLY file with Unity dependencies in the look variance system.
    /// </summary>
    public static class LookDirectionHelper
    {
        /// <summary>
        /// Makes the bot look toward a world position (x, y, z).
        /// Computes a horizontal direction vector from the bot's position and uses
        /// <c>BotOwner.Steering.LookToDirection</c> to smoothly rotate.
        /// </summary>
        public static void LookAt(BotOwner bot, float x, float y, float z)
        {
            if (bot?.Steering == null)
                return;

            var direction = new Vector3(x - bot.Position.x, 0f, z - bot.Position.z);
            if (direction.sqrMagnitude < 0.001f)
                return;

            bot.Steering.LookToDirection(direction.normalized);
        }
    }
}
