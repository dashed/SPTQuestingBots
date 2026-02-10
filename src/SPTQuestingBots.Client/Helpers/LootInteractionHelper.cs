using System;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Static wrapper around BSG inventory operations for picking up, equipping,
    /// and merging items. Follows the InteractionsHandlerClass.Move + TryRunNetworkTransaction
    /// pattern established in <see cref="ItemHelpers.TryTransferItem"/>.
    /// </summary>
    public static class LootInteractionHelper
    {
        /// <summary>
        /// Attempts to pick up a loose item by finding a grid slot and executing
        /// the move transaction. Returns true if the move was initiated.
        /// </summary>
        public static bool TryPickupItem(InventoryController controller, Item item)
        {
            try
            {
                // FindGridToPickUp searches all equipped containers for a free grid slot
                var gridAddress = controller.FindGridToPickUp(item);
                if (gridAddress == null)
                {
                    LoggingController.LogDebug("LootInteractionHelper: no grid slot found for " + item.LocalizedName());
                    return false;
                }

                var moveResult = InteractionsHandlerClass.Move(item, gridAddress, controller, true);
                if (!moveResult.Succeeded)
                {
                    LoggingController.LogWarning("LootInteractionHelper: Move failed for " + item.LocalizedName());
                    return false;
                }

                controller.TryRunNetworkTransaction(
                    moveResult,
                    new Callback(
                        (result) =>
                        {
                            if (result.Failed)
                            {
                                LoggingController.LogError("LootInteractionHelper: pickup transaction failed for " + item.LocalizedName());
                            }
                        }
                    )
                );

                return true;
            }
            catch (Exception e)
            {
                LoggingController.LogError("LootInteractionHelper.TryPickupItem: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Attempts to equip an item to an appropriate equipment slot.
        /// Returns true if the equip was initiated.
        /// </summary>
        public static bool TryEquipItem(InventoryController controller, Item item)
        {
            try
            {
                var slotAddress = controller.FindSlotToPickUp(item);
                if (slotAddress == null)
                {
                    LoggingController.LogDebug("LootInteractionHelper: no equipment slot found for " + item.LocalizedName());
                    return false;
                }

                var moveResult = InteractionsHandlerClass.Move(item, slotAddress, controller, true);
                if (!moveResult.Succeeded)
                {
                    LoggingController.LogWarning("LootInteractionHelper: equip Move failed for " + item.LocalizedName());
                    return false;
                }

                controller.TryRunNetworkTransaction(
                    moveResult,
                    new Callback(
                        (result) =>
                        {
                            if (result.Failed)
                            {
                                LoggingController.LogError("LootInteractionHelper: equip transaction failed for " + item.LocalizedName());
                            }
                        }
                    )
                );

                return true;
            }
            catch (Exception e)
            {
                LoggingController.LogError("LootInteractionHelper.TryEquipItem: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Attempts to merge a stackable item with an existing item of the same type.
        /// Uses InteractionsHandlerClass.Merge following the LootingBots pattern.
        /// Returns true if the merge was initiated.
        /// </summary>
        public static bool TryMergeItem(InventoryController controller, Item sourceItem, Item targetItem)
        {
            try
            {
                var mergeResult = InteractionsHandlerClass.Merge(sourceItem, targetItem, controller, true);
                if (!mergeResult.Succeeded)
                {
                    LoggingController.LogWarning("LootInteractionHelper: Merge failed for " + sourceItem.LocalizedName());
                    return false;
                }

                controller.TryRunNetworkTransaction(
                    mergeResult,
                    new Callback(
                        (result) =>
                        {
                            if (result.Failed)
                            {
                                LoggingController.LogError(
                                    "LootInteractionHelper: merge transaction failed for " + sourceItem.LocalizedName()
                                );
                            }
                        }
                    )
                );

                return true;
            }
            catch (Exception e)
            {
                LoggingController.LogError("LootInteractionHelper.TryMergeItem: " + e.Message);
                return false;
            }
        }
    }
}
