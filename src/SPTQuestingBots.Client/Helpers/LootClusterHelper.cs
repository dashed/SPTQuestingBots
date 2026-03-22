using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Reads BSG's pre-computed <see cref="AILootPointsCluster"/> data from
    /// <c>BotsController.CoversData.Patrols.LootPointClusters</c>.
    /// These clusters group nearby loot containers with value scores and radii,
    /// providing better loot zone awareness than manual Physics.OverlapSphere scanning.
    /// <para>
    /// Pure search/scoring logic is in <see cref="LootClusterScorer"/>.
    /// </para>
    /// </summary>
    public static class LootClusterHelper
    {
        /// <summary>
        /// Read all loot clusters from the current map.
        /// Returns an empty array if the data is unavailable.
        /// </summary>
        public static LootClusterData[] GetAllClusters()
        {
            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                if (botGame == null)
                    return System.Array.Empty<LootClusterData>();

                var patrols = botGame.BotsController?.CoversData?.Patrols;
                if (patrols == null)
                    return System.Array.Empty<LootClusterData>();

                var clusters = patrols.LootPointClusters;
                if (clusters == null || clusters.Count == 0)
                    return System.Array.Empty<LootClusterData>();

                var results = new LootClusterData[clusters.Count];
                int validCount = 0;

                for (int i = 0; i < clusters.Count; i++)
                {
                    var cluster = clusters[i];
                    if (cluster == null)
                        continue;

                    results[validCount] = new LootClusterData
                    {
                        CenterX = cluster._centerPosition.x,
                        CenterY = cluster._centerPosition.y,
                        CenterZ = cluster._centerPosition.z,
                        Radius = cluster._radius,
                        LootPointCount = cluster.LootPoints?.Count ?? 0,
                        ValueScore = cluster.EvaluatedCostRuntime_1,
                        ClusterId = cluster._clusterId,
                        IsLooted = cluster.Looted,
                    };
                    validCount++;
                }

                if (validCount < results.Length)
                {
                    var trimmed = new LootClusterData[validCount];
                    System.Array.Copy(results, trimmed, validCount);
                    results = trimmed;
                }

                LoggingController.LogInfo("[LootClusterHelper] Loaded " + validCount + " loot clusters");
                return results;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogWarning("[LootClusterHelper] Failed to read loot clusters: " + ex.Message);
                return System.Array.Empty<LootClusterData>();
            }
        }
    }
}
