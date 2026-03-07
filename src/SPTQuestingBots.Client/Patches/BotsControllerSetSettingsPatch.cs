using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using SPT.Reflection.Patching;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;
using SPTQuestingBots.Telemetry;

namespace SPTQuestingBots.Patches
{
    public class BotsControllerSetSettingsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotsController).GetMethod(nameof(BotsController.SetSettings), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        protected static void PatchPostfix()
        {
            // Re-initialize the dedicated log file for this raid (DisposeFileLogger closes it at raid end)
            LoggingController.InitFileLogger();

            if (Singleton<GameWorld>.Instance.gameObject.TryGetComponent(out Components.LocationData oldLocationData))
            {
                LoggingController.LogError("There is already a LocationData component added to the current GameWorld instance.");
                return;
            }

            Singleton<GameWorld>.Instance.gameObject.GetOrAddComponent<Components.LocationData>();

            initializeTelemetry();

            if (ConfigController.Config.BotSpawns.Enabled && ConfigController.Config.BotSpawns.DelayGameStartUntilBotGenFinishes)
            {
                Spawning.GameStartPatch.ClearMissedWaves();
                Spawning.GameStartPatch.IsDelayingGameStart = true;

                LoggingController.LogInfo("Delaying the game start until bot generation finishes...");
            }
        }

        private static void initializeTelemetry()
        {
            try
            {
                var locationData = Singleton<GameWorld>.Instance?.GetComponent<Components.LocationData>();
                if (locationData?.CurrentLocation == null)
                    return;

                string raidId = System.Guid.NewGuid().ToString("N");
                string mapId = locationData.CurrentLocation.Id;
                float escapeTimeSec = locationData.CurrentLocation.EscapeTimeLimit * 60f;
                string playerSide = Singleton<GameWorld>.Instance?.MainPlayer?.Side.ToString() ?? "Unknown";
                bool isScavRaid = RaidHelpers.IsScavRun;
                string modVersion = QuestingBotsPlugin.Instance?.Info?.Metadata?.Version?.ToString() ?? "unknown";

                TelemetryRecorder.Initialize(
                    ConfigController.Config.Telemetry,
                    raidId,
                    mapId,
                    escapeTimeSec,
                    playerSide,
                    isScavRaid,
                    modVersion
                );
            }
            catch (Exception ex)
            {
                LoggingController.LogError("[Telemetry] Failed to initialize: " + ex.Message);
            }
        }
    }
}
