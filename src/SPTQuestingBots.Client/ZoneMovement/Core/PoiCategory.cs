namespace SPTQuestingBots.ZoneMovement.Core;

/// <summary>
/// Categories of points of interest discovered in the game world.
/// Each category carries a default weight that influences how strongly
/// it attracts bots during destination scoring.
/// </summary>
public enum PoiCategory
{
    /// <summary>Lootable container (e.g. weapon box, med bag).</summary>
    Container,

    /// <summary>Loose loot item on the ground.</summary>
    LooseLoot,

    /// <summary>Quest-related trigger or interaction point.</summary>
    Quest,

    /// <summary>Exfiltration point.</summary>
    Exfil,

    /// <summary>Bot or player spawn point marker.</summary>
    SpawnPoint,

    /// <summary>
    /// Synthetically generated point in an otherwise empty grid cell,
    /// created by sampling the NavMesh at the cell center.
    /// </summary>
    Synthetic,
}
