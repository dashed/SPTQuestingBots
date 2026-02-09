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
                return true;
            }

            outX = outY = outZ = 0f;
            return false;
        }
    }
}
