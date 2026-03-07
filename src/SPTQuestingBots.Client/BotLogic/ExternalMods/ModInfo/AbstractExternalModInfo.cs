using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using EFT;
using SPTQuestingBots.BotLogic.ExternalMods.Functions.Extract;
using SPTQuestingBots.BotLogic.ExternalMods.Functions.Hearing;
using SPTQuestingBots.BotLogic.ExternalMods.Functions.Loot;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.BotLogic.ExternalMods.ModInfo
{
    public abstract class AbstractExternalModInfo
    {
        public abstract string GUID { get; }

        public virtual Version MinCompatibleVersion => new Version("0.0.0");
        public virtual Version MaxCompatibleVersion => new Version("9999.9999.9999");

        public bool IsInstalled { get; private set; } = false;
        public PluginInfo PluginInfo { get; private set; } = null;
        public bool CompatibilitySatisfied { get; private set; } = true;

        public virtual string IncompatibilityMessage => "";

        public virtual bool IsCompatible() => IsVersionCompatible();

        public virtual bool UsesInterop => false;
        public virtual bool IsInteropRequiredForCurrentConfig => false;
        public virtual bool CanUseInterop { get; protected set; } = false;
        public virtual string InteropStatusMessage { get; protected set; } = "Interop not applicable";

        public virtual bool CheckInteropAvailability()
        {
            return SetInteropAvailability(false, "Interop not available");
        }

        public virtual string GetConfiguredFeatureDescription() => "compatibility-only detection";

        public virtual string GetFallbackBehaviorDescription() => "using QuestingBots default behavior";

        public virtual string GetInteropUnavailableMessage() =>
            GetDisplayName() + " detected, but QuestingBots could not initialize its interoperability layer.";

        private bool checkedIfInstalled = false;

        public virtual AbstractExtractFunction CreateExtractFunction(BotOwner _botOwner) => new InternalExtractFunction(_botOwner);

        public virtual AbstractHearingFunction CreateHearingFunction(BotOwner _botOwner) => new InternalHearingFunction(_botOwner);

        public virtual AbstractLootFunction CreateLootFunction(BotOwner _botOwner) => new InternalLootFunction(_botOwner);

        public bool CheckIfInstalled()
        {
            checkedIfInstalled = true;
            IsInstalled = false;
            PluginInfo = null;
            CanUseInterop = false;
            CompatibilitySatisfied = true;
            InteropStatusMessage = UsesInterop ? "Plugin not detected" : "Plugin not applicable";

            IEnumerable<PluginInfo> matchingPlugins = Chainloader
                .PluginInfos.Where(p => p.Value.Metadata.GUID == GUID)
                .Select(p => p.Value);

            if (!matchingPlugins.Any())
            {
                return false;
            }

            if (matchingPlugins.Count() > 1)
            {
                LoggingController.LogError("Found multiple instances of plugins with GUID " + GUID + ". Interoperability disabled.");
                InteropStatusMessage = "Multiple plugin instances detected";
                return false;
            }

            PluginInfo = matchingPlugins.First();
            IsInstalled = true;

            return IsInstalled;
        }

        public void RecordCompatibilityResult(bool isCompatible)
        {
            CompatibilitySatisfied = isCompatible;
        }

        protected bool SetInteropAvailability(bool canUseInterop, string statusMessage)
        {
            CanUseInterop = canUseInterop;
            InteropStatusMessage = statusMessage;
            return CanUseInterop;
        }

        public bool IsVersionCompatible()
        {
            Version actualVersion = GetVersion();
            if (actualVersion == null)
            {
                return true;
            }

            return actualVersion.IsCompatible(MinCompatibleVersion, MaxCompatibleVersion);
        }

        public Version GetVersion()
        {
            if (!checkedIfInstalled)
            {
                CheckIfInstalled();
            }

            return PluginInfo?.Metadata?.Version;
        }

        public string GetName()
        {
            if (!checkedIfInstalled)
            {
                CheckIfInstalled();
            }

            return PluginInfo?.Metadata?.Name;
        }

        public string GetDisplayName() => GetName() ?? GUID;

        public string GetVersionText() => GetVersion()?.ToString() ?? "unknown";

        public string BuildStartupSummary(bool isCompatible)
        {
            string interopState;
            if (!UsesInterop)
            {
                interopState = "n/a";
            }
            else if (!isCompatible)
            {
                interopState = "skipped";
            }
            else
            {
                interopState = CanUseInterop ? "initialized" : "unavailable";
            }

            return GetDisplayName()
                + " v"
                + GetVersionText()
                + ": compatible="
                + (isCompatible ? "yes" : "no")
                + ", interop="
                + interopState
                + " ("
                + InteropStatusMessage
                + ")"
                + ", feature="
                + GetConfiguredFeatureDescription()
                + ", fallback="
                + GetFallbackBehaviorDescription();
        }
    }
}
