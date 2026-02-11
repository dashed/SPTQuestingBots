using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Snapshot of a bot's current gear stats for loot decision-making.
/// </summary>
public readonly struct GearStats
{
    public readonly int ArmorClass;
    public readonly int WeaponValue;
    public readonly int BackpackGridSize;
    public readonly int RigGridSize;
    public readonly bool RigIsArmored;
    public readonly float InventorySpaceFree;

    public GearStats(int armorClass, int weaponValue, int backpackGridSize, int rigGridSize, bool rigIsArmored, float inventorySpaceFree)
    {
        ArmorClass = armorClass;
        WeaponValue = weaponValue;
        BackpackGridSize = backpackGridSize;
        RigGridSize = rigGridSize;
        RigIsArmored = rigIsArmored;
        InventorySpaceFree = inventorySpaceFree;
    }
}

/// <summary>
/// Describes a loot item's properties for the inventory planner.
/// </summary>
public readonly struct LootItemInfo
{
    public readonly int Value;
    public readonly int GridSize;
    public readonly bool IsArmor;
    public readonly int ArmorClass;
    public readonly bool IsWeapon;
    public readonly int WeaponValue;
    public readonly bool IsBackpack;
    public readonly int BackpackGridSize;
    public readonly bool IsRig;
    public readonly int RigGridSize;
    public readonly bool RigIsArmored;

    public LootItemInfo(
        int value,
        int gridSize,
        bool isArmor,
        int armorClass,
        bool isWeapon,
        int weaponValue,
        bool isBackpack,
        int backpackGridSize,
        bool isRig,
        int rigGridSize,
        bool rigIsArmored
    )
    {
        Value = value;
        GridSize = gridSize;
        IsArmor = isArmor;
        ArmorClass = armorClass;
        IsWeapon = isWeapon;
        WeaponValue = weaponValue;
        IsBackpack = isBackpack;
        BackpackGridSize = backpackGridSize;
        IsRig = isRig;
        RigGridSize = rigGridSize;
        RigIsArmored = rigIsArmored;
    }
}

/// <summary>
/// Pure-logic decision about what to do with a loot item.
/// Checks gear upgrades first (swap), then value + space for pickup.
/// </summary>
public static class LootInventoryPlanner
{
    /// <summary>
    /// Decide what action to take for a given loot item based on current gear and config.
    /// </summary>
    /// <param name="item">The loot item being evaluated.</param>
    /// <param name="gear">The bot's current gear stats.</param>
    /// <param name="minItemValue">Minimum value threshold for non-gear items.</param>
    /// <param name="gearSwapEnabled">Whether gear swapping is allowed.</param>
    /// <returns>The recommended loot action.</returns>
    public static LootActionType PlanAction(in LootItemInfo item, in GearStats gear, int minItemValue, bool gearSwapEnabled)
    {
        // Gear upgrade checks (swap)
        if (item.IsArmor && GearComparer.IsArmorUpgrade(gear.ArmorClass, item.ArmorClass))
        {
            var action = gearSwapEnabled ? LootActionType.Swap : LootActionType.Skip;
            LoggingController.LogDebug(
                "[LootInventoryPlanner] Armor upgrade — current="
                    + gear.ArmorClass
                    + ", candidate="
                    + item.ArmorClass
                    + ", action="
                    + action
            );
            return action;
        }

        if (item.IsWeapon && GearComparer.IsWeaponUpgrade(gear.WeaponValue, item.WeaponValue))
        {
            var action = gearSwapEnabled ? LootActionType.Swap : LootActionType.Skip;
            LoggingController.LogDebug(
                "[LootInventoryPlanner] Weapon upgrade — current="
                    + gear.WeaponValue
                    + ", candidate="
                    + item.WeaponValue
                    + ", action="
                    + action
            );
            return action;
        }

        if (item.IsBackpack && GearComparer.IsContainerUpgrade(gear.BackpackGridSize, item.BackpackGridSize))
        {
            var action = gearSwapEnabled ? LootActionType.Swap : LootActionType.Skip;
            LoggingController.LogDebug(
                "[LootInventoryPlanner] Backpack upgrade — current="
                    + gear.BackpackGridSize
                    + " slots, candidate="
                    + item.BackpackGridSize
                    + " slots, action="
                    + action
            );
            return action;
        }

        if (
            item.IsRig
            && GearComparer.IsRigUpgrade(
                gear.RigGridSize,
                gear.ArmorClass,
                gear.RigIsArmored,
                item.RigGridSize,
                item.ArmorClass,
                item.RigIsArmored
            )
        )
        {
            var action = gearSwapEnabled ? LootActionType.Swap : LootActionType.Skip;
            LoggingController.LogDebug(
                "[LootInventoryPlanner] Rig upgrade — current="
                    + gear.RigGridSize
                    + " slots, candidate="
                    + item.RigGridSize
                    + " slots, action="
                    + action
            );
            return action;
        }

        // Value check for non-gear items
        if (item.Value < minItemValue)
        {
            LoggingController.LogDebug("[LootInventoryPlanner] Skip — value=" + item.Value + " below min=" + minItemValue);
            return LootActionType.Skip;
        }

        // Space check for pickup
        if (item.GridSize <= gear.InventorySpaceFree)
        {
            LoggingController.LogDebug(
                "[LootInventoryPlanner] Pickup — value="
                    + item.Value
                    + ", size="
                    + item.GridSize
                    + ", free="
                    + gear.InventorySpaceFree.ToString("F0")
            );
            return LootActionType.Pickup;
        }

        LoggingController.LogDebug(
            "[LootInventoryPlanner] Skip — no space: size=" + item.GridSize + ", free=" + gear.InventorySpaceFree.ToString("F0")
        );
        return LootActionType.Skip;
    }
}
