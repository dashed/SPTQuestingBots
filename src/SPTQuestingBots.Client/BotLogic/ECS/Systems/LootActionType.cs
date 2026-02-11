namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Action decision for a specific loot item from the inventory planner.
/// </summary>
public enum LootActionType : byte
{
    /// <summary>Skip this item — not worth picking up.</summary>
    Skip = 0,

    /// <summary>Equip this item — it's a gear upgrade.</summary>
    Equip = 1,

    /// <summary>Pick up this item into inventory.</summary>
    Pickup = 2,

    /// <summary>Swap current gear for this item — drop current + equip.</summary>
    Swap = 3,
}
