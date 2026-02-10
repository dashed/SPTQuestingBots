using SPTQuestingBots.Controllers;
using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Static wrapper around NavMesh.SamplePosition for validating/snapping
    /// tactical positions to walkable NavMesh surfaces.
    /// <para>
    /// Designed to be passed as a <see cref="BotLogic.ECS.UtilityAI.PositionValidator"/>
    /// delegate to keep GotoObjectiveStrategy free of Unity dependencies.
    /// </para>
    /// </summary>
    public static class NavMeshPositionValidator
    {
        /// <summary>
        /// Maximum distance from the input position to search for a valid NavMesh point.
        /// Set from <see cref="Configuration.SquadStrategyConfig.NavMeshSampleRadius"/> at init time.
        /// </summary>
        public static float SampleRadius { get; set; } = 2.0f;

        /// <summary>
        /// Attempts to snap the given world position to the nearest walkable NavMesh surface.
        /// Signature matches <see cref="BotLogic.ECS.UtilityAI.PositionValidator"/>.
        /// </summary>
        public static bool TrySnap(float inX, float inY, float inZ, out float outX, out float outY, out float outZ)
        {
            Vector3 source = new Vector3(inX, inY, inZ);

            if (NavMesh.SamplePosition(source, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas))
            {
                outX = hit.position.x;
                outY = hit.position.y;
                outZ = hit.position.z;
                LoggingController.LogDebug(
                    "[NavMeshPositionValidator] Snapped ("
                        + inX
                        + ", "
                        + inY
                        + ", "
                        + inZ
                        + ") -> ("
                        + outX
                        + ", "
                        + outY
                        + ", "
                        + outZ
                        + ")"
                );
                return true;
            }

            outX = outY = outZ = 0f;
            LoggingController.LogDebug(
                "[NavMeshPositionValidator] Failed to snap (" + inX + ", " + inY + ", " + inZ + ") within radius=" + SampleRadius
            );
            return false;
        }

        /// <summary>
        /// Checks if a walkable NavMesh path exists between two positions
        /// within a maximum path length budget.
        /// Signature matches <see cref="BotLogic.ECS.UtilityAI.ReachabilityValidator"/>.
        /// </summary>
        public static bool IsReachable(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float maxPathLength)
        {
            Vector3 from = new Vector3(fromX, fromY, fromZ);
            Vector3 to = new Vector3(toX, toY, toZ);
            NavMeshPath path = new NavMeshPath();

            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path))
                return false;

            if (path.status != NavMeshPathStatus.PathComplete)
                return false;

            // Calculate total path length
            float totalLength = 0f;
            Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                float dx = corners[i].x - corners[i - 1].x;
                float dy = corners[i].y - corners[i - 1].y;
                float dz = corners[i].z - corners[i - 1].z;
                totalLength += (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            bool reachable = totalLength <= maxPathLength;
            LoggingController.LogDebug(
                "[NavMeshPositionValidator] Reachability: pathLen=" + totalLength + " maxLen=" + maxPathLength + " reachable=" + reachable
            );
            return reachable;
        }

        /// <summary>
        /// Checks line-of-sight between two positions using Physics.Linecast.
        /// Returns true if there are NO obstacles blocking the view.
        /// Signature matches <see cref="BotLogic.ECS.UtilityAI.LosValidator"/>.
        /// </summary>
        public static bool HasLineOfSight(float fromX, float fromY, float fromZ, float toX, float toY, float toZ)
        {
            Vector3 from = new Vector3(fromX, fromY + 0.5f, fromZ); // Offset up from ground
            Vector3 to = new Vector3(toX, toY + 0.5f, toZ);

            // Returns true if NO obstacle was hit (i.e., line of sight exists)
            bool hasLos = !Physics.Linecast(from, to);
            LoggingController.LogDebug(
                "[NavMeshPositionValidator] LOS check: (" + fromX + ", " + fromZ + ") -> (" + toX + ", " + toZ + ") = " + hasLos
            );
            return hasLos;
        }
    }
}
