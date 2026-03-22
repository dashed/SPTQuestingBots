using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Validates that movement targets share the same <c>ConnectionGroupId</c> as the bot's
    /// current position. BSG's navigation graph partitions the map into connection groups;
    /// points in different groups may not have valid NavMesh paths between them.
    /// <para>
    /// Uses <c>BotsController.CoversData.Points</c> (the pre-baked <see cref="GroupPoint"/> list)
    /// to find the nearest cover point and read its <c>ConnectionGroup</c>.
    /// </para>
    /// </summary>
    public static class ConnectionGroupHelper
    {
        /// <summary>
        /// Get the ConnectionGroup ID for the nearest cover point to the given position.
        /// Returns -1 if the data is unavailable or no nearby point is found.
        /// </summary>
        /// <param name="position">World-space position to query.</param>
        /// <param name="maxSearchRadius">Maximum distance to search for a cover point.</param>
        /// <returns>ConnectionGroup ID, or -1 if not found.</returns>
        public static int GetConnectionGroup(Vector3 position, float maxSearchRadius = 50f)
        {
            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                if (botGame == null)
                    return -1;

                var coversData = botGame.BotsController?.CoversData;
                if (coversData == null)
                    return -1;

                var points = coversData.Points;
                if (points == null || points.Count == 0)
                    return -1;

                float maxRadiusSqr = maxSearchRadius * maxSearchRadius;
                float bestDistSqr = float.MaxValue;
                int bestGroup = -1;

                for (int i = 0; i < points.Count; i++)
                {
                    var gp = points[i];
                    if (gp == null)
                        continue;

                    float dx = gp._position.x - position.x;
                    float dy = gp._position.y - position.y;
                    float dz = gp._position.z - position.z;
                    float distSqr = dx * dx + dy * dy + dz * dz;

                    if (distSqr < bestDistSqr && distSqr < maxRadiusSqr)
                    {
                        bestDistSqr = distSqr;
                        bestGroup = gp.ConnectionGroup;
                    }
                }

                return bestGroup;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogDebug("[ConnectionGroupHelper] Failed to query connection group: " + ex.Message);
                return -1;
            }
        }

        /// <summary>
        /// Check if two positions are in the same ConnectionGroup.
        /// Returns true if both positions resolve to the same group, or if either cannot be resolved
        /// (fail-open to avoid blocking valid movement).
        /// </summary>
        /// <param name="from">Source position.</param>
        /// <param name="to">Destination position.</param>
        /// <param name="maxSearchRadius">Maximum distance to search for cover points.</param>
        /// <returns>True if positions share the same ConnectionGroup or if validation is inconclusive.</returns>
        public static bool AreConnected(Vector3 from, Vector3 to, float maxSearchRadius = 50f)
        {
            int fromGroup = GetConnectionGroup(from, maxSearchRadius);
            int toGroup = GetConnectionGroup(to, maxSearchRadius);

            // Fail-open: if we can't determine either group, don't block movement
            if (fromGroup < 0 || toGroup < 0)
                return true;

            bool connected = fromGroup == toGroup;
            if (!connected)
            {
                LoggingController.LogDebug(
                    "[ConnectionGroupHelper] Positions in different groups: from="
                        + fromGroup
                        + " at ("
                        + from.x.ToString("F0")
                        + ","
                        + from.z.ToString("F0")
                        + ") to="
                        + toGroup
                        + " at ("
                        + to.x.ToString("F0")
                        + ","
                        + to.z.ToString("F0")
                        + ")"
                );
            }

            return connected;
        }

        /// <summary>
        /// Get the ConnectionGroup ID for a bot's current position using its BotOwner.
        /// </summary>
        public static int GetBotConnectionGroup(BotOwner botOwner, float maxSearchRadius = 50f)
        {
            if (botOwner == null)
                return -1;

            try
            {
                return GetConnectionGroup(botOwner.Position, maxSearchRadius);
            }
            catch
            {
                return -1;
            }
        }
    }
}
