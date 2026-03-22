namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Pure C# scoring and search logic for loot clusters.
    /// No Unity or EFT dependencies — fully testable in net9.0.
    /// </summary>
    public static class LootClusterScorer
    {
        /// <summary>
        /// Find the nearest un-looted loot cluster to the given position within a maximum distance.
        /// </summary>
        /// <param name="posX">Query position X.</param>
        /// <param name="posY">Query position Y.</param>
        /// <param name="posZ">Query position Z.</param>
        /// <param name="maxDistance">Maximum search distance.</param>
        /// <param name="clusters">Pre-loaded cluster array from <see cref="LootClusterHelper.GetAllClusters"/>.</param>
        /// <param name="nearest">The nearest cluster data (only valid if method returns true).</param>
        /// <returns>True if a cluster was found within range.</returns>
        public static bool TryFindNearestCluster(
            float posX,
            float posY,
            float posZ,
            float maxDistance,
            LootClusterData[] clusters,
            out LootClusterData nearest
        )
        {
            nearest = default;

            if (clusters == null || clusters.Length == 0)
                return false;

            float maxDistSqr = maxDistance * maxDistance;
            float bestDistSqr = float.MaxValue;
            bool found = false;

            for (int i = 0; i < clusters.Length; i++)
            {
                var cluster = clusters[i];
                if (cluster.IsLooted)
                    continue;

                float dx = cluster.CenterX - posX;
                float dy = cluster.CenterY - posY;
                float dz = cluster.CenterZ - posZ;
                float distSqr = dx * dx + dy * dy + dz * dz;

                if (distSqr < maxDistSqr && distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    nearest = cluster;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Compute a loot density score at a position based on nearby clusters.
        /// Higher scores indicate areas with more valuable, concentrated loot.
        /// </summary>
        /// <param name="posX">Query position X.</param>
        /// <param name="posZ">Query position Z.</param>
        /// <param name="clusters">Pre-loaded cluster array.</param>
        /// <param name="influenceRadius">Radius within which clusters contribute to the score.</param>
        /// <returns>Normalized density score (0.0-1.0).</returns>
        public static float ComputeLootDensity(float posX, float posZ, LootClusterData[] clusters, float influenceRadius)
        {
            if (clusters == null || clusters.Length == 0)
                return 0f;

            float influenceRadiusSqr = influenceRadius * influenceRadius;
            float totalScore = 0f;

            for (int i = 0; i < clusters.Length; i++)
            {
                var cluster = clusters[i];
                if (cluster.IsLooted)
                    continue;

                float dx = cluster.CenterX - posX;
                float dz = cluster.CenterZ - posZ;
                float distSqr = dx * dx + dz * dz;

                if (distSqr < influenceRadiusSqr)
                {
                    // Linear falloff: full score at center, zero at influence radius
                    float dist = (float)System.Math.Sqrt(distSqr);
                    float falloff = 1f - (dist / influenceRadius);
                    totalScore += cluster.ValueScore * falloff * cluster.LootPointCount;
                }
            }

            // Normalize: cap at a reasonable maximum
            return System.Math.Min(totalScore / 100000f, 1f);
        }
    }
}
