using System;
using System.Reflection;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Centralized reflection helpers with validation and error logging.
    ///
    /// BSG's deobfuscator uses inconsistent naming conventions:
    /// <list type="bullet">
    ///   <item><b>GClass / abstract types</b>: PascalCase fields (e.g. <c>Vector3_0</c>, <c>List_0</c>, <c>Boss_1</c>)</item>
    ///   <item><b>Named EFT namespace types</b>: camelCase fields (e.g. <c>float_2</c>, <c>wavesSpawnScenario_0</c>)</item>
    /// </list>
    ///
    /// All lookups are case-sensitive and return <c>null</c> silently on mismatch.
    /// This helper logs errors loudly so field renames after game updates are caught immediately.
    /// </summary>
    public static class ReflectionHelper
    {
        /// <summary>
        /// Known reflection field lookups. Validated at startup by <see cref="ValidateAllReflectionFields"/>.
        /// Each entry: (target type, field name, description of usage).
        ///
        /// Dynamic lookups (LogicLayerMonitor) and BigBrain internal fields are excluded
        /// because their target types are resolved at runtime.
        /// </summary>
        private static readonly (Type Type, string FieldName, string Context)[] KnownFields = new[]
        {
            // AccessTools.Field lookups
            (typeof(BotCurrentPathAbstractClass), "Vector3_0", "BotPathingHelpers — path corner points"),
            (typeof(NonWavesSpawnScenario), "float_2", "TrySpawnFreeAndDelayPatch — retry time delay"),
            (typeof(LocalGame), "wavesSpawnScenario_0", "GameStartPatch — waves spawn scenario"),
            (typeof(BotsGroup), "<BotZone>k__BackingField", "GoToPositionAbstractAction — bot zone"),
            // Harmony ___param field injections
            (typeof(BossGroup), "Boss_1", "SetNewBossPatch ___Boss_1"),
            (typeof(BotSpawner), "Bots", "BotDiedPatch ___Bots"),
            (typeof(BotSpawner), "OnBotRemoved", "BotDiedPatch ___OnBotRemoved"),
            (typeof(BotSpawner), "AllPlayers", "GetAllBossPlayersPatch ___AllPlayers"),
            (typeof(AirdropLogicClass), "AirdropSynchronizableObject_0", "AirdropLandPatch ___AirdropSynchronizableObject_0"),
            (typeof(LighthouseTraderZone), "physicsTriggerHandler_0", "LighthouseTraderZone patches ___physicsTriggerHandler_0"),
        };

        /// <summary>
        /// Drop-in replacement for <see cref="AccessTools.Field(Type, string)"/> that logs an
        /// error when the field is not found instead of silently returning <c>null</c>.
        /// </summary>
        /// <param name="type">The type to search for the field on.</param>
        /// <param name="fieldName">The exact field name (case-sensitive).</param>
        /// <param name="context">Human-readable description for error messages.</param>
        /// <returns>The <see cref="FieldInfo"/>, or <c>null</c> if not found.</returns>
        public static FieldInfo RequireField(Type type, string fieldName, string context = null)
        {
            FieldInfo field = AccessTools.Field(type, fieldName);
            if (field == null)
            {
                string msg = "Obfuscated field '" + fieldName + "' not found on " + type.FullName + ".";
                if (context != null)
                {
                    msg += " Context: " + context + ".";
                }
                msg += " This field may have been renamed in a game update.";
                LoggingController.LogError(msg);
            }
            return field;
        }

        /// <summary>
        /// Validates all entries in <see cref="KnownFields"/> against the loaded game assemblies.
        /// Call once during plugin startup (after game types are loaded).
        /// </summary>
        /// <returns>The number of validation failures (0 = all OK).</returns>
        public static int ValidateAllReflectionFields()
        {
            int failures = 0;
            foreach (var (type, fieldName, context) in KnownFields)
            {
                FieldInfo field = AccessTools.Field(type, fieldName);
                if (field == null)
                {
                    LoggingController.LogError(
                        "Reflection validation FAILED: field '"
                            + fieldName
                            + "' not found on "
                            + type.FullName
                            + " ("
                            + context
                            + "). This field was likely renamed in a game update."
                    );
                    failures++;
                }
            }

            if (failures == 0)
            {
                LoggingController.LogInfo("Reflection validation passed: all " + KnownFields.Length + " field lookups verified.");
            }
            else
            {
                LoggingController.LogError("Reflection validation: " + failures + " of " + KnownFields.Length + " field lookups FAILED.");
            }

            return failures;
        }
    }
}
