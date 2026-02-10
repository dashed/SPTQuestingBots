using System;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Static helper for gear swap operations (throw and equip).
    /// Follows the LootingBots LootingTransactionController.ThrowAndEquip pattern:
    /// InventoryController.ThrowItem with a Callback that equips the replacement.
    /// </summary>
    public static class GearSwapHelper
    {
        /// <summary>
        /// Drops an item and equips its replacement in the callback.
        /// Matches LootingBots ThrowAndEquip: ThrowItem(toDrop) -> on success -> TryEquipItem(toEquip).
        /// Returns true if the throw was initiated.
        /// </summary>
        public static bool TryThrowAndEquip(InventoryController controller, Item toDrop, Item toEquip)
        {
            try
            {
                if (controller == null || toDrop == null || toEquip == null)
                    return false;

                controller.ThrowItem(
                    toDrop,
                    false,
                    new Callback(
                        (throwResult) =>
                        {
                            if (throwResult.Succeed)
                            {
                                // In the throw callback, equip the replacement item
                                var slotAddress = controller.FindSlotToPickUp(toEquip);
                                if (slotAddress == null)
                                {
                                    LoggingController.LogWarning(
                                        "GearSwapHelper: no slot found for " + toEquip.LocalizedName() + " after throw"
                                    );
                                    return;
                                }

                                var moveResult = InteractionsHandlerClass.Move(toEquip, slotAddress, controller, true);
                                if (!moveResult.Succeeded)
                                {
                                    LoggingController.LogWarning("GearSwapHelper: equip Move failed for " + toEquip.LocalizedName());
                                    return;
                                }

                                controller.TryRunNetworkTransaction(
                                    moveResult,
                                    new Callback(
                                        (equipResult) =>
                                        {
                                            if (equipResult.Failed)
                                            {
                                                LoggingController.LogError(
                                                    "GearSwapHelper: equip transaction failed for " + toEquip.LocalizedName()
                                                );
                                            }
                                        }
                                    )
                                );
                            }
                            else
                            {
                                LoggingController.LogWarning("GearSwapHelper: throw failed for " + toDrop.LocalizedName());
                            }
                        }
                    )
                );

                return true;
            }
            catch (Exception e)
            {
                LoggingController.LogError("GearSwapHelper.TryThrowAndEquip: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Drops an item on the ground using InventoryController.ThrowItem.
        /// Returns true if the throw was initiated.
        /// </summary>
        public static bool TryDropItem(InventoryController controller, Item item)
        {
            try
            {
                if (controller == null || item == null)
                    return false;

                controller.ThrowItem(
                    item,
                    false,
                    new Callback(
                        (result) =>
                        {
                            if (result.Failed)
                            {
                                LoggingController.LogWarning("GearSwapHelper: drop failed for " + item.LocalizedName());
                            }
                        }
                    )
                );

                return true;
            }
            catch (Exception e)
            {
                LoggingController.LogError("GearSwapHelper.TryDropItem: " + e.Message);
                return false;
            }
        }
    }
}
