using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

// ── GearComparer ────────────────────────────────────────────────

[TestFixture]
public class GearComparerTests
{
    // ── IsArmorUpgrade ──────────────────────────────────────────

    [Test]
    public void IsArmorUpgrade_EqualClass_ReturnsFalse()
    {
        Assert.IsFalse(GearComparer.IsArmorUpgrade(4, 4));
    }

    [Test]
    public void IsArmorUpgrade_LowerCandidate_ReturnsFalse()
    {
        Assert.IsFalse(GearComparer.IsArmorUpgrade(4, 3));
    }

    [Test]
    public void IsArmorUpgrade_HigherCandidate_ReturnsTrue()
    {
        Assert.IsTrue(GearComparer.IsArmorUpgrade(3, 5));
    }

    // ── IsWeaponUpgrade ─────────────────────────────────────────

    [Test]
    public void IsWeaponUpgrade_EqualValue_ReturnsFalse()
    {
        Assert.IsFalse(GearComparer.IsWeaponUpgrade(5000, 5000));
    }

    [Test]
    public void IsWeaponUpgrade_LowerCandidate_ReturnsFalse()
    {
        Assert.IsFalse(GearComparer.IsWeaponUpgrade(5000, 3000));
    }

    [Test]
    public void IsWeaponUpgrade_HigherCandidate_ReturnsTrue()
    {
        Assert.IsTrue(GearComparer.IsWeaponUpgrade(3000, 8000));
    }

    // ── IsContainerUpgrade ──────────────────────────────────────

    [Test]
    public void IsContainerUpgrade_EqualSize_ReturnsFalse()
    {
        Assert.IsFalse(GearComparer.IsContainerUpgrade(16, 16));
    }

    [Test]
    public void IsContainerUpgrade_SmallerCandidate_ReturnsFalse()
    {
        Assert.IsFalse(GearComparer.IsContainerUpgrade(16, 12));
    }

    [Test]
    public void IsContainerUpgrade_LargerCandidate_ReturnsTrue()
    {
        Assert.IsTrue(GearComparer.IsContainerUpgrade(12, 25));
    }

    // ── IsRigUpgrade ────────────────────────────────────────────

    [Test]
    public void IsRigUpgrade_NonArmoredSizeUpgrade_ReturnsTrue()
    {
        Assert.IsTrue(
            GearComparer.IsRigUpgrade(
                myRigGridSize: 8,
                myArmorClass: 0,
                myRigIsArmored: false,
                candidateRigGridSize: 12,
                candidateArmorClass: 0,
                candidateIsArmored: false
            )
        );
    }

    [Test]
    public void IsRigUpgrade_NonArmoredSameSize_ReturnsFalse()
    {
        Assert.IsFalse(
            GearComparer.IsRigUpgrade(
                myRigGridSize: 12,
                myArmorClass: 0,
                myRigIsArmored: false,
                candidateRigGridSize: 12,
                candidateArmorClass: 0,
                candidateIsArmored: false
            )
        );
    }

    [Test]
    public void IsRigUpgrade_ArmoredVsNonArmored_AcceptableClass_ReturnsTrue()
    {
        // Candidate is armored rig with class 4, current is non-armored with body armor class 3
        Assert.IsTrue(
            GearComparer.IsRigUpgrade(
                myRigGridSize: 8,
                myArmorClass: 3,
                myRigIsArmored: false,
                candidateRigGridSize: 10,
                candidateArmorClass: 4,
                candidateIsArmored: true
            )
        );
    }

    [Test]
    public void IsRigUpgrade_ArmoredVsNonArmored_LowerClass_ReturnsFalse()
    {
        // Candidate armored rig has lower class than current body armor
        Assert.IsFalse(
            GearComparer.IsRigUpgrade(
                myRigGridSize: 8,
                myArmorClass: 5,
                myRigIsArmored: false,
                candidateRigGridSize: 10,
                candidateArmorClass: 3,
                candidateIsArmored: true
            )
        );
    }

    [Test]
    public void IsRigUpgrade_BothArmored_HigherClass_ReturnsTrue()
    {
        Assert.IsTrue(
            GearComparer.IsRigUpgrade(
                myRigGridSize: 10,
                myArmorClass: 3,
                myRigIsArmored: true,
                candidateRigGridSize: 10,
                candidateArmorClass: 5,
                candidateIsArmored: true
            )
        );
    }

    [Test]
    public void IsRigUpgrade_BothArmored_SameClassLargerSize_ReturnsTrue()
    {
        Assert.IsTrue(
            GearComparer.IsRigUpgrade(
                myRigGridSize: 8,
                myArmorClass: 4,
                myRigIsArmored: true,
                candidateRigGridSize: 12,
                candidateArmorClass: 4,
                candidateIsArmored: true
            )
        );
    }

