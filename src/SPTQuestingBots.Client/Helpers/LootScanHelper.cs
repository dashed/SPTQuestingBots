using System;
using System.Buffers;
using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Physics.OverlapSphereNonAlloc wrapper for scanning nearby lootable objects.
    /// Matches LootingBots LootFinder pattern: ArrayPool collider buffer, layer mask,
    /// distance sorting, per-type distance filtering.
    /// </summary>
    public static class LootScanHelper
    {
        private static readonly ArrayPool<Collider> ColliderPool = ArrayPool<Collider>.Shared;

        /// <summary>
        /// LayerMask combining Interactive, Loot, and Deadbody layers.
        /// Matches LootingBots LootUtils.LootMask.
        /// </summary>
        private static readonly LayerMask LootMask = LayerMask.GetMask("Interactive", "Loot", "Deadbody");

        /// <summary>
        /// Scan for loot around a position. Returns number of results written.
        /// Results are sorted by distance (closest first).
        /// </summary>
        /// <param name="botPosition">Bot's current world position.</param>
        /// <param name="containerDist">Max detection distance for containers.</param>
        /// <param name="itemDist">Max detection distance for loose items.</param>
        /// <param name="corpseDist">Max detection distance for corpses.</param>
        /// <param name="containersEnabled">Whether container scanning is enabled.</param>
        /// <param name="itemsEnabled">Whether loose item scanning is enabled.</param>
        /// <param name="corpsesEnabled">Whether corpse scanning is enabled.</param>
        /// <param name="ignoreIds">Set of instance IDs to skip. Can be null.</param>
        /// <param name="results">Output buffer for scan results.</param>
        /// <param name="maxResults">Maximum number of results to write.</param>
        /// <returns>Number of results written to the buffer.</returns>
        public static int ScanForLoot(
            Vector3 botPosition,
            float containerDist,
            float itemDist,
            float corpseDist,
            bool containersEnabled,
            bool itemsEnabled,
            bool corpsesEnabled,
            HashSet<int> ignoreIds,
            LootScanResult[] results,
            int maxResults
        )
        {
            float detectionRadius = Mathf.Max(containerDist, Mathf.Max(itemDist, corpseDist));
            Collider[] colliders = ColliderPool.Rent(3000);
            int resultCount = 0;

            try
            {
                int hits = Physics.OverlapSphereNonAlloc(botPosition, detectionRadius, colliders, LootMask, QueryTriggerInteraction.Ignore);

                if (hits == 0)
                {
                    LoggingController.LogDebug("[LootScanHelper] Scan at " + botPosition + ": 0 colliders hit");
                    return 0;
                }

                // Sort by distance (closest first) — matches LootingBots pattern
                Array.Sort(colliders, 0, hits, new ColliderDistanceComparer(botPosition));

                float containerDistSqr = containerDist * containerDist;
                float itemDistSqr = itemDist * itemDist;
                float corpseDistSqr = corpseDist * corpseDist;

                for (int i = 0; i < hits && resultCount < maxResults; i++)
                {
                    var collider = colliders[i];
                    if (collider == null)
                        continue;

                    // Try container — uses GetComponentInParent matching LootingBots
                    if (containersEnabled)
                    {
                        var container = collider.gameObject.GetComponentInParent<LootableContainer>();
                        if (container != null)
                        {
                            if (container.DoorState == EDoorState.Locked)
                                continue;
                            if (!container.isActiveAndEnabled)
                                continue;

                            int id = container.GetInstanceID();
                            if (ignoreIds != null && ignoreIds.Contains(id))
                                continue;

                            Vector3 pos = container.transform.position;
                            float distSqr = (botPosition - pos).sqrMagnitude;
                            if (distSqr > containerDistSqr)
                                continue;

                            results[resultCount++] = new LootScanResult
                            {
                                Id = id,
                                X = pos.x,
                                Y = pos.y,
                                Z = pos.z,
                                Type = LootTargetType.Container,
                                Value = 0,
                                DistanceSqr = distSqr,
                            };
                            continue;
                        }
                    }

                    // Try loose item — filter out Corpse subtype (handled via Player below)
                    if (itemsEnabled)
                    {
                        var lootItem = collider.gameObject.GetComponentInParent<LootItem>();
                        if (lootItem != null && !(lootItem is Corpse))
                        {
                            var rootItem = lootItem.ItemOwner?.RootItem;
                            if (rootItem != null && !rootItem.QuestItem)
                            {
                                int id = lootItem.GetInstanceID();
                                if (ignoreIds == null || !ignoreIds.Contains(id))
                                {
                                    Vector3 pos = lootItem.transform.position;
                                    float distSqr = (botPosition - pos).sqrMagnitude;
                                    if (distSqr <= itemDistSqr)
                                    {
                                        results[resultCount++] = new LootScanResult
                                        {
                                            Id = id,
                                            X = pos.x,
                                            Y = pos.y,
                                            Z = pos.z,
                                            Type = LootTargetType.LooseItem,
                                            Value = 0,
                                            DistanceSqr = distSqr,
                                        };
                                        continue;
                                    }
                                }
                            }
                        }
                    }

                    // Try corpse — Player component on Deadbody layer
                    // LootingBots checks: corpse != null && corpse.GetPlayer != null
                    // GetPlayer != null distinguishes bot corpses from static "Dead scav" corpses
                    if (corpsesEnabled)
                    {
                        var player = collider.gameObject.GetComponentInParent<Player>();
                        if (player != null && player.GetPlayer != null)
                        {
                            int id = player.GetInstanceID();
                            if (ignoreIds != null && ignoreIds.Contains(id))
                                continue;

                            Vector3 pos = player.Transform.position;
                            float distSqr = (botPosition - pos).sqrMagnitude;
                            if (distSqr > corpseDistSqr)
                                continue;

                            results[resultCount++] = new LootScanResult
                            {
                                Id = id,
                                X = pos.x,
                                Y = pos.y,
                                Z = pos.z,
                                Type = LootTargetType.Corpse,
                                Value = 0,
                                DistanceSqr = distSqr,
                            };
                        }
                    }
                }
            }
            finally
            {
                ColliderPool.Return(colliders, clearArray: true);
            }

            LoggingController.LogDebug(
                "[LootScanHelper] Scan complete: found "
                    + resultCount
                    + " loot targets within "
                    + detectionRadius.ToString("F0")
                    + "m"
                    + " (containers="
                    + (containersEnabled ? "on" : "off")
                    + ", items="
                    + (itemsEnabled ? "on" : "off")
                    + ", corpses="
                    + (corpsesEnabled ? "on" : "off")
                    + ")"
            );

            return resultCount;
        }
    }

    /// <summary>
    /// Sorts colliders by squared distance from a reference position.
    /// Uses bounds.center matching LootingBots ColliderDistanceComparer.
    /// </summary>
    internal struct ColliderDistanceComparer : IComparer<Collider>
    {
        private readonly Vector3 _origin;

        public ColliderDistanceComparer(Vector3 origin) => _origin = origin;

        public int Compare(Collider a, Collider b)
        {
            if (a == null && b == null)
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;
            float da = (a.bounds.center - _origin).sqrMagnitude;
            float db = (b.bounds.center - _origin).sqrMagnitude;
            return da.CompareTo(db);
        }
    }
}
