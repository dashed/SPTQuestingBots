using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Pure-logic gear quality comparison. Static class with AggressiveInlining.
/// Used by LootInventoryPlanner to determine if a loot item is a gear upgrade.
/// </summary>
public static class GearComparer
{
    /// <summary>
    /// Returns true if the candidate armor class is strictly higher.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsArmorUpgrade(int myArmorClass, int candidateArmorClass)
    {
        bool result = candidateArmorClass > myArmorClass;
        if (result)
        {
            LoggingController.LogDebug("[GearComparer] Armor upgrade: current=" + myArmorClass + ", candidate=" + candidateArmorClass);
        }
        return result;
    }

    /// <summary>
    /// Returns true if the candidate weapon value is strictly higher.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWeaponUpgrade(int myWeaponValue, int candidateWeaponValue)
    {
        bool result = candidateWeaponValue > myWeaponValue;
        if (result)
        {
            LoggingController.LogDebug("[GearComparer] Weapon upgrade: current=" + myWeaponValue + ", candidate=" + candidateWeaponValue);
        }
        return result;
    }

    /// <summary>
    /// Returns true if the candidate container (backpack) grid size is strictly larger.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsContainerUpgrade(int myGridSize, int candidateGridSize)
    {
        bool result = candidateGridSize > myGridSize;
        if (result)
        {
            LoggingController.LogDebug(
                "[GearComparer] Container upgrade: current=" + myGridSize + " slots, candidate=" + candidateGridSize + " slots"
            );
        }
        return result;
    }

    /// <summary>
    /// Tactical rig comparison with armored rig special cases.
    /// An armored rig can replace a non-armored rig if armor class is acceptable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRigUpgrade(
        int myRigGridSize,
        int myArmorClass,
        bool myRigIsArmored,
        int candidateRigGridSize,
        int candidateArmorClass,
        bool candidateIsArmored
    )
    {
        bool result;

        // Candidate is armored, current is not: check if armor class is acceptable
        if (candidateIsArmored && !myRigIsArmored)
        {
            result = candidateArmorClass >= myArmorClass && candidateRigGridSize >= myRigGridSize;
        }
        // Both armored: prefer higher armor class, or same class with larger size
        else if (candidateIsArmored && myRigIsArmored)
        {
            result = (candidateArmorClass > myArmorClass) || (candidateArmorClass == myArmorClass && candidateRigGridSize > myRigGridSize);
        }
        // Non-armored: just compare size
        else
        {
            result = candidateRigGridSize > myRigGridSize;
        }

        if (result)
        {
            LoggingController.LogDebug(
                "[GearComparer] Rig upgrade: current="
                    + myRigGridSize
                    + " slots (armored="
                    + myRigIsArmored
                    + ")"
                    + ", candidate="
                    + candidateRigGridSize
                    + " slots (armored="
                    + candidateIsArmored
                    + ")"
            );
        }

        return result;
    }
}
