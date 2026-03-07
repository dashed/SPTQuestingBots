using System.Collections.Generic;
using System.Linq;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SPTQuestingBots.BotLogic.ExternalMods.Functions.Extract;
using SPTQuestingBots.BotLogic.ExternalMods.Functions.Hearing;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ExternalMods.ModInfo
{
    public class SAINModInfo : AbstractExternalModInfo
    {
        public override string GUID { get; } = "me.sol.sain";
        public override bool UsesInterop => true;
        public override bool IsInteropRequiredForCurrentConfig => shouldUseSainForExtracting();

        private Dictionary<string, int> minimumBrainLayerPrioritiesForBrains = new Dictionary<string, int>();
        private Dictionary<string, MinMaxConfig> searchTimeAfterCombatForBrains = new Dictionary<string, MinMaxConfig>();

        public static bool IsSAINLayer(string layerName) => layerName.StartsWith("SAIN");

        public override bool CheckInteropAvailability()
        {
            if (SAIN.Plugin.SAINInterop.Init())
            {
                return SetInteropAvailability(true, "Resolved " + SAIN.Plugin.SAINInterop.ExternalTypeName);
            }

            return SetInteropAvailability(false, SAIN.Plugin.SAINInterop.LastInitError);
        }

        public override AbstractExtractFunction CreateExtractFunction(BotOwner _botOwner)
        {
            if (!CanUseInterop || !shouldUseSainForExtracting())
            {
                return base.CreateExtractFunction(_botOwner);
            }

            return new SAINExtractFunction(_botOwner);
        }

        public override AbstractHearingFunction CreateHearingFunction(BotOwner _botOwner)
        {
            if (!CanUseInterop)
            {
                return base.CreateHearingFunction(_botOwner);
            }

            return new SAINHearingFunction(_botOwner);
        }

        public override string GetConfiguredFeatureDescription() =>
            shouldUseSainForExtracting() ? "SAIN extraction integration enabled" : "SAIN extraction integration disabled in config";

        public override string GetFallbackBehaviorDescription()
        {
            if (CanUseInterop)
            {
                return shouldUseSainForExtracting()
                    ? "using SAIN extraction and hearing integration"
                    : "using QuestingBots extraction while keeping SAIN hearing integration";
            }

            return "using QuestingBots extraction and hearing behavior";
        }

        public override string GetInteropUnavailableMessage()
        {
            string message =
                GetDisplayName()
                + " detected, but QuestingBots could not initialize SAIN interop. "
                + "Reason: "
                + InteropStatusMessage
                + ".";

            if (shouldUseSainForExtracting())
            {
                return message + " Falling back to QuestingBots extraction behavior.";
            }

            return message + " QuestingBots will continue without SAIN extraction or hearing integration.";
        }

        public MinMaxConfig GetSearchTimeAfterCombat(string brainName)
        {
            if (searchTimeAfterCombatForBrains.TryGetValue(brainName, out MinMaxConfig searchTime))
            {
                return searchTime;
            }

            MinMaxConfig minMax = ConfigController.Config.Questing.BotQuestingRequirements.SearchTimeAfterCombat.PrioritizedQuesting;
            if (GetMinimumLayerPriority(brainName) > ConfigController.Config.Questing.BrainLayerPriorities.WithSAIN.Questing)
            {
                minMax = ConfigController.Config.Questing.BotQuestingRequirements.SearchTimeAfterCombat.PrioritizedSAIN;
            }

            searchTimeAfterCombatForBrains.Add(brainName, minMax);
            return minMax;
        }

        public int GetMinimumLayerPriority(string brainName)
        {
            if (minimumBrainLayerPrioritiesForBrains.TryGetValue(brainName, out int minimumPriority))
            {
                return minimumPriority;
            }

            minimumPriority = findMinimumLayerPriority(brainName);
            minimumBrainLayerPrioritiesForBrains.Add(brainName, minimumPriority);

            return minimumPriority;
        }

        private int findMinimumLayerPriority(string brainName)
        {
            if (!CanUseInterop)
            {
                return -1;
            }

            IEnumerable<int> sainBrainLayerPrioritiesForBotRole = BrainManager
                .CustomLayersReadOnly.Where(l => l.Value.customLayerType.FullName.StartsWith("SAIN."))
                .Where(l => l.Value.CustomLayerBrains.Contains(brainName))
                .Select(i => i.Value.customLayerPriority);

            if (sainBrainLayerPrioritiesForBotRole.Any())
            {
                return sainBrainLayerPrioritiesForBotRole.Min();
            }
            else
            {
                LoggingController.LogWarning("No SAIN brain layers found for brain type " + brainName);
            }

            return -1;
        }

        private static bool shouldUseSainForExtracting() =>
            ConfigController.Config?.Questing?.ExtractionRequirements?.UseSAINForExtracting == true;
    }
}
