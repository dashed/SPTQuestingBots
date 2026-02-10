using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Static helper for interacting with <see cref="LootableContainer"/> objects.
    /// Wraps BSG container APIs following the LootingBots LootUtils.InteractContainer pattern.
    /// </summary>
    public static class ContainerInteractionHelper
    {
        /// <summary>
        /// Checks if a container can be accessed for looting.
        /// </summary>
        public static bool IsContainerAccessible(LootableContainer container)
        {
            if (container == null)
                return false;
            if (!container.isActiveAndEnabled)
                return false;
            if (container.DoorState == EDoorState.Locked)
                return false;
            if (container.ItemOwner == null)
                return false;

            return true;
        }

        /// <summary>
        /// Opens a container by calling container.Interact with EInteractionType.Open.
        /// Matches the LootingBots LootUtils.InteractContainer pattern.
        /// </summary>
        public static void OpenContainer(LootableContainer container)
        {
            if (container == null)
                return;

            if (container.DoorState == EDoorState.Shut)
            {
                InteractionResult result = new InteractionResult(EInteractionType.Open);
                container.Interact(result);
            }
        }

        /// <summary>
        /// Closes a container by calling container.Interact with EInteractionType.Close.
        /// </summary>
        public static void CloseContainer(LootableContainer container)
        {
            if (container == null)
                return;

            if (container.DoorState == EDoorState.Open)
            {
                InteractionResult result = new InteractionResult(EInteractionType.Close);
                container.Interact(result);
            }
        }

        /// <summary>
        /// Gets the first-level items from a container's root item.
        /// Returns an empty list if the container or its contents are null.
        /// </summary>
        public static List<Item> GetContainerItems(LootableContainer container)
        {
            var items = new List<Item>();

            if (container?.ItemOwner?.RootItem == null)
                return items;

            var rootItem = container.ItemOwner.RootItem as SearchableItemItemClass;
            if (rootItem == null)
                return items;

            foreach (var item in rootItem.GetFirstLevelItems())
            {
                // Skip the root item itself
                if (item.Id == rootItem.Id)
                    continue;

                items.Add(item);
            }

            return items;
        }
    }
}
