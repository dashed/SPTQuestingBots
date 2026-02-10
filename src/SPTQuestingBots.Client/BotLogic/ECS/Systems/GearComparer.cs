using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
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
        public static bool IsArmorUpgrade(int myArmorClass, int candidateArmorClass) => candidateArmorClass > myArmorClass;

        /// <summary>
        /// Returns true if the candidate weapon value is strictly higher.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWeaponUpgrade(int myWeaponValue, int candidateWeaponValue) => candidateWeaponValue > myWeaponValue;

        /// <summary>
        /// Returns true if the candidate container (backpack) grid size is strictly larger.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsContainerUpgrade(int myGridSize, int candidateGridSize) => candidateGridSize > myGridSize;

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
            // Candidate is armored, current is not: check if armor class is acceptable
            if (candidateIsArmored && !myRigIsArmored)
                return candidateArmorClass >= myArmorClass && candidateRigGridSize >= myRigGridSize;

            // Both armored: prefer higher armor class, or same class with larger size
            if (candidateIsArmored && myRigIsArmored)
                return (candidateArmorClass > myArmorClass)
                    || (candidateArmorClass == myArmorClass && candidateRigGridSize > myRigGridSize);

            // Non-armored: just compare size
            return candidateRigGridSize > myRigGridSize;
        }
    }
}
