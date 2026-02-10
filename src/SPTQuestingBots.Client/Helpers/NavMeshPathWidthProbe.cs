using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Probes NavMesh laterally (perpendicular to heading) to measure
    /// available path width for formation type selection.
    /// </summary>
    public static class NavMeshPathWidthProbe
    {
        /// <summary>Maximum lateral probe distance per side (meters).</summary>
        public const float MaxProbeDistance = 15f;

        /// <summary>
        /// Measure the walkable width perpendicular to the given heading at the specified position.
        /// Casts NavMesh.Raycast left and right perpendicular to heading direction.
        /// Returns total width (left distance + right distance).
        /// </summary>
        public static float MeasureWidth(float posX, float posY, float posZ, float headingX, float headingZ)
        {
            var position = new Vector3(posX, posY, posZ);

            // Perpendicular left: rotate heading 90° CCW in XZ → (-headingZ, 0, headingX)
            var leftTarget = new Vector3(posX - headingZ * MaxProbeDistance, posY, posZ + headingX * MaxProbeDistance);

            // Perpendicular right: rotate heading 90° CW in XZ → (headingZ, 0, -headingX)
            var rightTarget = new Vector3(posX + headingZ * MaxProbeDistance, posY, posZ - headingX * MaxProbeDistance);

            float leftDist = MaxProbeDistance;
            if (NavMesh.Raycast(position, leftTarget, out var leftHit, NavMesh.AllAreas))
                leftDist = leftHit.distance;

            float rightDist = MaxProbeDistance;
            if (NavMesh.Raycast(position, rightTarget, out var rightHit, NavMesh.AllAreas))
                rightDist = rightHit.distance;

            return leftDist + rightDist;
        }
    }
}
