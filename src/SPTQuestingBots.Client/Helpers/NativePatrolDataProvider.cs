using EFT;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Extracts patrol waypoints from BSG's native <see cref="PatrollingData"/> system
    /// and converts them to our <see cref="PatrolRoute"/> format for use as a fallback
    /// when no custom JSON-defined patrol routes are available.
    /// </summary>
    public static class NativePatrolDataProvider
    {
        /// <summary>Default pause range for native patrol waypoints.</summary>
        private const float DefaultPauseMin = 2f;
        private const float DefaultPauseMax = 5f;

        /// <summary>
        /// Try to extract a <see cref="PatrolRoute"/> from a bot's native PatrollingData.
        /// Returns null if the data is unavailable or has no usable waypoints.
        /// </summary>
        public static PatrolRoute TryCreateNativeRoute(BotOwner botOwner)
        {
            if (botOwner == null)
                return null;

            PatrollingData patrolData;
            try
            {
                patrolData = botOwner.PatrollingData;
            }
            catch
            {
                return null;
            }

            if (patrolData == null)
                return null;

            PatrolWay way;
            try
            {
                way = patrolData.Way;
            }
            catch
            {
                return null;
            }

            if (way == null || way.Points == null || way.Points.Count == 0)
                return null;

            var points = way.Points;
            var waypoints = new PatrolWaypoint[points.Count];
            int validCount = 0;

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point == null)
                    continue;

                try
                {
                    var pos = point.transform.position;
                    waypoints[validCount] = new PatrolWaypoint(pos.x, pos.y, pos.z, DefaultPauseMin, DefaultPauseMax);
                    validCount++;
                }
                catch
                {
                    // Skip points whose transform is not accessible
                }
            }

            if (validCount == 0)
                return null;

            // Trim array if some points were skipped
            if (validCount < waypoints.Length)
            {
                var trimmed = new PatrolWaypoint[validCount];
                System.Array.Copy(waypoints, trimmed, validCount);
                waypoints = trimmed;
            }

            // Determine route type from BSG's PatrolType
            var routeType = MapPatrolType(way.PatrolType);

            // Single-waypoint routes are degenerate — skip them
            if (validCount <= 1)
            {
                LoggingController.LogDebug(
                    "[NativePatrolDataProvider] Skipping native route with " + validCount + " waypoint(s) for bot " + botOwner.GetText()
                );
                return null;
            }

            var route = new PatrolRoute(name: "Native_" + way.name, type: routeType, waypoints: waypoints, isLoop: true);

            LoggingController.LogDebug(
                "[NativePatrolDataProvider] Created native route '"
                    + route.Name
                    + "' with "
                    + validCount
                    + " waypoints for bot "
                    + botOwner.GetText()
            );

            return route;
        }

        private static PatrolRouteType MapPatrolType(PatrolType bsgType)
        {
            switch (bsgType)
            {
                case PatrolType.patrolling:
                    return PatrolRouteType.Perimeter;
                case PatrolType.boss:
                    return PatrolRouteType.Overwatch;
                default:
                    return PatrolRouteType.Interior;
            }
        }
    }
}