    [Test]
    public void IsRigUpgrade_BothArmored_SameClassSameSize_ReturnsFalse()
    {
        Assert.IsFalse(
            GearComparer.IsRigUpgrade(
                myRigGridSize: 10,
                myArmorClass: 4,
                myRigIsArmored: true,
                candidateRigGridSize: 10,
                candidateArmorClass: 4,
                candidateIsArmored: true
            )
        );
    }

    [Test]
    public void IsRigUpgrade_BothArmored_LowerClass_ReturnsFalse()
    {
        Assert.IsFalse(
            GearComparer.IsRigUpgrade(
                myRigGridSize: 8,
                myArmorClass: 5,
                myRigIsArmored: true,
                candidateRigGridSize: 12,
                candidateArmorClass: 3,
                candidateIsArmored: true
            )
        );
    }
}

// ── LootInventoryPlanner ────────────────────────────────────────

[TestFixture]
public class LootInventoryPlannerTests
{
    private static GearStats DefaultGear(
        int armorClass = 3,
        int weaponValue = 5000,
        int backpackGridSize = 16,
        int rigGridSize = 8,
        bool rigIsArmored = false,
        float inventorySpaceFree = 10f
    )
    {
        return new GearStats(armorClass, weaponValue, backpackGridSize, rigGridSize, rigIsArmored, inventorySpaceFree);
    }

    private static LootItemInfo ArmorItem(int armorClass, int value = 10000)
    {
        return new LootItemInfo(
            value: value,
            gridSize: 4,
            isArmor: true,
            armorClass: armorClass,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
    }

    private static LootItemInfo WeaponItem(int weaponValue, int value = 15000)
    {
        return new LootItemInfo(
            value: value,
            gridSize: 8,
            isArmor: false,
            armorClass: 0,
            isWeapon: true,
            weaponValue: weaponValue,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
    }

    private static LootItemInfo BackpackItem(int gridSize, int value = 12000)
    {
        return new LootItemInfo(
            value: value,
            gridSize: 6,
            isArmor: false,
            armorClass: 0,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: true,
            backpackGridSize: gridSize,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
    }

    private static LootItemInfo RigItem(int rigGridSize, int armorClass = 0, bool isArmored = false, int value = 8000)
    {
        return new LootItemInfo(
            value: value,
            gridSize: 4,
            isArmor: false,
            armorClass: armorClass,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: true,
            rigGridSize: rigGridSize,
            rigIsArmored: isArmored
        );
    }

    private static LootItemInfo GenericItem(int value, int gridSize)
    {
        return new LootItemInfo(
            value: value,
            gridSize: gridSize,
            isArmor: false,
            armorClass: 0,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
    }

    // ── Armor ───────────────────────────────────────────────────

    [Test]
    public void PlanAction_ArmorUpgrade_ReturnsSwap()
    {
        var gear = DefaultGear(armorClass: 3);
        var item = ArmorItem(armorClass: 5);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Swap, result);
    }

    [Test]
    public void PlanAction_ArmorDowngrade_ChecksValueThreshold()
    {
        var gear = DefaultGear(armorClass: 5);
        // Armor class 3 is not an upgrade; falls through to value check
        var item = ArmorItem(armorClass: 3, value: 10000);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        // Not an armor upgrade, but value 10000 >= 5000 and gridSize 4 <= 10 free space
        Assert.AreEqual(LootActionType.Pickup, result);
    }

    [Test]
    public void PlanAction_ArmorUpgrade_SwapDisabled_ReturnsSkip()
    {
        var gear = DefaultGear(armorClass: 3);
        var item = ArmorItem(armorClass: 5);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: false);

        Assert.AreEqual(LootActionType.Skip, result);
    }

    // ── Weapon ──────────────────────────────────────────────────

    [Test]
    public void PlanAction_WeaponUpgrade_ReturnsSwap()
    {
        var gear = DefaultGear(weaponValue: 5000);
        var item = WeaponItem(weaponValue: 12000);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Swap, result);
    }

    [Test]
    public void PlanAction_WeaponBelowThreshold_ReturnsSkip()
    {
        var gear = DefaultGear(weaponValue: 10000);
        // Not an upgrade AND value below threshold
        var item = WeaponItem(weaponValue: 3000, value: 1000);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Skip, result);
    }

    // ── Backpack ────────────────────────────────────────────────

