namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Pre-clustered loot zone data from BSG's <see cref="AILootPointsCluster"/>.
    /// Pure C# struct for zero-allocation reads.
    /// </summary>
    public struct LootClusterData
    {
        /// <summary>Cluster center X coordinate.</summary>
        public float CenterX;

        /// <summary>Cluster center Y coordinate.</summary>
        public float CenterY;

        /// <summary>Cluster center Z coordinate.</summary>
        public float CenterZ;

        /// <summary>Cluster radius.</summary>
        public float Radius;

        /// <summary>Number of loot points in this cluster.</summary>
        public int LootPointCount;

        /// <summary>Runtime-evaluated value score for the cluster.</summary>
        public float ValueScore;

        /// <summary>Cluster ID.</summary>
        public int ClusterId;

        /// <summary>Whether this cluster has been looted.</summary>
        public bool IsLooted;
    }
}
