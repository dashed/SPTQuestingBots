using System.Collections.Generic;
using EFT.Interactive;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.ZoneMovement.Integration;

/// <summary>
/// Scans the Unity scene for points of interest and converts them to
/// <see cref="PointOfInterest"/> instances for the zone movement grid.
/// <para>
/// This is a thin scene adapter — it bridges Unity's <c>FindObjectsOfType</c>
/// to the pure-logic <see cref="PointOfInterest"/> model. Each discovered
/// object is validated against the NavMesh to ensure bots can actually reach it.
/// </para>
/// </summary>
public static class PoiScanner
{
    /// <summary>
    /// Maximum distance from a scene object's position to search for a valid NavMesh point.
    /// </summary>
    private const float NavMeshSearchDistance = 2f;

    /// <summary>
    /// Scans the current scene for lootable containers, quest triggers, and
    /// exfiltration points, returning them as <see cref="PointOfInterest"/> instances.
    /// </summary>
    /// <returns>
    /// A list of NavMesh-validated POIs. Objects whose positions cannot be
    /// snapped to the NavMesh are silently excluded.
    /// </returns>
    /// <remarks>
    /// Spawn points are not scanned here — they are added separately from
    /// <c>SpawnPointParams</c> data, which is already available as <c>Vector3[]</c>.
    /// </remarks>
    public static List<PointOfInterest> ScanScene()
    {
        var pois = new List<PointOfInterest>();

        int containerCount = ScanContainers(pois);
        int questCount = ScanQuestTriggers(pois);
        int exfilCount = ScanExfiltrationPoints(pois);

        LoggingController.LogInfo(
            $"[ZoneMovement] POI scan: {containerCount} containers, {questCount} quest triggers, {exfilCount} exfils ({pois.Count} total)"
        );

        return pois;
    }

    private static int ScanContainers(List<PointOfInterest> pois)
    {
        int count = 0;
        foreach (var container in Object.FindObjectsOfType<LootableContainer>())
        {
            if (TryCreatePoi(container.transform.position, PoiCategory.Container, out var poi))
            {
                pois.Add(poi);
                count++;
            }
        }
        return count;
    }

    private static int ScanQuestTriggers(List<PointOfInterest> pois)
    {
        int count = 0;
        foreach (var trigger in Object.FindObjectsOfType<TriggerWithId>())
        {
            var collider = trigger.gameObject.GetComponent<Collider>();
            Vector3 position = collider != null ? collider.bounds.center : trigger.transform.position;

            if (TryCreatePoi(position, PoiCategory.Quest, out var poi))
            {
                pois.Add(poi);
                count++;
            }
        }
        return count;
    }

    private static int ScanExfiltrationPoints(List<PointOfInterest> pois)
    {
        int count = 0;
        foreach (var exfil in Object.FindObjectsOfType<ExfiltrationPoint>())
        {
            if (TryCreatePoi(exfil.transform.position, PoiCategory.Exfil, out var poi))
            {
                pois.Add(poi);
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Attempts to create a <see cref="PointOfInterest"/> at the given position
    /// after validating it against the NavMesh.
    /// </summary>
    private static bool TryCreatePoi(Vector3 position, PoiCategory category, out PointOfInterest poi)
    {
        poi = null;

        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, NavMeshSearchDistance, NavMesh.AllAreas))
        {
            return false;
        }

        poi = new PointOfInterest(hit.position, category);
        return true;
    }
}
