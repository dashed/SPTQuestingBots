using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Core;

/// <summary>
/// An immutable point of interest in the game world. POIs are discovered by
/// scanning the Unity scene (containers, quest triggers, exfils, etc.) and
/// assigned to <see cref="GridCell"/> instances based on their position.
/// </summary>
/// <remarks>
/// Each POI carries a <see cref="Weight"/> that contributes to its cell's
/// <see cref="GridCell.PoiDensity"/>, which in turn influences destination
/// scoring via <see cref="Selection.CellScorer"/>.
/// </remarks>
public sealed class PointOfInterest
{
    /// <summary>World-space position of this point of interest.</summary>
    public Vector3 Position { get; }

    /// <summary>Classification of this POI (container, quest, exfil, etc.).</summary>
    public PoiCategory Category { get; }

    /// <summary>
    /// Importance weight used for destination scoring.
    /// Higher values make the containing cell more attractive to bots.
    /// </summary>
    public float Weight { get; }

    /// <summary>
    /// Creates a POI with an explicit weight.
    /// </summary>
    /// <param name="position">World-space position.</param>
    /// <param name="category">POI classification.</param>
    /// <param name="weight">Custom importance weight.</param>
    public PointOfInterest(Vector3 position, PoiCategory category, float weight)
    {
        Position = position;
        Category = category;
        Weight = weight;
    }

    /// <summary>
    /// Creates a POI using the default weight for its category.
    /// </summary>
    /// <param name="position">World-space position.</param>
    /// <param name="category">POI classification.</param>
    public PointOfInterest(Vector3 position, PoiCategory category)
        : this(position, category, DefaultWeight(category)) { }

    /// <summary>
    /// Returns the default weight for a given <see cref="PoiCategory"/>.
    /// Quest triggers are weighted highest (1.2) to encourage bots to visit
    /// quest-relevant areas; synthetic points are weighted lowest (0.2).
    /// </summary>
    public static float DefaultWeight(PoiCategory category)
    {
        return category switch
        {
            PoiCategory.Container => 1.0f,
            PoiCategory.LooseLoot => 0.8f,
            PoiCategory.Quest => 1.2f,
            PoiCategory.Exfil => 0.5f,
            PoiCategory.SpawnPoint => 0.3f,
            PoiCategory.Synthetic => 0.2f,
            _ => 0.5f,
        };
    }
}
