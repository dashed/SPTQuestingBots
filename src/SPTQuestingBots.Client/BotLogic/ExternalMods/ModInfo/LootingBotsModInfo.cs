using EFT;
using SPTQuestingBots.BotLogic.ExternalMods.Functions.Loot;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ExternalMods.ModInfo
{
    public class LootingBotsModInfo : AbstractExternalModInfo
    {
        public override string GUID { get; } = "me.skwizzy.lootingbots";
        public override bool UsesInterop => true;
        public override bool IsInteropRequiredForCurrentConfig => shouldDisableNativeLootingWhenLootingBotsDetected();

        public override bool CheckInteropAvailability()
        {
            if (LootingBots.LootingBotsInterop.Init())
            {
                return SetInteropAvailability(true, "Resolved " + LootingBots.LootingBotsInterop.ExternalTypeName);
            }

            return SetInteropAvailability(false, LootingBots.LootingBotsInterop.LastInitError);
        }

        public override AbstractLootFunction CreateLootFunction(BotOwner _botOwner)
        {
            if (!ShouldUseExternalLooting())
            {
                return base.CreateLootFunction(_botOwner);
            }

            return new LootingBotsLootFunction(_botOwner);
        }

        public override string GetConfiguredFeatureDescription() =>
            shouldDisableNativeLootingWhenLootingBotsDetected()
                ? "native looting disabled when LootingBots is healthy"
                : "native looting kept enabled alongside LootingBots";

        public override string GetFallbackBehaviorDescription()
        {
            if (CanUseInterop)
            {
                return shouldDisableNativeLootingWhenLootingBotsDetected()
                    ? "using LootingBots interoperability and disabling native looting"
                    : "using LootingBots interoperability while retaining native looting";
            }

            return "using QuestingBots native looting because LootingBots interop is unavailable";
        }

        public override string GetInteropUnavailableMessage() =>
            GetDisplayName()
            + " detected, but QuestingBots could not initialize LootingBots interop. "
            + "Reason: "
            + InteropStatusMessage
            + ". Falling back to QuestingBots native looting behavior.";

        public bool ShouldUseExternalLooting()
        {
            return CompatibilitySatisfied && CanUseInterop && shouldDisableNativeLootingWhenLootingBotsDetected();
        }

        private static bool shouldDisableNativeLootingWhenLootingBotsDetected() =>
            ConfigController.Config?.Questing?.Looting?.DisableWhenLootingBotsDetected == true;
    }
}
