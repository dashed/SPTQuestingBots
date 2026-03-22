using EFT;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Extracts patrol waypoints from BSG's native <see cref="PatrollingData"/> system
    /// and converts them to our <see cref="PatrolRoute"/> format for use as a fallback
    /// when no custom JSON-defined patrol routes are available.
    /// <para>
    /// Extracts rich data from each <see cref="PatrolPoint"/>: ShallSit, PatrolPointType,
    /// look directions (from <see cref="PointWithLookSides"/>), and sub-point counts.
    /// </para>
    /// </summary>
    public static class NativePatrolDataProvider
    {
        /// <summary>Default pause range for checkpoint waypoints (brief stop).</summary>
        private const float CheckPointPauseMin = 2f;
        private const float CheckPointPauseMax = 5f;

        /// <summary>Default pause range for stayPoint waypoints (longer hold).</summary>
        private const float StayPointPauseMin = 8f;
        private const float StayPointPauseMax = 20f;

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

                    // Determine pause duration based on point type
                    bool isStayPoint = point.PatrolPointType == PatrolPointType.stayPoint;
                    float pauseMin = isStayPoint ? StayPointPauseMin : CheckPointPauseMin;
                    float pauseMax = isStayPoint ? StayPointPauseMax : CheckPointPauseMax;

                    var wp = new PatrolWaypoint(pos.x, pos.y, pos.z, pauseMin, pauseMax);
                    wp.ShallSit = point.ShallSit;
                    wp.PointType = isStayPoint ? PatrolPointTypeId.StayPoint : PatrolPointTypeId.CheckPoint;

                    // Extract look direction from PointWithLookSides
                    ExtractLookDirection(point, ref wp);

                    // Count sub-points
                    ExtractSubPointCount(point, ref wp);

                    waypoints[validCount] = wp;
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

            int sitCount = 0;
            int lookCount = 0;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i].ShallSit)
                    sitCount++;
                if (waypoints[i].HasLookDirection)
                    lookCount++;
            }

            LoggingController.LogDebug(
                "[NativePatrolDataProvider] Created native route '"
                    + route.Name
                    + "' with "
                    + validCount
                    + " waypoints ("
                    + sitCount
                    + " sit, "
                    + lookCount
                    + " look) for bot "
                    + botOwner.GetText()
            );

            return route;
        }

        /// <summary>
        /// Extract the first look direction from a PatrolPoint's PointWithLookSides component.
        /// </summary>
        private static void ExtractLookDirection(PatrolPoint point, ref PatrolWaypoint wp)
        {
            try
            {
                var lookSides = point.PointWithLookSides;
                if (lookSides == null)
                    return;

                var directions = lookSides.Directions;
                if (directions == null || directions.Count == 0)
                    return;

                var dir = directions[0];
                float mag = (float)System.Math.Sqrt(dir.x * dir.x + dir.y * dir.y + dir.z * dir.z);
                if (mag < 0.001f)
                    return;

                wp.HasLookDirection = true;
                wp.LookDirX = dir.x / mag;
                wp.LookDirY = dir.y / mag;
                wp.LookDirZ = dir.z / mag;
            }
            catch
            {
                // PointWithLookSides may not be accessible
            }
        }

        /// <summary>
        /// Count sub-points under a PatrolPoint.
        /// </summary>
        private static void ExtractSubPointCount(PatrolPoint point, ref PatrolWaypoint wp)
        {
            try
            {
                // subPoints is a private field, accessed via reflection-like pattern
                // PatrolPoint has a public SubManual flag and private subPoints list
                // We use the SubManual flag as an indicator and set count to 1 if it has sub-points
                if (point.SubManual)
                {
                    wp.SubPointCount = 1; // At least one sub-point exists
                }
            }
            catch
            {
                // Ignore if not accessible
            }
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
