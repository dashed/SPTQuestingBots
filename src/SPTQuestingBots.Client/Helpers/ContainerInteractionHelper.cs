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
            {
                LoggingController.LogWarning("[ContainerInteractionHelper] IsContainerAccessible: container is null");
                return false;
            }
            if (!container.isActiveAndEnabled)
            {
                LoggingController.LogDebug("[ContainerInteractionHelper] Container " + container.GetInstanceID() + " is not active");
                return false;
            }
            if (container.DoorState == EDoorState.Locked)
            {
                LoggingController.LogDebug("[ContainerInteractionHelper] Container " + container.GetInstanceID() + " is locked");
                return false;
            }
            if (container.ItemOwner == null)
            {
                LoggingController.LogWarning("[ContainerInteractionHelper] Container " + container.GetInstanceID() + " has null ItemOwner");
                return false;
            }

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
                LoggingController.LogInfo("[ContainerInteractionHelper] Opening container " + container.GetInstanceID());
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
                LoggingController.LogDebug("[ContainerInteractionHelper] Closing container " + container.GetInstanceID());
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
            {
                LoggingController.LogWarning("[ContainerInteractionHelper] GetContainerItems: null root item");
                return items;
            }

            var rootItem = container.ItemOwner.RootItem as SearchableItemItemClass;
            if (rootItem == null)
            {
                LoggingController.LogWarning("[ContainerInteractionHelper] GetContainerItems: root item is not SearchableItemItemClass");
                return items;
            }

            foreach (var item in rootItem.GetFirstLevelItems())
            {
                // Skip the root item itself
                if (item.Id == rootItem.Id)
                    continue;

                items.Add(item);
            }

            LoggingController.LogDebug(
                "[ContainerInteractionHelper] Container " + container.GetInstanceID() + ": found " + items.Count + " items"
            );

            return items;
        }
    }
}
