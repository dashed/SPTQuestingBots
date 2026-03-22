using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Queries BSG's <see cref="AIPlaceInfoHolder"/> for per-area environment data.
    /// Each <see cref="AIPlaceInfo"/> has a <see cref="BoxCollider"/> defining its volume,
    /// plus <c>IsInside</c>, <c>IsDark</c>, and <c>AreaId</c> flags.
    /// <para>
    /// Use alongside <c>Player.Environment</c> for more reliable indoor detection
    /// in room clearing and tactical positioning.
    /// </para>
    /// </summary>
    public static class PlaceInfoHelper
    {
        /// <summary>
        /// Query the nearest <see cref="AIPlaceInfo"/> that contains the given position.
        /// Uses the BoxCollider bounds check to determine containment.
        /// </summary>
        /// <param name="position">World-space position to query.</param>
        /// <returns>Place info data; IsValid=false if no matching place found.</returns>
        public static PlaceInfoData GetPlaceInfo(Vector3 position)
        {
            var result = new PlaceInfoData();

            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                if (botGame == null)
                    return result;

                var coversData = botGame.BotsController?.CoversData;
                if (coversData == null)
                    return result;

                var placeHolder = coversData.AIPlaceInfoHolder;
                if (placeHolder == null)
                    return result;

                var places = placeHolder.Places;
                if (places == null || places.Count == 0)
                    return result;

                for (int i = 0; i < places.Count; i++)
                {
                    var place = places[i];
                    if (place == null)
                        continue;

                    var collider = place.Collider;
                    if (collider == null)
                        continue;

                    // Check if position is within the box collider bounds
                    if (collider.bounds.Contains(position))
                    {
                        result.IsInside = place.IsInside;
                        result.IsDark = place.IsDark;
                        result.AreaId = place.AreaId;
                        result.IsValid = true;
                        return result;
                    }
                }

                return result;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogDebug("[PlaceInfoHelper] Failed to query place info: " + ex.Message);
                return result;
            }
        }

        /// <summary>
        /// Check if a position is considered "inside" by BSG's place info system.
        /// Returns the Player.Environment-based value as fallback if no AIPlaceInfo covers the position.
        /// </summary>
        /// <param name="position">World-space position to query.</param>
        /// <param name="playerEnvironmentIsIndoor">Fallback from Player.Environment.</param>
        /// <returns>True if the position is inside a building.</returns>
        public static bool IsInsideBuilding(Vector3 position, bool playerEnvironmentIsIndoor)
        {
            var info = GetPlaceInfo(position);
            if (info.IsValid)
                return info.IsInside;

            return playerEnvironmentIsIndoor;
        }

        /// <summary>
        /// Check if a position is in a dark area according to BSG's place info.
        /// </summary>
        /// <param name="position">World-space position to query.</param>
        /// <returns>True if the area is dark; false if not dark or no data.</returns>
        public static bool IsDarkArea(Vector3 position)
        {
            var info = GetPlaceInfo(position);
            return info.IsValid && info.IsDark;
        }

        /// <summary>
        /// Get the total number of AIPlaceInfo zones on the current map.
        /// Useful for diagnostics.
        /// </summary>
        public static int GetPlaceCount()
        {
            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                if (botGame == null)
                    return 0;

                var places = botGame.BotsController?.CoversData?.AIPlaceInfoHolder?.Places;
                return places?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
