using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EFT;
using EFT.InputSystem;
using SPT.Reflection.Patching;
using SPTQuestingBots.BotLogic.ExternalMods;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Patches
{
    public class TarkovInitPatch : ModulePatch
    {
        public static string MinVersion { get; set; } = "0.0.0.0";
        public static string MaxVersion { get; set; } = "999999.999999.999999.999999";

        protected override MethodBase GetTargetMethod()
        {
            return typeof(TarkovApplication).GetMethod(nameof(TarkovApplication.Init), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        protected static void PatchPostfix(IAssetsManager assetsManager, InputTree inputTree)
        {
            ExternalModHandler.CheckForExternalMods();

            addQuestingBotsBrainLayers();
        }

        public static bool IsCurrentVersionSupported(out string currentVersion)
        {
            return Helpers.GameCompatibilityCheckHelper.IsSPTWithinVersionRange(MinVersion, MaxVersion, out currentVersion);
        }

        public static string BuildVersionErrorMessage(string modName, string currentVersion)
        {
            string errorMessage = "Could not load " + modName + " because it requires SPT ";

            if (MinVersion == MaxVersion)
            {
                errorMessage += MinVersion;
            }
            else if (MaxVersion == "999999.999999.999999.999999")
            {
                errorMessage += MinVersion + " or later";
            }
            else if (MinVersion == "0.0.0.0")
            {
                errorMessage += MaxVersion + " or older";
            }
            else
            {
                errorMessage += "between versions " + MinVersion + " and " + MaxVersion;
            }

            errorMessage += ". The current version is " + (currentVersion ?? "unknown") + ".";

            return errorMessage;
        }

        private static void addQuestingBotsBrainLayers()
        {
            if (!ConfigController.Config.Enabled)
            {
                return;
            }

            if (ExternalModHandler.SAINModInfo.IsInstalled)
            {
                LoggingController.LogInfo("SAIN detected. Adjusting Questing Bots brain layer priorities...");
                BotBrainHelpers.AddQuestingBotsBrainLayers(ConfigController.Config.Questing.BrainLayerPriorities.WithSAIN);
            }
            else
            {
                BotBrainHelpers.AddQuestingBotsBrainLayers(ConfigController.Config.Questing.BrainLayerPriorities.WithoutSAIN);
            }
        }
    }
}
