using EFT;
using EFT.InventoryLogic;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Static helper for computing approximate free inventory space.
    /// Used by BotEntityBridge.SyncQuestState to populate InventorySpaceFree on BotEntity.
    /// </summary>
    public static class InventorySpaceHelper
    {
        /// <summary>
        /// Compute approximate free grid slots across backpack + tactical vest.
        /// Returns total (GridWidth * GridHeight) minus item count for each grid.
        /// This is an approximation — items may occupy multiple cells.
        /// </summary>
        public static float ComputeFreeSlots(BotOwner botOwner)
        {
            var inventory = botOwner?.GetPlayer?.InventoryController?.Inventory;
            if (inventory?.Equipment == null)
                return 0f;

            float freeSlots = 0f;
            freeSlots += CountFreeInSlot(inventory.Equipment, EquipmentSlot.Backpack);
            freeSlots += CountFreeInSlot(inventory.Equipment, EquipmentSlot.TacticalVest);
            return freeSlots;
        }

        private static float CountFreeInSlot(InventoryEquipment equipment, EquipmentSlot slot)
        {
            var contained = equipment.GetSlot(slot)?.ContainedItem;
            if (contained is SearchableItemItemClass searchable)
            {
                float free = 0f;
                foreach (var grid in searchable.Grids)
                {
                    int total = grid.GridWidth * grid.GridHeight;
                    int used = 0;
                    foreach (var _ in grid.Items)
                        used++;
                    free += total - used;
                }
                return free;
            }
            return 0f;
        }
    }
}
