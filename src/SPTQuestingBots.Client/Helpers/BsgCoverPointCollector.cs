using Comfort.Common;
using EFT;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Queries BSG's pre-computed cover voxel grid (<see cref="AICoversData"/>) to collect
    /// nearby cover positions for squad tactical placement.
    /// <para>
    /// Signature matches <see cref="BotLogic.ECS.UtilityAI.CoverPositionSource"/> delegate,
    /// keeping <see cref="BotLogic.ECS.UtilityAI.GotoObjectiveStrategy"/> free of Unity/BSG deps.
    /// </para>
    /// <para>
    /// Algorithm follows Phobos <c>LocationGatherer.CollectBuiltinCoverData</c>:
    /// voxel neighborhood query → inner-radius distance filter → Wall covers first.
    /// </para>
    /// </summary>
    public static class BsgCoverPointCollector
    {
        /// <summary>
        /// BSG voxel grid cell size (XZ=10, Y=5). We use XZ for search range calculation.
        /// </summary>
        private const float VoxelSize = 10f;

        /// <summary>
        /// Collects cover positions from BSG's voxel grid near the given objective.
        /// Wall cover types are preferred; other types fill remaining slots.
        /// </summary>
        /// <param name="objX">Objective X position.</param>
        /// <param name="objY">Objective Y position.</param>
        /// <param name="objZ">Objective Z position.</param>
        /// <param name="radius">Search radius around the objective.</param>
        /// <param name="outPositions">Output buffer for x,y,z position triples.</param>
        /// <param name="maxCount">Maximum number of positions to collect.</param>
        /// <returns>Number of positions written to outPositions.</returns>
        public static int CollectCoverPositions(float objX, float objY, float objZ, float radius, float[] outPositions, int maxCount)
        {
            if (maxCount <= 0)
                return 0;

            var botGame = Singleton<IBotGame>.Instance;
            if (botGame == null)
                return 0;

            var coversData = botGame.BotsController?.CoversData;
            if (coversData == null)
                return 0;

            var objective = new Vector3(objX, objY, objZ);
            var voxelIndex = coversData.GetIndexes(objective);
            int searchRange = Mathf.CeilToInt(2f * radius / VoxelSize);

            var voxels = coversData.GetVoxelesExtended(voxelIndex.x, voxelIndex.y, voxelIndex.z, searchRange, true);

            if (voxels == null || voxels.Count == 0)
                return 0;

            // Phobos uses 0.75 * radius as inner filter radius
            float innerRadius = 0.75f * radius;
            float innerRadiusSqr = innerRadius * innerRadius;
            int count = 0;

            // First pass: Wall cover points (hard cover, preferred for tactical positions)
            for (int i = 0; i < voxels.Count && count < maxCount; i++)
            {
                var points = voxels[i].Points;
                for (int j = 0; j < points.Count && count < maxCount; j++)
                {
                    var gp = points[j];
                    if (gp.CoverType != CoverType.Wall)
                        continue;

                    float dx = gp.Position.x - objX;
                    float dy = gp.Position.y - objY;
                    float dz = gp.Position.z - objZ;
                    if (dx * dx + dy * dy + dz * dz > innerRadiusSqr)
                        continue;

                    outPositions[count * 3] = gp.Position.x;
                    outPositions[count * 3 + 1] = gp.Position.y;
                    outPositions[count * 3 + 2] = gp.Position.z;
                    count++;
                }
            }

            // Second pass: any remaining cover types (foliage, etc.) if Wall wasn't enough
            if (count < maxCount)
            {
                for (int i = 0; i < voxels.Count && count < maxCount; i++)
                {
                    var points = voxels[i].Points;
                    for (int j = 0; j < points.Count && count < maxCount; j++)
                    {
                        var gp = points[j];
                        if (gp.CoverType == CoverType.Wall)
                            continue; // already collected in first pass

                        float dx = gp.Position.x - objX;
                        float dy = gp.Position.y - objY;
                        float dz = gp.Position.z - objZ;
                        if (dx * dx + dy * dy + dz * dz > innerRadiusSqr)
                            continue;

                        outPositions[count * 3] = gp.Position.x;
                        outPositions[count * 3 + 1] = gp.Position.y;
                        outPositions[count * 3 + 2] = gp.Position.z;
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
