using System.Collections.Generic;
using BepInEx.Bootstrap;
using EFT;
using SPTQuestingBots.BotLogic.ExternalMods.Functions.Extract;
using SPTQuestingBots.BotLogic.ExternalMods.Functions.Hearing;
using SPTQuestingBots.BotLogic.ExternalMods.Functions.Loot;
using SPTQuestingBots.BotLogic.ExternalMods.ModInfo;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ExternalMods
{
    public static class ExternalModHandler
    {
        public static SAINModInfo SAINModInfo { get; private set; } = new SAINModInfo();
        public static LootingBotsModInfo LootingBotsModInfo { get; private set; } = new LootingBotsModInfo();
        public static DonutsModInfo DonutsModInfo { get; private set; } = new DonutsModInfo();
        public static PerformanceImprovementsModInfo PerformanceImprovementsModInfo { get; private set; } =
            new PerformanceImprovementsModInfo();
        public static PleaseJustFightModInfo PleaseJustFightModInfo { get; private set; } = new PleaseJustFightModInfo();

        private static readonly List<AbstractExternalModInfo> externalMods = new List<AbstractExternalModInfo>
        {
            SAINModInfo,
            LootingBotsModInfo,
            DonutsModInfo,
            PerformanceImprovementsModInfo,
            PleaseJustFightModInfo,
        };

        public static AbstractExtractFunction CreateExtractFunction(this BotOwner _botOwner) =>
            SAINModInfo.CreateExtractFunction(_botOwner);

        public static AbstractHearingFunction CreateHearingFunction(this BotOwner _botOwner) =>
            SAINModInfo.CreateHearingFunction(_botOwner);

        public static AbstractLootFunction CreateLootFunction(this BotOwner _botOwner) => LootingBotsModInfo.CreateLootFunction(_botOwner);

        public static int GetMinimumCombatLayerPriority(string _brainName) => SAINModInfo.GetMinimumLayerPriority(_brainName);

        public static MinMaxConfig GetSearchTimeAfterCombat(string _brainName) => SAINModInfo.GetSearchTimeAfterCombat(_brainName);

        /// <summary>
        /// Returns <c>true</c> when the native (internal) looting system should be active.
        /// This is the case when LootingBots is not installed, or when the config
        /// explicitly opts in to native looting even with LootingBots present.
        /// </summary>
        public static bool IsNativeLootingEnabled()
        {
            if (!LootingBotsModInfo.IsInstalled)
                return true;

            return !LootingBotsModInfo.ShouldUseExternalLooting();
        }

        public static void CheckForExternalMods()
        {
            if (!ConfigController.Config.Enabled)
            {
                return;
            }

            var installedMods = new List<AbstractExternalModInfo>();

            foreach (AbstractExternalModInfo modInfo in externalMods)
            {
                if (!modInfo.CheckIfInstalled())
                {
                    continue;
                }

                installedMods.Add(modInfo);
                LoggingController.LogInfo($"Found external mod {modInfo.GetDisplayName()} (version {modInfo.GetVersionText()})");

                bool isCompatible = modInfo.IsCompatible();
                modInfo.RecordCompatibilityResult(isCompatible);

                if (!isCompatible)
                {
                    LoggingController.LogError(modInfo.IncompatibilityMessage);
                    addDependencyError(modInfo.IncompatibilityMessage);
                    continue;
                }

                if (modInfo.UsesInterop && !modInfo.CheckInteropAvailability())
                {
                    string interopMessage = modInfo.GetInteropUnavailableMessage();

                    if (modInfo.IsInteropRequiredForCurrentConfig)
                    {
                        LoggingController.LogError(interopMessage);
                        addDependencyError(interopMessage);
                    }
                    else
                    {
                        LoggingController.LogWarning(interopMessage);
                    }
                }
            }

            logStartupCompatibilitySummary(installedMods);
        }

        private static void logStartupCompatibilitySummary(IReadOnlyCollection<AbstractExternalModInfo> installedMods)
        {
            if (installedMods.Count == 0)
            {
                LoggingController.LogInfo("External mod compatibility summary: no supported external mods detected.", true);
                return;
            }

            LoggingController.LogInfo("External mod compatibility summary:", true);

            foreach (AbstractExternalModInfo modInfo in installedMods)
            {
                LoggingController.LogInfo(" - " + modInfo.BuildStartupSummary(modInfo.CompatibilitySatisfied), true);
            }
        }

        private static void addDependencyError(string message)
        {
            if (!Chainloader.DependencyErrors.Contains(message))
            {
                Chainloader.DependencyErrors.Add(message);
            }
        }
    }
}