    [Test]
    public void PlanAction_BackpackUpgrade_ReturnsSwap()
    {
        var gear = DefaultGear(backpackGridSize: 16);
        var item = BackpackItem(gridSize: 25);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Swap, result);
    }

    [Test]
    public void PlanAction_BackpackDowngrade_FallsThrough()
    {
        var gear = DefaultGear(backpackGridSize: 25);
        var item = BackpackItem(gridSize: 16, value: 10000);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        // Not a backpack upgrade; value 10000 >= 5000, gridSize 6 <= 10 free
        Assert.AreEqual(LootActionType.Pickup, result);
    }

    // ── Rig ─────────────────────────────────────────────────────

    [Test]
    public void PlanAction_RigUpgrade_ReturnsSwap()
    {
        var gear = DefaultGear(rigGridSize: 8, rigIsArmored: false);
        var item = RigItem(rigGridSize: 14);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Swap, result);
    }

    // ── Non-gear items ──────────────────────────────────────────

    [Test]
    public void PlanAction_GenericAboveMinValueWithSpace_ReturnsPickup()
    {
        var gear = DefaultGear(inventorySpaceFree: 10f);
        var item = GenericItem(value: 8000, gridSize: 4);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Pickup, result);
    }

    [Test]
    public void PlanAction_GenericBelowMinValue_ReturnsSkip()
    {
        var gear = DefaultGear(inventorySpaceFree: 10f);
        var item = GenericItem(value: 2000, gridSize: 2);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Skip, result);
    }

    [Test]
    public void PlanAction_GenericAboveValueButNoSpace_ReturnsSkip()
    {
        var gear = DefaultGear(inventorySpaceFree: 1f);
        var item = GenericItem(value: 20000, gridSize: 4);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Skip, result);
    }

    [Test]
    public void PlanAction_GenericExactlyMinValue_ReturnsPickup()
    {
        var gear = DefaultGear(inventorySpaceFree: 10f);
        var item = GenericItem(value: 5000, gridSize: 4);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Pickup, result);
    }

    [Test]
    public void PlanAction_GenericExactlyFitsSpace_ReturnsPickup()
    {
        var gear = DefaultGear(inventorySpaceFree: 4f);
        var item = GenericItem(value: 8000, gridSize: 4);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Pickup, result);
    }

    // ── Edge cases ──────────────────────────────────────────────

    [Test]
    public void PlanAction_ZeroArmorClassBoth_NoUpgrade()
    {
        var gear = DefaultGear(armorClass: 0);
        var item = ArmorItem(armorClass: 0, value: 10000);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        // Equal armor class = no upgrade; falls through to value check → Pickup
        Assert.AreEqual(LootActionType.Pickup, result);
    }

    [Test]
    public void PlanAction_GearUpgrade_ZeroInventorySpace_StillSwaps()
    {
        // Gear upgrades don't need inventory space (it's a swap, not pickup)
        var gear = DefaultGear(armorClass: 2, inventorySpaceFree: 0f);
        var item = ArmorItem(armorClass: 5);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Swap, result);
    }

    [Test]
    public void PlanAction_WeaponUpgrade_SwapDisabled_ReturnsSkip()
    {
        var gear = DefaultGear(weaponValue: 3000);
        var item = WeaponItem(weaponValue: 15000);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: false);

        Assert.AreEqual(LootActionType.Skip, result);
    }

    [Test]
    public void PlanAction_MultipleGearFlags_ArmorCheckedFirst()
    {
        // Item that is both armor (class 5) and a rig — armor path should trigger first
        var gear = DefaultGear(armorClass: 3, rigGridSize: 8);
        var item = new LootItemInfo(
            value: 20000,
            gridSize: 6,
            isArmor: true,
            armorClass: 5,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: true,
            rigGridSize: 14,
            rigIsArmored: false
        );

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Swap, result);
    }

    [Test]
    public void PlanAction_ArmoredRigUpgrade_ReturnsSwap()
    {
        // Current: non-armored rig size 8, body armor class 3
        // Candidate: armored rig size 10, class 4
        var gear = DefaultGear(rigGridSize: 8, rigIsArmored: false, armorClass: 3);
        var item = RigItem(rigGridSize: 10, armorClass: 4, isArmored: true);

        var result = LootInventoryPlanner.PlanAction(item, gear, minItemValue: 5000, gearSwapEnabled: true);

        Assert.AreEqual(LootActionType.Swap, result);
    }

    // ── GearStats / LootItemInfo struct construction ────────────

    [Test]
    public void GearStats_StoresAllFields()
    {
        var gear = new GearStats(4, 8000, 25, 12, true, 5.5f);

        Assert.AreEqual(4, gear.ArmorClass);
        Assert.AreEqual(8000, gear.WeaponValue);
        Assert.AreEqual(25, gear.BackpackGridSize);
        Assert.AreEqual(12, gear.RigGridSize);
        Assert.IsTrue(gear.RigIsArmored);
        Assert.AreEqual(5.5f, gear.InventorySpaceFree);
    }

    [Test]
    public void LootItemInfo_StoresAllFields()
    {
        var item = new LootItemInfo(10000, 4, true, 5, false, 0, true, 25, false, 0, false);

        Assert.AreEqual(10000, item.Value);
        Assert.AreEqual(4, item.GridSize);
        Assert.IsTrue(item.IsArmor);
        Assert.AreEqual(5, item.ArmorClass);
        Assert.IsFalse(item.IsWeapon);
        Assert.AreEqual(0, item.WeaponValue);
        Assert.IsTrue(item.IsBackpack);
        Assert.AreEqual(25, item.BackpackGridSize);
        Assert.IsFalse(item.IsRig);
        Assert.AreEqual(0, item.RigGridSize);
        Assert.IsFalse(item.RigIsArmored);
    }
}
