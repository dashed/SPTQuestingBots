using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Static helper for accessing corpse inventories and extracting lootable items.
    /// Follows the LootingBots LootingBrain.LootCorpse + LootUtils.GetPrioritySlots pattern.
    /// </summary>
    public static class CorpseSearchHelper
    {
        /// <summary>
        /// Equipment slot priority for weapons (looted first when corpse has backpack/rig).
        /// </summary>
        private static readonly EquipmentSlot[] WeaponSlots =
        {
            EquipmentSlot.Holster,
            EquipmentSlot.FirstPrimaryWeapon,
            EquipmentSlot.SecondPrimaryWeapon,
        };

        /// <summary>
        /// Equipment slot priority for storage containers.
        /// </summary>
        private static readonly EquipmentSlot[] StorageSlots =
        {
            EquipmentSlot.Backpack,
            EquipmentSlot.ArmorVest,
            EquipmentSlot.TacticalVest,
            EquipmentSlot.Pockets,
        };

        /// <summary>
        /// Other equipment slots to check last.
        /// </summary>
        private static readonly EquipmentSlot[] OtherSlots =
        {
            EquipmentSlot.Headwear,
            EquipmentSlot.Earpiece,
            EquipmentSlot.Dogtag,
            EquipmentSlot.Scabbard,
            EquipmentSlot.FaceCover,
        };

        /// <summary>
        /// Checks if a corpse Player object is valid for looting.
        /// Matches LootingBots check: corpse != null, GetPlayer != null.
        /// </summary>
        public static bool IsCorpseValid(Player corpse)
        {
            if (corpse == null)
            {
                LoggingController.LogWarning("[CorpseSearchHelper] IsCorpseValid: corpse is null");
                return false;
            }

            // GetPlayer != null distinguishes bot corpses from static "Dead scav" corpses
            if (corpse.GetPlayer == null)
            {
                LoggingController.LogDebug("[CorpseSearchHelper] Corpse " + corpse.GetInstanceID() + " has null GetPlayer (static corpse)");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the InventoryController from a corpse Player.
        /// Returns null if the corpse is invalid.
        /// </summary>
        public static InventoryController GetCorpseInventory(Player corpse)
        {
            if (!IsCorpseValid(corpse))
                return null;

            if (corpse.InventoryController == null)
            {
                LoggingController.LogWarning("[CorpseSearchHelper] Corpse " + corpse.GetInstanceID() + " has null InventoryController");
            }

            return corpse.InventoryController;
        }

        /// <summary>
        /// Gets lootable items from a corpse in priority order.
        /// Priority follows LootingBots LootUtils.GetPrioritySlots:
        /// - If corpse has backpack/rig: weapons first, then storage, then other
        /// - Otherwise: storage first, then weapons, then other
        /// Locked slots are skipped.
        /// </summary>
        public static List<Item> GetLootableItems(Player corpse)
        {
            var items = new List<Item>();

            InventoryController corpseController = GetCorpseInventory(corpse);
            if (corpseController == null)
                return items;

            var equipment = corpseController.Inventory.Equipment;
            bool hasBackpack = equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem != null;
            bool hasTacVest = equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem != null;

            var prioritySlotList = new List<EquipmentSlot>(13);

            // Add slots in priority order matching LootingBots pattern
            if (hasBackpack || hasTacVest)
            {
                AddUnlockedSlots(equipment, prioritySlotList, WeaponSlots);
                AddUnlockedSlots(equipment, prioritySlotList, StorageSlots);
            }
            else
            {
                AddUnlockedSlots(equipment, prioritySlotList, StorageSlots);
                AddUnlockedSlots(equipment, prioritySlotList, WeaponSlots);
            }

            AddUnlockedSlots(equipment, prioritySlotList, OtherSlots);

            // Extract items from priority slots
            foreach (Slot slot in equipment.GetSlotsByName(prioritySlotList))
            {
                Item item = slot.ContainedItem;
                if (item != null)
                {
                    items.Add(item);
                }
            }

            LoggingController.LogDebug(
                "[CorpseSearchHelper] Corpse "
                    + corpse.GetInstanceID()
                    + ": found "
                    + items.Count
                    + " lootable items from "
                    + prioritySlotList.Count
                    + " slots"
                    + " (backpack="
                    + hasBackpack
                    + ", tacVest="
                    + hasTacVest
                    + ")"
            );

            return items;
        }

        /// <summary>
        /// Gets items locked in slots (e.g. armor plates) that should not be looted.
        /// Matches LootingBots LootUtils.GetAllLockedItems pattern.
        /// </summary>
        public static List<Item> GetLockedItems(CompoundItem itemWithSlots)
        {
            var lockedItems = new List<Item>();

            if (itemWithSlots?.Slots == null)
                return lockedItems;

            foreach (Slot slot in itemWithSlots.Slots)
            {
                if (slot.Locked && slot.Items != null)
                {
                    foreach (Item item in slot.Items)
                    {
                        lockedItems.Add(item);
                    }
                }
            }

            if (lockedItems.Count > 0)
            {
                LoggingController.LogDebug("[CorpseSearchHelper] Found " + lockedItems.Count + " locked items to skip");
            }

            return lockedItems;
        }

        private static void AddUnlockedSlots(InventoryEquipment equipment, List<EquipmentSlot> targetList, EquipmentSlot[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!equipment.GetSlot(slots[i]).Locked)
                {
                    targetList.Add(slots[i]);
                }
            }
        }
    }
}
