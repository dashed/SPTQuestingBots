using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
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
            {
                LoggingController.LogWarning("[BsgCoverPointCollector] IBotGame instance is null");
                return 0;
            }

            var coversData = botGame.BotsController?.CoversData;
            if (coversData == null)
            {
                LoggingController.LogWarning("[BsgCoverPointCollector] CoversData is null");
                return 0;
            }

            var objective = new Vector3(objX, objY, objZ);
            var voxelIndex = coversData.GetIndexes(objective);
            int searchRange = Mathf.CeilToInt(2f * radius / VoxelSize);

            var voxels = coversData.GetVoxelesExtended(voxelIndex.x, voxelIndex.y, voxelIndex.z, searchRange, true);

            if (voxels == null || voxels.Count == 0)
            {
                LoggingController.LogDebug(
                    "[BsgCoverPointCollector] No voxels found near (" + objX + ", " + objZ + ") with radius=" + radius
                );
                return 0;
            }

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

            LoggingController.LogDebug(
                "[BsgCoverPointCollector] Collected "
                    + count
                    + " cover positions near ("
                    + objX
                    + ", "
                    + objZ
                    + ") (max="
                    + maxCount
                    + ", radius="
                    + radius
                    + ")"
            );
            return count;
        }

        /// <summary>
        /// Collects cover positions from BSG's native cover graph, filtered by ConnectionGroup.
        /// Only returns points that share the same ConnectionGroup as the given target,
        /// ensuring the bot can actually navigate to them.
        /// </summary>
        /// <param name="connectionGroupId">The ConnectionGroup to filter by.</param>
        /// <param name="objX">Objective X position.</param>
        /// <param name="objY">Objective Y position.</param>
        /// <param name="objZ">Objective Z position.</param>
        /// <param name="radius">Search radius around the objective.</param>
        /// <param name="outPositions">Output buffer for x,y,z position triples.</param>
        /// <param name="maxCount">Maximum number of positions to collect.</param>
        /// <returns>Number of positions written to outPositions.</returns>
        public static int CollectCoverByConnectionGroup(
            int connectionGroupId,
            float objX,
            float objY,
            float objZ,
            float radius,
            float[] outPositions,
            int maxCount
        )
        {
            if (maxCount <= 0 || connectionGroupId < 0)
                return 0;

            var botGame = Singleton<IBotGame>.Instance;
            if (botGame == null)
                return 0;

            var coversData = botGame.BotsController?.CoversData;
            if (coversData == null)
                return 0;

            var points = coversData.Points;
            if (points == null || points.Count == 0)
                return 0;

            float radiusSqr = radius * radius;
            int count = 0;

            // First pass: Wall covers in the matching connection group
            for (int i = 0; i < points.Count && count < maxCount; i++)
            {
                var gp = points[i];
                if (gp == null || gp.ConnectionGroup != connectionGroupId)
                    continue;

                if (gp.CoverType != CoverType.Wall)
                    continue;

                float dx = gp._position.x - objX;
                float dy = gp._position.y - objY;
                float dz = gp._position.z - objZ;
                if (dx * dx + dy * dy + dz * dz > radiusSqr)
                    continue;

                outPositions[count * 3] = gp._position.x;
                outPositions[count * 3 + 1] = gp._position.y;
                outPositions[count * 3 + 2] = gp._position.z;
                count++;
            }

            // Second pass: any cover type in the matching connection group
            if (count < maxCount)
            {
                for (int i = 0; i < points.Count && count < maxCount; i++)
                {
                    var gp = points[i];
                    if (gp == null || gp.ConnectionGroup != connectionGroupId)
                        continue;

                    if (gp.CoverType == CoverType.Wall)
                        continue; // already collected

                    float dx = gp._position.x - objX;
                    float dy = gp._position.y - objY;
                    float dz = gp._position.z - objZ;
                    if (dx * dx + dy * dy + dz * dz > radiusSqr)
                        continue;

                    outPositions[count * 3] = gp._position.x;
                    outPositions[count * 3 + 1] = gp._position.y;
                    outPositions[count * 3 + 2] = gp._position.z;
                    count++;
                }
            }

            LoggingController.LogDebug(
                "[BsgCoverPointCollector] ConnectionGroup "
                    + connectionGroupId
                    + ": collected "
                    + count
                    + " cover positions near ("
                    + objX.ToString("F0")
                    + ", "
                    + objZ.ToString("F0")
                    + ")"
            );
            return count;
        }

        /// <summary>
        /// Collects cover positions using the bot's per-bot <see cref="BotCoversData"/>,
        /// which respects bot ID ownership and spotted status.
        /// Falls back to the global voxel grid if per-bot data yields insufficient results.
        /// </summary>
        /// <param name="botOwner">Bot whose cover data to query.</param>
        /// <param name="objX">Objective X position.</param>
        /// <param name="objY">Objective Y position.</param>
        /// <param name="objZ">Objective Z position.</param>
        /// <param name="radius">Search radius around the objective.</param>
        /// <param name="outPositions">Output buffer for x,y,z position triples.</param>
        /// <param name="maxCount">Maximum number of positions to collect.</param>
        /// <returns>Number of positions written to outPositions.</returns>
        public static int CollectBotCoverPositions(
            BotOwner botOwner,
            float objX,
            float objY,
            float objZ,
            float radius,
            float[] outPositions,
            int maxCount
        )
        {
            if (maxCount <= 0)
                return 0;

            if (botOwner?.Covers == null)
                return CollectCoverPositions(objX, objY, objZ, radius, outPositions, maxCount);

            // Use per-bot BotCoversData.GetClosePoints for ownership-aware cover queries
            System.Collections.Generic.List<CustomNavigationPoint> closePoints;
            try
            {
                var objective = new Vector3(objX, objY, objZ);
                closePoints = botOwner.Covers.GetClosePoints(objective, radius);
            }
            catch
            {
                return CollectCoverPositions(objX, objY, objZ, radius, outPositions, maxCount);
            }

            if (closePoints == null || closePoints.Count == 0)
                return CollectCoverPositions(objX, objY, objZ, radius, outPositions, maxCount);

            int count = 0;
            float radiusSqr = radius * radius;

            for (int i = 0; i < closePoints.Count && count < maxCount; i++)
            {
                var point = closePoints[i];
                if (point == null)
                    continue;

                var pos = point.Position;
                float dx = pos.x - objX;
                float dy = pos.y - objY;
                float dz = pos.z - objZ;
                if (dx * dx + dy * dy + dz * dz > radiusSqr)
                    continue;

                outPositions[count * 3] = pos.x;
                outPositions[count * 3 + 1] = pos.y;
                outPositions[count * 3 + 2] = pos.z;
                count++;
            }

            // If per-bot data didn't yield enough, fall back to global voxel grid
            if (count < maxCount)
            {
                LoggingController.LogDebug(
                    "[BsgCoverPointCollector] Per-bot cover found " + count + "/" + maxCount + ", falling back to global grid"
                );
                return CollectCoverPositions(objX, objY, objZ, radius, outPositions, maxCount);
            }

            LoggingController.LogDebug(
                "[BsgCoverPointCollector] Per-bot cover collected " + count + " positions near (" + objX + ", " + objZ + ")"
            );
            return count;
        }
    }
}
