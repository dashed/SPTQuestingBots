using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Null-safe static helpers for danger avoidance: danger places, mines, and grenade positions.
    /// Wraps BSG's <c>VoxelesPersonalData</c>, <c>MinesData</c>, and <c>AIPlaceInfo.GrenadePlaces</c>.
    /// </summary>
    public static class DangerAvoidanceHelper
    {
        /// <summary>
        /// Returns an array of nearby danger place positions that the bot should avoid.
        /// Reads from <c>BotOwner.VoxelesPersonalData.CurVoxel.GetNearestPlacesToAvoid()</c>.
        /// Returns an empty array if no danger places are found or the API is unavailable.
        /// </summary>
        public static Vector3[] GetNearbyDangerPlaces(BotOwner bot)
        {
            if (bot == null)
                return System.Array.Empty<Vector3>();

            try
            {
                var voxelData = bot.VoxelesPersonalData;
                if (voxelData == null)
                    return System.Array.Empty<Vector3>();

                var curVoxel = voxelData.CurVoxel;
                if (curVoxel == null)
                    return System.Array.Empty<Vector3>();

                var places = curVoxel.PlacesToAvoid;
                if (places == null || places.Count == 0)
                    return System.Array.Empty<Vector3>();

                var result = new Vector3[places.Count];
                for (int i = 0; i < places.Count; i++)
                {
                    result[i] = places[i].Position;
                }

                return result;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogDebug("[DangerAvoidanceHelper] GetNearbyDangerPlaces error: " + ex.Message);
                return System.Array.Empty<Vector3>();
            }
        }

        /// <summary>
        /// Attempts to reroute the bot's current path around known danger places.
        /// Uses <c>BotOwner.Mover.TryRelacePathAround()</c> with the danger positions
        /// as avoidance points.
        /// </summary>
        /// <returns><c>true</c> if the path was successfully rerouted.</returns>
        public static bool TryRerouteAroundDanger(BotOwner bot, Vector3[] dangerPositions)
        {
            if (bot?.Mover == null || dangerPositions == null || dangerPositions.Length == 0)
                return false;

            try
            {
                // Use the centroid of danger positions as the avoidance center
                Vector3 center = Vector3.zero;
                for (int i = 0; i < dangerPositions.Length; i++)
                {
                    center += dangerPositions[i];
                }
                center /= dangerPositions.Length;

                bool success = bot.Mover.TryRelacePathAround(center, null, out var avoidancePath);

                if (success && avoidancePath != null)
                {
                    LoggingController.LogDebug(
                        "[DangerAvoidanceHelper] Rerouted path around " + dangerPositions.Length + " danger place(s) for " + bot.GetText()
                    );
                    return true;
                }

                return false;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogDebug("[DangerAvoidanceHelper] TryRerouteAroundDanger error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Returns the positions of known mines near the bot.
        /// Reads from <c>BotsGroup.BotGame.BotsController.CoversData.AIMinesPositions.MinesSimple</c>.
        /// Returns an empty array if no mines are found or the API is unavailable.
        /// </summary>
        public static Vector3[] GetNearbyMinePositions(BotOwner bot, float maxDistance = 50f)
        {
            if (bot == null)
                return System.Array.Empty<Vector3>();

            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                if (botGame == null)
                    return System.Array.Empty<Vector3>();

                var minesData = botGame.BotsController?.CoversData?.AIMinesPositions;
                if (minesData == null)
                    return System.Array.Empty<Vector3>();

                var mines = minesData.Mines;
                if (mines == null || mines.Count == 0)
                    return System.Array.Empty<Vector3>();

                float maxDistSqr = maxDistance * maxDistance;
                Vector3 botPos = bot.Position;

                // First pass: count nearby mines
                int nearbyCount = 0;
                for (int i = 0; i < mines.Count; i++)
                {
                    var mine = mines[i];
                    if (mine == null)
                        continue;
                    Vector3 minePos = mine.transform.position;
                    if ((minePos - botPos).sqrMagnitude <= maxDistSqr)
                        nearbyCount++;
                }

                if (nearbyCount == 0)
                    return System.Array.Empty<Vector3>();

                // Second pass: collect positions
                var result = new Vector3[nearbyCount];
                int idx = 0;
                for (int i = 0; i < mines.Count; i++)
                {
                    var mine = mines[i];
                    if (mine == null)
                        continue;
                    Vector3 minePos = mine.transform.position;
                    if ((minePos - botPos).sqrMagnitude <= maxDistSqr)
                    {
                        result[idx++] = minePos;
                    }
                }

                return result;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogDebug("[DangerAvoidanceHelper] GetNearbyMinePositions error: " + ex.Message);
                return System.Array.Empty<Vector3>();
            }
        }

        /// <summary>
        /// Returns pre-computed grenade throw positions from the nearest AIPlaceInfo.
        /// These positions indicate known vantage points for throwing grenades.
        /// </summary>
        public static Vector3[] GetNearbyGrenadePlaces(Vector3 position)
        {
            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                if (botGame == null)
                    return System.Array.Empty<Vector3>();

                var placeHolder = botGame.BotsController?.CoversData?.AIPlaceInfoHolder;
                if (placeHolder == null)
                    return System.Array.Empty<Vector3>();

                var places = placeHolder.Places;
                if (places == null || places.Count == 0)
                    return System.Array.Empty<Vector3>();

                // Find the place info that contains this position
                for (int i = 0; i < places.Count; i++)
                {
                    var place = places[i];
                    if (place?.Collider == null)
                        continue;

                    if (!place.Collider.bounds.Contains(position))
                        continue;

                    var grenadePlaces = place.GrenadePlaces;
                    if (grenadePlaces == null || grenadePlaces.Length == 0)
                        continue;

                    var result = new Vector3[grenadePlaces.Length];
                    for (int j = 0; j < grenadePlaces.Length; j++)
                    {
                        result[j] = grenadePlaces[j].From.position;
                    }

                    return result;
                }

                return System.Array.Empty<Vector3>();
            }
            catch (System.Exception ex)
            {
                LoggingController.LogDebug("[DangerAvoidanceHelper] GetNearbyGrenadePlaces error: " + ex.Message);
                return System.Array.Empty<Vector3>();
            }
        }
    }
}
